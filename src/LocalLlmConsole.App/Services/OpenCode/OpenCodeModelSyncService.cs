namespace LocalLlmConsole.Services;

public sealed record OpenCodeModelLimits(
    int ContextSize,
    int OutputLimit,
    bool SupportsVision = false);

public sealed record OpenCodeLocalModelDraftRequest(
    string ConfigPath,
    ModelRecord Model,
    AppSettings ApplicationSettings,
    AppSettings LaunchSettings,
    OpenCodeModelLimits Limits,
    bool UseGatewayProvider);

public sealed record OpenCodeLocalModelSaveRequest(
    string ConfigPath,
    ModelRecord Model,
    AppSettings ApplicationSettings,
    AppSettings LaunchSettings,
    string Snippet,
    bool AddAsNew,
    bool UseGatewayProvider);

public sealed record OpenCodeGatewayModelSyncItem(
    ModelRecord Model,
    AppSettings LaunchSettings,
    OpenCodeModelLimits Limits);

public sealed class OpenCodeModelSyncService
{
    private readonly OpenCodeConfigService _openCode;

    public OpenCodeModelSyncService(OpenCodeConfigService openCode)
    {
        _openCode = openCode ?? throw new ArgumentNullException(nameof(openCode));
    }

    public OpenCodeModelLimits ResolveLimits(AppSettings launchSettings, ModelCapabilitySummary capabilities)
    {
        var contextSize = launchSettings.ContextSize;
        if (contextSize <= 0)
            contextSize = capabilities.ContextLength;

        var outputLimit = launchSettings.MaxTokens > 0
            ? launchSettings.MaxTokens
            : contextSize > 0
                ? Math.Clamp(contextSize / 4, 4096, OpenCodeConfigService.DefaultOutputLimit)
                : 8192;

        return new OpenCodeModelLimits(
            contextSize,
            outputLimit,
            SupportsVision: !string.Equals(launchSettings.VisionMode, "off", StringComparison.OrdinalIgnoreCase)
                && capabilities.HasVisionProjector);
    }

    public OpenCodeModelLimits ResolveLimits(
        ModelRecord model,
        AppSettings launchSettings,
        ModelCapabilitySummary capabilities)
        => ResolveLimits(launchSettings, capabilities) with
        {
            SupportsVision = SupportsVision(model, launchSettings, capabilities)
        };

    public OpenCodeModelLimits ResolveLimits(
        AppSettings launchSettings,
        ModelCapabilitySummary capabilities,
        bool supportsVision)
        => ResolveLimits(launchSettings, capabilities) with { SupportsVision = supportsVision };

    public static bool SupportsVision(
        ModelRecord model,
        AppSettings launchSettings,
        ModelCapabilitySummary capabilities)
    {
        if (string.Equals(launchSettings.VisionMode, "off", StringComparison.OrdinalIgnoreCase))
            return false;

        return VisionProjectorSelection.IsEmbeddedOrMainModel(model.ModelPath, launchSettings.VisionProjectorPath)
            || capabilities.HasVisionProjector
            || ModelCatalogService.ResolveVisionProjectorPath(model.ModelPath, launchSettings.VisionProjectorPath) is not null;
    }

    public OpenCodeLocalModelDraft CreateDraft(OpenCodeLocalModelDraftRequest request)
        => _openCode.CreateLocalModelDraft(
            request.ConfigPath,
            request.Model,
            BaseUrlFor(request.ApplicationSettings, request.LaunchSettings, request.UseGatewayProvider),
            RuntimeEndpointService.ModelApiKeyForClient(request.LaunchSettings),
            request.Limits.ContextSize,
            request.Limits.OutputLimit,
            request.UseGatewayProvider,
            request.Limits.SupportsVision);

    public OpenCodeModelAddAnalysis AnalyzeLocalModelSnippet(
        string configPath,
        ModelRecord model,
        string snippet,
        bool useGatewayProvider)
        => _openCode.AnalyzeLocalModelSnippet(configPath, model, snippet, useGatewayProvider);

    public string SaveLocalModelSnippet(OpenCodeLocalModelSaveRequest request)
        => _openCode.SaveLocalModelSnippet(
            request.ConfigPath,
            request.Model,
            BaseUrlFor(request.ApplicationSettings, request.LaunchSettings, request.UseGatewayProvider),
                RuntimeEndpointService.ModelApiKeyForClient(request.LaunchSettings),
                request.Snippet,
                request.AddAsNew,
                request.UseGatewayProvider);

    public int SyncGatewayProvider(string configPath, AppSettings settings, IEnumerable<OpenCodeGatewayModelSyncItem> models)
    {
        var baseUrl = RuntimeEndpointService.LocalGatewayOpenAiBaseUrl(settings);
        var apiKey = RuntimeEndpointService.ModelApiKeyForClient(settings);
        var synced = 0;
        foreach (var item in models)
        {
            _openCode.AddOrUpdateLocalModel(
                configPath,
                item.Model,
                baseUrl,
                apiKey,
                item.Limits.ContextSize,
                item.Limits.OutputLimit,
                useGatewayProvider: true,
                supportsVision: item.Limits.SupportsVision);
            synced++;
        }

        return synced;
    }

    public bool UpdateDirectProviderCredentials(string configPath, AppSettings settings)
        => _openCode.UpdateLocalProviderCredentials(
            configPath,
            RuntimeEndpointService.LocalOpenAiBaseUrl(settings),
            RuntimeEndpointService.ModelApiKeyForClient(settings));

    public OpenCodeLocalProviderHealth InspectGatewayProvider(
        string configPath,
        IReadOnlyList<ModelRecord> expectedModels,
        AppSettings settings)
        => _openCode.InspectLocalGatewayProvider(
            configPath,
            expectedModels,
            RuntimeEndpointService.LocalGatewayOpenAiBaseUrl(settings));

    public static string BaseUrlFor(AppSettings applicationSettings, AppSettings launchSettings, bool useGatewayProvider)
        => useGatewayProvider
            ? RuntimeEndpointService.LocalGatewayOpenAiBaseUrl(applicationSettings)
            : RuntimeEndpointService.LocalOpenAiBaseUrl(launchSettings);
}
