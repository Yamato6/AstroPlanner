using AstroPlanner.Core.Interfaces;
using AstroPlanner.Core.Models;
using AstroPlanner.UI.Services;

namespace AstroPlanner.Tests;

/// <summary>Objetos de catálogo usados en varias pruebas.</summary>
internal static class TestCatalog
{
    public static readonly CelestialObject M42 = new(
        "M42", "Nebulosa de Orión", "Orion", CelestialObjectType.DiffuseNebula, 4.0,
        new EquatorialCoordinates(5.59, -5.39));

    public static readonly CelestialObject M31 = new(
        "M31", "Galaxia de Andrómeda", "Andromeda", CelestialObjectType.Galaxy, 3.4,
        new EquatorialCoordinates(0.712, 41.27));

    public static readonly CelestialObject M13 = new(
        "M13", "Gran Cúmulo de Hércules", "Hercules", CelestialObjectType.GlobularCluster, 5.8,
        new EquatorialCoordinates(16.69, 36.46));
}

internal sealed class FakeCelestialRepository(IReadOnlyList<CelestialObject> objects) : ICelestialRepository
{
    public Task<IReadOnlyList<CelestialObject>> GetAllAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(objects);
}

internal sealed class FakeCityRepository : ICityRepository
{
    public Task<IReadOnlyList<City>> GetAllAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<City>>([]);
}

internal sealed class FakeSettingsService(ObserverSettings saved) : ISettingsService
{
    private ObserverSettings _saved = saved;

    public int SaveCount { get; private set; }

    public ObserverSettings Load() => _saved;

    public void Save(ObserverSettings settings)
    {
        _saved = settings;
        SaveCount++;
    }
}

internal sealed class FakePlanService : IObservationPlanService
{
    public List<ObservationPlanEntry> Entries { get; set; } = [];

    public IReadOnlyList<string> Ids => Entries.Select(entry => entry.Id).ToList();

    public IReadOnlyList<ObservationPlanEntry> Load() => Entries;

    public void Save(IEnumerable<ObservationPlanEntry> entries) => Entries = entries.ToList();
}

internal sealed class FakeLogService : IObservationLogService
{
    public List<ObservationLogEntry> Entries { get; set; } = [];

    public int SaveCount { get; private set; }

    public bool FailOnSave { get; set; }

    public IReadOnlyList<ObservationLogEntry> Load() => Entries;

    public void Save(IEnumerable<ObservationLogEntry> entries)
    {
        if (FailOnSave)
        {
            throw new IOException("disco lleno");
        }

        Entries = entries.ToList();
        SaveCount++;
    }
}

internal sealed class NullLogger : IAppLogger
{
    public string LogLocation => "";

    public void Info(string message) { }

    public void Error(string message, Exception? exception = null) { }
}

/// <summary>
/// Reloj manual para probar el modo en vivo: la hora y el temporizador se
/// controlan desde la prueba (sin esperar minutos reales).
/// </summary>
internal sealed class ManualTimeProvider(DateTimeOffset utcNow) : TimeProvider
{
    public DateTimeOffset UtcNow { get; set; } = utcNow;

    public ManualTimer? LastTimer { get; private set; }

    public override DateTimeOffset GetUtcNow() => UtcNow;

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period) =>
        LastTimer = new ManualTimer(callback, state);

    internal sealed class ManualTimer(TimerCallback callback, object? state) : ITimer
    {
        public bool IsDisposed { get; private set; }

        public void Fire() => callback(state);

        public bool Change(TimeSpan dueTime, TimeSpan period) => true;

        public void Dispose() => IsDisposed = true;

        public ValueTask DisposeAsync()
        {
            Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
