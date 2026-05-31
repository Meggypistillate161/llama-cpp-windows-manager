namespace LocalLlmConsole.Services;

public static class ModelGatewayResponseWriter
{
    public static async Task WriteJsonAsync(HttpListenerContext context, int status, object value, CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(value, new JsonSerializerOptions { WriteIndented = true });
        var bytes = Encoding.UTF8.GetBytes(json);
        context.Response.StatusCode = status;
        context.Response.ContentType = "application/json; charset=utf-8";
        context.Response.ContentLength64 = bytes.Length;
        await context.Response.OutputStream.WriteAsync(bytes, cancellationToken);
        context.Response.Close();
    }

    public static void TryClose(HttpListenerResponse response)
    {
        try { response.Close(); } catch { }
    }

    public static object ModelsResponse(IReadOnlyList<ModelRecord> models)
        => new
        {
            @object = "list",
            data = models
                .OrderBy(model => model.Name, StringComparer.OrdinalIgnoreCase)
                .Select(model => new
                {
                    id = model.Id,
                    @object = "model",
                    created = model.UpdatedAt.ToUnixTimeSeconds(),
                    owned_by = "local-llm-console",
                    name = model.Name
                })
                .ToArray()
        };

    public static object GatewayError(string message, string type, string code)
        => new { error = new { message, type, code } };

    public static string GatewayClientLoadError(ModelRecord model, string requestedModel, Exception ex)
    {
        var requested = string.Equals(requestedModel, model.Id, StringComparison.OrdinalIgnoreCase)
            ? model.Id
            : $"{requestedModel} -> {model.Id}";
        var message = string.IsNullOrWhiteSpace(ex.Message) ? InnermostMessage(ex) : ex.Message;
        return $"Auto-load gateway could not load {model.Name} ({requested}). {message}";
    }

    public static string InnermostMessage(Exception ex)
    {
        while (ex.InnerException is not null)
            ex = ex.InnerException;
        return string.IsNullOrWhiteSpace(ex.Message) ? ex.GetType().Name : ex.Message;
    }

    public static object[] RunningModelRows(IReadOnlyList<LoadedModelSessionSnapshot> sessions)
        => sessions
            .Where(session => session.IsRunning)
            .Select(session => new
            {
                id = session.ModelId,
                name = session.ModelName,
                endpoint = session.Endpoint,
                status = session.Status.ToString(),
                runtime = session.RuntimeName,
                startedAt = session.StartedAt
            })
            .ToArray();
}
