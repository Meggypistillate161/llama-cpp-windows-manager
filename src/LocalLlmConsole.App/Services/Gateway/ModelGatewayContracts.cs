namespace LocalLlmConsole.Services;

public enum ModelGatewaySwapPolicy
{
    KeepLoaded,
    SingleActive
}

public sealed record ModelGatewayOptions(
    bool Enabled,
    string AccessMode,
    int Port,
    string ApiKey,
    ModelGatewaySwapPolicy SwapPolicy,
    long MaxRequestBodyBytes = 64L * 1024 * 1024)
{
    public bool AllowLanAccess
        => AppPreferenceService.GatewayAllowsLanAccess(AccessMode);

    public string ListenerPrefix
        => AllowLanAccess
            ? $"http://+:{Port.ToString(CultureInfo.InvariantCulture)}/"
            : $"http://127.0.0.1:{Port.ToString(CultureInfo.InvariantCulture)}/";

    public string LocalOpenAiBaseUrl
        => $"http://127.0.0.1:{Port.ToString(CultureInfo.InvariantCulture)}/v1";

    public static ModelGatewayOptions FromSettings(AppSettings settings)
        => new(
            settings.AutoLoadGatewayEnabled,
            settings.ModelAccessMode,
            settings.AutoLoadGatewayPort,
            RuntimeEndpointService.ModelApiKeyForClient(settings),
            AppPreferenceService.GatewaySwapPolicy(settings.AutoLoadGatewayPolicy) == "singleActive"
                ? ModelGatewaySwapPolicy.SingleActive
                : ModelGatewaySwapPolicy.KeepLoaded);
}

public interface IModelGatewayRuntimeController
{
    Task<IReadOnlyList<ModelRecord>> ListModelsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<LoadedModelSessionSnapshot>> RunningSessionsAsync(CancellationToken cancellationToken = default);
    Task<LoadedModelSessionSnapshot> EnsureModelLoadedAsync(ModelRecord model, ModelGatewaySwapPolicy policy, CancellationToken cancellationToken = default);
}

public interface IModelGatewayHost : IAsyncDisposable
{
    Task StartAsync(CancellationToken cancellationToken = default);
}
