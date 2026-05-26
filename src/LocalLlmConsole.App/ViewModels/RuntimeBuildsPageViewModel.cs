using System.Collections.ObjectModel;

namespace LocalLlmConsole.ViewModels;

public sealed class RuntimeBuildsPageViewModel
{
    public ObservableCollection<RuntimeBuildPresetRow> Rows { get; } = new();

    public void ReplacePresets(
        IReadOnlyList<RuntimeBuildPreset> presets,
        IReadOnlyList<RuntimeRecord> runtimes,
        IReadOnlyList<RuntimeSourceEntry> sources,
        IReadOnlyDictionary<string, RuntimeUpdateState> updateStates)
    {
        Rows.Clear();
        foreach (var preset in presets)
        {
            var downloaded = sources
                .Where(source => string.Equals(source.PresetId, preset.Id, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(source => source.DownloadedAt)
                .ToList();
            var installed = runtimes
                .Where(runtime => string.Equals(RuntimeMetadataService.ManagedPresetId(runtime), preset.Id, StringComparison.OrdinalIgnoreCase))
                .GroupBy(runtime => RuntimeMetadataService.Folder(runtime), StringComparer.OrdinalIgnoreCase)
                .Select(group => group.OrderByDescending(runtime => runtime.UpdatedAt).First())
                .OrderByDescending(runtime => runtime.UpdatedAt)
                .ToList();
            var localCount = downloaded.Count + installed.Count;
            var localCommit = RuntimeBuildCatalogService.LatestLocalCommitValue(downloaded, installed);
            var latestLocal = RuntimeBuildCatalogService.LatestLocalCommitLabel(downloaded, installed);
            var checkedState = CurrentRuntimeUpdateState(updateStates, preset.Id, localCommit);
            var commitUnavailable = localCount > 0 && string.IsNullOrWhiteSpace(localCommit);
            var canDownload = RuntimeBuildCatalogService.CanDownloadPreset(installed, downloaded, checkedState);
            if (checkedState is not null)
            {
                latestLocal = checkedState.HasUpdate
                    ? $"update available {RuntimeMetadataService.DisplayCommit(localCommit)} -> {RuntimeMetadataService.DisplayCommit(checkedState.RemoteCommit)}"
                    : $"current {RuntimeMetadataService.DisplayCommit(localCommit)} - checked {checkedState.CheckedAt.ToLocalTime():g}";
            }

            Rows.Add(new RuntimeBuildPresetRow
            {
                Label = preset.Label,
                Backend = RuntimeBuildCatalogService.BackendLabel(preset),
                LocalStatus = RuntimeBuildCatalogService.LocalStatusLabel(downloaded, installed, commitUnavailable),
                LatestLocal = latestLocal,
                Source = preset.RepoUrl,
                DownloadAction = RuntimeBuildCatalogService.DownloadButtonLabel(downloaded, installed, checkedState),
                CheckAction = "Check",
                DeleteAction = preset.Custom && localCount == 0 ? "Remove" : "Delete All",
                DownloadToolTip = canDownload
                    ? "Download or refresh this llama.cpp source preset."
                    : "This preset is already downloaded or installed.",
                CheckToolTip = localCount > 0
                    ? "Check the remote repository for newer commits."
                    : "Download or build this preset before checking for updates.",
                DeleteToolTip = preset.Custom && localCount == 0
                    ? "Remove this custom repository preset."
                    : "Delete local sources and built runtimes for this preset.",
                CanDownload = canDownload,
                CanCheck = localCount > 0,
                CanDelete = preset.Custom || localCount > 0,
                Preset = preset
            });
        }

        Rows.Add(new RuntimeBuildPresetRow
        {
            Backend = "CPU WSL",
            LocalStatus = "Custom",
            DownloadAction = "Add",
            DownloadToolTip = "Add a custom llama.cpp Git repository preset.",
            CanDownload = true,
            IsCustomAdd = true
        });
    }

    private static RuntimeUpdateState? CurrentRuntimeUpdateState(
        IReadOnlyDictionary<string, RuntimeUpdateState> updateStates,
        string presetId,
        string localCommit)
    {
        if (!updateStates.TryGetValue(presetId, out var state)) return null;
        return RuntimeMetadataService.CommitsMatch(state.LocalCommit, localCommit) ? state : null;
    }
}
