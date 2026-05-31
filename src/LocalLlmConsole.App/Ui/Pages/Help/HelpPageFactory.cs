using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using WpfApplication = System.Windows.Application;
using WpfBinding = System.Windows.Data.Binding;
using WpfBrush = System.Windows.Media.Brush;
using WpfButton = System.Windows.Controls.Button;

namespace LocalLlmConsole;

public sealed record HelpPageActions(
    Action<string> ShowSection,
    Action<string> Navigate);

public sealed record HelpPageRequest(
    HelpSectionDefinition ActiveSection,
    IReadOnlyList<HelpSectionDefinition> Sections,
    HelpPageActions Actions);

public sealed record HelpPageBuildResult(
    DockPanel Content);

public static class HelpPageFactory
{
    public static HelpPageBuildResult Create(HelpPageRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.ActiveSection);
        ArgumentNullException.ThrowIfNull(request.Sections);
        ArgumentNullException.ThrowIfNull(request.Actions);
        ArgumentNullException.ThrowIfNull(request.Actions.ShowSection);
        ArgumentNullException.ThrowIfNull(request.Actions.Navigate);

        var root = new DockPanel { Margin = new Thickness(16) };
        var tabs = HelpSectionTabs(request);
        DockPanel.SetDock(tabs, Dock.Top);
        root.Children.Add(tabs);
        root.Children.Add(Scroll(HelpSectionBody(request), new Thickness(0, 0, 10, 0)));

        return new HelpPageBuildResult(root);
    }

    private static WrapPanel HelpSectionTabs(HelpPageRequest request)
    {
        var tabs = Bar();
        tabs.Margin = new Thickness(0, 0, 0, 10);
        foreach (var section in request.Sections)
        {
            var button = Button(section.Label, () => request.Actions.ShowSection(section.Key));
            if (string.Equals(section.Key, request.ActiveSection.Key, StringComparison.Ordinal))
                button.Tag = "Active";
            SetButtonToolTip(button, $"Show {section.Label} help.");
            tabs.Children.Add(button);
        }
        return tabs;
    }

    private static StackPanel HelpSectionBody(HelpPageRequest request)
    {
        var panel = new StackPanel { Margin = new Thickness(0, 0, 0, 0) };
        panel.Children.Add(Text(request.ActiveSection.Title, 22, true));
        panel.Children.Add(Text(request.ActiveSection.Summary, 13, muted: true));

        HelpContentFactory.AddSection(panel, request.ActiveSection.Key, request.Actions.Navigate);

        return panel;
    }

    private static ScrollViewer Scroll(UIElement child, Thickness? padding = null)
    {
        var viewer = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
        };
        var content = new Border { Padding = padding ?? new Thickness(16), Child = child };
        content.SetBinding(FrameworkElement.WidthProperty, new WpfBinding(nameof(ScrollViewer.ViewportWidth)) { Source = viewer });
        viewer.Content = content;
        viewer.Loaded += (_, _) => viewer.Dispatcher.BeginInvoke(
            new Action(viewer.ScrollToTop),
            System.Windows.Threading.DispatcherPriority.ContextIdle);
        return viewer;
    }

    private static WrapPanel Bar() => new()
    {
        Orientation = System.Windows.Controls.Orientation.Horizontal,
        Margin = new Thickness(0, 0, 0, 10)
    };

    private static TextBlock Text(string text, int size = 13, bool bold = false, bool muted = false) => new()
    {
        Text = text,
        FontSize = size,
        FontWeight = bold ? FontWeights.SemiBold : FontWeights.Normal,
        Foreground = muted ? (WpfBrush)WpfApplication.Current.Resources["TextMuted"] : (WpfBrush)WpfApplication.Current.Resources["TextMain"],
        TextWrapping = TextWrapping.Wrap,
        Margin = new Thickness(0, size >= 18 ? 10 : 0, 0, size >= 18 ? 10 : 8)
    };

    private static WpfButton Button(string text, Action click)
    {
        var button = new WpfButton { Content = text };
        button.Click += (_, _) => click();
        return button;
    }

    private static void SetButtonToolTip(WpfButton button, string text)
    {
        button.ToolTip = text;
        ToolTipService.SetShowOnDisabled(button, true);
    }
}
