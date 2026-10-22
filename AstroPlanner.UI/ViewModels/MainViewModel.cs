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

        // La configuración persistida manda sobre los valores en runtime. Se
        // asigna todo antes de calcular: con valores a medias (p. ej. la fecha
        // aún en DateTime.MinValue) el cálculo de la noche no es válido.
        _isInitializing = true;
        Latitude = settings.Latitude;
        Longitude = settings.Longitude;
        Elevation = settings.Elevation;
        MinimumAltitude = settings.MinimumAltitude;
        TimeZoneId = settings.TimeZoneId;
        ObservationDateTime = DateTime.Now;
        TypeFilter = "All";
        _isInitializing = false;
        RecalculateAndFilter();

        settings.SettingsSaved += OnSettingsSaved;
    }

    private bool _isInitializing;

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

    /// <summary>Distancia a la Luna por debajo de la cual se avisa de interferencia.</summary>
    public const double MoonInterferenceSeparationDegrees = 30;

    /// <summary>Iluminación mínima de la Luna (25 %) para que su brillo moleste.</summary>
    public const double MoonInterferenceMinimumIllumination = 0.25;

    private NightInfo? _night;
    private MoonState? _moon;

    /// <summary>Noche de observación (puesta → salida del Sol) de la fecha elegida.</summary>
    public NightInfo? Night => _night;

    /// <summary>La Luna en el instante elegido.</summary>
    public MoonState? Moon => _moon;

    /// <summary>Día, crepúsculo o noche oscura en el instante elegido.</summary>
    [ObservableProperty] private SkyCondition skyCondition = SkyCondition.Dark;

    public string ChartTitle => _night is null
        ? "Altitud durante la noche"
        : $"Altitud durante la noche ({_night.WindowStartLocal:HH:mm} → {_night.WindowEndLocal:HH:mm})";

    public string DarkNightText => _night switch
    {
        null => "—",
        { HasAstronomicalDarkness: true } => $"{_night.AstronomicalDuskLocal:HH:mm} → {_night.AstronomicalDawnLocal:HH:mm}",
        _ => "Sin noche oscura"
    };

    public string SunTimesText => _night switch
    {
        null => "",
        { IsFallbackWindow: true } => "El Sol no se pone o no sale: ventana 18:00 → 06:00",
        _ => $"Puesta {_night.SunsetLocal:HH:mm} · Salida {_night.SunriseLocal:HH:mm}"
    };

    public string MoonIlluminationText => _moon is null ? "—" : $"{_moon.IlluminatedFraction * 100:0} %";

    public string MoonDetailText => _moon is null
        ? ""
        : $"{DescribeMoonPhase(_moon.IlluminatedFraction, _moon.IsWaxing)} · "
          + (_moon.IsAboveHorizon ? $"{_moon.Horizontal.AltitudeDegrees:0}° de altura" : "bajo el horizonte");

    /// <summary>Aviso cuando a la hora elegida el cielo no está oscuro; null de noche.</summary>
    public string? SkyBanner => (SkyCondition, _night) switch
    {
        (_, { HasAstronomicalDarkness: false }) when SkyCondition != SkyCondition.Day =>
            "En esta fecha y lugar el Sol no baja de -18°: el cielo nunca está totalmente oscuro.",
        (SkyCondition.Day, { HasAstronomicalDarkness: true }) =>
            $"Es de día a esta hora. La noche oscura empieza a las {_night!.AstronomicalDuskLocal:HH:mm}.",
        (SkyCondition.Day, _) => "Es de día a esta hora.",
        (SkyCondition.Twilight, _) =>
            $"Crepúsculo: el cielo aún no está oscuro (noche oscura de {_night!.AstronomicalDuskLocal:HH:mm} "
            + $"a {_night.AstronomicalDawnLocal:HH:mm}).",
        _ => null
    };

    /// <summary>Nombre de la fase lunar a partir de la fracción iluminada.</summary>
    public static string DescribeMoonPhase(double illuminatedFraction, bool isWaxing) => illuminatedFraction switch
    {
        < 0.03 => "Luna nueva",
        > 0.97 => "Luna llena",
        < 0.47 => isWaxing ? "Creciente" : "Menguante",
        <= 0.53 => isWaxing ? "Cuarto creciente" : "Cuarto menguante",
        _ => isWaxing ? "Gibosa creciente" : "Gibosa menguante"
    };

    /// <summary>
    /// True si la Luna está sobre el horizonte, bastante iluminada y a menos de
    /// <see cref="MoonInterferenceSeparationDegrees"/> del objeto.
    /// </summary>
    public static bool MoonInterferes(MoonState moon, double separationDegrees) =>
        moon.IsAboveHorizon
        && moon.IlluminatedFraction >= MoonInterferenceMinimumIllumination
        && separationDegrees < MoonInterferenceSeparationDegrees;

    /// <summary>Recalcula noche, Luna y estado del cielo, y notifica los textos derivados.</summary>
    private void UpdateSky(ObserverLocation observer, DateTime utc)
    {
        _night = _astronomy.GetNightInfo(observer, ObservationDateTime, EffectiveTimeZone);
        _moon = _astronomy.GetMoon(observer, utc);
        SkyCondition = _astronomy.GetSkyCondition(observer, utc);

        OnPropertyChanged(nameof(Night));
        OnPropertyChanged(nameof(Moon));
        OnPropertyChanged(nameof(ChartTitle));
        OnPropertyChanged(nameof(DarkNightText));
        OnPropertyChanged(nameof(SunTimesText));
        OnPropertyChanged(nameof(MoonIlluminationText));
        OnPropertyChanged(nameof(MoonDetailText));
        OnPropertyChanged(nameof(SkyBanner));
    }

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

    private static readonly TimeSpan CurveStep = TimeSpan.FromMinutes(20);

    /// <summary>
    /// Serie de altitud del objeto seleccionado de la puesta a la salida del
    /// Sol, en hora local del observador, para dibujar la curva de tránsito.
    /// </summary>
    public IReadOnlyList<(DateTime LocalTime, double AltitudeDegrees)> GetNightAltitudeCurve()
    {
        if (SelectedObject is null || _night is null || GetInputError() is not null)
        {
            return [];
        }

        return _astronomy.GetNightAltitudeSeries(
            SelectedObject.Object,
            new ObserverLocation(Latitude, Longitude, Elevation),
            _night.WindowStartLocal,
            _night.WindowEndLocal,
            EffectiveTimeZone,
            CurveStep);
    }

    /// <summary>Serie de altitud de la Luna en la misma ventana que <see cref="GetNightAltitudeCurve"/>.</summary>
    public IReadOnlyList<(DateTime LocalTime, double AltitudeDegrees)> GetMoonAltitudeCurve()
    {
        if (_night is null || GetInputError() is not null)
        {
            return [];
        }

        return _astronomy.GetMoonAltitudeSeries(
            new ObserverLocation(Latitude, Longitude, Elevation),
            _night.WindowStartLocal,
            _night.WindowEndLocal,
            EffectiveTimeZone,
            CurveStep);
    }

    public CelestialObjectObservation? GetObservation(CelestialObjectRowViewModel? item)
    {
        if (item is null || _night is null || GetInputError() is not null)
        {
            return null;
        }

        var observer = new ObserverLocation(Latitude, Longitude, Elevation);
        var events = _astronomy.GetNightEvents(
            item.Object,
            observer,
            _night.WindowStartLocal,
            _night.WindowEndLocal,
            EffectiveTimeZone,
            MinimumAltitude);

        // Posición en el instante exacto elegido, igual que en la tabla.
        var utc = ToUtc(ObservationDateTime);
        var current = _astronomy.GetHorizontal(item.Object, observer, utc);
        double? moonSeparation = _moon is null ? null : _astronomy.GetMoonSeparationDegrees(item.Object, _moon, utc);
        var moonInterferes = _moon is not null && moonSeparation is not null && MoonInterferes(_moon, moonSeparation.Value);

        return new CelestialObjectObservation
        {
            Object = item.Object,
            Coordinates = current,
            IsVisible = current.AltitudeDegrees >= MinimumAltitude,
            RiseTimeLocal = events.RiseLocal,
            VisibilityStartTimeLocal = events.ThresholdCrossingLocal,
            BestObservationTimeLocal = events.CulminationLocal,
            SetTimeLocal = events.SetLocal,
            MaximumAltitudeDegrees = events.MaximumAltitudeDegrees,
            MoonSeparationDegrees = moonSeparation,
            MoonWarning = moonInterferes
                ? $"La Luna ({_moon!.IlluminatedFraction * 100:0} % iluminada) está a {moonSeparation:0}°: "
                  + "su brillo dificulta la observación."
                : null
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
        if (_isInitializing)
        {
            return;
        }

        OnPropertyChanged(nameof(LocationSummary));
        OnPropertyChanged(nameof(ActiveLocationName));
        OnPropertyChanged(nameof(CatalogCount));
        OnPropertyChanged(nameof(VisibleObjectsCount));

        // Con una entrada inválida se avisa y se conserva el último cálculo
        // válido, en lugar de vaciar la tabla sin explicación.
        ValidationMessage = GetInputError();
        if (ValidationMessage is not null)
        {
            return;
        }

        var observer = new ObserverLocation(Latitude, Longitude, Elevation);
        var utc = ToUtc(ObservationDateTime);

        // La noche y la Luna no dependen del catálogo: la cabecera se rellena aunque aún no haya cargado.
        UpdateSky(observer, utc);
        if (_allObjects.Count == 0)
        {
            return;
        }

        var moon = _moon!;
        foreach (var item in _allObjects)
        {
            var separation = _astronomy.GetMoonSeparationDegrees(item.Object, moon, utc);
            item.UpdatePosition(
                _astronomy.GetHorizontal(item.Object, observer, utc),
                MinimumAltitude,
                separation,
                MoonInterferes(moon, separation));
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

    /// <summary>Distancia angular a la Luna (grados).</summary>
    [ObservableProperty] private double moonSeparationDegrees;

    /// <summary>True si la Luna, brillante y cercana, dificulta observar el objeto.</summary>
    [ObservableProperty] private bool moonInterferes;

    public string VisibilityStatus => IsVisible ? "Visible" : "No visible";

    partial void OnIsVisibleChanged(bool value) => OnPropertyChanged(nameof(VisibilityStatus));

    public void UpdatePosition(
        HorizontalCoordinates coordinates,
        double threshold,
        double moonSeparation,
        bool moonInterferes)
    {
        AltitudeDegrees = coordinates.AltitudeDegrees;
        AzimuthDegrees = coordinates.AzimuthDegrees;
        IsVisible = AltitudeDegrees >= threshold;
        MoonSeparationDegrees = moonSeparation;
        MoonInterferes = moonInterferes;
    }
}
