using System.Windows;
using System.Windows.Navigation;
using AstroPlanner.UI.Services;

namespace AstroPlanner.UI;

/// <summary>Versión, créditos (incluida la atribución a Open-Meteo), carpeta de datos y licencia.</summary>
public partial class AboutWindow : Window
{
    public AboutWindow()
    {
        InitializeComponent();
        NativeWindows.UseDarkTitleBar(this);
        VersionRun.Text = AppVersion.Current.ToString();
        DataFolderText.Text = AppPaths.DataDirectory;
        DataFolderText.ToolTip = AppPaths.DataDirectory;
    }

    private void OnLinkClicked(object sender, RequestNavigateEventArgs e)
    {
        SafeLinks.Open(e.Uri);
        e.Handled = true;
    }

    private void OnOpenDataFolderClicked(object sender, RoutedEventArgs e) =>
        SafeLinks.OpenFolder(AppPaths.DataDirectory);
}
