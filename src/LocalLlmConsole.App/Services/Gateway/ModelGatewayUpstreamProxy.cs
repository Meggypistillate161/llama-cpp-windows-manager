using System.Net.Http.Headers;

namespace LocalLlmConsole.Services;

public sealed class ModelGatewayUpstreamProxy : IDisposable
{
    private static readonly HashSet<string> HopByHopHeaders = new(StringComparer.OrdinalIgnoreCase)
    {
        "Connection",
        "Keep-Alive",
        "Proxy-Authenticate",
        "Proxy-Authorization",
        "TE",
        "Trailer",
        "Transfer-Encoding",
        "Upgrade",
        "Host",
        "Content-Length"
    };

    private readonly HttpClient _client;

    public ModelGatewayUpstreamProxy(HttpClient? client = null)
    {
        _client = client ?? new HttpClient { Timeout = Timeout.InfiniteTimeSpan };
    }

    public async Task ForwardAsync(
        HttpListenerContext context,
        LoadedModelSessionSnapshot session,
        byte[] body,
        CancellationToken cancellationToken)
    {
        var upstream = new Uri($"{RuntimeEndpointService.LocalServerBaseUrl(session.LaunchSettings)}{context.Request.Url?.PathAndQuery ?? "/"}");
        using var request = BuildUpstreamRequest(context.Request, upstream, body, session.LaunchSettings);
        using var response = await _client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        await CopyResponseAsync(context, response, cancellationToken);
    }

    private static HttpRequestMessage BuildUpstreamRequest(
        HttpListenerRequest source,
        Uri upstream,
        byte[] body,
        AppSettings launchSettings)
    {
        var request = new HttpRequestMessage(new HttpMethod(source.HttpMethod), upstream);
        foreach (var key in source.Headers.AllKeys)
        {
            if (string.IsNullOrWhiteSpace(key) || HopByHopHeaders.Contains(key)) continue;
            if (key.Equals("Authorization", StringComparison.OrdinalIgnoreCase)) continue;
            var values = source.Headers.GetValues(key);
            if (values is not null)
                request.Headers.TryAddWithoutValidation(key, values);
        }

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", RuntimeEndpointService.ModelApiKeyForClient(launchSettings));
        request.Content = new ByteArrayContent(body);
        if (!string.IsNullOrWhiteSpace(source.ContentType))
            request.Content.Headers.TryAddWithoutValidation("Content-Type", source.ContentType);
        return request;
    }

    private static async Task CopyResponseAsync(
        HttpListenerContext context,
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        context.Response.StatusCode = (int)response.StatusCode;
        foreach (var header in response.Headers)
        {
            if (HopByHopHeaders.Contains(header.Key)) continue;
            TrySetResponseHeader(context.Response, header.Key, string.Join(",", header.Value));
        }

        foreach (var header in response.Content.Headers)
        {
            if (HopByHopHeaders.Contains(header.Key)) continue;
            if (header.Key.Equals("Content-Type", StringComparison.OrdinalIgnoreCase)) continue;
            TrySetResponseHeader(context.Response, header.Key, string.Join(",", header.Value));
        }

        context.Response.ContentType = response.Content.Headers.ContentType?.ToString() ?? "application/json";
        if (response.Content.Headers.ContentLength is { } contentLength)
        {
            context.Response.ContentLength64 = contentLength;
        }
        else
        {
            context.Response.SendChunked = true;
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        await stream.CopyToAsync(context.Response.OutputStream, cancellationToken);
        context.Response.Close();
    }

    private static void TrySetResponseHeader(HttpListenerResponse response, string name, string value)
    {
        try
        {
            response.Headers[name] = value;
        }
        catch
        {
            // Some framework-controlled headers, such as Date or Server, cannot be
            // copied through HttpListener. The body/status still carry the response.
        }
    }

    public void Dispose()
        => _client.Dispose();
}
