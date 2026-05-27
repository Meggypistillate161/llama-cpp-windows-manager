using System.Collections.ObjectModel;

namespace LocalLlmConsole.ViewModels;

public sealed class RuntimePackagesPageViewModel
{
    public ObservableCollection<RuntimePackagePresetRow> Rows { get; } = new();

    public void ReplacePresets(
        IReadOnlyList<RuntimePackagePreset> presets,
        IReadOnlyList<RuntimeRecord> runtimes,
        IReadOnlyDictionary<string, RuntimePackageUpdateState> updateStates)
    {
        Rows.Clear();
        foreach (var preset in presets)
        {
            var installed = RuntimePackageCatalogService.InstalledPackages(runtimes, preset);
            var sourceBuilds = RuntimePackageCatalogService.MatchingSourceBuilds(runtimes, preset);
            var localIdentity = RuntimePackageCatalogService.LocalIdentity(installed, sourceBuilds);
            var checkedState = CurrentRuntimePackageUpdateState(updateStates, preset.Id, localIdentity);
            var canInstall = RuntimePackageCatalogService.CanInstallPackage(installed, sourceBuilds, checkedState);

            Rows.Add(new RuntimePackagePresetRow
            {
                Label = preset.Label,
                Backend = RuntimePackageCatalogService.BackendLabel(preset),
                LocalStatus = RuntimePackageCatalogService.LocalStatusLabel(installed, sourceBuilds, checkedState),
                LatestRelease = RuntimePackageCatalogService.LatestLocalLabel(installed, sourceBuilds, checkedState),
                Assets = checkedState?.AssetSummary ?? RuntimePackageCatalogService.ReleasesUrl,
                InstallAction = RuntimePackageCatalogService.InstallButtonLabel(installed, sourceBuilds, checkedState),
                CheckAction = "Check",
                DeleteAction = "Delete All",
                InstallToolTip = canInstall
                    ? "Install or update this official prebuilt llama.cpp runtime."
                    : "This official prebuilt runtime is already installed.",
                CheckToolTip = "Check the official llama.cpp latest release for this runtime package.",
                DeleteToolTip = installed.Count + sourceBuilds.Count > 0
                    ? "Delete all local installs for this official prebuilt runtime package."
                    : "No local installs exist for this official prebuilt runtime package.",
                CanInstall = canInstall,
                CanCheck = true,
                CanDelete = installed.Count + sourceBuilds.Count > 0,
                Preset = preset
            });
        }
    }

    private static RuntimePackageUpdateState? CurrentRuntimePackageUpdateState(
        IReadOnlyDictionary<string, RuntimePackageUpdateState> updateStates,
        string presetId,
        string localIdentity)
    {
        if (!updateStates.TryGetValue(presetId, out var state)) return null;
        if (string.IsNullOrWhiteSpace(state.LocalIdentity))
        {
            var legacyIdentity = string.IsNullOrWhiteSpace(state.LocalTag) ? "" : $"package:{state.LocalTag}";
            return string.Equals(legacyIdentity, localIdentity ?? "", StringComparison.OrdinalIgnoreCase) ? state : null;
        }
        return string.Equals(state.LocalIdentity ?? "", localIdentity ?? "", StringComparison.OrdinalIgnoreCase) ? state : null;
    }
}
