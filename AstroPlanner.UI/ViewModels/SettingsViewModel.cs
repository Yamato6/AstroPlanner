using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AstroPlanner.UI.ViewModels;

/// <summary>
/// ViewModel de la pestaña de Configuración: latitud, longitud, elevación,
/// zona horaria y umbral de altitud. Persiste la configuración y notifica
/// al ViewModel principal mediante <see cref="SettingsSaved"/>.
/// </summary>
public partial class SettingsViewModel : ObservableObject
{
    private readonly Services.ISettingsService _settingsService;

    public SettingsViewModel(Services.ISettingsService settingsService)
    {
        _settingsService = settingsService;
        LoadFrom(_settingsService.Load());
    }

    /// <summary>
    /// Se dispara después de guardar; el ViewModel principal la usa para
    /// aplicar la nueva configuración y recalcular.
    /// </summary>
    public event Action<Services.ObserverSettings>? SettingsSaved;

    public IReadOnlyList<TimeZoneInfo> AvailableTimeZones { get; } =
        TimeZoneInfo.GetSystemTimeZones().ToList();

    [ObservableProperty] private double latitude;
    [ObservableProperty] private double longitude;
    [ObservableProperty] private double elevation;
    [ObservableProperty] private string timeZoneId = TimeZoneInfo.Local.Id;
    [ObservableProperty] private double minimumAltitude = 20;
    [ObservableProperty] private string? statusMessage;
    [ObservableProperty] private bool hasError;

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
        Latitude = settings.LatitudeDegrees;
        Longitude = settings.LongitudeDegrees;
        Elevation = settings.ElevationMeters;
        TimeZoneId = settings.TimeZoneId;
        MinimumAltitude = settings.MinimumAltitude;
        OnPropertyChanged(nameof(SelectedTimeZone));
    }
}
