namespace LocalLlmConsole.Services;

public enum WindowsToolSetupAction
{
    Cpu,
    Cuda,
    Vulkan,
    Sycl
}

public sealed record WindowsToolSetupPlan(
    WindowsToolSetupAction Action,
    string Title,
    string ConfirmationMessage,
    string PowerShellScript,
    bool Elevated,
    string StartedStatus);

public sealed class WindowsToolSetupWorkflowService
{
    private readonly VisibleCommandLaunchService _commandLauncher;
    private readonly Func<WindowsToolSnapshot> _detectTools;

    public WindowsToolSetupWorkflowService(
        VisibleCommandLaunchService commandLauncher,
        WindowsEnvironmentService windowsEnvironment)
        : this(commandLauncher, windowsEnvironment.Detect)
    {
    }

    public WindowsToolSetupWorkflowService(
        VisibleCommandLaunchService commandLauncher,
        Func<WindowsToolSnapshot> detectTools)
    {
        _commandLauncher = commandLauncher ?? throw new ArgumentNullException(nameof(commandLauncher));
        _detectTools = detectTools ?? throw new ArgumentNullException(nameof(detectTools));
    }

    public Task<WindowsToolSnapshot> RefreshAsync(CancellationToken cancellationToken = default)
        => Task.Run(_detectTools, cancellationToken);

    public WindowsToolSetupPlan Plan(WindowsToolSetupAction action)
        => action switch
        {
            WindowsToolSetupAction.Cpu => new(
                action,
                "Install Windows CPU tools",
                "This opens an elevated PowerShell window and installs or repairs Git, CMake, and Visual Studio C++ Build Tools with winget when available. If winget is unavailable, it opens official installer pages instead.",
                WindowsSetupCommands.InstallCpuToolsPowerShell(),
                Elevated: true,
                "Windows CPU tool setup started in a PowerShell window."),
            WindowsToolSetupAction.Cuda => new(
                action,
                "Install Windows CUDA Toolkit",
                "This opens an elevated PowerShell window and installs or repairs the NVIDIA CUDA Toolkit with winget when available. If winget is unavailable, it opens NVIDIA's CUDA download page instead.",
                WindowsSetupCommands.InstallCudaPowerShell(),
                Elevated: true,
                "Windows CUDA Toolkit setup started in a PowerShell window."),
            WindowsToolSetupAction.Vulkan => new(
                action,
                "Install Windows Vulkan SDK",
                "This opens an elevated PowerShell window and installs or repairs the Vulkan SDK with winget when available. If winget is unavailable, it opens the Vulkan SDK download page instead.",
                WindowsSetupCommands.InstallVulkanPowerShell(),
                Elevated: true,
                "Windows Vulkan SDK setup started in a PowerShell window."),
            WindowsToolSetupAction.Sycl => new(
                action,
                "Install Windows Intel oneAPI",
                "This opens an elevated PowerShell window and installs or repairs Intel oneAPI Base Toolkit with winget when available. If winget is unavailable, it opens Intel's oneAPI download page instead. Install or update the Intel Arc graphics driver separately if sycl-ls does not see a Level Zero GPU.",
                WindowsSetupCommands.InstallOneApiPowerShell(),
                Elevated: true,
                "Windows Intel oneAPI setup started in a PowerShell window."),
            _ => throw new ArgumentOutOfRangeException(nameof(action), action, "Unknown Windows tool setup action.")
        };

    public void Execute(WindowsToolSetupPlan plan)
        => _commandLauncher.StartVisiblePowerShellScript(plan.PowerShellScript, plan.Elevated);
}
