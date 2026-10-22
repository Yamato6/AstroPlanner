# AstroPlanner

Planificador de observaciones astronómicas para Windows (WPF, .NET 8). Dada una
ubicación, fecha y hora, calcula qué objetos del catálogo Messier son visibles y
su trayectoria de altitud/azimut a lo largo de la noche.

## Funcionalidad

- **Planificador:** tabla con los 110 objetos Messier (altitud, azimut, estado de
  visibilidad), filtros por tipo, constelación y "solo visibles", y gráfico de
  altitud de 18:00 a 06:00 (hora local) del objeto seleccionado.
- **Detalle del objeto:** hora de salida, inicio de visibilidad (cruce del umbral
  de altitud), mejor momento de observación, ocaso y altitud máxima.
- **Mi plan:** tabla con los objetos añadidos desde el detalle, con su posición
  actual; se guarda entre sesiones.
- **Configuración** (ventana modal): ciudad del catálogo o coordenadas manuales,
  elevación, zona horaria y altitud mínima.

## Arquitectura

Clean Architecture + MVVM estricto. `AstroPlanner.Core` no depende de UI.

```
AstroPlanner.Core/    Modelos, algoritmos astronómicos, interfaces. Sin dependencias de UI.
  ├── Models/           EquatorialCoordinates, HorizontalCoordinates, ObserverLocation,
  │                     CelestialObject (+ enum CelestialObjectType), City
  ├── Algorithms/       AstronomyMath (JD, GMST, LST, normalización)
  │                     CoordinateTransformer (ecuatorial → horizontal, Math.Clamp)
  ├── Services/         AstronomyService (implementación de IAstronomyService)
  └── Interfaces/       ICelestialRepository, ICityRepository, IAstronomyService

AstroPlanner.Data/    Catálogos y persistencia de datos.
  ├── Repositories/     JsonCelestialRepository (System.Text.Json + enum por string)
  │                     JsonCityRepository
  └── SeedData/         messier_objects.json (110 objetos, coordenadas J2000)
                        cities.json (ciudades de México con zona horaria)

AstroPlanner.UI/      WPF (.NET 8), ViewModels, vistas XAML, servicios de presentación.
  ├── MainWindow.xaml   Pestañas Planificador / Detalle del objeto / Mi plan
  ├── SettingsWindow.xaml  Ventana modal de configuración
  ├── ViewModels/       MainViewModel (planificador, filtros, plan de observación)
  │                     CelestialObjectObservation (datos del detalle)
  │                     SettingsViewModel (configuración persistida, ciudades)
  ├── Services/         ISettingsService + JsonSettingsService (settings.json)
  │                     IObservationPlanService + JsonObservationPlanService (plan.json)
  └── App.xaml.cs       Contenedor de DI (Microsoft.Extensions.DependencyInjection)

AstroPlanner.Tests/    xUnit + FluentAssertions (22 pruebas de Core y Data).
```

## Decisiones no obvias

- **.NET 8 (LTS) como TFM.** El SDK instalado es el 10.0, pero se fija `net8.0`
  (y `net8.0-windows` en UI) porque es LTS y estable para WPF. El SDK más nuevo
  compila sin problemas hacia `net8.0`.
- **`AstronomyMath` y `CoordinateTransformer` son estáticos y puros.** Son
  matemática sin estado; no aporta nada convertirlos en servicios inyectados.
  `AstronomyService` (inyectable) es una envoltura fina para que los ViewModels
  no dependan de clases estáticas: `MainViewModel` solo usa `IAstronomyService`.
- **Noche de observación.** La curva y los eventos (salida, culminación, puesta)
  cubren de 18:00 a 06:00. Una hora antes del mediodía pertenece a la noche que
  empezó el día anterior (las 02:00 del día 5 son la noche del 4 al 5).
- **Fecha/hora "de pared" → UTC.** El usuario elige hora local del observador;
  se guarda `TimeZoneId` en la configuración y se convierte a UTC con
  `TimeZoneInfo.ConvertTimeToUtc` antes de cualquier cálculo astronómico. Las
  horas inexistentes por cambio de horario de verano se desplazan una hora.
- **Ids de zona horaria de Windows** (p. ej. `Central Standard Time (Mexico)`)
  en `cities.json` y en la configuración; si el id guardado no existe en el
  sistema se usa la zona local.
- **Configuración en `%LocalAppData%\AstroPlanner\settings.json`**, con escritura
  atómica (archivo temporal + rename) para no corromper si la app se cierra a
  mitad de guardado. Si el archivo es inválido se cargan valores por defecto
  (Ciudad de México, altitud mínima 20°). Cerrar la ventana de configuración sin
  guardar descarta los cambios. "Mi plan" se guarda igual en `plan.json`, como
  lista de ids del catálogo.
- **`CelestialObjectType.Unknown`** existe porque M40 (doble estrella) y M73
  (agrupación de estrellas sin confirmar) no encajan en las categorías estándar.
- **ScottPlot 5** para el gráfico. La dependencia transitoria
  `SkiaSharp.Views.WPF` genera el warning NU1701; es conocido de ScottPlot y no
  afecta al funcionamiento.
- **Precisión.** Se usan coordenadas J2000 sin corregir precesión ni nutación, y
  no se aplica refracción atmosférica; la elevación del observador se guarda
  pero no interviene en el cálculo. Suficiente para planificar (error del orden
  de décimas de grado), no para apuntado preciso.

## Catálogos

- `AstroPlanner.Data/SeedData/messier_objects.json`: los 110 objetos con id,
  nombre común, constelación, tipo, magnitud aparente y coordenadas ecuatoriales
  J2000 (ascensión recta en horas, declinación en grados).
- `AstroPlanner.Data/SeedData/cities.json`: ciudades de México con id, nombre,
  estado, país, latitud, longitud, elevación y zona horaria.

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
| Tests | xunit | 2.9.3 |
| Tests | xunit.runner.visualstudio | 3.1.4 |
| Tests | FluentAssertions | 8.11.0 |
| Tests | Microsoft.NET.Test.Sdk | 18.11.0 |
| Tests | coverlet.collector | 6.0.4 |

## Siguientes pasos pendientes del MVP

1. Refinar validación de entrada de la tabla (avisar de coordenadas inválidas
   en la propia tabla en lugar de vaciarla en silencio).
2. Modo "en tiempo real" con actualización automática periódica.
3. Más pruebas de referencia contra efemérides publicadas (p. ej. posiciones de
   M31/M42 verificadas en Stellarium para instantes concretos).
4. Estilo Fluent/Windows 11 (p. ej. con Wpf.Ui, aún no instalado).
5. Empaquetado (MSIX/installer) y publicación en GitHub con capturas.

> Fases futuras (fuera del MVP): carta polar 2D, efemérides de Luna/planetas,
> bitácora de observaciones, pronóstico meteorológico.
