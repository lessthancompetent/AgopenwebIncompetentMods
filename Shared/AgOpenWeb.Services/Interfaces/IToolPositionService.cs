// AgOpenWeb
// Copyright (C) 2024-2025 AgOpenWeb Contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with this program. If not, see <https://www.gnu.org/licenses/>.

using System;
using AgOpenWeb.Models.Base;

namespace AgOpenWeb.Services.Interfaces;

/// <summary>
/// Service for calculating tool/implement position relative to vehicle pivot point.
/// Handles fixed (front/rear), trailing, and TBT (Tow-Between-Tractor) configurations.
/// </summary>
public interface IToolPositionService
{
    /// <summary>
    /// Current tool center position in world coordinates
    /// </summary>
    Vec3 ToolPosition { get; }

    /// <summary>
    /// Tool pivot/hitch position in world coordinates
    /// </summary>
    Vec3 ToolPivotPosition { get; }

    /// <summary>
    /// Current tool heading in radians
    /// </summary>
    double ToolHeading { get; }

    /// <summary>
    /// Tank trailer position for TBT mode (zero if not TBT)
    /// </summary>
    Vec3 TankPosition { get; }

    /// <summary>
    /// Hitch point position on vehicle
    /// </summary>
    Vec3 HitchPosition { get; }

    /// <summary>
    /// True when tool heading is based on actual GPS movement, not startup default.
    /// </summary>
    bool IsToolPositionReady { get; }

    /// <summary>
    /// Update tool position based on vehicle position.
    /// Should be called every GPS update for smooth trailing behavior.
    /// </summary>
    /// <param name="vehiclePivot">Vehicle pivot point position</param>
    /// <param name="vehicleHeading">Vehicle heading in radians</param>
    void Update(Vec3 vehiclePivot, double vehicleHeading);

    /// <summary>
    /// Reset trailing state (e.g., when starting new field or after pause).
    /// Snaps tool directly behind vehicle to prevent jackknife on startup.
    /// </summary>
    /// <param name="vehiclePivot">Vehicle pivot point position</param>
    /// <param name="vehicleHeading">Vehicle heading in radians</param>
    void ResetTrailingState(Vec3 vehiclePivot, double vehicleHeading);
}
