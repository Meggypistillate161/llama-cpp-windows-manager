using System.Windows;

namespace LocalLlmConsole;

public sealed record DownloadHistoryRowActionControllerActions(
    Func<object, JobRecord?> JobFromRowButton,
    Func<JobRecord?, Task> ResumeDownloadAsync,
    Func<JobRecord?, Task> PauseDownloadAsync,
    Func<JobRecord?, Task> StopDownloadAsync,
    Func<JobRecord?, Task> DeleteDownloadAsync,
    Func<Func<Task>, Task> RunEventAsync);

public sealed class DownloadHistoryRowActionController
{
    private readonly DownloadHistoryRowActionControllerActions _actions;

    public DownloadHistoryRowActionController(DownloadHistoryRowActionControllerActions actions)
    {
        _actions = actions;
    }

    public async void ResumeDownloadRow_Click(object sender, RoutedEventArgs e)
        => await RunJobActionAsync(sender, _actions.ResumeDownloadAsync);

    public async void PauseDownloadRow_Click(object sender, RoutedEventArgs e)
        => await RunJobActionAsync(sender, _actions.PauseDownloadAsync);

    public async void StopDownloadRow_Click(object sender, RoutedEventArgs e)
        => await RunJobActionAsync(sender, _actions.StopDownloadAsync);

    public async void DeleteDownloadRow_Click(object sender, RoutedEventArgs e)
        => await RunJobActionAsync(sender, _actions.DeleteDownloadAsync);

    private async Task RunJobActionAsync(object sender, Func<JobRecord?, Task> action)
    {
        await _actions.RunEventAsync(async () =>
        {
            var job = _actions.JobFromRowButton(sender);
            if (job is not null) await action(job);
        });
    }
}
