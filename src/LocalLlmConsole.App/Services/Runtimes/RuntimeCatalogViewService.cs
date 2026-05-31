namespace LocalLlmConsole.Services;

public sealed record RuntimeCatalogViewRequest(
    IReadOnlyList<RuntimeRecord> Runtimes,
    IReadOnlyList<RuntimeSourceEntry> Sources,
    IReadOnlyList<RuntimeBuildPreset> BuildPresets,
    IReadOnlyList<RuntimePackagePreset> PackagePresets,
    IReadOnlyDictionary<string, List<string>> ModelsByRuntime,
    IReadOnlySet<string> ActiveRuntimeIds,
    IReadOnlyDictionary<string, RuntimeUpdateState> RuntimeUpdateStates,
    IReadOnlyDictionary<string, RuntimePackageUpdateState> RuntimePackageUpdateStates);

public sealed record RuntimeCatalogViewRows(
    IReadOnlyList<RuntimeCatalogRow> Runtimes,
    IReadOnlyList<RuntimeBuildPresetRow> BuildPresets,
    IReadOnlyList<RuntimePackagePresetRow> PackagePresets);

public sealed class RuntimeCatalogViewService
{
    private readonly RuntimePackageStatusService _packageStatus;

    public RuntimeCatalogViewService(RuntimePackageStatusService packageStatus)
    {
        _packageStatus = packageStatus ?? throw new ArgumentNullException(nameof(packageStatus));
    }

    public RuntimeCatalogViewRows BuildRows(RuntimeCatalogViewRequest request)
        => new(
            BuildRuntimeRows(request.Runtimes, request.Sources, request.ModelsByRuntime, request.ActiveRuntimeIds),
            BuildPresetRows(request.BuildPresets, request.Runtimes, request.Sources, request.RuntimeUpdateStates),
            BuildPackageRows(request.PackagePresets, request.Runtimes, request.RuntimePackageUpdateStates));

    public static IReadOnlyList<RuntimeCatalogRow> BuildRuntimeRows(
        IReadOnlyList<RuntimeRecord> runtimes,
        IReadOnlyList<RuntimeSourceEntry> sources,
        IReadOnlyDictionary<string, List<string>> modelsByRuntime,
        IReadOnlySet<string> activeRuntimeIds)
    {
        var rows = new List<RuntimeCatalogRow>();
        foreach (var runtime in runtimes)
        {
            modelsByRuntime.TryGetValue(runtime.Id, out var modelNames);
            modelNames ??= [];
            var isActiveRuntime = activeRuntimeIds.Contains(runtime.Id);
            rows.Add(new RuntimeCatalogRow
            {
                Kind = RuntimeCatalogRowKind.Runtime,
                Name = runtime.Name,
                Backend = runtime.Backend.ToString(),
                State = $"Built {runtime.Mode}",
                Location = runtime.ExecutablePath,
                Details = modelNames.Count == 0
                    ? "No saved model launch settings use this runtime."
                    : "Models using this runtime:" + Environment.NewLine + string.Join(Environment.NewLine, modelNames.Select(model => $"- {model}")),
                CanBuild = false,
                BuildToolTip = "This source has already been built.",
                CanDelete = !isActiveRuntime,
                DeleteToolTip = RuntimeDeleteToolTip(isActiveRuntime, modelNames),
                Runtime = runtime
            });
        }

        foreach (var source in sources.Where(source => !HasBuiltRuntimeForSource(source, runtimes)))
        {
            rows.Add(new RuntimeCatalogRow
            {
                Kind = RuntimeCatalogRowKind.Source,
                Name = source.Label,
                Backend = RuntimeBuildCatalogService.BackendLabel(source),
                State = "Downloaded",
                Location = source.SourceDir,
                Details = $"Downloaded source at {RuntimeMetadataService.ShortCommit(source.Commit)}. Build it before using it to launch models.",
                BuildAction = "Build",
                BuildToolTip = "Build this downloaded llama.cpp source into a usable runtime.",
                CanBuild = true,
                CanDelete = true,
                DeleteToolTip = "Delete this downloaded runtime source.",
                Source = source
            });
        }

        return rows;
    }

    public IReadOnlyList<RuntimePackagePresetRow> BuildPackageRows(
        IReadOnlyList<RuntimePackagePreset> presets,
        IReadOnlyList<RuntimeRecord> runtimes,
        IReadOnlyDictionary<string, RuntimePackageUpdateState> updateStates)
        => presets
            .Select(preset => _packageStatus.CreateRow(preset, _packageStatus.BuildInventory(preset, runtimes, updateStates)))
            .ToList();

    public static IReadOnlyList<RuntimeBuildPresetRow> BuildPresetRows(
        IReadOnlyList<RuntimeBuildPreset> presets,
        IReadOnlyList<RuntimeRecord> runtimes,
        IReadOnlyList<RuntimeSourceEntry> sources,
        IReadOnlyDictionary<string, RuntimeUpdateState> updateStates)
    {
        var rows = new List<RuntimeBuildPresetRow>();
        foreach (var preset in presets)
        {
            var local = RuntimeCatalogDataService.BuildPresetLocalState(preset, runtimes, sources, updateStates);
            var latestLocal = RuntimeBuildCatalogService.LatestLocalCommitLabel(local.DownloadedSources, local.InstalledRuntimes);
            if (local.UpdateState is not null)
            {
                latestLocal = local.UpdateState.HasUpdate
                    ? $"update available {RuntimeMetadataService.DisplayCommit(local.LocalCommit)} -> {RuntimeMetadataService.DisplayCommit(local.UpdateState.RemoteCommit)}"
                    : $"current {RuntimeMetadataService.DisplayCommit(local.LocalCommit)} - checked {local.UpdateState.CheckedAt.ToLocalTime():g}";
            }

            rows.Add(new RuntimeBuildPresetRow
            {
                Label = preset.Label,
                Backend = RuntimeBuildCatalogService.BackendLabel(preset),
                LocalStatus = RuntimeBuildCatalogService.LocalStatusLabel(local.DownloadedSources, local.InstalledRuntimes, local.CommitUnavailable),
                LatestLocal = latestLocal,
                Source = preset.RepoUrl,
                DownloadAction = local.DownloadAction,
                CheckAction = "Check",
                DeleteAction = preset.Custom && local.LocalCount == 0 ? "Remove" : "Delete All",
                DownloadToolTip = local.CanDownload
                    ? "Download or refresh this llama.cpp source preset."
                    : "This preset is already downloaded or installed.",
                CheckToolTip = local.LocalCount > 0
                    ? "Check the remote repository for newer commits."
                    : "Download or build this preset before checking for updates.",
                DeleteToolTip = preset.Custom && local.LocalCount == 0
                    ? "Remove this custom repository preset."
                    : "Delete local sources and built runtimes for this preset.",
                CanDownload = local.CanDownload,
                CanCheck = local.LocalCount > 0,
                CanDelete = preset.Custom || local.LocalCount > 0,
                Preset = preset
            });
        }

        rows.Add(new RuntimeBuildPresetRow
        {
            Backend = "CPU Windows",
            LocalStatus = "Custom",
            DownloadAction = "Add",
            DownloadToolTip = "Add a custom llama.cpp Git repository preset.",
            CanDownload = true,
            IsCustomAdd = true
        });

        return rows;
    }

    private static string RuntimeDeleteToolTip(bool isActiveRuntime, IReadOnlyList<string> modelNames)
    {
        var modelList = string.Join(", ", modelNames);
        if (isActiveRuntime && modelNames.Count > 0)
            return $"Unload the running model before deleting this runtime. Saved model profiles using it: {modelList}.";
        if (isActiveRuntime)
            return "Unload the running model before deleting this runtime.";
        if (modelNames.Count > 0)
            return $"Delete this runtime and move saved launch settings that use it to another registered runtime. Used by: {modelList}.";
        return "Delete this runtime registration and local build files.";
    }

    public static bool HasBuiltRuntimeForSource(RuntimeSourceEntry source, IReadOnlyList<RuntimeRecord> runtimes)
        => !string.IsNullOrWhiteSpace(source.Commit)
            && runtimes.Any(runtime => string.Equals(RuntimeMetadataService.ManagedPresetId(runtime), source.PresetId, StringComparison.OrdinalIgnoreCase)
                && RuntimeMetadataService.CommitsMatch(RuntimeMetadataService.Commit(runtime), source.Commit));

}
