using System.Windows;
using AstroPlanner.UI.ViewModels;

namespace AstroPlanner.UI;

public partial class LogEntryWindow : Window
{
    public LogEntryWindow(LogEntryDraftViewModel draft)
    {
        InitializeComponent();
        DataContext = draft;
    }

    private void OnSaveClicked(object sender, RoutedEventArgs e)
    {
        if (DataContext is not LogEntryDraftViewModel draft) return;

        draft.Validate();
        if (draft.HasErrors || draft.ObservedAtLocal is null)
        {
            ErrorText.Text = "Revisa la hora: escríbela como HH:mm (por ejemplo 22:30).";
            ErrorText.Visibility = Visibility.Visible;
            return;
        }

        DialogResult = true;
    }
}
