using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using AstroPlanner.Core.Interfaces;
using AstroPlanner.Core.Services;
using AstroPlanner.Data.Repositories;
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

    public App()
    {
        var services = new ServiceCollection();
        ConfigureServices(services);
        _serviceProvider = services.BuildServiceProvider();
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        // Datos
        services.AddSingleton<ICelestialRepository, JsonCelestialRepository>();
        services.AddSingleton<ICityRepository, JsonCityRepository>();

        // Servicios de aplicación (Core puro)
        services.AddSingleton<IAstronomyService, AstronomyService>();

        // Persistencia de configuración de la UI
        services.AddSingleton<ISettingsService, JsonSettingsService>();

        // ViewModels
        services.AddSingleton<SettingsViewModel>();
        services.AddSingleton<MainViewModel>();

        // Vistas
        services.AddSingleton<MainWindow>();
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        if (_serviceProvider is null) return;

        var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
        mainWindow.DataContext = _serviceProvider.GetRequiredService<MainViewModel>();
        mainWindow.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _serviceProvider?.Dispose();
        base.OnExit(e);
    }
}
