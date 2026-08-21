// AgOpenWeb
// Copyright (C) 2024-2026 AgOpenWeb Contributors
//
// Licensed under GNU GPL v3. See LICENSE.md.

using AgOpenWeb.Services.Track;

namespace AgOpenWeb.Services.Tests;

/// <summary>
/// Pins the lateral tool-offset model: pass lines are TOOL centerlines; the
/// vehicle guidance line is the tool line shifted −Offset in the vehicle
/// frame, so the field-frame term flips with travel direction, and adjacent
/// opposite-direction passes sit w∓2·Offset apart (the asymmetric U-turn
/// throw). The sign convention is Offset positive = tool RIGHT of travel.
/// </summary>
[TestFixture]
public class GuidanceGeometryTests
{
    private const double W = 6.0;   // width minus overlap
    private const double O = 1.5;   // tool 1.5 m RIGHT of the tractor

    [Test]
    public void ToolLine_IsDirectionIndependent()
    {
        Assert.That(GuidanceGeometry.ToolDistAway(3, W, 0.2), Is.EqualTo(18.2).Within(1e-9));
    }

    // Travel along the track heading: tool sits track-right of the tractor,
    // so the VEHICLE line is track-LEFT of the tool line (w·k − o).
    [Test]
    public void VehicleLine_SameWay_ShiftsLeftOfToolLine()
    {
        Assert.That(GuidanceGeometry.VehicleDistAway(3, W, 0, O, travelSameWay: true),
            Is.EqualTo(18.0 - O).Within(1e-9));
    }

    // Travel against the track heading: tool-right is track-LEFT, so the
    // vehicle line lands track-right of the tool line (w·k + o).
    [Test]
    public void VehicleLine_OppositeWay_ShiftsRightOfToolLine()
    {
        Assert.That(GuidanceGeometry.VehicleDistAway(3, W, 0, O, travelSameWay: false),
            Is.EqualTo(18.0 + O).Within(1e-9));
    }

    [Test]
    public void VehicleLine_NegativeOffset_MirrorsSign()
    {
        Assert.That(GuidanceGeometry.VehicleDistAway(0, W, 0, -O, travelSameWay: true),
            Is.EqualTo(+O).Within(1e-9));
        Assert.That(GuidanceGeometry.VehicleDistAway(0, W, 0, -O, travelSameWay: false),
            Is.EqualTo(-O).Within(1e-9));
    }

    [Test]
    public void NudgeAddsInFieldFrame_IndependentOfDirection()
    {
        double same = GuidanceGeometry.VehicleDistAway(2, W, 0.3, O, true);
        double opp = GuidanceGeometry.VehicleDistAway(2, W, 0.3, O, false);
        Assert.That(same, Is.EqualTo(12.0 + 0.3 - O).Within(1e-9));
        Assert.That(opp, Is.EqualTo(12.0 + 0.3 + O).Within(1e-9));
    }

    /// <summary>
    /// The asymmetric turn throw falls out of differencing: alternating
    /// passes driven opposite ways sit w−2o apart on one pairing and w+2o
    /// on the other; the round trip over two turns still advances 2w so no
    /// drift accumulates.
    /// </summary>
    [Test]
    public void AdjacentOppositePasses_AlternateTightAndWideSpacing()
    {
        double p0 = GuidanceGeometry.VehicleDistAway(0, W, 0, O, true);
        double p1 = GuidanceGeometry.VehicleDistAway(1, W, 0, O, false);
        double p2 = GuidanceGeometry.VehicleDistAway(2, W, 0, O, true);

        Assert.That(p1 - p0, Is.EqualTo(W + 2 * O).Within(1e-9), "wide pairing");
        Assert.That(p2 - p1, Is.EqualTo(W - 2 * O).Within(1e-9), "tight pairing");
        Assert.That(p2 - p0, Is.EqualTo(2 * W).Within(1e-9), "no cumulative drift");
    }

    /// <summary>
    /// Seam-tightness identity: whatever direction each pass is driven, the
    /// TOOL band always lands on the tool line — steering the vehicle line of
    /// pass k puts the tool (vehicle + o·rightOfTravel) exactly at w·k.
    /// </summary>
    [Test]
    public void SteeringVehicleLine_LandsToolBandOnToolLine([Values(true, false)] bool sameWay)
    {
        double vehicle = GuidanceGeometry.VehicleDistAway(4, W, 0, O, sameWay);
        // rightOfTravel in track coordinates: +1 when travelling the track
        // heading, −1 against it.
        double toolBand = vehicle + (sameWay ? +O : -O);
        Assert.That(toolBand, Is.EqualTo(GuidanceGeometry.ToolDistAway(4, W, 0)).Within(1e-9));
    }
}
