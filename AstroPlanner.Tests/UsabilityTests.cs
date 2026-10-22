using System.Globalization;
using System.Windows.Documents;
using AstroPlanner.Core.Models;
using AstroPlanner.Core.Services;
using AstroPlanner.UI.Services;
using AstroPlanner.UI.ViewModels;
using FluentAssertions;

namespace AstroPlanner.Tests;

/// <summary>
/// Funciones de la 0.6: búsqueda, filtro de magnitud, modo en vivo, Mi plan
/// (notas, orden, CSV, impresión) y carta del cielo.
/// </summary>
public sealed class UsabilityTests
{
    // 2026-01-16 04:00 UTC = 2026-01-15 22:00 en CDMX (UTC-6).
    private static readonly DateTimeOffset WinterNightUtc = new(2026, 1, 16, 4, 0, 0, TimeSpan.Zero);

    private readonly FakeSettingsService _settingsService = new(new ObserverSettings { MinimumAltitude = 20 });
    private readonly FakePlanService _planService = new();

    private MainViewModel CreateMain(TimeProvider? time = null) => new(
        new FakeCelestialRepository([TestCatalog.M42, TestCatalog.M31, TestCatalog.M13]),
        new AstronomyService(),
        _planService,
        new NullLogger(),
        new SettingsViewModel(_settingsService, new FakeCityRepository(), new NullLogger()),
        time);

    private async Task<MainViewModel> CreateLoadedMainAsync()
    {
        var viewModel = CreateMain(new ManualTimeProvider(WinterNightUtc));
        await viewModel.LoadCommand.ExecuteAsync(null);
        return viewModel;
    }

    // ─── Búsqueda y magnitud ────────────────────────────────────────────

    [Theory]
    [InlineData("M31", "M31", true)]
    [InlineData("m 31", "M31", true)]
    [InlineData("31", "M31", true)]
    [InlineData("M3", "M31", false)]     // el id es exacto: M3 no encuentra M31
    [InlineData("andromeda", "M31", true)]
    [InlineData("ANDRÓMEDA", "M31", true)]
    [InlineData("orion", "M42", true)]   // sin acento encuentra "Orión" y la constelación "Orion"
    [InlineData("nebulosa de orion", "M42", true)]
    [InlineData("hercules", "M42", false)]
    [InlineData("", "M42", true)]
    public void CatalogSearch_MatchesIdNameAndConstellationIgnoringAccents(string query, string id, bool expected)
    {
        var item = new[] { TestCatalog.M42, TestCatalog.M31, TestCatalog.M13 }.Single(o => o.Id == id);

        CatalogSearch.Matches(item.Id, item.CommonName, item.Constellation, query).Should().Be(expected);
    }

    [Fact]
    public async Task SearchText_FiltersTheTableAndClearingRestoresIt()
    {
        var viewModel = await CreateLoadedMainAsync();

        viewModel.SearchText = "andrómeda";
        viewModel.Objects.Select(o => o.Id).Should().Equal("M31");
        viewModel.SelectedObject!.Id.Should().Be("M31");

        viewModel.SearchText = "";
        viewModel.Objects.Should().HaveCount(3);
    }

    [Fact]
    public async Task MaximumMagnitude_HidesFainterObjects()
    {
        var viewModel = await CreateLoadedMainAsync();

        viewModel.MaximumMagnitude = 5.0;

        viewModel.Objects.Select(o => o.Id).Should().BeEquivalentTo(["M42", "M31"]);
    }

    [Fact]
    public async Task ResetFilters_ClearsSearchAndRestoresSavedMagnitude()
    {
        var viewModel = await CreateLoadedMainAsync();
        viewModel.SearchText = "m42";
        viewModel.MaximumMagnitude = 3;

        viewModel.ResetFiltersCommand.Execute(null);

        viewModel.SearchText.Should().BeEmpty();
        viewModel.MaximumMagnitude.Should().Be(12);
        viewModel.Objects.Should().HaveCount(3);
    }

    // ─── Modo en vivo ───────────────────────────────────────────────────

    [Fact]
    public void SetNow_UsesTheObserverTimeZoneNotThePcOne()
    {
        var viewModel = CreateMain(new ManualTimeProvider(WinterNightUtc));
        viewModel.ObservationDateTime = new DateTime(2020, 1, 1, 12, 0, 0);

        viewModel.SetNowCommand.Execute(null);

        viewModel.ObservationDateTime.Should().Be(new DateTime(2026, 1, 15, 22, 0, 0));
    }

    [Fact]
    public void LiveMode_FollowsTheClockOnEachTick()
    {
        var clock = new ManualTimeProvider(WinterNightUtc);
        var viewModel = CreateMain(clock);
        viewModel.ObservationDateTime = new DateTime(2026, 1, 10, 20, 0, 0);

        viewModel.IsLive = true;
        viewModel.ObservationDateTime.Should().Be(new DateTime(2026, 1, 15, 22, 0, 0));

        clock.UtcNow = clock.UtcNow.AddMinutes(1);
        clock.LastTimer!.Fire();

        viewModel.ObservationDateTime.Should().Be(new DateTime(2026, 1, 15, 22, 1, 0));
        viewModel.IsLive.Should().BeTrue();
    }

    [Fact]
    public void LiveMode_TickFromAnotherThreadIsMarshalledToTheUiContext()
    {
        // Regresión: el temporizador real dispara en un hilo del pool. El cambio
        // debe pasar por el contexto de la interfaz, no aplicarse en ese hilo
        // (WPF lanzaría una excepción y la app se cerraría).
        var clock = new ManualTimeProvider(WinterNightUtc);
        var viewModel = CreateMain(clock);
        var uiContext = new QueueingSynchronizationContext();
        var previous = SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(uiContext);
        try
        {
            viewModel.IsLive = true;
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(previous);
        }

        clock.UtcNow = clock.UtcNow.AddMinutes(1);
        var poolThread = new Thread(() => clock.LastTimer!.Fire());
        poolThread.Start();
        poolThread.Join();

        viewModel.ObservationDateTime.Should().Be(new DateTime(2026, 1, 15, 22, 0, 0), "aún no se aplicó en el hilo de la interfaz");
        uiContext.Pending.Should().Be(1);

        uiContext.RunPending();

        viewModel.ObservationDateTime.Should().Be(new DateTime(2026, 1, 15, 22, 1, 0));
    }

    /// <summary>Contexto que encola lo que se le envía, como el despachador de WPF.</summary>
    private sealed class QueueingSynchronizationContext : SynchronizationContext
    {
        private readonly Queue<(SendOrPostCallback Callback, object? State)> _queue = new();

        public int Pending
        {
            get { lock (_queue) return _queue.Count; }
        }

        public override void Post(SendOrPostCallback d, object? state)
        {
            lock (_queue) _queue.Enqueue((d, state));
        }

        public void RunPending()
        {
            while (true)
            {
                (SendOrPostCallback Callback, object? State) item;
                lock (_queue)
                {
                    if (_queue.Count == 0) return;
                    item = _queue.Dequeue();
                }

                item.Callback(item.State);
            }
        }
    }

    [Fact]
    public void LiveMode_ChoosingATimeByHandTurnsItOff()
    {
        var clock = new ManualTimeProvider(WinterNightUtc);
        var viewModel = CreateMain(clock);
        viewModel.IsLive = true;
        var timer = clock.LastTimer!;

        viewModel.ShiftObservationTimeCommand.Execute("60");

        viewModel.IsLive.Should().BeFalse();
        timer.IsDisposed.Should().BeTrue();
        viewModel.ObservationDateTime.Should().Be(new DateTime(2026, 1, 15, 23, 0, 0));
    }

    // ─── Mi plan ────────────────────────────────────────────────────────

    [Fact]
    public async Task ObservationPlan_IsSortedByCulmination()
    {
        var viewModel = await CreateLoadedMainAsync();

        // M42 culmina hacia las 22:20 y M31 al anochecer: M31 va primero aunque se agregue después.
        foreach (var id in new[] { "M42", "M31" })
        {
            viewModel.SelectedObject = viewModel.Objects.Single(o => o.Id == id);
            viewModel.AddSelectedToObservationPlanCommand.Execute(null);
        }

        viewModel.ObservationPlan.Select(item => item.Id).Should().Equal("M31", "M42");
        viewModel.ObservationPlan.Select(item => item.CulminationLocal).Should().BeInAscendingOrder();
        viewModel.ObservationPlan.Should().OnlyContain(item => item.CulminationLocal >= viewModel.Night!.WindowStartLocal);
    }

    [Fact]
    public async Task ObservationPlan_NotesAreRestoredAndSaved()
    {
        _planService.Entries = [new("M42", "usar filtro UHC")];
        var viewModel = await CreateLoadedMainAsync();

        viewModel.ObservationPlan.Single().Note.Should().Be("usar filtro UHC");

        viewModel.ObservationPlan.Single().Note = "ocular de 25 mm";

        _planService.Entries.Should().Equal(new ObservationPlanEntry("M42", "ocular de 25 mm"));
    }

    [Fact]
    public async Task PlanCsv_HasHeaderOneRowPerItemAndEscapesSeparators()
    {
        _planService.Entries = [new("M42", "filtro UHC, ocular 25 mm"), new("M31")];
        var viewModel = await CreateLoadedMainAsync();

        var lines = viewModel.BuildObservationPlanCsv(CultureInfo.InvariantCulture)
            .Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);

        lines.Should().HaveCount(3);
        lines[0].Should().StartWith("Orden,ID,Nombre");
        lines[1].Should().StartWith("1,M31,Galaxia de Andrómeda");
        lines[2].Should().StartWith("2,M42,").And.EndWith("\"filtro UHC, ocular 25 mm\"");
    }

    [Fact]
    public async Task PlanCsv_UsesTheCultureListSeparator()
    {
        _planService.Entries = [new("M42")];
        var viewModel = await CreateLoadedMainAsync();

        var csv = viewModel.BuildObservationPlanCsv(CultureInfo.GetCultureInfo("es-ES"));

        csv.Should().StartWith("Orden;ID;Nombre").And.Contain(";4,0;", "es-ES usa coma decimal y punto y coma como separador");
    }

    [Fact]
    public async Task ExportObservationPlan_WritesUtf8FileWithBom()
    {
        _planService.Entries = [new("M31")];
        var viewModel = await CreateLoadedMainAsync();
        var path = Path.Combine(Path.GetTempPath(), $"astroplanner-plan-{Guid.NewGuid():N}.csv");

        try
        {
            viewModel.ExportObservationPlan(path).Should().BeTrue();

            var bytes = File.ReadAllBytes(path);
            bytes.Take(3).Should().Equal(0xEF, 0xBB, 0xBF);
            File.ReadAllText(path).Should().Contain("Andrómeda");
            viewModel.PlanStatusMessage.Should().StartWith("Plan exportado");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task PrintDocument_HasHeaderRowPlusOneRowPerItem()
    {
        _planService.Entries = [new("M42", "nota"), new("M31")];
        var viewModel = await CreateLoadedMainAsync();
        var items = viewModel.ObservationPlan.ToList();
        var summary = viewModel.PlanSummaryText;

        // FlowDocument requiere un hilo STA, como el de la interfaz.
        var rowCount = RunOnStaThread(() =>
        {
            var document = PlanPrintDocument.Create(items, summary);
            return document.Blocks.OfType<Table>().Single().RowGroups[0].Rows.Count;
        });

        rowCount.Should().Be(3);
        summary.Should().Contain("Noche del 15/01/2026").And.Contain("Luna");
    }

    // ─── Carta del cielo ────────────────────────────────────────────────

    [Theory]
    [InlineData(90, 0, 0, 0)]      // cénit en el centro
    [InlineData(0, 0, 0, 90)]      // norte arriba, en el horizonte
    [InlineData(0, 90, -90, 0)]    // este a la izquierda
    [InlineData(0, 270, 90, 0)]    // oeste a la derecha
    [InlineData(30, 180, 0, -60)]  // sur, a 30° de altura
    public void SkyProjection_PutsZenithInTheCenterAndNorthUp(double altitude, double azimuth, double x, double y)
    {
        var (px, py) = SkyProjection.ToXY(altitude, azimuth);

        px.Should().BeApproximately(x, 1e-9);
        py.Should().BeApproximately(y, 1e-9);
    }

    [Fact]
    public async Task SkyMap_ShowsOnlyObjectsAboveTheHorizonAndMarksPlanAndSelection()
    {
        _planService.Entries = [new("M31")];
        var viewModel = await CreateLoadedMainAsync();

        var points = viewModel.GetSkyMapPoints();

        points.Should().OnlyContain(p => p.AltitudeDegrees > 0);
        points.Select(p => p.Id).Should().NotContain("M13", "M13 está bajo el horizonte en esa noche");
        points.Single(p => p.Id == "M31").IsInPlan.Should().BeTrue();
        points.Should().ContainSingle(p => p.IsSelected);
    }

    [Fact]
    public async Task SkyMap_ClickSelectsTheNearestObjectOrNothing()
    {
        var viewModel = await CreateLoadedMainAsync();
        var m31 = viewModel.GetSkyMapPoints().Single(p => p.Id == "M31");

        viewModel.SelectNearestOnSkyMap(m31.Position.X + 1, m31.Position.Y - 1).Should().BeTrue();
        viewModel.SelectedObject!.Id.Should().Be("M31");

        viewModel.SelectNearestOnSkyMap(500, 500).Should().BeFalse();
        viewModel.SelectedObject!.Id.Should().Be("M31");
    }

    private static T RunOnStaThread<T>(Func<T> action)
    {
        T result = default!;
        Exception? error = null;
        var thread = new Thread(() =>
        {
            try { result = action(); }
            catch (Exception ex) { error = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        return error is null ? result : throw new InvalidOperationException("Falló en el hilo STA.", error);
    }
}
