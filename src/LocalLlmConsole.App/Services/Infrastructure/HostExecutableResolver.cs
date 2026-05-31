
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

    public static string CMakeExe()
    {
        try
        {
            return ResolveOnPath("cmake.exe");
        }
        catch (FileNotFoundException)
        {
        }

        foreach (var candidate in CMakeCandidates())
        {
            if (File.Exists(candidate)) return candidate;
        }

        throw new FileNotFoundException("cmake.exe was not found on PATH, in the standalone CMake install folder, or in Visual Studio Build Tools.", "cmake.exe");
    }

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

    private static IEnumerable<string> CMakeCandidates()
    {
        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        if (!string.IsNullOrWhiteSpace(programFiles))
            yield return Path.Combine(programFiles, "CMake", "bin", "cmake.exe");

        var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        if (!string.IsNullOrWhiteSpace(programFilesX86))
            yield return Path.Combine(programFilesX86, "CMake", "bin", "cmake.exe");

        foreach (var installPath in VisualStudioInstallPaths())
            yield return Path.Combine(installPath, "Common7", "IDE", "CommonExtensions", "Microsoft", "CMake", "CMake", "bin", "cmake.exe");
    }

    private static IEnumerable<string> VisualStudioInstallPaths()
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var vswhere = VsWherePath();
        if (!string.IsNullOrWhiteSpace(vswhere))
        {
            foreach (var path in RunVsWhere(vswhere))
            {
                if (seen.Add(path)) yield return path;
            }
        }

        var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        var root = string.IsNullOrWhiteSpace(programFilesX86)
            ? ""
            : Path.Combine(programFilesX86, "Microsoft Visual Studio");
        if (Directory.Exists(root))
        {
            foreach (var path in Directory.EnumerateDirectories(root, "*", SearchOption.AllDirectories)
                         .Where(path => File.Exists(Path.Combine(path, "Common7", "IDE", "devenv.exe"))
                             || File.Exists(Path.Combine(path, "Common7", "Tools", "VsDevCmd.bat"))
                             || Directory.Exists(Path.Combine(path, "VC", "Tools", "MSVC"))))
            {
                if (seen.Add(path)) yield return path;
            }
        }
    }

    private static IEnumerable<string> RunVsWhere(string vswhere)
    {
        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo(vswhere)
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                }
            };
            foreach (var arg in new[] { "-products", "*", "-requires", "Microsoft.VisualStudio.Component.VC.Tools.x86.x64", "-property", "installationPath" })
                process.StartInfo.ArgumentList.Add(arg);
            if (!process.Start()) yield break;
            var output = process.StandardOutput.ReadToEnd();
            if (!process.WaitForExit(5000) || process.ExitCode != 0) yield break;
            foreach (var line in output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
            {
                var path = line.Trim();
                if (Directory.Exists(path)) yield return path;
            }
        }
        finally
        {
        }
    }

    private static string VsWherePath()
    {
        var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        if (string.IsNullOrWhiteSpace(programFilesX86)) return "";
        var path = Path.Combine(programFilesX86, "Microsoft Visual Studio", "Installer", "vswhere.exe");
        return File.Exists(path) ? path : "";
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
