namespace LocalLlmConsole.Services;

public sealed class RuntimeEndpointProbeService
{
    private static readonly string[] AliveProbePaths = ["health", "v1/models"];
    private static readonly string[] RespondingProbePaths = ["health", "v1/models", "metrics"];

    private readonly HttpClient _http;

    public RuntimeEndpointProbeService(HttpClient http)
    {
        _http = http ?? throw new ArgumentNullException(nameof(http));
    }

    public async Task<bool> IsAliveAsync(AppSettings launchSettings, CancellationToken cancellationToken = default)
    {
        foreach (var path in AliveProbePaths)
        {
            try
            {
                using var response = await GetAsync(launchSettings, path, cancellationToken);
                if (response.IsSuccessStatusCode) return true;
            }
            catch
            {
                // Try the next runtime endpoint.
            }
        }

        return false;
    }

    public async Task<bool> IsRespondingAsync(AppSettings launchSettings, CancellationToken cancellationToken = default)
    {
        foreach (var path in RespondingProbePaths)
        {
            try
            {
                using var _ = await GetAsync(launchSettings, path, cancellationToken);
                return true;
            }
            catch
            {
                // Try the next runtime endpoint.
            }
        }

        return false;
    }

    public async Task<IReadOnlyList<string>> ServedModelsAsync(
        AppSettings launchSettings,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var json = await RuntimeEndpointService.RuntimeGetStringAsync(
                _http,
                $"{RuntimeEndpointService.LocalOpenAiBaseUrl(launchSettings)}/models",
                launchSettings,
                cancellationToken);
            return RuntimeEndpointService.ExtractServedModelIds(json).ToArray();
        }
        catch
        {
            return [];
        }
    }

    private async Task<HttpResponseMessage> GetAsync(
        AppSettings launchSettings,
        string path,
        CancellationToken cancellationToken)
    {
        using var request = RuntimeEndpointService.RuntimeGetRequest(
            $"{RuntimeEndpointService.LocalServerBaseUrl(launchSettings)}/{path}",
            launchSettings);
        return await _http.SendAsync(request, cancellationToken);
    }
}
