// AgOpenWeb
// Copyright (C) 2024-2026 AgOpenWeb Contributors
//
// Licensed under GNU GPL v3. See LICENSE.md.

using AgOpenWeb.Models;
using AgOpenWeb.Services.AutoSteer;

namespace AgOpenWeb.Services.Tests;

/// <summary>
/// Pins the PGN 254 status byte to the STOCK AgOpenGPS wire contract:
/// strictly 0 or 1. Real AIO firmware reads the whole byte as
/// guidanceStatus (any nonzero = engaged), so packing flag bits in here —
/// as an earlier version did with GPS-valid/work-switch bits — could
/// engage the wheel off a mere GPS fix. Free drive must steer, so it
/// sends 1; normal mode sends 1 only while autosteer is engaged.
/// </summary>
[TestFixture]
public class FreeDrivePgnEngagementTests
{
    private static byte[] BuildFreeDrivePgn(double angleDeg)
    {
        var state = new VehicleState
        {
            IsInFreeDriveMode = true,
            FreeDriveSteerAngle = angleDeg,
        };
        return (byte[])PgnBuilder.BuildAutoSteerPgn(ref state).Clone();
    }

    [Test]
    public void FreeDrivePgn254_StatusByte_IsExactlyOne()
    {
        var packet = BuildFreeDrivePgn(angleDeg: 5.0);
        Assert.That(packet[7], Is.EqualTo(1),
            "Free-drive PGN 254 status must be exactly 1 (stock contract) so the " +
            "firmware PID engages and drives toward the commanded angle.");
    }

    [Test]
    public void NormalPgn254_StatusByte_IsZeroOrOne_NeverFlagBits()
    {
        var disengaged = new VehicleState
        {
            GpsValid = true,          // must NOT leak into the status byte
            GuidanceValid = true,
            WorkSwitchActive = true,
            SteerSwitchActive = true,
            IsAutoSteerEngaged = false,
        };
        var packet = (byte[])PgnBuilder.BuildAutoSteerPgn(ref disengaged).Clone();
        Assert.That(packet[7], Is.EqualTo(0),
            "Disengaged status must be 0 even with GPS/switch flags set — real " +
            "firmware treats ANY nonzero byte as engaged.");

        var engaged = new VehicleState { IsAutoSteerEngaged = true, GuidanceValid = true };
        packet = (byte[])PgnBuilder.BuildAutoSteerPgn(ref engaged).Clone();
        Assert.That(packet[7], Is.EqualTo(1));
    }

    [Test]
    public void NormalPgn254_Xte_UsesStockOffset127HalfCmEncoding()
    {
        // Stock lightbar encoding: mm × 0.05 (2 cm units), clamp ±127, +127
        // offset; 255 = no guidance line.
        var state = new VehicleState
        {
            IsAutoSteerEngaged = true,
            GuidanceValid = true,
            CrossTrackError = 0.50, // 50 cm right of line -> 25 units -> 152
        };
        var packet = (byte[])PgnBuilder.BuildAutoSteerPgn(ref state).Clone();
        Assert.That(packet[10], Is.EqualTo(127 + 25));

        state.CrossTrackError = 0;
        packet = (byte[])PgnBuilder.BuildAutoSteerPgn(ref state).Clone();
        Assert.That(packet[10], Is.EqualTo(127), "on line = centred (127)");

        state.GuidanceValid = false;
        packet = (byte[])PgnBuilder.BuildAutoSteerPgn(ref state).Clone();
        Assert.That(packet[10], Is.EqualTo(255), "no guidance line = 255 sentinel");
    }

    [Test]
    public void FreeDrivePgn254_AngleEncoding_IsLittleEndianX100()
    {
        // Sanity check: the angle goes out in the same scale the
        // simulator's parser expects (signed int16, *100). 5.0° -> 500.
        var packet = BuildFreeDrivePgn(angleDeg: 5.0);
        short angleRaw = (short)(packet[8] | (packet[9] << 8));

        Assert.That(angleRaw, Is.EqualTo(500));
    }

    [Test]
    public void FreeDrivePgn254_SpeedField_IsEightKmhFakeValue()
    {
        // The firmware refuses to drive the motor below MinSpeed (default
        // 1 km/h) to keep stationary turning from burning out the motor.
        // Free-drive bypasses that by reporting a constant fake speed —
        // verify it stays where the receiver expects (~8 km/h x10 = 80).
        var packet = BuildFreeDrivePgn(angleDeg: 0);
        ushort speedRaw = (ushort)(packet[5] | (packet[6] << 8));

        Assert.That(speedRaw, Is.EqualTo(80));
    }
}
