// AgOpenWeb
// Copyright (C) 2024-2026 AgOpenWeb Contributors
//
// Licensed under GNU GPL v3. See LICENSE.md.
//
// Pins the outbound PGN frames that were found to diverge from the stock
// AgOpenGPS wire contract in the 2026-08 AIO 4.5 compatibility audit:
// 251 (short frame), 235 (absent), 229 (fake L/R speeds).

using AgOpenWeb.Models;
using AgOpenWeb.Models.Configuration;
using AgOpenWeb.Services.AutoSteer;

namespace AgOpenWeb.Services.Tests;

[TestFixture]
public class PgnWireFormatTests
{
    private static byte SumCrc(byte[] p) // stock: additive over bytes 2..n-2
    {
        byte crc = 0;
        for (int i = 2; i < p.Length - 1; i++) crc += p[i];
        return crc;
    }

    [Test]
    public void SteerConfig251_IsStockFourteenByteFrame()
    {
        var cfg = new AutoSteerConfig { MinSteerSpeed = 1.5, TurnSensorCounts = 99 };
        var p = (byte[])PgnBuilder.BuildSteerConfigPgn(cfg).Clone();

        Assert.That(p.Length, Is.EqualTo(14), "stock frame is 14 bytes");
        Assert.That(p[4], Is.EqualTo(8), "stock length byte is 8");
        Assert.That(p[6], Is.EqualTo(99), "maxPulse = TurnSensorCounts");
        Assert.That(p[7], Is.EqualTo(15), "minSpeed x10");
        Assert.That(p[10], Is.Zero); Assert.That(p[11], Is.Zero); Assert.That(p[12], Is.Zero);
        Assert.That(p[13], Is.EqualTo(SumCrc(p)), "CRC over bytes 2..12");
        Assert.That(PgnBuilder.ValidateChecksum(p), Is.True);
    }

    [Test]
    public void SectionDimensions235_CarriesWidthsAndCount()
    {
        var tool = new ToolConfig();
        tool.SetSectionWidth(0, 250);   // cm
        tool.SetSectionWidth(1, 300);
        tool.SetSectionWidth(2, 250);
        var p = PgnBuilder.BuildSectionDimensionsPgn(tool, numSections: 3);

        Assert.That(p.Length, Is.EqualTo(39));
        Assert.That(p[3], Is.EqualTo(0xEB));
        Assert.That(p[4], Is.EqualTo(33));
        Assert.That(p[5] | (p[6] << 8), Is.EqualTo(250), "section 1 width cm LE");
        Assert.That(p[7] | (p[8] << 8), Is.EqualTo(300), "section 2 width cm LE");
        Assert.That(p[9] | (p[10] << 8), Is.EqualTo(250), "section 3 width cm LE");
        Assert.That(p[11] | (p[12] << 8), Is.Zero, "unused sections zeroed");
        Assert.That(p[37], Is.EqualTo(3), "numSections");
        Assert.That(p[38], Is.EqualTo(SumCrc(p)));
        Assert.That(PgnBuilder.ValidateChecksum(p), Is.True);
    }

    [Test]
    public void CorrectedPosition100_CarriesLonLatDoubles()
    {
        // The layout RateController's PGN100 parser documents:
        // len 16, longitude double at 5-12, latitude double at 13-20, CRC 21.
        var p = (byte[])PgnBuilder.BuildCorrectedPositionPgn(51.1234567, -0.7654321).Clone();

        Assert.That(p.Length, Is.EqualTo(22));
        Assert.That(p[3], Is.EqualTo(0x64));
        Assert.That(p[4], Is.EqualTo(16));
        Assert.That(System.BitConverter.ToDouble(p, 5), Is.EqualTo(-0.7654321).Within(1e-9), "longitude");
        Assert.That(System.BitConverter.ToDouble(p, 13), Is.EqualTo(51.1234567).Within(1e-9), "latitude");
        Assert.That(p[21], Is.EqualTo(SumCrc(p)));
        Assert.That(PgnBuilder.ValidateChecksum(p), Is.True);
    }

    [Test]
    public void Sections229_LeftRightTipSpeeds_TurnCompensated()
    {
        // 10 km/h, turning right at 10°/s with a 15 m tool: the LEFT tip
        // travels faster. omega = 0.1745 rad/s x 7.5 m x 3.6 = 4.71 km/h.
        var state = new VehicleState { Speed = 10 / 3.6, YawRate = 10 }; // 10 km/h
        var p = (byte[])PgnBuilder.BuildSection64Pgn(ref state, toolHalfWidthM: 7.5).Clone();

        int left = p[13], right = p[14];
        Assert.That(left, Is.EqualTo(147).Within(2), "left tip ~14.7 km/h x10");
        Assert.That(right, Is.EqualTo(53).Within(2), "right tip ~5.3 km/h x10");

        // Straight-line: both tips at vehicle speed.
        state.YawRate = 0;
        p = (byte[])PgnBuilder.BuildSection64Pgn(ref state, toolHalfWidthM: 7.5).Clone();
        Assert.That(p[13], Is.EqualTo(100));
        Assert.That(p[14], Is.EqualTo(100));
    }
}
