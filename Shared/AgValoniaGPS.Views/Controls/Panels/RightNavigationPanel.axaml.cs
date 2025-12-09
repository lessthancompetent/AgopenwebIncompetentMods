using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Threading;

namespace AgValoniaGPS.Views.Controls.Panels;

public partial class RightNavigationPanel : UserControl
{
    // Drag state
    private bool _isDragging = false;
    private Point _dragStartPoint;
    private double _panelStartLeft;
    private double _panelStartTop;
    private DispatcherTimer? _holdTimer;
    private bool _isHolding = false;

    /// <summary>
    /// Event raised when the user is dragging the panel.
    /// Provides the new absolute position (Left, Top).
    /// </summary>
    public event EventHandler<Point>? DragMoved;

    public RightNavigationPanel()
    {
        InitializeComponent();

        // Wire up drag handle events
        var dragHandle = this.FindControl<Grid>("DragHandle");
        if (dragHandle != null)
        {
            dragHandle.PointerPressed += DragHandle_PointerPressed;
            dragHandle.PointerMoved += DragHandle_PointerMoved;
            dragHandle.PointerReleased += DragHandle_PointerReleased;
        }

        // Setup hold timer for drag detection
        _holdTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(300)
        };
        _holdTimer.Tick += HoldTimer_Tick;
    }

    private void DragHandle_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Grid handle)
        {
            // Get position relative to parent for smooth dragging
            _dragStartPoint = e.GetPosition(this.Parent as Visual);

            // Get current canvas position - handle Canvas.Right case
            _panelStartLeft = Canvas.GetLeft(this);
            _panelStartTop = Canvas.GetTop(this);

            // If using Canvas.Right, convert to Left
            if (double.IsNaN(_panelStartLeft))
            {
                var parent = this.Parent as Visual;
                if (parent is Canvas canvas)
                {
                    var parentBounds = canvas.Bounds;
                    var rightValue = Canvas.GetRight(this);
                    if (double.IsNaN(rightValue)) rightValue = 20;
                    _panelStartLeft = parentBounds.Width - this.Bounds.Width - rightValue;
                }
                else
                {
                    _panelStartLeft = 20;
                }
            }
            if (double.IsNaN(_panelStartTop)) _panelStartTop = 100;

            _isHolding = false;
            _holdTimer?.Start();

            // Close any tooltips
            ToolTip.SetIsOpen(handle, false);
            e.Handled = true;
        }
    }

    private void HoldTimer_Tick(object? sender, EventArgs e)
    {
        _holdTimer?.Stop();
        _isHolding = true;
        _isDragging = true;
    }

    private void DragHandle_PointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_isDragging) return;

        var currentPoint = e.GetPosition(this.Parent as Visual);
        var deltaX = currentPoint.X - _dragStartPoint.X;
        var deltaY = currentPoint.Y - _dragStartPoint.Y;

        // Calculate new absolute position
        var newLeft = _panelStartLeft + deltaX;
        var newTop = _panelStartTop + deltaY;

        // Fire event with new position
        DragMoved?.Invoke(this, new Point(newLeft, newTop));

        e.Handled = true;
    }

    private void DragHandle_PointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        _holdTimer?.Stop();

        if (!_isHolding && !_isDragging)
        {
            // Quick tap - rotate the panel
            RotatePanel();
        }

        _isDragging = false;
        _isHolding = false;
        e.Handled = true;
    }

    private void RotatePanel()
    {
        var buttonStack = this.FindControl<StackPanel>("ButtonStack");
        if (buttonStack == null) return;

        // Toggle orientation
        bool goingHorizontal = buttonStack.Orientation == Orientation.Vertical;
        buttonStack.Orientation = goingHorizontal ? Orientation.Horizontal : Orientation.Vertical;

        // Reverse children order for counter-clockwise rotation effect
        // When vertical: Handle at top, buttons below
        // When horizontal: Buttons on left, handle on right (counter-clockwise from vertical)
        var children = buttonStack.Children.ToList();
        buttonStack.Children.Clear();
        for (int i = children.Count - 1; i >= 0; i--)
        {
            buttonStack.Children.Add(children[i]);
        }
    }
}
