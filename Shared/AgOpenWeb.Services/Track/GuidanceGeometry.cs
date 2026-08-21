// AgOpenWeb
// Copyright (C) 2024-2026 AgOpenWeb Contributors
//
// Licensed under GNU GPL v3. See LICENSE.md.

namespace AgOpenWeb.Services.Track;

/// <summary>
/// The one home of the pass-line geometry shared by live guidance, the U-turn
/// system and (via plan generation) the route planner. Multiple call sites
/// must produce byte-identical lines for the same pass — the U-turn handoff
/// depends on it — so the formulas live here and ONLY here.
///
/// Model: pass lines are TOOL centerlines spaced by (width − overlap); the
/// tool carries the lateral offset (Tool.Offset, right-positive in the tool
/// frame — see ToolPositionService.ApplyLateralOffset). The VEHICLE guidance
/// line for a pass is therefore the tool line shifted by −Offset in the
/// vehicle frame, which in FIELD frame flips sign with the direction of
/// travel: driving the track heading (sameWay) the vehicle sits at −Offset,
/// driving against it at +Offset. Adjacent opposite-direction passes then sit
/// w∓2·Offset apart — the asymmetric U-turn throw falls out of differencing
/// two calls to <see cref="VehicleDistAway"/> rather than any bespoke ±2o
/// term.
/// </summary>
public static class GuidanceGeometry
{
    /// <summary>
    /// Field-frame lateral distance (track-right positive) from the base
    /// track to the VEHICLE guidance line of pass <paramref name="pathsAway"/>.
    /// <paramref name="travelSameWay"/> is the pass's direction of TRAVEL
    /// relative to the track heading — for the live vehicle that is
    /// isHeadingSameWay XOR reversing; for a turn's exit pass it is the
    /// negation of the entry pass's value (turns reverse direction).
    /// </summary>
    public static double VehicleDistAway(
        int pathsAway, double widthMinusOverlap, double nudge,
        double toolOffset, bool travelSameWay)
        => widthMinusOverlap * pathsAway + nudge
           - (travelSameWay ? toolOffset : -toolOffset);

    /// <summary>
    /// The TOOL centerline of a pass — what coverage tiles against and what
    /// boundary/worked decisions must test. Direction-independent: the tool
    /// band lands here whichever way the pass is driven (that is the point
    /// of the vehicle-line correction).
    /// </summary>
    public static double ToolDistAway(int pathsAway, double widthMinusOverlap, double nudge)
        => widthMinusOverlap * pathsAway + nudge;
}
