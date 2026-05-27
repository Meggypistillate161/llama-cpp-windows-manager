using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Media;
using System.Text.RegularExpressions;
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
    private static readonly Regex MetricImportantValuePattern = new(
        @"\d[\d,]*(?:\.\d+)?(?:/\d[\d,]*(?:\.\d+)?)?\s*(?:t/s|/s|avg|%|C|GiB|GB|MiB|tokens?)?",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly System.Windows.Media.FontFamily MetricValueFont = new("Cascadia Mono, Consolas, Segoe UI");

    private static StackPanel Stack() => new();
    private static DockPanel Dock() => new() { Margin = new Thickness(16) };
    private static WrapPanel Bar() => new() { Orientation = System.Windows.Controls.Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 10) };
    private static ScrollViewer Scroll(UIElement child, Thickness? padding = null)
    {
        var viewer = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
        var content = new Border { Padding = padding ?? new Thickness(16), Child = child };
        content.SetBinding(FrameworkElement.WidthProperty, new WpfBinding(nameof(ScrollViewer.ViewportWidth)) { Source = viewer });
        viewer.Content = content;
        viewer.Loaded += (_, _) => viewer.Dispatcher.BeginInvoke(new Action(viewer.ScrollToTop), System.Windows.Threading.DispatcherPriority.ContextIdle);
        return viewer;
    }
    private static TextBlock Text(string text, int size = 13, bool bold = false, bool muted = false) => new()
    {
        Text = text,
        FontSize = size,
        FontWeight = bold ? FontWeights.SemiBold : FontWeights.Normal,
        Foreground = muted ? (System.Windows.Media.Brush)WpfApplication.Current.Resources["TextMuted"] : (System.Windows.Media.Brush)WpfApplication.Current.Resources["TextMain"],
        TextWrapping = TextWrapping.Wrap,
        Margin = new Thickness(0, size >= 18 ? 10 : 0, 0, size >= 18 ? 10 : 8)
    };
    private static Grid AddMetric(Grid grid, string label, int row, int column)
        => AddMetric(grid, label, row, column, includeProgress: false, out _);

    private static Grid AddMetric(Grid grid, string label, int row, int column, bool includeProgress, out WpfProgressBar? progress)
    {
        progress = null;
        var card = new Border
        {
            Style = (Style)WpfApplication.Current.Resources["MetricCard"],
            Margin = new Thickness(column == 0 ? 0 : 5, 0, column == 0 ? 5 : 0, 7)
        };
        var stack = new StackPanel();
        var labelText = Text(label, 11, true, true);
        labelText.Margin = new Thickness(0, 0, 0, 2);
        stack.Children.Add(labelText);
        var valueRows = new Grid { MinHeight = 34, Tag = label };
        valueRows.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(74) });
        valueRows.ColumnDefinitions.Add(new ColumnDefinition());
        SetMetricText(valueRows, "...");
        stack.Children.Add(valueRows);
        if (includeProgress)
        {
            progress = new WpfProgressBar
            {
                Height = 4,
                Minimum = 0,
                Maximum = 100,
                Value = 0,
                Visibility = Visibility.Collapsed,
                Margin = new Thickness(0, 5, 0, 0)
            };
            stack.Children.Add(progress);
        }
        card.Child = stack;
        Grid.SetRow(card, row);
        Grid.SetColumn(card, column);
        grid.Children.Add(card);
        return valueRows;
    }
    private static Grid FolderStrip(string label, string path, out TextBlock pathText, RoutedEventHandler choose, RoutedEventHandler open)
    {
        var grid = new Grid { Margin = new Thickness(0, 0, 0, 8) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) });
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.Children.Add(new TextBlock { Text = label, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 0) });
        pathText = new TextBlock
        {
            Text = path,
            Foreground = (System.Windows.Media.Brush)WpfApplication.Current.Resources["TextMuted"],
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 10, 0)
        };
        Grid.SetColumn(pathText, 1);
        grid.Children.Add(pathText);
        var chooseButton = Button("Choose", choose);
        Grid.SetColumn(chooseButton, 2);
        grid.Children.Add(chooseButton);
        var openButton = Button("Open", open);
        Grid.SetColumn(openButton, 3);
        grid.Children.Add(openButton);
        return grid;
    }

    private static Grid FolderStripActionsFirst(string label, string path, out TextBlock pathText, params (string Text, RoutedEventHandler Click)[] actions)
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
            Foreground = (System.Windows.Media.Brush)WpfApplication.Current.Resources["TextMuted"],
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 10, 6)
        };
        Grid.SetColumn(pathText, column);
        grid.Children.Add(pathText);
        return grid;
    }

    private static TextBlock AddFolderLine(System.Windows.Controls.Panel panel, string label, string path, RoutedEventHandler? choose, RoutedEventHandler open)
    {
        var grid = new Grid { Margin = new Thickness(0, 0, 0, 8) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(110) });
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        if (choose is not null) grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.Children.Add(new TextBlock { Text = label, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 0) });
        var pathText = new TextBlock
        {
            Text = path,
            Foreground = (System.Windows.Media.Brush)WpfApplication.Current.Resources["TextMuted"],
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 10, 0)
        };
        Grid.SetColumn(pathText, 1);
        grid.Children.Add(pathText);
        var openColumn = 2;
        if (choose is not null)
        {
            var chooseButton = Button("Choose", choose);
            Grid.SetColumn(chooseButton, 2);
            grid.Children.Add(chooseButton);
            openColumn = 3;
        }
        var openButton = Button("Open", open);
        Grid.SetColumn(openButton, openColumn);
        grid.Children.Add(openButton);
        panel.Children.Add(grid);
        return pathText;
    }
    private static void UpdateFolderText(TextBlock? textBlock, string value)
    {
        if (textBlock is not null) textBlock.Text = value;
    }
    private static WpfButton Button(string text, RoutedEventHandler click)
    {
        var button = new WpfButton { Content = text };
        SetButtonToolTip(button, ButtonToolTip(text));
        button.Click += click;
        return button;
    }

    private static void SetButtonToolTip(WpfButton? button, string toolTip)
    {
        if (button is null || string.IsNullOrWhiteSpace(toolTip)) return;
        button.ToolTip = TooltipText(toolTip);
        ToolTipService.SetShowOnDisabled(button, true);
    }

    private static string? PickFolder(string initial)
    {
        using var dialog = new Forms.FolderBrowserDialog { SelectedPath = Directory.Exists(initial) ? initial : "" };
        return dialog.ShowDialog() == Forms.DialogResult.OK ? dialog.SelectedPath : null;
    }
    private static void OpenFolder(string path)
    {
        Directory.CreateDirectory(path);
        Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
    }

    private static void OpenUrl(string url)
        => Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });

    private static void SetMetricText(Grid? target, string value, bool emphasizeLoadedStatus = false)
    {
        if (target is null) return;

        target.Children.Clear();
        target.RowDefinitions.Clear();
        var lines = (string.IsNullOrWhiteSpace(value) ? "..." : value.TrimEnd())
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Split('\n', StringSplitOptions.None);

        for (var i = 0; i < lines.Length; i++)
        {
            target.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            if (TryAddStatusNameMetricLine(target, lines[i], emphasizeLoadedStatus, i))
                continue;

            if (MetricShouldRenderNeutralStatus(target, lines[i]))
            {
                var neutralBlock = MetricPlainValueBlock(lines[i].Trim(), compact: false);
                Grid.SetRow(neutralBlock, i);
                Grid.SetColumn(neutralBlock, 0);
                Grid.SetColumnSpan(neutralBlock, 2);
                target.Children.Add(neutralBlock);
                continue;
            }

            if (MetricShouldEmphasizeWholeLine(target, lines[i], emphasizeLoadedStatus))
            {
                var statusBlock = MetricValueBlock(lines[i].Trim(), compact: false, emphasizeWholeLine: true);
                Grid.SetRow(statusBlock, i);
                Grid.SetColumn(statusBlock, 0);
                Grid.SetColumnSpan(statusBlock, 2);
                target.Children.Add(statusBlock);
                continue;
            }

            var (label, metricValue) = SplitMetricLine(lines[i]);
            if (!string.IsNullOrWhiteSpace(label))
            {
                var labelBlock = new TextBlock
                {
                    Text = label,
                    FontSize = 11,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = (System.Windows.Media.Brush)WpfApplication.Current.Resources["TextMuted"],
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0, 0, 8, 2)
                };
                Grid.SetRow(labelBlock, i);
                Grid.SetColumn(labelBlock, 0);
                target.Children.Add(labelBlock);

                var valueBlock = MetricValueBlock(metricValue, compact: true);
                Grid.SetRow(valueBlock, i);
                Grid.SetColumn(valueBlock, 1);
                target.Children.Add(valueBlock);
            }
            else
            {
                var valueBlock = MetricValueBlock(metricValue, compact: false);
                Grid.SetRow(valueBlock, i);
                Grid.SetColumn(valueBlock, 0);
                Grid.SetColumnSpan(valueBlock, 2);
                target.Children.Add(valueBlock);
            }
        }
    }

    private static bool MetricShouldRenderNeutralStatus(Grid target, string line)
    {
        if (target.Tag is not string label || !IsStatusNameMetricLabel(label)) return false;
        var text = line.Trim();
        return !string.IsNullOrWhiteSpace(text) && IsNeutralMetricStatus(text);
    }

    private static bool TryAddStatusNameMetricLine(Grid target, string line, bool emphasizeLoadedStatus, int row)
    {
        if (target.Tag is not string label) return false;
        var text = line.Trim();
        if (string.IsNullOrWhiteSpace(text)) return false;
        if (!IsStatusNameMetricLabel(label)) return false;

        if (IsNeutralMetricStatus(text))
        {
            AddSpanningMetricBlock(target, MetricPlainValueBlock(text, compact: false), row);
            return true;
        }

        if (string.Equals(label, "Model status", StringComparison.Ordinal)
            && TrySplitModelStatusName(text, out var statusPrefix, out var modelName))
        {
            AddSpanningMetricBlock(target, MetricStatusNameBlock(statusPrefix, modelName), row);
            return true;
        }

        if (string.Equals(label, "Model status", StringComparison.Ordinal))
        {
            AddSpanningMetricBlock(target, MetricPlainValueBlock(text, compact: false), row);
            return true;
        }

        if (string.Equals(label, "Runtime build", StringComparison.Ordinal))
        {
            var runtimeBlock = emphasizeLoadedStatus && !LooksLikeEndpoint(text)
                ? MetricStatusNameBlock("", text)
                : MetricPlainValueBlock(text, compact: false);
            AddSpanningMetricBlock(target, runtimeBlock, row);
            return true;
        }

        return false;
    }

    private static void AddSpanningMetricBlock(Grid target, UIElement block, int row)
    {
        Grid.SetRow(block, row);
        Grid.SetColumn(block, 0);
        Grid.SetColumnSpan(block, 2);
        target.Children.Add(block);
    }

    private static bool LooksLikeEndpoint(string text)
        => text.Contains("://", StringComparison.Ordinal)
           || text.StartsWith("localhost:", StringComparison.OrdinalIgnoreCase)
           || text.StartsWith("127.0.0.1:", StringComparison.OrdinalIgnoreCase)
           || text.StartsWith("0.0.0.0:", StringComparison.OrdinalIgnoreCase);

    private static bool TrySplitModelStatusName(string text, out string statusPrefix, out string modelName)
    {
        statusPrefix = "";
        modelName = "";

        foreach (var prefix in new[] { "Loaded:", "Loading", "Warm:", "Stopped:" })
        {
            if (!text.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) continue;
            var separator = prefix.EndsWith(":", StringComparison.Ordinal) ? prefix.Length : prefix.Length;
            var remainder = text[separator..].TrimStart();
            if (string.IsNullOrWhiteSpace(remainder)) return false;

            statusPrefix = prefix.EndsWith(":", StringComparison.Ordinal) ? $"{text[..separator]} " : $"{text[..separator]} ";
            modelName = remainder;
            return true;
        }

        return false;
    }

    private static bool IsStatusNameMetricLabel(string label)
        => string.Equals(label, "Model status", StringComparison.Ordinal)
           || string.Equals(label, "Runtime build", StringComparison.Ordinal);

    private static bool MetricShouldEmphasizeWholeLine(Grid target, string line, bool emphasizeLoadedStatus)
    {
        if (target.Tag is not string label) return false;
        var text = line.Trim();
        if (string.IsNullOrWhiteSpace(text) || IsNeutralMetricStatus(text)) return false;

        if (string.Equals(label, "Model status", StringComparison.Ordinal))
            return false;

        if (string.Equals(label, "Runtime build", StringComparison.Ordinal))
            return emphasizeLoadedStatus;

        return false;
    }

    private static bool IsNeutralMetricStatus(string text)
    {
        var normalized = text.Trim();
        return string.Equals(normalized, "None", StringComparison.OrdinalIgnoreCase)
               || string.Equals(normalized, "Stopped", StringComparison.OrdinalIgnoreCase)
               || string.Equals(normalized, "Loading", StringComparison.OrdinalIgnoreCase)
               || string.Equals(normalized, "Loaded", StringComparison.OrdinalIgnoreCase)
               || string.Equals(normalized, "Warm", StringComparison.OrdinalIgnoreCase)
               || string.Equals(normalized, "Unavailable", StringComparison.OrdinalIgnoreCase)
               || string.Equals(normalized, "Unknown runtime", StringComparison.OrdinalIgnoreCase)
               || string.Equals(normalized, "Unknown model", StringComparison.OrdinalIgnoreCase)
               || string.Equals(normalized, "No runtime", StringComparison.OrdinalIgnoreCase)
               || string.Equals(normalized, "No loaded runtime", StringComparison.OrdinalIgnoreCase)
               || normalized.StartsWith("Failed", StringComparison.OrdinalIgnoreCase);
    }

    private static (string Label, string Value) SplitMetricLine(string line)
    {
        var text = line.Trim();
        if (string.IsNullOrWhiteSpace(text)) return ("", "");

        var colon = text.IndexOf(':');
        if (colon > 0 && colon < 16)
            return (text[..colon].Trim(), text[(colon + 1)..].Trim());

        foreach (var label in new[] { "KV cache", "Context", "Prompt", "Gen" })
        {
            if (text.StartsWith(label + " ", StringComparison.Ordinal))
                return (label, text[label.Length..].Trim());
        }

        return ("", text);
    }

    private static TextBlock MetricValueBlock(string text, bool compact, bool emphasizeWholeLine = false)
    {
        var block = new TextBlock
        {
            FontSize = compact ? 13 : 14,
            FontWeight = FontWeights.SemiBold,
            Foreground = (System.Windows.Media.Brush)WpfApplication.Current.Resources["TextMain"],
            TextWrapping = TextWrapping.Wrap,
            LineHeight = compact ? 16 : 17,
            Margin = new Thickness(0, 0, 0, 2)
        };
        AddMetricValueInlines(block, text, emphasizeWholeLine);
        return block;
    }
}
