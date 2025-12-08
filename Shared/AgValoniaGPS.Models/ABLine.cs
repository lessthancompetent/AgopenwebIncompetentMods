namespace AgValoniaGPS.Models;

/// <summary>
/// Mode for creating AB lines/curves
/// </summary>
public enum ABCreationMode
{
    None,           // Not creating an AB line
    DriveAB,        // Drive from A to B - uses current position when tapping
    DrawAB,         // Draw on map - tap to place points
    APlusLine,      // Create from current position + heading
    Curve           // Record curve while driving
}

/// <summary>
/// Which point is being set in AB creation
/// </summary>
public enum ABPointStep
{
    None,
    SettingPointA,
    SettingPointB
}

/// <summary>
/// Represents an AB guidance line for field operations
/// </summary>
public class ABLine
{
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Starting point of the AB line (Point A)
    /// </summary>
    public Position PointA { get; set; } = new();

    /// <summary>
    /// Ending point of the AB line (Point B)
    /// </summary>
    public Position PointB { get; set; } = new();

    /// <summary>
    /// Heading angle in degrees
    /// </summary>
    public double Heading { get; set; }

    /// <summary>
    /// Whether this is a curve line (as opposed to straight)
    /// </summary>
    public bool IsCurve { get; set; }

    /// <summary>
    /// Whether this AB line is currently active for guidance
    /// </summary>
    public bool IsActive { get; set; }
}