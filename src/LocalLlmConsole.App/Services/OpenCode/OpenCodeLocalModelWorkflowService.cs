namespace LocalLlmConsole.Services;

public delegate ValueTask<AppSettings> OpenCodeApiKeyEnsurer(AppSettings settings, CancellationToken cancellationToken);

public delegate ValueTask<ModelLaunchSettings?> OpenCodeLaunchProfileEnsurer(ModelRecord model, CancellationToken cancellationToken);

public delegate ValueTask<ModelCapabilitySummary> OpenCodeModelCapabilityReader(ModelRecord model, CancellationToken cancellationToken);

public sealed record OpenCodeLocalModelActionState(
    string Status,
    bool AddVisible,
    bool AddEnabled,
    bool UpdateVisible,
    bool UpdateEnabled,
    bool AddAsNewVisible,
    bool AddAsNewEnabled);

public sealed record OpenCodeLocalModelLaunchSettingsRequest(
    ModelRecord Model,
    AppSettings ApplicationSettings,
    LoadedModelSessionSnapshot? LoadedSession,
    OpenCodeLaunchProfileEnsurer EnsureProfileAsync,
    OpenCodeApiKeyEnsurer EnsureApiKeyAsync);

public sealed record OpenCodeLocalModelDraftBuildRequest(
    OpenCodeFileSet Files,
    ModelRecord Model,
    AppSettings ApplicationSettings,
    AppSettings LaunchSettings,
    bool UseGatewayProvider);

public sealed record OpenCodeLocalModelSaveWorkflowRequest(
    OpenCodeFileSet Files,
    ModelRecord Model,
    AppSettings ApplicationSettings,
    AppSettings LaunchSettings,
    string Snippet,
    bool AddAsNew,
    bool UseGatewayProvider);

public sealed record OpenCodeLocalModelSaveResult(
    string FullId,
    string StatusMessage);

public sealed class OpenCodeLocalModelWorkflowService
{
    private readonly OpenCodeModelSyncService _sync;

    public OpenCodeLocalModelWorkflowService(OpenCodeModelSyncService sync)
    {
        _sync = sync ?? throw new ArgumentNullException(nameof(sync));
    }

    public async Task<AppSettings> ResolveLaunchSettingsAsync(
        OpenCodeLocalModelLaunchSettingsRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.EnsureProfileAsync);
        ArgumentNullException.ThrowIfNull(request.EnsureApiKeyAsync);

        if (request.LoadedSession is { IsRunning: true })
            return await request.EnsureApiKeyAsync(request.LoadedSession.LaunchSettings, cancellationToken);

        var launchSettings = request.ApplicationSettings;
        var profile = await request.EnsureProfileAsync(request.Model, cancellationToken);
        if (profile is not null)
            launchSettings = profile.ApplyTo(request.ApplicationSettings);

        return await request.EnsureApiKeyAsync(launchSettings, cancellationToken);
    }

    public async Task<OpenCodeModelLimits> ResolveLimitsAsync(
        ModelRecord model,
        AppSettings launchSettings,
        OpenCodeModelCapabilityReader readCapabilitiesAsync,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(readCapabilitiesAsync);
        var capabilities = launchSettings.ContextSize <= 0
            ? await readCapabilitiesAsync(model, cancellationToken)
            : ModelCapabilityService.Empty();
        return _sync.ResolveLimits(model, launchSettings, capabilities);
    }

    public async Task<OpenCodeLocalModelDraft> CreateDraftAsync(
        OpenCodeLocalModelDraftBuildRequest request,
        OpenCodeModelCapabilityReader readCapabilitiesAsync,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var limits = await ResolveLimitsAsync(request.Model, request.LaunchSettings, readCapabilitiesAsync, cancellationToken);
        return _sync.CreateDraft(new OpenCodeLocalModelDraftRequest(
            request.Files.ConfigPath,
            request.Model,
            request.ApplicationSettings,
            request.LaunchSettings,
            limits,
            request.UseGatewayProvider));
    }

    public OpenCodeLocalModelActionState NoLocalModelSelected()
        => LocalModelActionState("Choose a local model to add.", valid: false, sameIdExists: false, sameConfig: false);

    public OpenCodeLocalModelActionState AnalyzeLocalModelSnippet(
        string configPath,
        ModelRecord model,
        string snippet,
        bool useGatewayProvider)
    {
        var analysis = _sync.AnalyzeLocalModelSnippet(configPath, model, snippet, useGatewayProvider);
        if (!analysis.SnippetValid)
            return LocalModelActionState(analysis.Error, valid: false, analysis.SameIdExists, analysis.SameConfig);

        var status = StatusForAnalysis(analysis, useGatewayProvider);
        return LocalModelActionState(status, valid: true, analysis.SameIdExists, analysis.SameConfig);
    }

    public string SaveSnippet(OpenCodeLocalModelSaveRequest request)
        => _sync.SaveLocalModelSnippet(request);

    public OpenCodeLocalModelSaveResult Save(OpenCodeLocalModelSaveWorkflowRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var fullId = SaveSnippet(new OpenCodeLocalModelSaveRequest(
            request.Files.ConfigPath,
            request.Model,
            request.ApplicationSettings,
            request.LaunchSettings,
            request.Snippet,
            request.AddAsNew,
            request.UseGatewayProvider));
        return new OpenCodeLocalModelSaveResult(
            fullId,
            request.AddAsNew ? $"Added OpenCode model {fullId}." : $"Saved OpenCode model {fullId}.");
    }

    public static string StatusForAnalysis(OpenCodeModelAddAnalysis analysis, bool useGatewayProvider)
    {
        if (!analysis.SnippetValid) return analysis.Error;
        if (analysis.SameIdExists && analysis.SameConfig)
        {
            return useGatewayProvider
                ? $"Already added to OpenCode automatically when the model was saved: {analysis.FullId}"
                : $"Already exists with the same config: {analysis.FullId}";
        }

        if (analysis.SameIdExists)
            return $"Same model id exists with different config: {analysis.FullId}";
        if (analysis.SimilarMatches.Count > 0)
            return "Similar existing model: " + string.Join("; ", analysis.SimilarMatches.Take(3));
        return $"Ready to add: {analysis.FullId}";
    }

    private static OpenCodeLocalModelActionState LocalModelActionState(string status, bool valid, bool sameIdExists, bool sameConfig)
        => new(
            status,
            AddVisible: !sameIdExists,
            AddEnabled: valid && !sameIdExists,
            UpdateVisible: sameIdExists && !sameConfig,
            UpdateEnabled: valid && sameIdExists && !sameConfig,
            AddAsNewVisible: sameIdExists && !sameConfig,
            AddAsNewEnabled: valid && sameIdExists && !sameConfig);

}
