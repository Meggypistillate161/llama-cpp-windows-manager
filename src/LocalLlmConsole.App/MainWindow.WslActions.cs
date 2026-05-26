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
    private async void UseWslDistroRow_Click(object sender, RoutedEventArgs e)
    {
        await RunEventAsync(async () =>
        {
            if ((sender as FrameworkElement)?.Tag is not UiRow row) return;
            var distro = row.Data["Name"]?.ToString() ?? row.C2;
            if (string.IsNullOrWhiteSpace(distro)) return;

            _settings = _settings with { WslDistro = distro };
            await PersistSettingsAsync();
            await RefreshWslLinuxAsync();
            SetStatus($"WSL distro set to {distro}.");
        });
    }

    private async Task InstallWslAsync()
    {
        var result = ThemedMessageBox.Show(
            this,
            "This opens an elevated PowerShell window and runs:\n\nwsl.exe --install --no-distribution\n\nWindows may ask for administrator approval or a reboot.",
            "Install WSL",
            MessageBoxButton.YesNo,
            MessageBoxImage.Information);
        if (result != MessageBoxResult.Yes) return;

        CommandLineService.StartVisibleWindowsCommand(HostExecutableResolver.WslExe(), ["--install", "--no-distribution"], elevated: true);
        SetStatus("WSL install started in a PowerShell window.");
        await Task.CompletedTask;
    }

    private async Task InstallWslUbuntuAsync()
    {
        var result = ThemedMessageBox.Show(
            this,
            $"This opens an elevated PowerShell window and runs:\n\nwsl.exe --install -d {WslSetupCommands.RecommendedUbuntuDistro}\n\nThen, if Ubuntu is ready, it installs the CPU build toolchain:\n\nsudo apt install -y {WslSetupCommands.BuildToolsPackages}\n\nWindows may ask for administrator approval, a reboot, or first-run Ubuntu account setup.",
            "Install Ubuntu for WSL",
            MessageBoxButton.YesNo,
            MessageBoxImage.Information);
        if (result != MessageBoxResult.Yes) return;

        CommandLineService.StartVisiblePowerShellScript(
            WslSetupCommands.InstallUbuntuAndBuildToolsPowerShell(HostExecutableResolver.WslExe()),
            elevated: true);
        SetStatus("WSL Ubuntu install and build-tool setup started in a PowerShell window.");
        await Task.CompletedTask;
    }

    private async Task CheckWslUpdatesAsync()
    {
        var result = ThemedMessageBox.Show(
            this,
            "This opens an elevated PowerShell window and runs:\n\nwsl.exe --update",
            "Check WSL updates",
            MessageBoxButton.YesNo,
            MessageBoxImage.Information);
        if (result != MessageBoxResult.Yes) return;

        CommandLineService.StartVisibleWindowsCommand(HostExecutableResolver.WslExe(), ["--update"], elevated: true);
        SetStatus("WSL update check started in a PowerShell window.");
        await Task.CompletedTask;
    }

    private async Task DeleteWslAsync()
    {
        var result = ThemedMessageBox.Show(
            this,
            "This opens an elevated PowerShell window to uninstall the Windows Subsystem for Linux package.\n\nThis can break all WSL-based runtimes until WSL is installed again. Existing distro data is not intentionally unregistered by this action, but WSL will be unavailable.",
            "Delete WSL",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);
        if (result != MessageBoxResult.Yes) return;

        CommandLineService.StartVisiblePowerShellScript(WslSetupCommands.DeleteWslPowerShell(HostExecutableResolver.WslExe()), elevated: true);
        SetStatus("WSL uninstall started in a PowerShell window.");
        await Task.CompletedTask;
    }

    private async Task CheckUbuntuUpdatesAsync()
    {
        var distro = SelectedUbuntuDistroName();
        if (string.IsNullOrWhiteSpace(distro)) { SetStatus("Install or select an Ubuntu distro first."); return; }
        var result = ThemedMessageBox.Show(
            this,
            $"This opens PowerShell and checks available package updates inside {distro}:\n\nsudo apt update && apt list --upgradable",
            "Check Ubuntu updates",
            MessageBoxButton.YesNo,
            MessageBoxImage.Information);
        if (result != MessageBoxResult.Yes) return;

        CommandLineService.StartVisibleWslBashScript(distro, "sudo apt update && apt list --upgradable", elevated: false);
        SetStatus($"Ubuntu update check started for {distro}.");
        await Task.CompletedTask;
    }

    private async Task DeleteUbuntuAsync()
    {
        var distro = SelectedUbuntuDistroName();
        if (string.IsNullOrWhiteSpace(distro)) { SetStatus("Install or select an Ubuntu distro first."); return; }
        var result = ThemedMessageBox.Show(
            this,
            $"This will unregister and delete the Ubuntu distro root filesystem:\n\n{distro}\n\nThis deletes files inside that WSL distro. It does not delete models stored in the Windows app data folder.",
            "Delete Ubuntu distro",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);
        if (result != MessageBoxResult.Yes) return;

        CommandLineService.StartVisiblePowerShellScript(WslSetupCommands.DeleteUbuntuPowerShell(HostExecutableResolver.WslExe(), distro), elevated: false);
        SetStatus($"Ubuntu unregister started for {distro}.");
        await Task.CompletedTask;
    }

    private async Task InstallUbuntuBuildToolsAsync()
    {
        var distro = SelectedUbuntuDistroName();
        if (string.IsNullOrWhiteSpace(distro)) { SetStatus("Install or select an Ubuntu distro first."); return; }
        var result = ThemedMessageBox.Show(
            this,
            $"This opens PowerShell and installs the CPU build toolchain inside {distro}:\n\nsudo apt update\nsudo apt install -y {WslSetupCommands.BuildToolsPackages}\n\nUbuntu may ask for your Linux sudo password. CUDA runtime builds still require a separate NVIDIA CUDA Toolkit/runtime library install inside WSL.",
            "Install Ubuntu CPU build tools",
            MessageBoxButton.YesNo,
            MessageBoxImage.Information);
        if (result != MessageBoxResult.Yes) return;

        CommandLineService.StartVisibleWslBashScript(distro, WslSetupCommands.InstallBuildToolsCommand, elevated: false);
        SetStatus($"Ubuntu CPU build tool install started for {distro}.");
        await Task.CompletedTask;
    }

    private async Task DeleteUbuntuBuildToolsAsync()
    {
        var distro = SelectedUbuntuDistroName();
        if (string.IsNullOrWhiteSpace(distro)) { SetStatus("Install or select an Ubuntu distro first."); return; }
        var result = ThemedMessageBox.Show(
            this,
            $"This opens PowerShell and removes the CPU build packages from {distro}:\n\nsudo apt remove -y {WslSetupCommands.BuildToolsPackages}\n\nThese packages may also be used outside {AppDisplayName}.",
            "Delete Ubuntu CPU build tools",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);
        if (result != MessageBoxResult.Yes) return;

        CommandLineService.StartVisibleWslBashScript(distro, WslSetupCommands.RemoveBuildToolsCommand, elevated: false);
        SetStatus($"Ubuntu CPU build tool removal started for {distro}.");
        await Task.CompletedTask;
    }

    private async Task InstallUbuntuCudaToolkitAsync()
    {
        var distro = SelectedUbuntuDistroName();
        if (string.IsNullOrWhiteSpace(distro)) { SetStatus("Install or select an Ubuntu distro first."); return; }
        var result = ThemedMessageBox.Show(
            this,
            $"This opens PowerShell and installs NVIDIA's WSL CUDA Toolkit inside {distro}:\n\n{WslSetupCommands.CudaToolkitPackage}\n\nIt adds NVIDIA's WSL CUDA apt keyring from developer.download.nvidia.com, runs apt update, installs the toolkit, and verifies both nvcc and libcudart. Ubuntu may ask for your Linux sudo password. The Windows NVIDIA driver with WSL GPU support must already be installed.",
            "Install WSL CUDA Toolkit",
            MessageBoxButton.YesNo,
            MessageBoxImage.Information);
        if (result != MessageBoxResult.Yes) return;

        CommandLineService.StartVisibleWslBashScript(distro, WslSetupCommands.InstallCudaToolkitCommand, elevated: false);
        SetStatus($"WSL CUDA Toolkit install started for {distro}.");
        await Task.CompletedTask;
    }

    private async Task DeleteUbuntuCudaToolkitAsync()
    {
        var distro = SelectedUbuntuDistroName();
        if (string.IsNullOrWhiteSpace(distro)) { SetStatus("Install or select an Ubuntu distro first."); return; }
        var result = ThemedMessageBox.Show(
            this,
            $"This opens PowerShell and removes NVIDIA CUDA Toolkit packages from {distro}:\n\nsudo apt remove -y {WslSetupCommands.CudaRemovePackages}\n\nThese CUDA packages may also be used outside {AppDisplayName}.",
            "Delete WSL CUDA Toolkit",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);
        if (result != MessageBoxResult.Yes) return;

        CommandLineService.StartVisibleWslBashScript(distro, WslSetupCommands.RemoveCudaToolkitCommand, elevated: false);
        SetStatus($"WSL CUDA Toolkit removal started for {distro}.");
        await Task.CompletedTask;
    }

    private async Task InstallUbuntuVulkanToolsAsync()
    {
        var distro = SelectedUbuntuDistroName();
        if (string.IsNullOrWhiteSpace(distro)) { SetStatus("Install or select an Ubuntu distro first."); return; }
        var result = ThemedMessageBox.Show(
            this,
            $"This opens PowerShell and installs Ubuntu Vulkan packages inside {distro}:\n\nsudo apt install -y {WslSetupCommands.VulkanToolsPackages}\n\nIt then runs vulkaninfo --summary to verify a usable Vulkan driver/device before llama.cpp Vulkan builds are attempted. Ubuntu may ask for your Linux sudo password. Your Windows GPU driver and WSL graphics stack must expose Vulkan inside WSL.",
            "Install WSL Vulkan tools",
            MessageBoxButton.YesNo,
            MessageBoxImage.Information);
        if (result != MessageBoxResult.Yes) return;

        CommandLineService.StartVisibleWslBashScript(distro, WslSetupCommands.InstallVulkanToolsCommand, elevated: false);
        SetStatus($"WSL Vulkan tool install started for {distro}.");
        await Task.CompletedTask;
    }

    private async Task DeleteUbuntuVulkanToolsAsync()
    {
        var distro = SelectedUbuntuDistroName();
        if (string.IsNullOrWhiteSpace(distro)) { SetStatus("Install or select an Ubuntu distro first."); return; }
        var result = ThemedMessageBox.Show(
            this,
            $"This opens PowerShell and removes Ubuntu Vulkan packages from {distro}:\n\nsudo apt remove -y {WslSetupCommands.VulkanToolsPackages}\n\nThese Vulkan packages may also be used outside {AppDisplayName}.",
            "Delete WSL Vulkan tools",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);
        if (result != MessageBoxResult.Yes) return;

        CommandLineService.StartVisibleWslBashScript(distro, WslSetupCommands.RemoveVulkanToolsCommand, elevated: false);
        SetStatus($"WSL Vulkan tool removal started for {distro}.");
        await Task.CompletedTask;
    }

    private string SelectedUbuntuDistroName()
    {
        if (_wslDistroGrid?.SelectedItem is UiRow row)
        {
            var selectedName = row.Data["Name"]?.ToString() ?? row.C2;
            var selectedUbuntu = row.Data["IsUbuntu"]?.GetValue<bool?>() ?? selectedName.Contains("ubuntu", StringComparison.OrdinalIgnoreCase);
            if (!string.IsNullOrWhiteSpace(selectedName) && selectedUbuntu) return selectedName;
        }

        var configured = _viewModel.WslLinux.Rows
            .FirstOrDefault(row => string.Equals(row.Data["Name"]?.ToString(), _settings.WslDistro, StringComparison.OrdinalIgnoreCase)
                && (row.Data["IsUbuntu"]?.GetValue<bool?>() ?? false));
        if (configured is not null) return configured.Data["Name"]?.ToString() ?? configured.C2;

        var ubuntu = _viewModel.WslLinux.Rows.FirstOrDefault(row => row.Data["IsUbuntu"]?.GetValue<bool?>() == true);
        return ubuntu?.Data["Name"]?.ToString() ?? ubuntu?.C2 ?? "";
    }
}
