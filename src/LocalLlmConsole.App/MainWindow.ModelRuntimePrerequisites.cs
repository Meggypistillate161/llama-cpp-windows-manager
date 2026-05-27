using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using Forms = System.Windows.Forms;
using WpfApplication = System.Windows.Application;
using WpfBinding = System.Windows.Data.Binding;
using WpfButton = System.Windows.Controls.Button;
using WpfCheckBox = System.Windows.Controls.CheckBox;
using WpfComboBox = System.Windows.Controls.ComboBox;
using WpfProgressBar = System.Windows.Controls.ProgressBar;
using WpfTextBox = System.Windows.Controls.TextBox;
namespace LocalLlmConsole;

public partial class MainWindow
{
    private async Task EnsureRuntimeLaunchPrerequisitesAsync(RuntimeRecord runtime, AppSettings launchSettings)
    {
        if (runtime.Mode == RuntimeMode.Wsl)
            await EnsureWslDistroReadyAsync(launchSettings.WslDistro);
        if (runtime.Mode == RuntimeMode.Wsl && runtime.Backend == RuntimeBackend.Sycl)
            await EnsureWslSyclToolsReadyAsync(launchSettings.WslDistro);
        if (runtime.Mode == RuntimeMode.Native && runtime.Backend == RuntimeBackend.Sycl)
            EnsureWindowsSyclToolsReady();

        if (await IsRuntimePortOccupiedAsync(launchSettings))
            throw new InvalidOperationException($"Port {launchSettings.Port} is already in use. Stop the existing process or choose a different model port before launching llama.cpp.");
    }

    private async Task EnsureWslDistroReadyAsync(string distroName)
    {
        var report = await _wslEnvironment.DetectAsync();
        if (!report.WslExeFound)
            throw new InvalidOperationException("WSL is not installed or wsl.exe was not found.");
        if (!report.WslWorking)
            throw new InvalidOperationException($"WSL is installed but not ready: {report.Details}");

        var distro = report.Distros.FirstOrDefault(item => item.Name.Equals(distroName, StringComparison.OrdinalIgnoreCase));
        if (distro is null)
            throw new InvalidOperationException($"WSL distro '{distroName}' is not installed. Select an installed Ubuntu distro or install Ubuntu first.");
        if (!string.Equals(distro.Version, "2", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"WSL distro '{distroName}' is version {distro.Version}. llama.cpp runtimes require WSL 2.");
    }

    private async Task EnsureWslCudaToolkitReadyAsync(string distroName)
    {
        var result = await RunWslPreflightAsync(distroName, WslSetupCommands.CudaToolkitPreflightCommand);
        if (result.ExitCode == 0) return;

        var detail = CommandLineService.FirstNonBlankLine(result.Error);
        if (string.IsNullOrWhiteSpace(detail)) detail = CommandLineService.FirstNonBlankLine(result.Output);
        throw new InvalidOperationException(WslEnvironmentService.CudaToolkitIncompleteMessage(distroName, detail));
    }

    private async Task EnsureWslVulkanToolsReadyAsync(string distroName)
    {
        var result = await RunWslPreflightAsync(distroName, WslSetupCommands.VulkanToolsPreflightCommand);
        if (result.ExitCode == 0) return;

        var detail = CommandLineService.FirstNonBlankLine(result.Error);
        if (string.IsNullOrWhiteSpace(detail)) detail = CommandLineService.FirstNonBlankLine(result.Output);
        throw new InvalidOperationException(WslEnvironmentService.VulkanToolsIncompleteMessage(distroName, detail));
    }

    private async Task EnsureWslSyclToolsReadyAsync(string distroName)
    {
        var result = await RunWslPreflightAsync(distroName, WslSetupCommands.SyclToolsPreflightCommand);
        if (result.ExitCode == 0) return;

        var detail = CommandLineService.FirstNonBlankLine(result.Error);
        if (string.IsNullOrWhiteSpace(detail)) detail = CommandLineService.FirstNonBlankLine(result.Output);
        throw new InvalidOperationException(WslEnvironmentService.SyclToolsIncompleteMessage(distroName, detail));
    }

    private void EnsureWindowsBuildToolsReady(RuntimeBackend backend)
    {
        var tools = _windowsEnvironment.Detect();
        if (!tools.CpuToolsInstalled)
            throw new InvalidOperationException($"Windows CPU build tools are not ready. Open Windows and install CPU Tools first.{Environment.NewLine}{WindowsEnvironmentService.CpuDetails(tools)}");
        if (backend == RuntimeBackend.Cuda && !tools.CudaToolsInstalled)
            throw new InvalidOperationException($"Windows CUDA Toolkit is not ready. Open Windows and install CUDA first.{Environment.NewLine}{tools.CudaDetails}");
        if (backend == RuntimeBackend.Vulkan && !tools.VulkanToolsInstalled)
            throw new InvalidOperationException($"Windows Vulkan SDK is not ready. Open Windows and install Vulkan first.{Environment.NewLine}{tools.VulkanDetails}");
        if (backend == RuntimeBackend.Sycl && !tools.SyclToolsInstalled)
            throw new InvalidOperationException($"Windows Intel oneAPI/SYCL tools are not ready. Open Windows and install oneAPI first.{Environment.NewLine}{tools.SyclDetails}");
    }

    private void EnsureWindowsSyclToolsReady()
    {
        var tools = _windowsEnvironment.Detect();
        if (!tools.SyclToolsInstalled)
            throw new InvalidOperationException($"Windows Intel oneAPI/SYCL tools are not ready. Open Windows and install oneAPI first.{Environment.NewLine}{tools.SyclDetails}");
    }

    private async Task<bool> ConfirmVramAdmissionAsync(RuntimeRecord runtime, ModelRecord model, AppSettings launchSettings)
    {
        if (!_sessions.HasRunningSessions) return true;
        if (runtime.Backend is not (RuntimeBackend.Cuda or RuntimeBackend.Vulkan or RuntimeBackend.Sycl)) return true;

        var memory = runtime.Backend == RuntimeBackend.Cuda
            ? await GpuStatusService.MemoryAsync()
            : null;
        var result = _vramAdmission.Assess(model, runtime, launchSettings, memory);
        if (result.Decision == VramAdmissionDecision.Allow) return true;
        if (result.Decision == VramAdmissionDecision.Block)
        {
            ThemedMessageBox.Show(
                this,
                $"{result.Message}\n\nUnload another model or reduce GPU layers/context before loading {model.Name}.",
                "VRAM check",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return false;
        }

        return ThemedMessageBox.Show(
            this,
            $"{result.Message}\n\nLoad {model.Name} anyway? Existing loaded models will keep serving on their own ports.",
            "VRAM check",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning) == MessageBoxResult.Yes;
    }

    private async Task<ProcessRunResult> RunWslPreflightAsync(string distroName, string script)
    {
        var psi = new ProcessStartInfo(HostExecutableResolver.WslExe())
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        foreach (var arg in new[] { "-d", distroName, "--", "bash", "-s" })
            psi.ArgumentList.Add(arg);

        return await _processRunner.RunAsync(psi, TimeSpan.FromSeconds(30), standardInput: script);
    }

    private async Task<bool> IsRuntimePortOccupiedAsync(AppSettings launchSettings)
    {
        var port = launchSettings.Port;
        if (port is < 1 or > 65535) return false;
        if (await RuntimeEndpointRespondingAsync(launchSettings)) return true;

        using var client = new TcpClient();
        try
        {
            var connectTask = client.ConnectAsync(IPAddress.Loopback, port);
            var completed = await Task.WhenAny(connectTask, Task.Delay(TimeSpan.FromMilliseconds(500)));
            if (completed != connectTask) return false;
            await connectTask;
            return client.Connected;
        }
        catch
        {
            return false;
        }
    }
}
