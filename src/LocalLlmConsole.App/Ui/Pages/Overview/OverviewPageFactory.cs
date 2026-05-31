using LocalLlmConsole.ViewModels;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using WpfApplication = System.Windows.Application;
using WpfBrush = System.Windows.Media.Brush;
using WpfButton = System.Windows.Controls.Button;
using WpfComboBox = System.Windows.Controls.ComboBox;
using WpfTextBox = System.Windows.Controls.TextBox;

namespace LocalLlmConsole;

public sealed record OverviewPageActions(
    Func<Task> SelectModelSessionAsync,
    Func<Task> LoadSelectedModelAsync,
    Func<Task> UnloadSelectedModelAsync,
    Func<Task> SelectLoadedSessionRowAsync);

public sealed record OverviewPageRequest(
    MainWindowViewModel ViewModel,
    OverviewPageActions Actions,
    Action<DataGrid> ConfigureRuntimeMetricsGrid);

public sealed record OverviewPageControls(
    Grid Root,
    WpfComboBox ModelCombo,
    WpfButton LoadButton,
    WpfButton UnloadButton,
    DataGrid LoadedSessionsGrid,
    Grid RuntimeDashboardModel,
    Grid RuntimeDashboardRuntime,
    Grid RuntimeDashboardRequests,
    Grid RuntimeDashboardGenerationRate,
    TextBlock RuntimeDashboardGenerationRateLastKnown,
    Grid RuntimeDashboardTotalTokens,
    Grid RuntimeDashboardGpu,
    WpfTextBox RuntimeLogBox,
    DataGrid RuntimeMetricsGrid);

public static class OverviewPageFactory
{
    public const string LoadedSessionsTitle = "Loaded Model Sessions";
    public const string LiveRuntimeLogTitle = "Live Runtime Log";
    public const string RuntimeMetricsTitle = "All llama.cpp Metrics";

    public static readonly (string Header, string Binding, double Weight)[] LoadedSessionColumns =
    [
        ("Model", "C1", 1.45),
        ("Size", "C2", .62),
        ("State", "C3", .62),
        ("API endpoints", "C4", 1.9),
        ("Runtime", "C5", 1.25),
        ("Backend", "C6", .75)
    ];

    public static readonly (string Header, string Binding, double Weight)[] RuntimeMetricColumns =
    [
        ("Metric", "C1", 1.5),
        ("Labels", "C2", 2.2),
        ("Value", "C3", .9),
        ("Type", "C4", .7),
        ("Help", "C5", 3)
    ];

    public static OverviewPageControls Create(OverviewPageRequest request)
    {
        var root = new Grid { Margin = new Thickness(16) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1.08, GridUnitType.Star), MinHeight = 150 });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(8) });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(.92, GridUnitType.Star), MinHeight = 130 });

        var modelBar = ModelBar(request, out var modelCombo, out var loadButton, out var unloadButton);
        Grid.SetRow(modelBar, 0);
        root.Children.Add(modelBar);

        var dashboardSection = Stack();
        var loadedSessionsGrid = PageSectionFactory.GridFor(LoadedSessionColumns);
        loadedSessionsGrid.ItemsSource = request.ViewModel.Overview.SessionRows;
        loadedSessionsGrid.SelectionChanged += async (_, _) => await request.Actions.SelectLoadedSessionRowAsync();
        dashboardSection.Children.Add(PageSectionFactory.GridSection(LoadedSessionsTitle, loadedSessionsGrid));
        dashboardSection.Children.Add(Text("Model Status", 18, true));

        var runtimeDashboard = RuntimeDashboard(
            out var runtimeDashboardModel,
            out var runtimeDashboardRuntime,
            out var runtimeDashboardRequests,
            out var runtimeDashboardGenerationRate,
            out var runtimeDashboardGenerationRateLastKnown,
            out var runtimeDashboardTotalTokens,
            out var runtimeDashboardGpu);
        dashboardSection.Children.Add(runtimeDashboard);
        Grid.SetRow(dashboardSection, 1);
        root.Children.Add(dashboardSection);

        var runtimeLogBox = RuntimeLogBox();
        var runtimeLogSection = PageSectionFactory.FramedSection(LiveRuntimeLogTitle, runtimeLogBox);
        Grid.SetRow(runtimeLogSection, 2);
        root.Children.Add(runtimeLogSection);
        root.Children.Add(PageSectionFactory.HorizontalGridSplitter(3));

        var runtimeMetricsGrid = PageSectionFactory.GridFor(RuntimeMetricColumns);
        runtimeMetricsGrid.ItemsSource = request.ViewModel.RuntimeMetrics.Rows;
        runtimeMetricsGrid.VerticalAlignment = VerticalAlignment.Stretch;
        request.ConfigureRuntimeMetricsGrid(runtimeMetricsGrid);
        var metricsSection = PageSectionFactory.GridSection(RuntimeMetricsTitle, runtimeMetricsGrid);
        Grid.SetRow(metricsSection, 4);
        root.Children.Add(metricsSection);

        return new OverviewPageControls(
            root,
            modelCombo,
            loadButton,
            unloadButton,
            loadedSessionsGrid,
            runtimeDashboardModel,
            runtimeDashboardRuntime,
            runtimeDashboardRequests,
            runtimeDashboardGenerationRate,
            runtimeDashboardGenerationRateLastKnown,
            runtimeDashboardTotalTokens,
            runtimeDashboardGpu,
            runtimeLogBox,
            runtimeMetricsGrid);
    }

    private static Grid ModelBar(OverviewPageRequest request, out WpfComboBox modelCombo, out WpfButton loadButton, out WpfButton unloadButton)
    {
        var modelBar = new Grid { Margin = new Thickness(0, 0, 0, 10) };
        modelBar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) });
        modelBar.ColumnDefinitions.Add(new ColumnDefinition());
        modelBar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        modelBar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        modelBar.Children.Add(new TextBlock
        {
            Text = "Model",
            FontWeight = FontWeights.SemiBold,
            Foreground = (WpfBrush)WpfApplication.Current.Resources["TextSoft"],
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 10, 6)
        });
        modelCombo = new WpfComboBox
        {
            ItemsSource = request.ViewModel.Overview.ModelChoices,
            DisplayMemberPath = nameof(ModelRecord.Name),
            SelectedValuePath = nameof(ModelRecord.Id),
            MinHeight = 30,
            Margin = new Thickness(0, 0, 8, 6),
            HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch,
            ToolTip = "Choose a local model to load with its saved launch profile."
        };
        modelCombo.SelectionChanged += async (_, _) => await request.Actions.SelectModelSessionAsync();
        Grid.SetColumn(modelCombo, 1);
        modelBar.Children.Add(modelCombo);

        loadButton = Button("Load", request.Actions.LoadSelectedModelAsync);
        Grid.SetColumn(loadButton, 2);
        modelBar.Children.Add(loadButton);

        unloadButton = Button("Unload", request.Actions.UnloadSelectedModelAsync);
        Grid.SetColumn(unloadButton, 3);
        modelBar.Children.Add(unloadButton);
        return modelBar;
    }

    private static Grid RuntimeDashboard(
        out Grid model,
        out Grid runtime,
        out Grid requests,
        out Grid generationRate,
        out TextBlock generationRateLastKnown,
        out Grid totalTokens,
        out Grid gpu)
    {
        var runtimeDashboard = new Grid { Margin = new Thickness(0, 2, 0, 8) };
        runtimeDashboard.ColumnDefinitions.Add(new ColumnDefinition());
        runtimeDashboard.ColumnDefinitions.Add(new ColumnDefinition());
        runtimeDashboard.ColumnDefinitions.Add(new ColumnDefinition());
        for (var row = 0; row < 2; row++)
            runtimeDashboard.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        model = MetricCardFactory.AddMetric(runtimeDashboard, "Model status", 0, 0);
        runtime = MetricCardFactory.AddMetric(runtimeDashboard, "Runtime build", 0, 1);
        requests = MetricCardFactory.AddMetric(runtimeDashboard, "Settings", 0, 2);
        generationRate = MetricCardFactory.AddMetric(runtimeDashboard, "Tokens (Live)", 1, 0, out generationRateLastKnown);
        totalTokens = MetricCardFactory.AddMetric(runtimeDashboard, "Tokens (Total)", 1, 1);
        gpu = MetricCardFactory.AddMetric(runtimeDashboard, "GPU", 1, 2);
        return runtimeDashboard;
    }

    private static WpfTextBox RuntimeLogBox()
        => new()
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

    private static WpfButton Button(string text, Func<Task> click)
    {
        var button = new WpfButton { Content = text };
        button.ToolTip = text switch
        {
            "Load" => "Load the selected model with its saved launch settings.",
            "Unload" => "Stop the currently loading or loaded model and free runtime resources.",
            _ => $"Run {text}."
        };
        ToolTipService.SetShowOnDisabled(button, true);
        button.Click += async (_, _) => await click();
        return button;
    }

    private static StackPanel Stack() => new();

    private static TextBlock Text(string text, int size = 13, bool bold = false, bool muted = false) => new()
    {
        Text = text,
        FontSize = size,
        FontWeight = bold ? FontWeights.SemiBold : FontWeights.Normal,
        Foreground = muted ? (WpfBrush)WpfApplication.Current.Resources["TextMuted"] : (WpfBrush)WpfApplication.Current.Resources["TextMain"],
        TextWrapping = TextWrapping.Wrap,
        Margin = new Thickness(0, size >= 18 ? 10 : 0, 0, size >= 18 ? 10 : 8)
    };
}
