using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using WpfApplication = System.Windows.Application;
using WpfBinding = System.Windows.Data.Binding;
using WpfBrush = System.Windows.Media.Brush;
using WpfButton = System.Windows.Controls.Button;

namespace LocalLlmConsole;

public sealed record WslPageActions(
    Func<Task> RefreshAsync,
    Func<Task> InstallWslAsync,
    Func<Task> CheckWslUpdatesAsync,
    Func<Task> DeleteWslAsync,
    Func<Task> InstallUbuntuAsync,
    Func<Task> CheckUbuntuUpdatesAsync,
    Func<Task> DeleteUbuntuAsync,
    Func<Task> InstallBuildToolsAsync,
    Func<Task> DeleteBuildToolsAsync,
    Func<Task> InstallCudaToolkitAsync,
    Func<Task> DeleteCudaToolkitAsync,
    Func<Task> InstallVulkanToolsAsync,
    Func<Task> DeleteVulkanToolsAsync,
    Func<Task> InstallSyclRuntimeAsync,
    Func<Task> DeleteSyclRuntimeAsync,
    Func<Task> InstallSyclOneApiAsync,
    Func<Task> DeleteSyclOneApiAsync,
    RoutedEventHandler UseDistroRowClick);

public sealed record WslPageRequest(
    MainWindowViewModel ViewModel,
    WslPageActions Actions,
    Func<string, string> ButtonToolTip);

public sealed record WslPageControls(
    DockPanel Root,
    Grid StatusMetric,
    Grid SelectedMetric,
    Grid InfoMetric,
    Grid ToolsMetric,
    DataGrid DistroGrid,
    WpfButton InstallButton,
    WpfButton CheckUpdatesButton,
    WpfButton DeleteButton,
    WpfButton InstallUbuntuButton,
    WpfButton CheckUbuntuUpdatesButton,
    WpfButton DeleteUbuntuButton,
    WpfButton InstallBuildToolsButton,
    WpfButton DeleteBuildToolsButton,
    WpfButton InstallCudaToolkitButton,
    WpfButton DeleteCudaToolkitButton,
    WpfButton InstallVulkanToolsButton,
    WpfButton DeleteVulkanToolsButton,
    WpfButton InstallSyclRuntimeButton,
    WpfButton DeleteSyclRuntimeButton,
    WpfButton InstallSyclOneApiButton,
    WpfButton DeleteSyclOneApiButton);

public static class WslPageFactory
{
    public static WslPageControls Create(WslPageRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.ViewModel);
        ArgumentNullException.ThrowIfNull(request.Actions);
        ArgumentNullException.ThrowIfNull(request.ButtonToolTip);

        var root = new DockPanel { Margin = new Thickness(16) };
        var toolbar = Bar();
        toolbar.Children.Add(Button("Refresh", request.Actions.RefreshAsync, request.ButtonToolTip));
        DockPanel.SetDock(toolbar, Dock.Top);
        root.Children.Add(toolbar);

        var body = new StackPanel();
        var setupRows = SetupRows(request, out var buttons);
        body.Children.Add(setupRows);

        var statusGrid = StatusGrid(out var statusMetric, out var selectedMetric, out var infoMetric, out var toolsMetric);
        body.Children.Add(statusGrid);

        body.Children.Add(Text(
            "The selected distro is used for WSL llama.cpp launches and builds. Ubuntu is recommended; other real Linux distros remain selectable when detected.",
            muted: true));

        var distroGrid = DistroGrid(request);
        body.Children.Add(PageSectionFactory.GridSection("Installed Linux distros", distroGrid));

        root.Children.Add(Scroll(body, new Thickness(16)));

        return new WslPageControls(
            root,
            statusMetric,
            selectedMetric,
            infoMetric,
            toolsMetric,
            distroGrid,
            buttons.InstallWsl,
            buttons.CheckWslUpdates,
            buttons.DeleteWsl,
            buttons.InstallUbuntu,
            buttons.CheckUbuntuUpdates,
            buttons.DeleteUbuntu,
            buttons.InstallBuildTools,
            buttons.DeleteBuildTools,
            buttons.InstallCudaToolkit,
            buttons.DeleteCudaToolkit,
            buttons.InstallVulkanTools,
            buttons.DeleteVulkanTools,
            buttons.InstallSyclRuntime,
            buttons.DeleteSyclRuntime,
            buttons.InstallSyclOneApi,
            buttons.DeleteSyclOneApi);
    }

    private static UIElement SetupRows(WslPageRequest request, out WslSetupButtons buttons)
    {
        var panel = new StackPanel { Margin = new Thickness(0, 0, 0, 12) };
        panel.Children.Add(SetupRow(
            "WSL",
            "Windows Subsystem for Linux",
            out var installWslButton,
            "Install WSL",
            request.Actions.InstallWslAsync,
            out var checkWslUpdatesButton,
            "Update WSL",
            request.Actions.CheckWslUpdatesAsync,
            out var deleteWslButton,
            "Delete WSL",
            request.Actions.DeleteWslAsync,
            request.ButtonToolTip));
        panel.Children.Add(SetupRow(
            "Ubuntu",
            "Recommended Linux distro for llama.cpp builds",
            out var installUbuntuButton,
            "Install Ubuntu",
            request.Actions.InstallUbuntuAsync,
            out var checkUbuntuUpdatesButton,
            "Update Ubuntu",
            request.Actions.CheckUbuntuUpdatesAsync,
            out var deleteUbuntuButton,
            "Delete Ubuntu",
            request.Actions.DeleteUbuntuAsync,
            request.ButtonToolTip));
        panel.Children.Add(ToolActionRow(
            "CPU tools",
            "Install Git, CMake, compiler, pkg-config, libcurl headers, ccache, and Ninja inside the selected Ubuntu distro. CUDA presets require a separate NVIDIA CUDA Toolkit install in WSL.",
            out var installBuildToolsButton,
            "Install CPU Tools",
            request.Actions.InstallBuildToolsAsync,
            out var deleteBuildToolsButton,
            "Delete",
            request.Actions.DeleteBuildToolsAsync));
        panel.Children.Add(ToolActionRow(
            "CUDA tools",
            $"Install NVIDIA's WSL CUDA Toolkit package ({WslSetupCommands.CudaToolkitPackage}) inside the selected Ubuntu distro for CUDA runtime builds.",
            out var installCudaToolkitButton,
            "Install CUDA",
            request.Actions.InstallCudaToolkitAsync,
            out var deleteCudaToolkitButton,
            "Delete",
            request.Actions.DeleteCudaToolkitAsync));
        panel.Children.Add(ToolActionRow(
            "Vulkan tools",
            $"Install Ubuntu Vulkan build/runtime tools ({WslSetupCommands.VulkanToolsPackages}) inside the selected Ubuntu distro for official llama.cpp Vulkan builds.",
            out var installVulkanToolsButton,
            "Install Vulkan",
            request.Actions.InstallVulkanToolsAsync,
            out var deleteVulkanToolsButton,
            "Delete",
            request.Actions.DeleteVulkanToolsAsync));
        panel.Children.Add(ToolActionRow(
            "Intel GPU runtime",
            $"Install Intel Level Zero/OpenCL runtime packages ({WslSetupCommands.SyclRuntimePackages}) inside the selected Ubuntu distro for Intel Arc SYCL runtimes.",
            out var installSyclRuntimeButton,
            "Install Intel GPU",
            request.Actions.InstallSyclRuntimeAsync,
            out var deleteSyclRuntimeButton,
            "Delete",
            request.Actions.DeleteSyclRuntimeAsync));
        panel.Children.Add(ToolActionRow(
            "Intel oneAPI",
            $"Install Intel oneAPI DPC++ compiler, MKL, and DNNL packages ({WslSetupCommands.SyclOneApiPackages}) inside the selected Ubuntu distro for llama.cpp SYCL builds.",
            out var installSyclOneApiButton,
            "Install oneAPI",
            request.Actions.InstallSyclOneApiAsync,
            out var deleteSyclOneApiButton,
            "Delete",
            request.Actions.DeleteSyclOneApiAsync));

        buttons = new WslSetupButtons(
            installWslButton,
            checkWslUpdatesButton,
            deleteWslButton,
            installUbuntuButton,
            checkUbuntuUpdatesButton,
            deleteUbuntuButton,
            installBuildToolsButton,
            deleteBuildToolsButton,
            installCudaToolkitButton,
            deleteCudaToolkitButton,
            installVulkanToolsButton,
            deleteVulkanToolsButton,
            installSyclRuntimeButton,
            deleteSyclRuntimeButton,
            installSyclOneApiButton,
            deleteSyclOneApiButton);
        return panel;
    }

    private static Grid StatusGrid(out Grid statusMetric, out Grid selectedMetric, out Grid infoMetric, out Grid toolsMetric)
    {
        var statusGrid = new Grid { Margin = new Thickness(0, 0, 0, 10) };
        statusGrid.ColumnDefinitions.Add(new ColumnDefinition());
        statusGrid.ColumnDefinitions.Add(new ColumnDefinition());
        statusGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        statusGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        statusMetric = MetricCardFactory.AddMetric(statusGrid, "WSL status", 0, 0);
        selectedMetric = MetricCardFactory.AddMetric(statusGrid, "Selected distro", 0, 1);
        infoMetric = MetricCardFactory.AddMetric(statusGrid, "WSL info", 1, 0);
        toolsMetric = MetricCardFactory.AddMetric(statusGrid, "Tools", 1, 1);
        return statusGrid;
    }

    private static DataGrid DistroGrid(WslPageRequest request)
    {
        var grid = PageSectionFactory.GridFor(
            ("Distro", "C2", 1.4),
            ("State", "C3", .7),
            ("WSL", "C4", .45),
            ("Notes", "C5", 2.3));
        PageSectionFactory.AddButtonColumn(grid, "Action", "C6", "B1", request.Actions.UseDistroRowClick, .55, tooltipBinding: "T1");
        grid.ItemsSource = request.ViewModel.WslLinux.Rows;
        return grid;
    }

    private static Grid SetupRow(
        string label,
        string description,
        out WpfButton installButton,
        string installText,
        Func<Task> installClick,
        out WpfButton updateButton,
        string updateText,
        Func<Task> updateClick,
        out WpfButton deleteButton,
        string deleteText,
        Func<Task> deleteClick,
        Func<string, string> buttonToolTip)
    {
        var row = BaseRow(columnCount: 5, label, description);
        installButton = Button(installText, installClick, buttonToolTip);
        Grid.SetColumn(installButton, 2);
        row.Children.Add(installButton);
        updateButton = Button(updateText, updateClick, buttonToolTip);
        Grid.SetColumn(updateButton, 3);
        row.Children.Add(updateButton);
        deleteButton = Button(deleteText, deleteClick, buttonToolTip);
        Grid.SetColumn(deleteButton, 4);
        row.Children.Add(deleteButton);
        return row;
    }

    private static Grid ToolActionRow(
        string label,
        string description,
        out WpfButton actionButton,
        string actionText,
        Func<Task> actionClick,
        out WpfButton deleteButton,
        string deleteText,
        Func<Task> deleteClick)
    {
        var row = BaseRow(columnCount: 4, label, description);
        actionButton = Button(actionText, actionClick, _ => $"Install or repair {label.ToLowerInvariant()} in the selected Ubuntu distro.");
        Grid.SetColumn(actionButton, 2);
        row.Children.Add(actionButton);
        deleteButton = Button(deleteText, deleteClick, _ => $"Remove {label.ToLowerInvariant()} from the selected Ubuntu distro.");
        Grid.SetColumn(deleteButton, 3);
        row.Children.Add(deleteButton);
        return row;
    }

    private static Grid BaseRow(int columnCount, string label, string description)
    {
        var row = new Grid { Margin = new Thickness(0, 0, 0, 8) };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(110) });
        row.ColumnDefinitions.Add(new ColumnDefinition());
        for (var i = 2; i < columnCount; i++)
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        row.Children.Add(new TextBlock
        {
            Text = label,
            FontWeight = FontWeights.SemiBold,
            Foreground = (WpfBrush)WpfApplication.Current.Resources["TextSoft"],
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 10, 0)
        });
        var descriptionText = new TextBlock
        {
            Text = description,
            Foreground = (WpfBrush)WpfApplication.Current.Resources["TextMuted"],
            TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 10, 0)
        };
        Grid.SetColumn(descriptionText, 1);
        row.Children.Add(descriptionText);
        return row;
    }

    private static ScrollViewer Scroll(UIElement child, Thickness padding)
    {
        var viewer = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
        };
        var content = new Border { Padding = padding, Child = child };
        content.SetBinding(FrameworkElement.WidthProperty, new WpfBinding(nameof(ScrollViewer.ViewportWidth)) { Source = viewer });
        viewer.Content = content;
        viewer.Loaded += (_, _) => viewer.Dispatcher.BeginInvoke(new Action(viewer.ScrollToTop), System.Windows.Threading.DispatcherPriority.ContextIdle);
        return viewer;
    }

    private static WrapPanel Bar()
        => new() { Orientation = System.Windows.Controls.Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 10) };

    private static TextBlock Text(string text, bool muted = false) => new()
    {
        Text = text,
        FontSize = 13,
        Foreground = muted ? (WpfBrush)WpfApplication.Current.Resources["TextMuted"] : (WpfBrush)WpfApplication.Current.Resources["TextMain"],
        TextWrapping = TextWrapping.Wrap,
        Margin = new Thickness(0, 0, 0, 8)
    };

    private static WpfButton Button(string text, Func<Task> click, Func<string, string> toolTip)
    {
        var button = new WpfButton
        {
            Content = text,
            ToolTip = toolTip(text)
        };
        ToolTipService.SetShowOnDisabled(button, true);
        button.Click += async (_, _) => await click();
        return button;
    }

    private sealed record WslSetupButtons(
        WpfButton InstallWsl,
        WpfButton CheckWslUpdates,
        WpfButton DeleteWsl,
        WpfButton InstallUbuntu,
        WpfButton CheckUbuntuUpdates,
        WpfButton DeleteUbuntu,
        WpfButton InstallBuildTools,
        WpfButton DeleteBuildTools,
        WpfButton InstallCudaToolkit,
        WpfButton DeleteCudaToolkit,
        WpfButton InstallVulkanTools,
        WpfButton DeleteVulkanTools,
        WpfButton InstallSyclRuntime,
        WpfButton DeleteSyclRuntime,
        WpfButton InstallSyclOneApi,
        WpfButton DeleteSyclOneApi);
}
