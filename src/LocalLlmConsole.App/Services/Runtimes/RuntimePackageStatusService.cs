namespace LocalLlmConsole.Services;

public sealed record RuntimePackageInventory(
    IReadOnlyList<RuntimeRecord> Installed,
    IReadOnlyList<RuntimeRecord> SourceBuilds,
    string LocalTag,
    string LocalAssets,
    string SourceCommit,
    string LocalIdentity,
    RuntimePackageUpdateState? CheckedState);

public sealed record RuntimePackageCheckResult(
    RuntimePackageUpdateState State,
    string Message,
    string LocalStatus,
    string LatestRelease,
    string Assets,
    string InstallAction,
    bool CanInstall);

public sealed class RuntimePackageStatusService
{
    public RuntimePackageInventory BuildInventory(
        RuntimePackagePreset preset,
        IReadOnlyList<RuntimeRecord> runtimes,
        IReadOnlyDictionary<string, RuntimePackageUpdateState> updateStates)
    {
        var installed = RuntimePackageInventoryPresenter.InstalledPackages(runtimes, preset);
        var sourceBuilds = RuntimePackageInventoryPresenter.MatchingSourceBuilds(runtimes, preset);
        var localIdentity = RuntimePackageInventoryPresenter.LocalIdentity(installed, sourceBuilds);
        return new RuntimePackageInventory(
            installed,
            sourceBuilds,
            RuntimePackageInventoryPresenter.LatestInstalledTag(installed),
            RuntimePackageInventoryPresenter.LatestInstalledAssetSummary(installed),
            RuntimePackageInventoryPresenter.LatestSourceCommit(sourceBuilds),
            localIdentity,
            CurrentUpdateState(updateStates, preset.Id, localIdentity));
    }

    public RuntimePackageCheckResult EvaluateAvailableRelease(
        RuntimePackageInventory inventory,
        RuntimePackageRelease release,
        RuntimePackageSelection selection,
        DateTimeOffset checkedAt)
    {
        var hasPackageTag = !string.IsNullOrWhiteSpace(inventory.LocalTag);
        var hasAssetChange = hasPackageTag
            && !string.IsNullOrWhiteSpace(inventory.LocalAssets)
            && !RuntimePackageAssetSelector.AssetSummariesMatch(inventory.LocalAssets, selection.AssetSummary);
        var hasUpdate = hasPackageTag
            ? !string.Equals(inventory.LocalTag, selection.ReleaseTag, StringComparison.OrdinalIgnoreCase) || hasAssetChange
            : inventory.SourceBuilds.Count > 0
                && !RuntimeMetadataService.CommitsMatch(inventory.SourceCommit, release.TargetCommit);
        var message = CheckMessage(inventory, release, selection, hasUpdate, hasAssetChange);
        var state = new RuntimePackageUpdateState(
            hasUpdate,
            inventory.LocalTag,
            selection.ReleaseTag,
            selection.ReleaseUrl,
            selection.AssetSummary,
            checkedAt,
            inventory.LocalIdentity,
            release.TargetCommit);
        return ResultFromState(inventory, state, message, hasUpdate);
    }

    public RuntimePackageCheckResult EvaluateUnavailableRelease(
        RuntimePackageInventory inventory,
        RuntimePackageRelease? release,
        string message,
        DateTimeOffset checkedAt)
    {
        var state = new RuntimePackageUpdateState(
            false,
            inventory.LocalTag,
            release?.TagName ?? "",
            release?.HtmlUrl ?? RuntimePackageSourceCatalog.ReleasesUrl,
            message,
            checkedAt,
            inventory.LocalIdentity,
            release?.TargetCommit ?? "",
            IsAvailable: false);
        return ResultFromState(inventory, state, message, hasUpdate: false);
    }

    public RuntimePackagePresetRow CreateRow(
        RuntimePackagePreset preset,
        RuntimePackageInventory inventory)
    {
        var canInstall = RuntimePackageInventoryPresenter.CanInstallPackage(inventory.Installed, inventory.SourceBuilds, inventory.CheckedState);
        var runtimeLabel = RuntimePackageSourceCatalog.PackageRuntimeLabel(preset);
        var sourceLabel = RuntimePackageSourceCatalog.PackageSourceLabel(preset);
        return new RuntimePackagePresetRow
        {
            Label = preset.Label,
            Backend = RuntimePackageSourceCatalog.BackendLabel(preset),
            LocalStatus = RuntimePackageInventoryPresenter.LocalStatusLabel(inventory.Installed, inventory.SourceBuilds, inventory.CheckedState),
            LatestRelease = RuntimePackageInventoryPresenter.LatestLocalLabel(inventory.Installed, inventory.SourceBuilds, inventory.CheckedState),
            Assets = inventory.CheckedState?.AssetSummary ?? RuntimePackageSourceCatalog.ReleasePageUrlFor(preset),
            InstallAction = RuntimePackageInventoryPresenter.InstallButtonLabel(inventory.Installed, inventory.SourceBuilds, inventory.CheckedState),
            CheckAction = "Check",
            DeleteAction = "Delete All",
            InstallToolTip = canInstall
                ? $"Install or update this {runtimeLabel}."
                : $"This {runtimeLabel} is already installed.",
            CheckToolTip = $"Check the {sourceLabel} latest release for this runtime package.",
            DeleteToolTip = inventory.Installed.Count + inventory.SourceBuilds.Count > 0
                ? $"Delete all local installs for this {runtimeLabel}."
                : $"No local installs exist for this {runtimeLabel}.",
            CanInstall = canInstall,
            CanCheck = true,
            CanDelete = inventory.Installed.Count + inventory.SourceBuilds.Count > 0,
            Preset = preset
        };
    }

    public static void ApplyCheckResult(RuntimePackagePresetRow row, RuntimePackageCheckResult result)
    {
        row.LocalStatus = result.LocalStatus;
        row.LatestRelease = result.LatestRelease;
        row.Assets = result.Assets;
        row.InstallAction = result.InstallAction;
        row.CanInstall = result.CanInstall;
        row.CheckAction = "Check";
        row.CanCheck = true;
    }

    private static RuntimePackageUpdateState? CurrentUpdateState(
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

    private static RuntimePackageCheckResult ResultFromState(
        RuntimePackageInventory inventory,
        RuntimePackageUpdateState state,
        string message,
        bool hasUpdate)
        => new(
            state,
            message,
            hasUpdate ? "Update available" : RuntimePackageInventoryPresenter.LocalStatusLabel(inventory.Installed, inventory.SourceBuilds, state),
            RuntimePackageInventoryPresenter.LatestLocalLabel(inventory.Installed, inventory.SourceBuilds, state),
            state.AssetSummary,
            RuntimePackageInventoryPresenter.InstallButtonLabel(inventory.Installed, inventory.SourceBuilds, state),
            RuntimePackageInventoryPresenter.CanInstallPackage(inventory.Installed, inventory.SourceBuilds, state));

    private static string CheckMessage(
        RuntimePackageInventory inventory,
        RuntimePackageRelease release,
        RuntimePackageSelection selection,
        bool hasUpdate,
        bool hasAssetChange)
        => inventory.Installed.Count == 0 && inventory.SourceBuilds.Count == 0
            ? $"Latest available release is {selection.ReleaseTag}. Use Install to download the {RuntimePackageSourceCatalog.PackageRuntimeLabel(selection.Preset)}."
            : inventory.Installed.Count == 0
                ? hasUpdate
                    ? $"Source build found at {RuntimeMetadataService.DisplayCommit(inventory.SourceCommit)}. Latest prebuilt release is {selection.ReleaseTag} ({RuntimeMetadataService.DisplayCommit(release.TargetCommit)})."
                    : $"Source build matches the latest release commit {RuntimeMetadataService.DisplayCommit(release.TargetCommit)}. Install the prebuilt package only if you want exact binary verification."
                : hasUpdate
                    ? hasAssetChange && string.Equals(inventory.LocalTag, selection.ReleaseTag, StringComparison.OrdinalIgnoreCase)
                        ? $"Package variant available for {selection.ReleaseTag}. Use Update to install {selection.AssetSummary}."
                        : $"Update available: {RuntimePackageInventoryPresenter.DisplayTag(inventory.LocalTag)} -> {selection.ReleaseTag}. Use Update to install the new prebuilt runtime."
                    : $"Already current at {selection.ReleaseTag}.";
}
