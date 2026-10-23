using AstroPlanner.Core.Models;

namespace AstroPlanner.Core.Interfaces;

/// <summary>Pronóstico meteorológico por horas.</summary>
public interface IWeatherForecastService
{
    /// <summary>
    /// Pronóstico de los próximos días para la ubicación.
    /// </summary>
    /// <exception cref="WeatherForecastException">Si no se pudo obtener.</exception>
    Task<WeatherForecast> GetForecastAsync(double latitude, double longitude, CancellationToken cancellationToken = default);
}
