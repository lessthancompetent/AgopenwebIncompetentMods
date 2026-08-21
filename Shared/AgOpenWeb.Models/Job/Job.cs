// AgOpenWeb
// Copyright (C) 2024-2026 AgOpenWeb Contributors
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

namespace AgOpenWeb.Models.Job;

/// <summary>
/// One work session against a <see cref="Models.Field"/>.
/// Owns coverage paint, section-log events, and per-session notes.
/// Persistent geometry (boundary, headland, tracks, flags, elevation)
/// stays on the parent <see cref="Models.Field"/>.
/// </summary>
public class Job
{
    /// <summary>
    /// Stable identifier. Coverage and section-log files key off this on
    /// disk so renaming <see cref="TaskName"/> doesn't break references.
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Name of the parent field (folder name under FieldsRoot).
    /// </summary>
    public string FieldName { get; set; } = string.Empty;

    /// <summary>
    /// Operator-visible task label, default
    /// <c>YYYY-MM-DD_&lt;work_type&gt;[_&lt;vehicle_or_sim&gt;]</c>.
    /// Also used as the on-disk folder name under <c>jobs/</c>.
    /// User-editable.
    /// </summary>
    public string TaskName { get; set; } = string.Empty;

    /// <summary>
    /// Free-text work type (Decision #1). Suggested values come from
    /// <see cref="JobWorkTypeSuggestions"/> plus the distinct set of
    /// prior <c>WorkType</c> values across jobs.
    /// </summary>
    public string WorkType { get; set; } = string.Empty;

    /// <summary>
    /// Operator notes. Multi-line. The <c>[Use Last]</c> button on the
    /// New-Job form copies notes from the most recent job for this field.
    /// </summary>
    public string Notes { get; set; } = string.Empty;

    /// <summary>Product applied in this job (e.g. "Urea 46"). Drives the per-product
    /// coverage record exported as coverage.geojson.</summary>
    public string Product { get; set; } = string.Empty;

    /// <summary>Application rate (in <see cref="RateUnit"/> per hectare terms).</summary>
    public double Rate { get; set; }

    /// <summary>Rate unit label, e.g. "kg/ha" or "L/ha".</summary>
    public string RateUnit { get; set; } = string.Empty;

    /// <summary>Measured total product applied in this job (loader-scale weight
    /// into the spreader, sprayer tank totals, …). 0 = not measured; consumers
    /// fall back to <see cref="Rate"/> × worked area as an estimate.</summary>
    public double AppliedAmount { get; set; }

    /// <summary>Unit of <see cref="AppliedAmount"/>, e.g. "kg" or "L".</summary>
    public string AppliedUnit { get; set; } = string.Empty;

    /// <summary>
    /// Tank-mix calculator state for this job (client JSON blob: area mode,
    /// carrier L/ha, tank/buffer volumes, chemical rows). Opaque to the
    /// backend — persisted with the job so a refill mid-job reopens the
    /// same mix. Empty = no mix set.
    /// </summary>
    public string TankMixJson { get; set; } = string.Empty;

    public DateTime StartedAt { get; set; } = DateTime.Now;

    /// <summary>
    /// Set when the job is closed; cleared when resumed (Decision #3).
    /// </summary>
    public DateTime? EndedAt { get; set; }

    public DateTime LastOpenedAt { get; set; } = DateTime.Now;

    public JobStatus Status { get; set; } = JobStatus.InProgress;

    /// <summary>
    /// Distance the vehicle traveled while this job was active, in meters.
    /// Computed from the per-job section log on close.
    /// </summary>
    public double DistanceTraveledMeters { get; set; }

    /// <summary>
    /// Area painted by coverage during this job, in hectares.
    /// Computed from the per-job coverage on close.
    /// </summary>
    public double AreaWorkedHectares { get; set; }

    /// <summary>
    /// Number of U-turns executed during this job.
    /// </summary>
    public int UTurnCount { get; set; }
}
