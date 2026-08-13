using AgOpenWeb.Models;
using AgOpenWeb.Services.AutoSteer;
using NUnit.Framework;

namespace AgOpenWeb.Services.Tests;

/// <summary>
/// Fences the PGN 254 status byte so the wire-level engage signal can never
/// silently regress. Contract (stock AgOpenGPS): the byte is EXACTLY 0 or 1 —
/// real AIO firmware reads it whole as guidanceStatus and treats any nonzero
/// value as engaged, so auxiliary flags (GPS fix, switches) must never leak in.
/// The companion VM-level wiring tests ensure VehicleState.IsAutoSteerEngaged
/// actually gets set when the user toggles autosteer; this ensures that, once
/// set, the byte ends up right on the outbound packet.
/// </summary>
[TestFixture]
public class PgnBuilderEngageBitTests
{
    private const int STATUS_BYTE_INDEX = 7;

    [Test]
    public void BuildAutoSteerPgn_WhenEngaged_StatusIsOne()
    {
        var state = new VehicleState
        {
            IsAutoSteerEngaged = true,
            GpsValid = true,
            SteerSwitchActive = true
        };

        var packet = PgnBuilder.BuildAutoSteerPgn(ref state);

        Assert.That(packet[STATUS_BYTE_INDEX], Is.EqualTo(1),
            "Status must be exactly 1 when engaged (stock contract, no flag bits).");
    }

    [Test]
    public void BuildAutoSteerPgn_WhenNotEngaged_StatusIsZero()
    {
        var state = new VehicleState
        {
            IsAutoSteerEngaged = false,
            GpsValid = true,
            SteerSwitchActive = true,
            WorkSwitchActive = true
        };

        var packet = PgnBuilder.BuildAutoSteerPgn(ref state);

        Assert.That(packet[STATUS_BYTE_INDEX], Is.EqualTo(0),
            "Status must be exactly 0 when disengaged — firmware engages on ANY " +
            "nonzero byte, so GPS/switch flags must never leak into it.");
    }
}
