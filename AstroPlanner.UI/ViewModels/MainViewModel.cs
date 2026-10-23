using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AstroPlanner.Core.Algorithms;
using AstroPlanner.Core.Interfaces;
using AstroPlanner.Core.Models;
using AstroPlanner.UI.Services;

namespace AstroPlanner.UI.ViewModels;

public partial class MainViewModel : ObservableValidator, IDisposable
{
    /// <summary>Cada cuánto se actualiza la hora en el modo en vivo.</summary>
    public static readonly TimeSpan LiveUpdateInterval = TimeSpan.FromMinutes(1);

    private readonly ICelestialRepository _repository;
    private readonly IAstronomyService _astronomy;
    private readonly IObservationPlanService _planService;
    private readonly IAppLogger _logger;
    private readonly TimeProvider _time;
    private SynchronizationContext? _uiContext;
    private readonly List<CelestialObjectRowViewModel> _allObjects = [];
    private IReadOnlyList<string> _constellationFilters = [""];
    private CelestialObjectObservation? _selectedObservation;
    private ITimer? _liveTimer;
    private bool _isApplyingLiveTime;
    private bool _isInitializing;

    public MainViewModel(
        ICelestialRepository repository,
        IAstronomyService astronomy,
        IObservationPlanService planService,
        IAppLogger logger,
        SettingsViewModel settings,
        LogbookViewModel logbook,
        TimeProvider? timeProvider = null)
    {
        _repository = repository;
        _astronomy = astronomy;
        _planService = planService;
        _logger = logger;
        _time = timeProvider ?? TimeProvider.System;
        Settings = settings;
        Logbook = logbook;
        Logbook.Changed += RefreshObservedMarks;

        // La configuración persistida manda sobre los valores en runtime. Se
        // asigna todo antes de calcular: con valores a medias (p. ej. la fecha
        // aún en DateTime.MinValue) el cálculo de la noche no es válido.
        _isInitializing = true;
        Latitude = settings.Latitude;
        Longitude = settings.Longitude;
        Elevation = settings.Elevation;
        MinimumAltitude = settings.MinimumAltitude;
        MaximumMagnitude = settings.MaximumMagnitude;
        TimeZoneId = settings.TimeZoneId;
        ObservationDateTime = NowInObserverZone;
        TypeFilter = "All";
        _isInitializing = false;
        RecalculateAndFilter();

        settings.SettingsSaved += OnSettingsSaved;
    }

    /// <summary>Pestaña de Configuración, compartida con la vista.</summary>
    public SettingsViewModel Settings { get; }

    /// <summary>Bitácora de observaciones, compartida con la vista.</summary>
    public LogbookViewModel Logbook { get; }

    /// <summary>Resumen del maratón Messier para la cabecera.</summary>
    public string ObservedSummaryText => $"Messier observados: {Logbook.MessierProgressText}";

    /// <summary>Marca como observados los objetos con alguna entrada en la bitácora.</summary>
    private void RefreshObservedMarks()
    {
        foreach (var item in _allObjects)
        {
            item.IsObserved = Logbook.HasBeenObserved(item.Id);
        }

        OnPropertyChanged(nameof(ObservedSummaryText));
    }

    /// <summary>Borrador de bitácora para el objeto seleccionado a la hora elegida.</summary>
    public LogEntryDraftViewModel? CreateLogDraftForSelected() =>
        SelectedObject is null
            ? null
            : Logbook.CreateDraft(SelectedObject.Id, SelectedObject.CommonName, ObservationDateTime, ActiveLocationName);

    public ObservableCollection<CelestialObjectRowViewModel> Objects { get; } = [];

    /// <summary>Mi plan, ordenado por hora de culminación (orden de observación).</summary>
    public ObservableCollection<PlanItemViewModel> ObservationPlan { get; } = [];

    public IReadOnlyList<string> TypeFilters { get; } =
    [
        "All",
        nameof(CelestialObjectType.Galaxy),
        nameof(CelestialObjectType.OpenCluster),
        nameof(CelestialObjectType.GlobularCluster),
        nameof(CelestialObjectType.PlanetaryNebula),
        nameof(CelestialObjectType.DiffuseNebula),
        nameof(CelestialObjectType.SupernovaRemnant),
        nameof(CelestialObjectType.Unknown),
        nameof(CelestialObjectType.Planet)
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

    /// <summary>Oculta los objetos más débiles que esta magnitud.</summary>
    [ObservableProperty, NotifyDataErrorInfo]
    [Range(-2, 20, ErrorMessage = "La magnitud máxima debe estar entre -2 y 20.")]
    private double maximumMagnitude = 12;

    /// <summary>Texto de búsqueda por id, nombre o constelación.</summary>
    [ObservableProperty] private string searchText = "";

    /// <summary>Modo en vivo: la hora sigue al reloj y se actualiza cada minuto.</summary>
    [ObservableProperty] private bool isLive;

    /// <summary>Aviso de entrada inválida; mientras exista, la tabla conserva el último cálculo válido.</summary>
    [ObservableProperty] private string? validationMessage;
    [ObservableProperty] private string timeZoneId = TimeZoneInfo.Local.Id;
    [ObservableProperty] private bool onlyVisible;
    [ObservableProperty] private string typeFilter = "All";
    [ObservableProperty] private string constellationFilter = "";
    [ObservableProperty] private CelestialObjectRowViewModel? selectedObject;
    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private string? errorMessage;

    /// <summary>Resultado de la última exportación del plan.</summary>
    [ObservableProperty] private string? planStatusMessage;

    /// <summary>Cambia cada vez que hay que redibujar la carta del cielo.</summary>
    [ObservableProperty] private int skyMapVersion;

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

    /// <summary>
    /// Hora actual en la zona horaria del observador (no la del PC: si se
    /// planifica para otra ciudad, "ahora" es la hora de allí).
    /// </summary>
    private DateTime NowInObserverZone
    {
        get
        {
            var now = TimeZoneInfo.ConvertTime(_time.GetUtcNow(), EffectiveTimeZone).DateTime;
            return new DateTime(now.Ticks - now.Ticks % TimeSpan.TicksPerSecond, DateTimeKind.Unspecified);
        }
    }

    [RelayCommand]
    private void SetNow() => ObservationDateTime = NowInObserverZone;

    partial void OnIsLiveChanged(bool value)
    {
        _liveTimer?.Dispose();
        _liveTimer = null;

        if (value)
        {
            // El temporizador dispara en otro hilo; sus cambios se devuelven al
            // hilo de la interfaz a través de este contexto. Se captura aquí (el
            // modo en vivo se activa desde la interfaz) y no en el constructor:
            // el ViewModel puede crearse antes de que exista el contexto de WPF,
            // y entonces el cambio llegaría desde otro hilo y tumbaría la app.
            _uiContext = SynchronizationContext.Current;
            ApplyLiveTime();
            _liveTimer = _time.CreateTimer(_ => OnLiveTimerTick(), null, LiveUpdateInterval, LiveUpdateInterval);
        }
    }

    private void OnLiveTimerTick()
    {
        if (_uiContext is null || SynchronizationContext.Current == _uiContext)
        {
            ApplyLiveTime();
        }
        else
        {
            _uiContext.Post(_ => ApplyLiveTime(), null);
        }
    }

    private void ApplyLiveTime()
    {
        if (!IsLive)
        {
            return;
        }

        _isApplyingLiveTime = true;
        try
        {
            ObservationDateTime = NowInObserverZone;
        }
        finally
        {
            _isApplyingLiveTime = false;
        }
    }

    public void Dispose()
    {
        _liveTimer?.Dispose();
        _liveTimer = null;
        GC.SuppressFinalize(this);
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

    /// <summary>Resumen de la noche para la cabecera del plan impreso.</summary>
    public string PlanSummaryText
    {
        get
        {
            var night = ObservationNight.GetNightStartLocal(ObservationDateTime);
            var parts = new List<string>
            {
                $"Noche del {night:dd/MM/yyyy} al {night.AddDays(1):dd/MM/yyyy}",
                ActiveLocationName
            };
            if (_night is { HasAstronomicalDarkness: true })
            {
                parts.Add($"Noche oscura {DarkNightText}");
            }

            if (_moon is not null)
            {
                parts.Add($"Luna {MoonIlluminationText} ({DescribeMoonPhase(_moon.IlluminatedFraction, _moon.IsWaxing)})");
            }

            parts.Add($"Altitud mínima {MinimumAltitude:0}°");
            return string.Join(" · ", parts);
        }
    }

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
        OnPropertyChanged(nameof(PlanSummaryText));
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

        if (double.IsNaN(MaximumMagnitude) || MaximumMagnitude is < -2 or > 20)
        {
            return "La magnitud máxima debe estar entre -2 y 20.";
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
        var moonInterferes = !item.IsPlanet
            && _moon is not null && moonSeparation is not null && MoonInterferes(_moon, moonSeparation.Value);
        var planet = _astronomy.GetPlanetPosition(item.Object, utc);

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
            Magnitude = _astronomy.GetApparentMagnitude(item.Object, utc),
            Planet = planet,
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

    // ─── Carta del cielo ────────────────────────────────────────────────

    /// <summary>
    /// Objetos sobre el horizonte que pasan los filtros, para la carta del cielo.
    /// </summary>
    public IReadOnlyList<SkyMapPoint> GetSkyMapPoints()
    {
        var planIds = ObservationPlan.Select(item => item.Id).ToHashSet();
        return Objects
            .Where(item => item.AltitudeDegrees > 0)
            .Select(item => new SkyMapPoint(
                item.Id,
                item.AltitudeDegrees,
                item.AzimuthDegrees,
                item.ApparentMagnitude,
                item.IsVisible,
                planIds.Contains(item.Id),
                ReferenceEquals(item, SelectedObject),
                item.IsPlanet))
            .ToList();
    }

    /// <summary>
    /// Selecciona el objeto de la carta más cercano a (x, y), en unidades de la
    /// proyección (grados), si está a menos de <paramref name="maximumDistance"/>.
    /// </summary>
    public bool SelectNearestOnSkyMap(double x, double y, double maximumDistance = 6)
    {
        var nearest = GetSkyMapPoints()
            .Select(point => (point.Id, Distance: Math.Sqrt(
                Math.Pow(point.Position.X - x, 2) + Math.Pow(point.Position.Y - y, 2))))
            .Where(candidate => candidate.Distance <= maximumDistance)
            .OrderBy(candidate => candidate.Distance)
            .FirstOrDefault();

        if (nearest.Id is null)
        {
            return false;
        }

        SelectedObject = Objects.First(item => item.Id == nearest.Id);
        return true;
    }

    private void InvalidateSkyMap() => SkyMapVersion++;

    // ─── Carga y filtros ────────────────────────────────────────────────

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
            RefreshObservedMarks();
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
        SearchText = "";
        // Vuelve a los valores de la configuración guardada, no a valores fijos.
        MaximumMagnitude = Settings.MaximumMagnitude;
        MinimumAltitude = Settings.MinimumAltitude;
        RecalculateAndFilter();
    }

    partial void OnLatitudeChanged(double value) => RecalculateAndFilter();
    partial void OnLongitudeChanged(double value) => RecalculateAndFilter();
    partial void OnElevationChanged(double value) => RecalculateAndFilter();

    partial void OnObservationDateTimeChanged(DateTime value)
    {
        // Elegir una hora a mano sale del modo en vivo.
        if (IsLive && !_isApplyingLiveTime)
        {
            IsLive = false;
        }

        OnPropertyChanged(nameof(ObservationDate));
        OnPropertyChanged(nameof(ObservationHour));
        OnPropertyChanged(nameof(ObservationMinute));
        RecalculateAndFilter();
    }

    partial void OnMinimumAltitudeChanged(double value) => RecalculateAndFilter();

    partial void OnTimeZoneIdChanged(string value)
    {
        // En vivo, "ahora" depende de la zona horaria del observador.
        if (IsLive)
        {
            ApplyLiveTime();
        }

        RecalculateAndFilter();
    }

    // Estos filtros no cambian posiciones: solo se vuelve a filtrar la tabla.
    partial void OnOnlyVisibleChanged(bool value) => ApplyFilter();
    partial void OnTypeFilterChanged(string value) => ApplyFilter();
    partial void OnConstellationFilterChanged(string value) => ApplyFilter();
    partial void OnSearchTextChanged(string value) => ApplyFilter();
    partial void OnMaximumMagnitudeChanged(double value) => ApplyFilter();

    partial void OnSelectedObjectChanged(CelestialObjectRowViewModel? value)
    {
        OnPropertyChanged(nameof(HasSelectedObject));
        OnPropertyChanged(nameof(IsSelectedObjectInObservationPlan));
        UpdateSelectedObservation();
        InvalidateSkyMap();
    }

    // ─── Mi plan ────────────────────────────────────────────────────────

    [RelayCommand]
    private void AddSelectedToObservationPlan()
    {
        if (SelectedObject is not null && !IsSelectedObjectInObservationPlan)
        {
            ObservationPlan.Add(new PlanItemViewModel(SelectedObject, "", OnPlanNoteChanged));
            UpdatePlanSchedule();
            OnPropertyChanged(nameof(IsSelectedObjectInObservationPlan));
            SaveObservationPlan();
            InvalidateSkyMap();
        }
    }

    [RelayCommand]
    private void RemoveSelectedFromObservationPlan() =>
        RemoveFromObservationPlan(ObservationPlan.FirstOrDefault(item => item.Row == SelectedObject));

    [RelayCommand]
    private void RemoveFromObservationPlan(PlanItemViewModel? item)
    {
        if (item is not null && ObservationPlan.Remove(item))
        {
            OnPropertyChanged(nameof(IsSelectedObjectInObservationPlan));
            SaveObservationPlan();
            InvalidateSkyMap();
        }
    }

    public bool IsSelectedObjectInObservationPlan =>
        SelectedObject is not null && ObservationPlan.Any(item => item.Row == SelectedObject);

    private void OnPlanNoteChanged(PlanItemViewModel item) => SaveObservationPlan();

    /// <summary>Reconstruye el plan guardado a partir de los ids del catálogo.</summary>
    private void RestoreObservationPlan()
    {
        ObservationPlan.Clear();
        foreach (var entry in _planService.Load())
        {
            var row = _allObjects.FirstOrDefault(o => o.Id == entry.Id);
            if (row is not null && ObservationPlan.All(item => item.Row != row))
            {
                ObservationPlan.Add(new PlanItemViewModel(row, entry.Note, OnPlanNoteChanged));
            }
        }
    }

    private void SaveObservationPlan()
    {
        try
        {
            _planService.Save(ObservationPlan.Select(item => new ObservationPlanEntry(item.Id, item.Note)));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.Error("No se pudo guardar el plan de observación.", ex);
            ErrorMessage = $"No se pudo guardar el plan: {ex.Message}";
        }
    }

    /// <summary>
    /// Calcula la culminación de cada objeto del plan en la noche actual y
    /// ordena el plan por esa hora, que es el orden en que conviene observar.
    /// </summary>
    private void UpdatePlanSchedule()
    {
        if (_night is null || GetInputError() is not null || ObservationPlan.Count == 0)
        {
            return;
        }

        var observer = new ObserverLocation(Latitude, Longitude, Elevation);
        foreach (var item in ObservationPlan)
        {
            var events = _astronomy.GetNightEvents(
                item.Row.Object,
                observer,
                _night.WindowStartLocal,
                _night.WindowEndLocal,
                EffectiveTimeZone,
                MinimumAltitude);
            item.CulminationLocal = events.CulminationLocal;
            item.MaximumAltitudeDegrees = events.MaximumAltitudeDegrees;
        }

        var ordered = ObservationPlan.OrderBy(item => item.CulminationLocal).ThenBy(item => item.Id).ToList();
        for (var index = 0; index < ordered.Count; index++)
        {
            var current = ObservationPlan.IndexOf(ordered[index]);
            if (current != index)
            {
                ObservationPlan.Move(current, index);
            }
        }
    }

    /// <summary>Contenido CSV del plan (ver <see cref="PlanExport"/>).</summary>
    public string BuildObservationPlanCsv(CultureInfo culture) => PlanExport.BuildCsv(ObservationPlan, culture);

    /// <summary>Exporta el plan a CSV (UTF-8 con BOM para que Excel respete los acentos).</summary>
    public bool ExportObservationPlan(string filePath)
    {
        try
        {
            File.WriteAllText(filePath, BuildObservationPlanCsv(CultureInfo.CurrentCulture), new UTF8Encoding(true));
            PlanStatusMessage = $"Plan exportado a {Path.GetFileName(filePath)}.";
            _logger.Info($"Plan exportado a {filePath}.");
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.Error("No se pudo exportar el plan.", ex);
            PlanStatusMessage = $"No se pudo exportar el plan: {ex.Message}";
            return false;
        }
    }

    /// <summary>Aplica la configuración guardada en la pestaña de ajustes.</summary>
    private void OnSettingsSaved(Services.ObserverSettings settings)
    {
        Latitude = settings.LatitudeDegrees;
        Longitude = settings.LongitudeDegrees;
        Elevation = settings.ElevationMeters;
        MinimumAltitude = settings.MinimumAltitude;
        MaximumMagnitude = settings.MaximumMagnitude;
        TimeZoneId = settings.TimeZoneId;
        OnPropertyChanged(nameof(ActiveLocationName));
        // Los setters ya disparan RecalculateAndFilter.
    }

    /// <summary>Recalcula posiciones, noche y Luna, y después vuelve a filtrar.</summary>
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
                // Los planetas son lo bastante brillantes para que la Luna no los oculte.
                !item.IsPlanet && MoonInterferes(moon, separation),
                _astronomy.GetApparentMagnitude(item.Object, utc));
        }

        OnPropertyChanged(nameof(VisibleObjectsCount));
        UpdatePlanSchedule();
        ApplyFilter();
    }

    /// <summary>Filtra y ordena la tabla con las posiciones ya calculadas.</summary>
    private void ApplyFilter()
    {
        if (_isInitializing || _allObjects.Count == 0)
        {
            return;
        }

        ValidationMessage = GetInputError();
        if (ValidationMessage is not null)
        {
            return;
        }

        var filtered = _allObjects
            .Where(item =>
                (!OnlyVisible || item.IsVisible)
                && (TypeFilter == "All" || item.Type.ToString() == TypeFilter)
                && (string.IsNullOrWhiteSpace(ConstellationFilter)
                    || item.Constellation.Equals(ConstellationFilter, StringComparison.OrdinalIgnoreCase))
                && item.ApparentMagnitude <= MaximumMagnitude
                && CatalogSearch.Matches(item, SearchText))
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
            // El cambio de selección ya recalcula el detalle y la carta.
            SelectedObject = selection;
        }
        else
        {
            UpdateSelectedObservation();
            OnPropertyChanged(nameof(IsSelectedObjectInObservationPlan));
            InvalidateSkyMap();
        }
    }
}

public partial class CelestialObjectRowViewModel : ObservableObject
{
    private readonly CelestialObject _item;

    public CelestialObjectRowViewModel(CelestialObject item)
    {
        _item = item;
        apparentMagnitude = item.ApparentMagnitude;
    }

    public string Id => _item.Id;
    public string CommonName => _item.CommonName;
    public string Constellation => _item.Constellation;
    public CelestialObjectType Type => _item.Type;
    public EquatorialCoordinates Coordinates => _item.Coordinates;
    public CelestialObject Object => _item;
    public bool IsPlanet => _item.IsPlanet;

    /// <summary>Magnitud aparente: fija para el cielo profundo; en los planetas cambia con la fecha.</summary>
    [ObservableProperty] private double apparentMagnitude;

    /// <summary>True si hay alguna observación del objeto en la bitácora.</summary>
    [ObservableProperty] private bool isObserved;

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
        bool moonInterferes,
        double apparentMagnitude)
    {
        AltitudeDegrees = coordinates.AltitudeDegrees;
        AzimuthDegrees = coordinates.AzimuthDegrees;
        IsVisible = AltitudeDegrees >= threshold;
        MoonSeparationDegrees = moonSeparation;
        MoonInterferes = moonInterferes;
        ApparentMagnitude = apparentMagnitude;
    }
}
