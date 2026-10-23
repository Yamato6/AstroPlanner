using System.Windows;
using System.ComponentModel;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Navigation;
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
        await _viewModel.CheckForUpdatesAsync(typeof(App).Assembly.GetName().Version ?? new Version(0, 0));
    }

    /// <summary>Abre la página de la versión nueva en el navegador (solo enlaces de GitHub).</summary>
    private void OnUpdateLinkClicked(object sender, RequestNavigateEventArgs e)
    {
        if (e.Uri is { Scheme: "https", Host: "github.com" })
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
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

    private void OnExportPlanClicked(object sender, RoutedEventArgs e)
    {
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
        if (_viewModel is null || _viewModel.ObservationPlan.Count == 0) return;

        var dialog = new System.Windows.Controls.PrintDialog();
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

    /// <summary>
    /// Carta polar del cielo: círculos de altitud cada 30°, puntos cardinales,
    /// objetos sobre el horizonte (tamaño según magnitud), Mi plan, selección y Luna.
    /// </summary>
    private void UpdateSkyMap(MainViewModel viewModel)
    {
        var plot = SkyPlot.Plot;
        plot.Clear();
        plot.FigureBackground.Color = ScottPlot.Color.FromHex("#0F1B2D");
        plot.DataBackground.Color = ScottPlot.Color.FromHex("#0F1B2D");
        plot.Axes.Frameless();
        plot.HideGrid();

        var gridColor = ScottPlot.Color.FromHex("#2A425F");
        var horizon = plot.Add.Circle(0, 0, SkyProjection.HorizonRadius);
        horizon.LineColor = ScottPlot.Color.FromHex("#4A6A90");
        horizon.LineWidth = 2;
        horizon.FillColor = ScottPlot.Color.FromHex("#101E33");

        foreach (var altitude in new[] { 30.0, 60.0 })
        {
            var ring = plot.Add.Circle(0, 0, SkyProjection.HorizonRadius - altitude);
            ring.LineColor = gridColor;
            ring.LineWidth = 1;
        }

        // Altitud mínima: por encima de este círculo el objeto cuenta como visible.
        var threshold = plot.Add.Circle(0, 0, SkyProjection.HorizonRadius - viewModel.MinimumAltitude);
        threshold.LineColor = ScottPlot.Color.FromHex("#FFB45E").WithAlpha(0.6);
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
            text.LabelFontColor = ScottPlot.Color.FromHex("#C9D7ED");
            text.LabelFontSize = 16;
            text.LabelBold = true;
            text.LabelAlignment = ScottPlot.Alignment.MiddleCenter;
        }

        // La Luna va primero (debajo): su disco no debe tapar los objetos que tenga cerca.
        if (viewModel.Moon is { IsAboveHorizon: true } moon)
        {
            var (x, y) = SkyProjection.ToXY(moon.Horizontal.AltitudeDegrees, moon.Horizontal.AzimuthDegrees);
            plot.Add.Marker(x, y, ScottPlot.MarkerShape.FilledCircle, 22, ScottPlot.Color.FromHex("#E8E3C8").WithAlpha(0.85));
            var label = plot.Add.Text($"Luna {viewModel.MoonIlluminationText}", x, y - 6);
            label.LabelFontColor = ScottPlot.Color.FromHex("#E8E3C8");
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
                    ScottPlot.Color.FromHex(point.IsInPlan ? "#FFB45E" : "#F5C26B"));
            }
            else
            {
                var color = point.IsInPlan ? "#FFB45E" : point.IsVisible ? "#55C7E8" : "#5D7392";
                size = (float)Math.Clamp(13 - point.Magnitude, 4, 10);
                plot.Add.Marker(x, y, ScottPlot.MarkerShape.FilledCircle, size, ScottPlot.Color.FromHex(color));
            }

            if (point.IsSelected)
            {
                plot.Add.Marker(x, y, ScottPlot.MarkerShape.OpenCircle, size + 12, ScottPlot.Colors.White);
            }

            var label = plot.Add.Text(point.Id, x + 2.5, y);
            var highlighted = point.IsInPlan || point.IsSelected || point.IsPlanet;
            label.LabelFontColor = ScottPlot.Color.FromHex(point.IsPlanet ? "#F5C26B" : highlighted ? "#EAF2FF" : "#7F93B0");
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
        AltitudePlot.Plot.FigureBackground.Color = ScottPlot.Color.FromHex("#172033");
        AltitudePlot.Plot.DataBackground.Color = ScottPlot.Color.FromHex("#0F1B2D");
        AltitudePlot.Plot.Axes.Color(ScottPlot.Color.FromHex("#8FA8C8"));
        AltitudePlot.Plot.Grid.MajorLineColor = ScottPlot.Color.FromHex("#2A425F");
        AltitudePlot.Plot.Grid.MinorLineColor = ScottPlot.Color.FromHex("#1B2D4A");

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
                moonLine.Color = ScottPlot.Color.FromHex("#E8E3C8").WithAlpha(0.8);
                moonLine.LineWidth = 1.5f;
                moonLine.LinePattern = ScottPlot.LinePattern.Dashed;
                moonLine.MarkerSize = 0;
            }

            var selectedTime = viewModel.ObservationDateTime.ToOADate();
            if (selectedTime >= x[0] && selectedTime <= x[^1])
            {
                var nowLine = AltitudePlot.Plot.Add.VerticalLine(selectedTime);
                nowLine.LineColor = ScottPlot.Color.FromHex("#8FA8C8");
                nowLine.LineWidth = 1;
                nowLine.LinePattern = ScottPlot.LinePattern.Dotted;
                nowLine.LegendText = "Hora elegida";
            }

            var altitudeLine = AltitudePlot.Plot.Add.Scatter(x, y);
            altitudeLine.LegendText = "Altitud";
            altitudeLine.Color = ScottPlot.Color.FromHex("#55C7E8");
            altitudeLine.LineWidth = 2;
            var thresholdLine = AltitudePlot.Plot.Add.HorizontalLine(viewModel.MinimumAltitude);
            thresholdLine.LineColor = ScottPlot.Color.FromHex("#FFB45E");
            thresholdLine.LineWidth = 2;
            thresholdLine.LegendText = "Altitud mínima";

            AltitudePlot.Plot.Axes.Title.Label.Text =
                $"{viewModel.SelectedObject?.Id} — {viewModel.SelectedObject?.CommonName}";
            AltitudePlot.Plot.Axes.Bottom.Label.Text = "Hora local";
            AltitudePlot.Plot.Axes.Left.Label.Text = "Altitud (°)";
            AltitudePlot.Plot.ShowLegend();
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
            span.FillStyle.Color = ScottPlot.Color.FromHex("#A9B4C2").WithAlpha(cloud / 100.0 * 0.45);
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
            span.FillStyle.Color = ScottPlot.Color.FromHex("#3A4F7A").WithAlpha(0.35);
            span.LineStyle.Width = 0;
            if (first)
            {
                span.LegendText = "Crepúsculo";
                first = false;
            }
        }
    }
}
