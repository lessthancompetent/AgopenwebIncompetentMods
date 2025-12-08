using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using System;

namespace AgValoniaGPS.Views.Controls.Panels;

public partial class BottomNavigationPanel : UserControl
{
    private Border? _mainPanel;
    private Border? _abLineFlyoutPanel;
    private Border? _flagsFlyoutPanel;
    private Button? _abLineMenuButton;
    private Button? _flagMenuButton;
    private Grid? _dragHandle;
    private bool _isABLineFlyoutOpen = false;
    private bool _isFlagsFlyoutOpen = false;

    // Drag state
    private bool _isDragging = false;
    private Point _dragStartPoint;
    private double _panelStartLeft;
    private double _panelStartTop;
    private DispatcherTimer? _holdTimer;
    private bool _isHolding = false;

    // Orientation (0 = horizontal, 1 = vertical)
    private int _orientation = 0;

    public BottomNavigationPanel()
    {
        InitializeComponent();

        // Find controls
        _mainPanel = this.FindControl<Border>("MainPanel");
        _abLineMenuButton = this.FindControl<Button>("ABLineMenuButton");
        _flagMenuButton = this.FindControl<Button>("FlagMenuButton");
        _abLineFlyoutPanel = this.FindControl<Border>("ABLineFlyoutPanel");
        _flagsFlyoutPanel = this.FindControl<Border>("FlagsFlyoutPanel");
        _dragHandle = this.FindControl<Grid>("DragHandle");

        // Wire up menu buttons to toggle flyouts
        if (_abLineMenuButton != null)
        {
            _abLineMenuButton.Click += ABLineMenuButton_Click;
        }

        if (_flagMenuButton != null)
        {
            _flagMenuButton.Click += FlagMenuButton_Click;
        }

        // Wire up drag handle
        if (_dragHandle != null)
        {
            _dragHandle.PointerPressed += DragHandle_PointerPressed;
            _dragHandle.PointerMoved += DragHandle_PointerMoved;
            _dragHandle.PointerReleased += DragHandle_PointerReleased;
        }

        // Setup hold timer for drag detection
        _holdTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(300)
        };
        _holdTimer.Tick += HoldTimer_Tick;

        // Close flyouts when clicking outside
        this.PointerPressed += OnPanelPointerPressed;
    }

    private void DragHandle_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_mainPanel == null) return;

        _dragStartPoint = e.GetPosition(this.Parent as Visual);
        _panelStartLeft = Canvas.GetLeft(_mainPanel);
        _panelStartTop = Canvas.GetTop(_mainPanel);

        if (double.IsNaN(_panelStartLeft)) _panelStartLeft = 0;
        if (double.IsNaN(_panelStartTop)) _panelStartTop = 0;

        _isHolding = false;
        _holdTimer?.Start();

        e.Handled = true;
    }

    private void HoldTimer_Tick(object? sender, EventArgs e)
    {
        _holdTimer?.Stop();
        _isHolding = true;
        _isDragging = true;
    }

    private void DragHandle_PointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_isDragging || _mainPanel == null) return;

        var currentPoint = e.GetPosition(this.Parent as Visual);
        var deltaX = currentPoint.X - _dragStartPoint.X;
        var deltaY = currentPoint.Y - _dragStartPoint.Y;

        Canvas.SetLeft(_mainPanel, _panelStartLeft + deltaX);
        Canvas.SetTop(_mainPanel, _panelStartTop + deltaY);

        // Update flyout positions to follow
        UpdateFlyoutPositions();

        e.Handled = true;
    }

    private void DragHandle_PointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        _holdTimer?.Stop();

        if (!_isHolding && !_isDragging)
        {
            // Quick tap - rotate orientation
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

        _orientation = (_orientation + 1) % 2;

        if (_orientation == 0)
        {
            // Horizontal
            buttonStack.Orientation = Avalonia.Layout.Orientation.Horizontal;
        }
        else
        {
            // Vertical
            buttonStack.Orientation = Avalonia.Layout.Orientation.Vertical;
        }
    }

    private void UpdateFlyoutPositions()
    {
        if (_mainPanel == null) return;

        var panelLeft = Canvas.GetLeft(_mainPanel);
        var panelTop = Canvas.GetTop(_mainPanel);

        if (double.IsNaN(panelLeft)) panelLeft = 0;
        if (double.IsNaN(panelTop)) panelTop = -78; // Default from XAML

        // Get main panel width, use estimate if not yet measured
        var mainPanelWidth = _mainPanel.Bounds.Width > 0 ? _mainPanel.Bounds.Width : 700;

        // Position flyouts above the main panel
        // Use estimated height if bounds not yet calculated (first show)
        if (_abLineFlyoutPanel != null)
        {
            // AB flyout has ~8 buttons @ 64px + spacing + padding = ~580px estimated
            var abFlyoutHeight = _abLineFlyoutPanel.Bounds.Height > 0
                ? _abLineFlyoutPanel.Bounds.Height
                : 580;
            Canvas.SetLeft(_abLineFlyoutPanel, panelLeft + mainPanelWidth - 70);
            Canvas.SetTop(_abLineFlyoutPanel, panelTop - abFlyoutHeight - 10);
        }

        if (_flagsFlyoutPanel != null)
        {
            // Flags flyout has 4 buttons @ 64px + spacing + padding = ~320px estimated
            var flagsFlyoutHeight = _flagsFlyoutPanel.Bounds.Height > 0
                ? _flagsFlyoutPanel.Bounds.Height
                : 320;
            Canvas.SetLeft(_flagsFlyoutPanel, panelLeft + mainPanelWidth - 130);
            Canvas.SetTop(_flagsFlyoutPanel, panelTop - flagsFlyoutHeight - 10);
        }
    }

    private void ABLineMenuButton_Click(object? sender, RoutedEventArgs e)
    {
        // Close flags flyout if open
        CloseFlagsFlyout();
        ToggleABLineFlyout();
        e.Handled = true;
    }

    private void FlagMenuButton_Click(object? sender, RoutedEventArgs e)
    {
        // Close AB line flyout if open
        CloseABLineFlyout();
        ToggleFlagsFlyout();
        e.Handled = true;
    }

    private void ToggleABLineFlyout()
    {
        _isABLineFlyoutOpen = !_isABLineFlyoutOpen;
        if (_abLineFlyoutPanel != null)
        {
            if (_isABLineFlyoutOpen)
            {
                // Height depends on HasActiveTrack - check DataContext
                // 3 buttons (64px each) + spacing (6px * 2) + padding (16px) = ~220px without active track
                // 7 items + 2 separators + nudge rows when active = ~580px with active track
                double estimatedHeight = 230; // Default: no active track (3 buttons)

                if (DataContext is AgValoniaGPS.ViewModels.MainViewModel vm && vm.HasActiveTrack)
                {
                    estimatedHeight = 580; // Full menu with active track
                }

                PositionFlyoutAboveButton(_abLineFlyoutPanel, _abLineMenuButton, estimatedHeight);
            }
            _abLineFlyoutPanel.IsVisible = _isABLineFlyoutOpen;
        }
    }

    private void ToggleFlagsFlyout()
    {
        _isFlagsFlyoutOpen = !_isFlagsFlyoutOpen;
        if (_flagsFlyoutPanel != null)
        {
            if (_isFlagsFlyoutOpen)
            {
                // Position relative to the button
                PositionFlyoutAboveButton(_flagsFlyoutPanel, _flagMenuButton, 320);
            }
            _flagsFlyoutPanel.IsVisible = _isFlagsFlyoutOpen;
        }
    }

    private void PositionFlyoutAboveButton(Border flyout, Button? button, double estimatedHeight)
    {
        if (button == null || _mainPanel == null) return;

        // Get the button's position relative to the main panel
        var buttonBounds = button.Bounds;
        var buttonPosition = button.TranslatePoint(new Point(0, 0), _mainPanel);

        if (buttonPosition.HasValue)
        {
            // Get the main panel's canvas position
            var panelLeft = Canvas.GetLeft(_mainPanel);
            var panelTop = Canvas.GetTop(_mainPanel);
            if (double.IsNaN(panelLeft)) panelLeft = 0;
            if (double.IsNaN(panelTop)) panelTop = -78;

            // Always use estimated height for consistent first-open positioning
            // The panel renders the same every time, so this is reliable
            var flyoutHeight = estimatedHeight;

            // Calculate X position - align flyout's right edge roughly with button
            var flyoutWidth = 80.0; // Single column of 64px buttons + padding
            var flyoutLeft = panelLeft + buttonPosition.Value.X + buttonBounds.Width - flyoutWidth;

            // Position flyout above the main panel
            Canvas.SetLeft(flyout, flyoutLeft);
            Canvas.SetTop(flyout, panelTop - flyoutHeight - 10);
        }
    }

    private void CloseABLineFlyout()
    {
        _isABLineFlyoutOpen = false;
        if (_abLineFlyoutPanel != null)
        {
            _abLineFlyoutPanel.IsVisible = false;
        }
    }

    private void CloseFlagsFlyout()
    {
        _isFlagsFlyoutOpen = false;
        if (_flagsFlyoutPanel != null)
        {
            _flagsFlyoutPanel.IsVisible = false;
        }
    }

    private void CloseAllFlyouts()
    {
        CloseABLineFlyout();
        CloseFlagsFlyout();
    }

    private void OnPanelPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        // If any flyout is open and user clicked outside of it, close it
        if (_isABLineFlyoutOpen && _abLineFlyoutPanel != null)
        {
            var position = e.GetPosition(_abLineFlyoutPanel);
            var bounds = _abLineFlyoutPanel.Bounds;

            // Check if click is outside the flyout panel bounds
            if (position.X < 0 || position.Y < 0 ||
                position.X > bounds.Width || position.Y > bounds.Height)
            {
                // But don't close if they clicked the menu button
                if (_abLineMenuButton != null)
                {
                    var menuPos = e.GetPosition(_abLineMenuButton);
                    var menuBounds = _abLineMenuButton.Bounds;
                    if (menuPos.X >= 0 && menuPos.Y >= 0 &&
                        menuPos.X <= menuBounds.Width && menuPos.Y <= menuBounds.Height)
                    {
                        // Clicked menu button, let the Click handler deal with it
                        return;
                    }
                }

                CloseABLineFlyout();
            }
        }

        if (_isFlagsFlyoutOpen && _flagsFlyoutPanel != null)
        {
            var position = e.GetPosition(_flagsFlyoutPanel);
            var bounds = _flagsFlyoutPanel.Bounds;

            // Check if click is outside the flyout panel bounds
            if (position.X < 0 || position.Y < 0 ||
                position.X > bounds.Width || position.Y > bounds.Height)
            {
                // But don't close if they clicked the menu button
                if (_flagMenuButton != null)
                {
                    var menuPos = e.GetPosition(_flagMenuButton);
                    var menuBounds = _flagMenuButton.Bounds;
                    if (menuPos.X >= 0 && menuPos.Y >= 0 &&
                        menuPos.X <= menuBounds.Width && menuPos.Y <= menuBounds.Height)
                    {
                        // Clicked menu button, let the Click handler deal with it
                        return;
                    }
                }

                CloseFlagsFlyout();
            }
        }
    }

    /// <summary>
    /// Close all flyouts when any action button is clicked.
    /// Call this from flyout button click handlers if needed.
    /// </summary>
    public void CloseFlyoutOnAction()
    {
        CloseAllFlyouts();
    }
}
