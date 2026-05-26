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

        if (!_llama.IsRunning && await IsRuntimePortOccupiedAsync(launchSettings))
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
