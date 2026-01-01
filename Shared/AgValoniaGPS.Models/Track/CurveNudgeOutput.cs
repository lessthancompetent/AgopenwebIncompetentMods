using AgValoniaGPS.Models.Base;
using System.Collections.Generic;

namespace AgValoniaGPS.Models.Track;

/// <summary>
/// Output from curve nudging calculation.
/// </summary>
/// <param name="NewCurvePoints">New curve points after nudging, filtering, and smoothing. Empty if curve is too short.</param>
public record CurveNudgeOutput(List<Vec3> NewCurvePoints);
