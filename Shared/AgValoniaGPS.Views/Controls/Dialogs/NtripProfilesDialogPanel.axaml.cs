using Avalonia.Controls;
using Avalonia.Input;

namespace AgValoniaGPS.Views.Controls.Dialogs;

public partial class NtripProfilesDialogPanel : UserControl
{
    public NtripProfilesDialogPanel()
    {
        InitializeComponent();
    }

    private void Backdrop_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        // Close dialog when clicking the backdrop
        if (DataContext is AgValoniaGPS.ViewModels.MainViewModel vm)
        {
            vm.State.UI.CloseDialog();
        }
    }
}
