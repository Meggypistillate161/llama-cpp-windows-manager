using System.Collections.ObjectModel;

namespace LocalLlmConsole.ViewModels;

public sealed class JobsViewModel
{
    public ObservableCollection<UiRow> Rows { get; } = new();
    public ObservableCollection<UiRow> RuntimeRows { get; } = new();

    public void ReplaceJobs(IEnumerable<JobRecord> jobs)
    {
        Rows.Clear();
        RuntimeRows.Clear();
        foreach (var job in jobs)
        {
            var row = JobRow(job);
            Rows.Add(row);
            if (job.Kind.Contains("runtime", StringComparison.OrdinalIgnoreCase))
                RuntimeRows.Add(JobRow(job));
        }
    }

    public static UiRow JobRow(JobRecord job) => new()
    {
        C1 = job.Status.ToString(),
        C2 = job.Kind,
        C3 = job.Id,
        C4 = job.UpdatedAt.ToLocalTime().ToString("g"),
        C5 = LogFileService.RuntimeJobProgressSummary(job),
        C6 = "Log",
        C7 = "Cancel",
        C8 = "Retry",
        C9 = "Clear",
        T1 = File.Exists(job.LogPath) ? "Open this job's log file." : "This job does not have a log file yet.",
        T2 = RuntimeBuildJobService.CanCancel(job) ? "Cancel this running or queued runtime job." : "Only queued or running jobs can be cancelled.",
        T3 = RuntimeBuildJobService.CanRetry(job) ? "Retry this failed or interrupted runtime job." : "Only failed or interrupted runtime jobs can be retried.",
        T4 = RuntimeBuildJobService.CanClear(job) ? "Remove this finished runtime job from the list." : "Only finished runtime jobs can be cleared.",
        B1 = File.Exists(job.LogPath),
        B2 = RuntimeBuildJobService.CanCancel(job),
        B3 = RuntimeBuildJobService.CanRetry(job),
        B4 = RuntimeBuildJobService.CanClear(job),
        Data = JsonSerializer.SerializeToNode(job)!.AsObject()
    };
}
