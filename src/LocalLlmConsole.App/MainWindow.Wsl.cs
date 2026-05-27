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
    private void ShowWslLinux()
    {
        SetPage("WSL Linux", "Detect WSL, choose the Linux distro used for llama.cpp, and open setup actions.");
        var root = Dock();

        var toolbar = Bar();
        toolbar.Children.Add(Button("Refresh", async (_, _) => await RefreshWslLinuxAsync()));
        System.Windows.Controls.DockPanel.SetDock(toolbar, System.Windows.Controls.Dock.Top);
        root.Children.Add(toolbar);

        var body = Stack();
        body.Children.Add(WslSetupRows());

        var statusGrid = new Grid { Margin = new Thickness(0, 0, 0, 10) };
        statusGrid.ColumnDefinitions.Add(new ColumnDefinition());
        statusGrid.ColumnDefinitions.Add(new ColumnDefinition());
        statusGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        statusGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        _wslStatusMetric = AddMetric(statusGrid, "WSL status", 0, 0);
        _wslSelectedMetric = AddMetric(statusGrid, "Selected distro", 0, 1);
        _wslInfoMetric = AddMetric(statusGrid, "WSL info", 1, 0);
        _wslToolsMetric = AddMetric(statusGrid, "Tools", 1, 1);
        body.Children.Add(statusGrid);

        body.Children.Add(Text("The selected distro is used for WSL llama.cpp launches and builds. Ubuntu is recommended; other real Linux distros remain selectable when detected.", muted: true));

        _wslDistroGrid = GridFor(("Distro", "C2", 1.4), ("State", "C3", .7), ("WSL", "C4", .45), ("Notes", "C5", 2.3));
        AddButtonColumn(_wslDistroGrid, "Action", "C6", "B1", UseWslDistroRow_Click, .55, tooltipBinding: "T1");
        _wslDistroGrid.ItemsSource = _viewModel.WslLinux.Rows;
        body.Children.Add(GridSection("Installed Linux distros", _wslDistroGrid));

        root.Children.Add(Scroll(body, new Thickness(16)));
        PageHost.Content = root;
        if (_cachedWslReport is not null && _cachedWslTools is not null)
            PopulateWslLinuxPage(_cachedWslReport, _cachedWslTools);

        ApplyPendingHelpFocus();
        if (!_wslLinuxAutoRefreshDone)
        {
            _wslLinuxAutoRefreshDone = true;
            RunBackground(RefreshWslLinuxAsync, "WSL refresh failed");
        }
    }

    private UIElement WslSetupRows()
    {
        var panel = new StackPanel { Margin = new Thickness(0, 0, 0, 12) };
        panel.Children.Add(WslSetupRow(
            "WSL",
            "Windows Subsystem for Linux",
            out _wslInstallButton,
            "Install WSL",
            async (_, _) => await InstallWslAsync(),
            out _wslCheckUpdatesButton,
            "Update WSL",
            async (_, _) => await CheckWslUpdatesAsync(),
            out _wslDeleteButton,
            "Delete WSL",
            async (_, _) => await DeleteWslAsync()));
        panel.Children.Add(WslSetupRow(
            "Ubuntu",
            "Recommended Linux distro for llama.cpp builds",
            out _wslInstallUbuntuButton,
            "Install Ubuntu",
            async (_, _) => await InstallWslUbuntuAsync(),
            out _wslCheckUbuntuUpdatesButton,
            "Update Ubuntu",
            async (_, _) => await CheckUbuntuUpdatesAsync(),
            out _wslDeleteUbuntuButton,
            "Delete Ubuntu",
            async (_, _) => await DeleteUbuntuAsync()));
        panel.Children.Add(WslToolActionRow(
            "CPU tools",
            "Install Git, CMake, compiler, pkg-config, libcurl headers, ccache, and Ninja inside the selected Ubuntu distro. CUDA presets require a separate NVIDIA CUDA Toolkit install in WSL.",
            out _wslInstallBuildToolsButton,
            "Install CPU Tools",
            async (_, _) => await InstallUbuntuBuildToolsAsync(),
            out _wslDeleteBuildToolsButton,
            "Delete",
            async (_, _) => await DeleteUbuntuBuildToolsAsync()));
        panel.Children.Add(WslToolActionRow(
            "CUDA tools",
            $"Install NVIDIA's WSL CUDA Toolkit package ({WslSetupCommands.CudaToolkitPackage}) inside the selected Ubuntu distro for CUDA runtime builds.",
            out _wslInstallCudaToolkitButton,
            "Install CUDA",
            async (_, _) => await InstallUbuntuCudaToolkitAsync(),
            out _wslDeleteCudaToolkitButton,
            "Delete",
            async (_, _) => await DeleteUbuntuCudaToolkitAsync()));
        panel.Children.Add(WslToolActionRow(
            "Vulkan tools",
            $"Install Ubuntu Vulkan build/runtime tools ({WslSetupCommands.VulkanToolsPackages}) inside the selected Ubuntu distro for official llama.cpp Vulkan builds.",
            out _wslInstallVulkanToolsButton,
            "Install Vulkan",
            async (_, _) => await InstallUbuntuVulkanToolsAsync(),
            out _wslDeleteVulkanToolsButton,
            "Delete",
            async (_, _) => await DeleteUbuntuVulkanToolsAsync()));
        panel.Children.Add(WslToolActionRow(
            "Intel GPU runtime",
            $"Install Intel Level Zero/OpenCL runtime packages ({WslSetupCommands.SyclRuntimePackages}) inside the selected Ubuntu distro for Intel Arc SYCL runtimes.",
            out _wslInstallSyclRuntimeButton,
            "Install Intel GPU",
            async (_, _) => await InstallUbuntuSyclRuntimeAsync(),
            out _wslDeleteSyclRuntimeButton,
            "Delete",
            async (_, _) => await DeleteUbuntuSyclRuntimeAsync()));
        panel.Children.Add(WslToolActionRow(
            "Intel oneAPI",
            $"Install Intel oneAPI DPC++ compiler, MKL, and DNNL packages ({WslSetupCommands.SyclOneApiPackages}) inside the selected Ubuntu distro for llama.cpp SYCL builds.",
            out _wslInstallSyclOneApiButton,
            "Install oneAPI",
            async (_, _) => await InstallUbuntuSyclOneApiAsync(),
            out _wslDeleteSyclOneApiButton,
            "Delete",
            async (_, _) => await DeleteUbuntuSyclOneApiAsync()));
        return panel;
    }

    private static Grid WslSetupRow(
        string label,
        string description,
        out WpfButton installButton,
        string installText,
        RoutedEventHandler installClick,
        out WpfButton updateButton,
        string updateText,
        RoutedEventHandler updateClick,
        out WpfButton deleteButton,
        string deleteText,
        RoutedEventHandler deleteClick)
    {
        var row = new Grid { Margin = new Thickness(0, 0, 0, 8) };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(110) });
        row.ColumnDefinitions.Add(new ColumnDefinition());
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
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
        installButton = Button(installText, installClick);
        Grid.SetColumn(installButton, 2);
        row.Children.Add(installButton);
        updateButton = Button(updateText, updateClick);
        Grid.SetColumn(updateButton, 3);
        row.Children.Add(updateButton);
        deleteButton = Button(deleteText, deleteClick);
        Grid.SetColumn(deleteButton, 4);
        row.Children.Add(deleteButton);
        return row;
    }

    private static Grid WslToolActionRow(
        string label,
        string description,
        out WpfButton actionButton,
        string actionText,
        RoutedEventHandler actionClick,
        out WpfButton deleteButton,
        string deleteText,
        RoutedEventHandler deleteClick)
    {
        var row = new Grid { Margin = new Thickness(0, 0, 0, 8) };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(110) });
        row.ColumnDefinitions.Add(new ColumnDefinition());
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
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
        SetButtonToolTip(actionButton, $"Install or repair {label.ToLowerInvariant()} in the selected Ubuntu distro.");
        Grid.SetColumn(actionButton, 2);
        row.Children.Add(actionButton);
        deleteButton = Button(deleteText, deleteClick);
        SetButtonToolTip(deleteButton, $"Remove {label.ToLowerInvariant()} from the selected Ubuntu distro.");
        Grid.SetColumn(deleteButton, 3);
        row.Children.Add(deleteButton);
        return row;
    }

    private async Task RefreshWslLinuxAsync()
    {
        await RunAsync("Detecting WSL...", async () =>
        {
            var report = await Task.Run(() => _wslEnvironment.DetectAsync());
            await ApplyDetectedWslDistroAsync(report);
            var tools = await DetectSelectedWslToolsAsync(report);
            _cachedWslReport = report;
            _cachedWslTools = tools;
            PopulateWslLinuxPage(report, tools);
            SetStatus(report.Status);
        });
    }

    private async Task AutoSelectDetectedWslDistroAsync()
    {
        var report = await Task.Run(() => _wslEnvironment.DetectAsync());
        await ApplyDetectedWslDistroAsync(report);
    }

    private async Task ApplyDetectedWslDistroAsync(WslEnvironmentReport report)
    {
        if (_stateStore is null || report.Distros.Count == 0) return;
        if (report.Distros.Any(distro => distro.Name.Equals(_settings.WslDistro, StringComparison.OrdinalIgnoreCase))) return;
        var detected = report.RecommendedDistro;
        if (string.IsNullOrWhiteSpace(detected)) return;
        if (!report.Distros.Any(distro => distro.Name.Equals(detected, StringComparison.OrdinalIgnoreCase))) return;

        _settings = _settings with { WslDistro = detected };
        await PersistSettingsAsync();
    }

    private async Task<WslToolSnapshot> DetectSelectedWslToolsAsync(WslEnvironmentReport report)
    {
        var distro = WslEnvironmentService.SelectedUbuntuDistroName(report, _settings.WslDistro);
        if (!report.WslExeFound || !report.WslWorking || string.IsNullOrWhiteSpace(distro))
            return WslEnvironmentService.UnknownToolSnapshot();
        var psi = new ProcessStartInfo(HostExecutableResolver.WslExe())
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        foreach (var arg in new[] { "-d", distro, "--", "bash", "-s" })
            psi.ArgumentList.Add(arg);

        try
        {
            var result = await _processRunner.RunAsync(psi, TimeSpan.FromSeconds(15), standardInput: WslSetupCommands.ToolProbeCommand);
            if (result.ExitCode != 0 && string.IsNullOrWhiteSpace(result.Output))
                return WslEnvironmentService.UnknownToolSnapshot();

            return WslEnvironmentService.ParseToolProbeOutput(result.Output);
        }
        catch
        {
            return WslEnvironmentService.UnknownToolSnapshot();
        }
    }

    private void PopulateWslLinuxPage(WslEnvironmentReport report, WslToolSnapshot tools)
    {
        var hasUbuntu = report.Distros.Any(distro => distro.IsUbuntu);
        SetWslActionVisibility(report, hasUbuntu, tools);

        SetMetricText(_wslStatusMetric, report.Status);
        SetMetricText(_wslSelectedMetric, WslEnvironmentService.SelectedDistroSummary(report, _settings.WslDistro));
        SetMetricText(_wslInfoMetric, WslEnvironmentService.InstalledDistroSummary(report));
        SetMetricText(_wslToolsMetric, WslEnvironmentService.ToolSummary(tools));

        _viewModel.WslLinux.ReplaceDistroRows(report, _settings.WslDistro);
        _wslDistroGrid?.Items.Refresh();
        ApplyPendingHelpFocus();
    }

    private void SetWslActionVisibility(WslEnvironmentReport report, bool hasUbuntu, WslToolSnapshot tools)
    {
        SetButtonVisibility(_wslInstallButton, !report.WslExeFound);
        SetButtonVisibility(_wslCheckUpdatesButton, report.WslExeFound);
        SetButtonVisibility(_wslDeleteButton, report.WslExeFound);
        SetButtonVisibility(_wslInstallUbuntuButton, report.WslExeFound && !hasUbuntu);
        SetButtonVisibility(_wslInstallBuildToolsButton, report.WslExeFound && hasUbuntu);
        SetButtonVisibility(_wslInstallCudaToolkitButton, report.WslExeFound && hasUbuntu);
        SetButtonVisibility(_wslInstallVulkanToolsButton, report.WslExeFound && hasUbuntu);
        SetButtonVisibility(_wslInstallSyclRuntimeButton, report.WslExeFound && hasUbuntu);
        SetButtonVisibility(_wslInstallSyclOneApiButton, report.WslExeFound && hasUbuntu);
        SetButtonVisibility(_wslCheckUbuntuUpdatesButton, report.WslExeFound && hasUbuntu);
        SetButtonVisibility(_wslDeleteUbuntuButton, report.WslExeFound && hasUbuntu);
        SetButtonVisibility(_wslDeleteBuildToolsButton, report.WslExeFound && hasUbuntu && tools.CpuToolsInstalled);
        SetButtonVisibility(_wslDeleteCudaToolkitButton, report.WslExeFound && hasUbuntu && tools.CudaToolsInstalled);
        SetButtonVisibility(_wslDeleteVulkanToolsButton, report.WslExeFound && hasUbuntu && tools.VulkanToolsInstalled);
        SetButtonVisibility(_wslDeleteSyclRuntimeButton, report.WslExeFound && hasUbuntu && tools.SyclToolsInstalled);
        SetButtonVisibility(_wslDeleteSyclOneApiButton, report.WslExeFound && hasUbuntu && tools.SyclToolsInstalled);
        if (_wslInstallBuildToolsButton is not null)
            _wslInstallBuildToolsButton.Content = WslEnvironmentService.CpuToolsActionLabel(tools);
        if (_wslInstallCudaToolkitButton is not null)
            _wslInstallCudaToolkitButton.Content = WslEnvironmentService.CudaToolsActionLabel(tools);
        if (_wslInstallVulkanToolsButton is not null)
            _wslInstallVulkanToolsButton.Content = WslEnvironmentService.VulkanToolsActionLabel(tools);
        if (_wslInstallSyclRuntimeButton is not null)
            _wslInstallSyclRuntimeButton.Content = tools.SyclToolsInstalled ? "Update Intel GPU" : "Install Intel GPU";
        if (_wslInstallSyclOneApiButton is not null)
            _wslInstallSyclOneApiButton.Content = WslEnvironmentService.SyclToolsActionLabel(tools);
    }

    private static void SetButtonVisibility(WpfButton? button, bool visible)
    {
        if (button is null) return;
        button.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
    }

}
