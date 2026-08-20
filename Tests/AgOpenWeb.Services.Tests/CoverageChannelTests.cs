// AgOpenWeb
// Copyright (C) 2024-2026 AgOpenWeb Contributors
//
// Licensed under GNU GPL v3. See LICENSE.md.

using AgOpenWeb.Models.Base;
using AgOpenWeb.Models.Configuration;
using AgOpenWeb.Services.Coverage;

namespace AgOpenWeb.Services.Tests;

/// <summary>
/// Dual coverage channels for deliberate double-coverage jobs (cross-drilling):
/// channel 1's marking and "already covered" checks must be fully independent
/// of channel 0, so the crossing family neither reads nor trips the other's
/// paint — the reason sections used to cut for ~3 m at every crossing.
/// </summary>
[TestFixture]
[NonParallelizable] // ConfigurationStore singleton.
public class CoverageChannelTests
{
    private CoverageMapService _svc = null!;

    [SetUp]
    public void SetUp()
    {
        _svc = new CoverageMapService(ConfigurationStore.Instance);
        _svc.SetFieldBounds(0, 100, 0, 100);
    }

    [Test]
    public void Channel1_DoesNotSeeChannel0Paint()
    {
        _svc.MarkRectangleCovered(10, 20, 10, 20);
        Assert.That(_svc.IsPointCovered(15, 15), Is.True, "channel 0 sees its own paint");

        _svc.ActiveChannel = 1;
        Assert.That(_svc.IsPointCovered(15, 15), Is.False,
            "the crossing family must NOT read the first family's paint as covered");
        var seg = _svc.GetSegmentCoverage(new Vec2(15, 15), 0, 1.5);
        Assert.That(seg.CoveragePercent, Is.EqualTo(0),
            "section-control's segment query follows the active channel too");
    }

    [Test]
    public void Channels_MarkIndependently_AndChannel0Unaffected()
    {
        _svc.MarkRectangleCovered(10, 20, 10, 20);       // ch 0
        _svc.ActiveChannel = 1;
        _svc.MarkRectangleCovered(12, 18, 12, 18);       // ch 1 over the same ground

        Assert.That(_svc.IsPointCovered(15, 15), Is.True, "channel 1 now covered where it worked");
        Assert.That(_svc.IsPointCovered(19.5, 19.5), Is.False, "outside channel 1's band");

        _svc.ActiveChannel = 0;
        Assert.That(_svc.IsPointCovered(15, 15), Is.True, "channel 0 keeps its own paint");
        Assert.That(_svc.IsPointCovered(19.5, 19.5), Is.True);
    }

    [Test]
    public void ClearAll_DropsSecondChannel_AndResetsActive()
    {
        _svc.ActiveChannel = 1;
        _svc.MarkRectangleCovered(10, 20, 10, 20);
        _svc.ClearAll();

        Assert.That(_svc.ActiveChannel, Is.EqualTo(0), "a cleared job starts back on channel 0");
        Assert.That(_svc.IsPointCovered(15, 15), Is.False);
        _svc.ActiveChannel = 1;
        Assert.That(_svc.IsPointCovered(15, 15), Is.False);
    }

    [Test]
    public void SecondChannel_SurvivesSaveAndLoad()
    {
        string dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "agow-covch-" + System.Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(dir);
        try
        {
            _svc.MarkRectangleCovered(10, 20, 10, 20);
            _svc.ActiveChannel = 1;
            _svc.MarkRectangleCovered(30, 40, 30, 40);
            _svc.ActiveChannel = 0;
            _svc.SaveToFile(dir);

            var fresh = new CoverageMapService(ConfigurationStore.Instance);
            fresh.SetFieldBounds(0, 100, 0, 100);
            fresh.LoadFromFile(dir);

            Assert.That(fresh.IsPointCovered(15, 15), Is.True, "channel 0 restored");
            Assert.That(fresh.IsPointCovered(35, 35), Is.False, "channel 1 paint invisible to channel 0");
            fresh.ActiveChannel = 1;
            Assert.That(fresh.IsPointCovered(35, 35), Is.True, "channel 1 restored");
            Assert.That(fresh.IsPointCovered(15, 15), Is.False, "channel 0 paint invisible to channel 1");
        }
        finally
        {
            try { System.IO.Directory.Delete(dir, true); } catch { /* temp cleanup */ }
        }
    }
}
