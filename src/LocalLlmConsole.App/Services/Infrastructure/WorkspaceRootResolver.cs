
namespace LocalLlmConsole.Services;

public static class WorkspaceRootResolver
{
    public const string EnvironmentVariable = "LLAMA_CPP_WINDOWS_MANAGER_WORKSPACE";
    public const string LegacyConsoleEnvironmentVariable = "LLAMA_CPP_CONSOLE_WORKSPACE";
    public const string LegacyEnvironmentVariable = "LOCAL_LLM_CONSOLE_WORKSPACE";

    public static string Resolve()
        => Resolve(
            FirstConfiguredWorkspace(),
            Environment.ProcessPath,
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));

    public static string Resolve(string? configuredWorkspace, string? executablePath, string localAppDataRoot)
    {
        if (!string.IsNullOrWhiteSpace(configuredWorkspace))
            return Path.GetFullPath(configuredWorkspace);

        var executableDirectory = string.IsNullOrWhiteSpace(executablePath)
            ? ""
            : Path.GetDirectoryName(Path.GetFullPath(executablePath)) ?? "";
        var portableRoot = TryCreatePortableWorkspace(executableDirectory);
        if (!string.IsNullOrWhiteSpace(portableRoot))
            return portableRoot;

        var fallbackRoot = string.IsNullOrWhiteSpace(localAppDataRoot)
            ? AppContext.BaseDirectory
            : localAppDataRoot;
        var preferredFallback = Path.Combine(fallbackRoot, "llama.cpp Windows Manager");
        var legacyProductFallback = Path.Combine(fallbackRoot, "llama.cpp Console");
        var legacyCodeFallback = Path.Combine(fallbackRoot, "LocalLlmConsole");
        if (!Directory.Exists(preferredFallback))
        {
            if (Directory.Exists(legacyProductFallback))
                return Path.GetFullPath(legacyProductFallback);
            if (Directory.Exists(legacyCodeFallback))
                return Path.GetFullPath(legacyCodeFallback);
        }

        return Path.GetFullPath(preferredFallback);
    }

    private static string? FirstConfiguredWorkspace()
    {
        var configured = Environment.GetEnvironmentVariable(EnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(configured)) return configured;
        configured = Environment.GetEnvironmentVariable(LegacyConsoleEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(configured)) return configured;
        return Environment.GetEnvironmentVariable(LegacyEnvironmentVariable);
    }

    private static string TryCreatePortableWorkspace(string executableDirectory)
    {
        if (string.IsNullOrWhiteSpace(executableDirectory)) return "";
        var dataRoot = Path.GetFullPath(Path.Combine(executableDirectory, "data"));
        try
        {
            Directory.CreateDirectory(dataRoot);
            var probe = Path.Combine(dataRoot, $".write-test-{Guid.NewGuid():N}.tmp");
            File.WriteAllText(probe, "");
            File.Delete(probe);
            return dataRoot;
        }
        catch
        {
            return "";
        }
    }
}
