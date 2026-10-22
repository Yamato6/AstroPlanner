using System.Windows;
using System.ComponentModel;
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
    }

    private void OnNowClicked(object sender, RoutedEventArgs e)
    {
        if (_viewModel is not null)
        {
            _viewModel.ObservationDateTime = DateTime.Now;
        }
    }

    private void UpdateChart(MainViewModel viewModel)
    {
        var points = viewModel.GetNightAltitudeCurve();
        AltitudePlot.Plot.Clear();
        AltitudePlot.Plot.FigureBackground.Color = ScottPlot.Color.FromHex("#172033");
        AltitudePlot.Plot.DataBackground.Color = ScottPlot.Color.FromHex("#0F1B2D");
        AltitudePlot.Plot.Axes.Color(ScottPlot.Color.FromHex("#8FA8C8"));
        AltitudePlot.Plot.Grid.MajorLineColor = ScottPlot.Color.FromHex("#2A425F");
        AltitudePlot.Plot.Grid.MinorLineColor = ScottPlot.Color.FromHex("#1B2D4A");

        if (points.Count > 0)
        {
            // Eje X con fechas reales para que el cambio de día muestre
            // correctamente 23:00, 00:00, 01:00, etc.
            var x = points.Select(p => p.LocalTime.ToOADate()).ToArray();
            var y = points.Select(p => p.AltitudeDegrees).ToArray();

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
}
