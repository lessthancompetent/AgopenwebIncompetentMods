// AgOpenWeb
// Copyright (C) 2024-2026 AgOpenWeb Contributors
//
// Licensed under GNU GPL v3. See LICENSE.md.

using AgOpenWeb.Services.Elevation;

namespace AgOpenWeb.Services.Tests;

/// <summary>
/// Terrain elevation grid: 5 m cells of running-mean RTK altitude. The service
/// records every sample it is given (throttling is the caller's concern), so
/// these tests drive Record directly.
/// </summary>
[TestFixture]
public class ElevationMapServiceTests
{
    private ElevationMapService _svc = null!;

    [SetUp]
    public void SetUp() => _svc = new ElevationMapService();

    [Test]
    public void Record_RunningMeanPerCell()
    {
        // All inside the same 5 m cell (0..5, 0..5)
        _svc.Record(1.0, 1.0, 100.0);
        _svc.Record(4.0, 4.0, 102.0);
        _svc.Record(2.5, 2.5, 104.0);

        var cells = _svc.GetCells();
        Assert.That(cells, Has.Count.EqualTo(1));
        Assert.That(cells[0].AltM, Is.EqualTo(102.0).Within(1e-9), "running mean of 100/102/104");
        Assert.That(cells[0].CellE, Is.EqualTo(2.5), "cell centre easting");
        Assert.That(cells[0].CellN, Is.EqualTo(2.5), "cell centre northing");
    }

    [Test]
    public void Record_SeparateCells_IncludingNegativeCoords()
    {
        _svc.Record(1.0, 1.0, 100.0);    // cell (0, 0)
        _svc.Record(6.0, 1.0, 101.0);    // cell (1, 0)
        _svc.Record(-1.0, -1.0, 99.0);   // cell (-1, -1) — Floor, not truncation

        Assert.That(_svc.CellCount, Is.EqualTo(3));
        var cells = _svc.GetCells();
        Assert.That(cells, Has.One.Matches<(double E, double N, double A)>(
            c => c.E == -2.5 && c.N == -2.5 && c.A == 99.0),
            "negative coords land in their own cell with centre (-2.5, -2.5)");
    }

    [Test]
    public void MinMax_TrackRecordedRange()
    {
        Assert.That(_svc.AltitudeRange, Is.Null, "no samples → no range");

        _svc.Record(1, 1, 105.2);
        _svc.Record(10, 10, 98.7);
        _svc.Record(20, 20, 110.4);

        var range = _svc.AltitudeRange;
        Assert.That(range, Is.Not.Null);
        Assert.That(range!.Value.MinM, Is.EqualTo(98.7).Within(1e-9));
        Assert.That(range.Value.MaxM, Is.EqualTo(110.4).Within(1e-9));
    }

    [Test]
    public void Clear_DropsEverything()
    {
        _svc.Record(1, 1, 100);
        _svc.Clear();
        Assert.That(_svc.CellCount, Is.EqualTo(0));
        Assert.That(_svc.AltitudeRange, Is.Null);
        Assert.That(_svc.BuildElevationJson(), Is.EqualTo("{}"));
    }

    [Test]
    public void SaveLoad_RoundTripsGridAndRange()
    {
        string dir = Path.Combine(Path.GetTempPath(),
            "agow-terrain-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            _svc.Record(1, 1, 100.0);
            _svc.Record(2, 2, 102.0);     // same cell → mean 101
            _svc.Record(7, 1, 98.5);      // cell (1, 0)
            _svc.Record(-3, 12, 104.25);  // cell (-1, 2)
            _svc.SaveToFile(dir);
            Assert.That(File.Exists(Path.Combine(dir, "elevation.json")), Is.True);

            var fresh = new ElevationMapService();
            fresh.LoadFromFile(dir);

            Assert.That(fresh.CellCount, Is.EqualTo(3));
            var cells = fresh.GetCells().OrderBy(c => c.CellE).ThenBy(c => c.CellN).ToList();
            Assert.That(cells[0], Is.EqualTo((-2.5, 12.5, 104.25)));
            Assert.That(cells[1].AltM, Is.EqualTo(101.0).Within(0.001), "cell mean survives the round-trip");
            Assert.That(cells[2].AltM, Is.EqualTo(98.5).Within(0.001));
            var range = fresh.AltitudeRange;
            Assert.That(range, Is.Not.Null);
            Assert.That(range!.Value.MinM, Is.EqualTo(98.5).Within(0.001));
            Assert.That(range.Value.MaxM, Is.EqualTo(104.25).Within(0.001));

            // Loaded counts keep accumulating correctly: cell (0,0) holds
            // 2 samples at mean 101; a third sample of 104 → mean 102.
            fresh.Record(1, 1, 104.0);
            var cell00 = fresh.GetCells().Single(c => c.CellE == 2.5 && c.CellN == 2.5);
            Assert.That(cell00.AltM, Is.EqualTo(102.0).Within(0.001));
        }
        finally
        {
            try { Directory.Delete(dir, true); } catch { /* temp cleanup */ }
        }
    }

    [Test]
    public void SaveToFile_SkipsWhenCleanOrEmpty()
    {
        string dir = Path.Combine(Path.GetTempPath(),
            "agow-terrain-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            _svc.SaveToFile(dir); // empty grid → nothing written
            Assert.That(File.Exists(Path.Combine(dir, "elevation.json")), Is.False);

            _svc.Record(1, 1, 100);
            _svc.SaveToFile(dir);
            var path = Path.Combine(dir, "elevation.json");
            var stamp = File.GetLastWriteTimeUtc(path);

            _svc.SaveToFile(dir); // clean since last save → no rewrite
            Assert.That(File.GetLastWriteTimeUtc(path), Is.EqualTo(stamp));
        }
        finally
        {
            try { Directory.Delete(dir, true); } catch { /* temp cleanup */ }
        }
    }

    [Test]
    public void BuildElevationJson_CarriesCellSizeRangeAndCentres()
    {
        _svc.Record(1, 1, 100.0);
        _svc.Record(7, 1, 104.0);

        var json = _svc.BuildElevationJson();
        Assert.That(json, Does.StartWith("{\"cell\":5,"));
        Assert.That(json, Does.Contain("\"min\":100.0"));
        Assert.That(json, Does.Contain("\"max\":104.0"));
        Assert.That(json, Does.Contain("[2.5,2.5,100.0]"));
        Assert.That(json, Does.Contain("[7.5,2.5,104.0]"));
    }

    [Test]
    public void BuildGeoJson_PointFeaturesWithAlt()
    {
        Assert.That(_svc.BuildGeoJson(-43.5, 172.6), Is.Null, "empty grid → no export");

        _svc.Record(1, 1, 100.0);
        var geo = _svc.BuildGeoJson(-43.5, 172.6);
        Assert.That(geo, Is.Not.Null);
        Assert.That(geo, Does.StartWith("{\"type\":\"FeatureCollection\""));
        Assert.That(geo, Does.Contain("\"type\":\"Point\""));
        Assert.That(geo, Does.Contain("\"alt\":100"));
    }
}
