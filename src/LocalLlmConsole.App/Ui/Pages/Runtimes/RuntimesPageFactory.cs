using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using WpfApplication = System.Windows.Application;
using WpfBrush = System.Windows.Media.Brush;
using WpfButton = System.Windows.Controls.Button;
using WpfComboBox = System.Windows.Controls.ComboBox;

namespace LocalLlmConsole;

public sealed record RuntimesPageActions(
    Func<Task> ChooseRuntimeFolderAsync,
    Func<Task> ChangeCudaPackagePreferenceAsync,
    Action ToggleAdvancedRuntimes,
    MouseButtonEventHandler RuntimeGridPreviewMouseLeftButtonDown,
    RoutedEventHandler BuildRuntimeRowClick,
    RoutedEventHandler DeleteRuntimeRowClick,
    RoutedEventHandler InstallRuntimePackageRowClick,
    RoutedEventHandler CheckRuntimePackageUpdateRowClick,
    RoutedEventHandler DeleteRuntimePackageRowClick,
    RoutedEventHandler DownloadRuntimePresetRowClick,
    RoutedEventHandler CheckRuntimePresetUpdateRowClick,
    RoutedEventHandler DeleteRuntimePresetRowClick,
    RoutedEventHandler OpenRuntimeJobLogRowClick,
    RoutedEventHandler CancelRuntimeJobRowClick,
    RoutedEventHandler RetryRuntimeJobRowClick,
    RoutedEventHandler ClearRuntimeJobRowClick,
    Action<DataGrid> ConfigureRuntimeGridColumnSizing,
    Action<DataGrid> ConfigureRuntimeBuildGridColumnSizing,
    Action<DataGrid> ConfigureRuntimeJobsGridColumnSizing);

public sealed record RuntimesPageRequest(
    MainWindowViewModel ViewModel,
    string RuntimeRoot,
    bool ShowAdvancedRuntimes,
    string CudaPackagePreference,
    RuntimesPageActions Actions);

public sealed record RuntimesPageControls(
    Grid Root,
    TextBlock RuntimesFolderText,
    DataGrid RuntimeGrid,
    DataGrid RuntimePackageGrid,
    DataGrid? RuntimeBuildGrid,
    DataGrid? RuntimeJobsGrid,
    WpfButton RuntimeAdvancedToggleButton,
    WpfComboBox RuntimeCudaPreferenceCombo);

public static class RuntimesPageFactory
{
    public const string InstalledLocalBuildsTitle = "Installed Local Builds";
    public const string RuntimeDownloadsTitle = "Runtime Downloads";
    public const string BuildFromSourceTitle = "Build From Source (Advanced)";
    public const string RuntimeJobsTitle = "Runtime Jobs";

    public static RuntimesPageControls Create(RuntimesPageRequest request)
    {
        var root = RootGrid(request.ShowAdvancedRuntimes);
        var (header, runtimesFolderText, runtimeAdvancedToggleButton, runtimeCudaPreferenceCombo) = Header(request);
        Grid.SetRow(header, 0);
        root.Children.Add(header);

        var runtimeGrid = InstalledRuntimesGrid(request);
        var runtimeSection = PageSectionFactory.GridSection(
            InstalledLocalBuildsTitle,
            runtimeGrid,
            "Registered llama-server builds found on disk. Build from downloaded source or delete unused builds that are not actively serving a model.");
        Grid.SetRow(runtimeSection, 1);
        root.Children.Add(runtimeSection);
        root.Children.Add(PageSectionFactory.HorizontalGridSplitter(2));

        var runtimePackageGrid = RuntimePackageGrid(request);
        var packageSection = PageSectionFactory.GridSection(
            RuntimeDownloadsTitle,
            runtimePackageGrid,
            "Install prebuilt llama.cpp releases. Build tools are not required for these downloads.");
        Grid.SetRow(packageSection, 3);
        root.Children.Add(packageSection);
        if (request.ShowAdvancedRuntimes)
            root.Children.Add(PageSectionFactory.HorizontalGridSplitter(4));

        var runtimeBuildGrid = request.ShowAdvancedRuntimes ? RuntimeBuildGrid(request) : null;
        var runtimeJobsGrid = request.ShowAdvancedRuntimes ? RuntimeJobsGrid(request) : null;
        if (request.ShowAdvancedRuntimes)
        {
            var buildSection = PageSectionFactory.GridSection(
                BuildFromSourceTitle,
                runtimeBuildGrid!,
                "Use source builds for custom forks, patches, branches, or runtime targets without a prebuilt release.");
            Grid.SetRow(buildSection, 5);
            root.Children.Add(buildSection);
            root.Children.Add(PageSectionFactory.HorizontalGridSplitter(6));

            var jobsSection = PageSectionFactory.GridSection(
                RuntimeJobsTitle,
                runtimeJobsGrid!,
                "Recent runtime download and build work. Use Log to inspect compiler, git, Windows, or WSL output.");
            Grid.SetRow(jobsSection, 7);
            root.Children.Add(jobsSection);
        }

        return new RuntimesPageControls(
            root,
            runtimesFolderText,
            runtimeGrid,
            runtimePackageGrid,
            runtimeBuildGrid,
            runtimeJobsGrid,
            runtimeAdvancedToggleButton,
            runtimeCudaPreferenceCombo);
    }

    private static Grid RootGrid(bool showAdvancedRuntimes)
    {
        var root = new Grid { Margin = new Thickness(16) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(showAdvancedRuntimes ? .72 : 1, GridUnitType.Star), MinHeight = 86 });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(8) });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(showAdvancedRuntimes ? .72 : 1, GridUnitType.Star), MinHeight = 94 });
        if (showAdvancedRuntimes)
        {
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(8) });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(.72, GridUnitType.Star), MinHeight = 94 });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(8) });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(.82, GridUnitType.Star), MinHeight = 120 });
        }

        return root;
    }

    private static (Grid Header, TextBlock RuntimesFolderText, WpfButton AdvancedToggle, WpfComboBox CudaPreferenceCombo) Header(RuntimesPageRequest request)
    {
        var folderStrip = FolderStripActionsFirst(
            "Runtimes folder",
            request.RuntimeRoot,
            out var runtimesFolderText,
            ("Choose Folder", request.Actions.ChooseRuntimeFolderAsync));
        var header = new Grid { Margin = new Thickness(0, 0, 0, 8) };
        header.ColumnDefinitions.Add(new ColumnDefinition());
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        folderStrip.Margin = new Thickness(0);
        header.Children.Add(folderStrip);
        var rightActions = new StackPanel
        {
            Orientation = System.Windows.Controls.Orientation.Horizontal,
            HorizontalAlignment = System.Windows.HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center
        };
        rightActions.Children.Add(new TextBlock
        {
            Text = "CUDA downloads",
            Foreground = (WpfBrush)WpfApplication.Current.Resources["TextMuted"],
            FontSize = 12,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(12, 0, 8, 6)
        });
        var runtimeCudaPreferenceCombo = LaunchCombo(AppPreferenceService.CudaPackagePreferenceOptions());
        runtimeCudaPreferenceCombo.Width = 132;
        runtimeCudaPreferenceCombo.SelectedItem = AppPreferenceService.CudaPackagePreferenceLabel(request.CudaPackagePreference);
        runtimeCudaPreferenceCombo.ToolTip = TooltipText("Choose whether official CUDA runtime downloads prefer the newest CUDA asset or the CUDA 12 compatibility asset.");
        runtimeCudaPreferenceCombo.SelectionChanged += async (_, _) => await request.Actions.ChangeCudaPackagePreferenceAsync();
        rightActions.Children.Add(runtimeCudaPreferenceCombo);
        var runtimeAdvancedToggleButton = Button(request.ShowAdvancedRuntimes ? "Hide advanced" : "Show advanced", () =>
        {
            request.Actions.ToggleAdvancedRuntimes();
            return Task.CompletedTask;
        });
        runtimeAdvancedToggleButton.ToolTip = TooltipText(request.ShowAdvancedRuntimes ? "Hide source builds and runtime job history." : "Show source builds and runtime job history.");
        ToolTipService.SetShowOnDisabled(runtimeAdvancedToggleButton, true);
        runtimeAdvancedToggleButton.Margin = new Thickness(12, 0, 0, 6);
        rightActions.Children.Add(runtimeAdvancedToggleButton);
        Grid.SetColumn(rightActions, 1);
        header.Children.Add(rightActions);
        return (header, runtimesFolderText, runtimeAdvancedToggleButton, runtimeCudaPreferenceCombo);
    }

    private static DataGrid InstalledRuntimesGrid(RuntimesPageRequest request)
    {
        var grid = PageSectionFactory.GridFor(
            ("Name", nameof(RuntimeCatalogRow.Name), 1.4),
            ("Backend", nameof(RuntimeCatalogRow.Backend), .55),
            ("State", nameof(RuntimeCatalogRow.State), .55),
            ("Location", nameof(RuntimeCatalogRow.Location), 3));
        grid.RowDetailsVisibilityMode = DataGridRowDetailsVisibilityMode.VisibleWhenSelected;
        grid.RowDetailsTemplate = PageSectionFactory.RowDetailsTemplate(nameof(RuntimeCatalogRow.Details));
        grid.PreviewMouseLeftButtonDown += request.Actions.RuntimeGridPreviewMouseLeftButtonDown;
        PageSectionFactory.AddButtonColumn(grid, "Build", nameof(RuntimeCatalogRow.BuildAction), nameof(RuntimeCatalogRow.CanBuild), request.Actions.BuildRuntimeRowClick, .65, tooltipBinding: nameof(RuntimeCatalogRow.BuildToolTip));
        PageSectionFactory.AddButtonColumn(grid, "Action", nameof(RuntimeCatalogRow.DeleteAction), nameof(RuntimeCatalogRow.CanDelete), request.Actions.DeleteRuntimeRowClick, .65, tooltipBinding: nameof(RuntimeCatalogRow.DeleteToolTip));
        PageSectionFactory.ApplyGridTextMargin(grid, new Thickness(6, 0, 6, 0));
        request.Actions.ConfigureRuntimeGridColumnSizing(grid);
        grid.ItemsSource = request.ViewModel.Runtimes.Rows;
        return grid;
    }

    private static DataGrid RuntimePackageGrid(RuntimesPageRequest request)
    {
        var grid = PageSectionFactory.GridFor(
            ("Runtime", nameof(RuntimePackagePresetRow.Label), 1.45),
            ("Backend", nameof(RuntimePackagePresetRow.Backend), .68),
            ("Local", nameof(RuntimePackagePresetRow.LocalStatus), .78),
            ("Latest Release", nameof(RuntimePackagePresetRow.LatestRelease), 1.2),
            ("Assets", nameof(RuntimePackagePresetRow.Assets), 2.35));
        PageSectionFactory.AddButtonColumn(grid, "Install", nameof(RuntimePackagePresetRow.InstallAction), nameof(RuntimePackagePresetRow.CanInstall), request.Actions.InstallRuntimePackageRowClick, .75, tooltipBinding: nameof(RuntimePackagePresetRow.InstallToolTip));
        PageSectionFactory.AddButtonColumn(grid, "Update", nameof(RuntimePackagePresetRow.CheckAction), nameof(RuntimePackagePresetRow.CanCheck), request.Actions.CheckRuntimePackageUpdateRowClick, .75, tooltipBinding: nameof(RuntimePackagePresetRow.CheckToolTip));
        PageSectionFactory.AddButtonColumn(grid, "Delete", nameof(RuntimePackagePresetRow.DeleteAction), nameof(RuntimePackagePresetRow.CanDelete), request.Actions.DeleteRuntimePackageRowClick, .75, tooltipBinding: nameof(RuntimePackagePresetRow.DeleteToolTip));
        PageSectionFactory.ApplyGridTextMargin(grid, new Thickness(6, 0, 6, 0));
        request.Actions.ConfigureRuntimeBuildGridColumnSizing(grid);
        grid.ItemsSource = request.ViewModel.RuntimePackages.Rows;
        return grid;
    }

    private static DataGrid RuntimeBuildGrid(RuntimesPageRequest request)
    {
        var grid = PageSectionFactory.GridFor(
            ("Repository", nameof(RuntimeBuildPresetRow.Label), 1.4),
            ("Backend", nameof(RuntimeBuildPresetRow.Backend), .7),
            ("Local", nameof(RuntimeBuildPresetRow.LocalStatus), .85),
            ("Latest Local", nameof(RuntimeBuildPresetRow.LatestLocal), 1.2),
            ("Source", nameof(RuntimeBuildPresetRow.Source), 2.3));
        grid.IsReadOnly = false;
        PageSectionFactory.AddButtonColumn(grid, "Download", nameof(RuntimeBuildPresetRow.DownloadAction), nameof(RuntimeBuildPresetRow.CanDownload), request.Actions.DownloadRuntimePresetRowClick, .75, tooltipBinding: nameof(RuntimeBuildPresetRow.DownloadToolTip));
        PageSectionFactory.AddButtonColumn(grid, "Update", nameof(RuntimeBuildPresetRow.CheckAction), nameof(RuntimeBuildPresetRow.CanCheck), request.Actions.CheckRuntimePresetUpdateRowClick, .75, tooltipBinding: nameof(RuntimeBuildPresetRow.CheckToolTip));
        PageSectionFactory.AddButtonColumn(grid, "Delete", nameof(RuntimeBuildPresetRow.DeleteAction), nameof(RuntimeBuildPresetRow.CanDelete), request.Actions.DeleteRuntimePresetRowClick, .75, tooltipBinding: nameof(RuntimeBuildPresetRow.DeleteToolTip));
        PageSectionFactory.ApplyGridTextMargin(grid, new Thickness(6, 0, 6, 0));
        request.Actions.ConfigureRuntimeBuildGridColumnSizing(grid);
        grid.ItemsSource = request.ViewModel.RuntimeBuilds.Rows;
        return grid;
    }

    private static DataGrid RuntimeJobsGrid(RuntimesPageRequest request)
    {
        var grid = PageSectionFactory.GridFor(
            ("Status", "C1", .8),
            ("Kind", "C2", 1),
            ("Updated", "C4", 1.1),
            ("Payload", "C5", 3.2));
        PageSectionFactory.AddButtonColumn(grid, "Log", "C6", "B1", request.Actions.OpenRuntimeJobLogRowClick, .55, tooltipBinding: "T1");
        PageSectionFactory.AddButtonColumn(grid, "Cancel", "C7", "B2", request.Actions.CancelRuntimeJobRowClick, .7, tooltipBinding: "T2");
        PageSectionFactory.AddButtonColumn(grid, "Retry", "C8", "B3", request.Actions.RetryRuntimeJobRowClick, .65, tooltipBinding: "T3");
        PageSectionFactory.AddButtonColumn(grid, "Clear", "C9", "B4", request.Actions.ClearRuntimeJobRowClick, .65, tooltipBinding: "T4");
        PageSectionFactory.ApplyRuntimeJobsRowStyle(grid);
        request.Actions.ConfigureRuntimeJobsGridColumnSizing(grid);
        grid.ItemsSource = request.ViewModel.Jobs.RuntimeRows;
        return grid;
    }

    private static Grid FolderStripActionsFirst(string label, string path, out TextBlock pathText, params (string Text, Func<Task> Click)[] actions)
    {
        var grid = new Grid { Margin = new Thickness(0, 0, 0, 8) };
        var column = 0;
        foreach (var _ in actions)
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) });
        grid.ColumnDefinitions.Add(new ColumnDefinition());

        foreach (var action in actions)
        {
            var button = Button(action.Text, action.Click);
            Grid.SetColumn(button, column++);
            grid.Children.Add(button);
        }

        var labelBlock = new TextBlock
        {
            Text = label,
            FontWeight = FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(4, 0, 10, 6)
        };
        Grid.SetColumn(labelBlock, column++);
        grid.Children.Add(labelBlock);

        pathText = new TextBlock
        {
            Text = path,
            Foreground = (WpfBrush)WpfApplication.Current.Resources["TextMuted"],
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 10, 6)
        };
        Grid.SetColumn(pathText, column);
        grid.Children.Add(pathText);
        return grid;
    }

    private static WpfComboBox LaunchCombo(IEnumerable<string> values) => new()
    {
        ItemsSource = values.ToArray(),
        SelectedIndex = 0,
        MinHeight = 27,
        MinWidth = 76,
        Margin = new Thickness(0, 0, 6, 4),
        HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch
    };

    private static WpfButton Button(string text, Func<Task> click)
    {
        var button = new WpfButton { Content = text, ToolTip = TooltipText(ButtonToolTip(text)) };
        ToolTipService.SetShowOnDisabled(button, true);
        button.Click += async (_, _) => await click();
        return button;
    }

    private static string ButtonToolTip(string text)
        => (text ?? "").Trim() switch
        {
            "Choose Folder" => "Choose a folder.",
            "Show advanced" => "Show source builds and runtime job history.",
            "Hide advanced" => "Hide source builds and runtime job history.",
            var label => string.IsNullOrWhiteSpace(label) ? "" : $"Run {label}."
        };

    private static string TooltipText(string text) => text;
}
