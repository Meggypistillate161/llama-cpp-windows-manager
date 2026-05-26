
namespace LocalLlmConsole.Services;

public sealed class LocalAppService : IAsyncDisposable
{
    private readonly HttpListener _listener = new();
    private readonly CancellationTokenSource _stop = new();
    private readonly ApiSecurity _security = new();
    private readonly object _requestHandlersLock = new();
    private readonly HashSet<Task> _requestHandlers = [];
    private readonly StateStore _stateStore;
    private readonly JobEngine _jobs;
    private Task? _loop;

    public Uri BaseUri { get; }

    public LocalAppService(StateStore stateStore, JobEngine jobs, int port)
    {
        _stateStore = stateStore;
        _jobs = jobs;
        BaseUri = new Uri($"http://127.0.0.1:{port}/");
        _listener.Prefixes.Add(BaseUri.ToString());
    }

    public async Task StartAsync()
    {
        await _jobs.RecoverAfterRestartAsync();
        _listener.Start();
        _loop = Task.Run(() => ListenAsync(_stop.Token));
    }

    private async Task ListenAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested && _listener.IsListening)
        {
            HttpListenerContext context;
            try
            {
                context = await _listener.GetContextAsync();
            }
            catch when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            QueueRequest(context, cancellationToken);
        }
    }

    private void QueueRequest(HttpListenerContext context, CancellationToken cancellationToken)
    {
        var task = Task.Run(() => HandleAsync(context), cancellationToken);
        lock (_requestHandlersLock)
        {
            _requestHandlers.Add(task);
        }

        task.ContinueWith(
            completed =>
            {
                lock (_requestHandlersLock)
                {
                    _requestHandlers.Remove(completed);
                }
                _ = completed.Exception;
            },
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    private async Task HandleAsync(HttpListenerContext context)
    {
        try
        {
            AddSecurityHeaders(context);
            if (!_security.IsLocalHostHeaderAllowed(context.Request.Headers["Host"], BaseUri.Port))
            {
                await WriteJsonAsync(context, 403, new { ok = false, error = "Non-local Host header rejected." });
                return;
            }

            if (context.Request.HttpMethod.Equals("OPTIONS", StringComparison.OrdinalIgnoreCase))
            {
                context.Response.StatusCode = 204;
                context.Response.Close();
                return;
            }

            var path = context.Request.Url?.AbsolutePath ?? "/";
            if (!_security.IsLocalOriginAllowed(context.Request.Headers["Origin"]))
            {
                await WriteJsonAsync(context, 403, new { ok = false, error = "Non-local browser origin rejected." });
                return;
            }
            if (path.StartsWith("/api/", StringComparison.OrdinalIgnoreCase)
                && path != "/api/health"
                && !_security.IsAuthorized(context.Request.Headers["Authorization"]))
            {
                await WriteJsonAsync(context, 401, new { ok = false, error = "Missing or invalid local API token." });
                return;
            }

            if (path == "/" || path == "/api/health")
            {
                await WriteJsonAsync(context, 200, new { ok = true, app = "llama.cpp Console", auth = "required", tokenHint = "WPF shell holds token in memory" });
                return;
            }
            if (path == "/api/jobs")
            {
                await WriteJsonAsync(context, 200, new { ok = true, jobs = RedactedJobs(await _stateStore.ListJobsAsync()) });
                return;
            }

            await WriteJsonAsync(context, 404, new { ok = false, error = "Not found." });
        }
        catch
        {
            try { await WriteJsonAsync(context, 500, new { ok = false, error = "Internal server error." }); }
            catch { }
        }
    }

    private static object[] RedactedJobs(IReadOnlyList<LocalLlmConsole.Models.JobRecord> jobs)
        => jobs.Select(job => new
        {
            job.Id,
            job.Kind,
            Status = job.Status.ToString(),
            LogFile = string.IsNullOrWhiteSpace(job.LogPath) ? "" : Path.GetFileName(job.LogPath),
            job.CreatedAt,
            job.UpdatedAt
        }).ToArray();

    private void AddSecurityHeaders(HttpListenerContext context)
    {
        var origin = context.Request.Headers["Origin"];
        if (!string.IsNullOrWhiteSpace(origin) && _security.IsLocalOriginAllowed(origin))
            context.Response.Headers["Access-Control-Allow-Origin"] = origin;
        context.Response.Headers["Access-Control-Allow-Methods"] = "GET,POST,OPTIONS";
        context.Response.Headers["Access-Control-Allow-Headers"] = "content-type,authorization";
        context.Response.Headers["Vary"] = "Origin";
        context.Response.Headers["Cache-Control"] = "no-store";
        context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    }

    private static async Task WriteJsonAsync(HttpListenerContext context, int status, object value)
    {
        var json = JsonSerializer.Serialize(value, new JsonSerializerOptions { WriteIndented = true });
        var bytes = Encoding.UTF8.GetBytes(json);
        context.Response.StatusCode = status;
        context.Response.ContentType = "application/json; charset=utf-8";
        context.Response.ContentLength64 = bytes.Length;
        await context.Response.OutputStream.WriteAsync(bytes);
        context.Response.Close();
    }

    public async ValueTask DisposeAsync()
    {
        _stop.Cancel();
        if (_listener.IsListening) _listener.Stop();
        _listener.Close();
        if (_loop is not null)
        {
            var completed = await Task.WhenAny(_loop, Task.Delay(1000));
            if (completed == _loop) await ObserveCompletionAsync(_loop);
        }

        Task[] activeHandlers;
        lock (_requestHandlersLock)
        {
            activeHandlers = _requestHandlers.ToArray();
        }

        if (activeHandlers.Length > 0)
        {
            var allHandlers = Task.WhenAll(activeHandlers);
            var completed = await Task.WhenAny(allHandlers, Task.Delay(1000));
            if (completed == allHandlers) await ObserveCompletionAsync(allHandlers);
        }

        _stop.Dispose();
    }

    private static async Task ObserveCompletionAsync(Task task)
    {
        try
        {
            await task;
        }
        catch
        {
        }
    }
}
