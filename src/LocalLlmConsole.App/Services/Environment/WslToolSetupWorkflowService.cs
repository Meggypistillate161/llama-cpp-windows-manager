namespace LocalLlmConsole.Services;

public enum WslToolSetupAction
{
    InstallWsl,
    InstallUbuntu,
    CheckWslUpdates,
    DeleteWsl,
    CheckUbuntuUpdates,
    DeleteUbuntu,
    InstallUbuntuBuildTools,
    DeleteUbuntuBuildTools,
    InstallUbuntuCudaToolkit,
    DeleteUbuntuCudaToolkit,
    InstallUbuntuVulkanTools,
    DeleteUbuntuVulkanTools,
    InstallUbuntuSyclRuntime,
    DeleteUbuntuSyclRuntime,
    InstallUbuntuSyclOneApi,
    DeleteUbuntuSyclOneApi
}

public enum WslToolSetupLaunchKind
{
    WindowsCommand,
    PowerShellScript,
    WslBashScript
}

public sealed record WslToolSetupPlan(
    WslToolSetupAction Action,
    WslToolSetupLaunchKind LaunchKind,
    string Title,
    string ConfirmationMessage,
    bool IsWarning,
    bool Elevated,
    string StartedStatus,
    string Executable = "",
    IReadOnlyList<string>? Arguments = null,
    string PowerShellScript = "",
    string DistroName = "",
    string BashScript = "");

public sealed class WslToolSetupWorkflowService
{
    private readonly VisibleCommandLaunchService _commandLauncher;
    private readonly Func<string> _wslExe;

    public WslToolSetupWorkflowService(
        VisibleCommandLaunchService commandLauncher,
        Func<string>? wslExe = null)
    {
        _commandLauncher = commandLauncher ?? throw new ArgumentNullException(nameof(commandLauncher));
        _wslExe = wslExe ?? HostExecutableResolver.WslExe;
    }

    public bool RequiresUbuntuDistro(WslToolSetupAction action)
        => action is WslToolSetupAction.CheckUbuntuUpdates
            or WslToolSetupAction.DeleteUbuntu
            or WslToolSetupAction.InstallUbuntuBuildTools
            or WslToolSetupAction.DeleteUbuntuBuildTools
            or WslToolSetupAction.InstallUbuntuCudaToolkit
            or WslToolSetupAction.DeleteUbuntuCudaToolkit
            or WslToolSetupAction.InstallUbuntuVulkanTools
            or WslToolSetupAction.DeleteUbuntuVulkanTools
            or WslToolSetupAction.InstallUbuntuSyclRuntime
            or WslToolSetupAction.DeleteUbuntuSyclRuntime
            or WslToolSetupAction.InstallUbuntuSyclOneApi
            or WslToolSetupAction.DeleteUbuntuSyclOneApi;

    public WslToolSetupPlan Plan(WslToolSetupAction action, string distroName = "", string appDisplayName = "llama.cpp Windows Manager")
    {
        if (RequiresUbuntuDistro(action) && string.IsNullOrWhiteSpace(distroName))
            throw new ArgumentException("An Ubuntu distro name is required for this WSL action.", nameof(distroName));

        return action switch
        {
            WslToolSetupAction.InstallWsl => WindowsCommandPlan(
                action,
                "Install WSL",
                "This opens an elevated PowerShell window and runs:\n\nwsl.exe --install --no-distribution\n\nWindows may ask for administrator approval or a reboot.",
                ["--install", "--no-distribution"],
                elevated: true,
                "WSL install started in a PowerShell window."),
            WslToolSetupAction.InstallUbuntu => PowerShellPlan(
                action,
                "Install Ubuntu for WSL",
                $"This opens an elevated PowerShell window and runs:\n\nwsl.exe --install -d {WslSetupCommands.RecommendedUbuntuDistro}\n\nThen, if Ubuntu is ready, it installs the CPU build toolchain:\n\nsudo apt install -y {WslSetupCommands.BuildToolsPackages}\n\nWindows may ask for administrator approval, a reboot, or first-run Ubuntu account setup.",
                WslSetupCommands.InstallUbuntuAndBuildToolsPowerShell(_wslExe()),
                elevated: true,
                isWarning: false,
                "WSL Ubuntu install and build-tool setup started in a PowerShell window."),
            WslToolSetupAction.CheckWslUpdates => WindowsCommandPlan(
                action,
                "Check WSL updates",
                "This opens an elevated PowerShell window and runs:\n\nwsl.exe --update",
                ["--update"],
                elevated: true,
                "WSL update check started in a PowerShell window."),
            WslToolSetupAction.DeleteWsl => PowerShellPlan(
                action,
                "Delete WSL",
                "This opens an elevated PowerShell window to uninstall the Windows Subsystem for Linux package.\n\nThis can break all WSL-based runtimes until WSL is installed again. Existing distro data is not intentionally unregistered by this action, but WSL will be unavailable.",
                WslSetupCommands.DeleteWslPowerShell(_wslExe()),
                elevated: true,
                isWarning: true,
                "WSL uninstall started in a PowerShell window."),
            WslToolSetupAction.CheckUbuntuUpdates => WslBashPlan(
                action,
                "Check Ubuntu updates",
                $"This opens PowerShell and checks available package updates inside {distroName}:\n\nsudo apt update && apt list --upgradable",
                distroName,
                "sudo apt update && apt list --upgradable",
                isWarning: false,
                $"Ubuntu update check started for {distroName}."),
            WslToolSetupAction.DeleteUbuntu => PowerShellPlan(
                action,
                "Delete Ubuntu distro",
                $"This will unregister and delete the Ubuntu distro root filesystem:\n\n{distroName}\n\nThis deletes files inside that WSL distro. It does not delete models stored in the Windows app data folder.",
                WslSetupCommands.DeleteUbuntuPowerShell(_wslExe(), distroName),
                elevated: false,
                isWarning: true,
                $"Ubuntu unregister started for {distroName}."),
            WslToolSetupAction.InstallUbuntuBuildTools => WslBashPlan(
                action,
                "Install Ubuntu CPU build tools",
                $"This opens PowerShell and installs the CPU build toolchain inside {distroName}:\n\nsudo apt update\nsudo apt install -y {WslSetupCommands.BuildToolsPackages}\n\nUbuntu may ask for your Linux sudo password. CUDA runtime builds still require a separate NVIDIA CUDA Toolkit/runtime library install inside WSL.",
                distroName,
                WslSetupCommands.InstallBuildToolsCommand,
                isWarning: false,
                $"Ubuntu CPU build tool install started for {distroName}."),
            WslToolSetupAction.DeleteUbuntuBuildTools => WslBashPlan(
                action,
                "Delete Ubuntu CPU build tools",
                $"This opens PowerShell and removes the CPU build packages from {distroName}:\n\nsudo apt remove -y {WslSetupCommands.BuildToolsPackages}\n\nThese packages may also be used outside {appDisplayName}.",
                distroName,
                WslSetupCommands.RemoveBuildToolsCommand,
                isWarning: true,
                $"Ubuntu CPU build tool removal started for {distroName}."),
            WslToolSetupAction.InstallUbuntuCudaToolkit => WslBashPlan(
                action,
                "Install WSL CUDA Toolkit",
                $"This opens PowerShell and installs NVIDIA's WSL CUDA Toolkit inside {distroName}:\n\n{WslSetupCommands.CudaToolkitPackage}\n\nIt adds NVIDIA's WSL CUDA apt keyring from developer.download.nvidia.com, runs apt update, installs the toolkit, and verifies both nvcc and libcudart. Ubuntu may ask for your Linux sudo password. The Windows NVIDIA driver with WSL GPU support must already be installed.",
                distroName,
                WslSetupCommands.InstallCudaToolkitCommand,
                isWarning: false,
                $"WSL CUDA Toolkit install started for {distroName}."),
            WslToolSetupAction.DeleteUbuntuCudaToolkit => WslBashPlan(
                action,
                "Delete WSL CUDA Toolkit",
                $"This opens PowerShell and removes NVIDIA CUDA Toolkit packages from {distroName}:\n\nsudo apt remove -y {WslSetupCommands.CudaRemovePackages}\n\nThese CUDA packages may also be used outside {appDisplayName}.",
                distroName,
                WslSetupCommands.RemoveCudaToolkitCommand,
                isWarning: true,
                $"WSL CUDA Toolkit removal started for {distroName}."),
            WslToolSetupAction.InstallUbuntuVulkanTools => WslBashPlan(
                action,
                "Install WSL Vulkan tools",
                $"This opens PowerShell and installs Ubuntu Vulkan packages inside {distroName}:\n\nsudo apt install -y {WslSetupCommands.VulkanToolsPackages}\n\nIt then runs vulkaninfo --summary to verify a usable Vulkan driver/device before llama.cpp Vulkan builds are attempted. Ubuntu may ask for your Linux sudo password. Your Windows GPU driver and WSL graphics stack must expose Vulkan inside WSL.",
                distroName,
                WslSetupCommands.InstallVulkanToolsCommand,
                isWarning: false,
                $"WSL Vulkan tool install started for {distroName}."),
            WslToolSetupAction.DeleteUbuntuVulkanTools => WslBashPlan(
                action,
                "Delete WSL Vulkan tools",
                $"This opens PowerShell and removes Ubuntu Vulkan packages from {distroName}:\n\nsudo apt remove -y {WslSetupCommands.VulkanToolsPackages}\n\nThese Vulkan packages may also be used outside {appDisplayName}.",
                distroName,
                WslSetupCommands.RemoveVulkanToolsCommand,
                isWarning: true,
                $"WSL Vulkan tool removal started for {distroName}."),
            WslToolSetupAction.InstallUbuntuSyclRuntime => WslBashPlan(
                action,
                "Install WSL Intel GPU runtime",
                $"This opens PowerShell and installs Intel Level Zero/OpenCL runtime packages inside {distroName}:\n\nsudo apt install -y {WslSetupCommands.SyclRuntimePackages}\n\nThese packages let sycl-ls see Intel Arc and supported Intel GPUs inside WSL.",
                distroName,
                WslSetupCommands.InstallSyclRuntimeCommand,
                isWarning: false,
                $"WSL Intel GPU runtime install started for {distroName}."),
            WslToolSetupAction.DeleteUbuntuSyclRuntime => WslBashPlan(
                action,
                "Delete WSL Intel GPU runtime",
                $"This opens PowerShell and removes Intel GPU runtime packages from {distroName}:\n\nsudo apt remove -y {WslSetupCommands.SyclRuntimePackages}\n\nThese packages may also be used outside {appDisplayName}.",
                distroName,
                WslSetupCommands.RemoveSyclRuntimeCommand,
                isWarning: true,
                $"WSL Intel GPU runtime removal started for {distroName}."),
            WslToolSetupAction.InstallUbuntuSyclOneApi => WslBashPlan(
                action,
                "Install WSL Intel oneAPI",
                $"This opens PowerShell and installs Intel oneAPI DPC++ compiler, MKL, and DNNL packages inside {distroName}:\n\nsudo apt install -y {WslSetupCommands.SyclOneApiPackages}\n\nIt adds Intel's oneAPI apt repository, sources setvars.sh, and runs sycl-ls to verify the toolchain.",
                distroName,
                WslSetupCommands.InstallSyclOneApiCommand,
                isWarning: false,
                $"WSL Intel oneAPI install started for {distroName}."),
            WslToolSetupAction.DeleteUbuntuSyclOneApi => WslBashPlan(
                action,
                "Delete WSL Intel oneAPI",
                $"This opens PowerShell and removes Intel oneAPI packages from {distroName}:\n\nsudo apt remove -y {WslSetupCommands.SyclOneApiPackages}\n\nThese packages may also be used outside {appDisplayName}.",
                distroName,
                WslSetupCommands.RemoveSyclOneApiCommand,
                isWarning: true,
                $"WSL Intel oneAPI removal started for {distroName}."),
            _ => throw new ArgumentOutOfRangeException(nameof(action), action, "Unknown WSL setup action.")
        };
    }

    public void Execute(WslToolSetupPlan plan)
    {
        switch (plan.LaunchKind)
        {
            case WslToolSetupLaunchKind.WindowsCommand:
                _commandLauncher.StartVisibleWindowsCommand(plan.Executable, plan.Arguments ?? [], plan.Elevated);
                break;
            case WslToolSetupLaunchKind.PowerShellScript:
                _commandLauncher.StartVisiblePowerShellScript(plan.PowerShellScript, plan.Elevated);
                break;
            case WslToolSetupLaunchKind.WslBashScript:
                _commandLauncher.StartVisibleWslBashScript(plan.DistroName, plan.BashScript, plan.Elevated);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(plan), plan.LaunchKind, "Unknown WSL setup launch kind.");
        }
    }

    private WslToolSetupPlan WindowsCommandPlan(
        WslToolSetupAction action,
        string title,
        string confirmationMessage,
        IReadOnlyList<string> arguments,
        bool elevated,
        string startedStatus)
        => new(
            action,
            WslToolSetupLaunchKind.WindowsCommand,
            title,
            confirmationMessage,
            IsWarning: false,
            elevated,
            startedStatus,
            Executable: _wslExe(),
            Arguments: arguments);

    private static WslToolSetupPlan PowerShellPlan(
        WslToolSetupAction action,
        string title,
        string confirmationMessage,
        string script,
        bool elevated,
        bool isWarning,
        string startedStatus)
        => new(
            action,
            WslToolSetupLaunchKind.PowerShellScript,
            title,
            confirmationMessage,
            isWarning,
            elevated,
            startedStatus,
            PowerShellScript: script);

    private static WslToolSetupPlan WslBashPlan(
        WslToolSetupAction action,
        string title,
        string confirmationMessage,
        string distroName,
        string bashScript,
        bool isWarning,
        string startedStatus)
        => new(
            action,
            WslToolSetupLaunchKind.WslBashScript,
            title,
            confirmationMessage,
            isWarning,
            Elevated: false,
            startedStatus,
            DistroName: distroName,
            BashScript: bashScript);
}
