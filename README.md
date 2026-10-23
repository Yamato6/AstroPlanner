# AstroPlanner

Planificador de observaciones astronómicas para Windows (WPF, .NET 8). Dada una
ubicación, fecha y hora, calcula qué objetos del catálogo Messier y qué planetas
son visibles y su trayectoria de altitud/azimut a lo largo de la noche, y lleva
una bitácora de lo observado.

## Funcionalidad

- **Noche y Luna:** puesta y salida del Sol, noche oscura (crepúsculo
  astronómico) y fase, iluminación y altura de la Luna para la fecha elegida.
  Aviso cuando a la hora elegida es de día o hay crepúsculo.
- **Fecha y hora:** calendario, hora/minuto, ±1 h, "Ahora" (hora actual de la
  ubicación del observador) y modo **En vivo**, que sigue el reloj y actualiza
  todo cada minuto hasta que se elige otra hora a mano.
- **Planificador:** tabla con los 110 objetos Messier y los 7 planetas (altitud,
  azimut, distancia a la Luna, estado de visibilidad, ✓ si ya se observó), búsqueda por id, nombre o
  constelación (sin distinguir acentos), filtros por tipo, constelación,
  magnitud máxima y "solo visibles", y gráfico de altitud de la puesta a la
  salida del Sol con el crepúsculo sombreado, la curva de la Luna y la hora elegida.
- **Cielo:** carta polar del cielo a la hora elegida (cénit en el centro, norte
  arriba, este a la izquierda) con los objetos sobre el horizonte, Mi plan, la
  Luna, los planetas y el círculo de altitud mínima; clic en un objeto para seleccionarlo.
- **Detalle del objeto:** hora de salida, inicio de visibilidad (cruce del umbral
  de altitud), mejor momento de observación, ocaso, altitud máxima, distancia a
  la Luna y aviso si su brillo dificulta la observación. En los planetas,
  además: distancia, tamaño aparente, fase y distancia al Sol en el cielo.
  Botón "Registrar observación…" para anotarlo en la bitácora.
- **Mi plan:** objetos añadidos desde el detalle, ordenados por hora de
  culminación (el orden en que conviene observar), con notas por objeto;
  exportar a CSV e imprimir. Se guarda entre sesiones.
- **Bitácora:** observaciones registradas (fecha, lugar, equipo, seeing,
  transparencia, valoración y notas), progreso del maratón Messier (n / 110) y
  exportación a CSV.
- **Configuración** (ventana modal): ciudad del catálogo o coordenadas manuales,
  elevación, zona horaria, altitud mínima y magnitud máxima.

## Arquitectura

Clean Architecture + MVVM estricto. `AstroPlanner.Core` no depende de UI.

```
AstroPlanner.Core/    Modelos, algoritmos astronómicos, interfaces. Sin dependencias de UI.
  ├── Models/           EquatorialCoordinates, HorizontalCoordinates, ObserverLocation,
  │                     CelestialObject (+ enum CelestialObjectType), City, ObjectNightEvents
  │                     SolarSystem: SunPosition, MoonPosition, MoonState, NightInfo, SkyCondition
  │                     Planets: Planet, PlanetPosition, SolarSystemCatalog (los 7 planetas)
  ├── Algorithms/       AstronomyMath (JD, GMST, LST, separación angular)
  │                     CoordinateTransformer (ecuatorial → horizontal, Math.Clamp)
  │                     Precession (J2000 → fecha, Meeus cap. 21)
  │                     Refraction (Saemundsson, Meeus cap. 16)
  │                     SolarSystemCalculator (Sol, Luna, fase lunar)
  │                     PlanetCalculator (elementos de JPL, Kepler, magnitud y fase de Meeus)
  │                     TimeZoneConversion (hora local → UTC, horario de verano)
  │                     ObservationNight (noche que contiene una hora, cruces de altitud)
  ├── Services/         AstronomyService (implementación de IAstronomyService)
  └── Interfaces/       ICelestialRepository, ICityRepository, IAstronomyService

AstroPlanner.Data/    Catálogos y persistencia de datos.
  ├── Repositories/     JsonCelestialRepository (System.Text.Json + enum por string)
  │                     JsonCityRepository, SeedDataSource (recursos incrustados)
  │                     CatalogWithPlanetsRepository (añade los planetas al catálogo)
  └── SeedData/         messier_objects.json (110 objetos, coordenadas J2000)
                        cities.json (ciudades de México con zona horaria)
                        Ambos se incrustan en el ensamblado (EmbeddedResource).

AstroPlanner.UI/      WPF (.NET 8), ViewModels, vistas XAML, servicios de presentación.
  ├── MainWindow.xaml   Pestañas Planificador / Cielo / Detalle del objeto / Mi plan / Bitácora
  ├── SettingsWindow.xaml  Ventana modal de configuración
  ├── LogEntryWindow.xaml  Ventana modal "Registrar observación"
  ├── Converters/       BooleanToTextConverter, BooleanToVisibilityConverter (con Invert)
  ├── ViewModels/       MainViewModel (planificador, filtros, modo en vivo, plan, carta)
  │                     PlanItemViewModel (fila de Mi plan: nota y culminación)
  │                     CelestialObjectObservation (datos del detalle)
  │                     CatalogSearch (búsqueda sin acentos), SkyProjection (carta polar)
  │                     SettingsViewModel (configuración persistida, ciudades)
  │                     LogbookViewModel + LogEntryDraftViewModel (bitácora)
  ├── Services/         ISettingsService + JsonSettingsService (settings.json)
  │                     IObservationPlanService + JsonObservationPlanService (plan.json)
  │                     IObservationLogService + JsonObservationLogService (logbook.json)
  │                     PlanExport (CSV), PlanPrintDocument (FlowDocument imprimible)
  │                     AtomicFile (escritura atómica), CsvBuilder (CSV con la cultura)
  │                     IAppLogger + FileAppLogger (logs\astroplanner-AAAAMMDD.log)
  └── App.xaml.cs       Contenedor de DI y manejo global de errores

AstroPlanner.Tests/    xUnit + FluentAssertions (135 pruebas de Core, Data y UI),
                       incluidas pruebas de referencia contra los ejemplos de Meeus.
                       net8.0-windows para poder probar ViewModels y servicios de UI.

.github/workflows/ci.yml  Compila y prueba en cada push/PR; con un tag v* publica
                          el .exe y crea un borrador de release en GitHub.
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
  cubren de la puesta a la salida del Sol (`IAstronomyService.GetNightInfo`).
  Una hora antes del mediodía pertenece a la noche que empezó el día anterior
  (las 02:00 del día 5 son la noche del 4 al 5). Un evento fuera de la ventana
  es `null` y se muestra como "—". Si el Sol no se pone o no sale (latitudes
  polares) se usa la ventana fija 18:00 → 06:00.
- **Sol y crepúsculo.** Puesta/salida con el centro del Sol a -0.833° (radio +
  refracción, convención estándar) y noche oscura con el Sol a -18°. Se buscan
  muestreando la altura del Sol cada 10 min e interpolando el cruce.
- **Luna.** Fórmulas de baja precisión del Astronomical Almanac (~0.3°), con
  paralaje (hasta ~1° cerca del horizonte) y refracción. Se considera que
  estorba si está sobre el horizonte, iluminada ≥ 25 % y a menos de 30° del
  objeto (`MainViewModel.MoonInterferes`).
- **Validación de entrada.** Los ViewModels heredan de `ObservableValidator` con
  `[Range]`. Con un valor inválido (p. ej. altitud mínima 120) se muestra un
  aviso y la tabla y el gráfico conservan el último cálculo válido en lugar de
  vaciarse.
- **Errores y registro.** `App` captura las excepciones no controladas (interfaz,
  dominio y tareas), las registra en `%LocalAppData%\AstroPlanner\logs` y avisa
  al usuario sin cerrar la aplicación. Los errores ya controlados (catálogo,
  ciudades, guardado) también se registran.
- **Catálogos incrustados.** Los JSON de `SeedData` van como recursos del
  ensamblado: el .exe publicado es un único archivo. Los repositorios aceptan
  una ruta explícita, que tiene prioridad (la usan las pruebas).
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
  `[{"id": "M31", "note": "..."}]`; también se lee el formato de la 0.3-0.5
  (lista de ids) para no perder planes existentes.
- **Filtrar no recalcula.** Búsqueda, tipo, constelación, magnitud y "solo
  visibles" solo vuelven a filtrar (`ApplyFilter`); las posiciones, la noche y
  la Luna se recalculan (`RecalculateAndFilter`) solo si cambian la hora, el
  lugar o la altitud mínima. Así escribir en la búsqueda no recalcula el cielo
  en cada tecla.
- **Modo en vivo con `TimeProvider`.** `MainViewModel` recibe un `TimeProvider`
  (en la app, `TimeProvider.System`) para la hora actual y el temporizador; las
  pruebas usan un reloj manual. El temporizador dispara en otro hilo y el cambio
  se aplica en el de la interfaz mediante el `SynchronizationContext` capturado.
- **Planetas.** Elementos orbitales keplerianos de JPL ("Approximate Positions
  of the Planets", tabla 1, 1800-2050) con corrección por tiempo de luz; error
  ~1' en los planetas interiores y hasta ~10' en Júpiter/Saturno. Magnitud con
  las fórmulas del Astronomical Almanac (Meeus 41), incluida la inclinación de
  los anillos de Saturno (Meeus 45). Son `CelestialObject` de tipo `Planet` con
  `Planet` asignado; `AstronomyService` calcula su posición y magnitud para cada
  instante en lugar de aplicar la precesión. No se marca interferencia de la Luna
  en los planetas: son lo bastante brillantes.
- **Bitácora en `logbook.json`.** Si el archivo está dañado no se sobrescribe:
  se aparta como `logbook.corrupto-<fecha>.json` y se empieza vacía, para no
  perder observaciones al guardar la siguiente.
- **CSV con la cultura del sistema.** Separador de listas y formato numérico de
  la cultura actual y UTF-8 con BOM, para que Excel lo abra en columnas y con
  acentos sin pasos extra.
- **`CelestialObjectType.Unknown`** existe porque M40 (doble estrella) y M73
  (agrupación de estrellas sin confirmar) no encajan en las categorías estándar.
- **ScottPlot 5** para el gráfico. La dependencia transitoria
  `SkiaSharp.Views.WPF` genera el warning NU1701; es conocido de ScottPlot y no
  afecta al funcionamiento.
- **Precisión.** `IAstronomyService.GetHorizontal` aplica la precesión J2000 →
  fecha (~0.36° en 2026) y la refracción estándar (~34' en el horizonte); no
  aplica nutación ni aberración (&lt; 30"). Se usa UT en lugar de TT (ΔT ≈ 70 s).
  La elevación del observador se guarda pero no interviene en el cálculo.
  `CoordinateTransformer` sigue siendo la transformación geométrica pura. Las
  pruebas de `SolarSystemTests` comparan con los ejemplos resueltos de Meeus y
  con la puesta/salida del Sol publicadas para CDMX (±3 min).

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

## Hoja de ruta

**0.5 (hecho):** Sol y noche real, Luna (fase, posición, distancia a cada
objeto), precesión, refracción y pruebas de referencia.

**0.6 (hecho):** búsqueda y filtro de magnitud, modo en vivo, Mi plan con
orden por culminación, notas, CSV e impresión, y carta polar del cielo.

**0.7 (hecho):** planetas (posición, magnitud, fase, tamaño) y bitácora de
observaciones con progreso del maratón Messier.

**0.8: conectado**
- Pronóstico de nubes por hora (Open-Meteo, sin clave); la app sigue
  funcionando sin internet.
- Aviso de nueva versión consultando las releases de GitHub.

**0.9 → 1.0:** instalador (Inno Setup) y estilo Fluent de Windows 11.
