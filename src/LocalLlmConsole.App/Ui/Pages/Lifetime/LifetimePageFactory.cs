using System.Collections;
using System.Windows;
using System.Windows.Controls;

namespace LocalLlmConsole;

public sealed record LifetimePageActions(
    RoutedEventHandler ResetLifetimeRowClick);

public sealed record LifetimePageRequest(
    IEnumerable Rows,
    LifetimePageActions Actions);

public sealed record LifetimePageControls(
    DataGrid MetricsGrid);

public sealed record LifetimePageBuildResult(
    DockPanel Content,
    LifetimePageControls Controls);

public static class LifetimePageFactory
{
    public const string TokenUsageTitle = "Lifetime token usage";

    public static readonly (string Header, string Binding, double Weight)[] MetricColumns =
    [
        ("Model", "C1", 2.4),
        ("Prompt", "C2", .8),
        ("Generated", "C3", .8),
        ("Total", "C4", .8),
        ("Updated", "C5", 1.1)
    ];

    public static LifetimePageBuildResult Create(LifetimePageRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Rows);
        ArgumentNullException.ThrowIfNull(request.Actions);

        var root = new DockPanel { Margin = new Thickness(16) };
        var metricsGrid = PageSectionFactory.GridFor(MetricColumns);
        PageSectionFactory.AddButtonColumn(metricsGrid, "Reset", "C6", "B1", request.Actions.ResetLifetimeRowClick, .55, tooltipBinding: "T1");
        metricsGrid.ItemsSource = request.Rows;
        root.Children.Add(PageSectionFactory.GridSection(TokenUsageTitle, metricsGrid));

        return new LifetimePageBuildResult(root, new LifetimePageControls(metricsGrid));
    }
}
