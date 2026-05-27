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
    private void ShowWindows()
    {
        SetPage("Windows", "Detect native Windows build tools for llama.cpp and open setup actions.");
        var root = Dock();

        var toolbar = Bar();
        toolbar.Children.Add(Button("Refresh", async (_, _) => await RefreshWindowsAsync()));
        System.Windows.Controls.DockPanel.SetDock(toolbar, System.Windows.Controls.Dock.Top);
        root.Children.Add(toolbar);

        var body = Stack();
        body.Children.Add(WindowsSetupRows());

        var statusGrid = new Grid { Margin = new Thickness(0, 0, 0, 10) };
        statusGrid.ColumnDefinitions.Add(new ColumnDefinition());
        statusGrid.ColumnDefinitions.Add(new ColumnDefinition());
        statusGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        statusGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        statusGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        _windowsStatusMetric = AddMetric(statusGrid, "Windows status", 0, 0);
        _windowsCpuMetric = AddMetric(statusGrid, "CPU build", 0, 1);
        _windowsCudaMetric = AddMetric(statusGrid, "CUDA build", 1, 0);
        _windowsVulkanMetric = AddMetric(statusGrid, "Vulkan build", 1, 1);
        _windowsSyclMetric = AddMetric(statusGrid, "Intel Arc SYCL", 2, 0);
        body.Children.Add(statusGrid);

        body.Children.Add(Text("Native Windows builds use Git, CMake, and Visual Studio C++ Build Tools. CUDA, Vulkan, and Intel Arc SYCL runtimes require their Windows SDK/toolkit in addition to the CPU build tools.", muted: true));

        _windowsToolsGrid = GridFor(("Toolchain", "C1", .75), ("Status", "C2", .6), ("Details", "C3", 2.8), ("Driver", "C4", 1.7));
        _windowsToolsGrid.ItemsSource = _viewModel.Windows.Rows;
        body.Children.Add(GridSection("Native Windows tools", _windowsToolsGrid));

        root.Children.Add(Scroll(body, new Thickness(16)));
        PageHost.Content = root;
        if (_cachedWindowsTools is not null)
            PopulateWindowsPage(_cachedWindowsTools);

        ApplyPendingHelpFocus();
        if (!_windowsAutoRefreshDone)
        {
            _windowsAutoRefreshDone = true;
            RunBackground(RefreshWindowsAsync, "Windows tool refresh failed");
        }
    }

    private UIElement WindowsSetupRows()
    {
        var panel = new StackPanel { Margin = new Thickness(0, 0, 0, 12) };
        panel.Children.Add(WindowsToolActionRow(
            "CPU tools",
            "Install Git, CMake, and Visual Studio C++ Build Tools for native llama.cpp CPU builds.",
            out _windowsInstallCpuToolsButton,
            "Install CPU Tools",
            async (_, _) => await InstallWindowsCpuToolsAsync()));
        panel.Children.Add(WindowsToolActionRow(
            "CUDA tools",
            "Install NVIDIA CUDA Toolkit for native llama.cpp CUDA builds. The Windows NVIDIA driver must also be installed.",
            out _windowsInstallCudaToolkitButton,
            "Install CUDA",
            async (_, _) => await InstallWindowsCudaToolkitAsync()));
        panel.Children.Add(WindowsToolActionRow(
            "Vulkan tools",
            "Install the Vulkan SDK for native llama.cpp Vulkan builds. The GPU driver must expose Vulkan on Windows.",
            out _windowsInstallVulkanToolsButton,
            "Install Vulkan",
            async (_, _) => await InstallWindowsVulkanToolsAsync()));
        panel.Children.Add(WindowsToolActionRow(
            "Intel oneAPI",
            "Install Intel oneAPI Base Toolkit for native llama.cpp SYCL builds on Intel Arc and supported Intel GPUs.",
            out _windowsInstallSyclToolsButton,
            "Install oneAPI",
            async (_, _) => await InstallWindowsSyclToolsAsync()));
        return panel;
    }

    private static Grid WindowsToolActionRow(
        string label,
        string description,
        out WpfButton actionButton,
        string actionText,
        RoutedEventHandler actionClick)
    {
        var row = new Grid { Margin = new Thickness(0, 0, 0, 8) };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(110) });
        row.ColumnDefinitions.Add(new ColumnDefinition());
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.Children.Add(new TextBlock
        {
            Text = label,
            FontWeight = FontWeights.SemiBold,
            Foreground = (System.Windows.Media.Brush)WpfApplication.Current.Resources["TextSoft"],
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 10, 0)
        });
        var descriptionText = new TextBlock
        {
            Text = description,
            Foreground = (System.Windows.Media.Brush)WpfApplication.Current.Resources["TextMuted"],
            TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 10, 0)
        };
        Grid.SetColumn(descriptionText, 1);
        row.Children.Add(descriptionText);
        actionButton = Button(actionText, actionClick);
        SetButtonToolTip(actionButton, $"Install or repair {label.ToLowerInvariant()} on Windows.");
        Grid.SetColumn(actionButton, 2);
        row.Children.Add(actionButton);
        return row;
    }

    private async Task RefreshWindowsAsync()
    {
        await RunAsync("Detecting Windows build tools...", async () =>
        {
            var tools = await Task.Run(() => _windowsEnvironment.Detect());
            _cachedWindowsTools = tools;
            PopulateWindowsPage(tools);
            SetStatus(WindowsEnvironmentService.Status(tools));
        });
    }

    private void PopulateWindowsPage(WindowsToolSnapshot tools)
    {
        SetMetricText(_windowsStatusMetric, WindowsEnvironmentService.Status(tools));
        SetMetricText(_windowsCpuMetric, tools.CpuToolsInstalled ? "Ready" : "Incomplete");
        SetMetricText(_windowsCudaMetric, tools.CudaToolsInstalled ? "Ready" : "Incomplete");
        SetMetricText(_windowsVulkanMetric, tools.VulkanToolsInstalled ? "Ready" : "Incomplete");
        SetMetricText(_windowsSyclMetric, tools.SyclToolsInstalled ? "Ready" : "Incomplete");

        if (_windowsInstallCpuToolsButton is not null)
            _windowsInstallCpuToolsButton.Content = WindowsEnvironmentService.CpuToolsActionLabel(tools);
        if (_windowsInstallCudaToolkitButton is not null)
            _windowsInstallCudaToolkitButton.Content = WindowsEnvironmentService.CudaToolsActionLabel(tools);
        if (_windowsInstallVulkanToolsButton is not null)
            _windowsInstallVulkanToolsButton.Content = WindowsEnvironmentService.VulkanToolsActionLabel(tools);
        if (_windowsInstallSyclToolsButton is not null)
            _windowsInstallSyclToolsButton.Content = WindowsEnvironmentService.SyclToolsActionLabel(tools);

        _viewModel.Windows.ReplaceToolRows(tools);
        _windowsToolsGrid?.Items.Refresh();
    }

    private async Task InstallWindowsCpuToolsAsync()
    {
        var result = ThemedMessageBox.Show(
            this,
            "This opens an elevated PowerShell window and installs or repairs Git, CMake, and Visual Studio C++ Build Tools with winget when available. If winget is unavailable, it opens official installer pages instead.",
            "Install Windows CPU tools",
            MessageBoxButton.YesNo,
            MessageBoxImage.Information);
        if (result != MessageBoxResult.Yes) return;

        CommandLineService.StartVisiblePowerShellScript(WindowsSetupCommands.InstallCpuToolsPowerShell(), elevated: true);
        SetStatus("Windows CPU tool setup started in a PowerShell window.");
        await Task.CompletedTask;
    }

    private async Task InstallWindowsCudaToolkitAsync()
    {
        var result = ThemedMessageBox.Show(
            this,
            "This opens an elevated PowerShell window and installs or repairs the NVIDIA CUDA Toolkit with winget when available. If winget is unavailable, it opens NVIDIA's CUDA download page instead.",
            "Install Windows CUDA Toolkit",
            MessageBoxButton.YesNo,
            MessageBoxImage.Information);
        if (result != MessageBoxResult.Yes) return;

        CommandLineService.StartVisiblePowerShellScript(WindowsSetupCommands.InstallCudaPowerShell(), elevated: true);
        SetStatus("Windows CUDA Toolkit setup started in a PowerShell window.");
        await Task.CompletedTask;
    }

    private async Task InstallWindowsVulkanToolsAsync()
    {
        var result = ThemedMessageBox.Show(
            this,
            "This opens an elevated PowerShell window and installs or repairs the Vulkan SDK with winget when available. If winget is unavailable, it opens the Vulkan SDK download page instead.",
            "Install Windows Vulkan SDK",
            MessageBoxButton.YesNo,
            MessageBoxImage.Information);
        if (result != MessageBoxResult.Yes) return;

        CommandLineService.StartVisiblePowerShellScript(WindowsSetupCommands.InstallVulkanPowerShell(), elevated: true);
        SetStatus("Windows Vulkan SDK setup started in a PowerShell window.");
        await Task.CompletedTask;
    }

    private async Task InstallWindowsSyclToolsAsync()
    {
        var result = ThemedMessageBox.Show(
            this,
            "This opens an elevated PowerShell window and installs or repairs Intel oneAPI Base Toolkit with winget when available. If winget is unavailable, it opens Intel's oneAPI download page instead. Install or update the Intel Arc graphics driver separately if sycl-ls does not see a Level Zero GPU.",
            "Install Windows Intel oneAPI",
            MessageBoxButton.YesNo,
            MessageBoxImage.Information);
        if (result != MessageBoxResult.Yes) return;

        CommandLineService.StartVisiblePowerShellScript(WindowsSetupCommands.InstallOneApiPowerShell(), elevated: true);
        SetStatus("Windows Intel oneAPI setup started in a PowerShell window.");
        await Task.CompletedTask;
    }
}
