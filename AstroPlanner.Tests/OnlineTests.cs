using System.Net;
using System.Text;
using AstroPlanner.Core.Interfaces;
using AstroPlanner.Core.Models;
using AstroPlanner.Core.Services;
using AstroPlanner.Data.Weather;
using AstroPlanner.UI.Services;
using AstroPlanner.UI.ViewModels;
using FluentAssertions;

namespace AstroPlanner.Tests;

/// <summary>
/// Funciones de la 0.8: pronóstico de nubes (Open-Meteo) y aviso de versión
/// nueva (GitHub), sin red: las respuestas HTTP son simuladas.
/// </summary>
public sealed class OnlineTests
{
    // 2026-01-16 04:00 UTC = 2026-01-15 22:00 en CDMX (UTC-6).
    private static readonly DateTimeOffset WinterNightUtc = new(2026, 1, 16, 4, 0, 0, TimeSpan.Zero);

    // ─── Dobles de prueba ───────────────────────────────────────────────

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public List<Uri> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request.RequestUri!);
            return Task.FromResult(respond(request));
        }
    }

    private static HttpResponseMessage Json(string json, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    /// <summary>Respuesta de Open-Meteo con 48 horas desde el 15/1/2026 00:00 UTC.</summary>
    private static string OpenMeteoJson(Func<int, int> cloudAtHour)
    {
        var start = new DateTime(2026, 1, 15, 0, 0, 0);
        var times = Enumerable.Range(0, 48).Select(i => $"\"{start.AddHours(i):yyyy-MM-dd'T'HH:mm}\"");
        var clouds = Enumerable.Range(0, 48).Select(i => cloudAtHour(i).ToString());
        return "{\"latitude\":19.43,\"longitude\":-99.13,\"timezone\":\"GMT\",\"hourly\":{"
            + $"\"time\":[{string.Join(",", times)}],"
            + $"\"cloud_cover\":[{string.Join(",", clouds)}],"
            + $"\"cloud_cover_low\":[{string.Join(",", Enumerable.Repeat("null", 48))}],"
            + $"\"precipitation_probability\":[{string.Join(",", Enumerable.Repeat("10", 48))}]"
            + "}}";
    }

    /// <summary>Como el servicio real, devuelve el pronóstico de las coordenadas pedidas.</summary>
    private sealed class CountingWeatherService(Func<double, double, WeatherForecast> make) : IWeatherForecastService
    {
        public int Calls { get; private set; }
        public Exception? FailWith { get; set; }

        public Task<WeatherForecast> GetForecastAsync(double latitude, double longitude, CancellationToken cancellationToken = default)
        {
            Calls++;
            return FailWith is not null
                ? Task.FromException<WeatherForecast>(FailWith)
                : Task.FromResult(make(latitude, longitude));
        }
    }

    private sealed class FixedUpdateChecker(UpdateInfo? result) : IUpdateChecker
    {
        public int Calls { get; private set; }

        public Task<UpdateInfo?> CheckAsync(Version current, CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.FromResult(result);
        }
    }

    /// <summary>Pronóstico para CDMX: despejado de 06 a 10 UTC (00-04 local), nublado el resto.</summary>
    private static WeatherForecast CdmxForecast(DateTime fetchedAtUtc, double latitude = 19.4326, double longitude = -99.1332)
    {
        var start = new DateTime(2026, 1, 15, 0, 0, 0, DateTimeKind.Utc);
        var hours = Enumerable.Range(0, 48)
            .Select(i => new HourlyWeather(start.AddHours(i), i is >= 30 and < 34 ? 5 : 80))
            .ToList();
        return new WeatherForecast(latitude, longitude, hours, fetchedAtUtc);
    }

    private static MainViewModel CreateMain(
        ManualTimeProvider clock, IWeatherForecastService? weather, IUpdateChecker? updates = null,
        ObserverSettings? settings = null, FakePlanService? plan = null) =>
        new(
            new FakeCelestialRepository([TestCatalog.M42, TestCatalog.M31, TestCatalog.M13]),
            new AstronomyService(),
            plan ?? new FakePlanService(),
            new NullLogger(),
            new SettingsViewModel(new FakeSettingsService(settings ?? new ObserverSettings()), new FakeCityRepository(), new NullLogger()),
            new LogbookViewModel(new FakeLogService(), new NullLogger()),
            clock,
            weather,
            updates);

    // ─── Open-Meteo ─────────────────────────────────────────────────────

    [Fact]
    public void OpenMeteoUrl_RoundsCoordinatesAndUsesInvariantCulture()
    {
        var url = OpenMeteoForecastService.BuildUrl(21.88534, -102.29164);

        url.Should().StartWith("https://api.open-meteo.com/v1/forecast?latitude=21.89&longitude=-102.29&")
            .And.Contain("hourly=cloud_cover").And.Contain("timezone=GMT").And.Contain("forecast_days=7");
    }

    [Fact]
    public async Task OpenMeteo_ParsesHourlyCloudCoverInUtc()
    {
        var handler = new StubHandler(_ => Json(OpenMeteoJson(i => i)));
        var service = new OpenMeteoForecastService(new HttpClient(handler), new ManualTimeProvider(WinterNightUtc));

        var forecast = await service.GetForecastAsync(19.4326, -99.1332);

        forecast.Hours.Should().HaveCount(48);
        forecast.Hours[0].TimeUtc.Should().Be(new DateTime(2026, 1, 15, 0, 0, 0, DateTimeKind.Utc));
        forecast.Hours[0].TimeUtc.Kind.Should().Be(DateTimeKind.Utc);
        forecast.At(new DateTime(2026, 1, 16, 4, 30, 0, DateTimeKind.Utc))!.CloudCoverPercent.Should().Be(28);
        forecast.Hours[0].PrecipitationProbabilityPercent.Should().Be(10);
        forecast.Hours[0].LowCloudPercent.Should().BeNull();
        forecast.FetchedAtUtc.Should().Be(WinterNightUtc.UtcDateTime);
        handler.Requests.Single().Query.Should().Contain("latitude=19.43");
    }

    [Theory]
    [InlineData("server-error")]
    [InlineData("bad-json")]
    [InlineData("incomplete")]
    [InlineData("no-network")]
    public async Task OpenMeteo_FailuresBecomeWeatherForecastException(string failure)
    {
        var handler = new StubHandler(_ => failure switch
        {
            "server-error" => Json("{}", HttpStatusCode.InternalServerError),
            "bad-json" => Json("{ no es json"),
            "incomplete" => Json("""{"hourly":{"time":["2026-01-15T00:00"]}}"""),
            _ => throw new HttpRequestException("sin red")
        });
        var service = new OpenMeteoForecastService(new HttpClient(handler));

        var act = () => service.GetForecastAsync(19.43, -99.13);

        await act.Should().ThrowAsync<WeatherForecastException>();
    }

    [Fact]
    public void Summarize_ReportsAverageAndClearestHour()
    {
        var forecast = CdmxForecast(WinterNightUtc.UtcDateTime);

        // 16/1 02:00 → 10:00 UTC: 4 horas al 80 % y 4 al 5 % (06-10 UTC).
        var summary = forecast.Summarize(
            new DateTime(2026, 1, 16, 2, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 1, 16, 10, 0, 0, DateTimeKind.Utc))!;

        summary.AverageCloudCoverPercent.Should().BeApproximately(42.5, 0.01);
        summary.MinimumCloudCoverPercent.Should().Be(5);
        summary.ClearestHourUtc.Should().Be(new DateTime(2026, 1, 16, 6, 0, 0, DateTimeKind.Utc));
        forecast.Summarize(new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 3, 1, 8, 0, 0, DateTimeKind.Utc))
            .Should().BeNull("fuera del horizonte de pronóstico");
        WeatherForecast.Describe(5).Should().Be("Despejado");
        WeatherForecast.Describe(95).Should().Be("Cubierto");
    }

    // ─── Pronóstico en el ViewModel ─────────────────────────────────────

    [Fact]
    public async Task Night_ShowsCloudSummaryAndPlanCloudsAtCulmination()
    {
        var clock = new ManualTimeProvider(WinterNightUtc);
        var weather = new CountingWeatherService((lat, lon) => CdmxForecast(clock.UtcNow.UtcDateTime, lat, lon));
        var plan = new FakePlanService { Entries = [new("M42")] };
        var viewModel = CreateMain(clock, weather, plan: plan);
        await viewModel.LoadCommand.ExecuteAsync(null);

        viewModel.CloudCoverText.Should().MatchRegex(@"^\d+ %$");
        viewModel.CloudDetailText.Should().Contain("mejor 00:00").And.Contain("(5 %)");
        viewModel.GetNightCloudCover().Should().NotBeEmpty()
            .And.OnlyContain(h => h.StartLocal >= viewModel.Night!.WindowStartLocal && h.EndLocal <= viewModel.Night.WindowEndLocal);

        var m42 = viewModel.ObservationPlan.Single();
        m42.CloudCoverAtCulmination.Should().Be(80, "M42 culmina hacia las 22:20 local (04:20 UTC), hora nublada");
        viewModel.SelectedObservation!.CloudCoverAtCulmination.Should().NotBeNull();
    }

    [Fact]
    public async Task Forecast_IsCachedForAnHourAndRefetchedWhenTheLocationChanges()
    {
        var clock = new ManualTimeProvider(WinterNightUtc);
        var weather = new CountingWeatherService((lat, lon) => CdmxForecast(clock.UtcNow.UtcDateTime, lat, lon));
        var viewModel = CreateMain(clock, weather);
        await viewModel.LoadCommand.ExecuteAsync(null);
        var callsAfterLoad = weather.Calls;

        // Cambiar la hora o filtrar no vuelve a consultar.
        viewModel.ShiftObservationTimeCommand.Execute("60");
        viewModel.SearchText = "m42";
        weather.Calls.Should().Be(callsAfterLoad);

        // Pasada una hora, sí.
        clock.UtcNow = clock.UtcNow.AddMinutes(61);
        await viewModel.RefreshWeatherAsync();
        weather.Calls.Should().Be(callsAfterLoad + 1);

        // Otra ubicación (más de ~1 km): se pide el pronóstico nuevo al recalcular,
        // y después vuelve a usarse la caché.
        viewModel.Latitude = 20.6597;
        weather.Calls.Should().Be(callsAfterLoad + 2);
        await viewModel.RefreshWeatherAsync();
        weather.Calls.Should().Be(callsAfterLoad + 2);
    }

    [Fact]
    public async Task Failure_IsShownAndNotRetriedOnEveryRecalculation()
    {
        var clock = new ManualTimeProvider(WinterNightUtc);
        var weather = new CountingWeatherService((lat, lon) => CdmxForecast(clock.UtcNow.UtcDateTime, lat, lon))
        {
            FailWith = new WeatherForecastException("Sin conexión con el servicio del tiempo.")
        };
        var viewModel = CreateMain(clock, weather);
        await viewModel.LoadCommand.ExecuteAsync(null);

        viewModel.CloudCoverText.Should().Be("—");
        viewModel.CloudDetailText.Should().Contain("Sin conexión");
        var calls = weather.Calls;

        viewModel.ShiftObservationTimeCommand.Execute("60");
        weather.Calls.Should().Be(calls, "tras un fallo se espera antes de reintentar");

        weather.FailWith = null;
        clock.UtcNow = clock.UtcNow.AddMinutes(6);
        await viewModel.RefreshWeatherAsync();
        viewModel.CloudCoverText.Should().EndWith("%");
    }

    [Fact]
    public async Task Disabled_NeverContactsTheServiceAndSaysSo()
    {
        var clock = new ManualTimeProvider(WinterNightUtc);
        var weather = new CountingWeatherService((lat, lon) => CdmxForecast(clock.UtcNow.UtcDateTime, lat, lon));
        var viewModel = CreateMain(clock, weather, settings: new ObserverSettings { WeatherForecastEnabled = false });
        await viewModel.LoadCommand.ExecuteAsync(null);
        await viewModel.RefreshWeatherAsync(force: true);

        weather.Calls.Should().Be(0);
        viewModel.CloudDetailText.Should().Contain("desactivado");
        viewModel.GetNightCloudCover().Should().BeEmpty();
    }

    // ─── Aviso de versión nueva ─────────────────────────────────────────

    private const string ReleasesJson = """
        [
          {"tag_name":"v0.9.0","name":"AstroPlanner 0.9","html_url":"https://github.com/Yamato6/AstroPlanner/releases/tag/v0.9.0","draft":true},
          {"tag_name":"v0.8.1","name":"AstroPlanner 0.8.1","html_url":"https://github.com/Yamato6/AstroPlanner/releases/tag/v0.8.1","draft":false},
          {"tag_name":"v0.8.0","name":"AstroPlanner 0.8","html_url":"https://github.com/Yamato6/AstroPlanner/releases/tag/v0.8.0","draft":false},
          {"tag_name":"PreRelease","name":"AstroPlanner_0.2","html_url":"https://github.com/Yamato6/AstroPlanner/releases/tag/PreRelease","draft":false}
        ]
        """;

    [Theory]
    [InlineData("0.8.0.0", "0.8.1")]   // hay una más nueva (el borrador 0.9 no cuenta)
    [InlineData("0.8.1.0", null)]      // ya es la última
    [InlineData("1.0.0.0", null)]      // más nueva que todas
    public async Task UpdateChecker_FindsTheNewestPublishedVersion(string current, string? expected)
    {
        var checker = new GitHubUpdateChecker(new HttpClient(new StubHandler(_ => Json(ReleasesJson))), new NullLogger());

        var update = await checker.CheckAsync(Version.Parse(current));

        update?.Version.ToString().Should().Be(expected);
        if (expected is null) update.Should().BeNull();
        else update!.Url.Should().EndWith("/v0.8.1");
    }

    [Fact]
    public async Task UpdateChecker_PrivateRepoOrNoNetwork_ReturnsNullWithoutThrowing()
    {
        var notFound = new GitHubUpdateChecker(
            new HttpClient(new StubHandler(_ => Json("""{"message":"Not Found"}""", HttpStatusCode.NotFound))), new NullLogger());
        var offline = new GitHubUpdateChecker(
            new HttpClient(new StubHandler(_ => throw new HttpRequestException("sin red"))), new NullLogger());

        (await notFound.CheckAsync(new Version(0, 8, 0))).Should().BeNull();
        (await offline.CheckAsync(new Version(0, 8, 0))).Should().BeNull();
    }

    [Theory]
    [InlineData("v0.8.0", "0.8.0")]
    [InlineData("0.10.2", "0.10.2")]
    [InlineData("v1.0", "1.0.0")]
    [InlineData("PreRelease", null)]
    [InlineData("Pre_0.3", null)]
    public void ParseTag_HandlesVersionTagsAndIgnoresOthers(string tag, string? expected)
    {
        GitHubUpdateChecker.ParseTag(tag)?.ToString().Should().Be(expected);
        if (expected is null) GitHubUpdateChecker.ParseTag(tag).Should().BeNull();
    }

    [Fact]
    public async Task UpdateBanner_ShowsWhenEnabledAndCanBeDismissed()
    {
        var update = new UpdateInfo(new Version(0, 9, 0), "AstroPlanner 0.9", "https://github.com/Yamato6/AstroPlanner/releases/tag/v0.9.0");
        var checker = new FixedUpdateChecker(update);
        var viewModel = CreateMain(new ManualTimeProvider(WinterNightUtc), null, checker);

        await viewModel.CheckForUpdatesAsync(new Version(0, 8, 0));

        viewModel.HasUpdate.Should().BeTrue();
        viewModel.UpdateBannerText.Should().Contain("AstroPlanner 0.9");
        viewModel.DismissUpdateCommand.Execute(null);
        viewModel.HasUpdate.Should().BeFalse();

        var disabled = CreateMain(new ManualTimeProvider(WinterNightUtc), null, checker,
            settings: new ObserverSettings { CheckForUpdates = false });
        await disabled.CheckForUpdatesAsync(new Version(0, 8, 0));
        disabled.HasUpdate.Should().BeFalse();
        checker.Calls.Should().Be(1, "con la opción desactivada no se consulta GitHub");
    }
}
