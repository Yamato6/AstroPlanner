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
        window.ShowDialog();
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

        if (points.Count > 0)
        {
            // Eje X en horas decimales desde 18:00 hasta 30:00 (= 06:00 del día
            // siguiente), para que la curva nocturna sea continua.
            var x = points
                .Select(p =>
                {
                    var hour = p.LocalTime.Hour + p.LocalTime.Minute / 60.0;
                    return hour < 12 ? hour + 24 : hour;
                })
                .ToArray();
            var y = points.Select(p => p.AltitudeDegrees).ToArray();

            AltitudePlot.Plot.Add.Scatter(x, y);
            var thresholdLine = AltitudePlot.Plot.Add.HorizontalLine(viewModel.MinimumAltitude);
            thresholdLine.LineColor = ScottPlot.Color.FromHex("#FFB45E");
            thresholdLine.LineWidth = 2;

            AltitudePlot.Plot.Axes.Title.Label.Text =
                $"{viewModel.SelectedObject?.Id} — altitud (°)";
            AltitudePlot.Plot.Axes.Bottom.Label.Text = "Hora local";
            AltitudePlot.Plot.Axes.Left.Label.Text = "Altitud (°)";
            AltitudePlot.Plot.Axes.SetLimitsY(-90, 90);
            AltitudePlot.Plot.Axes.SetLimitsX(18, 30);
        }

        AltitudePlot.Refresh();
    }
}
