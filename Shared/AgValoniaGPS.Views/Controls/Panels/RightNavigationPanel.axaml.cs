namespace AgValoniaGPS.Views.Controls.Panels;

public partial class RightNavigationPanel : DraggableRotatablePanel
{
    public RightNavigationPanel()
    {
        InitializeComponent();

        // Initialize drag and rotate behavior from base class
        InitializeDragRotate();
    }
}
