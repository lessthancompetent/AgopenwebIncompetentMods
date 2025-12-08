using System;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using AgValoniaGPS.ViewModels;
using AgValoniaGPS.Views.Controls;
using AgValoniaGPS.iOS.Services;
using AgValoniaGPS.Models;

namespace AgValoniaGPS.iOS.Views;

/// <summary>
/// iOS MainView with ViewModel - wires up map control to ViewModel commands
/// </summary>
public partial class MainView : UserControl
{
    private DrawingContextMapControl? _mapControl;
    private MainViewModel? _viewModel;

    // Section control drag state
    private bool _isDraggingSection;
    private Point _dragStartPoint;

    // Bottom nav panel drag state
    private bool _isDraggingBottomNav;
    private Point _bottomNavDragStartPoint;

    public MainView()
    {
        Console.WriteLine("[MainView] Constructor starting...");
        InitializeComponent();
        Console.WriteLine("[MainView] InitializeComponent completed.");

        // Get reference to map control
        _mapControl = this.FindControl<DrawingContextMapControl>("MapControl");
    }

    public MainView(MainViewModel viewModel, MapService mapService) : this()
    {
        Console.WriteLine("[MainView] Setting DataContext to MainViewModel...");
        DataContext = viewModel;
        _viewModel = viewModel;

        // Register the map control with the MapService so it can receive commands
        if (_mapControl != null)
        {
            mapService.RegisterMapControl(_mapControl);
            Console.WriteLine("[MainView] MapControl registered with MapService.");

            viewModel.ZoomInRequested += () => _mapControl.Zoom(1.2);
            viewModel.ZoomOutRequested += () => _mapControl.Zoom(0.8);

            // Wire up MapClicked event for AB line creation
            _mapControl.MapClicked += OnMapClicked;
        }

        // Wire up position updates - when ViewModel properties change, update map control
        viewModel.PropertyChanged += OnViewModelPropertyChanged;

        Console.WriteLine("[MainView] DataContext set.");
    }

    private void OnMapClicked(object? sender, MapClickEventArgs e)
    {
        if (_viewModel == null) return;

        // For DriveAB mode, we use current GPS position (not the clicked position)
        // For DrawAB mode, we use the clicked map position
        if (_viewModel.CurrentABCreationMode == ABCreationMode.DriveAB)
        {
            // In DriveAB mode, any tap triggers setting the point at current GPS position
            _viewModel.SetABPointCommand?.Execute(null);
        }
        else if (_viewModel.CurrentABCreationMode == ABCreationMode.DrawAB)
        {
            // In DrawAB mode, pass the clicked map coordinates
            var mapPosition = new Position
            {
                Easting = e.Easting,
                Northing = e.Northing
            };
            _viewModel.SetABPointCommand?.Execute(mapPosition);
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // Update vehicle position when Easting, Northing, or Heading changes
        if (_mapControl != null && _viewModel != null)
        {
            if (e.PropertyName == nameof(MainViewModel.Easting) ||
                e.PropertyName == nameof(MainViewModel.Northing) ||
                e.PropertyName == nameof(MainViewModel.Heading))
            {
                // Convert heading from degrees to radians (ViewModel stores degrees, map expects radians)
                double headingRadians = _viewModel.Heading * Math.PI / 180.0;
                _mapControl.SetVehiclePosition(_viewModel.Easting, _viewModel.Northing, headingRadians);
            }
            else if (e.PropertyName == nameof(MainViewModel.EnableABClickSelection))
            {
                // Update map control click selection mode
                _mapControl.EnableClickSelection = _viewModel.EnableABClickSelection;
            }
        }
    }

    // Section Control drag handlers
    private void SectionControl_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Control control)
        {
            _isDraggingSection = true;
            _dragStartPoint = e.GetPosition(this);
            e.Pointer.Capture(control);
        }
    }

    private void SectionControl_PointerMoved(object? sender, PointerEventArgs e)
    {
        if (_isDraggingSection && sender is Control control)
        {
            var currentPoint = e.GetPosition(this);
            var deltaX = currentPoint.X - _dragStartPoint.X;
            var deltaY = currentPoint.Y - _dragStartPoint.Y;

            var currentLeft = Canvas.GetLeft(control);
            var currentTop = Canvas.GetTop(control);

            Canvas.SetLeft(control, currentLeft + deltaX);
            Canvas.SetTop(control, currentTop + deltaY);

            _dragStartPoint = currentPoint;
        }
    }

    private void SectionControl_PointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (sender is Control control)
        {
            _isDraggingSection = false;
            e.Pointer.Capture(null);
        }
    }

    // Bottom Navigation Panel drag handlers
    private void BottomNavPanel_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Control control)
        {
            _isDraggingBottomNav = true;
            _bottomNavDragStartPoint = e.GetPosition(this);
            e.Pointer.Capture(control);
        }
    }

    private void BottomNavPanel_PointerMoved(object? sender, PointerEventArgs e)
    {
        if (_isDraggingBottomNav && sender is Control control)
        {
            var currentPoint = e.GetPosition(this);
            var deltaX = currentPoint.X - _bottomNavDragStartPoint.X;
            var deltaY = currentPoint.Y - _bottomNavDragStartPoint.Y;

            var currentLeft = Canvas.GetLeft(control);
            var currentTop = Canvas.GetTop(control);

            Canvas.SetLeft(control, currentLeft + deltaX);
            Canvas.SetTop(control, currentTop + deltaY);

            _bottomNavDragStartPoint = currentPoint;
        }
    }

    private void BottomNavPanel_PointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (sender is Control control)
        {
            _isDraggingBottomNav = false;
            e.Pointer.Capture(null);
        }
    }
}
