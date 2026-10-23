using AstroPlanner.Core.Models;
using AstroPlanner.Core.Services;
using AstroPlanner.UI.Services;
using AstroPlanner.UI.ViewModels;
using FluentAssertions;

namespace AstroPlanner.Tests;

/// <summary>
/// Pruebas de MainViewModel y SettingsViewModel sin interfaz, con dobles en
/// memoria para catálogo, configuración, plan y registro.
/// </summary>
public sealed class ViewModelTests
{
    private static readonly CelestialObject M42 = TestCatalog.M42;
    private static readonly CelestialObject M31 = TestCatalog.M31;
    private static readonly CelestialObject M13 = TestCatalog.M13;

    private readonly FakeSettingsService _settingsService = new(new ObserverSettings { MinimumAltitude = 35 });
    private readonly FakePlanService _planService = new();

    private SettingsViewModel CreateSettings() =>
        new(_settingsService, new FakeCityRepository(), new NullLogger());

    private async Task<MainViewModel> CreateLoadedMainAsync()
    {
        var viewModel = new MainViewModel(
            new FakeCelestialRepository([M42, M31, M13]),
            new AstronomyService(),
            _planService,
            new NullLogger(),
            CreateSettings(),
            new LogbookViewModel(new FakeLogService(), new NullLogger()));
        // Noche de invierno en CDMX: M42 alta, M13 bajo el horizonte.
        viewModel.ObservationDateTime = new DateTime(2026, 1, 15, 22, 0, 0);
        await viewModel.LoadCommand.ExecuteAsync(null);
        return viewModel;
    }

    [Fact]
    public async Task Load_FillsTableSortedByAltitudeAndSelectsFirstRow()
    {
        var viewModel = await CreateLoadedMainAsync();

        viewModel.CatalogCount.Should().Be(3);
        viewModel.Objects.Should().HaveCount(3);
        viewModel.Objects.Select(o => o.AltitudeDegrees).Should().BeInDescendingOrder();
        viewModel.SelectedObject.Should().BeSameAs(viewModel.Objects[0]);
        viewModel.SelectedObservation.Should().NotBeNull();
        viewModel.ValidationMessage.Should().BeNull();
    }

    [Fact]
    public async Task SelectedObservation_MatchesTablePosition()
    {
        var viewModel = await CreateLoadedMainAsync();

        var row = viewModel.SelectedObject!;
        var detail = viewModel.SelectedObservation!;

        detail.Coordinates.AltitudeDegrees.Should().BeApproximately(row.AltitudeDegrees, 1e-9);
        detail.Coordinates.AzimuthDegrees.Should().BeApproximately(row.AzimuthDegrees, 1e-9);
    }

    [Fact]
    public async Task OnlyVisible_HidesObjectsBelowThreshold()
    {
        var viewModel = await CreateLoadedMainAsync();

        viewModel.OnlyVisible = true;

        viewModel.Objects.Should().NotBeEmpty().And.OnlyContain(o => o.IsVisible);
        viewModel.Objects.Should().NotContain(o => o.Id == "M13");
    }

    [Fact]
    public async Task ResetFilters_RestoresSavedMinimumAltitude()
    {
        var viewModel = await CreateLoadedMainAsync();
        viewModel.MinimumAltitude = 60;
        viewModel.TypeFilter = nameof(CelestialObjectType.Galaxy);

        viewModel.ResetFiltersCommand.Execute(null);

        viewModel.MinimumAltitude.Should().Be(35);
        viewModel.TypeFilter.Should().Be("All");
        viewModel.Objects.Should().HaveCount(3);
    }

    [Fact]
    public async Task Filters_ShowSpanishLabelsAndCanBeCleared()
    {
        var viewModel = await CreateLoadedMainAsync();

        viewModel.TypeFilters[0].Should().Be(new FilterOption("All", "Todos"));
        viewModel.TypeFilters.Should().Contain(new FilterOption("GlobularCluster", "Cúmulo globular"));
        viewModel.ConstellationFilters[0].Should().Be(new FilterOption("", "Todas"));

        // Elegir una constelación y volver a "Todas" desde la propia lista.
        var constellation = viewModel.ConstellationFilters[1].Value;
        viewModel.ConstellationFilter = constellation;
        viewModel.Objects.Should().OnlyContain(o => o.Constellation == constellation);
        viewModel.ConstellationFilter = viewModel.ConstellationFilters[0].Value;
        viewModel.Objects.Should().HaveCount(3);
    }

    [Fact]
    public async Task InvalidMinimumAltitude_ShowsWarningAndKeepsLastTable()
    {
        var viewModel = await CreateLoadedMainAsync();
        var before = viewModel.Objects.ToList();

        viewModel.MinimumAltitude = 120;

        viewModel.ValidationMessage.Should().Contain("altitud mínima");
        viewModel.HasErrors.Should().BeTrue();
        viewModel.Objects.Should().Equal(before);

        viewModel.MinimumAltitude = 30;

        viewModel.ValidationMessage.Should().BeNull();
        viewModel.HasErrors.Should().BeFalse();
    }

    [Fact]
    public async Task AfterMidnight_DetailUsesTheNightInProgress()
    {
        var viewModel = await CreateLoadedMainAsync();

        viewModel.ObservationDateTime = new DateTime(2026, 1, 15, 2, 0, 0);

        // La noche en curso va de la puesta del 14 a la salida del Sol del 15.
        var night = viewModel.Night!;
        night.WindowStartLocal.Date.Should().Be(new DateTime(2026, 1, 14));
        night.WindowEndLocal.Date.Should().Be(new DateTime(2026, 1, 15));

        var culmination = viewModel.SelectedObservation!.BestObservationTimeLocal!.Value;
        culmination.Should().BeOnOrAfter(night.WindowStartLocal).And.BeOnOrBefore(night.WindowEndLocal);
        viewModel.GetNightAltitudeCurve()[0].LocalTime.Should().Be(night.WindowStartLocal);
    }

    [Fact]
    public async Task Night_InMexicoCityWinter_UsesSunsetAndAstronomicalDarkness()
    {
        var viewModel = await CreateLoadedMainAsync();

        // CDMX, 15/1/2026: puesta ≈ 18:16, salida ≈ 07:13 (UTC-6).
        viewModel.Night!.SunsetLocal.Should().BeCloseTo(new DateTime(2026, 1, 15, 18, 16, 0), TimeSpan.FromMinutes(5));
        viewModel.Night.SunriseLocal.Should().BeCloseTo(new DateTime(2026, 1, 16, 7, 13, 0), TimeSpan.FromMinutes(5));
        viewModel.DarkNightText.Should().MatchRegex(@"^\d\d:\d\d → \d\d:\d\d$");
        viewModel.ChartTitle.Should().Contain("→");
    }

    [Fact]
    public async Task SkyBanner_WarnsDuringTheDayAndClearsAtNight()
    {
        var viewModel = await CreateLoadedMainAsync();

        viewModel.ObservationDateTime = new DateTime(2026, 1, 15, 13, 0, 0);
        viewModel.SkyCondition.Should().Be(SkyCondition.Day);
        viewModel.SkyBanner.Should().StartWith("Es de día");

        viewModel.ObservationDateTime = new DateTime(2026, 1, 15, 23, 0, 0);
        viewModel.SkyCondition.Should().Be(SkyCondition.Dark);
        viewModel.SkyBanner.Should().BeNull();
    }

    [Fact]
    public async Task Moon_IsReportedAndSeparationComputedForEachRow()
    {
        var viewModel = await CreateLoadedMainAsync();

        viewModel.Moon.Should().NotBeNull();
        viewModel.MoonIlluminationText.Should().EndWith("%");
        viewModel.Objects.Should().OnlyContain(o => o.MoonSeparationDegrees >= 0 && o.MoonSeparationDegrees <= 180);
        viewModel.SelectedObservation!.MoonSeparationDegrees.Should().NotBeNull();
    }

    [Theory]
    [InlineData(0.01, true, "Luna nueva")]
    [InlineData(0.25, true, "Creciente")]
    [InlineData(0.5, true, "Cuarto creciente")]
    [InlineData(0.8, false, "Gibosa menguante")]
    [InlineData(0.99, false, "Luna llena")]
    public void DescribeMoonPhase_NamesThePhase(double fraction, bool waxing, string expected)
    {
        MainViewModel.DescribeMoonPhase(fraction, waxing).Should().Be(expected);
    }

    [Fact]
    public void MoonInterferes_OnlyWhenBrightCloseAndAboveHorizon()
    {
        var brightHigh = new MoonState(new HorizontalCoordinates(40, 180), new EquatorialCoordinates(1, 0), 0.9, true);
        var brightLow = brightHigh with { Horizontal = new HorizontalCoordinates(-5, 180) };
        var dim = brightHigh with { IlluminatedFraction = 0.1 };

        MainViewModel.MoonInterferes(brightHigh, 15).Should().BeTrue();
        MainViewModel.MoonInterferes(brightHigh, 45).Should().BeFalse();
        MainViewModel.MoonInterferes(brightLow, 15).Should().BeFalse();
        MainViewModel.MoonInterferes(dim, 15).Should().BeFalse();
    }

    [Fact]
    public async Task ObservationPlan_IsRestoredByIdAndSavedOnChanges()
    {
        _planService.Entries = [new("M31", "con filtro"), new("M999")];
        var viewModel = await CreateLoadedMainAsync();

        viewModel.ObservationPlan.Select(o => o.Id).Should().Equal("M31");

        viewModel.SelectedObject = viewModel.Objects.Single(o => o.Id == "M42");
        viewModel.AddSelectedToObservationPlanCommand.Execute(null);

        _planService.Ids.Should().Equal("M31", "M42");
        viewModel.IsSelectedObjectInObservationPlan.Should().BeTrue();

        viewModel.RemoveFromObservationPlanCommand.Execute(viewModel.ObservationPlan[0]);

        _planService.Ids.Should().Equal("M42");
    }

    [Fact]
    public void Settings_SaveWithInvalidLatitude_IsRejectedAndNotPersisted()
    {
        var settings = CreateSettings();
        settings.UseManualLocationCommand.Execute(null);
        settings.Latitude = 100;

        settings.SaveCommand.Execute(null);

        settings.HasError.Should().BeTrue();
        settings.StatusMessage.Should().Contain("latitud");
        _settingsService.SaveCount.Should().Be(0);
    }

    [Fact]
    public void Settings_RevertChanges_DiscardsUnsavedEdits()
    {
        var settings = CreateSettings();
        settings.MinimumAltitude = 70;

        settings.RevertChanges();

        settings.MinimumAltitude.Should().Be(35);
    }
}
