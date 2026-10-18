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
public partial class SettingsViewModel : ObservableObject
{
    private readonly Services.ISettingsService _settingsService;
    private readonly ICityRepository _cityRepository;

    public SettingsViewModel(Services.ISettingsService settingsService, ICityRepository cityRepository)
    {
        _settingsService = settingsService;
        _cityRepository = cityRepository;
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

    [ObservableProperty] private double latitude;
    [ObservableProperty] private double longitude;
    [ObservableProperty] private double elevation;
    [ObservableProperty] private string timeZoneId = TimeZoneInfo.Local.Id;
    [ObservableProperty] private double minimumAltitude = 20;
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
            if (Latitude is < -90 or > 90 || Longitude is < -180 or > 180)
            {
                HasError = true;
                StatusMessage = "Latitud deben estar entre -90 y 90; longitud entre -180 y 180.";
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
                MinimumAltitude = MinimumAltitude
            };

            _settingsService.Save(settings);
            HasError = false;
            StatusMessage = "Configuración guardada.";
            SettingsSaved?.Invoke(settings);
        }
        catch (IOException ex)
        {
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
        SelectedCity = null;
        OnPropertyChanged(nameof(SelectedTimeZone));
    }
}
