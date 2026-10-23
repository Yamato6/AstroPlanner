using System.Globalization;
using AstroPlanner.Core.Models;
using AstroPlanner.Core.Services;
using AstroPlanner.UI.Services;
using AstroPlanner.UI.ViewModels;
using FluentAssertions;

namespace AstroPlanner.Tests;

/// <summary>
/// Funciones de la 0.7: bitácora de observaciones (persistencia, progreso
/// Messier, CSV, marcas en la tabla) y planetas en el planificador.
/// </summary>
public sealed class LogbookAndPlanetUiTests : IDisposable
{
    private static readonly DateTimeOffset WinterNightUtc = new(2026, 1, 16, 4, 0, 0, TimeSpan.Zero);

    private readonly FakeLogService _logService = new();
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "AstroPlannerTests", Guid.NewGuid().ToString("N"));

    public LogbookAndPlanetUiTests() => Directory.CreateDirectory(_directory);

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    private LogbookViewModel CreateLogbook() => new(_logService, new NullLogger());

    private static ObservationLogEntry Entry(string id, DateTime when, int rating = 4) =>
        new(Guid.NewGuid(), id, id, when, "CDMX", "Dobson 8\"", 3, 4, rating, "");

    private async Task<MainViewModel> CreateLoadedMainAsync(LogbookViewModel logbook, params CelestialObject[] extra)
    {
        var viewModel = new MainViewModel(
            new FakeCelestialRepository([TestCatalog.M42, TestCatalog.M31, TestCatalog.M13, .. extra]),
            new AstronomyService(),
            new FakePlanService(),
            new NullLogger(),
            new SettingsViewModel(new FakeSettingsService(new ObserverSettings()), new FakeCityRepository(), new NullLogger()),
            logbook,
            new ManualTimeProvider(WinterNightUtc));
        await viewModel.LoadCommand.ExecuteAsync(null);
        return viewModel;
    }

    // ─── Bitácora ───────────────────────────────────────────────────────

    [Fact]
    public void Add_StoresTheEntryNewestFirstAndRemembersTheEquipment()
    {
        var logbook = CreateLogbook();

        var first = logbook.CreateDraft("M42", "Nebulosa de Orión", new DateTime(2026, 1, 10, 22, 15, 0), "CDMX");
        first.Equipment = "Refractor 80 mm";
        first.Rating = 5;
        logbook.Add(first).Should().BeTrue();

        var second = logbook.CreateDraft("M31", "Andrómeda", new DateTime(2026, 1, 12, 21, 0, 0), "CDMX");
        second.Equipment.Should().Be("Refractor 80 mm", "se propone el equipo de la última observación");
        logbook.Add(second).Should().BeTrue();

        logbook.Entries.Select(e => e.ObjectId).Should().Equal("M31", "M42");
        logbook.Entries.Last().ObservedAtLocal.Should().Be(new DateTime(2026, 1, 10, 22, 15, 0));
        logbook.Entries.Last().RatingStars.Should().Be("★★★★★");
        _logService.Entries.Should().HaveCount(2);
    }

    [Theory]
    [InlineData("22:30", true)]
    [InlineData("7:05", true)]
    [InlineData("24:00", false)]
    [InlineData("22.30", false)]
    [InlineData("", false)]
    public void Draft_RequiresAValidTime(string time, bool valid)
    {
        var logbook = CreateLogbook();
        var draft = logbook.CreateDraft("M42", "Orión", new DateTime(2026, 1, 10, 22, 0, 0), "CDMX");

        draft.ObservedTime = time;

        logbook.Add(draft).Should().Be(valid);
        logbook.Entries.Should().HaveCount(valid ? 1 : 0);
    }

    [Fact]
    public void MessierProgress_CountsDistinctMessierObjectsOnly()
    {
        _logService.Entries =
        [
            Entry("M42", new DateTime(2026, 1, 10)),
            Entry("M42", new DateTime(2026, 1, 11)),   // repetido: cuenta una vez
            Entry("M31", new DateTime(2026, 1, 11)),
            Entry("Júpiter", new DateTime(2026, 1, 11)) // no es Messier
        ];

        var logbook = CreateLogbook();

        logbook.ObservedMessierCount.Should().Be(2);
        logbook.MessierProgressText.Should().Be("2 / 110");
        logbook.HasBeenObserved("Júpiter").Should().BeTrue();
    }

    [Fact]
    public void Delete_RemovesTheEntryAndUpdatesProgress()
    {
        _logService.Entries = [Entry("M42", new DateTime(2026, 1, 10))];
        var logbook = CreateLogbook();

        logbook.DeleteCommand.Execute(logbook.Entries[0]);

        logbook.Entries.Should().BeEmpty();
        logbook.ObservedMessierCount.Should().Be(0);
        _logService.Entries.Should().BeEmpty();
    }

    [Fact]
    public void SaveFailure_KeepsMemoryConsistentWithDiskAndReportsIt()
    {
        var logbook = CreateLogbook();
        _logService.FailOnSave = true;

        var added = logbook.Add(logbook.CreateDraft("M42", "Orión", new DateTime(2026, 1, 10, 22, 0, 0), "CDMX"));

        added.Should().BeFalse();
        logbook.Entries.Should().BeEmpty("lo que no se pudo guardar no debe quedar a medias en pantalla");
        logbook.StatusMessage.Should().Contain("No se pudo guardar");
    }

    [Fact]
    public void Csv_ListsEntriesOldestFirstAndEscapesNotes()
    {
        _logService.Entries =
        [
            Entry("M31", new DateTime(2026, 1, 12, 21, 0, 0)) with { Notes = "brazos visibles, sin filtro" },
            Entry("M42", new DateTime(2026, 1, 10, 22, 15, 0))
        ];
        var logbook = CreateLogbook();

        var lines = logbook.BuildCsv(CultureInfo.InvariantCulture)
            .Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);

        lines.Should().HaveCount(3);
        lines[0].Should().StartWith("Fecha,Hora,ID,Nombre");
        lines[1].Should().StartWith("2026-01-10,22:15,M42");
        lines[2].Should().EndWith("\"brazos visibles, sin filtro\"");
    }

    [Fact]
    public void JsonLog_RoundTripsAndSetsAsideACorruptFile()
    {
        var path = Path.Combine(_directory, "logbook.json");
        var service = new JsonObservationLogService(path);
        var entry = Entry("M42", new DateTime(2026, 1, 10, 22, 15, 0)) with { Notes = "con \"comillas\"" };

        service.Save([entry]);
        service.Load().Should().Equal(entry);

        File.WriteAllText(path, "{ no es json");
        var corrupt = new JsonObservationLogService(path);

        corrupt.Load().Should().BeEmpty();
        File.Exists(path).Should().BeFalse("el archivo dañado se aparta en lugar de sobrescribirse");
        File.ReadAllText(corrupt.PreservedCorruptFilePath!).Should().Be("{ no es json");
    }

    [Fact]
    public async Task ObservedObjects_AreMarkedInTheTableAndUpdateLive()
    {
        _logService.Entries = [Entry("M42", new DateTime(2026, 1, 10))];
        var logbook = CreateLogbook();
        var viewModel = await CreateLoadedMainAsync(logbook);

        viewModel.Objects.Single(o => o.Id == "M42").IsObserved.Should().BeTrue();
        viewModel.Objects.Single(o => o.Id == "M31").IsObserved.Should().BeFalse();
        viewModel.ObservedSummaryText.Should().Be("Messier observados: 1 / 110");

        viewModel.SelectedObject = viewModel.Objects.Single(o => o.Id == "M31");
        var draft = viewModel.CreateLogDraftForSelected()!;
        draft.ObjectId.Should().Be("M31");
        draft.ObservedAtLocal.Should().Be(new DateTime(2026, 1, 15, 22, 0, 0));
        logbook.Add(draft);

        viewModel.Objects.Single(o => o.Id == "M31").IsObserved.Should().BeTrue();
        viewModel.ObservedSummaryText.Should().Be("Messier observados: 2 / 110");
    }

    // ─── Planetas en el planificador ────────────────────────────────────

    [Fact]
    public async Task Planets_GetDateDependentMagnitudeAndPlanetDetails()
    {
        var jupiter = SolarSystemCatalog.Planets.Single(p => p.Planet == Planet.Jupiter);
        var viewModel = await CreateLoadedMainAsync(CreateLogbook(), jupiter);

        var row = viewModel.Objects.Single(o => o.Id == "Júpiter");
        row.IsPlanet.Should().BeTrue();
        row.ApparentMagnitude.Should().BeApproximately(-2.7, 0.3, "Júpiter estaba en oposición el 10/1/2026");
        row.MoonInterferes.Should().BeFalse("la Luna no oculta a un planeta brillante");

        viewModel.SelectedObject = row;
        var detail = viewModel.SelectedObservation!;
        detail.IsPlanet.Should().BeTrue();
        detail.Magnitude.Should().BeApproximately(row.ApparentMagnitude, 0.01);
        detail.Planet!.ApparentDiameterArcsec.Should().BeGreaterThan(40);

        viewModel.TypeFilter = nameof(CelestialObjectType.Planet);
        viewModel.Objects.Should().ContainSingle().Which.Id.Should().Be("Júpiter");

        viewModel.TypeFilter = "All";
        viewModel.SearchText = "jupiter";
        viewModel.Objects.Should().ContainSingle().Which.Id.Should().Be("Júpiter");

        viewModel.SearchText = "";
        viewModel.GetSkyMapPoints().Single(p => p.Id == "Júpiter").IsPlanet.Should().BeTrue();
    }
}
