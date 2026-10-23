using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AstroPlanner.Core.Models;
using AstroPlanner.UI.Services;

namespace AstroPlanner.UI.ViewModels;

/// <summary>
/// Funciones que usan internet: pronóstico de nubes y aviso de nueva versión.
/// Ninguna es imprescindible: si fallan, la app sigue funcionando y lo indica.
/// </summary>
public partial class MainViewModel
{
    /// <summary>El pronóstico se reutiliza durante este tiempo para la misma ubicación.</summary>
    public static readonly TimeSpan WeatherCacheDuration = TimeSpan.FromHours(1);

    /// <summary>Tras un fallo, no se reintenta antes de este tiempo (salvo que cambie la ubicación).</summary>
    public static readonly TimeSpan WeatherRetryDelay = TimeSpan.FromMinutes(5);

    private WeatherForecast? _forecast;
    private string? _weatherError;
    private bool _isFetchingWeather;
    private DateTime _lastFailedWeatherAttemptUtc = DateTime.MinValue;
    private (double Latitude, double Longitude)? _lastFailedWeatherLocation;

    /// <summary>Nubosidad media de la noche oscura, p. ej. "20 %".</summary>
    [ObservableProperty] private string cloudCoverText = "—";

    /// <summary>Descripción y hora más despejada, o el motivo de que no haya pronóstico.</summary>
    [ObservableProperty] private string cloudDetailText = "";

    /// <summary>Cambia cada vez que hay que redibujar las nubes en el gráfico.</summary>
    [ObservableProperty] private int weatherVersion;

    /// <summary>Versión nueva publicada, si la hay.</summary>
    [ObservableProperty, NotifyPropertyChangedFor(nameof(UpdateBannerText), nameof(HasUpdate))]
    private UpdateInfo? availableUpdate;

    public bool HasUpdate => AvailableUpdate is not null;

    public string? UpdateBannerText => AvailableUpdate is null
        ? null
        : $"Hay una versión nueva de AstroPlanner: {AvailableUpdate.Name}.";

    /// <summary>Pronóstico vigente para la ubicación actual, o null.</summary>
    public WeatherForecast? Forecast => ForecastForCurrentLocation();

    private (double Latitude, double Longitude) RoundedLocation => (Math.Round(Latitude, 2), Math.Round(Longitude, 2));

    private WeatherForecast? ForecastForCurrentLocation() =>
        _weatherEnabled && _forecast is not null
        && (Math.Round(_forecast.Latitude, 2), Math.Round(_forecast.Longitude, 2)) == RoundedLocation
            ? _forecast
            : null;

    /// <summary>Nubosidad (%) pronosticada para una hora local; null si no hay pronóstico para ella.</summary>
    private int? CloudCoverAtLocal(DateTime? localTime) =>
        localTime is { } local ? Forecast?.At(ToUtc(local))?.CloudCoverPercent : null;

    /// <summary>
    /// Nubosidad por horas dentro de la ventana nocturna, en hora local, para el gráfico.
    /// </summary>
    public IReadOnlyList<(DateTime StartLocal, DateTime EndLocal, int CloudCoverPercent)> GetNightCloudCover()
    {
        if (Forecast is not { } forecast || _night is null)
        {
            return [];
        }

        var zone = EffectiveTimeZone;
        return forecast
            .Between(ToUtc(_night.WindowStartLocal), ToUtc(_night.WindowEndLocal))
            .Select(h => (
                Max(TimeZoneInfo.ConvertTimeFromUtc(h.TimeUtc, zone), _night.WindowStartLocal),
                Min(TimeZoneInfo.ConvertTimeFromUtc(h.TimeUtc.AddHours(1), zone), _night.WindowEndLocal),
                h.CloudCoverPercent))
            .ToList();

        static DateTime Max(DateTime a, DateTime b) => a > b ? a : b;
        static DateTime Min(DateTime a, DateTime b) => a < b ? a : b;
    }

    /// <summary>
    /// Obtiene el pronóstico si hace falta: no hay para esta ubicación o tiene más
    /// de <see cref="WeatherCacheDuration"/>. Nunca lanza: los fallos se muestran
    /// en <see cref="CloudDetailText"/> y se registran.
    /// </summary>
    public async Task RefreshWeatherAsync(bool force = false)
    {
        if (!_weatherEnabled || _weatherService is null || GetInputError() is not null)
        {
            UpdateCloudSummary();
            return;
        }

        var location = RoundedLocation;
        var now = _time.GetUtcNow().UtcDateTime;
        var current = ForecastForCurrentLocation();
        var recentFailure = _lastFailedWeatherLocation == location && now - _lastFailedWeatherAttemptUtc < WeatherRetryDelay;
        if (_isFetchingWeather
            || (!force && current is not null && now - current.FetchedAtUtc < WeatherCacheDuration)
            || (!force && current is null && recentFailure))
        {
            UpdateCloudSummary();
            return;
        }

        _isFetchingWeather = true;
        if (current is null)
        {
            CloudDetailText = "Consultando el pronóstico…";
        }

        try
        {
            _forecast = await _weatherService.GetForecastAsync(Latitude, Longitude);
            _weatherError = null;
            _lastFailedWeatherLocation = null;
        }
        catch (WeatherForecastException ex)
        {
            _logger.Info($"Pronóstico no disponible: {ex.Message}");
            _weatherError = ex.Message;
            _lastFailedWeatherAttemptUtc = now;
            _lastFailedWeatherLocation = location;
        }
        finally
        {
            _isFetchingWeather = false;
        }

        UpdateCloudSummary();

        // Si la ubicación cambió mientras se consultaba, se pide la nueva.
        if (RoundedLocation != location && _weatherError is null)
        {
            await RefreshWeatherAsync();
        }
    }

    /// <summary>Versión de "disparar y olvidar" para los recálculos: registra cualquier error inesperado.</summary>
    private async Task RefreshWeatherSafeAsync()
    {
        try
        {
            await RefreshWeatherAsync();
        }
        catch (Exception ex)
        {
            _logger.Error("Error inesperado al actualizar el pronóstico.", ex);
        }
    }

    /// <summary>Recalcula la tarjeta de nubes, las nubes del plan y del detalle, y avisa al gráfico.</summary>
    private void UpdateCloudSummary()
    {
        var forecast = Forecast;
        if (!_weatherEnabled)
        {
            CloudCoverText = "—";
            CloudDetailText = "Pronóstico desactivado en Configuración";
        }
        else if (forecast is null)
        {
            CloudCoverText = "—";
            CloudDetailText = _weatherError is null ? "Sin pronóstico" : $"Sin pronóstico: {_weatherError}";
        }
        else if (_night is not null)
        {
            // La noche oscura si la hay; si no, toda la ventana nocturna.
            var start = _night.AstronomicalDuskLocal ?? _night.WindowStartLocal;
            var end = _night.AstronomicalDawnLocal ?? _night.WindowEndLocal;
            if (forecast.Summarize(ToUtc(start), ToUtc(end)) is { } summary)
            {
                var clearest = TimeZoneInfo.ConvertTimeFromUtc(summary.ClearestHourUtc, EffectiveTimeZone);
                CloudCoverText = $"{summary.AverageCloudCoverPercent:0} %";
                CloudDetailText = $"{WeatherForecast.Describe(summary.AverageCloudCoverPercent)} · "
                    + $"mejor {clearest:HH:mm} ({summary.ClearestHourCloudCoverPercent} %)";
            }
            else
            {
                CloudCoverText = "—";
                CloudDetailText = "Sin pronóstico para esta fecha (hasta 7 días)";
            }
        }

        foreach (var item in ObservationPlan)
        {
            item.CloudCoverAtCulmination = CloudCoverAtLocal(item.CulminationLocal);
        }

        OnPropertyChanged(nameof(Forecast));
        UpdateSelectedObservation();
        WeatherVersion++;
    }

    /// <summary>
    /// Busca una versión más reciente que <paramref name="current"/> si está
    /// activado en la configuración. Nunca lanza.
    /// </summary>
    public async Task CheckForUpdatesAsync(Version current)
    {
        if (!Settings.CheckForUpdates || _updateChecker is null)
        {
            return;
        }

        try
        {
            AvailableUpdate = await _updateChecker.CheckAsync(current);
        }
        catch (Exception ex)
        {
            _logger.Error("Error inesperado al buscar actualizaciones.", ex);
        }
    }

    [RelayCommand]
    private void DismissUpdate() => AvailableUpdate = null;
}
