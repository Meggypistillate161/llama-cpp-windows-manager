
namespace LocalLlmConsole.Services;

public static class RuntimeBuildToolService
{
    public static ProcessStartInfo CreateBuildProcessStartInfo(
        string powershellExe,
        string scriptPath,
        string sourceDir,
        string buildDir,
        string installDir,
        RuntimeBuildPreset preset,
        string wslDistro,
        string processMarker,
        string wslExe,
        bool noUpdate)
    {
        var psi = new ProcessStartInfo(powershellExe)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        foreach (var arg in new[] { "-NoProfile", "-ExecutionPolicy", "Bypass", "-File", scriptPath, "-RepoUrl", preset.RepoUrl })
            psi.ArgumentList.Add(arg);
        if (!string.IsNullOrWhiteSpace(preset.Branch))
        {
            psi.ArgumentList.Add("-Branch");
            psi.ArgumentList.Add(preset.Branch);
        }
        foreach (var arg in new[] { "-SourceDir", sourceDir, "-BuildDir", buildDir, "-InstallDir", installDir, "-Runtime", "wsl", "-WslDistro", wslDistro, "-Clean" })
            psi.ArgumentList.Add(arg);
        foreach (var arg in new[] { "-ProcessMarker", processMarker, "-WslExe", wslExe })
            psi.ArgumentList.Add(arg);
        var backend = RuntimeBuildCatalogService.BuildBackend(preset);
        if (backend == RuntimeBackend.Cuda) psi.ArgumentList.Add("-Cuda");
        if (backend == RuntimeBackend.Vulkan) psi.ArgumentList.Add("-Vulkan");
        if (noUpdate) psi.ArgumentList.Add("-NoUpdate");
        return psi;
    }
}
