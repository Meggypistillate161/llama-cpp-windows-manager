using System.Collections.ObjectModel;

namespace LocalLlmConsole.ViewModels;

public sealed class RuntimesPageViewModel
{
    public ObservableCollection<RuntimeCatalogRow> Rows { get; } = new();

    public void ReplaceRuntimes(
        IReadOnlyList<RuntimeRecord> runtimes,
        IReadOnlyList<RuntimeSourceEntry> sources,
        IReadOnlyDictionary<string, List<string>> modelsByRuntime,
        Func<RuntimeRecord, bool> isRuntimeActive)
    {
        Rows.Clear();
        foreach (var runtime in runtimes)
        {
            modelsByRuntime.TryGetValue(runtime.Id, out var modelNames);
            modelNames ??= [];
            var isActiveRuntime = isRuntimeActive(runtime);
            var deleteToolTip = RuntimeDeleteToolTip(isActiveRuntime, modelNames);
            Rows.Add(new RuntimeCatalogRow
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
                CanDelete = !isActiveRuntime && modelNames.Count == 0,
                DeleteToolTip = deleteToolTip,
                Runtime = runtime
            });
        }

        foreach (var source in sources.Where(source => !HasBuiltRuntimeForSource(source, runtimes)))
        {
            Rows.Add(new RuntimeCatalogRow
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
    }

    private static string RuntimeDeleteToolTip(bool isActiveRuntime, IReadOnlyList<string> modelNames)
    {
        var modelList = string.Join(", ", modelNames);
        if (isActiveRuntime && modelNames.Count > 0)
            return $"Unload the running model before deleting this runtime. Saved model profiles using it: {modelList}.";
        if (isActiveRuntime)
            return "Unload the running model before deleting this runtime.";
        if (modelNames.Count > 0)
            return $"Update saved launch settings before deleting this runtime. Used by: {modelList}.";
        return "Delete this runtime registration and local build files.";
    }

    public static bool HasBuiltRuntimeForSource(RuntimeSourceEntry source, IReadOnlyList<RuntimeRecord> runtimes)
        => !string.IsNullOrWhiteSpace(source.Commit)
            && runtimes.Any(runtime => string.Equals(RuntimeMetadataService.ManagedPresetId(runtime), source.PresetId, StringComparison.OrdinalIgnoreCase)
                && RuntimeMetadataService.CommitsMatch(RuntimeMetadataService.Commit(runtime), source.Commit));
}
