
namespace LocalLlmConsole.Services;

public static class HostExecutableResolver
{
    public static string WslExe()
    {
        foreach (var candidate in WindowsSystemCandidates("wsl.exe"))
        {
            if (File.Exists(candidate)) return candidate;
        }

        throw new FileNotFoundException("wsl.exe was not found in the Windows system directory.", "wsl.exe");
    }

    public static string WindowsPowerShellExe()
    {
        foreach (var directory in WindowsSystemDirectories())
        {
            if (string.IsNullOrWhiteSpace(directory)) continue;
            var candidate = Path.Combine(directory, "WindowsPowerShell", "v1.0", "powershell.exe");
            if (File.Exists(candidate)) return candidate;
        }

        throw new FileNotFoundException("powershell.exe was not found in the Windows system directory.", "powershell.exe");
    }

    public static string GitExe() => ResolveOnPath("git.exe");

    public static string CMakeExe() => ResolveOnPath("cmake.exe");

    public static string NvidiaSmiExe()
    {
        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var nvidia = string.IsNullOrWhiteSpace(programFiles)
            ? ""
            : Path.Combine(programFiles, "NVIDIA Corporation", "NVSMI", "nvidia-smi.exe");
        return !string.IsNullOrWhiteSpace(nvidia) && File.Exists(nvidia)
            ? nvidia
            : ResolveOnPath("nvidia-smi.exe");
    }

    public static string ResolveOnPath(string executableName)
    {
        if (Path.IsPathFullyQualified(executableName))
        {
            if (!File.Exists(executableName)) throw new FileNotFoundException($"Executable not found: {executableName}", executableName);
            return executableName;
        }

        foreach (var directory in PathEntries())
        {
            var candidate = Path.Combine(directory, executableName);
            if (File.Exists(candidate)) return candidate;
        }

        throw new FileNotFoundException($"Executable not found on PATH: {executableName}", executableName);
    }

    private static IEnumerable<string> PathEntries()
    {
        var path = Environment.GetEnvironmentVariable("PATH") ?? "";
        foreach (var part in path.Split(Path.PathSeparator))
        {
            if (string.IsNullOrWhiteSpace(part)) continue;
            var expanded = Environment.ExpandEnvironmentVariables(part.Trim().Trim('"'));
            if (!Path.IsPathFullyQualified(expanded)) continue;
            if (Directory.Exists(expanded)) yield return Path.GetFullPath(expanded);
        }
    }

    private static IEnumerable<string> WindowsSystemCandidates(string executableName)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var directory in WindowsSystemDirectories())
        {
            if (string.IsNullOrWhiteSpace(directory)) continue;
            var candidate = Path.Combine(directory, executableName);
            if (seen.Add(candidate)) yield return candidate;
        }
    }

    private static IEnumerable<string> WindowsSystemDirectories()
    {
        yield return Environment.SystemDirectory;

        var windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        if (string.IsNullOrWhiteSpace(windows)) yield break;

        yield return Path.Combine(windows, "System32");
        yield return Path.Combine(windows, "Sysnative");
    }
}
