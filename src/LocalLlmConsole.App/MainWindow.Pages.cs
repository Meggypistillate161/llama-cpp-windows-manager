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
    private void ShowOverview()
    {
        SetPage("Overview", "Windows WPF shell supervising llama.cpp in Ubuntu/WSL.");
        var root = new Grid { Margin = new Thickness(16) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1.08, GridUnitType.Star), MinHeight = 150 });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(8) });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(.92, GridUnitType.Star), MinHeight = 130 });

        var modelBar = new Grid { Margin = new Thickness(0, 0, 0, 10) };
        modelBar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) });
        modelBar.ColumnDefinitions.Add(new ColumnDefinition());
        modelBar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        modelBar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        modelBar.Children.Add(new TextBlock
        {
            Text = "Model",
            FontWeight = FontWeights.SemiBold,
            Foreground = (System.Windows.Media.Brush)WpfApplication.Current.Resources["TextSoft"],
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 10, 6)
        });
        _overviewModelCombo = new WpfComboBox
        {
            ItemsSource = _viewModel.Overview.ModelChoices,
            DisplayMemberPath = nameof(ModelRecord.Name),
            SelectedValuePath = nameof(ModelRecord.Id),
            MinHeight = 30,
            Margin = new Thickness(0, 0, 8, 6),
            HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch,
            ToolTip = TooltipText("Choose a local model to load with its saved launch profile.")
        };
        _overviewModelCombo.SelectionChanged += (_, _) => UpdateOverviewModelActions();
        Grid.SetColumn(_overviewModelCombo, 1);
        modelBar.Children.Add(_overviewModelCombo);
        _overviewLoadButton = Button("Load", async (_, _) => await LoadOverviewSelectedModelAsync());
        Grid.SetColumn(_overviewLoadButton, 2);
        modelBar.Children.Add(_overviewLoadButton);
        _overviewUnloadButton = Button("Unload", async (_, _) => await UnloadOverviewSelectedModelAsync());
        Grid.SetColumn(_overviewUnloadButton, 3);
        modelBar.Children.Add(_overviewUnloadButton);
        Grid.SetRow(modelBar, 0);
        root.Children.Add(modelBar);

        var dashboardSection = Stack();
        dashboardSection.Children.Add(Text("Model Status", 18, true));
        var runtimeDashboard = new Grid { Margin = new Thickness(0, 2, 0, 8) };
        runtimeDashboard.ColumnDefinitions.Add(new ColumnDefinition());
        runtimeDashboard.ColumnDefinitions.Add(new ColumnDefinition());
        runtimeDashboard.ColumnDefinitions.Add(new ColumnDefinition());
        for (var row = 0; row < 2; row++)
            runtimeDashboard.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        _runtimeDashboardModel = AddMetric(runtimeDashboard, "Model status", 0, 0, includeProgress: true, out _runtimeDashboardModelProgress);
        _runtimeDashboardRuntime = AddMetric(runtimeDashboard, "Runtime build", 0, 1);
        _runtimeDashboardRequests = AddMetric(runtimeDashboard, "Settings", 0, 2);
        _runtimeDashboardGenerationRate = AddMetric(runtimeDashboard, "Tokens (Live)", 1, 0);
        _runtimeDashboardTotalTokens = AddMetric(runtimeDashboard, "Tokens (Total)", 1, 1);
        _runtimeDashboardGpu = AddMetric(runtimeDashboard, "GPU", 1, 2);
        dashboardSection.Children.Add(runtimeDashboard);
        Grid.SetRow(dashboardSection, 1);
        root.Children.Add(dashboardSection);

        _overviewRuntimeLogBox = new WpfTextBox
        {
            IsReadOnly = true,
            Text = "No runtime log is active.",
            BorderThickness = new Thickness(0),
            Margin = new Thickness(0),
            TextWrapping = TextWrapping.NoWrap,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        var runtimeLogSection = FramedSection("Live Runtime Log", _overviewRuntimeLogBox);
        Grid.SetRow(runtimeLogSection, 2);
        root.Children.Add(runtimeLogSection);
        root.Children.Add(HorizontalGridSplitter(3));

        _runtimeMetricsGrid = GridFor(("Metric", "C1", 1.5), ("Labels", "C2", 2.2), ("Value", "C3", .9), ("Type", "C4", .7), ("Help", "C5", 3));
        _runtimeMetricsGrid.ItemsSource = _viewModel.RuntimeMetrics.Rows;
        _runtimeMetricsGrid.VerticalAlignment = VerticalAlignment.Stretch;
        SetRuntimeMetricsGridColumnSizing(_runtimeMetricsGrid);
        var metricsSection = GridSection("All llama.cpp Metrics", _runtimeMetricsGrid);
        Grid.SetRow(metricsSection, 4);
        root.Children.Add(metricsSection);

        PageHost.Content = root;
        RunBackground(RefreshOverviewAsync, "Overview refresh failed");
        RunBackground(RefreshOverviewModelSelectorAsync, "Overview model refresh failed");
        RunBackground(RefreshRuntimeMetricsAsync, "Runtime metrics refresh failed");
        StartRuntimeDashboardRefreshTimer();
    }

    private static Grid FramedSection(string title, UIElement child)
    {
        var section = new Grid { Margin = new Thickness(0, 0, 0, 6) };
        section.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        section.RowDefinitions.Add(new RowDefinition());
        section.Children.Add(new TextBlock
        {
            Text = title,
            FontSize = 14,
            FontWeight = FontWeights.SemiBold,
            Foreground = (System.Windows.Media.Brush)WpfApplication.Current.Resources["TextSoft"],
            Margin = new Thickness(2, 0, 0, 4)
        });
        var frame = new Border
        {
            Background = (System.Windows.Media.Brush)WpfApplication.Current.Resources["InputBack"],
            BorderBrush = (System.Windows.Media.Brush)WpfApplication.Current.Resources["PanelBorder"],
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Margin = new Thickness(0, 6, 0, 6),
            Child = child
        };
        Grid.SetRow(frame, 1);
        section.Children.Add(frame);
        return section;
    }

    private void ShowModels()
    {
        SetPage("Models", "Scan, import, download, configure, and safely remove models.");
        _loadModelButton = null;
        _restartModelButton = null;
        _unloadModelButton = null;
        var root = new Grid { Margin = new Thickness(16) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star), MinHeight = 260 });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(8) });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(230), MinHeight = 120 });

        var folderStrip = FolderStripActionsFirst(
            "Models folder",
            _settings.ModelsRoot,
            out _modelsFolderText,
            ("Scan Models Folder", async (_, _) => await RunAsync("Scanning models...", async () => { Require(_catalog); await _catalog!.ScanAsync(_settings.ModelsRoot); await RefreshModelsAsync(); await RefreshOverviewAsync(); })),
            ("Choose", async (_, _) => await ChooseModelsFolderAsync(scanAfter: true)),
            ("Open", (_, _) => OpenFolder(_settings.ModelsRoot)));
        Grid.SetRow(folderStrip, 0);
        root.Children.Add(folderStrip);

        var body = new Grid();
        body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.05, GridUnitType.Star), MinWidth = 330 });
        body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8) });
        body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(.95, GridUnitType.Star), MinWidth = 380 });
        _modelsGrid = GridFor(("Name", nameof(ModelGridRow.Name), 2.5), ("Quant", nameof(ModelGridRow.Quant), .6));
        AddButtonColumn(_modelsGrid, "Open Folder", nameof(ModelGridRow.OpenFolderAction), nameof(ModelGridRow.CanOpenFolder), OpenModelFolderRow_Click, .85, tooltipBinding: nameof(ModelGridRow.OpenFolderToolTip));
        AddButtonColumn(_modelsGrid, "Delete", nameof(ModelGridRow.DeleteAction), nameof(ModelGridRow.CanDelete), DeleteModelRow_Click, .65, tooltipBinding: nameof(ModelGridRow.DeleteToolTip));
        SetModelGridColumnSizing(_modelsGrid);
        _modelsGrid.ItemsSource = _viewModel.Models.Rows;
        _modelsGrid.SelectionChanged += (_, _) =>
        {
            ScheduleSelectedModelLaunchSettingsRefresh();
            UpdateModelActionButtons();
        };
        body.Children.Add(GridFrame(_modelsGrid));
        body.Children.Add(VerticalGridSplitter(1));
        var launchSettings = CreateLaunchSettingsPanel();
        Grid.SetColumn(launchSettings, 2);
        body.Children.Add(launchSettings);
        Grid.SetRow(body, 1);
        root.Children.Add(body);
        root.Children.Add(HorizontalGridSplitter(2));

        var hf = new Grid { Margin = new Thickness(0, 10, 0, 0) };
        hf.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        hf.RowDefinitions.Add(new RowDefinition());
        var hfBar = Bar();
        _hfQueryBox = new WpfTextBox { Width = 280, ToolTip = "Hugging Face search term, repo id, or model file URL" };
        hfBar.Children.Add(_hfQueryBox);
        hfBar.Children.Add(Button("Search Hugging Face", async (_, _) => await SearchHuggingFaceAsync()));
        hfBar.Children.Add(Button("History", async (_, _) => await ShowDownloadHistoryAsync()));
        hf.Children.Add(hfBar);
        _hfGrid = new DataGrid();
        PolishGrid(_hfGrid);
        ConfigureHfSearchGrid();
        var hfGridFrame = GridFrame(_hfGrid);
        Grid.SetRow(hfGridFrame, 1);
        hf.Children.Add(hfGridFrame);
        Grid.SetRow(hf, 3);
        root.Children.Add(hf);
        PageHost.Content = root;
        RunBackground(RefreshModelsAsync, "Models refresh failed");
    }

    private void ShowRuntimes()
    {
        SetPage("Runtimes", "Register Ubuntu/WSL llama.cpp builds and run them without visible command prompts.");
        var root = new Grid { Margin = new Thickness(16) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(.86, GridUnitType.Star), MinHeight = 92 });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(8) });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(.72, GridUnitType.Star), MinHeight = 92 });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(8) });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(.94, GridUnitType.Star), MinHeight = 130 });

        var folderStrip = FolderStripActionsFirst(
            "Runtimes folder",
            _settings.RuntimeRoot,
            out _runtimesFolderText,
            ("Choose Folder", async (_, _) => await ChooseRuntimeFolderAsync(scanAfter: true)));
        Grid.SetRow(folderStrip, 0);
        root.Children.Add(folderStrip);

        _runtimeGrid = GridFor(("Name", nameof(RuntimeCatalogRow.Name), 1.4), ("Backend", nameof(RuntimeCatalogRow.Backend), .55), ("State", nameof(RuntimeCatalogRow.State), .55), ("Location", nameof(RuntimeCatalogRow.Location), 3));
        _runtimeGrid.RowDetailsVisibilityMode = DataGridRowDetailsVisibilityMode.VisibleWhenSelected;
        _runtimeGrid.RowDetailsTemplate = RowDetailsTemplate(nameof(RuntimeCatalogRow.Details));
        _runtimeGrid.PreviewMouseLeftButtonDown += RuntimeGrid_PreviewMouseLeftButtonDown;
        AddButtonColumn(_runtimeGrid, "Build", nameof(RuntimeCatalogRow.BuildAction), nameof(RuntimeCatalogRow.CanBuild), BuildRuntimeRow_Click, .65, tooltipBinding: nameof(RuntimeCatalogRow.BuildToolTip));
        AddButtonColumn(_runtimeGrid, "Action", nameof(RuntimeCatalogRow.DeleteAction), nameof(RuntimeCatalogRow.CanDelete), DeleteRuntimeRow_Click, .65, tooltipBinding: nameof(RuntimeCatalogRow.DeleteToolTip));
        ApplyGridTextMargin(_runtimeGrid, new Thickness(6, 0, 6, 0));
        SetRuntimeGridColumnSizing(_runtimeGrid);
        _runtimeGrid.ItemsSource = _viewModel.Runtimes.Rows;
        var runtimeSection = GridSection(
            "Installed Local Builds",
            _runtimeGrid,
            "Registered llama-server builds found on disk. Build from downloaded source or delete unused builds that are not actively serving a model.");
        Grid.SetRow(runtimeSection, 1);
        root.Children.Add(runtimeSection);
        root.Children.Add(HorizontalGridSplitter(2));

        _runtimeBuildGrid = GridFor(("Repository", nameof(RuntimeBuildPresetRow.Label), 1.4), ("Backend", nameof(RuntimeBuildPresetRow.Backend), .7), ("Local", nameof(RuntimeBuildPresetRow.LocalStatus), .85), ("Latest Local", nameof(RuntimeBuildPresetRow.LatestLocal), 1.2), ("Source", nameof(RuntimeBuildPresetRow.Source), 2.3));
        _runtimeBuildGrid.IsReadOnly = false;
        AddButtonColumn(_runtimeBuildGrid, "Download", nameof(RuntimeBuildPresetRow.DownloadAction), nameof(RuntimeBuildPresetRow.CanDownload), DownloadRuntimePresetRow_Click, .75, tooltipBinding: nameof(RuntimeBuildPresetRow.DownloadToolTip));
        AddButtonColumn(_runtimeBuildGrid, "Update", nameof(RuntimeBuildPresetRow.CheckAction), nameof(RuntimeBuildPresetRow.CanCheck), CheckRuntimePresetUpdateRow_Click, .75, tooltipBinding: nameof(RuntimeBuildPresetRow.CheckToolTip));
        AddButtonColumn(_runtimeBuildGrid, "Delete", nameof(RuntimeBuildPresetRow.DeleteAction), nameof(RuntimeBuildPresetRow.CanDelete), DeleteRuntimePresetRow_Click, .75, tooltipBinding: nameof(RuntimeBuildPresetRow.DeleteToolTip));
        ApplyGridTextMargin(_runtimeBuildGrid, new Thickness(6, 0, 6, 0));
        SetRuntimeBuildGridColumnSizing(_runtimeBuildGrid);
        _runtimeBuildGrid.ItemsSource = _viewModel.RuntimeBuilds.Rows;
        var buildSection = GridSection(
            "Runtime Repositories",
            _runtimeBuildGrid,
            "Known llama.cpp source repositories. Download source, check for upstream changes, or remove all managed builds from a repository.");
        Grid.SetRow(buildSection, 3);
        root.Children.Add(buildSection);
        root.Children.Add(HorizontalGridSplitter(4));

        _runtimeJobsGrid = GridFor(("Status", "C1", .8), ("Kind", "C2", 1), ("Updated", "C4", 1.1), ("Payload", "C5", 3.2));
        AddButtonColumn(_runtimeJobsGrid, "Log", "C6", "B1", OpenRuntimeJobLogRow_Click, .55, tooltipBinding: "T1");
        AddButtonColumn(_runtimeJobsGrid, "Cancel", "C7", "B2", CancelRuntimeJobRow_Click, .7, tooltipBinding: "T2");
        AddButtonColumn(_runtimeJobsGrid, "Retry", "C8", "B3", RetryRuntimeJobRow_Click, .65, tooltipBinding: "T3");
        AddButtonColumn(_runtimeJobsGrid, "Clear", "C9", "B4", ClearRuntimeJobRow_Click, .65, tooltipBinding: "T4");
        ApplyRuntimeJobsRowStyle(_runtimeJobsGrid);
        SetRuntimeJobsGridColumnSizing(_runtimeJobsGrid);
        _runtimeJobsGrid.ItemsSource = _viewModel.Jobs.RuntimeRows;
        var jobsSection = GridSection(
            "Runtime Jobs",
            _runtimeJobsGrid,
            "Recent runtime download and build work. Use Log to inspect compiler, git, or WSL output.");
        Grid.SetRow(jobsSection, 5);
        root.Children.Add(jobsSection);

        PageHost.Content = root;
        RunBackground(DetectAndRefreshRuntimesAsync, "Runtime refresh failed");
    }
}
