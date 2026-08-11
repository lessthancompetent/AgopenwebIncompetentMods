// AgOpenWeb — per-job coverage export (application records).
//
// Writes coverage.geojson (WGS84 union of the actually-driven coverage, tagged
// with the job's product/rate) into the job folder: automatically on field
// close, retroactively on field open if a crash skipped the close (self-healing
// — the boundary-flag persistence bug taught us closes aren't guaranteed), and
// on demand from the Field Tools panel. The job folder syncs off-device
// (Syncthing → Pi) so these files ARE the remote application record; any
// database over them is a rebuildable index.
//
// Licensed under GNU GPL v3. See LICENSE.md.

using System;
using System.IO;
using AgOpenWeb.Services.Coverage;
using AgOpenWeb.Services.Fields;

namespace AgOpenWeb.ViewModels;

public partial class MainViewModel
{
    /// <summary>Set/update the active job's product + rate and persist to job.json.</summary>
    public void SetActiveJobProduct(string product, double rate, string rateUnit)
    {
        var job = _jobService.ActiveJob;
        if (job == null) { StatusMessage = "Open a job first"; return; }
        job.Product = (product ?? "").Trim();
        job.Rate = Math.Max(0, rate);
        job.RateUnit = (rateUnit ?? "").Trim();
        _jobService.SaveActiveJob();
        StatusMessage = $"Job product: {job.Product}" +
            (job.Rate > 0 ? $" @ {job.Rate:0.###} {job.RateUnit}" : "");
    }

    /// <summary>
    /// Export the active job's coverage as coverage.geojson in the job folder.
    /// Quiet mode (auto hooks) never surfaces errors as status noise.
    /// </summary>
    public void ExportCoverageForActiveJob(bool quiet = false)
    {
        try
        {
            var job = _jobService.ActiveJob;
            var field = ActiveField;
            if (job == null || field == null || string.IsNullOrWhiteSpace(field.DirectoryPath))
            { if (!quiet) StatusMessage = "Open a field and job first"; return; }

            var dims = _coverageMapService.DisplayDimensions;
            var bounds = _coverageMapService.GetCoverageBounds();
            if (dims == null || bounds == null)
            { if (!quiet) StatusMessage = "No coverage to export yet"; return; }

            var cells = _coverageMapService.GetPaintedDisplayCells();
            if (cells.Count == 0)
            { if (!quiet) StatusMessage = "No coverage to export yet"; return; }

            var json = CoverageExportService.BuildGeoJson(
                cells, dims.Value.CellSize, bounds.Value.MinE, bounds.Value.MinN,
                field.Origin.Latitude, field.Origin.Longitude,
                field.Name ?? job.FieldName, job.TaskName,
                job.Product, job.Rate, job.RateUnit, job.WorkType,
                job.StartedAt, job.EndedAt, _configStore.ActualToolWidth);
            if (json == null)
            { if (!quiet) StatusMessage = "No coverage to export yet"; return; }

            var jobDir = JobJsonService.JobDirectory(field.DirectoryPath, job.TaskName);
            Directory.CreateDirectory(jobDir);
            File.WriteAllText(Path.Combine(jobDir, "coverage.geojson"), json);
            if (!quiet)
                StatusMessage = $"Coverage exported ({(string.IsNullOrEmpty(job.Product) ? "no product set" : job.Product)})";
        }
        catch (Exception ex)
        {
            if (!quiet) StatusMessage = $"Coverage export failed: {ex.Message}";
            System.Diagnostics.Debug.WriteLine($"[CoverageExport] {ex}");
        }
    }

    /// <summary>
    /// Self-healing: after a field open has loaded the job's coverage, write the
    /// coverage.geojson if it's missing (the previous session crashed / was killed
    /// before the on-close export ran).
    /// </summary>
    private void TryRetroExportCoverage()
    {
        try
        {
            var job = _jobService.ActiveJob;
            var field = ActiveField;
            if (job == null || field == null || string.IsNullOrWhiteSpace(field.DirectoryPath)) return;
            var path = Path.Combine(JobJsonService.JobDirectory(field.DirectoryPath, job.TaskName), "coverage.geojson");
            if (!File.Exists(path) && _coverageMapService.PatchCount > 0)
                ExportCoverageForActiveJob(quiet: true);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[CoverageExport] retro: {ex}");
        }
    }
}
