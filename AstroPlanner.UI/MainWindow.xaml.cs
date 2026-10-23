using System.Windows;
using System.ComponentModel;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Navigation;
using System.Windows.Threading;
using AstroPlanner.UI.Services;
using AstroPlanner.UI.ViewModels;

namespace AstroPlanner.UI;

/// <summary>
/// Code-behind mínimo: solo actualiza el gráfico de ScottPlot cuando cambian
/// los datos relevantes. Toda la lógica vive en los ViewModels (MVVM estricto).
/// </summary>
public partial class MainWindow : Window
{
    private MainViewModel? _viewModel;

    public MainWindow()
    {
        InitializeComponent();
        NativeWindows.UseDarkTitleBar(this);
        AltitudePlot.UserInputProcessor.IsEnabled = false;
        SkyPlot.UserInputProcessor.IsEnabled = false;
        SkyPlot.SizeChanged += (_, _) =>
        {
            if (_viewModel is not null) UpdateSkyMap(_viewModel);
        };
        DataContextChanged += OnDataContextChanged;
        Loaded += OnLoaded;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        // Desuscribirse del ViewModel anterior (si lo hubiera).
        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }

        _viewModel = e.NewValue as MainViewModel;

        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        }
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_viewModel is null) return;
        await _viewModel.Settings.LoadCitiesCommand.ExecuteAsync(null);
        await _viewModel.LoadCommand.ExecuteAsync(null);
        UpdateChart(_viewModel);
        UpdateSkyMap(_viewModel);
        await _viewModel.CheckForUpdatesAsync(AppVersion.Current);
    }

    /// <summary>
    /// Recupera el tamaño y la posición de la sesión anterior (ajustados al
    /// escritorio actual) y los guarda al cerrar. Se llama antes de Show().
    /// </summary>
    public void RememberPlacement(WindowPlacementStore store)
    {
        if (store.Load() is { } saved)
        {
            var virtualScreen = new Rect(SystemParameters.VirtualScreenLeft, SystemParameters.VirtualScreenTop,
                SystemParameters.VirtualScreenWidth, SystemParameters.VirtualScreenHeight);
            var placement = saved.FitTo(virtualScreen, SystemParameters.WorkArea, MinWidth, MinHeight);
            WindowStartupLocation = WindowStartupLocation.Manual;
            Left = placement.Left;
            Top = placement.Top;
            Width = placement.Width;
            Height = placement.Height;
            if (placement.IsMaximized)
            {
                WindowState = WindowState.Maximized;
            }
        }

        // Maximizada y luego minimizada debe volver maximizada la próxima vez.
        var wasMaximized = WindowState == WindowState.Maximized;
        StateChanged += (_, _) =>
        {
            if (WindowState != WindowState.Minimized)
            {
                wasMaximized = WindowState == WindowState.Maximized;
            }
        };

        Closing += (_, _) =>
        {
            // RestoreBounds: el tamaño "normal" aunque esté maximizada o minimizada.
            var bounds = WindowState == WindowState.Normal ? new Rect(Left, Top, Width, Height) : RestoreBounds;
            if (!bounds.IsEmpty)
            {
                store.Save(new WindowPlacement(bounds.Left, bounds.Top, bounds.Width, bounds.Height, wasMaximized));
            }
        };
    }

    /// <summary>Abre en el navegador los enlaces de la interfaz (solo sitios permitidos, ver <see cref="SafeLinks"/>).</summary>
    private void OnLinkClicked(object sender, RequestNavigateEventArgs e)
    {
        SafeLinks.Open(e.Uri);
        e.Handled = true;
    }

    private void OnAboutClicked(object sender, RoutedEventArgs e) => ShowAbout();

    private void ShowAbout() => new AboutWindow { Owner = this }.ShowDialog();

    /// <summary>
    /// Atajos de teclado: Ctrl+F buscar, F5 ahora, Ctrl+, configuración,
    /// Ctrl+P imprimir el plan, Ctrl+E exportar el plan, F1 acerca de.
    /// </summary>
    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        var ctrl = Keyboard.Modifiers == ModifierKeys.Control;
        var none = Keyboard.Modifiers == ModifierKeys.None;
        switch (e.Key)
        {
            case Key.F when ctrl:
                Tabs.SelectedItem = PlannerTab;
                // Tras cambiar de pestaña, la caja aún no está en pantalla: se enfoca cuando ya lo esté.
                Dispatcher.BeginInvoke(() =>
                {
                    SearchBox.Focus();
                    SearchBox.SelectAll();
                }, DispatcherPriority.Input);
                break;
            case Key.F5 when none:
                _viewModel?.SetNowCommand.Execute(null);
                break;
            case Key.OemComma when ctrl:
                OnSettingsClicked(sender, e);
                break;
            case Key.P when ctrl:
                Tabs.SelectedItem = PlanTab;   // con el plan vacío, al menos se ve por qué no hay nada que imprimir
                OnPrintPlanClicked(sender, e);
                break;
            case Key.E when ctrl:
                Tabs.SelectedItem = PlanTab;
                OnExportPlanClicked(sender, e);
                break;
            case Key.F1 when none:
                ShowAbout();
                break;
            default:
                return;
        }

        e.Handled = true;
    }

    private void OnSettingsClicked(object sender, RoutedEventArgs e)
    {
        if (_viewModel is null) return;

        var window = new SettingsWindow(_viewModel.Settings)
        {
            Owner = this
        };

        // Cancelar o cerrar con la X no debe dejar cambios a medias en memoria.
        if (window.ShowDialog() != true)
        {
            _viewModel.Settings.RevertChanges();
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_viewModel is null) return;

        // El gráfico depende del objeto seleccionado, del instante, del
        // observador, de la zona horaria y del umbral de altitud.
        if (e.PropertyName is nameof(MainViewModel.SelectedObject)
            or nameof(MainViewModel.ObservationDateTime)
            or nameof(MainViewModel.Latitude)
            or nameof(MainViewModel.Longitude)
            or nameof(MainViewModel.Elevation)
            or nameof(MainViewModel.TimeZoneId)
            or nameof(MainViewModel.MinimumAltitude))
        {
            UpdateChart(_viewModel);
        }

        if (e.PropertyName is nameof(MainViewModel.WeatherVersion))
        {
            UpdateChart(_viewModel);
        }

        if (e.PropertyName is nameof(MainViewModel.SkyMapVersion))
        {
            UpdateSkyMap(_viewModel);
        }
    }

    /// <summary>
    /// Las notas del plan se guardan al salir del campo; con un atajo de teclado
    /// el foco no sale, así que se confirma a mano antes de exportar o imprimir.
    /// </summary>
    private static void CommitFocusedTextBox() =>
        (Keyboard.FocusedElement as TextBox)?.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();

    private void OnExportPlanClicked(object sender, RoutedEventArgs e)
    {
        CommitFocusedTextBox();
        if (_viewModel is null || _viewModel.ObservationPlan.Count == 0) return;

        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Title = "Exportar Mi plan",
            Filter = "CSV (*.csv)|*.csv",
            FileName = $"AstroPlanner-plan-{_viewModel.ObservationDate:yyyy-MM-dd}.csv"
        };

        if (dialog.ShowDialog(this) == true)
        {
            _viewModel.ExportObservationPlan(dialog.FileName);
        }
    }

    private void OnLogObservationClicked(object sender, RoutedEventArgs e)
    {
        if (_viewModel?.CreateLogDraftForSelected() is not { } draft) return;

        var window = new LogEntryWindow(draft) { Owner = this };
        if (window.ShowDialog() == true)
        {
            _viewModel.Logbook.Add(draft);
        }
    }

    private void OnExportLogbookClicked(object sender, RoutedEventArgs e)
    {
        if (_viewModel is null || _viewModel.Logbook.Entries.Count == 0) return;

        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Title = "Exportar bitácora",
            Filter = "CSV (*.csv)|*.csv",
            FileName = $"AstroPlanner-bitacora-{DateTime.Now:yyyy-MM-dd}.csv"
        };

        if (dialog.ShowDialog(this) == true)
        {
            _viewModel.Logbook.Export(dialog.FileName);
        }
    }

    private void OnPrintPlanClicked(object sender, RoutedEventArgs e)
    {
        CommitFocusedTextBox();
        if (_viewModel is null || _viewModel.ObservationPlan.Count == 0) return;

        var dialog = new PrintDialog();
        if (dialog.ShowDialog() != true) return;

        var document = PlanPrintDocument.Create(_viewModel.ObservationPlan.ToList(), _viewModel.PlanSummaryText);
        document.PageWidth = dialog.PrintableAreaWidth;
        document.PageHeight = dialog.PrintableAreaHeight;
        dialog.PrintDocument(((IDocumentPaginatorSource)document).DocumentPaginator, "AstroPlanner - Mi plan");
    }

    private void OnSkyPlotClicked(object sender, MouseButtonEventArgs e)
    {
        if (_viewModel is null) return;

        // Píxeles de WPF → píxeles de ScottPlot (escala de pantalla) → coordenadas de la carta.
        var position = e.GetPosition(SkyPlot);
        var scale = SkyPlot.DisplayScale;
        var coordinates = SkyPlot.Plot.GetCoordinates(
            new ScottPlot.Pixel((float)(position.X * scale), (float)(position.Y * scale)));
        _viewModel.SelectNearestOnSkyMap(coordinates.X, coordinates.Y);
    }

    /// <summary>Color del tema (Themes/Dark.xaml) para ScottPlot: los gráficos comparten la paleta de la interfaz.</summary>
    private ScottPlot.Color Theme(string key) =>
        TryFindResource(key) is SolidColorBrush { Color: var c }
            ? new ScottPlot.Color(c.R, c.G, c.B, c.A)
            : ScottPlot.Colors.Magenta;   // clave mal escrita: se nota a la vista

    /// <summary>
    /// Carta polar del cielo: círculos de altitud cada 30°, puntos cardinales,
    /// objetos sobre el horizonte (tamaño según magnitud), Mi plan, selección y Luna.
    /// </summary>
    private void UpdateSkyMap(MainViewModel viewModel)
    {
        var plot = SkyPlot.Plot;
        plot.Clear();
        plot.FigureBackground.Color = Theme("SurfaceBrush");
        plot.DataBackground.Color = Theme("SurfaceBrush");
        plot.Axes.Frameless();
        plot.HideGrid();

        var gridColor = Theme("NightBorderBrush");
        var horizon = plot.Add.Circle(0, 0, SkyProjection.HorizonRadius);
        horizon.LineColor = Theme("SkyHorizonBrush");
        horizon.LineWidth = 2;
        horizon.FillColor = Theme("SkyFillBrush");

        foreach (var altitude in new[] { 30.0, 60.0 })
        {
            var ring = plot.Add.Circle(0, 0, SkyProjection.HorizonRadius - altitude);
            ring.LineColor = gridColor;
            ring.LineWidth = 1;
        }

        // Altitud mínima: por encima de este círculo el objeto cuenta como visible.
        var threshold = plot.Add.Circle(0, 0, SkyProjection.HorizonRadius - viewModel.MinimumAltitude);
        threshold.LineColor = Theme("WarningBrush").WithAlpha(0.6);
        threshold.LineWidth = 1;
        threshold.LinePattern = ScottPlot.LinePattern.Dashed;

        foreach (var azimuth in new[] { 0.0, 45.0, 90.0, 135.0 })
        {
            var (x1, y1) = SkyProjection.ToXY(0, azimuth);
            var (x2, y2) = SkyProjection.ToXY(0, azimuth + 180);
            var line = plot.Add.Line(x1, y1, x2, y2);
            line.LineColor = gridColor;
            line.LineWidth = 1;
        }

        foreach (var (label, azimuth) in new[] { ("N", 0.0), ("E", 90.0), ("S", 180.0), ("O", 270.0) })
        {
            var (x, y) = SkyProjection.ToXY(-8, azimuth);
            var text = plot.Add.Text(label, x, y);
            text.LabelFontColor = Theme("LabelTextBrush");
            text.LabelFontSize = 16;
            text.LabelBold = true;
            text.LabelAlignment = ScottPlot.Alignment.MiddleCenter;
        }

        // La Luna va primero (debajo): su disco no debe tapar los objetos que tenga cerca.
        if (viewModel.Moon is { IsAboveHorizon: true } moon)
        {
            var (x, y) = SkyProjection.ToXY(moon.Horizontal.AltitudeDegrees, moon.Horizontal.AzimuthDegrees);
            plot.Add.Marker(x, y, ScottPlot.MarkerShape.FilledCircle, 22, Theme("MoonBrush").WithAlpha(0.85));
            var label = plot.Add.Text($"Luna {viewModel.MoonIlluminationText}", x, y - 6);
            label.LabelFontColor = Theme("MoonBrush");
            label.LabelFontSize = 12;
            label.LabelAlignment = ScottPlot.Alignment.UpperCenter;
        }

        foreach (var point in viewModel.GetSkyMapPoints().OrderBy(p => p.IsInPlan || p.IsSelected || p.IsPlanet))
        {
            var (x, y) = point.Position;
            float size;
            if (point.IsPlanet)
            {
                // Planetas: rombo dorado (naranja si están en Mi plan), con su nombre.
                size = 13;
                plot.Add.Marker(x, y, ScottPlot.MarkerShape.FilledDiamond, size,
                    Theme(point.IsInPlan ? "WarningBrush" : "PlanetBrush"));
            }
            else
            {
                var color = point.IsInPlan ? "WarningBrush" : point.IsVisible ? "AccentBrush" : "PlaceholderTextBrush";
                size = (float)Math.Clamp(13 - point.Magnitude, 4, 10);
                plot.Add.Marker(x, y, ScottPlot.MarkerShape.FilledCircle, size, Theme(color));
            }

            if (point.IsSelected)
            {
                plot.Add.Marker(x, y, ScottPlot.MarkerShape.OpenCircle, size + 12, ScottPlot.Colors.White);
            }

            var label = plot.Add.Text(point.Id, x + 2.5, y);
            var highlighted = point.IsInPlan || point.IsSelected || point.IsPlanet;
            label.LabelFontColor = Theme(point.IsPlanet ? "PlanetBrush" : highlighted ? "StrongTextBrush" : "SkyLabelBrush");
            label.LabelFontSize = highlighted ? 12 : 10;
            label.LabelAlignment = ScottPlot.Alignment.MiddleLeft;
        }

        // Círculo completo y sin deformar: el lado corto del gráfico abarca
        // ±100 y el largo se amplía en proporción (SquareUnits recortaba el corto).
        var width = Math.Max(SkyPlot.ActualWidth, 1);
        var height = Math.Max(SkyPlot.ActualHeight, 1);
        const double halfSpan = 100;
        plot.Axes.SetLimits(
            -halfSpan * Math.Max(1, width / height),
            halfSpan * Math.Max(1, width / height),
            -halfSpan * Math.Max(1, height / width),
            halfSpan * Math.Max(1, height / width));
        SkyPlot.Refresh();
    }

    private void UpdateChart(MainViewModel viewModel)
    {
        var points = viewModel.GetNightAltitudeCurve();

        // Con una entrada inválida se conserva la última curva, igual que la tabla.
        if (points.Count == 0 && viewModel.ValidationMessage is not null)
        {
            return;
        }

        AltitudePlot.Plot.Clear();
        AltitudePlot.Plot.FigureBackground.Color = Theme("NightPanelBrush");
        AltitudePlot.Plot.DataBackground.Color = Theme("SurfaceBrush");
        AltitudePlot.Plot.Axes.Color(Theme("NightMutedTextBrush"));
        AltitudePlot.Plot.Grid.MajorLineColor = Theme("NightBorderBrush");
        AltitudePlot.Plot.Grid.MinorLineColor = Theme("ControlRaisedBrush");

        if (points.Count > 0)
        {
            // Primero el fondo (crepúsculo y nubes) para que las curvas queden encima.
            AddTwilightBands(viewModel.Night);
            AddCloudBands(viewModel);

            // Eje X con fechas reales para que el cambio de día muestre
            // correctamente 23:00, 00:00, 01:00, etc.
            var x = points.Select(p => p.LocalTime.ToOADate()).ToArray();
            var y = points.Select(p => p.AltitudeDegrees).ToArray();

            var moonPoints = viewModel.GetMoonAltitudeCurve();
            if (moonPoints.Count > 0)
            {
                var moonLine = AltitudePlot.Plot.Add.Scatter(
                    moonPoints.Select(p => p.LocalTime.ToOADate()).ToArray(),
                    moonPoints.Select(p => p.AltitudeDegrees).ToArray());
                moonLine.LegendText = $"Luna ({viewModel.MoonIlluminationText})";
                moonLine.Color = Theme("MoonBrush").WithAlpha(0.8);
                moonLine.LineWidth = 1.5f;
                moonLine.LinePattern = ScottPlot.LinePattern.Dashed;
                moonLine.MarkerSize = 0;
            }

            var selectedTime = viewModel.ObservationDateTime.ToOADate();
            if (selectedTime >= x[0] && selectedTime <= x[^1])
            {
                var nowLine = AltitudePlot.Plot.Add.VerticalLine(selectedTime);
                nowLine.LineColor = Theme("NightMutedTextBrush");
                nowLine.LineWidth = 1;
                nowLine.LinePattern = ScottPlot.LinePattern.Dotted;
                nowLine.LegendText = "Hora elegida";
            }

            var altitudeLine = AltitudePlot.Plot.Add.Scatter(x, y);
            altitudeLine.LegendText = "Altitud";
            altitudeLine.Color = Theme("AccentBrush");
            altitudeLine.LineWidth = 2;
            var thresholdLine = AltitudePlot.Plot.Add.HorizontalLine(viewModel.MinimumAltitude);
            thresholdLine.LineColor = Theme("WarningBrush");
            thresholdLine.LineWidth = 2;
            thresholdLine.LegendText = "Altitud mínima";

            AltitudePlot.Plot.Axes.Title.Label.Text =
                $"{viewModel.SelectedObject?.Id} — {viewModel.SelectedObject?.CommonName}";
            AltitudePlot.Plot.Axes.Bottom.Label.Text = "Hora local";
            AltitudePlot.Plot.Axes.Left.Label.Text = "Altitud (°)";
            AltitudePlot.Plot.ShowLegend();
            // Leyenda oscura: con el fondo blanco por defecto la línea de la Luna no se ve.
            AltitudePlot.Plot.Legend.BackgroundColor = Theme("NightPanelBrush").WithAlpha(0.92);
            AltitudePlot.Plot.Legend.FontColor = Theme("NightTextBrush");
            AltitudePlot.Plot.Legend.OutlineColor = Theme("ControlBorderBrush");
            var timeTicks = new ScottPlot.TickGenerators.DateTimeFixedInterval(
                new ScottPlot.TickGenerators.TimeUnits.Hour(),
                2,
                null,
                0,
                dateTime => dateTime.Date.AddHours(18));
            timeTicks.LabelFormatter = dateTime => dateTime.ToString("HH:mm");
            AltitudePlot.Plot.Axes.Bottom.TickGenerator = timeTicks;
            AltitudePlot.Plot.Axes.SetLimitsY(-90, 90);
            AltitudePlot.Plot.Axes.SetLimitsX(points[0].LocalTime.ToOADate(), points[^1].LocalTime.ToOADate());
        }

        AltitudePlot.Refresh();
    }

    /// <summary>
    /// Nubosidad pronosticada por horas: una franja gris por hora, más opaca
    /// cuanto más nublado. Sin pronóstico no se dibuja nada.
    /// </summary>
    private void AddCloudBands(MainViewModel viewModel)
    {
        var first = true;
        foreach (var (start, end, cloud) in viewModel.GetNightCloudCover())
        {
            if (cloud < 5 || end <= start)
            {
                continue;
            }

            var span = AltitudePlot.Plot.Add.HorizontalSpan(start.ToOADate(), end.ToOADate());
            span.FillStyle.Color = Theme("ChartCloudBrush").WithAlpha(cloud / 100.0 * 0.45);
            span.LineStyle.Width = 0;
            if (first)
            {
                span.LegendText = "Nubes (pronóstico)";
                first = false;
            }
        }
    }

    /// <summary>
    /// Sombrea el crepúsculo: de la puesta del Sol al final del crepúsculo
    /// astronómico y del inicio del crepúsculo matutino a la salida. Si nunca
    /// oscurece del todo, se sombrea toda la ventana.
    /// </summary>
    private void AddTwilightBands(Core.Models.NightInfo? night)
    {
        if (night is null || night.IsFallbackWindow)
        {
            return;
        }

        var bands = night.HasAstronomicalDarkness
            ? new[]
            {
                (night.WindowStartLocal, night.AstronomicalDuskLocal!.Value),
                (night.AstronomicalDawnLocal!.Value, night.WindowEndLocal)
            }
            : new[] { (night.WindowStartLocal, night.WindowEndLocal) };

        var first = true;
        foreach (var (from, to) in bands)
        {
            var span = AltitudePlot.Plot.Add.HorizontalSpan(from.ToOADate(), to.ToOADate());
            span.FillStyle.Color = Theme("ChartTwilightBrush").WithAlpha(0.35);
            span.LineStyle.Width = 0;
            if (first)
            {
                span.LegendText = "Crepúsculo";
                first = false;
            }
        }
    }
}
