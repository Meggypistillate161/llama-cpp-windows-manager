namespace LocalLlmConsole.Services;

public delegate ValueTask<ModelLaunchSettings?> OpenCodeLaunchProfileReader(ModelRecord model, CancellationToken cancellationToken);

public delegate ValueTask<OpenCodeModelLimits> OpenCodeModelLimitsResolver(ModelRecord model, AppSettings launchSettings, CancellationToken cancellationToken);

public sealed record OpenCodeSettingsSyncRequest(
    AppSettings Settings,
    IReadOnlyList<ModelRecord>? GatewayModels,
    OpenCodeLaunchProfileReader ReadProfileAsync,
    OpenCodeModelLimitsResolver ResolveLimitsAsync);

public sealed record OpenCodeSettingsSyncResult(
    OpenCodeFileSet? FileSet,
    bool Completed,
    bool UsedGateway,
    int SyncedModels,
    string Status);

public sealed class OpenCodeSettingsSyncService
{
    private readonly OpenCodePageWorkflowService _workflow;
    private readonly OpenCodeModelSyncService _sync;

    public OpenCodeSettingsSyncService(OpenCodePageWorkflowService workflow, OpenCodeModelSyncService sync)
    {
        _workflow = workflow ?? throw new ArgumentNullException(nameof(workflow));
        _sync = sync ?? throw new ArgumentNullException(nameof(sync));
    }

    public async Task<OpenCodeSettingsSyncResult> SyncAsync(OpenCodeSettingsSyncRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.ReadProfileAsync);
        ArgumentNullException.ThrowIfNull(request.ResolveLimitsAsync);

        if (string.IsNullOrWhiteSpace(RuntimeEndpointService.ModelApiKeyForClient(request.Settings)))
            return new OpenCodeSettingsSyncResult(null, Completed: false, UsedGateway: false, SyncedModels: 0, "Model API key is empty.");

        var fileSet = _workflow.LoadOrDetectFileSet();
        if (request.Settings.AutoLoadGatewayEnabled && request.GatewayModels is not null)
        {
            var items = await BuildGatewayItemsAsync(request, cancellationToken);
            var synced = _sync.SyncGatewayProvider(fileSet.ConfigPath, request.Settings, items);
            return new OpenCodeSettingsSyncResult(fileSet, Completed: true, UsedGateway: true, synced, "Gateway provider synced.");
        }

        var updated = _sync.UpdateDirectProviderCredentials(fileSet.ConfigPath, request.Settings);
        return new OpenCodeSettingsSyncResult(
            fileSet,
            Completed: updated,
            UsedGateway: false,
            SyncedModels: 0,
            updated ? "Direct provider credentials updated." : "No direct OpenCode provider credentials were updated.");
    }

    private static async Task<IReadOnlyList<OpenCodeGatewayModelSyncItem>> BuildGatewayItemsAsync(
        OpenCodeSettingsSyncRequest request,
        CancellationToken cancellationToken)
    {
        var items = new List<OpenCodeGatewayModelSyncItem>();
        foreach (var model in request.GatewayModels ?? [])
        {
            cancellationToken.ThrowIfCancellationRequested();
            var profile = await request.ReadProfileAsync(model, cancellationToken);
            var launchSettings = profile?.ApplyTo(request.Settings) ?? request.Settings;
            var limits = await request.ResolveLimitsAsync(model, launchSettings, cancellationToken);
            items.Add(new OpenCodeGatewayModelSyncItem(model, launchSettings, limits));
        }

        return items;
    }
}
