using System.Windows;

namespace LocalLlmConsole;

public sealed record RuntimesPageRowActionControllerActions(
    Func<object, RuntimeRecord?> RuntimeFromRowButton,
    Func<object, RuntimeSourceEntry?> RuntimeSourceFromRowButton,
    Func<object, RuntimeBuildPreset?> RuntimeBuildPresetFromRowButton,
    Func<object, RuntimePackagePreset?> RuntimePackagePresetFromRowButton,
    Func<object, JobRecord?> JobFromRowButton,
    Func<RuntimeBuildPresetRow, Task> AddCustomRuntimeRepositoryFromRowAsync,
    Func<RuntimeBuildPreset, Task> DownloadRuntimeSourceAsync,
    Func<RuntimePackagePreset, Task> InstallRuntimePackageAsync,
    Func<RuntimePackagePreset, RuntimePackagePresetRow?, Task> CheckRuntimePackageUpdateAsync,
    Func<RuntimePackagePreset, Task> DeleteRuntimePackageBuildsAsync,
    Func<RuntimeBuildPreset, RuntimeBuildPresetRow?, Task> CheckRuntimePresetUpdateAsync,
    Func<RuntimeBuildPreset, Task> DeleteAllRuntimePresetBuildsAsync,
    Func<RuntimeSourceEntry, Task> BuildRuntimeSourceAsync,
    Func<RuntimeSourceEntry, Task> DeleteRuntimeSourceAsync,
    Func<RuntimeRecord, Task> DeleteRuntimeBuildAsync,
    Func<JobRecord, Task> CancelRuntimeBuildJobAsync,
    Func<JobRecord, Task> RetryRuntimeBuildJobAsync,
    Func<JobRecord, Task> ClearRuntimeBuildJobAsync,
    Action<string> OpenLogPath,
    Func<Func<Task>, Task> RunEventAsync);

public sealed class RuntimesPageRowActionController
{
    private readonly RuntimesPageRowActionControllerActions _actions;

    public RuntimesPageRowActionController(RuntimesPageRowActionControllerActions actions)
    {
        _actions = actions;
    }

    public async void DownloadRuntimePresetRow_Click(object sender, RoutedEventArgs e)
    {
        await _actions.RunEventAsync(async () =>
        {
            if ((sender as FrameworkElement)?.Tag is RuntimeBuildPresetRow { IsCustomAdd: true } row)
            {
                await _actions.AddCustomRuntimeRepositoryFromRowAsync(row);
                return;
            }

            var preset = _actions.RuntimeBuildPresetFromRowButton(sender);
            if (preset is not null) await _actions.DownloadRuntimeSourceAsync(preset);
        });
    }

    public async void InstallRuntimePackageRow_Click(object sender, RoutedEventArgs e)
        => await RunRuntimePackageActionAsync(sender, preset => _actions.InstallRuntimePackageAsync(preset));

    public async void CheckRuntimePackageUpdateRow_Click(object sender, RoutedEventArgs e)
    {
        await _actions.RunEventAsync(async () =>
        {
            var row = (sender as FrameworkElement)?.Tag as RuntimePackagePresetRow;
            var preset = _actions.RuntimePackagePresetFromRowButton(sender);
            if (preset is not null) await _actions.CheckRuntimePackageUpdateAsync(preset, row);
        });
    }

    public async void DeleteRuntimePackageRow_Click(object sender, RoutedEventArgs e)
        => await RunRuntimePackageActionAsync(sender, preset => _actions.DeleteRuntimePackageBuildsAsync(preset));

    public async void CheckRuntimePresetUpdateRow_Click(object sender, RoutedEventArgs e)
    {
        await _actions.RunEventAsync(async () =>
        {
            var row = (sender as FrameworkElement)?.Tag as RuntimeBuildPresetRow;
            var preset = _actions.RuntimeBuildPresetFromRowButton(sender);
            if (preset is not null) await _actions.CheckRuntimePresetUpdateAsync(preset, row);
        });
    }

    public async void DeleteRuntimePresetRow_Click(object sender, RoutedEventArgs e)
        => await RunRuntimeBuildPresetActionAsync(sender, preset => _actions.DeleteAllRuntimePresetBuildsAsync(preset));

    public async void BuildRuntimeRow_Click(object sender, RoutedEventArgs e)
    {
        await _actions.RunEventAsync(async () =>
        {
            var source = _actions.RuntimeSourceFromRowButton(sender);
            if (source is not null) await _actions.BuildRuntimeSourceAsync(source);
        });
    }

    public async void DeleteRuntimeRow_Click(object sender, RoutedEventArgs e)
    {
        await _actions.RunEventAsync(async () =>
        {
            var source = _actions.RuntimeSourceFromRowButton(sender);
            if (source is not null)
            {
                await _actions.DeleteRuntimeSourceAsync(source);
                return;
            }

            var runtime = _actions.RuntimeFromRowButton(sender);
            if (runtime is not null) await _actions.DeleteRuntimeBuildAsync(runtime);
        });
    }

    public void OpenRuntimeJobLogRow_Click(object sender, RoutedEventArgs e)
    {
        var job = _actions.JobFromRowButton(sender);
        if (job is not null) _actions.OpenLogPath(job.LogPath);
    }

    public async void CancelRuntimeJobRow_Click(object sender, RoutedEventArgs e)
        => await RunRuntimeJobActionAsync(sender, job => _actions.CancelRuntimeBuildJobAsync(job));

    public async void RetryRuntimeJobRow_Click(object sender, RoutedEventArgs e)
        => await RunRuntimeJobActionAsync(sender, job => _actions.RetryRuntimeBuildJobAsync(job));

    public async void ClearRuntimeJobRow_Click(object sender, RoutedEventArgs e)
        => await RunRuntimeJobActionAsync(sender, job => _actions.ClearRuntimeBuildJobAsync(job));

    private async Task RunRuntimeBuildPresetActionAsync(object sender, Func<RuntimeBuildPreset, Task> action)
    {
        await _actions.RunEventAsync(async () =>
        {
            var preset = _actions.RuntimeBuildPresetFromRowButton(sender);
            if (preset is not null) await action(preset);
        });
    }

    private async Task RunRuntimePackageActionAsync(object sender, Func<RuntimePackagePreset, Task> action)
    {
        await _actions.RunEventAsync(async () =>
        {
            var preset = _actions.RuntimePackagePresetFromRowButton(sender);
            if (preset is not null) await action(preset);
        });
    }

    private async Task RunRuntimeJobActionAsync(object sender, Func<JobRecord, Task> action)
    {
        await _actions.RunEventAsync(async () =>
        {
            var job = _actions.JobFromRowButton(sender);
            if (job is not null) await action(job);
        });
    }
}
