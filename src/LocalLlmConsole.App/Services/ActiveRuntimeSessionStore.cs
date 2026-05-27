
namespace LocalLlmConsole.Services;

public sealed class ActiveRuntimeSessionStore
{
    private readonly string _workspaceRoot;

    public ActiveRuntimeSessionStore(string workspaceRoot)
    {
        _workspaceRoot = workspaceRoot;
    }

    public string SessionPath => Path.Combine(_workspaceRoot, "state", "active-runtime-session.json");
    public string SessionsPath => Path.Combine(_workspaceRoot, "state", "active-runtime-sessions.json");

    public async Task<IReadOnlyList<ActiveRuntimeSession>> ReadAllAsync(CancellationToken cancellationToken = default)
    {
        if (File.Exists(SessionsPath))
        {
            try
            {
                var json = await File.ReadAllTextAsync(SessionsPath, cancellationToken);
                return JsonSerializer.Deserialize<List<ActiveRuntimeSession>>(json) ?? [];
            }
            catch
            {
                Clear();
                return [];
            }
        }

        var legacy = await TryReadAsync(cancellationToken);
        return legacy is null ? [] : [legacy];
    }

    public async Task<ActiveRuntimeSession?> TryReadAsync(CancellationToken cancellationToken = default)
    {
        if (File.Exists(SessionsPath))
        {
            try
            {
                var json = await File.ReadAllTextAsync(SessionsPath, cancellationToken);
                var sessions = JsonSerializer.Deserialize<List<ActiveRuntimeSession>>(json) ?? [];
                return sessions.FirstOrDefault(session => session.IsSelected) ?? sessions.FirstOrDefault();
            }
            catch
            {
                Clear();
                return null;
            }
        }

        if (!File.Exists(SessionPath)) return null;
        try
        {
            var json = await File.ReadAllTextAsync(SessionPath, cancellationToken);
            return JsonSerializer.Deserialize<ActiveRuntimeSession>(json);
        }
        catch
        {
            Clear();
            return null;
        }
    }

    public async Task SaveAsync(ActiveRuntimeSession session, CancellationToken cancellationToken = default)
        => await SaveAllAsync([session], cancellationToken);

    public async Task SaveAllAsync(IReadOnlyList<ActiveRuntimeSession> sessions, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SessionsPath)!);
        await File.WriteAllTextAsync(
            SessionsPath,
            JsonSerializer.Serialize(sessions, new JsonSerializerOptions { WriteIndented = true }),
            cancellationToken);
        try
        {
            if (File.Exists(SessionPath)) File.Delete(SessionPath);
        }
        catch
        {
        }
    }

    public void Clear()
    {
        try
        {
            if (File.Exists(SessionPath)) File.Delete(SessionPath);
            if (File.Exists(SessionsPath)) File.Delete(SessionsPath);
        }
        catch
        {
            // Best effort only; stale sessions are revalidated on next startup.
        }
    }
}

