using Avalonia.Controls;
using Avalonia.Input;

namespace AgValoniaGPS.Views.Controls.Dialogs;

public partial class NtripProfileEditorPanel : UserControl
{
    public NtripProfileEditorPanel()
    {
        InitializeComponent();
    }

    private void Backdrop_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        // Don't close on backdrop click - user must use Cancel or Save
    }
}
