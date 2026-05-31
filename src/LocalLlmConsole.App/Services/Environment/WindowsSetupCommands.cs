namespace LocalLlmConsole.Services;

public static class WindowsSetupCommands
{
    public const string GitWingetId = "Git.Git";
    public const string CMakeWingetId = "Kitware.CMake";
    public const string VisualStudioBuildToolsWingetId = "Microsoft.VisualStudio.2022.BuildTools";
    public const string CudaWingetId = "Nvidia.CUDA";
    public const string VulkanSdkWingetId = "KhronosGroup.VulkanSDK";
    public const string OneApiBaseToolkitWingetId = "Intel.OneAPI.BaseToolkit";

    public static string InstallCpuToolsPowerShell() => WingetInstallScript(
        "Windows CPU build tools",
        [
            (GitWingetId, ""),
            (CMakeWingetId, ""),
            (VisualStudioBuildToolsWingetId, "--wait --passive --add Microsoft.VisualStudio.Workload.VCTools --includeRecommended")
        ],
        [
            "https://git-scm.com/download/win",
            "https://cmake.org/download/",
            "https://visualstudio.microsoft.com/downloads/#build-tools-for-visual-studio-2022"
        ]);

    public static string InstallCudaPowerShell() => WingetInstallScript(
        "NVIDIA CUDA Toolkit",
        [(CudaWingetId, "")],
        ["https://developer.nvidia.com/cuda-downloads"]);

    public static string InstallVulkanPowerShell() => WingetInstallScript(
        "Vulkan SDK",
        [(VulkanSdkWingetId, "")],
        ["https://vulkan.lunarg.com/sdk/home#windows"]);

    public static string InstallOneApiPowerShell() => WingetInstallScript(
        "Intel oneAPI Base Toolkit",
        [(OneApiBaseToolkitWingetId, "")],
        [
            "https://www.intel.com/content/www/us/en/developer/tools/oneapi/base-toolkit-download.html",
            "https://www.intel.com/content/www/us/en/download-center/home.html"
        ]);

    private static string WingetInstallScript(
        string title,
        IReadOnlyList<(string Id, string Override)> packages,
        IReadOnlyList<string> fallbackUrls)
    {
        var lines = new List<string>
        {
            "$ErrorActionPreference = 'Continue'",
            $"Write-Host 'Installing {EscapeSingleQuoted(title)}...'",
            "if (Get-Command winget -CommandType Application -ErrorAction SilentlyContinue) {"
        };

        foreach (var (id, overrideArgs) in packages)
        {
            var command = $"  winget install --id {id} --exact --accept-package-agreements --accept-source-agreements";
            if (!string.IsNullOrWhiteSpace(overrideArgs))
                command += $" --override {CommandLineService.PowerShellQuote(overrideArgs)}";
            lines.Add(command);
        }

        lines.Add("} else {");
        lines.Add("  Write-Host 'winget was not found. Opening official installer pages instead.'");
        foreach (var url in fallbackUrls)
            lines.Add($"  Start-Process {CommandLineService.PowerShellQuote(url)}");
        lines.Add("}");
        lines.Add("Write-Host ''");
        lines.Add("Write-Host 'After installers finish, restart llama.cpp Windows Manager so PATH and toolchain environment variables are refreshed.'");
        lines.Add("Read-Host 'Press Enter to close'");
        return string.Join(Environment.NewLine, lines);
    }

    private static string EscapeSingleQuoted(string value) => value.Replace("'", "''", StringComparison.Ordinal);
}
