using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AstroPlanner.Core.Algorithms;
using AstroPlanner.Core.Interfaces;
using AstroPlanner.Core.Models;

namespace AstroPlanner.UI.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly ICelestialRepository _repository;
    private readonly List<CelestialObjectRowViewModel> _allObjects = [];
    private IReadOnlyList<string> _constellationFilters = [""];

    public MainViewModel(ICelestialRepository repository, SettingsViewModel settings)
    {
        _repository = repository;
        Settings = settings;

        // La configuración persistida manda sobre los valores en runtime.
        Latitude = settings.Latitude;
        Longitude = settings.Longitude;
        Elevation = settings.Elevation;
        MinimumAltitude = settings.MinimumAltitude;
        TimeZoneId = settings.TimeZoneId;
        ObservationDateTime = DateTime.Now;
        TypeFilter = "All";

        settings.SettingsSaved += OnSettingsSaved;
    }

    /// <summary>Pestaña de Configuración, compartida con la vista.</summary>
    public SettingsViewModel Settings { get; }

    public ObservableCollection<CelestialObjectRowViewModel> Objects { get; } = [];

    public IReadOnlyList<string> TypeFilters { get; } =
    [
        "All",
        nameof(CelestialObjectType.Galaxy),
        nameof(CelestialObjectType.OpenCluster),
        nameof(CelestialObjectType.GlobularCluster),
        nameof(CelestialObjectType.PlanetaryNebula),
        nameof(CelestialObjectType.DiffuseNebula),
        nameof(CelestialObjectType.SupernovaRemnant),
        nameof(CelestialObjectType.Unknown)
    ];

    /// <summary>Constelaciones presentes en el catálogo ("" = todas).</summary>
    public IReadOnlyList<string> ConstellationFilters
    {
        get => _constellationFilters;
        private set { _constellationFilters = value; OnPropertyChanged(); }
    }

    public string LocationSummary => $"{Latitude:0.####}°, {Longitude:0.####}°";

    [ObservableProperty] private double latitude;
    [ObservableProperty] private double longitude;
    [ObservableProperty] private double elevation;
    [ObservableProperty] private DateTime observationDateTime;
    [ObservableProperty] private double minimumAltitude;
    [ObservableProperty] private string timeZoneId = TimeZoneInfo.Local.Id;
    [ObservableProperty] private bool onlyVisible;
    [ObservableProperty] private string typeFilter = "All";
    [ObservableProperty] private string constellationFilter = "";
    [ObservableProperty] private CelestialObjectRowViewModel? selectedObject;
    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private string? errorMessage;

    /// <summary>Zona horaria efectiva (la configurada, con reserva a la local).</summary>
    private TimeZoneInfo EffectiveTimeZone
    {
        get
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(TimeZoneId);
            }
            catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
            {
                return TimeZoneInfo.Local;
            }
        }
    }

    /// <summary>
    /// Convierte una fecha/hora "de pared" elegida por el usuario (en la zona
    /// horaria del observador) a UTC, que es lo que requieren los cálculos.
    /// </summary>
    private DateTime ToUtc(DateTime localWallClock) =>
        TimeZoneInfo.ConvertTimeToUtc(
            DateTime.SpecifyKind(localWallClock, DateTimeKind.Unspecified),
            EffectiveTimeZone);

    /// <summary>
    /// Serie de altitud a lo largo de la noche (18:00 → 06:00) en hora local
    /// del observador, para dibujar la curva de tránsito.
    /// </summary>
    public IReadOnlyList<(DateTime LocalTime, double AltitudeDegrees)> GetNightAltitudeCurve()
    {
        if (SelectedObject is null || Latitude is < -90 or > 90 || Longitude is < -180 or > 180)
        {
            return [];
        }

        var observer = new ObserverLocation(Latitude, Longitude, Elevation);
        var start = ObservationDateTime.Date.AddHours(18);
        var coordinates = SelectedObject.Coordinates;
        var points = new List<(DateTime LocalTime, double AltitudeDegrees)>();

        // 25 puntos: 18:00, 18:30, ..., 06:00 (12 horas cada 30 min).
        for (var index = 0; index <= 24; index++)
        {
            var localTime = start.AddMinutes(index * 30);
            var utcTime = ToUtc(localTime);
            var horizontal = CoordinateTransformer.ToHorizontal(coordinates, observer, utcTime);
            points.Add((localTime, horizontal.AltitudeDegrees));
        }

        return points;
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (IsBusy) return;
        try
        {
            IsBusy = true;
            ErrorMessage = null;
            var objects = await _repository.GetAllAsync();
            _allObjects.Clear();
            _allObjects.AddRange(objects.Select(item => new CelestialObjectRowViewModel(item)));

            ConstellationFilters =
                [.. _allObjects.Select(o => o.Constellation).Distinct().OrderBy(c => c)];

            RecalculateAndFilter();
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or JsonException)
        {
            ErrorMessage = $"No se pudo cargar el catálogo: {ex.Message}";
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private void ResetFilters()
    {
        OnlyVisible = false;
        TypeFilter = "All";
        ConstellationFilter = "";
        MinimumAltitude = 20;
        RecalculateAndFilter();
    }

    partial void OnLatitudeChanged(double value) => RecalculateAndFilter();
    partial void OnLongitudeChanged(double value) => RecalculateAndFilter();
    partial void OnElevationChanged(double value) => RecalculateAndFilter();
    partial void OnObservationDateTimeChanged(DateTime value) => RecalculateAndFilter();
    partial void OnMinimumAltitudeChanged(double value) => RecalculateAndFilter();
    partial void OnTimeZoneIdChanged(string value) => RecalculateAndFilter();
    partial void OnOnlyVisibleChanged(bool value) => RecalculateAndFilter();
    partial void OnTypeFilterChanged(string value) => RecalculateAndFilter();
    partial void OnConstellationFilterChanged(string value) => RecalculateAndFilter();
    partial void OnSelectedObjectChanged(CelestialObjectRowViewModel? value) =>
        OnPropertyChanged(nameof(SelectedObject));

    /// <summary>Aplica la configuración guardada en la pestaña de ajustes.</summary>
    private void OnSettingsSaved(Services.ObserverSettings settings)
    {
        Latitude = settings.LatitudeDegrees;
        Longitude = settings.LongitudeDegrees;
        Elevation = settings.ElevationMeters;
        MinimumAltitude = settings.MinimumAltitude;
        TimeZoneId = settings.TimeZoneId;
        // Los setters ya disparan RecalculateAndFilter.
    }

    private void RecalculateAndFilter()
    {
        OnPropertyChanged(nameof(LocationSummary));
        if (_allObjects.Count == 0 || Latitude is < -90 or > 90 || Longitude is < -180 or > 180)
        {
            Objects.Clear();
            return;
        }

        var observer = new ObserverLocation(Latitude, Longitude, Elevation);
        var utc = ToUtc(ObservationDateTime);

        foreach (var item in _allObjects)
        {
            item.UpdatePosition(observer, utc, MinimumAltitude);
        }

        var filtered = _allObjects
            .Where(item =>
                (!OnlyVisible || item.IsVisible)
                && (TypeFilter == "All" || item.Type.ToString() == TypeFilter)
                && (string.IsNullOrWhiteSpace(ConstellationFilter)
                    || item.Constellation.Equals(ConstellationFilter, StringComparison.OrdinalIgnoreCase)))
            .OrderByDescending(item => item.AltitudeDegrees);

        Objects.Clear();
        foreach (var item in filtered)
        {
            Objects.Add(item);
        }

        SelectedObject ??= Objects.FirstOrDefault();
        if (SelectedObject is not null && !Objects.Contains(SelectedObject))
        {
            SelectedObject = Objects.FirstOrDefault();
        }
    }
}

public partial class CelestialObjectRowViewModel : ObservableObject
{
    private readonly CelestialObject _item;

    public CelestialObjectRowViewModel(CelestialObject item) => _item = item;

    public string Id => _item.Id;
    public string CommonName => _item.CommonName;
    public string Constellation => _item.Constellation;
    public CelestialObjectType Type => _item.Type;
    public double ApparentMagnitude => _item.ApparentMagnitude;
    public EquatorialCoordinates Coordinates => _item.Coordinates;

    [ObservableProperty] private double altitudeDegrees;
    [ObservableProperty] private double azimuthDegrees;
    [ObservableProperty] private bool isVisible;

    public void UpdatePosition(ObserverLocation observer, DateTime utc, double threshold)
    {
        var coordinates = CoordinateTransformer.ToHorizontal(_item.Coordinates, observer, utc);
        AltitudeDegrees = coordinates.AltitudeDegrees;
        AzimuthDegrees = coordinates.AzimuthDegrees;
        IsVisible = AltitudeDegrees >= threshold;
    }
}
