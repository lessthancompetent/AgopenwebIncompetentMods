using System;
using Avalonia;
using Avalonia.Controls;

namespace AgValoniaGPS.Views.Controls.Panels;

public partial class LeftNavigationPanel : DraggableRotatablePanel
{
    public LeftNavigationPanel()
    {
        InitializeComponent();

        // Initialize drag and rotate behavior from base class
        InitializeDragRotate();

        // Wire up sub-panel drag events
        WireUpSubPanelDrag<SimulatorPanel>("SimulatorPanelControl");
        WireUpSubPanelDrag<ViewSettingsPanel>("ViewSettingsPanelControl");
        WireUpSubPanelDrag<FileMenuPanel>("FileMenuPanelControl");
        WireUpSubPanelDrag<ToolsPanel>("ToolsPanelControl");
        WireUpSubPanelDrag<ConfigurationPanel>("ConfigurationPanelControl");
        WireUpSubPanelDrag<JobMenuPanel>("JobMenuPanelControl");
        WireUpSubPanelDrag<FieldToolsPanel>("FieldToolsPanelControl");
        WireUpSubPanelDrag<BoundaryRecordingPanel>("BoundaryRecordingPanelControl");
        WireUpSubPanelDrag<BoundaryPlayerPanel>("BoundaryPlayerPanelControl");
    }

    private void WireUpSubPanelDrag<T>(string controlName) where T : UserControl
    {
        var panel = this.FindControl<T>(controlName);
        if (panel == null) return;

        // Use reflection to check for DragMoved event
        var dragMovedEvent = typeof(T).GetEvent("DragMoved");
        if (dragMovedEvent != null)
        {
            dragMovedEvent.AddEventHandler(panel, new EventHandler<Vector>((sender, delta) =>
            {
                if (sender is Control control)
                {
                    var left = Canvas.GetLeft(control);
                    var top = Canvas.GetTop(control);
                    if (double.IsNaN(left)) left = 0;
                    if (double.IsNaN(top)) top = 0;
                    Canvas.SetLeft(control, left + delta.X);
                    Canvas.SetTop(control, top + delta.Y);
                }
            }));
        }
    }
}
