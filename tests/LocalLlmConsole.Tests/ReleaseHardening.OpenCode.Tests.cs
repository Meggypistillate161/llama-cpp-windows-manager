using LocalLlmConsole.Models;
using LocalLlmConsole.Services;
using LocalLlmConsole.ViewModels;
using System.Windows;

namespace LocalLlmConsole.Tests;


public sealed partial class ReleaseHardeningTests
{
    [Fact]
    public void OpenCodeLocalProviderCredentialsCanBeRefreshed()
    {
        var root = CreateTempRoot();
        var service = new OpenCodeConfigService(root);
        var configPath = Path.Combine(root, "opencode.jsonc");
        var model = new ModelRecord(
            "model",
            "Test Model",
            Path.Combine(root, "models", "test-model.gguf"),
            OwnershipKind.AppOwned,
            "{}",
            DateTimeOffset.UtcNow);
        var draft = service.CreateLocalModelDraft(configPath, model, "http://127.0.0.1:8081/v1", "old-key", 131_072, 32_768);
        service.SaveLocalModelSnippet(configPath, model, "http://127.0.0.1:8081/v1", "old-key", draft.Snippet, addAsNew: false);

        var updated = service.UpdateLocalProviderCredentials(configPath, "http://127.0.0.1:8090/v1", "new-key");
        var config = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(configPath))!;
        var options = config["provider"]?[draft.ProviderId]?["options"];

        Assert.True(updated);
        Assert.Equal("http://127.0.0.1:8081/v1", options?["baseURL"]?.ToString());
        Assert.Equal("new-key", options?["apiKey"]?.ToString());
    }


    [Fact]
    public void OpenCodePageLayoutLivesBehindFactory()
    {
        var source = ReadMainWindowSources();
        var openCodePage = File.ReadAllText(FindRepositoryFile("src", "LocalLlmConsole.App", "MainWindow.OpenCode.cs"));
        var factory = File.ReadAllText(FindRepositoryFile("src", "LocalLlmConsole.App", "Ui", "OpenCodePageFactory.cs"));
        var pageState = File.ReadAllText(FindRepositoryFile("src", "LocalLlmConsole.App", "Ui", "OpenCodePageState.cs"));
        var fileSetState = File.ReadAllText(FindRepositoryFile("src", "LocalLlmConsole.App", "Services", "OpenCodeFileSetState.cs"));

        Assert.Contains("OpenCodePageFactory.Create(new OpenCodePageRequest(", openCodePage, StringComparison.Ordinal);
        Assert.Contains("ApplyOpenCodePageControls(page)", openCodePage, StringComparison.Ordinal);
        Assert.Contains("_coreServices.OpenCodeServices.OpenCodeFileSetApplication.LoadOrDetect()", openCodePage, StringComparison.Ordinal);
        Assert.Contains("_openCodePage.Apply(page);", openCodePage, StringComparison.Ordinal);
        Assert.DoesNotContain("_openCodeServices.Page", openCodePage, StringComparison.Ordinal);
        Assert.Contains("private readonly OpenCodeFileSetState _openCodeFileSet;", source, StringComparison.Ordinal);
        Assert.Contains("_openCodeFileSet = uiState.OpenCodeFileSet", source, StringComparison.Ordinal);
        Assert.DoesNotContain("private readonly OpenCodeFileSetState _openCodeFileSet = new();", source, StringComparison.Ordinal);
        Assert.Contains("public sealed record OpenCodePageActions(", factory, StringComparison.Ordinal);
        Assert.Contains("public sealed record OpenCodePageControls(", factory, StringComparison.Ordinal);
        Assert.Contains("public sealed class OpenCodePageState", pageState, StringComparison.Ordinal);
        Assert.Contains("public sealed class OpenCodeFileSetState", fileSetState, StringComparison.Ordinal);
        Assert.Contains("public OpenCodeModelEntry? SelectedModel", pageState, StringComparison.Ordinal);
        Assert.Contains("public ModelRecord? SelectedLocalModel", pageState, StringComparison.Ordinal);
        Assert.Contains("public OpenCodeAgentEntry? SelectedAgent", pageState, StringComparison.Ordinal);
        Assert.Contains("public string ModelSnippet", pageState, StringComparison.Ordinal);
        Assert.Contains("public OpenCodeFileSet Current", fileSetState, StringComparison.Ordinal);
        Assert.Contains("public OpenCodeFileSet Set(OpenCodeFileSet fileSet)", fileSetState, StringComparison.Ordinal);
        Assert.Contains("OpenCode models", factory, StringComparison.Ordinal);
        Assert.Contains("OpenCode agents", factory, StringComparison.Ordinal);
        Assert.Contains("Detect Files", factory, StringComparison.Ordinal);
        Assert.Contains("Choose Config", factory, StringComparison.Ordinal);
        Assert.Contains("Choose Agents Folder", factory, StringComparison.Ordinal);
        Assert.Contains("Add As New", factory, StringComparison.Ordinal);
        Assert.Contains("markdown file", factory, StringComparison.Ordinal);
        Assert.Contains("PageSectionFactory.VerticalGridSplitter(1)", factory, StringComparison.Ordinal);
        Assert.Contains("request.Actions.ModelSnippetChanged()", factory, StringComparison.Ordinal);
        Assert.DoesNotContain("OpenCodeSplitSection", source, StringComparison.Ordinal);
        Assert.DoesNotContain("OpenCodePaneFrame", source, StringComparison.Ordinal);
        Assert.DoesNotContain("OpenCodePathRow", source, StringComparison.Ordinal);
        Assert.DoesNotContain("_openCodeModelCombo", source, StringComparison.Ordinal);
        Assert.DoesNotContain("_openCodeLocalModelCombo", source, StringComparison.Ordinal);
        Assert.DoesNotContain("_openCodeAgentCombo", source, StringComparison.Ordinal);
        Assert.DoesNotContain("_openCodeModelSnippetBox", source, StringComparison.Ordinal);
        Assert.DoesNotContain("_openCodeAgentSnippetBox", source, StringComparison.Ordinal);
        Assert.DoesNotContain("_openCodeSaveModelButton", source, StringComparison.Ordinal);
        Assert.DoesNotContain("_openCodeCreateAgentButton", source, StringComparison.Ordinal);
        Assert.DoesNotContain("_openCodeFiles", source, StringComparison.Ordinal);
    }


    [Fact]
    public void OpenCodeModelEditorSessionOwnsSnapshotAndProgrammaticState()
    {
        var source = ReadMainWindowSources();
        var session = new OpenCodeModelEditorSession();
        var observedProgrammaticState = false;

        session.SetSavedSnippet("saved");
        session.RunProgrammaticUpdate(() => observedProgrammaticState = session.IsProgrammaticUpdate);
        session.ClearSavedSnippet();

        Assert.True(observedProgrammaticState);
        Assert.False(session.IsProgrammaticUpdate);
        Assert.Equal("", session.SavedSnippet);
        Assert.Contains("_openCodeModelEditor.IsProgrammaticUpdate", source, StringComparison.Ordinal);
        Assert.Contains("_openCodeModelEditor.SavedSnippet", source, StringComparison.Ordinal);
        Assert.Contains("_coreServices.OpenCodeServices.OpenCodeModelApplication.EditorState", source, StringComparison.Ordinal);
        Assert.DoesNotContain("_openCodeModelEditor.MatchesSavedSnippet", source, StringComparison.Ordinal);
        Assert.DoesNotContain("public bool MatchesSavedSnippet", File.ReadAllText(FindRepositoryFile("src", "LocalLlmConsole.App", "Services", "OpenCodeModelEditorSession.cs")), StringComparison.Ordinal);
        Assert.Contains("_openCodeModelEditor.RunProgrammaticUpdate", source, StringComparison.Ordinal);
        Assert.Contains("SetOpenCodeModelSnippetText", source, StringComparison.Ordinal);
        Assert.DoesNotContain("_updatingOpenCodeModelEditor", source, StringComparison.Ordinal);
        Assert.DoesNotContain("_openCodeSelectedModelSnapshot", source, StringComparison.Ordinal);
    }

    [Fact]
    public async Task OpenCodeLocalModelApplicationServiceAppliesDraftAndSaveStatesInOrder()
    {
        var service = CreateOpenCodeLocalModelApplicationService(CreateTempRoot());
        var calls = new List<string>();
        var lastState = new OpenCodeLocalModelActionState("", false, false, false, false, false, false);
        var actions = new OpenCodeLocalModelDraftApplicationActions(
            snippet =>
            {
                calls.Add($"snippet:{snippet}");
            },
            () =>
            {
                calls.Add("editor");
            },
            status =>
            {
                calls.Add($"status:{status}");
            },
            state =>
            {
                lastState = state;
                calls.Add($"state:{state.Status}:{state.AddEnabled}");
            },
            (valid, matchesSaved) =>
            {
                calls.Add($"existing:{valid}:{matchesSaved}");
            });

        service.ApplyNoLocalModelSelected(
            new OpenCodeLocalModelActionState("Choose a local model to add.", AddVisible: true, AddEnabled: false, UpdateVisible: false, UpdateEnabled: false, AddAsNewVisible: false, AddAsNewEnabled: false),
            actions);
        service.ApplyDraftLoaded(
            new OpenCodeLocalModelDraft("provider/model", "provider", "model", "Model", "{ draft }"),
            actions);
        service.ApplyDraftLoadFailure("failed to draft", actions);
        await service.ApplySaveResultAsync(
            new OpenCodeLocalModelSaveResult("provider/model", "Saved OpenCode model provider/model."),
            new OpenCodeLocalModelSaveApplicationActions(
                preferredModelId =>
                {
                    calls.Add($"refresh:{preferredModelId}");
                    return Task.CompletedTask;
                },
                status =>
                {
                    calls.Add($"status:{status}");
                }));

        Assert.Equal(
            [
                "snippet:",
                "state:Choose a local model to add.:False",
                "existing:False:True",
                "snippet:{ draft }",
                "editor",
                "status:failed to draft",
                "state:failed to draft:False",
                "existing:False:True",
                "refresh:provider/model",
                "status:Saved OpenCode model provider/model."
            ],
            calls);
        Assert.True(lastState.AddVisible);
        Assert.False(lastState.AddEnabled);
        Assert.False(lastState.UpdateVisible);
        Assert.False(lastState.AddAsNewVisible);
    }

    [Fact]
    public async Task OpenCodeLocalModelApplicationServiceLoadsDraftsAndHandlesStaleOrFailedSelections()
    {
        var root = CreateTempRoot();
        var service = CreateOpenCodeLocalModelApplicationService(root);
        var settings = AppSettings.CreateDefault(root) with { ContextSize = 0 };
        var model = new ModelRecord("model-1", "Model One", Path.Combine(root, "model.gguf"), OwnershipKind.External, "{}", DateTimeOffset.UtcNow);
        var calls = new List<string>();
        ModelRecord? selectedModel = model;
        var actions = new OpenCodeLocalModelDraftApplicationActions(
            snippet => calls.Add($"snippet:{snippet.Contains("\"provider\"", StringComparison.Ordinal)}"),
            () => calls.Add("editor"),
            status => calls.Add($"status:{status}"),
            state => calls.Add($"state:{state.Status}:{state.AddEnabled}"),
            (valid, matchesSaved) => calls.Add($"existing:{valid}:{matchesSaved}"));

        var loaded = await service.LoadDraftAsync(
            Request(model),
            Actions(
                ensureProfile: (_, _) => ValueTask.FromResult<ModelLaunchSettings?>(ModelLaunchSettings.FromAppSettings(settings with { Port = 8091 })),
                readCapabilities: (_, _) =>
                {
                    calls.Add("capabilities");
                    return ValueTask.FromResult(ModelCapabilityService.Empty());
                },
                draftActions: actions),
            TestContext.Current.CancellationToken);
        selectedModel = new ModelRecord("model-2", "Model Two", Path.Combine(root, "model2.gguf"), OwnershipKind.External, "{}", DateTimeOffset.UtcNow);
        var stale = await service.LoadDraftAsync(
            Request(model),
            Actions(
                ensureProfile: (_, _) => ValueTask.FromResult<ModelLaunchSettings?>(null),
                readCapabilities: (_, _) => throw new InvalidOperationException("Stale selections should not read capabilities."),
                draftActions: actions),
            TestContext.Current.CancellationToken);
        selectedModel = model;
        var staleAfterCreate = await service.LoadDraftAsync(
            Request(model),
            Actions(
                ensureProfile: (_, _) => ValueTask.FromResult<ModelLaunchSettings?>(null),
                readCapabilities: (_, _) =>
                {
                    selectedModel = new ModelRecord("model-2", "Model Two", Path.Combine(root, "model2.gguf"), OwnershipKind.External, "{}", DateTimeOffset.UtcNow);
                    return ValueTask.FromResult(ModelCapabilityService.Empty());
                },
                draftActions: actions),
            TestContext.Current.CancellationToken);
        var noSelection = await service.LoadDraftAsync(
            Request(null),
            Actions(
                ensureProfile: (_, _) => throw new InvalidOperationException("No model should not resolve settings."),
                readCapabilities: (_, _) => throw new InvalidOperationException("No model should not read capabilities."),
                draftActions: actions),
            TestContext.Current.CancellationToken);
        selectedModel = model;
        var failed = await service.LoadDraftAsync(
            Request(model),
            Actions(
                ensureProfile: (_, _) => throw new InvalidOperationException("cannot draft"),
                readCapabilities: (_, _) => throw new InvalidOperationException("Should not read after resolve failure."),
                draftActions: actions),
            TestContext.Current.CancellationToken);

        Assert.Equal(OpenCodeLocalModelDraftLoadOutcome.DraftLoaded, loaded);
        Assert.Equal(OpenCodeLocalModelDraftLoadOutcome.StaleSelection, stale);
        Assert.Equal(OpenCodeLocalModelDraftLoadOutcome.StaleSelection, staleAfterCreate);
        Assert.Equal(OpenCodeLocalModelDraftLoadOutcome.NoLocalModelSelected, noSelection);
        Assert.Equal(OpenCodeLocalModelDraftLoadOutcome.Failed, failed);
        Assert.Equal(
            [
                "capabilities",
                "snippet:True",
                "editor",
                "snippet:False",
                "state:Choose a local model to add.:False",
                "existing:False:True",
                "status:cannot draft",
                "state:cannot draft:False",
                "existing:False:True"
            ],
            calls);

        OpenCodeLocalModelDraftLoadRequest Request(ModelRecord? selected)
            => new(
                IsAddNewModelSelected: true,
                HasModelSnippetBox: true,
                SelectedLocalModel: selected,
                Files: new OpenCodeFileSet(Path.Combine(root, "opencode.jsonc"), Path.Combine(root, "agent")),
                ApplicationSettings: settings,
                LoadedSession: null,
                UseGatewayProvider: true);

        OpenCodeLocalModelDraftLoadActions Actions(
            OpenCodeLaunchProfileEnsurer ensureProfile,
            OpenCodeModelCapabilityReader readCapabilities,
            OpenCodeLocalModelDraftApplicationActions draftActions)
            => new(
                () => selectedModel,
                ensureProfile,
                (settings, _) => ValueTask.FromResult(settings),
                readCapabilities,
                draftActions);
    }

    [Fact]
    public async Task OpenCodeLocalModelApplicationServiceSavesSnippetsAndHandlesMissingSelection()
    {
        var root = CreateTempRoot();
        var service = CreateOpenCodeLocalModelApplicationService(root);
        var files = new OpenCodeFileSet(Path.Combine(root, "opencode.jsonc"), Path.Combine(root, "agent"));
        var settings = AppSettings.CreateDefault(root) with { Port = 8082 };
        var launchSettings = settings with { Port = 8123 };
        var model = new ModelRecord("model-1", "Model One", Path.Combine(root, "model.gguf"), OwnershipKind.External, "{}", DateTimeOffset.UtcNow);
        var workflow = CreateOpenCodeLocalModelWorkflowService(root);
        var draft = await workflow.CreateDraftAsync(
            new OpenCodeLocalModelDraftBuildRequest(files, model, settings, launchSettings, UseGatewayProvider: false),
            (_, _) => ValueTask.FromResult(ModelCapabilityService.Empty()),
            TestContext.Current.CancellationToken);
        var calls = new List<string>();
        var resultActions = new OpenCodeLocalModelSaveApplicationActions(
            preferredModelId =>
            {
                calls.Add($"refresh:{preferredModelId}");
                return Task.CompletedTask;
            },
            status => calls.Add($"status:{status}"));

        var missing = await service.SaveSnippetAsync(
            new OpenCodeLocalModelSnippetSaveRequest(
                SelectedLocalModel: null,
                files,
                settings,
                LoadedSession: null,
                Snippet: "{ missing }",
                AddAsNew: false,
                UseGatewayProvider: true),
            new OpenCodeLocalModelSnippetSaveActions(
                (_, _) => throw new InvalidOperationException("Missing selection should not resolve launch settings."),
                (_, _) => throw new InvalidOperationException("Missing selection should not ensure an API key."),
                resultActions),
            TestContext.Current.CancellationToken);
        var saved = await service.SaveSnippetAsync(
            new OpenCodeLocalModelSnippetSaveRequest(
                model,
                files,
                settings,
                LoadedSession: null,
                Snippet: draft.Snippet,
                AddAsNew: true,
                UseGatewayProvider: false),
            new OpenCodeLocalModelSnippetSaveActions(
                (selected, _) =>
                {
                    calls.Add($"profile:{selected.Id}");
                    return ValueTask.FromResult<ModelLaunchSettings?>(ModelLaunchSettings.FromAppSettings(launchSettings));
                },
                (resolved, _) =>
                {
                    calls.Add($"api:{resolved.Port}");
                    return ValueTask.FromResult(resolved);
                },
                resultActions),
            TestContext.Current.CancellationToken);

        Assert.Equal(OpenCodeLocalModelSnippetSaveOutcome.NoLocalModelSelected, missing);
        Assert.Equal(OpenCodeLocalModelSnippetSaveOutcome.Saved, saved);
        Assert.Equal(
            [
                "status:Choose a local model to add.",
                "profile:model-1",
                "api:8123"
            ],
            calls.Take(3).ToArray());
        Assert.StartsWith("refresh:local-llm-console-model-", calls[3], StringComparison.Ordinal);
        Assert.StartsWith("status:Added OpenCode model local-llm-console-model-", calls[4], StringComparison.Ordinal);
    }

    [Fact]
    public async Task OpenCodeLocalModelApplicationServiceUpdatesAddStateForSelectionAnalysisAndFailures()
    {
        var root = CreateTempRoot();
        var openCode = new OpenCodeConfigService(root);
        var workflow = new OpenCodeLocalModelWorkflowService(new OpenCodeModelSyncService(openCode));
        var service = new OpenCodeLocalModelApplicationService(workflow);
        var model = new ModelRecord("model-1", "Model One", Path.Combine(root, "model.gguf"), OwnershipKind.External, "{}", DateTimeOffset.UtcNow);
        var calls = new List<string>();
        var files = new OpenCodeFileSet(Path.Combine(root, "opencode.jsonc"), Path.Combine(root, "agent"));
        var settings = AppSettings.CreateDefault(root);
        openCode.EnsureFiles(files);
        var draft = await workflow.CreateDraftAsync(
            new OpenCodeLocalModelDraftBuildRequest(
                files,
                model,
                settings,
                settings,
                UseGatewayProvider: true),
            (_, _) => ValueTask.FromResult(ModelCapabilityService.Empty()),
            TestContext.Current.CancellationToken);

        var skipped = service.UpdateAddState(
            Request(IsAddNewModelSelected: false, model),
            Actions());
        var noSelection = service.UpdateAddState(
            Request(IsAddNewModelSelected: true, selected: null),
            Actions());
        var analyzed = service.UpdateAddState(
            Request(IsAddNewModelSelected: true, model),
            Actions());
        File.WriteAllText(files.ConfigPath, "{");
        var failed = service.UpdateAddState(
            Request(IsAddNewModelSelected: true, model),
            Actions());

        Assert.Equal(OpenCodeLocalModelAddStateOutcome.Skipped, skipped);
        Assert.Equal(OpenCodeLocalModelAddStateOutcome.NoLocalModelSelected, noSelection);
        Assert.Equal(OpenCodeLocalModelAddStateOutcome.Analyzed, analyzed);
        Assert.Equal(OpenCodeLocalModelAddStateOutcome.Failed, failed);
        Assert.Equal(3, calls.Count);
        Assert.Equal("state:Choose a local model to add.:False", calls[0]);
        Assert.StartsWith("state:Ready to add: local-llm-console/model-", calls[1], StringComparison.Ordinal);
        Assert.EndsWith(":True", calls[1], StringComparison.Ordinal);
        Assert.StartsWith("state:", calls[2], StringComparison.Ordinal);
        Assert.DoesNotContain("Ready to add", calls[2], StringComparison.Ordinal);
        Assert.EndsWith(":False", calls[2], StringComparison.Ordinal);

        OpenCodeLocalModelAddStateRequest Request(bool IsAddNewModelSelected, ModelRecord? selected)
            => new(
                IsAddNewModelSelected,
                files.ConfigPath,
                selected,
                Snippet: draft.Snippet,
                UseGatewayProvider: true);

        OpenCodeLocalModelAddStateActions Actions()
            => new(state => calls.Add($"state:{state.Status}:{state.AddEnabled}"));
    }

    [Fact]
    public async Task OpenCodePageApplicationServiceAppliesChoicesLoadPathsHealthAndFileSetsInOrder()
    {
        var source = ReadMainWindowSources();
        var openCodeCommandSources = string.Join(
            Environment.NewLine,
            File.ReadAllText(FindRepositoryFile("src", "LocalLlmConsole.App", "MainWindow.OpenCodeActions.cs")),
            File.ReadAllText(FindRepositoryFile("src", "LocalLlmConsole.App", "MainWindow.OpenCodeAgents.cs")));
        var service = new OpenCodePageApplicationService();
        var calls = new List<string>();
        var model = new OpenCodeModelEntry("provider/model", "provider", "model", "Model");
        var addModel = new OpenCodeModelEntry("", "", "", "Add New...", IsAddNew: true);
        var agent = new OpenCodeAgentEntry("config:agent", "agent", OpenCodeAgentKind.Config, "opencode.jsonc", "agent (config)");
        var addAgent = new OpenCodeAgentEntry("", "", OpenCodeAgentKind.Config, "", "Add New...", IsAddNew: true);
        var local = new ModelRecord("local-model", "Local Model", "local.gguf", OwnershipKind.External, "{}", DateTimeOffset.UtcNow);

        var choicesActions = new OpenCodeChoicesApplicationActions(
            choices => calls.Add($"choices:{choices.LocalModels.Count}:{choices.Models.Count}:{choices.Agents.Count}"),
            () => calls.Add("local:first"),
            selected => calls.Add($"model:{selected?.FullId ?? ""}"),
            selected => calls.Add($"agent:{selected?.Id ?? ""}"));
        var pathActions = new OpenCodePathApplicationActions(
            path => calls.Add($"config:{Path.GetFileName(path)}"),
            path => calls.Add($"agents:{Path.GetFileName(path)}"));
        var healthActions = new OpenCodeHealthApplicationActions(
            summary => calls.Add($"health:{summary}"),
            detail => calls.Add($"health-detail:{detail}"),
            resource => calls.Add($"health-resource:{resource}"));
        var fileSetActions = new OpenCodeFileSetApplicationActions(
            files => calls.Add($"files:{Path.GetFileName(files.ConfigPath)}:{Path.GetFileName(files.AgentsDirectory)}"),
            () =>
            {
                calls.Add("refresh");
                return Task.CompletedTask;
            },
            status => calls.Add($"status:{status}"));

        service.ApplyPaths(new OpenCodeFileSet(@"C:\opencode\opencode.jsonc", @"C:\opencode\agent"), pathActions);
        service.ApplyHealth(new OpenCodeGatewayHealthState("healthy", "all good", IsWarning: false), healthActions);
        service.ApplyHealth(new OpenCodeGatewayHealthState("warning", "fix config", IsWarning: true), healthActions);
        service.ApplyChoices(new OpenCodePageChoices([local], [model, addModel], [agent, addAgent], model, agent), choicesActions);
        await service.ApplyDetectedFileSetAsync(new OpenCodeFileSet(@"C:\detected\opencode.jsonc", @"C:\detected\agent"), fileSetActions);
        await service.ApplyConfigFileSetAsync(new OpenCodeFileSet(@"C:\chosen\opencode.jsonc", @"C:\chosen\agent"), fileSetActions);
        await service.ApplyAgentsDirectoryFileSetAsync(new OpenCodeFileSet(@"C:\chosen\opencode.jsonc", @"C:\agents"), fileSetActions);
        await service.ApplyEnsuredFileSetAsync(new OpenCodeFileSet(@"C:\ready\opencode.jsonc", @"C:\ready\agent"), fileSetActions);

        Assert.Equal(
            [
                "config:opencode.jsonc",
                "agents:agent",
                "health:healthy",
                "health-detail:all good",
                "health-resource:TextMuted",
                "health:warning",
                "health-detail:fix config",
                "health-resource:Warning",
                "choices:1:2:2",
                "local:first",
                "model:provider/model",
                "agent:config:agent",
                "files:opencode.jsonc:agent",
                "refresh",
                "status:OpenCode files detected.",
                "files:opencode.jsonc:agent",
                "refresh",
                "status:OpenCode config set to C:\\chosen\\opencode.jsonc",
                "files:opencode.jsonc:agents",
                "refresh",
                "status:OpenCode agents folder set to C:\\agents",
                "files:opencode.jsonc:agent",
                "refresh",
                "status:OpenCode config and agents folder are ready."
            ],
            calls);
        Assert.Contains("_coreServices.OpenCodeServices.OpenCodeRefreshApplication.RefreshAsync", source, StringComparison.Ordinal);
        Assert.Contains("_coreServices.OpenCodeServices.OpenCodeRefreshApplication.RefreshHealthAsync", source, StringComparison.Ordinal);
        Assert.Contains("_coreServices.OpenCodeServices.OpenCodeModelApplication.SaveSnippetAsync", source, StringComparison.Ordinal);
        Assert.Contains("_coreServices.OpenCodeServices.OpenCodeModelApplication.DeleteAsync", source, StringComparison.Ordinal);
        Assert.Contains("_coreServices.OpenCodeServices.OpenCodeAgentApplication.SaveSnippetAsync", source, StringComparison.Ordinal);
        Assert.Contains("_coreServices.OpenCodeServices.OpenCodeAgentApplication.CreateAsync", source, StringComparison.Ordinal);
        Assert.Contains("_coreServices.OpenCodeServices.OpenCodeAgentApplication.DeleteAsync", source, StringComparison.Ordinal);
        Assert.DoesNotContain("_openCodePage.ModelCombo.SelectedItem = choices.SelectedModel", source, StringComparison.Ordinal);
        Assert.DoesNotContain("_openCodePage.AgentSnippetBox.Text = ex.Message", source, StringComparison.Ordinal);
        Assert.DoesNotContain("_openCodePage.HealthText.Foreground = (System.Windows.Media.Brush)System.Windows.Application.Current.Resources[health.IsWarning", source, StringComparison.Ordinal);
        Assert.DoesNotContain("SetStatus($\"OpenCode config set to", source, StringComparison.Ordinal);
        Assert.DoesNotContain("await RefreshOpenCodeAsync(preferredModelId: result.FullId);", openCodeCommandSources, StringComparison.Ordinal);
        Assert.DoesNotContain("await RefreshOpenCodeAsync(preferredAgentId: result.AgentId);", openCodeCommandSources, StringComparison.Ordinal);
        Assert.DoesNotContain("SetStatus(result.StatusMessage);", openCodeCommandSources, StringComparison.Ordinal);
    }

    [Fact]
    public async Task OpenCodeRefreshApplicationServiceAppliesPathsHealthChoicesAndLoadsSelections()
    {
        var root = CreateTempRoot();
        var openCode = new OpenCodeConfigService(root);
        var sync = new OpenCodeModelSyncService(openCode);
        var workflow = new OpenCodePageWorkflowService(openCode, sync);
        var pageApplication = new OpenCodePageApplicationService();
        var refresh = new OpenCodeRefreshApplicationService(workflow, pageApplication);
        var files = new OpenCodeFileSet(Path.Combine(root, "opencode.jsonc"), Path.Combine(root, "agent"));
        openCode.EnsureFiles(files);
        var settings = AppSettings.CreateDefault(root) with
        {
            AutoLoadGatewayEnabled = false,
            ModelApiKey = "abcdefghijklmnopqrstuvwxyz123456"
        };
        var local = new ModelRecord("qwen", "Qwen", Path.Combine(root, "qwen.gguf"), OwnershipKind.External, "{}", DateTimeOffset.UtcNow);
        var draft = sync.CreateDraft(new OpenCodeLocalModelDraftRequest(files.ConfigPath, local, settings, settings, new OpenCodeModelLimits(8192, 4096), UseGatewayProvider: true));
        var fullId = sync.SaveLocalModelSnippet(new OpenCodeLocalModelSaveRequest(files.ConfigPath, local, settings, settings, draft.Snippet, AddAsNew: false, UseGatewayProvider: true));
        var agentWorkflow = new OpenCodeAgentWorkflowService(openCode);
        var agent = agentWorkflow.CreateAgent(
            files,
            agentWorkflow.AnalyzeNewAgentDraft("Build Agent", markdown: false, []),
            new OpenCodeModelEntry(fullId, draft.ProviderId, draft.ModelId, "Qwen")).Agent;
        var calls = new List<string>();
        var modelReads = 0;

        await refresh.RefreshAsync(
            new OpenCodeRefreshApplicationRequest(
                files,
                settings,
                PreferredModelId: "",
                PreferredAgentId: "",
                CurrentModelId: fullId,
                CurrentAgentId: agent.Id,
                HasHealthTarget: true),
            new OpenCodeRefreshApplicationActions(
                () =>
                {
                    modelReads++;
                    calls.Add("models");
                    return Task.FromResult<IReadOnlyList<ModelRecord>>([local]);
                },
                () =>
                {
                    calls.Add("load-model");
                    return Task.CompletedTask;
                },
                () =>
                {
                    calls.Add("load-agent");
                    return Task.CompletedTask;
                },
                new OpenCodePathApplicationActions(
                    path => calls.Add($"config:{Path.GetFileName(path)}"),
                    path => calls.Add($"agents:{Path.GetFileName(path)}")),
                new OpenCodeHealthApplicationActions(
                    summary => calls.Add($"health:{summary.StartsWith("OpenCode sync:", StringComparison.Ordinal)}"),
                    _ => calls.Add("health-detail"),
                    resource => calls.Add($"health-resource:{resource}")),
                new OpenCodeChoicesApplicationActions(
                    choices => calls.Add($"choices:{choices.LocalModels.Count}:{choices.Models.Count}:{choices.Agents.Count}"),
                    () => calls.Add("local:first"),
                    selected => calls.Add($"model:{selected?.FullId ?? ""}"),
                    selected => calls.Add($"agent:{selected?.Id ?? ""}"))));

        Assert.Equal(1, modelReads);
        Assert.Equal(
            [
                "config:opencode.jsonc",
                "agents:agent",
                "models",
                "health:True",
                "health-detail",
                "health-resource:TextMuted",
                "choices:1:2:2",
                "local:first",
                $"model:{fullId}",
                $"agent:{agent.Id}",
                "load-model",
                "load-agent"
            ],
            calls);
    }

    [Fact]
    public async Task OpenCodeFileSetApplicationServiceAppliesDetectedConfiguredAgentAndEnsuredTransitions()
    {
        var openCodeFiles = File.ReadAllText(FindRepositoryFile("src", "LocalLlmConsole.App", "MainWindow.OpenCodeFiles.cs"));
        var dialogs = File.ReadAllText(FindRepositoryFile("src", "LocalLlmConsole.App", "Services", "FileSystemDialogService.cs"));
        var root = CreateTempRoot();
        var openCode = new OpenCodeConfigService(root);
        var sync = new OpenCodeModelSyncService(openCode);
        var workflow = new OpenCodePageWorkflowService(openCode, sync);
        var pageApplication = new OpenCodePageApplicationService();
        var application = new OpenCodeFileSetApplicationService(workflow, pageApplication);
        var current = new OpenCodeFileSet(
            Path.Combine(root, "initial", "opencode.jsonc"),
            Path.Combine(root, "initial", "agent"));
        var chosenConfig = Path.Combine(root, "chosen", "opencode.jsonc");
        var chosenAgents = Path.Combine(root, "agents");
        var calls = new List<string>();
        var pickerCalls = new List<string>();
        var pickerPlans = new List<OpenCodeConfigFilePickerPlan>();
        var folderRequests = new List<string>();
        OpenCodeFileSet? latest = null;
        Directory.CreateDirectory(Path.GetDirectoryName(current.ConfigPath)!);

        var picker = application.BuildConfigFilePicker(current);
        var openedFolder = application.OpenConfigFolder(current, new OpenCodeConfigFolderOpenActions(folder => calls.Add($"open:{folder}")));
        var ignoredFolder = application.OpenConfigFolder(current with { ConfigPath = "" }, new OpenCodeConfigFolderOpenActions(folder => calls.Add($"open:{folder}")));
        var pickedConfig = await application.ChooseConfigPathAsync(current, PickerActions(chosenConfig, null));
        var pickedAgents = await application.ChooseAgentsDirectoryAsync(current, PickerActions(null, chosenAgents));
        var cancelledConfig = await application.ChooseConfigPathAsync(current, PickerActions("", null));
        var cancelledAgents = await application.ChooseAgentsDirectoryAsync(current, PickerActions(null, null));
        var detected = await application.DetectAsync(Actions());
        var configured = await application.SaveConfigPathAsync(current, chosenConfig, Actions());
        var loadedAfterConfig = application.LoadOrDetect();
        var agents = await application.SaveAgentsDirectoryAsync(current, chosenAgents, Actions());
        var ensured = await application.EnsureAsync(current, Actions());

        Assert.Equal("Choose OpenCode config", picker.Title);
        Assert.Contains("opencode.jsonc", picker.Filter, StringComparison.Ordinal);
        Assert.False(picker.CheckFileExists);
        Assert.True(picker.AddExtension);
        Assert.Equal(".jsonc", picker.DefaultExt);
        Assert.Equal("opencode.jsonc", picker.FileName);
        Assert.Equal(Path.GetDirectoryName(current.ConfigPath), picker.InitialDirectory);
        Assert.Contains("_coreServices.OpenCodeServices.OpenCodeFileSetApplication.ChooseConfigPathAsync", openCodeFiles, StringComparison.Ordinal);
        Assert.Contains("_coreServices.OpenCodeServices.OpenCodeFileSetApplication.ChooseAgentsDirectoryAsync", openCodeFiles, StringComparison.Ordinal);
        Assert.Contains("private OpenCodeFileSetPickerActions OpenCodeFileSetPickerActions()", openCodeFiles, StringComparison.Ordinal);
        Assert.Contains("_coreServices.App.FileSystemDialogs.PickOpenCodeConfigFile(plan, this)", openCodeFiles, StringComparison.Ordinal);
        Assert.Contains("_coreServices.OpenCodeServices.OpenCodeFileSetApplication.OpenConfigFolder", openCodeFiles, StringComparison.Ordinal);
        Assert.DoesNotContain("BuildConfigFilePicker(_openCodeFileSet.Current)", openCodeFiles, StringComparison.Ordinal);
        Assert.DoesNotContain("SaveConfigPathAsync", openCodeFiles, StringComparison.Ordinal);
        Assert.Contains("public async Task<OpenCodeFileSetPickerOutcome> ChooseConfigPathAsync", File.ReadAllText(FindRepositoryFile("src", "LocalLlmConsole.App", "Services", "OpenCodeFileSetApplicationService.cs")), StringComparison.Ordinal);
        Assert.Contains("new WpfOpenFileDialog", dialogs, StringComparison.Ordinal);
        Assert.DoesNotContain("OpenFileDialog", openCodeFiles, StringComparison.Ordinal);
        Assert.DoesNotContain("Path.GetDirectoryName(_openCodeFileSet.ConfigPath)", openCodeFiles, StringComparison.Ordinal);
        Assert.DoesNotContain("Directory.Exists(initialDirectory)", openCodeFiles, StringComparison.Ordinal);
        Assert.DoesNotContain("OpenCodePageWorkflowService.ConfigDirectory", openCodeFiles, StringComparison.Ordinal);
        Assert.Equal(OpenCodeConfigFolderOpenOutcome.Opened, openedFolder);
        Assert.Equal(OpenCodeConfigFolderOpenOutcome.Ignored, ignoredFolder);
        Assert.Equal(OpenCodeFileSetPickerOutcome.Applied, pickedConfig);
        Assert.Equal(OpenCodeFileSetPickerOutcome.Applied, pickedAgents);
        Assert.Equal(OpenCodeFileSetPickerOutcome.Cancelled, cancelledConfig);
        Assert.Equal(OpenCodeFileSetPickerOutcome.Cancelled, cancelledAgents);
        Assert.Equal(OpenCodeFileSetTransitionOutcome.Applied, detected);
        Assert.Equal(OpenCodeFileSetTransitionOutcome.Applied, configured);
        Assert.Equal(OpenCodeFileSetTransitionOutcome.Applied, agents);
        Assert.Equal(OpenCodeFileSetTransitionOutcome.Applied, ensured);
        Assert.Equal([picker.Title, picker.Title], pickerPlans.Select(plan => plan.Title).ToArray());
        Assert.Equal([current.AgentsDirectory, current.AgentsDirectory], folderRequests);
        Assert.Contains($"status:OpenCode config set to {Path.GetFullPath(chosenConfig)}", pickerCalls);
        Assert.Contains($"status:OpenCode agents folder set to {Path.GetFullPath(chosenAgents)}", pickerCalls);
        Assert.NotNull(latest);
        Assert.Equal(Path.GetFullPath(chosenConfig), loadedAfterConfig.ConfigPath);
        Assert.Equal(Path.GetFullPath(current.AgentsDirectory), loadedAfterConfig.AgentsDirectory);
        Assert.True(File.Exists(current.ConfigPath));
        Assert.True(Directory.Exists(current.AgentsDirectory));
        Assert.Contains($"set:{Path.GetFileName(chosenConfig)}:agent", calls);
        Assert.Contains($"set:{Path.GetFileName(current.ConfigPath)}:{Path.GetFileName(chosenAgents)}", calls);
        Assert.Equal(
            [
                $"open:{Path.GetDirectoryName(current.ConfigPath)}",
                "busy:Detecting OpenCode files...",
                "refresh",
                "status:OpenCode files detected.",
                "busy:Setting OpenCode config...",
                "refresh",
                $"status:OpenCode config set to {Path.GetFullPath(chosenConfig)}",
                "busy:Setting OpenCode agents folder...",
                "refresh",
                $"status:OpenCode agents folder set to {Path.GetFullPath(chosenAgents)}",
                "busy:Creating OpenCode files...",
                "refresh",
                "status:OpenCode config and agents folder are ready."
            ],
            calls.Where(call => !call.StartsWith("set:", StringComparison.Ordinal)).ToArray());

        OpenCodeFileSetTransitionActions Actions()
            => new(
                async (message, action) =>
                {
                    calls.Add($"busy:{message}");
                    await action();
                },
                new OpenCodeFileSetApplicationActions(
                    files =>
                    {
                        latest = files;
                        calls.Add($"set:{Path.GetFileName(files.ConfigPath)}:{Path.GetFileName(files.AgentsDirectory)}");
                    },
                    () =>
                    {
                        calls.Add("refresh");
                        return Task.CompletedTask;
                    },
                    status => calls.Add($"status:{status}")));

        OpenCodeFileSetPickerActions PickerActions(string? configPath, string? agentsDirectory)
            => new(
                plan =>
                {
                    pickerPlans.Add(plan);
                    return configPath;
                },
                initialDirectory =>
                {
                    folderRequests.Add(initialDirectory);
                    return agentsDirectory;
                },
                new OpenCodeFileSetTransitionActions(
                    async (message, action) =>
                    {
                        pickerCalls.Add($"busy:{message}");
                        await action();
                    },
                    new OpenCodeFileSetApplicationActions(
                        files => pickerCalls.Add($"set:{Path.GetFileName(files.ConfigPath)}:{Path.GetFileName(files.AgentsDirectory)}"),
                        () =>
                        {
                            pickerCalls.Add("refresh");
                            return Task.CompletedTask;
                        },
                        status => pickerCalls.Add($"status:{status}"))));
    }


    [Fact]
    public void OpenCodeLocalModelsUseSeparateProvidersForConcurrentEndpoints()
    {
        var root = CreateTempRoot();
        var service = new OpenCodeConfigService(root);
        var configPath = Path.Combine(root, "opencode.jsonc");
        var first = new ModelRecord("model-a", "First Model", Path.Combine(root, "models", "first-model.gguf"), OwnershipKind.AppOwned, "{}", DateTimeOffset.UtcNow);
        var second = new ModelRecord("model-b", "Second Model", Path.Combine(root, "models", "second-model.gguf"), OwnershipKind.AppOwned, "{}", DateTimeOffset.UtcNow);

        var firstDraft = service.CreateLocalModelDraft(configPath, first, "http://127.0.0.1:8081/v1", "key-a", 8192, 4096);
        var secondDraft = service.CreateLocalModelDraft(configPath, second, "http://127.0.0.1:8082/v1", "key-b", 8192, 4096);
        var firstId = service.SaveLocalModelSnippet(configPath, first, "http://127.0.0.1:8081/v1", "key-a", firstDraft.Snippet, addAsNew: false);
        var secondId = service.SaveLocalModelSnippet(configPath, second, "http://127.0.0.1:8082/v1", "key-b", secondDraft.Snippet, addAsNew: false);

        var config = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(configPath))!;
        var providers = config["provider"]!;

        Assert.Equal(firstDraft.FullId, firstId);
        Assert.Equal(secondDraft.FullId, secondId);
        Assert.Equal("http://127.0.0.1:8081/v1", providers[firstDraft.ProviderId]?["options"]?["baseURL"]?.ToString());
        Assert.Equal("http://127.0.0.1:8082/v1", providers[secondDraft.ProviderId]?["options"]?["baseURL"]?.ToString());
        Assert.NotNull(providers[firstDraft.ProviderId]?["models"]?[firstDraft.ModelId]);
        Assert.NotNull(providers[secondDraft.ProviderId]?["models"]?[secondDraft.ModelId]);
    }


    [Fact]
    public void OpenCodeLocalModelsWithSameBasenameUseDistinctProviders()
    {
        var root = CreateTempRoot();
        var service = new OpenCodeConfigService(root);
        var configPath = Path.Combine(root, "opencode.jsonc");
        var first = new ModelRecord("model-a", "First Model", Path.Combine(root, "models", "first", "model.gguf"), OwnershipKind.AppOwned, "{}", DateTimeOffset.UtcNow);
        var second = new ModelRecord("model-b", "Second Model", Path.Combine(root, "models", "second", "model.gguf"), OwnershipKind.AppOwned, "{}", DateTimeOffset.UtcNow);

        var firstDraft = service.CreateLocalModelDraft(configPath, first, "http://127.0.0.1:8081/v1", "key-a", 8192, 4096);
        var secondDraft = service.CreateLocalModelDraft(configPath, second, "http://127.0.0.1:8082/v1", "key-b", 8192, 4096);
        service.SaveLocalModelSnippet(configPath, first, "http://127.0.0.1:8081/v1", "key-a", firstDraft.Snippet, addAsNew: false);
        service.SaveLocalModelSnippet(configPath, second, "http://127.0.0.1:8082/v1", "key-b", secondDraft.Snippet, addAsNew: false);

        var config = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(configPath))!;
        var providers = config["provider"]!;

        Assert.NotEqual(firstDraft.ProviderId, secondDraft.ProviderId);
        Assert.NotEqual(firstDraft.ModelId, secondDraft.ModelId);
        Assert.StartsWith("local-llm-console-model-", firstDraft.ProviderId, StringComparison.Ordinal);
        Assert.StartsWith("local-llm-console-model-", secondDraft.ProviderId, StringComparison.Ordinal);
        Assert.Equal("http://127.0.0.1:8081/v1", providers[firstDraft.ProviderId]?["options"]?["baseURL"]?.ToString());
        Assert.Equal("http://127.0.0.1:8082/v1", providers[secondDraft.ProviderId]?["options"]?["baseURL"]?.ToString());
        Assert.NotNull(providers[firstDraft.ProviderId]?["models"]?[firstDraft.ModelId]);
        Assert.NotNull(providers[secondDraft.ProviderId]?["models"]?[secondDraft.ModelId]);
    }


    [Fact]
    public void OpenCodeGatewayModelsShareOneAutoLoadProvider()
    {
        var root = CreateTempRoot();
        var service = new OpenCodeConfigService(root);
        var configPath = Path.Combine(root, "opencode.jsonc");
        var first = new ModelRecord("model-a", "First Model", Path.Combine(root, "models", "first-model.gguf"), OwnershipKind.AppOwned, "{}", DateTimeOffset.UtcNow);
        var second = new ModelRecord("model-b", "Second Model", Path.Combine(root, "models", "second-model.gguf"), OwnershipKind.AppOwned, "{}", DateTimeOffset.UtcNow);

        var firstId = service.AddOrUpdateLocalModel(configPath, first, "http://127.0.0.1:8082/v1", "gateway-key", 8192, 0, useGatewayProvider: true);
        var secondId = service.AddOrUpdateLocalModel(configPath, second, "http://127.0.0.1:8082/v1", "gateway-key", 16384, 8192, useGatewayProvider: true);

        var config = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(configPath))!;
        var provider = config["provider"]?[OpenCodeConfigService.LocalProviderId];

        Assert.StartsWith($"{OpenCodeConfigService.LocalProviderId}/", firstId, StringComparison.Ordinal);
        Assert.StartsWith($"{OpenCodeConfigService.LocalProviderId}/", secondId, StringComparison.Ordinal);
        Assert.Equal("http://127.0.0.1:8082/v1", provider?["options"]?["baseURL"]?.ToString());
        Assert.Equal("gateway-key", provider?["options"]?["apiKey"]?.ToString());
        Assert.Equal("8192", provider?["models"]?[firstId.Split('/')[1]]?["limit"]?["context"]?.ToString());
        Assert.Equal(OpenCodeConfigService.DefaultOutputLimit.ToString(), provider?["models"]?[firstId.Split('/')[1]]?["limit"]?["output"]?.ToString());
        Assert.Equal("16384", provider?["models"]?[secondId.Split('/')[1]]?["limit"]?["context"]?.ToString());
        Assert.Equal("8192", provider?["models"]?[secondId.Split('/')[1]]?["limit"]?["output"]?.ToString());
    }


    [Fact]
    public void OpenCodeModelSyncServiceOwnsEndpointChoiceAndLimits()
    {
        var root = CreateTempRoot();
        var openCode = new OpenCodeConfigService(root);
        var sync = new OpenCodeModelSyncService(openCode);
        var configPath = Path.Combine(root, "opencode.jsonc");
        var settings = AppSettings.CreateDefault(root) with
        {
            AutoLoadGatewayPort = 8082,
            ContextSize = 0,
            MaxTokens = -1,
            ModelApiKey = "abcdefghijklmnopqrstuvwxyz123456"
        };
        var launchSettings = settings with { Port = 8084 };
        var model = new ModelRecord("model-a", "First Model", Path.Combine(root, "models", "first-model.gguf"), OwnershipKind.AppOwned, "{}", DateTimeOffset.UtcNow);

        var limits = sync.ResolveLimits(launchSettings, ModelCapabilityService.Empty() with { ContextLength = 32768 });
        var gatewayDraft = sync.CreateDraft(new OpenCodeLocalModelDraftRequest(configPath, model, settings, launchSettings, limits, UseGatewayProvider: true));
        var directDraft = sync.CreateDraft(new OpenCodeLocalModelDraftRequest(configPath, model, settings, launchSettings, limits, UseGatewayProvider: false));

        Assert.Equal(32768, limits.ContextSize);
        Assert.Equal(8192, limits.OutputLimit);
        Assert.Contains("http://127.0.0.1:8082/v1", gatewayDraft.Snippet, StringComparison.Ordinal);
        Assert.Contains("http://127.0.0.1:8084/v1", directDraft.Snippet, StringComparison.Ordinal);
    }

    [Fact]
    public async Task OpenCodeLocalModelWorkflowMarksVisionModelsForOpenCode()
    {
        var root = CreateTempRoot();
        var modelDir = Path.Combine(root, "models");
        Directory.CreateDirectory(modelDir);
        var modelPath = Path.Combine(modelDir, "qwen3-vl.gguf");
        File.WriteAllText(modelPath, "model");
        File.WriteAllText(Path.Combine(modelDir, "mmproj-qwen3-vl.gguf"), "projector");
        var openCode = new OpenCodeConfigService(root);
        var sync = new OpenCodeModelSyncService(openCode);
        var workflow = new OpenCodeLocalModelWorkflowService(sync);
        var files = new OpenCodeFileSet(Path.Combine(root, "opencode.jsonc"), Path.Combine(root, "agent"));
        var settings = AppSettings.CreateDefault(root) with
        {
            AutoLoadGatewayEnabled = true,
            AutoLoadGatewayPort = 8082,
            ContextSize = 8192,
            MaxTokens = 4096,
            ModelApiKey = new string('v', 32)
        };
        var model = new ModelRecord("qwen3-vl", "Qwen3 VL", modelPath, OwnershipKind.AppOwned, "{}", DateTimeOffset.UtcNow);

        var limits = await workflow.ResolveLimitsAsync(
            model,
            settings,
            (_, _) => throw new InvalidOperationException("Vision projector detection should not require metadata when context is already known."),
            TestContext.Current.CancellationToken);
        var draft = await workflow.CreateDraftAsync(
            new OpenCodeLocalModelDraftBuildRequest(files, model, settings, settings, UseGatewayProvider: true),
            (_, _) => throw new InvalidOperationException("Vision projector detection should not require metadata when context is already known."),
            TestContext.Current.CancellationToken);
        var synced = sync.SyncGatewayProvider(
            files.ConfigPath,
            settings,
            [new OpenCodeGatewayModelSyncItem(model, settings, limits)]);

        var draftModel = System.Text.Json.Nodes.JsonNode.Parse(draft.Snippet)!["provider"]![draft.ProviderId]!["models"]![draft.ModelId]!;
        var config = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(files.ConfigPath))!;
        var syncedModel = config["provider"]?[OpenCodeConfigService.LocalProviderId]?["models"]?[OpenCodeConfigService.LocalModelIdFor(model)];
        var inputModalities = draftModel["modalities"]?["input"]?.AsArray().Select(node => node?.ToString()).ToArray()
            ?? [];

        Assert.True(limits.SupportsVision);
        Assert.Equal(1, synced);
        Assert.Equal("true", draftModel["attachment"]?.ToString());
        Assert.Contains("image", inputModalities);
        Assert.Equal("true", syncedModel?["attachment"]?.ToString());

        var explicitDir = Path.Combine(root, "explicit-vision");
        Directory.CreateDirectory(explicitDir);
        var explicitModelPath = Path.Combine(explicitDir, "gemma-main.gguf");
        var explicitVisionHead = Path.Combine(explicitDir, "gemma-mtp-vision.gguf");
        File.WriteAllText(explicitModelPath, "model");
        File.WriteAllText(explicitVisionHead, "vision");
        var explicitModel = model with { Id = "gemma-main", Name = "Gemma Main", ModelPath = explicitModelPath };
        var explicitLimits = await workflow.ResolveLimitsAsync(
            explicitModel,
            settings with { VisionProjectorPath = explicitVisionHead },
            (_, _) => throw new InvalidOperationException("Explicit vision head detection should not require metadata when context is already known."),
            TestContext.Current.CancellationToken);

        Assert.True(explicitLimits.SupportsVision);

        var embeddedLimits = await workflow.ResolveLimitsAsync(
            explicitModel,
            settings with { VisionProjectorPath = VisionProjectorSelection.EmbeddedToken },
            (_, _) => throw new InvalidOperationException("Embedded vision selection should not require metadata when context is already known."),
            TestContext.Current.CancellationToken);

        Assert.True(embeddedLimits.SupportsVision);
    }

    [Fact]
    public async Task OpenCodeLocalModelWorkflowServiceResolvesLaunchSettingsLimitsAndDrafts()
    {
        var source = ReadMainWindowSources();
        var root = CreateTempRoot();
        var openCode = new OpenCodeConfigService(root);
        var sync = new OpenCodeModelSyncService(openCode);
        var workflow = new OpenCodeLocalModelWorkflowService(sync);
        var files = new OpenCodeFileSet(Path.Combine(root, "opencode.jsonc"), Path.Combine(root, "agent"));
        openCode.EnsureFiles(files);
        var model = new ModelRecord("model-a", "First Model", Path.Combine(root, "models", "first-model.gguf"), OwnershipKind.AppOwned, "{}", DateTimeOffset.UtcNow);
        var settings = AppSettings.CreateDefault(root) with
        {
            AutoLoadGatewayEnabled = true,
            AutoLoadGatewayPort = 8082,
            Port = 8081,
            ContextSize = 0,
            MaxTokens = -1,
            ModelApiKey = "abcdefghijklmnopqrstuvwxyz123456"
        };
        var profile = ModelLaunchSettings.FromAppSettings(settings) with
        {
            Port = 8091,
            ContextSize = 8192,
            MaxTokens = 2048
        };
        var runningSettings = settings with { Port = 8101, ContextSize = 4096, MaxTokens = 1024 };
        var running = new LoadedModelSessionSnapshot(
            "session-model-a",
            model.Id,
            model.Name,
            "runtime-a",
            "Runtime A",
            RuntimeMode.Native,
            RuntimeBackend.Cpu,
            runningSettings,
            "",
            DateTimeOffset.UtcNow,
            "",
            0,
            LoadedModelSessionStatus.Running,
            IsRunning: true,
            IsSelected: true);
        var profileReads = 0;
        var ensuredPorts = new List<int>();

        var fromRunning = await workflow.ResolveLaunchSettingsAsync(new OpenCodeLocalModelLaunchSettingsRequest(
            model,
            settings,
            running,
            (_, _) =>
            {
                profileReads++;
                return ValueTask.FromResult<ModelLaunchSettings?>(profile);
            },
            (launchSettings, _) =>
            {
                ensuredPorts.Add(launchSettings.Port);
                return ValueTask.FromResult(launchSettings);
            }),
            TestContext.Current.CancellationToken);
        var fromProfile = await workflow.ResolveLaunchSettingsAsync(new OpenCodeLocalModelLaunchSettingsRequest(
            model,
            settings,
            LoadedSession: null,
            (_, _) =>
            {
                profileReads++;
                return ValueTask.FromResult<ModelLaunchSettings?>(profile);
            },
            (launchSettings, _) =>
            {
                ensuredPorts.Add(launchSettings.Port);
                return ValueTask.FromResult(launchSettings);
            }),
            TestContext.Current.CancellationToken);
        var capabilitiesRead = 0;
        var limitsFromCapabilities = await workflow.ResolveLimitsAsync(
            model,
            settings,
            (_, _) =>
            {
                capabilitiesRead++;
                return ValueTask.FromResult(ModelCapabilityService.Empty() with { ContextLength = 32768 });
            },
            TestContext.Current.CancellationToken);
        var limitsFromSettings = await workflow.ResolveLimitsAsync(
            model,
            profile.ApplyTo(settings),
            (_, _) =>
            {
                capabilitiesRead++;
                return ValueTask.FromResult(ModelCapabilityService.Empty() with { ContextLength = 65536 });
            },
            TestContext.Current.CancellationToken);
        var draft = await workflow.CreateDraftAsync(
            new OpenCodeLocalModelDraftBuildRequest(files, model, settings, fromProfile, UseGatewayProvider: true),
            (_, _) => ValueTask.FromResult(ModelCapabilityService.Empty() with { ContextLength = 65536 }),
            TestContext.Current.CancellationToken);
        var fullId = workflow.SaveSnippet(new OpenCodeLocalModelSaveRequest(
            files.ConfigPath,
            model,
            settings,
            fromProfile,
            draft.Snippet,
            AddAsNew: false,
            UseGatewayProvider: true));
        var save = workflow.Save(new OpenCodeLocalModelSaveWorkflowRequest(
            files,
            model,
            settings,
            fromProfile,
            draft.Snippet,
            AddAsNew: true,
            UseGatewayProvider: true));

        Assert.Equal(8101, fromRunning.Port);
        Assert.Equal(8091, fromProfile.Port);
        Assert.Equal([8101, 8091], ensuredPorts);
        Assert.Equal(1, profileReads);
        Assert.Equal(32768, limitsFromCapabilities.ContextSize);
        Assert.Equal(8192, limitsFromCapabilities.OutputLimit);
        Assert.Equal(8192, limitsFromSettings.ContextSize);
        Assert.Equal(2048, limitsFromSettings.OutputLimit);
        Assert.Equal(1, capabilitiesRead);
        Assert.Contains("http://127.0.0.1:8082/v1", draft.Snippet, StringComparison.Ordinal);
        Assert.Contains("limit", draft.Snippet, StringComparison.Ordinal);
        Assert.Equal($"{OpenCodeConfigService.LocalProviderId}/{OpenCodeConfigService.LocalModelIdFor(model)}", fullId);
        Assert.Equal($"{OpenCodeConfigService.LocalProviderId}/{OpenCodeConfigService.LocalModelIdFor(model)}-2", save.FullId);
        Assert.Equal($"Added OpenCode model {save.FullId}.", save.StatusMessage);
        Assert.Contains("_coreServices.OpenCodeServices.OpenCodeLocalModelApplication.SaveSnippetAsync(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("new OpenCodeLocalModelSaveWorkflowRequest(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("SetStatus(addAsNew ? $\"Added OpenCode model", source, StringComparison.Ordinal);
    }


    [Fact]
    public void OpenCodeModelSyncServiceSyncsGatewayProvider()
    {
        var root = CreateTempRoot();
        var openCode = new OpenCodeConfigService(root);
        var sync = new OpenCodeModelSyncService(openCode);
        var configPath = Path.Combine(root, "opencode.jsonc");
        var settings = AppSettings.CreateDefault(root) with
        {
            AutoLoadGatewayPort = 8082,
            ModelApiKey = "abcdefghijklmnopqrstuvwxyz123456"
        };
        var first = new ModelRecord("model-a", "First Model", Path.Combine(root, "models", "first-model.gguf"), OwnershipKind.AppOwned, "{}", DateTimeOffset.UtcNow);
        var second = new ModelRecord("model-b", "Second Model", Path.Combine(root, "models", "second-model.gguf"), OwnershipKind.AppOwned, "{}", DateTimeOffset.UtcNow);

        var synced = sync.SyncGatewayProvider(
            configPath,
            settings,
            [
                new OpenCodeGatewayModelSyncItem(first, settings with { Port = 8084 }, new OpenCodeModelLimits(8192, 4096)),
                new OpenCodeGatewayModelSyncItem(second, settings with { Port = 8085 }, new OpenCodeModelLimits(16384, 8192))
            ]);
        var health = sync.InspectGatewayProvider(configPath, [first, second], settings);

        var config = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(configPath))!;
        var provider = config["provider"]?[OpenCodeConfigService.LocalProviderId];
        Assert.Equal(2, synced);
        Assert.True(health.Ok);
        Assert.Equal("http://127.0.0.1:8082/v1", provider?["options"]?["baseURL"]?.ToString());
        Assert.Equal("abcdefghijklmnopqrstuvwxyz123456", provider?["options"]?["apiKey"]?.ToString());
        Assert.Equal("8192", provider?["models"]?[OpenCodeConfigService.LocalModelIdFor(first)]?["limit"]?["context"]?.ToString());
        Assert.Equal("8192", provider?["models"]?[OpenCodeConfigService.LocalModelIdFor(second)]?["limit"]?["output"]?.ToString());
    }


    [Fact]
    public void OpenCodePageWorkflowServiceBuildsChoicesAndHealthStates()
    {
        var root = CreateTempRoot();
        var openCode = new OpenCodeConfigService(root);
        var sync = new OpenCodeModelSyncService(openCode);
        var workflow = new OpenCodePageWorkflowService(openCode, sync);
        var localModelWorkflow = new OpenCodeLocalModelWorkflowService(sync);
        var files = new OpenCodeFileSet(Path.Combine(root, "opencode.jsonc"), Path.Combine(root, "agent"));
        openCode.EnsureFiles(files);
        var settings = AppSettings.CreateDefault(root) with
        {
            AutoLoadGatewayEnabled = true,
            AutoLoadGatewayPort = 8082
        };
        var qwen = new ModelRecord("qwen", "Qwen", Path.Combine(root, "qwen.gguf"), OwnershipKind.External, "{}", DateTimeOffset.UtcNow);
        var llama = new ModelRecord("llama", "Llama", Path.Combine(root, "llama.gguf"), OwnershipKind.External, "{}", DateTimeOffset.UtcNow);
        var draft = sync.CreateDraft(new OpenCodeLocalModelDraftRequest(files.ConfigPath, qwen, settings, settings, new OpenCodeModelLimits(8192, 4096), UseGatewayProvider: true));
        var fullId = sync.SaveLocalModelSnippet(new OpenCodeLocalModelSaveRequest(files.ConfigPath, qwen, settings, settings, draft.Snippet, AddAsNew: false, UseGatewayProvider: true));

        var choices = workflow.BuildChoices(files, [qwen, llama], fullId, "");
        var sameConfig = localModelWorkflow.AnalyzeLocalModelSnippet(files.ConfigPath, qwen, draft.Snippet, useGatewayProvider: true);
        var noModel = localModelWorkflow.NoLocalModelSelected();
        var healthyGateway = workflow.GatewayHealth(files.ConfigPath, [qwen], settings);
        var disabledGateway = workflow.GatewayHealth(files.ConfigPath, [qwen], settings with { AutoLoadGatewayEnabled = false });
        var invalidConfigPath = Path.Combine(root, "invalid-opencode.jsonc");
        File.WriteAllText(invalidConfigPath, "{");
        var unreadableGateway = workflow.GatewayHealth(invalidConfigPath, [qwen], settings);

        Assert.Equal(["Llama", "Qwen"], choices.LocalModels.Select(model => model.Name).ToArray());
        Assert.Equal(fullId, choices.SelectedModel?.FullId);
        Assert.True(choices.Models.Last().IsAddNew);
        Assert.True(choices.SelectedAgent?.IsAddNew);
        Assert.Contains("Already added to OpenCode automatically", sameConfig.Status, StringComparison.Ordinal);
        Assert.False(sameConfig.AddVisible);
        Assert.False(sameConfig.UpdateVisible);
        Assert.Equal("Choose a local model to add.", noModel.Status);
        Assert.True(noModel.AddVisible);
        Assert.False(noModel.AddEnabled);
        Assert.False(healthyGateway.IsWarning);
        Assert.Contains("healthy", healthyGateway.Summary, StringComparison.OrdinalIgnoreCase);
        Assert.False(disabledGateway.IsWarning);
        Assert.Contains("auto-load gateway is disabled", disabledGateway.Summary, StringComparison.OrdinalIgnoreCase);
        Assert.True(unreadableGateway.IsWarning);
        Assert.Equal("OpenCode sync: config cannot be read.", unreadableGateway.Summary);
    }

    [Fact]
    public void OpenCodeModelWorkflowServiceManagesModelSnippetWorkflow()
    {
        var source = ReadMainWindowSources();
        var root = CreateTempRoot();
        var openCode = new OpenCodeConfigService(root);
        var sync = new OpenCodeModelSyncService(openCode);
        var pageWorkflow = new OpenCodePageWorkflowService(openCode, sync);
        var modelWorkflow = new OpenCodeModelWorkflowService(openCode);
        var files = new OpenCodeFileSet(Path.Combine(root, "opencode.jsonc"), Path.Combine(root, "agent"));
        openCode.EnsureFiles(files);
        var settings = AppSettings.CreateDefault(root) with
        {
            AutoLoadGatewayEnabled = true,
            AutoLoadGatewayPort = 8082,
            ModelApiKey = "abcdefghijklmnopqrstuvwxyz123456"
        };
        var qwen = new ModelRecord("qwen", "Qwen", Path.Combine(root, "qwen.gguf"), OwnershipKind.External, "{}", DateTimeOffset.UtcNow);
        var draft = sync.CreateDraft(new OpenCodeLocalModelDraftRequest(files.ConfigPath, qwen, settings, settings, new OpenCodeModelLimits(8192, 4096), UseGatewayProvider: true));
        var fullId = sync.SaveLocalModelSnippet(new OpenCodeLocalModelSaveRequest(files.ConfigPath, qwen, settings, settings, draft.Snippet, AddAsNew: false, UseGatewayProvider: true));
        var model = pageWorkflow.BuildChoices(files, [], fullId, "").SelectedModel!;

        var savedState = modelWorkflow.ExistingModelEditorState(model, snippetValid: true, matchesSaved: true);
        var dirtyState = modelWorkflow.ExistingModelEditorState(model, snippetValid: true, matchesSaved: false);
        var addNewModel = pageWorkflow.BuildChoices(files, [], "", "").Models.Last();
        var addNewState = modelWorkflow.ExistingModelEditorState(addNewModel, snippetValid: false, matchesSaved: true);
        var snippet = modelWorkflow.ReadModelSnippet(files, model);
        var updatedSnippet = snippet.Replace("\"name\": \"Qwen\"", "\"name\": \"Qwen Updated\"", StringComparison.Ordinal);
        var application = new OpenCodeModelApplicationService(modelWorkflow);
        var editorSavedState = application.EditorState(new OpenCodeModelEditorStateApplicationRequest(model, snippet, snippet));
        var editorDirtyState = application.EditorState(new OpenCodeModelEditorStateApplicationRequest(model, updatedSnippet, snippet));
        var editorAddState = application.EditorState(new OpenCodeModelEditorStateApplicationRequest(addNewModel, draft.Snippet, ""));
        var invalidSaveAdmission = modelWorkflow.SaveAdmission(null);
        var invalidDeleteAdmission = modelWorkflow.DeleteAdmission(addNewModel);
        var saveAdmission = modelWorkflow.SaveAdmission(model);
        var deleteAdmission = modelWorkflow.DeleteAdmission(model);
        var save = modelWorkflow.SaveModelSnippet(files, model, updatedSnippet);
        var readBack = modelWorkflow.ReadModelSnippet(files, model);
        var delete = modelWorkflow.DeleteModel(files, model);

        Assert.Equal("Saved", savedState.SaveContent);
        Assert.False(savedState.SaveEnabled);
        Assert.True(savedState.DeleteEnabled);
        Assert.Equal("Update Config", dirtyState.SaveContent);
        Assert.True(dirtyState.SaveEnabled);
        Assert.False(addNewState.SaveVisible);
        Assert.False(editorSavedState.ExistingModelState.SaveEnabled);
        Assert.Equal("Saved", editorSavedState.ExistingModelState.SaveContent);
        Assert.True(editorDirtyState.ExistingModelState.SaveEnabled);
        Assert.False(editorDirtyState.RefreshLocalModelAddState);
        Assert.False(editorAddState.ExistingModelState.SaveVisible);
        Assert.True(editorAddState.RefreshLocalModelAddState);
        Assert.False(invalidSaveAdmission.CanRun);
        Assert.Equal("Choose an OpenCode model first.", invalidSaveAdmission.StatusMessage);
        Assert.False(invalidDeleteAdmission.CanRun);
        Assert.True(saveAdmission.CanRun);
        Assert.Null(saveAdmission.Confirmation);
        Assert.True(deleteAdmission.CanRun);
        Assert.Equal("Delete OpenCode model", deleteAdmission.Confirmation?.Title);
        Assert.Contains(model.Label, deleteAdmission.Confirmation?.Message, StringComparison.Ordinal);
        Assert.True(modelWorkflow.SnippetsEquivalent(readBack, updatedSnippet));
        Assert.Equal($"OpenCode model {fullId} is already saved.", modelWorkflow.AlreadySavedStatus(model));
        Assert.Equal(fullId, save.FullId);
        Assert.Equal($"Saved OpenCode model {fullId}.", save.StatusMessage);
        Assert.Contains("Qwen Updated", readBack, StringComparison.Ordinal);
        Assert.Equal($"Deleted OpenCode model {fullId}.", delete.StatusMessage);
        Assert.DoesNotContain(pageWorkflow.BuildChoices(files, [], "", "").Models, candidate => candidate.FullId == fullId);
        Assert.Contains("_coreServices.OpenCodeServices.OpenCodeModelApplication.EditorState", source, StringComparison.Ordinal);
        Assert.Contains("OpenCodeModelEditorStateApplicationRequest", source, StringComparison.Ordinal);
        Assert.DoesNotContain("_coreServices.OpenCodeServices.OpenCodeModelApplication.SnippetsEquivalent", source, StringComparison.Ordinal);
        Assert.DoesNotContain("_openCodeServices", source, StringComparison.Ordinal);
        Assert.Contains("SaveAdmission(OpenCodeModelEntry? model)", File.ReadAllText(FindRepositoryFile("src", "LocalLlmConsole.App", "Services", "OpenCodeModelWorkflowService.cs")), StringComparison.Ordinal);
        Assert.DoesNotContain("SetStatus($\"OpenCode model", source, StringComparison.Ordinal);
        Assert.DoesNotContain("SetStatus($\"Saved OpenCode model", source, StringComparison.Ordinal);
        Assert.DoesNotContain("SetStatus($\"Deleted OpenCode model", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Choose an OpenCode model first.", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Delete this OpenCode model config?", source, StringComparison.Ordinal);
    }

    [Fact]
    public async Task OpenCodeModelApplicationServiceRunsSaveAlreadySavedAndDeleteCommands()
    {
        var root = CreateTempRoot();
        var openCode = new OpenCodeConfigService(root);
        var sync = new OpenCodeModelSyncService(openCode);
        var pageWorkflow = new OpenCodePageWorkflowService(openCode, sync);
        var workflow = new OpenCodeModelWorkflowService(openCode);
        var application = new OpenCodeModelApplicationService(workflow);
        var files = new OpenCodeFileSet(Path.Combine(root, "opencode.jsonc"), Path.Combine(root, "agent"));
        openCode.EnsureFiles(files);
        var settings = AppSettings.CreateDefault(root) with
        {
            AutoLoadGatewayEnabled = true,
            AutoLoadGatewayPort = 8082,
            ModelApiKey = "abcdefghijklmnopqrstuvwxyz123456"
        };
        var modelRecord = new ModelRecord("qwen", "Qwen", Path.Combine(root, "qwen.gguf"), OwnershipKind.External, "{}", DateTimeOffset.UtcNow);
        var draft = sync.CreateDraft(new OpenCodeLocalModelDraftRequest(files.ConfigPath, modelRecord, settings, settings, new OpenCodeModelLimits(8192, 4096), UseGatewayProvider: true));
        var fullId = sync.SaveLocalModelSnippet(new OpenCodeLocalModelSaveRequest(files.ConfigPath, modelRecord, settings, settings, draft.Snippet, AddAsNew: false, UseGatewayProvider: true));
        var model = pageWorkflow.BuildChoices(files, [], fullId, "").SelectedModel!;
        var savedSnippet = workflow.ReadModelSnippet(files, model);
        var updatedSnippet = savedSnippet.Replace("\"name\": \"Qwen\"", "\"name\": \"Qwen Updated\"", StringComparison.Ordinal);
        var calls = new List<string>();
        var resultActions = new OpenCodeModelCommandApplicationActions(
            () => calls.Add("editor"),
            preferredModelId =>
            {
                calls.Add($"refresh-model:{preferredModelId}");
                return Task.CompletedTask;
            },
            () =>
            {
                calls.Add("refresh");
                return Task.CompletedTask;
            },
            status => calls.Add($"status:{status}"));

        var missing = await application.SaveSnippetAsync(
            new OpenCodeModelSaveApplicationRequest(files, null, "{}", ""),
            SaveActions(confirmDelete: true));
        var alreadySaved = await application.SaveSnippetAsync(
            new OpenCodeModelSaveApplicationRequest(files, model, savedSnippet, savedSnippet),
            SaveActions(confirmDelete: true));
        var saved = await application.SaveSnippetAsync(
            new OpenCodeModelSaveApplicationRequest(files, model, updatedSnippet, savedSnippet),
            SaveActions(confirmDelete: true));
        var deleteDeclined = await application.DeleteAsync(
            new OpenCodeModelDeleteApplicationRequest(files, model),
            DeleteActions(confirmDelete: false));
        var deleted = await application.DeleteAsync(
            new OpenCodeModelDeleteApplicationRequest(files, model),
            DeleteActions(confirmDelete: true));

        Assert.Equal(OpenCodeModelCommandOutcome.Rejected, missing);
        Assert.Equal(OpenCodeModelCommandOutcome.AlreadySaved, alreadySaved);
        Assert.Equal(OpenCodeModelCommandOutcome.Saved, saved);
        Assert.Equal(OpenCodeModelCommandOutcome.Rejected, deleteDeclined);
        Assert.Equal(OpenCodeModelCommandOutcome.Deleted, deleted);
        Assert.Equal(
            [
                "status:Choose an OpenCode model first.",
                "editor",
                $"status:OpenCode model {fullId} is already saved.",
                "busy:Saving OpenCode model snippet...",
                $"refresh-model:{fullId}",
                $"status:Saved OpenCode model {fullId}.",
                "confirm:Delete OpenCode model:False",
                "confirm:Delete OpenCode model:True",
                "busy:Deleting OpenCode model config...",
                "refresh",
                $"status:Deleted OpenCode model {fullId}."
            ],
            calls);
        Assert.DoesNotContain(pageWorkflow.BuildChoices(files, [], "", "").Models, candidate => candidate.FullId == fullId);

        OpenCodeModelSaveApplicationActions SaveActions(bool confirmDelete)
            => new(
                RunBusy,
                Confirm(confirmDelete),
                resultActions);

        OpenCodeModelDeleteApplicationActions DeleteActions(bool confirmDelete)
            => new(
                RunBusy,
                Confirm(confirmDelete),
                resultActions);

        async Task RunBusy(string message, Func<Task> action)
        {
            calls.Add($"busy:{message}");
            await action();
        }

        Func<OpenCodeCommandConfirmation, bool> Confirm(bool result)
            => confirmation =>
            {
                calls.Add($"confirm:{confirmation.Title}:{result}");
                return result;
            };
    }

    [Fact]
    public async Task OpenCodeModelApplicationServiceLoadsSelectedModelsAndAddNewDrafts()
    {
        var root = CreateTempRoot();
        var openCode = new OpenCodeConfigService(root);
        var sync = new OpenCodeModelSyncService(openCode);
        var pageWorkflow = new OpenCodePageWorkflowService(openCode, sync);
        var workflow = new OpenCodeModelWorkflowService(openCode);
        var application = new OpenCodeModelApplicationService(workflow);
        var files = new OpenCodeFileSet(Path.Combine(root, "opencode.jsonc"), Path.Combine(root, "agent"));
        openCode.EnsureFiles(files);
        var settings = AppSettings.CreateDefault(root) with
        {
            AutoLoadGatewayEnabled = true,
            AutoLoadGatewayPort = 8082,
            ModelApiKey = "abcdefghijklmnopqrstuvwxyz123456"
        };
        var modelRecord = new ModelRecord("qwen", "Qwen", Path.Combine(root, "qwen.gguf"), OwnershipKind.External, "{}", DateTimeOffset.UtcNow);
        var draft = sync.CreateDraft(new OpenCodeLocalModelDraftRequest(files.ConfigPath, modelRecord, settings, settings, new OpenCodeModelLimits(8192, 4096), UseGatewayProvider: true));
        var fullId = sync.SaveLocalModelSnippet(new OpenCodeLocalModelSaveRequest(files.ConfigPath, modelRecord, settings, settings, draft.Snippet, AddAsNew: false, UseGatewayProvider: true));
        var choices = pageWorkflow.BuildChoices(files, [], fullId, "");
        var model = choices.SelectedModel!;
        var addNew = choices.Models.Last();
        var missing = new OpenCodeModelEntry("missing/model", "missing", "model", "Missing Model");
        var calls = new List<string>();

        var noEditor = await application.LoadSelectedAsync(
            new OpenCodeModelLoadApplicationRequest(files, model, HasSnippetEditor: false),
            LoadActions());
        var adding = await application.LoadSelectedAsync(
            new OpenCodeModelLoadApplicationRequest(files, addNew, HasSnippetEditor: true),
            LoadActions());
        var loaded = await application.LoadSelectedAsync(
            new OpenCodeModelLoadApplicationRequest(files, model, HasSnippetEditor: true),
            LoadActions());
        File.WriteAllText(files.ConfigPath, "{ invalid json");
        var failed = await application.LoadSelectedAsync(
            new OpenCodeModelLoadApplicationRequest(files, missing, HasSnippetEditor: true),
            LoadActions());

        Assert.Equal(OpenCodeModelLoadOutcome.NoEditor, noEditor);
        Assert.Equal(OpenCodeModelLoadOutcome.Adding, adding);
        Assert.Equal(OpenCodeModelLoadOutcome.Loaded, loaded);
        Assert.Equal(OpenCodeModelLoadOutcome.Failed, failed);
        Assert.Equal(
            [
                "model:add-panel:False",
                "model:delete:True",
                "model:add-panel:True",
                "model:delete:False",
                "model:clear",
                "model:editor",
                "model:draft",
                "model:add-panel:False",
                "model:delete:True",
                "model:saved",
                "model:text",
                "model:editor",
                "model:add-panel:False",
                "model:delete:True",
                "model:clear",
                "model:text",
                "model:editor",
                "status"
            ],
            calls);

        OpenCodeModelLoadSelectedApplicationActions LoadActions()
            => new(
                () =>
                {
                    calls.Add("model:draft");
                    return Task.CompletedTask;
                },
                new OpenCodeModelLoadApplicationActions(
                    visible => calls.Add($"model:add-panel:{visible}"),
                    visible => calls.Add($"model:delete:{visible}"),
                    () => calls.Add("model:clear"),
                    _ => calls.Add("model:saved"),
                    _ => calls.Add("model:text"),
                    () => calls.Add("model:editor"),
                    _ => calls.Add("status")));
    }

    [Fact]
    public void OpenCodeAgentWorkflowServiceManagesAgentWorkflow()
    {
        var source = ReadMainWindowSources();
        var root = CreateTempRoot();
        var openCode = new OpenCodeConfigService(root);
        var sync = new OpenCodeModelSyncService(openCode);
        var pageWorkflow = new OpenCodePageWorkflowService(openCode, sync);
        var agentWorkflow = new OpenCodeAgentWorkflowService(openCode);
        var files = new OpenCodeFileSet(Path.Combine(root, "opencode.jsonc"), Path.Combine(root, "agent"));
        openCode.EnsureFiles(files);
        var selectedModel = new OpenCodeModelEntry("local/qwen", "local", "qwen", "Qwen");

        var invalidDraft = agentWorkflow.AnalyzeNewAgentDraft(" ", markdown: false, []);
        var configDraft = agentWorkflow.AnalyzeNewAgentDraft("Build Agent", markdown: false, []);
        var create = agentWorkflow.CreateAgent(files, configDraft, selectedModel);
        var created = create.Agent;
        var choices = pageWorkflow.BuildChoices(files, [], "", created.Id);
        var duplicate = agentWorkflow.AnalyzeNewAgentDraft("Build Agent", markdown: false, choices.Agents);
        var invalidCreateAdmission = agentWorkflow.CreateAdmission(" ", markdown: false, choices.Agents);
        var duplicateCreateAdmission = agentWorkflow.CreateAdmission("Build Agent", markdown: false, choices.Agents);
        var invalidSaveAdmission = agentWorkflow.SaveAdmission(null);
        var invalidDeleteAdmission = agentWorkflow.DeleteAdmission(choices.Agents.Last());
        var saveAdmission = agentWorkflow.SaveAdmission(created);
        var deleteAdmission = agentWorkflow.DeleteAdmission(created);
        var selectedState = agentWorkflow.AgentEditorState(created);
        var addState = agentWorkflow.AgentEditorState(choices.Agents.Last());
        var snippet = agentWorkflow.ReadAgentSnippet(files, created);

        Assert.False(invalidDraft.IsValid);
        Assert.Equal("Name the new agent first.", invalidDraft.ValidationMessage);
        Assert.True(configDraft.IsValid);
        Assert.Equal("build-agent", configDraft.SafeName);
        Assert.Equal(OpenCodeAgentKind.Config, configDraft.Kind);
        Assert.Equal(created.Id, choices.SelectedAgent?.Id);
        Assert.Equal(created, duplicate.Duplicate);
        Assert.False(invalidCreateAdmission.CanRun);
        Assert.Equal("Name the new agent first.", invalidCreateAdmission.StatusMessage);
        Assert.True(duplicateCreateAdmission.CanRun);
        Assert.Equal("OpenCode agent", duplicateCreateAdmission.Confirmation?.Title);
        Assert.Contains("Replace the existing OpenCode agent?", duplicateCreateAdmission.Confirmation?.Message, StringComparison.Ordinal);
        Assert.False(invalidSaveAdmission.CanRun);
        Assert.Equal("Choose an OpenCode agent first.", invalidSaveAdmission.StatusMessage);
        Assert.False(invalidDeleteAdmission.CanRun);
        Assert.True(saveAdmission.CanRun);
        Assert.Null(saveAdmission.Confirmation);
        Assert.True(deleteAdmission.CanRun);
        Assert.Equal("Delete OpenCode agent", deleteAdmission.Confirmation?.Title);
        Assert.Contains(created.Label, deleteAdmission.Confirmation?.Message, StringComparison.Ordinal);
        Assert.False(selectedState.AddPanelVisible);
        Assert.True(selectedState.SaveEnabled);
        Assert.True(selectedState.DeleteEnabled);
        Assert.True(addState.AddPanelVisible);
        Assert.False(addState.SaveEnabled);
        Assert.Contains("local/qwen", snippet, StringComparison.Ordinal);
        Assert.Equal("Created OpenCode agent build-agent.", create.StatusMessage);

        var save = agentWorkflow.SaveAgentSnippet(files, created, """
        {
          "description": "Build things",
          "mode": "subagent"
        }
        """);
        Assert.Equal(created.Id, save.AgentId);
        Assert.Equal("Saved OpenCode agent build-agent.", save.StatusMessage);
        Assert.Contains("Build things", agentWorkflow.ReadAgentSnippet(files, created), StringComparison.Ordinal);

        var delete = agentWorkflow.DeleteAgent(files, created);
        Assert.Equal("Deleted OpenCode agent build-agent.", delete.StatusMessage);
        Assert.DoesNotContain(pageWorkflow.BuildChoices(files, [], "", "").Agents, agent => agent.Id == created.Id);
        Assert.Contains("_coreServices.OpenCodeServices.OpenCodeAgentApplication", source, StringComparison.Ordinal);
        Assert.DoesNotContain("SetStatus($\"Saved OpenCode agent", source, StringComparison.Ordinal);
        Assert.DoesNotContain("SetStatus($\"Created OpenCode agent", source, StringComparison.Ordinal);
        Assert.DoesNotContain("SetStatus($\"Deleted OpenCode agent", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Choose an OpenCode agent first.", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Replace the existing OpenCode agent?", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Delete this OpenCode agent?", source, StringComparison.Ordinal);
    }

    [Fact]
    public async Task OpenCodeAgentApplicationServiceRunsSaveCreateAndDeleteCommands()
    {
        var root = CreateTempRoot();
        var openCode = new OpenCodeConfigService(root);
        var sync = new OpenCodeModelSyncService(openCode);
        var pageWorkflow = new OpenCodePageWorkflowService(openCode, sync);
        var workflow = new OpenCodeAgentWorkflowService(openCode);
        var application = new OpenCodeAgentApplicationService(workflow);
        var files = new OpenCodeFileSet(Path.Combine(root, "opencode.jsonc"), Path.Combine(root, "agent"));
        openCode.EnsureFiles(files);
        var selectedModel = new OpenCodeModelEntry("local/qwen", "local", "qwen", "Qwen");
        var calls = new List<string>();
        var resultActions = new OpenCodeAgentCommandApplicationActions(
            preferredAgentId =>
            {
                calls.Add($"refresh-agent:{preferredAgentId}");
                return Task.CompletedTask;
            },
            () =>
            {
                calls.Add("refresh");
                return Task.CompletedTask;
            },
            status => calls.Add($"status:{status}"));

        var missingSave = await application.SaveSnippetAsync(
            new OpenCodeAgentSaveApplicationRequest(files, SelectedAgent: null, Snippet: "{}"),
            SaveActions(confirm: true));
        var invalidCreate = await application.CreateAsync(
            new OpenCodeAgentCreateApplicationRequest(files, RequestedName: " ", Markdown: false, ExistingAgents: [], selectedModel),
            CreateActions(confirm: true));
        var created = await application.CreateAsync(
            new OpenCodeAgentCreateApplicationRequest(files, "Build Agent", Markdown: false, ExistingAgents: [], selectedModel),
            CreateActions(confirm: true));
        var agent = pageWorkflow.BuildChoices(files, [], "", "config:build-agent").SelectedAgent!;
        var duplicateDeclined = await application.CreateAsync(
            new OpenCodeAgentCreateApplicationRequest(files, "Build Agent", Markdown: false, ExistingAgents: pageWorkflow.BuildChoices(files, [], "", "").Agents, selectedModel),
            CreateActions(confirm: false));
        var saved = await application.SaveSnippetAsync(
            new OpenCodeAgentSaveApplicationRequest(files, agent, """
            {
              "description": "Build things",
              "mode": "subagent"
            }
            """),
            SaveActions(confirm: true));
        var deleteDeclined = await application.DeleteAsync(
            new OpenCodeAgentDeleteApplicationRequest(files, agent),
            DeleteActions(confirm: false));
        var deleted = await application.DeleteAsync(
            new OpenCodeAgentDeleteApplicationRequest(files, agent),
            DeleteActions(confirm: true));

        Assert.Equal(OpenCodeAgentCommandOutcome.Rejected, missingSave);
        Assert.Equal(OpenCodeAgentCommandOutcome.Rejected, invalidCreate);
        Assert.Equal(OpenCodeAgentCommandOutcome.Created, created);
        Assert.Equal(OpenCodeAgentCommandOutcome.Rejected, duplicateDeclined);
        Assert.Equal(OpenCodeAgentCommandOutcome.Saved, saved);
        Assert.Equal(OpenCodeAgentCommandOutcome.Rejected, deleteDeclined);
        Assert.Equal(OpenCodeAgentCommandOutcome.Deleted, deleted);
        Assert.Equal(
            [
                "status:Choose an OpenCode agent first.",
                "status:Name the new agent first.",
                "busy:Creating OpenCode agent...",
                "refresh-agent:config:build-agent",
                "status:Created OpenCode agent build-agent.",
                "confirm:OpenCode agent:False",
                "busy:Saving OpenCode agent...",
                "refresh-agent:config:build-agent",
                "status:Saved OpenCode agent build-agent.",
                "confirm:Delete OpenCode agent:False",
                "confirm:Delete OpenCode agent:True",
                "busy:Deleting OpenCode agent...",
                "refresh",
                "status:Deleted OpenCode agent build-agent."
            ],
            calls);
        Assert.DoesNotContain(pageWorkflow.BuildChoices(files, [], "", "").Agents, candidate => candidate.Id == agent.Id);

        OpenCodeAgentSaveApplicationActions SaveActions(bool confirm)
            => new(RunBusy, Confirm(confirm), resultActions);

        OpenCodeAgentCreateApplicationActions CreateActions(bool confirm)
            => new(RunBusy, Confirm(confirm), resultActions);

        OpenCodeAgentDeleteApplicationActions DeleteActions(bool confirm)
            => new(RunBusy, Confirm(confirm), resultActions);

        async Task RunBusy(string message, Func<Task> action)
        {
            calls.Add($"busy:{message}");
            await action();
        }

        Func<OpenCodeCommandConfirmation, bool> Confirm(bool result)
            => confirmation =>
            {
                calls.Add($"confirm:{confirmation.Title}:{result}");
                return result;
            };
    }

    [Fact]
    public async Task OpenCodeAgentApplicationServiceLoadsSelectedAgentsAndAddNewState()
    {
        var root = CreateTempRoot();
        var openCode = new OpenCodeConfigService(root);
        var sync = new OpenCodeModelSyncService(openCode);
        var pageWorkflow = new OpenCodePageWorkflowService(openCode, sync);
        var workflow = new OpenCodeAgentWorkflowService(openCode);
        var application = new OpenCodeAgentApplicationService(workflow);
        var files = new OpenCodeFileSet(Path.Combine(root, "opencode.jsonc"), Path.Combine(root, "agent"));
        openCode.EnsureFiles(files);
        var selectedModel = new OpenCodeModelEntry("local/qwen", "local", "qwen", "Qwen");
        var create = workflow.CreateAgent(
            files,
            workflow.AnalyzeNewAgentDraft("Build Agent", markdown: false, []),
            selectedModel);
        var choices = pageWorkflow.BuildChoices(files, [], "", create.Agent.Id);
        var agent = choices.SelectedAgent!;
        var addNew = choices.Agents.Last();
        var missing = new OpenCodeAgentEntry("config:missing", "missing", OpenCodeAgentKind.Config, "missing.json", "Missing");
        var calls = new List<string>();

        var noEditor = await application.LoadSelectedAsync(
            new OpenCodeAgentLoadApplicationRequest(files, agent, HasSnippetEditor: false),
            LoadActions());
        var adding = await application.LoadSelectedAsync(
            new OpenCodeAgentLoadApplicationRequest(files, addNew, HasSnippetEditor: true),
            LoadActions());
        var loaded = await application.LoadSelectedAsync(
            new OpenCodeAgentLoadApplicationRequest(files, agent, HasSnippetEditor: true),
            LoadActions());
        File.WriteAllText(files.ConfigPath, "{ invalid json");
        var failed = await application.LoadSelectedAsync(
            new OpenCodeAgentLoadApplicationRequest(files, missing, HasSnippetEditor: true),
            LoadActions());

        Assert.Equal(OpenCodeAgentLoadOutcome.NoEditor, noEditor);
        Assert.Equal(OpenCodeAgentLoadOutcome.Adding, adding);
        Assert.Equal(OpenCodeAgentLoadOutcome.Loaded, loaded);
        Assert.Equal(OpenCodeAgentLoadOutcome.Failed, failed);
        Assert.Equal(
            [
                "agent:editor:False:True:True:True",
                "agent:editor:True:False:False:True",
                "agent:text",
                "agent:editor:False:True:True:True",
                "agent:text",
                "agent:editor:False:True:True:True",
                "agent:text",
                "status"
            ],
            calls);

        OpenCodeAgentLoadApplicationActions LoadActions()
            => new(
                state => calls.Add($"agent:editor:{state.AddPanelVisible}:{state.SaveEnabled}:{state.DeleteEnabled}:{state.CreateEnabled}"),
                _ => calls.Add("agent:text"),
                _ => calls.Add("status"));
    }

    [Fact]
    public void OpenCodePageWorkflowServicePersistsFileSetTransitions()
    {
        var root = CreateTempRoot();
        var openCode = new OpenCodeConfigService(root);
        var sync = new OpenCodeModelSyncService(openCode);
        var workflow = new OpenCodePageWorkflowService(openCode, sync);
        var original = new OpenCodeFileSet(
            Path.Combine(root, "old", "opencode.jsonc"),
            Path.Combine(root, "old", "agent"));
        var configPath = Path.Combine(root, "chosen", "opencode.jsonc");
        var agentsDirectory = Path.Combine(root, "chosen", "agents");

        var withConfig = workflow.SaveConfigPath(original, configPath);
        var withAgents = workflow.SaveAgentsDirectory(withConfig, agentsDirectory);
        var ensured = workflow.EnsureAndSaveFileSet(withAgents);
        var loaded = workflow.LoadOrDetectFileSet();

        Assert.Equal(Path.GetFullPath(configPath), ensured.ConfigPath);
        Assert.Equal(Path.GetFullPath(agentsDirectory), ensured.AgentsDirectory);
        Assert.Equal(ensured, loaded);
        Assert.True(File.Exists(ensured.ConfigPath));
        Assert.True(Directory.Exists(ensured.AgentsDirectory));
        Assert.Equal(Path.GetDirectoryName(ensured.ConfigPath), OpenCodePageWorkflowService.ConfigDirectory(ensured));
    }

    [Fact]
    public async Task OpenCodeSettingsSyncServiceBuildsGatewayItemsAndUpdatesDirectCredentials()
    {
        var root = CreateTempRoot();
        var openCode = new OpenCodeConfigService(root);
        var sync = new OpenCodeModelSyncService(openCode);
        var workflow = new OpenCodePageWorkflowService(openCode, sync);
        var settingsSync = new OpenCodeSettingsSyncService(workflow, sync);
        var files = workflow.EnsureAndSaveFileSet(new OpenCodeFileSet(
            Path.Combine(root, "opencode.jsonc"),
            Path.Combine(root, "agent")));
        var settings = AppSettings.CreateDefault(root) with
        {
            ModelApiKey = "abcdefghijklmnopqrstuvwxyz123456",
            AutoLoadGatewayEnabled = true,
            AutoLoadGatewayPort = 8082,
            Port = 8081,
            ContextSize = 4096,
            MaxTokens = 1024
        };
        var first = new ModelRecord("first", "First Model", Path.Combine(root, "first.gguf"), OwnershipKind.External, "{}", DateTimeOffset.UtcNow);
        var second = new ModelRecord("second", "Second Model", Path.Combine(root, "second.gguf"), OwnershipKind.External, "{}", DateTimeOffset.UtcNow);
        var firstProfile = ModelLaunchSettings.FromAppSettings(settings) with
        {
            ContextSize = 8192,
            MaxTokens = 2048,
            Port = 8091
        };
        var resolvedPorts = new List<int>();

        var noKey = await settingsSync.SyncAsync(new OpenCodeSettingsSyncRequest(
            settings with { ModelApiKey = "" },
            [first],
            (_, _) => ValueTask.FromResult<ModelLaunchSettings?>(null),
            (_, _, _) => ValueTask.FromResult(new OpenCodeModelLimits(1, 1))),
            TestContext.Current.CancellationToken);
        var gateway = await settingsSync.SyncAsync(new OpenCodeSettingsSyncRequest(
            settings,
            [first, second],
            (model, _) => ValueTask.FromResult(model.Id == first.Id ? firstProfile : null),
            (_, launchSettings, _) =>
            {
                resolvedPorts.Add(launchSettings.Port);
                return ValueTask.FromResult(new OpenCodeModelLimits(launchSettings.ContextSize, launchSettings.MaxTokens));
            }),
            TestContext.Current.CancellationToken);

        var config = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(files.ConfigPath))!;
        var gatewayProvider = config["provider"]?[OpenCodeConfigService.LocalProviderId];
        Assert.False(noKey.Completed);
        Assert.Null(noKey.FileSet);
        Assert.True(gateway.Completed);
        Assert.True(gateway.UsedGateway);
        Assert.Equal(2, gateway.SyncedModels);
        Assert.Equal([8091, 8081], resolvedPorts);
        Assert.Equal("http://127.0.0.1:8082/v1", gatewayProvider?["options"]?["baseURL"]?.ToString());
        Assert.Equal("8192", gatewayProvider?["models"]?[OpenCodeConfigService.LocalModelIdFor(first)]?["limit"]?["context"]?.ToString());
        Assert.Equal("2048", gatewayProvider?["models"]?[OpenCodeConfigService.LocalModelIdFor(first)]?["limit"]?["output"]?.ToString());
        Assert.Equal("4096", gatewayProvider?["models"]?[OpenCodeConfigService.LocalModelIdFor(second)]?["limit"]?["context"]?.ToString());

        openCode.AddOrUpdateLocalModel(files.ConfigPath, first, "http://127.0.0.1:8099/v1", "old-key", 4096, 1024);
        var directSettings = settings with
        {
            AutoLoadGatewayEnabled = false,
            ModelApiKey = "1234567890abcdef1234567890abcdef"
        };
        var direct = await settingsSync.SyncAsync(new OpenCodeSettingsSyncRequest(
            directSettings,
            null,
            (_, _) => throw new InvalidOperationException("Direct sync should not read model profiles."),
            (_, _, _) => throw new InvalidOperationException("Direct sync should not resolve model limits.")),
            TestContext.Current.CancellationToken);

        config = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(files.ConfigPath))!;
        var directProviderId = $"{OpenCodeConfigService.LocalProviderId}-{OpenCodeConfigService.LocalModelIdFor(first)}";
        Assert.True(direct.Completed);
        Assert.False(direct.UsedGateway);
        Assert.Equal("1234567890abcdef1234567890abcdef", config["provider"]?[directProviderId]?["options"]?["apiKey"]?.ToString());
    }


    [Fact]
    public void OpenCodeGatewayProviderHealthFindsBrokenSync()
    {
        var root = CreateTempRoot();
        var service = new OpenCodeConfigService(root);
        var configPath = Path.Combine(root, "opencode.jsonc");
        var model = new ModelRecord("model-a", "First Model", Path.Combine(root, "models", "first-model.gguf"), OwnershipKind.AppOwned, "{}", DateTimeOffset.UtcNow);
        var modelId = OpenCodeConfigService.LocalModelIdFor(model);
        File.WriteAllText(configPath, $$"""
        {
          "$schema": "https://opencode.ai/config.json",
          "provider": {
            "local-llm-console": {
              "options": {
                "baseURL": "http://127.0.0.1:8082/v1"
              },
              "models": {
                "{{modelId}}": {
                  "name": "First Model",
                  "limit": {
                    "context": 8192
                  }
                }
              }
            }
          }
        }
        """);

        var broken = service.InspectLocalGatewayProvider(configPath, [model], "http://127.0.0.1:8082/v1");
        service.AddOrUpdateLocalModel(configPath, model, "http://127.0.0.1:8082/v1", "gateway-key", 8192, 4096, useGatewayProvider: true);
        var healthy = service.InspectLocalGatewayProvider(configPath, [model], "http://127.0.0.1:8082/v1");

        Assert.False(broken.Ok);
        Assert.Contains("output", broken.Detail, StringComparison.OrdinalIgnoreCase);
        Assert.True(healthy.Ok);
        Assert.Contains("healthy", healthy.Summary, StringComparison.OrdinalIgnoreCase);
    }


    [Fact]
    public void OpenCodeLocalModelWritesDropDeprecatedAttachmentConfig()
    {
        var root = CreateTempRoot();
        var service = new OpenCodeConfigService(root);
        var configPath = Path.Combine(root, "opencode.jsonc");
        File.WriteAllText(configPath, """
        {
          "$schema": "https://opencode.ai/config.json",
          "attachment": {
            "image": {
              "auto_resize": true
            }
          },
          "provider": {}
        }
        """);
        var model = new ModelRecord("model", "Test Model", Path.Combine(root, "models", "test-model.gguf"), OwnershipKind.AppOwned, "{}", DateTimeOffset.UtcNow);

        var draft = service.CreateLocalModelDraft(configPath, model, "http://127.0.0.1:8084/v1", "key-a", 8192, 4096);
        var fullId = service.SaveLocalModelSnippet(configPath, model, "http://127.0.0.1:8084/v1", "key-a", draft.Snippet, addAsNew: false);
        var snippet = service.ReadModelSnippet(configPath, new OpenCodeModelEntry(fullId, draft.ProviderId, draft.ModelId, "Test Model"));

        Assert.DoesNotContain("\"attachment\"", draft.Snippet, StringComparison.Ordinal);
        Assert.DoesNotContain("\"attachment\"", File.ReadAllText(configPath), StringComparison.Ordinal);
        Assert.DoesNotContain("\"attachment\"", snippet, StringComparison.Ordinal);
    }


    [Fact]
    public void OpenCodeLocalModelEditUpdateAndAddAsNewKeepPerModelEndpoint()
    {
        var root = CreateTempRoot();
        var service = new OpenCodeConfigService(root);
        var configPath = Path.Combine(root, "opencode.jsonc");
        var model = new ModelRecord("model", "Test Model", Path.Combine(root, "models", "test-model.gguf"), OwnershipKind.AppOwned, "{}", DateTimeOffset.UtcNow);
        var draft = service.CreateLocalModelDraft(configPath, model, "http://127.0.0.1:8084/v1", "key-a", 8192, 4096);
        var fullId = service.SaveLocalModelSnippet(configPath, model, "http://127.0.0.1:8084/v1", "key-a", draft.Snippet, addAsNew: false);
        var entry = new OpenCodeModelEntry(fullId, draft.ProviderId, draft.ModelId, "Test Model");
        var snippet = service.ReadModelSnippet(configPath, entry);
        var edited = System.Text.Json.Nodes.JsonNode.Parse(snippet)!.AsObject();
        edited["provider"]![draft.ProviderId]!["models"]![draft.ModelId]!["limit"]!["context"] = 16384;

        service.SaveModelSnippet(configPath, entry, edited.ToJsonString(new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
        var addedId = service.SaveLocalModelSnippet(configPath, model, "http://127.0.0.1:8084/v1", "key-a", draft.Snippet, addAsNew: true);

        var config = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(configPath))!;
        var provider = config["provider"]![draft.ProviderId]!;

        Assert.Equal(draft.FullId, fullId);
        Assert.Equal($"{draft.ProviderId}/{draft.ModelId}-2", addedId);
        Assert.Equal("http://127.0.0.1:8084/v1", provider["options"]?["baseURL"]?.ToString());
        Assert.Equal("key-a", provider["options"]?["apiKey"]?.ToString());
        Assert.Equal("16384", provider["models"]?[draft.ModelId]?["limit"]?["context"]?.ToString());
        Assert.NotNull(provider["models"]?[$"{draft.ModelId}-2"]);
    }
}
