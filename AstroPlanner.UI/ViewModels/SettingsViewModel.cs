using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AstroPlanner.Core.Interfaces;
using AstroPlanner.Core.Models;

namespace AstroPlanner.UI.ViewModels;

/// <summary>
/// ViewModel de la pestaña de Configuración: latitud, longitud, elevación,
/// zona horaria y umbral de altitud. Persiste la configuración y notifica
/// al ViewModel principal mediante <see cref="SettingsSaved"/>.
/// </summary>
public partial class SettingsViewModel : ObservableValidator
{
    private readonly Services.ISettingsService _settingsService;
    private readonly ICityRepository _cityRepository;
    private readonly Services.IAppLogger _logger;

    public SettingsViewModel(
        Services.ISettingsService settingsService,
        ICityRepository cityRepository,
        Services.IAppLogger logger)
    {
        _settingsService = settingsService;
        _cityRepository = cityRepository;
        _logger = logger;
        LoadFrom(_settingsService.Load());
    }

    /// <summary>
    /// Se dispara después de guardar; el ViewModel principal la usa para
    /// aplicar la nueva configuración y recalcular.
    /// </summary>
    public event Action<Services.ObserverSettings>? SettingsSaved;

    public IReadOnlyList<TimeZoneInfo> AvailableTimeZones { get; } =
        TimeZoneInfo.GetSystemTimeZones().ToList();

    public IReadOnlyList<City> Cities { get; private set; } = Array.Empty<City>();

    [ObservableProperty, NotifyDataErrorInfo]
    [Range(-90, 90, ErrorMessage = "La latitud debe estar entre -90 y 90°.")]
    private double latitude;

    [ObservableProperty, NotifyDataErrorInfo]
    [Range(-180, 180, ErrorMessage = "La longitud debe estar entre -180 y 180°.")]
    private double longitude;

    [ObservableProperty, NotifyDataErrorInfo]
    [Range(-500, 9000, ErrorMessage = "La elevación debe estar entre -500 y 9000 m.")]
    private double elevation;

    [ObservableProperty] private string timeZoneId = TimeZoneInfo.Local.Id;

    [ObservableProperty, NotifyDataErrorInfo]
    [Range(0, 90, ErrorMessage = "La altitud mínima debe estar entre 0 y 90°.")]
    private double minimumAltitude = 20;

    [ObservableProperty, NotifyDataErrorInfo]
    [Range(-2, 20, ErrorMessage = "La magnitud máxima debe estar entre -2 y 20.")]
    private double maximumMagnitude = 12;

    /// <summary>Consultar el pronóstico de nubes (Open-Meteo).</summary>
    [ObservableProperty] private bool weatherForecastEnabled = true;

    /// <summary>Buscar una versión nueva al iniciar.</summary>
    [ObservableProperty] private bool checkForUpdates = true;

    [ObservableProperty] private City? selectedCity;
    [ObservableProperty] private bool isManualLocation;
    [ObservableProperty] private string? statusMessage;
    [ObservableProperty] private bool hasError;

    public bool IsCitySelectionEnabled => !IsManualLocation;

    public bool IsManualFieldsEnabled => IsManualLocation;

    /// <summary>Zona horaria seleccionada, para enlazar directamente con un ComboBox.</summary>
    public TimeZoneInfo? SelectedTimeZone
    {
        get => AvailableTimeZones.FirstOrDefault(z => z.Id == TimeZoneId)
               ?? TimeZoneInfo.Local;
        set
        {
            if (value is null) return;
            TimeZoneId = value.Id;
            OnPropertyChanged();
        }
    }

    [RelayCommand]
    private async Task LoadCitiesAsync()
    {
        try
        {
            Cities = await _cityRepository.GetAllAsync();
            OnPropertyChanged(nameof(Cities));

            if (!IsManualLocation && SelectedCity is null)
            {
                SelectedCity = Cities.FirstOrDefault(city => city.Id == _settingsService.Load().CityId)
                    ?? Cities.FirstOrDefault();
            }
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or JsonException)
        {
            _logger.Error("No se pudo cargar el catálogo de ciudades.", ex);
            HasError = true;
            StatusMessage = $"No se pudo cargar el catálogo de ciudades: {ex.Message}";
        }
    }

    [RelayCommand]
    private void UseManualLocation()
    {
        IsManualLocation = true;
        SelectedCity = null;
        StatusMessage = "Modo manual activado.";
        HasError = false;
    }

    partial void OnSelectedCityChanged(City? value)
    {
        if (value is null || IsManualLocation) return;

        Latitude = value.LatitudeDegrees;
        Longitude = value.LongitudeDegrees;
        Elevation = value.ElevationMeters;
        TimeZoneId = value.TimeZoneId;
        OnPropertyChanged(nameof(SelectedTimeZone));
    }

    partial void OnIsManualLocationChanged(bool value)
    {
        OnPropertyChanged(nameof(IsCitySelectionEnabled));
        OnPropertyChanged(nameof(IsManualFieldsEnabled));
        if (value) return;
        if (SelectedCity is null && Cities.Count > 0)
        {
            SelectedCity = Cities.FirstOrDefault();
        }
    }

    [RelayCommand]
    private void Save()
    {
        try
        {
            ValidateAllProperties();
            if (HasErrors)
            {
                HasError = true;
                StatusMessage = GetErrors().First().ErrorMessage;
                return;
            }

            var settings = new Services.ObserverSettings
            {
                CityId = IsManualLocation ? null : SelectedCity?.Id,
                IsManualLocation = IsManualLocation,
                LatitudeDegrees = Latitude,
                LongitudeDegrees = Longitude,
                ElevationMeters = Elevation,
                TimeZoneId = TimeZoneId,
                MinimumAltitude = MinimumAltitude,
                MaximumMagnitude = MaximumMagnitude,
                WeatherForecastEnabled = WeatherForecastEnabled,
                CheckForUpdates = CheckForUpdates
            };

            _settingsService.Save(settings);
            HasError = false;
            StatusMessage = "Configuración guardada.";
            SettingsSaved?.Invoke(settings);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.Error("No se pudo guardar la configuración.", ex);
            HasError = true;
            StatusMessage = $"No se pudo guardar: {ex.Message}";
        }
    }

    [RelayCommand]
    private void LoadDefaults()
    {
        LoadFrom(new Services.ObserverSettings());
        StatusMessage = "Valores por defecto cargados (sin guardar).";
        HasError = false;
    }

    /// <summary>Descarta los cambios no guardados y vuelve a la configuración persistida.</summary>
    public void RevertChanges() => Apply(_settingsService.Load());

    /// <summary>Aplica una configuración a las propiedades observables.</summary>
    public void Apply(Services.ObserverSettings settings)
    {
        LoadFrom(settings);
        StatusMessage = null;
        HasError = false;
    }

    private void LoadFrom(Services.ObserverSettings settings)
    {
        IsManualLocation = settings.IsManualLocation;
        Latitude = settings.LatitudeDegrees;
        Longitude = settings.LongitudeDegrees;
        Elevation = settings.ElevationMeters;
        TimeZoneId = settings.TimeZoneId;
        MinimumAltitude = settings.MinimumAltitude;
        MaximumMagnitude = settings.MaximumMagnitude;
        WeatherForecastEnabled = settings.WeatherForecastEnabled;
        CheckForUpdates = settings.CheckForUpdates;
        // Si el catálogo de ciudades ya está cargado se recupera la ciudad guardada;
        // si no, LoadCitiesAsync la seleccionará al cargarlo.
        SelectedCity = settings.IsManualLocation
            ? null
            : Cities.FirstOrDefault(city => city.Id == settings.CityId);
        OnPropertyChanged(nameof(SelectedTimeZone));
    }
}
