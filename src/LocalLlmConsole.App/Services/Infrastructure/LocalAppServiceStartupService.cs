namespace LocalLlmConsole.Services;

public interface ILocalAppServiceHost : IAsyncDisposable
{
    Uri BaseUri { get; }
    Task StartAsync();
}

public sealed record LocalAppServiceStartupRequest(
    int PreferredPort,
    int MaxFallbackPort,
    Func<int, ILocalAppServiceHost> CreateService);

public sealed record LocalAppServiceStartupResult(
    ILocalAppServiceHost Service,
    int Port,
    string StatusMessage);

public sealed class LocalAppServiceStartupService
{
    public async Task<LocalAppServiceStartupResult> StartAsync(LocalAppServiceStartupRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.CreateService);

        if (request.PreferredPort is < 1 or > 65535)
            throw new ArgumentOutOfRangeException(nameof(request.PreferredPort), "Preferred port must be between 1 and 65535.");
        if (request.MaxFallbackPort < request.PreferredPort || request.MaxFallbackPort > 65535)
            throw new ArgumentOutOfRangeException(nameof(request.MaxFallbackPort), "Max fallback port must be between the preferred port and 65535.");

        for (var port = request.PreferredPort; port <= request.MaxFallbackPort; port++)
        {
            var service = request.CreateService(port);
            try
            {
                await service.StartAsync();
                var status = port == request.PreferredPort
                    ? ""
                    : $"Local app service moved to 127.0.0.1:{port} because port {request.PreferredPort} was busy.";
                return new LocalAppServiceStartupResult(service, port, status);
            }
            catch (Exception ex) when (CanTryNextPort(ex, port, request.MaxFallbackPort))
            {
                await service.DisposeAsync();
            }
        }

        throw new InvalidOperationException($"Could not start the local app service on 127.0.0.1:{request.PreferredPort}-{request.MaxFallbackPort}.");
    }

    private static bool CanTryNextPort(Exception exception, int port, int maxFallbackPort)
        => port < maxFallbackPort
            && (exception is HttpListenerException || exception is SocketException);
}
