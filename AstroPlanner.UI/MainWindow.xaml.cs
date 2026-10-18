using System.Windows;
using System.ComponentModel;
using AstroPlanner.Data.Repositories;
using AstroPlanner.UI.ViewModels;

namespace AstroPlanner.UI;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        var settings = new SettingsViewModel(new Services.JsonSettingsService());
        DataContext = new MainViewModel(new JsonCelestialRepository(), settings);
        ((MainViewModel)DataContext).PropertyChanged += OnViewModelPropertyChanged;
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel viewModel)
        {
            await viewModel.LoadCommand.ExecuteAsync(null);
            UpdateChart(viewModel);
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is MainViewModel viewModel && e.PropertyName is nameof(MainViewModel.SelectedObject) or nameof(MainViewModel.ObservationDateTime) or nameof(MainViewModel.Latitude) or nameof(MainViewModel.Longitude) or nameof(MainViewModel.Elevation) or nameof(MainViewModel.MinimumAltitude))
        {
            UpdateChart(viewModel);
        }
    }

    private void UpdateChart(MainViewModel viewModel)
    {
        var points = viewModel.GetNightAltitudeCurve();
        AltitudePlot.Plot.Clear();
        if (points.Count > 0)
        {
            var x = points.Select(point => point.LocalTime.Hour + point.LocalTime.Minute / 60.0).ToArray();
            var y = points.Select(point => point.AltitudeDegrees).ToArray();
            AltitudePlot.Plot.Add.Scatter(x, y);
            AltitudePlot.Plot.Add.HorizontalLine(viewModel.MinimumAltitude);
            AltitudePlot.Plot.Axes.Title.Label.Text = $"{viewModel.SelectedObject?.Id} - altitud (°)";
            AltitudePlot.Plot.Axes.Bottom.Label.Text = "Hora local";
            AltitudePlot.Plot.Axes.Left.Label.Text = "Altitud (°)";
            AltitudePlot.Plot.Axes.SetLimitsY(-90, 90);
        }
        AltitudePlot.Refresh();
    }
}