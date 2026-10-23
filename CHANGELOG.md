# Historial de cambios

Todas las versiones de AstroPlanner. Las descargas están en
[Releases](https://github.com/Yamato6/AstroPlanner/releases).

## [1.0.0] - 2026-10

Primera versión estable. Pule lo existente en lugar de añadir funciones nuevas.

### Instalación
- **Instalador para Windows** (Inno Setup):
  - instalación por usuario, sin permisos de administrador;
  - acceso en el menú Inicio, acceso opcional en el escritorio y desinstalador;
  - las versiones futuras se instalan encima;
  - desinstalar no borra tus datos.
- **Versión portátil** en zip, ahora con la licencia y el README.
- **Sumas SHA-256** (`SHA256SUMS.txt`) en cada versión.
- El ejecutable se llama **`AstroPlanner.exe`**, tiene icono propio y pesa **~76 MB en lugar de ~175 MB**.

### Aspecto
- **Icono** nuevo y **barra de título oscura** en todas las ventanas.
- **Tema unificado:** Configuración y Registrar observación ya usan los mismos controles oscuros que la ventana principal.
- Barras de desplazamiento, leyenda del gráfico, casillas y botones a juego con el tema.
- Los nombres largos de las tablas terminan en "…" y se leen completos al pasar el ratón.
- Nitidez en pantallas con escalado distinto (por monitor).

### En español
- Los tipos de objeto se muestran en español en la tabla, el detalle, los filtros y el CSV (por ejemplo "Cúmulo globular" en lugar de "GlobularCluster").
- El filtro de constelación tiene la opción **"Todas"**.

### Nuevo
- Ventana **Acerca de** (F1 o botón "?"): versión, créditos, licencia y un botón para abrir la carpeta de datos.
- **Atajos de teclado:**
  - Ctrl+F: buscar;
  - F5: hora actual;
  - Ctrl+, : configuración;
  - Ctrl+P: imprimir el plan;
  - Ctrl+E: exportar el plan.
- La ventana **recuerda su tamaño y posición**. Si quedó fuera de la pantalla (por ejemplo, al desconectar un monitor), se centra.
- **Una sola instancia:** abrir la app por segunda vez trae al frente la ventana que ya estaba abierta.
- Atribución a Open-Meteo junto al pronóstico, como pide su licencia.

### Correcciones
- Al registrar una observación de un planeta, el título ya no repite el nombre ("Júpiter", no "Júpiter — Júpiter").
- El aviso de versión entiende las versiones candidatas (`1.0.0-rc.1`). Quien usa una versión estable solo recibe avisos de versiones estables.
- Texto "Mostrando N objetos" legible sobre el fondo oscuro.

### Proyecto
- Licencia **MIT**; el repositorio pasa a ser público.
- README reescrito y este CHANGELOG.
- El CI compila con los avisos como errores, usa acciones con Node 24 y genera el instalador.

## [0.8.0] - 2026-10-23

### Nuevo
- **Pronóstico de nubes** (Open-Meteo):
  - tarjeta "Nubes esta noche" con la media de la noche oscura y la hora más despejada;
  - franjas grises por hora en el gráfico;
  - nubes a la hora de culminación en el detalle y en Mi plan.
- **Aviso de versión nueva**, que consulta las releases de GitHub.
- Sección **"Conexión a internet"** en Configuración para desactivar ambas funciones. Se envía la ubicación redondeada a ~1 km.

## [0.7.0] - 2026-10-23

### Nuevo
- **Planetas:** Mercurio, Venus, Marte, Júpiter, Saturno, Urano y Neptuno, en la tabla, la carta del cielo, Mi plan y la búsqueda. Muestran magnitud, distancia, tamaño aparente, fase y distancia al Sol.
- **Bitácora de observaciones:**
  - registro con fecha, lugar, equipo, seeing, transparencia, valoración y notas;
  - progreso del **maratón Messier**;
  - exportación a CSV;
  - ✓ en los objetos ya observados.

## [0.6.0] - 2026-10-23

### Nuevo
- **Búsqueda** por id, nombre o constelación, sin distinguir acentos, y **filtro de magnitud máxima**.
- **Modo "En vivo"**, que actualiza todo cada minuto. "Ahora" usa la hora de la ubicación del observador.
- **Mi plan:**
  - ordenado por hora de culminación;
  - notas por objeto;
  - exportar a CSV;
  - imprimir o guardar como PDF.
- **Carta del cielo** a la hora elegida; clic en un objeto para seleccionarlo.

## [0.5.0] - 2026-10-22

Incluye también los cambios de la 0.4, que no llegó a publicarse por separado.

### Nuevo
- **Noche real:** de la puesta a la salida del Sol, con la noche oscura (crepúsculo astronómico).
- **Luna:**
  - fase, iluminación y altura;
  - distancia a cada objeto;
  - aviso cuando su brillo estorba.
- **Más precisión:** precesión, refracción y, para la Luna, paralaje.
- Tarjetas "Noche oscura" y "Luna".
- Aviso de día o crepúsculo a la hora elegida.
- En el gráfico: crepúsculo sombreado, curva de la Luna y hora elegida.

### Robustez (0.4)
- Los errores inesperados ya no cierran la app: se muestra un aviso y se guarda un registro.
- Validación de datos con avisos, en lugar de vaciar la tabla.
- Los catálogos van dentro del `.exe`: ya no hace falta la carpeta `SeedData`.
- Integración continua en GitHub Actions.

## [0.3] - 2026-10-22

### Nuevo
- Pestañas **Detalle del objeto** (salida, umbral, culminación y puesta) y **Mi plan**, que se guarda entre sesiones.
- Selector de fecha y hora con calendario, listas y botones ±1 h.
- Gráfico de altitud con tema oscuro, leyenda y línea de altitud mínima.
- Divisor arrastrable entre la tabla y el gráfico; "No visible" en rojo.

### Correcciones
- Una hora de madrugada muestra la noche en curso, no la siguiente.
- Cancelar la configuración descarta los cambios.
- "Restablecer" vuelve a la altitud configurada.
- Las filas seleccionadas ya no se ven en blanco.

## [0.2] - 2026-10-18

- Primer ejecutable para Windows: tabla de objetos Messier visibles y gráfico de altitud.

[1.0.0]: https://github.com/Yamato6/AstroPlanner/compare/v0.8.0...v1.0.0
[0.8.0]: https://github.com/Yamato6/AstroPlanner/compare/v0.7.0...v0.8.0
[0.7.0]: https://github.com/Yamato6/AstroPlanner/compare/v0.6.0...v0.7.0
[0.6.0]: https://github.com/Yamato6/AstroPlanner/compare/v0.5.0...v0.6.0
[0.5.0]: https://github.com/Yamato6/AstroPlanner/compare/Pre_0.3...v0.5.0
[0.3]: https://github.com/Yamato6/AstroPlanner/releases/tag/Pre_0.3
[0.2]: https://github.com/Yamato6/AstroPlanner/releases/tag/PreRelease
