# AstroPlanner

Planificador de observaciones astronómicas para Windows (WPF, .NET 8). Dada una
ubicación, fecha y hora, calcula qué objetos del catálogo Messier son visibles y
su trayectoria de altitud/azimut a lo largo de la noche.

## Arquitectura

Clean Architecture + MVVM estricto. `AstroPlanner.Core` no depende de UI.

```
AstroPlanner.Core/    Modelos, algoritmos astronómicos, interfaces. Sin dependencias de UI.
  ├── Models/           EquatorialCoordinates, HorizontalCoordinates, ObserverLocation,
  │                     CelestialObject (+ enum CelestialObjectType)
  ├── Algorithms/       AstronomyMath (JD, GMST, LST, normalización)
  │                     CoordinateTransformer (ecuatorial → horizontal, Math.Clamp)
  ├── Services/         AstronomyService (implementación de IAstronomyService)
  └── Interfaces/       ICelestialRepository, IAstronomyService

AstroPlanner.Data/    Catálogo y persistencia de datos.
  ├── Repositories/     JsonCelestialRepository (System.Text.Json + enum por string)
  └── SeedData/         messier_objects.json (110 objetos, coordenadas J2000)

AstroPlanner.UI/      WPF (.NET 8), ViewModels, vistas XAML, servicios de presentación.
  ├── ViewModels/       MainViewModel (planificador y filtros)
  │                     SettingsViewModel (configuración persistida)
  ├── Views/            MainWindow.xaml (pestañas Planificador / Configuración)
  ├── Services/         ISettingsService + JsonSettingsService (settings.json)
  └── App.xaml.cs       Contenedor de DI (Microsoft.Extensions.DependencyInjection)

AstroPlanner.Tests/    xUnit + FluentAssertions (20 pruebas).
```

## Decisiones no obvias

- **.NET 8 (LTS) como TFM.** El SDK instalado es el 10.0, pero se fija `net8.0`
  (y `net8.0-windows` en UI) porque es LTS y estable para WPF. El SDK más nuevo
  compila sin problemas hacia `net8.0`.
- **`AstronomyMath` y `CoordinateTransformer` son estáticos y puros.** Son
  matemática sin estado; no aporta nada convertirlos en servicios inyectados.
  `AstronomyService` (inyectable) es una envoltura fina para que los ViewModels
  no dependan de clases estáticas.
- **Fecha/hora "de pared" → UTC.** El usuario elige hora local del observador;
  se guarda `TimeZoneId` en la configuración y se convierte a UTC con
  `TimeZoneInfo.ConvertTimeToUtc` antes de cualquier cálculo astronómico.
- **Configuración en `%LocalAppData%\AstroPlanner\settings.json`**, con escritura
  atómica (archivo temporal + rename) para no corromper si la app se cierra a
  mitad de guardado. Si el archivo es inválido se cargan valores por defecto.
- **`CelestialObjectType.Unknown`** existe porque M40 (doble estrella) y M73
  (agrupación de estrellas sin confirmar) no encajan en las categorías estándar.
- **ScottPlot 5** para el gráfico (sugerido en el documento). La dependencia
  transitoria `SkiaSharp.Views.WPF` genera el warning NU1701; es conocido de
  ScottPlot y no afecta al funcionamiento.
- **Wpf.Ui y CommunityToolkit.Mvvm** instalados para MVVM y estilo; el aspecto
  visual Fluent se puede activar en una fase posterior.

## Catálogo Messier

`AstroPlanner.Data/SeedData/messier_objects.json` contiene los 110 objetos con
id, nombre común, constelación, tipo, magnitud aparente y coordenadas
ecuatoriales J2000 (ascensión recta en horas, declinación en grados).

## Compilar y ejecutar

```bash
dotnet build AstroPlanner.slnx
dotnet run --project AstroPlanner.UI
```

## Ejecutar las pruebas

```bash
dotnet test AstroPlanner.slnx
# Solo pruebas de Core:
dotnet test AstroPlanner.Tests --filter "FullyQualifiedName~AstronomyMathTests|FullyQualifiedName~CoordinateTransformerTests"
```

## Dependencias NuGet

| Proyecto | Paquete | Versión |
|---|---|---|
| UI | CommunityToolkit.Mvvm | 8.4.2 |
| UI | Microsoft.Extensions.DependencyInjection | 10.0.13 |
| UI | ScottPlot.WPF | 5.1.59 |
| UI | Wpf.Ui | 3.4.2.7 |
| Tests | xunit | 2.9.3 |
| Tests | FluentAssertions | 8.11.0 |
| Tests | Microsoft.NET.Test.Sdk | 18.11.0 |

## Siguientes pasos pendientes del MVP

1. Refinar validación de entrada de la tabla (avisar de coordenadas inválidas
   en la propia tabla en lugar de vaciarla en silencio).
2. Modo "en tiempo real" con actualización automática periódica.
3. Más pruebas de referencia contra efemérides publicadas (p. ej. posiciones de
   M31/M42 verificadas en Stellarium para instantes concretos).
4. Estilo Fluent/Windows 11 con Wpf.Ui (tema, tipografía, navegación).
5. Empaquetado (MSIX/installer) y publicación en GitHub con capturas.

> Fases futuras (fuera del MVP): carta polar 2D, efemérides de Luna/planetas,
> bitácora de observaciones, pronóstico meteorológico.
