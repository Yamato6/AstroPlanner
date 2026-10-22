using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AstroPlanner.Core.Algorithms;
using AstroPlanner.Core.Interfaces;
using AstroPlanner.Core.Models;
using AstroPlanner.UI.Services;

namespace AstroPlanner.UI.ViewModels;

public partial class MainViewModel : ObservableValidator
{
    private readonly ICelestialRepository _repository;
    private readonly IAstronomyService _astronomy;
    private readonly IObservationPlanService _planService;
    private readonly IAppLogger _logger;
    private readonly List<CelestialObjectRowViewModel> _allObjects = [];
    private IReadOnlyList<string> _constellationFilters = [""];
    private CelestialObjectObservation? _selectedObservation;

    public MainViewModel(
        ICelestialRepository repository,
        IAstronomyService astronomy,
        IObservationPlanService planService,
        IAppLogger logger,
        SettingsViewModel settings)
    {
        _repository = repository;
        _astronomy = astronomy;
        _planService = planService;
        _logger = logger;
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
    public ObservableCollection<CelestialObjectRowViewModel> ObservationPlan { get; } = [];

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

    public string ActiveLocationName => Settings.IsManualLocation
        ? "Coordenadas manuales"
        : Settings.SelectedCity?.DisplayName ?? "Selecciona una ciudad";

    public int CatalogCount => _allObjects.Count;

    public int VisibleObjectsCount => _allObjects.Count(item => item.IsVisible);

    /// <summary>
    /// Detalle del objeto seleccionado. Se calcula una sola vez por cambio
    /// (ver <see cref="UpdateSelectedObservation"/>), no en cada lectura del binding.
    /// </summary>
    public CelestialObjectObservation? SelectedObservation => _selectedObservation;
    public bool HasSelectedObject => SelectedObject is not null;

    [ObservableProperty, NotifyDataErrorInfo]
    [Range(-90, 90, ErrorMessage = "La latitud debe estar entre -90 y 90°.")]
    private double latitude;

    [ObservableProperty, NotifyDataErrorInfo]
    [Range(-180, 180, ErrorMessage = "La longitud debe estar entre -180 y 180°.")]
    private double longitude;

    [ObservableProperty] private double elevation;
    [ObservableProperty] private DateTime observationDateTime;

    [ObservableProperty, NotifyDataErrorInfo]
    [Range(0, 90, ErrorMessage = "La altitud mínima debe estar entre 0 y 90°.")]
    private double minimumAltitude;

    /// <summary>Aviso de entrada inválida; mientras exista, la tabla conserva el último cálculo válido.</summary>
    [ObservableProperty] private string? validationMessage;
    [ObservableProperty] private string timeZoneId = TimeZoneInfo.Local.Id;
    [ObservableProperty] private bool onlyVisible;
    [ObservableProperty] private string typeFilter = "All";
    [ObservableProperty] private string constellationFilter = "";
    [ObservableProperty] private CelestialObjectRowViewModel? selectedObject;
    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private string? errorMessage;

    public IReadOnlyList<int> Hours { get; } = [.. Enumerable.Range(0, 24)];
    public IReadOnlyList<int> Minutes { get; } = [.. Enumerable.Range(0, 60)];

    /// <summary>Parte de fecha de <see cref="ObservationDateTime"/>, para el selector de calendario.</summary>
    public DateTime ObservationDate
    {
        get => ObservationDateTime.Date;
        set => ObservationDateTime = value.Date + ObservationDateTime.TimeOfDay;
    }

    /// <summary>Hora (0-23) de <see cref="ObservationDateTime"/>.</summary>
    public int ObservationHour
    {
        get => ObservationDateTime.Hour;
        set => ObservationDateTime = ObservationDateTime.Date.AddHours(value).AddMinutes(ObservationDateTime.Minute);
    }

    /// <summary>Minuto (0-59) de <see cref="ObservationDateTime"/>.</summary>
    public int ObservationMinute
    {
        get => ObservationDateTime.Minute;
        set => ObservationDateTime = ObservationDateTime.Date.AddHours(ObservationDateTime.Hour).AddMinutes(value);
    }

    /// <summary>Desplaza la hora de observación los minutos indicados (p. ej. ±60).</summary>
    [RelayCommand]
    private void ShiftObservationTime(string minutes)
    {
        if (int.TryParse(minutes, out var delta))
        {
            ObservationDateTime = ObservationDateTime.AddMinutes(delta);
        }
    }

    /// <summary>Zona horaria efectiva (la configurada, con reserva a la local).</summary>
    private TimeZoneInfo EffectiveTimeZone => TimeZoneConversion.FindOrLocal(TimeZoneId);

    private DateTime ToUtc(DateTime localWallClock) =>
        TimeZoneConversion.ToUtc(localWallClock, EffectiveTimeZone);

    /// <summary>Inicio (18:00 local) de la noche que contiene <see cref="ObservationDateTime"/>.</summary>
    private DateTime NightStartLocal => ObservationNight.GetNightStartLocal(ObservationDateTime);

    /// <summary>
    /// Mensaje de la primera entrada inválida, o null si todo es válido.
    /// Se comprueba aquí además de con [Range] para no depender del orden en
    /// que el toolkit valida y notifica los cambios.
    /// </summary>
    private string? GetInputError()
    {
        if (double.IsNaN(Latitude) || Latitude is < -90 or > 90)
        {
            return "La latitud debe estar entre -90 y 90°. Revisa la configuración.";
        }

        if (double.IsNaN(Longitude) || Longitude is < -180 or > 180)
        {
            return "La longitud debe estar entre -180 y 180°. Revisa la configuración.";
        }

        if (double.IsNaN(MinimumAltitude) || MinimumAltitude is < 0 or > 90)
        {
            return "La altitud mínima debe estar entre 0 y 90°.";
        }

        return null;
    }

    /// <summary>
    /// Serie de altitud a lo largo de la noche (18:00 → 06:00) en hora local
    /// del observador, para dibujar la curva de tránsito.
    /// </summary>
    public IReadOnlyList<(DateTime LocalTime, double AltitudeDegrees)> GetNightAltitudeCurve()
    {
        if (SelectedObject is null || GetInputError() is not null)
        {
            return [];
        }

        // 25 puntos: 18:00, 18:30, ..., 06:00 (12 horas cada 30 min).
        return _astronomy.GetNightAltitudeSeries(
            SelectedObject.Object,
            new ObserverLocation(Latitude, Longitude, Elevation),
            NightStartLocal,
            EffectiveTimeZone,
            TimeSpan.FromMinutes(30));
    }

    public CelestialObjectObservation? GetObservation(CelestialObjectRowViewModel? item)
    {
        if (item is null || GetInputError() is not null)
        {
            return null;
        }

        var observer = new ObserverLocation(Latitude, Longitude, Elevation);
        var events = _astronomy.GetNightEvents(
            item.Object,
            observer,
            NightStartLocal,
            EffectiveTimeZone,
            MinimumAltitude);

        // Posición en el instante exacto elegido, igual que en la tabla.
        var current = _astronomy.GetHorizontal(item.Object, observer, ToUtc(ObservationDateTime));

        return new CelestialObjectObservation
        {
            Object = item.Object,
            Coordinates = current,
            IsVisible = current.AltitudeDegrees >= MinimumAltitude,
            RiseTimeLocal = events.RiseLocal,
            VisibilityStartTimeLocal = events.ThresholdCrossingLocal,
            BestObservationTimeLocal = events.CulminationLocal,
            SetTimeLocal = events.SetLocal,
            MaximumAltitudeDegrees = events.MaximumAltitudeDegrees
        };
    }

    private void UpdateSelectedObservation()
    {
        _selectedObservation = GetObservation(SelectedObject);
        OnPropertyChanged(nameof(SelectedObservation));
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

            RestoreObservationPlan();
            RecalculateAndFilter();
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or JsonException)
        {
            _logger.Error("No se pudo cargar el catálogo Messier.", ex);
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
        // Vuelve al umbral de la configuración guardada, no a un valor fijo.
        MinimumAltitude = Settings.MinimumAltitude;
        RecalculateAndFilter();
    }

    partial void OnLatitudeChanged(double value) => RecalculateAndFilter();
    partial void OnLongitudeChanged(double value) => RecalculateAndFilter();
    partial void OnElevationChanged(double value) => RecalculateAndFilter();
    partial void OnObservationDateTimeChanged(DateTime value)
    {
        OnPropertyChanged(nameof(ObservationDate));
        OnPropertyChanged(nameof(ObservationHour));
        OnPropertyChanged(nameof(ObservationMinute));
        RecalculateAndFilter();
    }
    partial void OnMinimumAltitudeChanged(double value) => RecalculateAndFilter();
    partial void OnTimeZoneIdChanged(string value) => RecalculateAndFilter();
    partial void OnOnlyVisibleChanged(bool value) => RecalculateAndFilter();
    partial void OnTypeFilterChanged(string value) => RecalculateAndFilter();
    partial void OnConstellationFilterChanged(string value) => RecalculateAndFilter();
    partial void OnSelectedObjectChanged(CelestialObjectRowViewModel? value)
    {
        OnPropertyChanged(nameof(HasSelectedObject));
        OnPropertyChanged(nameof(IsSelectedObjectInObservationPlan));
        UpdateSelectedObservation();
    }

    [RelayCommand]
    private void AddSelectedToObservationPlan()
    {
        if (SelectedObject is not null && !ObservationPlan.Contains(SelectedObject))
        {
            ObservationPlan.Add(SelectedObject);
            OnPropertyChanged(nameof(IsSelectedObjectInObservationPlan));
            SaveObservationPlan();
        }
    }

    [RelayCommand]
    private void RemoveSelectedFromObservationPlan() => RemoveFromObservationPlan(SelectedObject);

    [RelayCommand]
    private void RemoveFromObservationPlan(CelestialObjectRowViewModel? item)
    {
        if (item is not null && ObservationPlan.Remove(item))
        {
            OnPropertyChanged(nameof(IsSelectedObjectInObservationPlan));
            SaveObservationPlan();
        }
    }

    /// <summary>Reconstruye el plan guardado a partir de los ids del catálogo.</summary>
    private void RestoreObservationPlan()
    {
        ObservationPlan.Clear();
        foreach (var id in _planService.Load())
        {
            var item = _allObjects.FirstOrDefault(o => o.Id == id);
            if (item is not null && !ObservationPlan.Contains(item))
            {
                ObservationPlan.Add(item);
            }
        }
    }

    private void SaveObservationPlan()
    {
        try
        {
            _planService.Save(ObservationPlan.Select(item => item.Id));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.Error("No se pudo guardar el plan de observación.", ex);
            ErrorMessage = $"No se pudo guardar el plan: {ex.Message}";
        }
    }

    public bool IsSelectedObjectInObservationPlan =>
        SelectedObject is not null && ObservationPlan.Contains(SelectedObject);

    /// <summary>Aplica la configuración guardada en la pestaña de ajustes.</summary>
    private void OnSettingsSaved(Services.ObserverSettings settings)
    {
        Latitude = settings.LatitudeDegrees;
        Longitude = settings.LongitudeDegrees;
        Elevation = settings.ElevationMeters;
        MinimumAltitude = settings.MinimumAltitude;
        TimeZoneId = settings.TimeZoneId;
        OnPropertyChanged(nameof(ActiveLocationName));
        // Los setters ya disparan RecalculateAndFilter.
    }

    private void RecalculateAndFilter()
    {
        OnPropertyChanged(nameof(LocationSummary));
        OnPropertyChanged(nameof(ActiveLocationName));
        OnPropertyChanged(nameof(CatalogCount));
        OnPropertyChanged(nameof(VisibleObjectsCount));

        // Con una entrada inválida se avisa y se conserva el último cálculo
        // válido, en lugar de vaciar la tabla sin explicación.
        ValidationMessage = GetInputError();
        if (_allObjects.Count == 0 || ValidationMessage is not null)
        {
            return;
        }

        var observer = new ObserverLocation(Latitude, Longitude, Elevation);
        var utc = ToUtc(ObservationDateTime);

        foreach (var item in _allObjects)
        {
            item.UpdatePosition(_astronomy.GetHorizontal(item.Object, observer, utc), MinimumAltitude);
        }

        OnPropertyChanged(nameof(VisibleObjectsCount));

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

        var selection = SelectedObject is not null && Objects.Contains(SelectedObject)
            ? SelectedObject
            : Objects.FirstOrDefault();

        if (!ReferenceEquals(selection, SelectedObject))
        {
            // El cambio de selección ya recalcula el detalle.
            SelectedObject = selection;
        }
        else
        {
            UpdateSelectedObservation();
            OnPropertyChanged(nameof(IsSelectedObjectInObservationPlan));
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
    public CelestialObject Object => _item;

    [ObservableProperty] private double altitudeDegrees;
    [ObservableProperty] private double azimuthDegrees;
    [ObservableProperty] private bool isVisible;

    public string VisibilityStatus => IsVisible ? "Visible" : "No visible";

    partial void OnIsVisibleChanged(bool value) => OnPropertyChanged(nameof(VisibilityStatus));

    public void UpdatePosition(HorizontalCoordinates coordinates, double threshold)
    {
        AltitudeDegrees = coordinates.AltitudeDegrees;
        AzimuthDegrees = coordinates.AzimuthDegrees;
        IsVisible = AltitudeDegrees >= threshold;
    }
}
