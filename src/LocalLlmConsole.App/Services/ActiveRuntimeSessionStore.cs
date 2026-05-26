
namespace LocalLlmConsole.Services;

public sealed class ActiveRuntimeSessionStore
{
    private readonly string _workspaceRoot;

    public ActiveRuntimeSessionStore(string workspaceRoot)
    {
        _workspaceRoot = workspaceRoot;
    }

    public string SessionPath => Path.Combine(_workspaceRoot, "state", "active-runtime-session.json");

    public async Task<ActiveRuntimeSession?> TryReadAsync(CancellationToken cancellationToken = default)
    {
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
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SessionPath)!);
        await File.WriteAllTextAsync(
            SessionPath,
            JsonSerializer.Serialize(session, new JsonSerializerOptions { WriteIndented = true }),
            cancellationToken);
    }

    public void Clear()
    {
        try
        {
            if (File.Exists(SessionPath)) File.Delete(SessionPath);
        }
        catch
        {
            // Best effort only; stale sessions are revalidated on next startup.
        }
    }
}

