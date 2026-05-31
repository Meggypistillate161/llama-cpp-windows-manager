using System.Diagnostics;
using LocalLlmConsole.Models;
using LocalLlmConsole.Services;
using LocalLlmConsole.ViewModels;
using Microsoft.Data.Sqlite;

namespace LocalLlmConsole.Tests;


public sealed partial class ReleaseHardeningTests
{
    [Fact]
    public async Task TrackedProcessRunnerCapturesOutputErrorAndStandardInput()
    {
        var runner = new TrackedProcessRunner();
        var psi = new System.Diagnostics.ProcessStartInfo(HostExecutableResolver.WindowsPowerShellExe());
        psi.ArgumentList.Add("-NoProfile");
        psi.ArgumentList.Add("-Command");
        psi.ArgumentList.Add("$text = [Console]::In.ReadToEnd(); Write-Output $text.Trim(); [Console]::Error.WriteLine('runner-error')");

        var result = await runner.RunAsync(
            psi,
            TimeSpan.FromSeconds(10),
            TestContext.Current.CancellationToken,
            "runner-output");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("runner-output", result.Output, StringComparison.Ordinal);
        Assert.Contains("runner-error", result.Error, StringComparison.Ordinal);
    }


    [Fact]
    public async Task RuntimePortAllocatorSkipsReservedAndOccupiedPorts()
    {
        var allocator = new RuntimePortAllocator();
        var occupied = new HashSet<int> { 8082 };

        var port = await allocator.AllocateAsync(
            8081,
            [8081],
            candidate => Task.FromResult(occupied.Contains(candidate)));

        Assert.Equal(8083, port);
    }


    [Fact]
    public void ModelPortAllocatorUsesLowestFreePortAndReusesGaps()
    {
        Assert.Equal(8081, ModelPortAllocator.NextAvailable(8081, []));
        Assert.Equal(8082, ModelPortAllocator.NextAvailable(8081, [8081]));
        Assert.Equal(8082, ModelPortAllocator.NextAvailable(8081, [8081, 8083]));
        Assert.Equal(8081, ModelPortAllocator.NextAvailable(8081, [8082, 8083]));
    }


    [Fact]
    public void ModelRuntimeUsesFixedModelPortsForStableOpenCodeEndpoints()
    {
        var source = ReadMainWindowSources();
        var coordinator = File.ReadAllText(FindRepositoryFile("src", "LocalLlmConsole.App", "Services", "RuntimeSessionCoordinator.cs"));
        var preparation = File.ReadAllText(FindRepositoryFile("src", "LocalLlmConsole.App", "Services", "ModelRuntimeLaunchPreparationService.cs"));
        var loadApplication = File.ReadAllText(FindRepositoryFile("src", "LocalLlmConsole.App", "Services", "ModelRuntimeLoadApplicationService.cs"));
        var renderSelectedProfile = loadApplication.IndexOf("await actions.RenderLaunchSettingsAsync();", StringComparison.Ordinal);
        var resolveRuntime = loadApplication.IndexOf("_runtimeSelection.Resolve(runtimes, request.SelectedRuntimeId, request.FallbackRuntime)", StringComparison.Ordinal);

        Assert.Contains("Set a unique model port next to the runtime before launching.", coordinator, StringComparison.Ordinal);
        Assert.Contains("_sessions.ReservedPorts(sessionId).Contains(launchSettings.Port)", coordinator, StringComparison.Ordinal);
        Assert.Contains("_runtimeSessions.EnsureLaunchPortAvailable", preparation, StringComparison.Ordinal);
        Assert.Contains("_coreServices.Models.ModelRuntimeLaunchApplication.LaunchAsync", source, StringComparison.Ordinal);
        Assert.DoesNotContain("launchSettings = launchSettings with { Port = allocatedPort }", source, StringComparison.Ordinal);
        Assert.Contains("_coreServices.Models.ModelRuntimeLoadApplication.LoadSelectedAsync(", source, StringComparison.Ordinal);
        Assert.True(renderSelectedProfile >= 0);
        Assert.True(resolveRuntime > renderSelectedProfile);
    }


    [Fact]
    public void ModelLaunchProfilePersistenceIsExplicit()
    {
        var source = ReadMainWindowSources();
        var runtimeSelection = File.ReadAllText(FindRepositoryFile("src", "LocalLlmConsole.App", "MainWindow.LaunchSettingsRuntimeSelection.cs"));
        var runtimeSelectionService = File.ReadAllText(FindRepositoryFile("src", "LocalLlmConsole.App", "Services", "LaunchRuntimeSelectionService.cs"));
        var workflow = File.ReadAllText(FindRepositoryFile("src", "LocalLlmConsole.App", "Services", "ModelLaunchSettingsWorkflowService.cs"));
        var saveApplication = File.ReadAllText(FindRepositoryFile("src", "LocalLlmConsole.App", "Services", "ModelLaunchSettingsSaveApplicationService.cs"));
        var renderApplication = File.ReadAllText(FindRepositoryFile("src", "LocalLlmConsole.App", "Services", "LaunchSettingsRenderApplicationService.cs"));
        var headSelectionApplication = File.ReadAllText(FindRepositoryFile("src", "LocalLlmConsole.App", "Services", "ModelLaunchHeadSelectionApplicationService.cs"));
        var variantSaveApplication = File.ReadAllText(FindRepositoryFile("src", "LocalLlmConsole.App", "Services", "ModelLaunchVariantSaveApplicationService.cs"));
        var renderStart = source.IndexOf("private async Task RenderSelectedModelLaunchSettingsAsync", StringComparison.Ordinal);
        var saveDefaultsStart = source.IndexOf("private async Task SaveLaunchDefaultsFromControlsAsync", StringComparison.Ordinal);
        var saveForModelStart = source.IndexOf("private async Task SaveLaunchSettingsForSelectedModelAsync", StringComparison.Ordinal);
        var saveDefaultsEnd = source.IndexOf("private void ResetLaunchSettingsToDefaults", saveDefaultsStart, StringComparison.Ordinal);
        var loadStart = source.IndexOf("private async Task LoadSelectedModelAsync", StringComparison.Ordinal);
        var loadEnd = source.IndexOf("private async Task UnloadSelectedModelAsync", loadStart, StringComparison.Ordinal);

        Assert.True(renderStart >= 0);
        Assert.True(saveDefaultsStart > renderStart);
        Assert.Contains("var launchSettings = ModelServices.ModelLaunchSettingsWorkflow;", source, StringComparison.Ordinal);
        Assert.Contains("_coreServices.Models.LaunchSettingsRenderApplication.RenderSelectedAsync(", source, StringComparison.Ordinal);
        Assert.Contains("new LaunchSettingsRenderActions(", source, StringComparison.Ordinal);
        Assert.Contains("return await launchSettings!.BuildAsync(selectedModel, defaults, token);", source, StringComparison.Ordinal);
        Assert.Contains("public sealed class LaunchSettingsRenderApplicationService", renderApplication, StringComparison.Ordinal);
        Assert.Contains("if (!string.Equals(actions.SelectedModel()?.Id, selectedId", renderApplication, StringComparison.Ordinal);
        Assert.Contains("_coreServices.Models.ModelLaunchHeadSelectionApplication.ChooseVisionProjector(", source, StringComparison.Ordinal);
        Assert.Contains("_coreServices.Models.ModelLaunchHeadSelectionApplication.ChooseMtpHead(", source, StringComparison.Ordinal);
        Assert.Contains("public sealed class ModelLaunchHeadSelectionApplicationService", headSelectionApplication, StringComparison.Ordinal);
        Assert.Contains("BuildPickerRequest(request, title)", headSelectionApplication, StringComparison.Ordinal);
        Assert.Contains("VisionProjectorSelection.IsEmbeddedOrMainModel", headSelectionApplication, StringComparison.Ordinal);
        Assert.DoesNotContain("VisionProjectorSelection.IsEmbeddedOrMainModel", source, StringComparison.Ordinal);
        Assert.Contains("_coreServices.Models.ModelLaunchVariantSaveApplication.SaveSelectedAsNewAsync(", source, StringComparison.Ordinal);
        Assert.Contains("ModelLaunchVariantSaveActions()", source, StringComparison.Ordinal);
        Assert.Contains("public sealed class ModelLaunchVariantSaveApplicationService", variantSaveApplication, StringComparison.Ordinal);
        Assert.Contains("request.Settings.AutoSaveOpenCodeOnLaunchSettingsSave", variantSaveApplication, StringComparison.Ordinal);
        Assert.Contains("await actions.SyncOpenCodeLocalProviderAsync(request.Settings)", variantSaveApplication, StringComparison.Ordinal);
        Assert.Contains("var profile = await _profiles.ReadAsync(model);", workflow, StringComparison.Ordinal);
        Assert.Contains("var effective = profile ?? await _profiles.DraftAsync(model, defaults);", workflow, StringComparison.Ordinal);
        Assert.DoesNotContain("EnsureModelLaunchProfileAsync(model)", source[renderStart..saveDefaultsStart], StringComparison.Ordinal);

        Assert.Contains("ModelLaunchSettingsWorkflowService.SaveLaunchDefaults(_settings, launchDefaults)", source, StringComparison.Ordinal);
        Assert.Contains("_coreServices.Models.ModelLaunchSettingsSaveApplication.SaveDefaultsFromControlsAsync(", source, StringComparison.Ordinal);
        Assert.Contains("public sealed class ModelLaunchSettingsSaveApplicationService", saveApplication, StringComparison.Ordinal);
        Assert.Contains("actions.SetSettings(result.Settings)", saveApplication, StringComparison.Ordinal);
        Assert.Contains("actions.MarkSaved(request.ModelId, request.Result.SavedSettings)", saveApplication, StringComparison.Ordinal);
        Assert.Contains("launchDefaults with { Port = currentSettings.Port }", workflow, StringComparison.Ordinal);
        Assert.Contains("Launch defaults saved. Model ports stay per-model.", workflow, StringComparison.Ordinal);
        Assert.DoesNotContain("SetStatus(\"Launch defaults saved. Model ports stay per-model.\")", source, StringComparison.Ordinal);
        Assert.DoesNotContain("_settings = ReadLaunchSettingsFromControls();", source, StringComparison.Ordinal);
        Assert.True(saveDefaultsEnd > saveDefaultsStart);
        Assert.DoesNotContain("_settings = result.Settings;", source[saveDefaultsStart..saveDefaultsEnd], StringComparison.Ordinal);

        Assert.True(loadStart >= 0);
        Assert.True(loadEnd > loadStart);
        Assert.DoesNotContain("SaveModelLaunchProfileAsync(model, launchSettings)", source[loadStart..loadEnd], StringComparison.Ordinal);
        Assert.Contains("_coreServices.Models.ModelLaunchSettingsSaveApplication.SaveSelectedProfileAsync(", source[saveForModelStart..], StringComparison.Ordinal);
        Assert.Contains("SaveModelLaunchProfileAsync", source[saveForModelStart..], StringComparison.Ordinal);
        Assert.Contains("SaveProfileAsync(", workflow, StringComparison.Ordinal);
        Assert.DoesNotContain("SetStatus($\"Launch profile saved for {model.Name}.\")", source, StringComparison.Ordinal);
        Assert.Contains("SaveForModelAsync(", workflow, StringComparison.Ordinal);
        Assert.DoesNotContain("SelectModelAfterRefresh(result.Alias.Id)", source, StringComparison.Ordinal);

        Assert.Contains("_coreServices.Models.LaunchRuntimeSelection.BuildSelectorState(runtimes, selectedRuntimeId)", runtimeSelection, StringComparison.Ordinal);
        Assert.Contains("Missing runtime ({state.MissingRuntimeId})", File.ReadAllText(FindRepositoryFile("src", "LocalLlmConsole.App", "ViewModels", "LaunchSettingsViewModel.cs")), StringComparison.Ordinal);
        Assert.DoesNotContain("Missing runtime ({selectedRuntimeId})", runtimeSelection, StringComparison.Ordinal);
        Assert.Contains("public LaunchRuntimeSelectorState BuildSelectorState(", runtimeSelectionService, StringComparison.Ordinal);
        Assert.Contains("_runtimeSelection.Resolve(runtimes, request.SelectedRuntimeId, request.FallbackRuntime)", File.ReadAllText(FindRepositoryFile("src", "LocalLlmConsole.App", "Services", "ModelRuntimeLoadApplicationService.cs")), StringComparison.Ordinal);
        Assert.Contains("_runtimeSelection.MissingRuntimeStatus(runtimes, request.SelectedRuntimeId)", File.ReadAllText(FindRepositoryFile("src", "LocalLlmConsole.App", "Services", "ModelRuntimeLoadApplicationService.cs")), StringComparison.Ordinal);
        Assert.DoesNotContain("Saved runtime '{runtimeId}' is missing.", runtimeSelection, StringComparison.Ordinal);
    }


    [Fact]
    public void LaunchRuntimeSelectionServiceOwnsResolutionAndMissingStatus()
    {
        var root = CreateTempRoot();
        var service = new LaunchRuntimeSelectionService();
        var cpu = new RuntimeRecord("runtime-cpu", "CPU", RuntimeMode.Native, RuntimeBackend.Cpu, Path.Combine(root, "cpu.exe"), "{}", DateTimeOffset.UtcNow);
        var cuda = new RuntimeRecord("runtime-cuda", "CUDA", RuntimeMode.Native, RuntimeBackend.Cuda, Path.Combine(root, "cuda.exe"), "{}", DateTimeOffset.UtcNow);

        Assert.Same(cuda, service.Resolve([cpu, cuda], "RUNTIME-CUDA"));
        Assert.Null(service.Resolve([cpu, cuda], "missing"));
        Assert.Same(cuda, service.Resolve([cpu], "", cuda));
        Assert.Same(cpu, service.Resolve([cpu, cuda], ""));
        Assert.Null(service.Resolve([], ""));
        var selectedState = service.BuildSelectorState([cpu, cuda], "RUNTIME-CUDA");
        Assert.Equal("runtime-cuda", selectedState.SelectedRuntimeId);
        Assert.Null(selectedState.MissingRuntimeId);
        Assert.Equal([cpu, cuda], selectedState.Runtimes);
        var defaultState = service.BuildSelectorState([cpu, cuda], "");
        Assert.Equal("runtime-cpu", defaultState.SelectedRuntimeId);
        Assert.Null(defaultState.MissingRuntimeId);
        var missingState = service.BuildSelectorState([cpu], "missing");
        Assert.Equal("missing", missingState.SelectedRuntimeId);
        Assert.Equal("missing", missingState.MissingRuntimeId);
        Assert.Equal("Register a llama.cpp runtime first.", service.MissingRuntimeStatus([], ""));
        Assert.Equal(
            "Saved runtime 'missing' is missing. Choose another runtime and save the model profile.",
            service.MissingRuntimeStatus([cpu], "missing"));
        Assert.Equal("Choose a llama.cpp runtime before loading the model.", service.MissingRuntimeStatus([cpu], ""));
    }


    [Fact]
    public void ModelLaunchHeadSelectionApplicationServiceOwnsPickerRequestsAndNormalization()
    {
        var root = CreateTempRoot();
        var modelsRoot = Path.Combine(root, "models");
        var modelFolder = Path.Combine(modelsRoot, "qwen");
        Directory.CreateDirectory(modelFolder);
        var modelPath = Path.Combine(modelFolder, "qwen.gguf");
        var projectorPath = Path.Combine(modelFolder, "mmproj.gguf");
        var mtpHeadPath = Path.Combine(modelFolder, "mtp-head.gguf");
        var model = new ModelRecord("model-1", "Qwen", modelPath, OwnershipKind.External, "{}", DateTimeOffset.UtcNow);
        var service = new ModelLaunchHeadSelectionApplicationService();
        var requests = new List<OpenFilePickerRequest>();
        var applied = new List<string>();

        LaunchHeadSelectionActions Actions(string? selected)
            => new(
                request =>
                {
                    requests.Add(request);
                    return selected;
                },
                applied.Add);

        var embedded = service.ChooseVisionProjector(
            new LaunchHeadSelectionRequest(model, modelsRoot),
            Actions(modelPath));

        Assert.Equal(LaunchHeadSelectionOutcome.Applied, embedded);
        Assert.Equal(VisionProjectorSelection.EmbeddedToken, applied.Single());
        var visionRequest = requests.Single();
        Assert.Equal("Choose vision head/projector GGUF", visionRequest.Title);
        Assert.Equal("GGUF files|*.gguf|All files|*.*", visionRequest.Filter);
        Assert.True(visionRequest.CheckFileExists);
        Assert.False(visionRequest.AddExtension);
        Assert.Equal(".gguf", visionRequest.DefaultExt);
        Assert.Equal("", visionRequest.FileName);
        Assert.Equal(modelFolder, visionRequest.InitialDirectory);

        requests.Clear();
        applied.Clear();
        var external = service.ChooseVisionProjector(
            new LaunchHeadSelectionRequest(model, modelsRoot),
            Actions(projectorPath));

        Assert.Equal(LaunchHeadSelectionOutcome.Applied, external);
        Assert.Equal(projectorPath, applied.Single());

        requests.Clear();
        applied.Clear();
        var mtp = service.ChooseMtpHead(
            new LaunchHeadSelectionRequest(model, modelsRoot),
            Actions(mtpHeadPath));

        Assert.Equal(LaunchHeadSelectionOutcome.Applied, mtp);
        Assert.Equal(mtpHeadPath, applied.Single());
        Assert.Equal("Choose MTP head GGUF", requests.Single().Title);

        requests.Clear();
        applied.Clear();
        var cancelled = service.ChooseMtpHead(
            new LaunchHeadSelectionRequest(null, modelsRoot),
            Actions(""));

        Assert.Equal(LaunchHeadSelectionOutcome.Cancelled, cancelled);
        Assert.Empty(applied);
        Assert.Equal(modelsRoot, requests.Single().InitialDirectory);
    }


    [Fact]
    public async Task ModelLaunchSettingsSaveApplicationServiceAppliesProfileAndDefaultSavesInOrder()
    {
        var root = CreateTempRoot();
        var settings = AppSettings.CreateDefault(root) with { ContextSize = 8192 };
        var saved = ModelLaunchSettings.FromAppSettings(settings, "runtime-cuda");
        var model = new ModelRecord("model-1", "Qwen", Path.Combine(root, "qwen.gguf"), OwnershipKind.External, "{}", DateTimeOffset.UtcNow);
        var service = new ModelLaunchSettingsSaveApplicationService();
        var calls = new List<string>();

        var noModel = await service.SaveSelectedProfileAsync(
            null,
            SaveSelectedProfileActions(editorLoaded: true));
        Assert.Equal(ModelLaunchProfileSaveApplicationOutcome.NoModelSelected, noModel);
        Assert.Equal(["status:Select a model before saving launch settings."], calls);

        calls.Clear();
        var selected = await service.SaveSelectedProfileAsync(
            model,
            SaveSelectedProfileActions(editorLoaded: false));
        Assert.Equal(ModelLaunchProfileSaveApplicationOutcome.Saved, selected);
        Assert.Equal(
            [
                "busy:Saving model launch profile...",
                "loaded:model-1:False",
                "render",
                "read",
                "save:model-1:8192",
                "mark:model-1:runtime-cuda",
                "save-state",
                "status:Profile saved.",
                "sync:8192"
            ],
            calls);

        calls.Clear();
        service.ApplyProfileSave(
            new ModelLaunchProfileSaveApplicationRequest(
                "model-1",
                new ModelLaunchSettingsSaveResult(saved, "Profile saved.")),
            new ModelLaunchProfileSaveActions(
                (modelId, profile) => calls.Add($"mark:{modelId}:{profile.RuntimeId}"),
                () => calls.Add("save-state"),
                status => calls.Add($"status:{status}")));

        Assert.Equal(["mark:model-1:runtime-cuda", "save-state", "status:Profile saved."], calls);

        calls.Clear();
        var selectedWithoutSync = await service.SaveSelectedProfileAsync(
            model,
            SaveSelectedProfileActions(editorLoaded: true, currentSettings: settings with { AutoSaveOpenCodeOnLaunchSettingsSave = false }));
        Assert.Equal(ModelLaunchProfileSaveApplicationOutcome.Saved, selectedWithoutSync);
        Assert.Equal(
            [
                "busy:Saving model launch profile...",
                "loaded:model-1:True",
                "read",
                "save:model-1:8192",
                "mark:model-1:runtime-cuda",
                "save-state",
                "status:Profile saved."
            ],
            calls);

        calls.Clear();
        var defaultsSaved = await service.SaveDefaultsFromControlsAsync(
            new LaunchDefaultsSaveFromControlsActions(
                async (message, action) =>
                {
                    calls.Add($"busy:{message}");
                    await action();
                },
                () =>
                {
                    calls.Add("read");
                    return settings;
                },
                launchDefaults =>
                {
                    calls.Add($"defaults:{launchDefaults.ContextSize}");
                    return new LaunchDefaultsSaveResult(settings, "Defaults saved.");
                },
                DefaultActions()));
        Assert.Equal(LaunchDefaultsSaveApplicationOutcome.Saved, defaultsSaved);
        Assert.Equal(["busy:Saving launch defaults...", "read", "defaults:8192", "settings:8192", "persist", "save-state", "status:Defaults saved."], calls);

        calls.Clear();
        await service.ApplyDefaultsSaveAsync(
            new LaunchDefaultsSaveResult(settings, "Defaults saved."),
            DefaultActions());

        Assert.Equal(["settings:8192", "persist", "save-state", "status:Defaults saved."], calls);

        ModelLaunchProfileSaveSelectedActions SaveSelectedProfileActions(bool editorLoaded, AppSettings? currentSettings = null)
            => new(
                async (message, action) =>
                {
                    calls.Add($"busy:{message}");
                    await action();
                },
                modelId =>
                {
                    calls.Add($"loaded:{modelId}:{editorLoaded}");
                    return editorLoaded;
                },
                () =>
                {
                    calls.Add("render");
                    return Task.CompletedTask;
                },
                () =>
                {
                    calls.Add("read");
                    return settings;
                },
                () => currentSettings ?? settings,
                (profileModel, launchSettings) =>
                {
                    calls.Add($"save:{profileModel.Id}:{launchSettings.ContextSize}");
                    return Task.FromResult(new ModelLaunchSettingsSaveResult(saved, "Profile saved."));
                },
                currentSettings =>
                {
                    calls.Add($"sync:{currentSettings.ContextSize}");
                    return Task.CompletedTask;
                },
                new ModelLaunchProfileSaveActions(
                    (modelId, profile) => calls.Add($"mark:{modelId}:{profile.RuntimeId}"),
                    () => calls.Add("save-state"),
                    status => calls.Add($"status:{status}")));

        LaunchDefaultsSaveActions DefaultActions()
            => new(
                next => calls.Add($"settings:{next.ContextSize}"),
                () =>
                {
                    calls.Add("persist");
                    return Task.CompletedTask;
                },
                () => calls.Add("save-state"),
                status => calls.Add($"status:{status}"));
    }


    [Fact]
    public async Task LaunchSettingsRenderApplicationServiceAppliesRenderFlowAndSkipsStaleSelection()
    {
        var root = CreateTempRoot();
        var settings = AppSettings.CreateDefault(root) with { Port = 8081 };
        var model = new ModelRecord("model-1", "Qwen", Path.Combine(root, "qwen.gguf"), OwnershipKind.External, "{}", DateTimeOffset.UtcNow);
        var other = model with { Id = "model-2", Name = "Llama" };
        var saved = ModelLaunchSettings.FromAppSettings(settings with { Port = 8095 }, "runtime-1");
        var viewState = new ModelLaunchSettingsViewState(
            model.Id,
            SavedProfile: saved,
            HasSavedProfile: true,
            RuntimeId: "runtime-1",
            LaunchSettings: saved.ApplyTo(settings));
        var service = new LaunchSettingsRenderApplicationService();
        var selected = model;
        var calls = new List<string>();

        LaunchSettingsRenderActions Actions(Func<ModelRecord, AppSettings, CancellationToken, Task<ModelLaunchSettingsViewState>> build)
            => new(
                () => selected,
                () => calls.Add("clear"),
                source => calls.Add($"name:{source?.Name ?? ""}"),
                build,
                state => calls.Add($"load:{state.ModelId}"),
                runtimeId => { calls.Add($"runtime:{runtimeId}"); return Task.CompletedTask; },
                launchSettings => calls.Add($"apply:{launchSettings.Port}"),
                (capabilityModel, _) =>
                {
                    calls.Add($"cap:{capabilityModel?.Id ?? ""}");
                    return Task.CompletedTask;
                },
                () => calls.Add("save"));

        await service.RenderSelectedAsync(
            null,
            settings,
            Actions((_, _, _) => throw new InvalidOperationException("No model render should not build a profile.")),
            TestContext.Current.CancellationToken);

        Assert.Equal(["clear", "name:", "runtime:", "apply:8081", "cap:", "save"], calls);

        calls.Clear();
        selected = model;
        await service.RenderSelectedAsync(
            model,
            settings,
            Actions((selectedModel, _, _) =>
            {
                calls.Add($"build:{selectedModel.Id}");
                return Task.FromResult(viewState);
            }),
            TestContext.Current.CancellationToken);

        Assert.Equal(["name:Qwen", "build:model-1", "load:model-1", "runtime:runtime-1", "apply:8095", "cap:model-1", "save"], calls);

        calls.Clear();
        selected = model;
        await service.RenderSelectedAsync(
            model,
            settings,
            Actions((selectedModel, _, _) =>
            {
                calls.Add($"build:{selectedModel.Id}");
                selected = other;
                return Task.FromResult(viewState);
            }),
            TestContext.Current.CancellationToken);

        Assert.Equal(["name:Qwen", "build:model-1"], calls);
    }


    [Fact]
    public async Task ModelLaunchVariantSaveApplicationServiceAppliesFailureAndSuccessFollowupsInOrder()
    {
        var root = CreateTempRoot();
        var settings = AppSettings.CreateDefault(root);
        var alias = new ModelRecord("alias-1", "Qwen 32K", Path.Combine(root, "qwen.gguf"), OwnershipKind.External, "{}", DateTimeOffset.UtcNow);
        var source = new ModelRecord("source-1", "Qwen", Path.Combine(root, "qwen.gguf"), OwnershipKind.External, "{}", DateTimeOffset.UtcNow);
        var service = new ModelLaunchVariantSaveApplicationService();
        var calls = new List<string>();

        ModelLaunchVariantSaveActions Actions()
            => new(
                () => { calls.Add("refresh-models"); return Task.CompletedTask; },
                id => calls.Add($"select:{id}"),
                () => { calls.Add("render"); return Task.CompletedTask; },
                () => { calls.Add("overview"); return Task.CompletedTask; },
                syncedSettings =>
                {
                    calls.Add($"opencode:{syncedSettings.WorkspaceRoot}");
                    return Task.CompletedTask;
                },
                status => calls.Add($"status:{status}"));

        var failed = await service.ApplyAsync(
            new ModelLaunchVariantSaveApplicationRequest(new ModelLaunchVariantWorkflowResult(false, "Change the name."), settings),
            Actions());

        Assert.False(failed);
        Assert.Equal(["status:Change the name."], calls);

        calls.Clear();
        var succeeded = await service.ApplyAsync(
            new ModelLaunchVariantSaveApplicationRequest(new ModelLaunchVariantWorkflowResult(true, "Saved.", alias), settings),
            Actions());

        Assert.True(succeeded);
        Assert.Equal(["refresh-models", "select:alias-1", "render", "overview", $"opencode:{settings.WorkspaceRoot}", "status:Saved."], calls);

        calls.Clear();
        var succeededWithoutSync = await service.ApplyAsync(
            new ModelLaunchVariantSaveApplicationRequest(
                new ModelLaunchVariantWorkflowResult(true, "Saved.", alias),
                settings with { AutoSaveOpenCodeOnLaunchSettingsSave = false }),
            Actions());

        Assert.True(succeededWithoutSync);
        Assert.Equal(["refresh-models", "select:alias-1", "render", "overview", "status:Saved."], calls);

        calls.Clear();
        var noModel = await service.SaveSelectedAsNewAsync(
            null,
            "Qwen 32K",
            settings,
            SaveSelectedVariantActions(editorLoaded: true, new ModelLaunchVariantWorkflowResult(true, "Saved.", alias)));
        Assert.Equal(ModelLaunchVariantSaveApplicationOutcome.NoModelSelected, noModel);
        Assert.Equal(["status:Select a model before saving a new model variant."], calls);

        calls.Clear();
        var selected = await service.SaveSelectedAsNewAsync(
            source,
            "Qwen 32K",
            settings,
            SaveSelectedVariantActions(editorLoaded: false, new ModelLaunchVariantWorkflowResult(true, "Saved.", alias)));
        Assert.Equal(ModelLaunchVariantSaveApplicationOutcome.Saved, selected);
        Assert.Equal(
            [
                "busy:Saving model variant...",
                "loaded:source-1:False",
                "render",
                "read",
                "runtime",
                "save-as:source-1:Qwen 32K:8081",
                "refresh-models",
                "select:alias-1",
                "render",
                "overview",
                $"opencode:{settings.WorkspaceRoot}",
                "status:Saved."
            ],
            calls);

        ModelLaunchVariantSaveSelectedActions SaveSelectedVariantActions(
            bool editorLoaded,
            ModelLaunchVariantWorkflowResult workflowResult)
            => new(
                async (message, action) =>
                {
                    calls.Add($"busy:{message}");
                    await action();
                },
                modelId =>
                {
                    calls.Add($"loaded:{modelId}:{editorLoaded}");
                    return editorLoaded;
                },
                () =>
                {
                    calls.Add("render");
                    return Task.CompletedTask;
                },
                () =>
                {
                    calls.Add("read");
                    return settings;
                },
                () =>
                {
                    calls.Add("runtime");
                    return "runtime-1";
                },
                request =>
                {
                    calls.Add($"save-as:{request.SourceModel.Id}:{request.RequestedName}:{request.LaunchSettings.Port}");
                    return Task.FromResult(workflowResult);
                },
                Actions());
    }


    [Fact]
    public async Task ModelLaunchSettingsWorkflowBuildsDraftsSavesProfilesAndPreservesDefaultPort()
    {
        var root = CreateTempRoot();
        await using var store = new StateStore(Path.Combine(root, "state", "local-llm-console.db"));
        await store.InitializeAsync();
        var model = new ModelRecord("model-1", "Qwen", Path.Combine(root, "qwen.gguf"), OwnershipKind.External, "{}", DateTimeOffset.UtcNow);
        await store.UpsertModelAsync(model);
        using var sessions = CreateLoadedModelSessionManager();
        var profiles = new ModelLaunchProfileService(store, sessions);
        var workflow = new ModelLaunchSettingsWorkflowService(profiles);
        var defaults = AppSettings.CreateDefault(root) with
        {
            Port = 8081,
            ContextSize = 4096,
            AutoLoadGatewayEnabled = true,
            AutoLoadGatewayPort = 8082
        };

        var draft = await workflow.BuildAsync(model, defaults, TestContext.Current.CancellationToken);
        var savedProfile = await workflow.SaveForModelAsync(model, draft.LaunchSettings with { Port = 8099, ContextSize = 32768 }, "runtime-1", TestContext.Current.CancellationToken);
        var savedResult = await workflow.SaveProfileAsync(model, draft.LaunchSettings with { Port = 8100, ContextSize = 65536 }, "runtime-2", TestContext.Current.CancellationToken);
        var saved = await workflow.BuildAsync(model, defaults, TestContext.Current.CancellationToken);
        var appliedDefaults = ModelLaunchSettingsWorkflowService.ApplyLaunchDefaults(defaults, defaults with { Port = 9000, ContextSize = 65536 });
        var defaultsResult = ModelLaunchSettingsWorkflowService.SaveLaunchDefaults(defaults, defaults with { Port = 9000, ContextSize = 131072 });

        Assert.False(draft.HasSavedProfile);
        Assert.Null(draft.SavedProfile);
        Assert.Equal(8081, draft.LaunchSettings.Port);
        Assert.Equal("runtime-1", savedProfile.RuntimeId);
        Assert.Equal("runtime-2", savedResult.SavedSettings.RuntimeId);
        Assert.Equal("Launch profile saved for Qwen.", savedResult.StatusMessage);
        Assert.True(saved.HasSavedProfile);
        Assert.Equal(8100, saved.LaunchSettings.Port);
        Assert.Equal(65536, saved.LaunchSettings.ContextSize);
        Assert.Equal("runtime-2", saved.RuntimeId);
        Assert.Equal(8081, appliedDefaults.Port);
        Assert.Equal(65536, appliedDefaults.ContextSize);
        Assert.Equal(8081, defaultsResult.Settings.Port);
        Assert.Equal(131072, defaultsResult.Settings.ContextSize);
        Assert.Equal("Launch defaults saved. Model ports stay per-model.", defaultsResult.StatusMessage);
    }


    [Fact]
    public async Task ModelLaunchProfileServiceAllocatesPortsAroundGatewayProfilesAndSessions()
    {
        var root = CreateTempRoot();
        await using var store = new StateStore(Path.Combine(root, "state", "local-llm-console.db"));
        await store.InitializeAsync();
        using var sessions = CreateLoadedModelSessionManager();
        var settings = AppSettings.CreateDefault(root) with
        {
            Port = 8081,
            AutoLoadGatewayEnabled = true,
            AutoLoadGatewayPort = 8082
        };
        var runtime = new RuntimeRecord("runtime", "llama.cpp CUDA", RuntimeMode.Native, RuntimeBackend.Cuda, Path.Combine(root, "llama-server.exe"), "{}", DateTimeOffset.UtcNow);
        var target = new ModelRecord("target", "Target Model", Path.Combine(root, "target.gguf"), OwnershipKind.External, "{}", DateTimeOffset.UtcNow);
        var profiled = new ModelRecord("profiled", "Profiled Model", Path.Combine(root, "profiled.gguf"), OwnershipKind.External, "{}", DateTimeOffset.UtcNow);
        var loaded = new ModelRecord("loaded", "Loaded Model", Path.Combine(root, "loaded.gguf"), OwnershipKind.External, "{}", DateTimeOffset.UtcNow);
        await store.UpsertModelAsync(target);
        await store.UpsertModelAsync(profiled);
        await store.UpsertModelAsync(loaded);
        await store.SaveModelLaunchSettingsAsync(profiled.Id, ModelLaunchSettings.FromAppSettings(settings with { Port = 8081 }));
        await store.SaveModelLaunchSettingsAsync(target.Id, ModelLaunchSettings.FromAppSettings(settings with { Port = 8081 }));
        sessions.AttachExisting(runtime, loaded, settings with { Port = 8083 }, "loaded.log", LlamaRuntimeState.Loaded, "", "loaded-session", DateTimeOffset.UtcNow);

        var service = new ModelLaunchProfileService(store, sessions);
        var ensured = await service.EnsureAsync(target, settings);
        var saved = await store.GetModelLaunchSettingsAsync(target.Id);

        Assert.False(await service.IsPortAvailableAsync(target.Id, 8081, settings));
        Assert.False(await service.IsPortAvailableAsync(target.Id, 8082, settings));
        Assert.False(await service.IsPortAvailableAsync(target.Id, 8083, settings));
        Assert.True(await service.IsPortAvailableAsync(target.Id, 8084, settings));
        Assert.Equal(8084, ensured?.Port);
        Assert.Equal(8084, saved?.Port);
    }


    [Fact]
    public async Task ModelLaunchVariantWorkflowCreatesAliasWithDedicatedPortAndProfile()
    {
        var root = CreateTempRoot();
        await using var store = new StateStore(Path.Combine(root, "state", "local-llm-console.db"));
        await store.InitializeAsync();
        using var sessions = CreateLoadedModelSessionManager();
        var settings = AppSettings.CreateDefault(root) with
        {
            Port = 8081,
            AutoLoadGatewayEnabled = true,
            AutoLoadGatewayPort = 8082
        };
        var source = new ModelRecord("qwen", "Qwen Test", Path.Combine(root, "qwen.gguf"), OwnershipKind.External, "{}", DateTimeOffset.UtcNow);
        var profiled = new ModelRecord("profiled", "Profiled Model", Path.Combine(root, "profiled.gguf"), OwnershipKind.External, "{}", DateTimeOffset.UtcNow);
        await store.UpsertModelAsync(source);
        await store.UpsertModelAsync(profiled);
        await store.SaveModelLaunchSettingsAsync(profiled.Id, ModelLaunchSettings.FromAppSettings(settings with { Port = 8081 }));
        var catalog = new ModelCatalogService(store);
        var profiles = new ModelLaunchProfileService(store, sessions);
        var workflow = new ModelLaunchVariantWorkflowService(catalog, profiles);

        var unchanged = await workflow.SaveAsNewAsync(new ModelLaunchVariantWorkflowRequest(
            source,
            source.Name,
            settings,
            "runtime-cuda",
            settings),
            TestContext.Current.CancellationToken);
        var created = await workflow.SaveAsNewAsync(new ModelLaunchVariantWorkflowRequest(
            source,
            "Qwen Test 32K",
            settings with { ContextSize = 32768, Port = 9000 },
            "runtime-cuda",
            settings),
            TestContext.Current.CancellationToken);
        var duplicate = await workflow.SaveAsNewAsync(new ModelLaunchVariantWorkflowRequest(
            source,
            "Qwen Test 32K",
            settings,
            "runtime-cuda",
            settings),
            TestContext.Current.CancellationToken);
        var alias = Assert.Single(await store.ListModelsAsync(), ModelAliasService.IsLaunchAlias);
        var saved = await store.GetModelLaunchSettingsAsync(alias.Id);

        Assert.False(unchanged.Success);
        Assert.Contains("Change the name", unchanged.StatusMessage, StringComparison.Ordinal);
        Assert.True(created.Success);
        Assert.NotNull(created.Alias);
        Assert.Equal("Qwen Test 32K", alias.Name);
        Assert.Equal(source.ModelPath, alias.ModelPath);
        Assert.Equal(8083, created.Port);
        Assert.Equal(8083, saved?.Port);
        Assert.Equal(32768, saved?.ContextSize);
        Assert.Equal("runtime-cuda", saved?.RuntimeId);
        Assert.False(duplicate.Success);
        Assert.Contains("already exists", duplicate.StatusMessage, StringComparison.OrdinalIgnoreCase);
    }


    [Fact]
    public async Task ModelRuntimeLaunchPreparationOwnsApiKeyPortsPrerequisitesAndGatewayAdmission()
    {
        var root = CreateTempRoot();
        var modelPath = Path.Combine(root, "model.gguf");
        File.WriteAllBytes(modelPath, new byte[1024 * 1024]);
        var settings = AppSettings.CreateDefault(root) with
        {
            Port = 8084,
            ModelApiKey = "  existing-api-key  ",
            ContextSize = 131072,
            GpuLayers = AppSettings.DefaultGpuLayers
        };
        var model = new ModelRecord("model", "Big Model", modelPath, OwnershipKind.External, "{}", DateTimeOffset.UtcNow);
        var cpuRuntime = new RuntimeRecord("runtime-cpu", "CPU", RuntimeMode.Native, RuntimeBackend.Cpu, "llama-server.exe", "{}", DateTimeOffset.UtcNow);
        var cudaRuntime = cpuRuntime with { Id = "runtime-cuda", Name = "CUDA", Backend = RuntimeBackend.Cuda };
        using var sessions = CreateLoadedModelSessionManager();
        var coordinator = new RuntimeSessionCoordinator(sessions, Path.Combine(root, "logs"));
        var portProbes = new List<int>();
        var prerequisites = new RuntimeLaunchPrerequisiteService(
            _ => Task.FromResult(ReadyWslReport()),
            () => WindowsBuildTools(),
            new ScriptedProcessRunner(_ => new ProcessRunResult(0, "ok", "")),
            (port, _) =>
            {
                portProbes.Add(port);
                return Task.FromResult(false);
            },
            () => "wsl.exe");
        var service = new ModelRuntimeLaunchPreparationService(
            coordinator,
            prerequisites,
            new RuntimeLaunchAdmissionService(new VramAdmissionService()),
            new GpuStatusProbeService(new ScriptedProcessRunner(_ => new ProcessRunResult(0, "", ""))));

        var prepared = await service.PrepareAsync(new ModelRuntimeLaunchPreparationRequest(
            cpuRuntime,
            model,
            settings,
            InteractivePrompts: true,
            AutoLoadGatewayEnabled: true,
            AutoLoadGatewayPort: 8082,
            (launchSettings, _) => Task.FromResult(launchSettings with { ModelApiKey = launchSettings.ModelApiKey.Trim() }),
            (_, _) => Task.FromResult(false),
            (_, _) => throw new InvalidOperationException("CPU launch should not request admission confirmation.")),
            TestContext.Current.CancellationToken);

        sessions.AttachExisting(cudaRuntime, model with { Id = "loaded", Name = "Loaded Model" }, settings with { Port = 8081 }, "loaded.log", LlamaRuntimeState.Loaded, "", "loaded-session", DateTimeOffset.UtcNow);
        var blocked = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.PrepareAsync(new ModelRuntimeLaunchPreparationRequest(
                cudaRuntime,
                model,
                settings,
                InteractivePrompts: false,
                AutoLoadGatewayEnabled: true,
                AutoLoadGatewayPort: 8082,
                (launchSettings, _) => Task.FromResult(launchSettings),
                (_, _) => Task.FromResult(false),
                ReadMemoryAsync: _ => Task.FromResult<VramMemorySnapshot?>(new VramMemorySnapshot(0.1, 24))),
                TestContext.Current.CancellationToken));

        Assert.True(prepared.CanLaunch);
        Assert.Equal("existing-api-key", prepared.LaunchSettings.ModelApiKey);
        Assert.Equal([8084, 8084], portProbes);
        Assert.Contains("Auto-load gateway refused", blocked.Message, StringComparison.Ordinal);
    }


}
