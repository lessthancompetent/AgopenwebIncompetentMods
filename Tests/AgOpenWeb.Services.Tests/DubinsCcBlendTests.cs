// AgOpenWeb
// Licensed under GNU GPL v3. See LICENSE.md.
//
// Continuous-curvature blend (DubinsTurn ccBlend) invariants. The blend replaces
// the region around each arc/line junction with a cubic Bezier so curvature ramps
// instead of stepping; these tests pin the properties that make that safe to use
// in the planner: endpoints exact, length near-analytic, and the junction
// curvature step actually reduced (the whole point of the port).

using System;
using System.Collections.Generic;
using AgOpenWeb.Models.Base;
using AgOpenWeb.Services.RoutePlanning;
using NUnit.Framework;

namespace AgOpenWeb.Services.Tests;

[TestFixture]
public class DubinsCcBlendTests
{
    // Canonical aligned 180-degree headland U-turn: pass ends 3 m apart (drill
    // spacing), both heading north, turn radius 6 m — the everyday case.
    private static readonly Vec3 Start = new(0, 0, 0);
    private static readonly Vec3 Goal = new(3, 0, Math.PI);
    private const double R = 6.0;

    private static List<Vec2> Shortest(double blend) =>
        DubinsTurn.AllPaths(Start, Goal, R, blend)[0].Coords;

    [Test]
    public void Blend_preserves_endpoints_exactly()
    {
        var b = Shortest(1.5);
        Assert.That(b[0].Easting, Is.EqualTo(Start.Easting).Within(1e-9));
        Assert.That(b[0].Northing, Is.EqualTo(Start.Northing).Within(1e-9));
        Assert.That(b[^1].Easting, Is.EqualTo(Goal.Easting).Within(1e-9));
        Assert.That(b[^1].Northing, Is.EqualTo(Goal.Northing).Within(1e-9));
    }

    [Test]
    public void Blend_keeps_length_close_to_plain_dubins()
    {
        double raw = PolylineLength(Shortest(0));
        double cc = PolylineLength(Shortest(1.5));
        Assert.That(cc, Is.EqualTo(raw).Within(raw * 0.02),
            "CC path length should stay within 2% of the plain Dubins length");
    }

    [Test]
    public void Blend_reduces_junction_curvature_step()
    {
        double rawStep = MaxCurvatureStep(Shortest(0));
        double ccStep = MaxCurvatureStep(Shortest(1.5));
        Assert.That(ccStep, Is.LessThan(rawStep * 0.55),
            $"blended max curvature step {ccStep:F4} should be well under raw {rawStep:F4}");
    }

    [Test]
    public void Blend_stays_near_the_plain_path()
    {
        var raw = Shortest(0);
        var cc = Shortest(1.5);
        double worst = 0;
        foreach (var p in cc)
        {
            double best = double.MaxValue;
            foreach (var q in raw)
            {
                double d = (p - q).GetLength();
                if (d < best) best = d;
            }
            if (best > worst) worst = best;
        }
        Assert.That(worst, Is.LessThan(1.0),
            "CC deviation from the analytic path should stay under 1 m at 1.5 m blend");
    }

    [Test]
    public void Too_short_segments_fall_back_to_plain_path()
    {
        // Goal so close that the blend cuts would overlap — must return the
        // unblended coords rather than a mangled path.
        var tight = new Vec3(0.5, 0.2, 0.1);
        var plain = DubinsTurn.AllPaths(Start, tight, R, 0)[0].Coords;
        var blended = DubinsTurn.AllPaths(Start, tight, R, 1.5)[0].Coords;
        Assert.That(blended.Count, Is.EqualTo(plain.Count));
    }

    // ---- helpers ----

    private static double PolylineLength(List<Vec2> pts)
    {
        double sum = 0;
        for (int i = 1; i < pts.Count; i++) sum += (pts[i] - pts[i - 1]).GetLength();
        return sum;
    }

    /// <summary>Largest change in discrete curvature between successive samples —
    /// the "steering jerk" the blend exists to remove. Curvature per sample from
    /// heading deltas over arc length.</summary>
    private static double MaxCurvatureStep(List<Vec2> pts)
    {
        var curv = new List<double>();
        for (int i = 1; i < pts.Count - 1; i++)
        {
            var a = pts[i] - pts[i - 1];
            var b = pts[i + 1] - pts[i];
            double la = a.GetLength(), lb = b.GetLength();
            if (la < 1e-6 || lb < 1e-6) continue;
            double ha = Math.Atan2(a.Easting, a.Northing);
            double hb = Math.Atan2(b.Easting, b.Northing);
            double dh = hb - ha;
            while (dh > Math.PI) dh -= 2 * Math.PI;
            while (dh < -Math.PI) dh += 2 * Math.PI;
            curv.Add(dh / ((la + lb) / 2.0));
        }
        double worst = 0;
        for (int i = 1; i < curv.Count; i++)
            worst = Math.Max(worst, Math.Abs(curv[i] - curv[i - 1]));
        return worst;
    }
}
