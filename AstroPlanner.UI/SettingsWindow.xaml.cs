using System.Windows;
using AstroPlanner.UI.Services;
using AstroPlanner.UI.ViewModels;

namespace AstroPlanner.UI;

public partial class SettingsWindow : Window
{
    public SettingsWindow(SettingsViewModel settings)
    {
        InitializeComponent();
        NativeWindows.UseDarkTitleBar(this);
        DataContext = settings;
    }

    private void OnSaveClicked(object sender, RoutedEventArgs e)
    {
        if (DataContext is SettingsViewModel settings)
        {
            settings.SaveCommand.Execute(null);
            if (!settings.HasError)
            {
                DialogResult = true;
            }
        }
    }

    private void OnCancelClicked(object sender, RoutedEventArgs e) => DialogResult = false;
}
