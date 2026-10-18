# AstroPlanner

MVP de escritorio WPF para planificar observaciones de objetos Messier desde una ubicación y una fecha/hora determinadas.

## Funcionalidades

- Configuración de latitud, longitud y elevación del observador.
- Selección de fecha y hora local de observación.
- Catálogo local JSON con 110 identificadores Messier.
- Cálculo de Día Juliano, GMST, LST y coordenadas horizontales.
- Altitud, azimut y filtro de visibilidad por altitud mínima.
- Filtros por tipo y visibilidad.
- Curva de altitud nocturna entre las 18:00 y las 06:00.

## Arquitectura

- `AstroPlanner.Core`: modelos, interfaces y algoritmos astronómicos sin dependencias de WPF.
- `AstroPlanner.Data`: repositorio JSON y datos semilla locales.
- `AstroPlanner.UI`: aplicación WPF, ViewModels MVVM y visualización ScottPlot.
- `AstroPlanner.Tests`: pruebas xUnit del núcleo y del repositorio.

## Convenciones

La fecha introducida en la interfaz se interpreta como hora local de Windows. Antes de calcular el tiempo sidéreo, se convierte a UTC. La longitud este es positiva y la longitud oeste negativa. El azimut se expresa en grados desde el norte, en sentido horario; la altitud va de -90° a 90°.

La fórmula inicial es geométrica y no incluye refracción atmosférica, precesión, nutación ni movimiento propio. La elevación se conserva en la configuración, pero no modifica todavía la transformación angular.

## Ejecución

Requisitos: Windows, Visual Studio con la carga de trabajo de escritorio .NET y SDK .NET 8.

```powershell
dotnet build AstroPlanner.slnx
dotnet test AstroPlanner.Tests/AstroPlanner.Tests.csproj
```

La aplicación ejecutable es `AstroPlanner.UI`. El archivo `AstroPlanner.Data/SeedData/messier_objects.json` se copia automáticamente al directorio de salida.

## Datos del catálogo

El archivo JSON incluido contiene los 110 registros necesarios para probar el flujo completo del MVP. Antes de utilizar AstroPlanner como herramienta astronómica de precisión, los nombres, tipos, magnitudes y coordenadas deben contrastarse con una fuente astronómica verificada y versionarse junto con la aplicación.
