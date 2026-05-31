namespace LocalLlmConsole.Services;

public sealed class RuntimeDeletionExecutorService
{
    private readonly StateStore _stateStore;

    public RuntimeDeletionExecutorService(StateStore stateStore)
    {
        _stateStore = stateStore ?? throw new ArgumentNullException(nameof(stateStore));
    }

    public async Task DeleteRuntimeSourceAsync(RuntimeSourceDeletionPlan plan, string runtimeRoot)
    {
        if (!plan.CanDelete) return;
        if (Directory.Exists(plan.SourceDir))
            RuntimeFileService.DeleteSafeRuntimeFolder(runtimeRoot, plan.SourceDir);
        await Task.CompletedTask;
    }

    public async Task DeleteRuntimeAsync(RuntimeDeletionPlan plan, string runtimeRoot)
    {
        if (!plan.CanDelete) return;

        await ReassignRuntimeProfilesAsync(plan.Reassignments);

        foreach (var runtime in plan.Runtimes)
            await _stateStore.DeleteRuntimeAsync(runtime.Id);

        if (plan.Kind != RuntimeDeletionPlanKind.DeleteFiles) return;
        foreach (var folder in plan.Folders)
        {
            if (Directory.Exists(folder) && RuntimeFileService.IsSafeRuntimeFolder(runtimeRoot, folder))
                RuntimeFileService.DeleteRuntimeFiles(runtimeRoot, folder);
        }
    }

    public async Task DeletePackageAsync(RuntimeDeletionPlan plan, string runtimeRoot)
    {
        if (!plan.CanDelete) return;

        foreach (var runtime in plan.Runtimes)
            await _stateStore.DeleteRuntimeAsync(runtime.Id);

        foreach (var folder in plan.Folders)
        {
            if (Directory.Exists(folder) && RuntimeFileService.IsSafeRuntimeFolder(runtimeRoot, folder))
                RuntimeFileService.DeleteSafeRuntimeFolder(runtimeRoot, folder);
        }
    }

    public async Task DeleteBuildPresetAsync(RuntimeBuildPresetDeletionPlan plan, string runtimeRoot)
    {
        if (!plan.CanDelete) return;

        foreach (var runtime in plan.Runtimes)
        {
            await _stateStore.DeleteRuntimeAsync(runtime.Id);
            if (RuntimeFileService.CanDeleteRuntimeFiles(runtime, runtimeRoot, out var folder, out _))
                RuntimeFileService.DeleteRuntimeFiles(runtimeRoot, folder);
        }

        foreach (var sourceDir in plan.SourceFolders)
        {
            if (Directory.Exists(sourceDir))
                RuntimeFileService.DeleteSafeRuntimeFolder(runtimeRoot, sourceDir);
        }

        if (plan.RemoveCustomRepository)
            await RemoveCustomRuntimeRepositoryAsync(plan.Preset, runtimeRoot);
    }

    private static async Task RemoveCustomRuntimeRepositoryAsync(RuntimeBuildPreset preset, string runtimeRoot)
    {
        var customPresets = RuntimeBuildCatalogService.ReadCustomPresets(runtimeRoot)
            .Where(candidate => !string.Equals(candidate.Id, preset.Id, StringComparison.OrdinalIgnoreCase))
            .ToList();
        await RuntimeBuildCatalogService.SaveCustomPresetsAsync(runtimeRoot, customPresets);
    }

    private async Task ReassignRuntimeProfilesAsync(IReadOnlyList<RuntimeProfileReassignment> reassignments)
    {
        foreach (var reassignment in reassignments)
        {
            var profile = await _stateStore.GetModelLaunchSettingsAsync(reassignment.ModelId);
            if (profile is null) continue;
            if (!string.Equals(profile.RuntimeId, reassignment.OldRuntimeId, StringComparison.OrdinalIgnoreCase)) continue;
            await _stateStore.SaveModelLaunchSettingsAsync(
                reassignment.ModelId,
                profile with { RuntimeId = reassignment.ReplacementRuntimeId });
        }
    }
}
