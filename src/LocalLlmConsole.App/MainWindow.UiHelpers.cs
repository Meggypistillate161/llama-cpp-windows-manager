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
    private void SetActiveNavigation(string title)
    {
        foreach (var button in new[] { OverviewNavButton, ModelsNavButton, RuntimesNavButton, WslLinuxNavButton, SettingsNavButton, OpenCodeNavButton, LifetimeNavButton, LogsNavButton, UpdatesNavButton, HelpNavButton })
            button.Tag = null;

        var active = title switch
        {
            "Overview" => OverviewNavButton,
            "Models" => ModelsNavButton,
            "Runtimes" => RuntimesNavButton,
            "WSL Linux" => WslLinuxNavButton,
            "Settings" => SettingsNavButton,
            "OpenCode" => OpenCodeNavButton,
            "Lifetime" => LifetimeNavButton,
            "Logs" => LogsNavButton,
            "Updates" => UpdatesNavButton,
            "Help" => HelpNavButton,
            _ => null
        };
        if (active is not null) active.Tag = "Active";
    }
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

    private void ApplyStaticButtonToolTips()
    {
        SetButtonToolTip(MinimizeButton, "Minimize the app window.");
        SetButtonToolTip(MaximizeButton, "Maximize or restore the app window.");
        SetButtonToolTip(CloseButton, "Close the app. Running models and downloads will be handled safely.");
        SetButtonToolTip(OverviewNavButton, "Open the model loading dashboard.");
        SetButtonToolTip(ModelsNavButton, "Open local models, Hugging Face search, and launch settings.");
        SetButtonToolTip(RuntimesNavButton, "Open llama.cpp source downloads, builds, and runtime jobs.");
        SetButtonToolTip(WslLinuxNavButton, "Open WSL, Ubuntu, and toolkit setup actions.");
        SetButtonToolTip(SettingsNavButton, "Open app preferences.");
        SetButtonToolTip(OpenCodeNavButton, "Open OpenCode model and agent configuration.");
        SetButtonToolTip(LifetimeNavButton, "Open persisted lifetime token counters.");
        SetButtonToolTip(LogsNavButton, "Open app, runtime, and job logs.");
        SetButtonToolTip(UpdatesNavButton, "Check for app updates from GitHub releases.");
        SetButtonToolTip(HelpNavButton, "Open first-run setup steps.");
    }

    private static string ButtonToolTip(string text)
    {
        var label = (text ?? "").Trim();
        return label switch
        {
            "Load" => "Load the selected model with its saved launch settings.",
            "Unload" => "Stop the currently loading or loaded model and free runtime resources.",
            "Save For Model" => "Save these launch settings for the selected model.",
            "Save As Default" => "Save these launch settings as the default for new models.",
            "Reset Defaults" => "Restore launch settings to the app defaults.",
            "Refresh Logs" => "Reload the log file list.",
            "Open Selected" => "Open the selected log file.",
            "Open Logs Folder" => "Open the app logs folder in File Explorer.",
            "Delete Selected" => "Delete the selected log files when they are safe to remove.",
            "Delete All Logs" => "Delete all removable log files.",
            "Detect Files" => "Find OpenCode config and agents files automatically.",
            "Choose Config" => "Choose the OpenCode provider config file.",
            "Choose Agents Folder" => "Choose the OpenCode agents folder.",
            "Update Config" => "Save changes to the selected OpenCode model config.",
            "Delete Config" => "Delete the selected OpenCode model config.",
            "Add" => "Add the selected item.",
            "Update" => "Update the selected item.",
            "Add As New" => "Add this model as a new OpenCode config entry.",
            "Save Agent" => "Save changes to the selected OpenCode agent.",
            "Delete Agent" => "Delete the selected OpenCode agent.",
            "Create Agent" => "Create a new OpenCode agent from the current draft.",
            "Search Hugging Face" => "Search Hugging Face for GGUF model files.",
            "History" => "Show model download history and controls.",
            "Save Settings" => "Save the current app preferences.",
            "Open GitHub" => "Open the app's GitHub repository in your browser.",
            "Refresh" => "Refresh the current page.",
            "Choose" => "Choose a folder.",
            "Open" => "Open this folder.",
            "Scan Models Folder" => "Scan the models folder for local GGUF files.",
            "Install WSL" => "Install Windows Subsystem for Linux.",
            "Update WSL" => "Check for WSL updates.",
            "Delete WSL" => "Remove the WSL feature from this machine.",
            "Install Ubuntu" => "Install the recommended Ubuntu distro for WSL builds.",
            "Update Ubuntu" => "Update packages in the selected Ubuntu distro.",
            "Delete Ubuntu" => "Remove the selected Ubuntu distro.",
            "Install CPU Tools" => "Install CPU build tools in the selected Ubuntu distro.",
            "Install CUDA" => "Install NVIDIA CUDA Toolkit packages in Ubuntu.",
            "Install Vulkan" => "Install Vulkan build and runtime tools in Ubuntu.",
            "Open WSL Linux" => "Open WSL Linux setup actions.",
            "Open Runtimes" => "Open runtime source download and build actions.",
            "Open Models" => "Open model search, download, and launch settings.",
            "Open Overview" => "Open the model loading dashboard.",
            "Open OpenCode" => "Open OpenCode setup actions.",
            _ when label.StartsWith("Install ", StringComparison.OrdinalIgnoreCase) => $"Run {label}.",
            _ when label.StartsWith("Delete ", StringComparison.OrdinalIgnoreCase) => $"Run {label}.",
            _ when label.StartsWith("Check", StringComparison.OrdinalIgnoreCase) => label,
            _ => string.IsNullOrWhiteSpace(label) ? "" : $"Run {label}."
        };
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
            if (MetricShouldRenderNeutralStatus(target, lines[i]))
            {
                var neutralBlock = MetricPlainValueBlock(lines[i].Trim(), compact: false);
                Grid.SetRow(neutralBlock, i);
                Grid.SetColumn(neutralBlock, 0);
                Grid.SetColumnSpan(neutralBlock, 2);
                target.Children.Add(neutralBlock);
                continue;
            }

            if (TryAddStatusNameMetricLine(target, lines[i], emphasizeLoadedStatus, i))
                continue;

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

        if (string.Equals(label, "Model status", StringComparison.Ordinal)
            && TrySplitModelStatusName(text, out var statusPrefix, out var modelName))
        {
            var statusBlock = MetricStatusNameBlock(statusPrefix, modelName);
            Grid.SetRow(statusBlock, row);
            Grid.SetColumn(statusBlock, 0);
            Grid.SetColumnSpan(statusBlock, 2);
            target.Children.Add(statusBlock);
            return true;
        }

        if (string.Equals(label, "Runtime build", StringComparison.Ordinal) && emphasizeLoadedStatus)
        {
            var statusBlock = MetricStatusNameBlock("", text);
            Grid.SetRow(statusBlock, row);
            Grid.SetColumn(statusBlock, 0);
            Grid.SetColumnSpan(statusBlock, 2);
            target.Children.Add(statusBlock);
            return true;
        }

        return false;
    }

    private static bool TrySplitModelStatusName(string text, out string statusPrefix, out string modelName)
    {
        statusPrefix = "";
        modelName = "";

        foreach (var prefix in new[] { "Loaded:", "Loading" })
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
               || string.Equals(normalized, "Unavailable", StringComparison.OrdinalIgnoreCase)
               || string.Equals(normalized, "Unknown runtime", StringComparison.OrdinalIgnoreCase)
               || string.Equals(normalized, "Unknown model", StringComparison.OrdinalIgnoreCase)
               || string.Equals(normalized, "No runtime", StringComparison.OrdinalIgnoreCase)
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
