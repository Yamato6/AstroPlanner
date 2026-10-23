using System.Net.Http;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using AstroPlanner.Core.Interfaces;
using AstroPlanner.Core.Services;
using AstroPlanner.Data.Repositories;
using AstroPlanner.Data.Weather;
using AstroPlanner.UI.Services;
using AstroPlanner.UI.ViewModels;

namespace AstroPlanner.UI;

/// <summary>
/// Punto de entrada de la aplicación WPF.
/// Configura el contenedor de dependencias (Clean Architecture): Core no
/// conoce la UI, y la UI ensambla las implementaciones.
/// </summary>
public partial class App : Application
{
    private ServiceProvider? _serviceProvider;
    private IAppLogger? _logger;

    public App()
    {
        var services = new ServiceCollection();
        ConfigureServices(services);
        _serviceProvider = services.BuildServiceProvider();
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        // Reloj del sistema (sustituible en pruebas; lo usa el modo en vivo)
        services.AddSingleton(TimeProvider.System);

        // Registro de errores (archivo diario en %LocalAppData%\AstroPlanner\logs)
        services.AddSingleton<IAppLogger, FileAppLogger>();

        // Datos
        // Catálogo Messier + planetas
        services.AddSingleton<ICelestialRepository>(
            new CatalogWithPlanetsRepository(new JsonCelestialRepository()));
        services.AddSingleton<ICityRepository, JsonCityRepository>();

        // Servicios de aplicación (Core puro)
        services.AddSingleton<IAstronomyService, AstronomyService>();

        // Internet: un HttpClient compartido con tiempo límite e identificación
        // (GitHub rechaza peticiones sin User-Agent).
        services.AddSingleton(_ =>
        {
            var http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
            http.DefaultRequestHeaders.UserAgent.ParseAdd($"AstroPlanner/{typeof(App).Assembly.GetName().Version}");
            return http;
        });
        services.AddSingleton<IWeatherForecastService>(sp =>
            new OpenMeteoForecastService(sp.GetRequiredService<HttpClient>(), sp.GetRequiredService<TimeProvider>()));
        services.AddSingleton<IUpdateChecker, GitHubUpdateChecker>();

        // Persistencia de configuración de la UI
        services.AddSingleton<ISettingsService, JsonSettingsService>();
        services.AddSingleton<IObservationPlanService, JsonObservationPlanService>();
        services.AddSingleton<IObservationLogService>(_ => new JsonObservationLogService());

        // ViewModels
        services.AddSingleton<SettingsViewModel>();
        services.AddSingleton<LogbookViewModel>();
        services.AddSingleton<MainViewModel>();

        // Vistas
        services.AddSingleton<MainWindow>();
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        if (_serviceProvider is null) return;

        _logger = _serviceProvider.GetRequiredService<IAppLogger>();
        _logger.Info($"AstroPlanner {typeof(App).Assembly.GetName().Version} iniciado.");
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

        var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
        mainWindow.DataContext = _serviceProvider.GetRequiredService<MainViewModel>();
        mainWindow.Show();
    }

    /// <summary>
    /// Error no controlado en el hilo de la interfaz: se registra, se avisa al
    /// usuario y la aplicación sigue abierta en lugar de cerrarse sin explicación.
    /// </summary>
    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        _logger?.Error("Error no controlado en la interfaz.", e.Exception);
        MessageBox.Show(
            "Ocurrió un error inesperado. La aplicación seguirá abierta, pero si el problema "
            + "se repite revisa el registro en:\n"
            + _logger?.LogLocation,
            "AstroPlanner",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
        e.Handled = true;
    }

    /// <summary>Error fatal fuera del hilo de la interfaz: solo puede registrarse.</summary>
    private void OnDomainUnhandledException(object sender, UnhandledExceptionEventArgs e) =>
        _logger?.Error("Error fatal no controlado.", e.ExceptionObject as Exception);

    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        _logger?.Error("Error no observado en una tarea en segundo plano.", e.Exception);
        e.SetObserved();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _serviceProvider?.Dispose();
        base.OnExit(e);
    }
}
