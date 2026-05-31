namespace LocalLlmConsole.Services;

public sealed partial class OpenCodeConfigService
{
    private string SettingsPath() => Path.Combine(_workspaceRoot, "settings", "opencode-integration.json");

    private IEnumerable<string> CandidateConfigPaths()
    {
        foreach (var root in CandidateProjectRoots())
        {
            yield return Path.Combine(root, ".opencode", "opencode.jsonc");
            yield return Path.Combine(root, ".opencode", "opencode.json");
            yield return Path.Combine(root, "opencode.jsonc");
            yield return Path.Combine(root, "opencode.json");
        }

        foreach (var root in CandidateGlobalOpenCodeDirectories())
        {
            yield return Path.Combine(root, "opencode.jsonc");
            yield return Path.Combine(root, "opencode.json");
        }
    }

    private IEnumerable<string> CandidateAgentDirectories()
    {
        foreach (var root in CandidateProjectRoots())
        {
            yield return Path.Combine(root, ".opencode", "agent");
            yield return Path.Combine(root, ".opencode", "agents");
        }

        foreach (var root in CandidateGlobalOpenCodeDirectories())
        {
            yield return Path.Combine(root, "agent");
            yield return Path.Combine(root, "agents");
        }
    }

    private IEnumerable<string> CandidateProjectRoots()
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var seed in new[] { Environment.CurrentDirectory, _workspaceRoot, AppContext.BaseDirectory })
        {
            if (string.IsNullOrWhiteSpace(seed)) continue;
            foreach (var root in Ancestors(seed))
            {
                if (seen.Add(root)) yield return root;
                if (Directory.Exists(Path.Combine(root, ".git"))) break;
            }
        }
    }

    private static IEnumerable<string> Ancestors(string seed)
    {
        var full = Path.GetFullPath(seed);
        var directory = Directory.Exists(full) ? new DirectoryInfo(full) : Directory.GetParent(full);
        while (directory is not null)
        {
            yield return directory.FullName;
            directory = directory.Parent;
        }
    }

    private static IEnumerable<string> CandidateGlobalOpenCodeDirectories()
    {
        yield return DefaultOpenCodeDirectory();

        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        if (!string.IsNullOrWhiteSpace(appData)) yield return Path.Combine(appData, "opencode");

        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (!string.IsNullOrWhiteSpace(localAppData)) yield return Path.Combine(localAppData, "opencode");
    }

    private static string DefaultConfigPath() => Path.Combine(DefaultOpenCodeDirectory(), "opencode.jsonc");

    private static string DefaultOpenCodeDirectory()
    {
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Path.Combine(string.IsNullOrWhiteSpace(userProfile) ? Environment.CurrentDirectory : userProfile, ".config", "opencode");
    }

    private static bool IsPathInside(string root, string path)
    {
        var fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var fullPath = Path.GetFullPath(path);
        return fullPath.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase);
    }

    public static string SafeOpenCodeId(string value)
    {
        var safe = Regex.Replace((value ?? "").Trim().ToLowerInvariant(), @"[^a-z0-9._-]+", "-").Trim('-', '.', '_');
        return string.IsNullOrWhiteSpace(safe) ? "local-model" : safe[..Math.Min(96, safe.Length)];
    }
}
