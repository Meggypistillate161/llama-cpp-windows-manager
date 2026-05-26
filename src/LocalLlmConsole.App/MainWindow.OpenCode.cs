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
    private void ShowOpenCode()
    {
        SetPage("OpenCode", "Model and agent config.");
        Require(_openCode);
        _openCodeFiles = _openCode!.LoadOrDetectFileSet();

        var root = new Grid { Margin = new Thickness(16) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star), MinHeight = 210 });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(8) });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star), MinHeight = 210 });

        var filesPanel = OpenCodeFilesPanel();
        Grid.SetRow(filesPanel, 0);
        root.Children.Add(filesPanel);

        var modelSection = OpenCodeModelSection();
        Grid.SetRow(modelSection, 1);
        root.Children.Add(modelSection);
        root.Children.Add(HorizontalGridSplitter(2));

        var agentSection = OpenCodeAgentSection();
        Grid.SetRow(agentSection, 3);
        root.Children.Add(agentSection);

        PageHost.Content = root;
        RunBackground(() => RunAsync("Loading OpenCode config...", () => RefreshOpenCodeAsync()), "OpenCode config load failed");
    }

    private Grid OpenCodeFilesPanel()
    {
        var panel = new Grid { Margin = new Thickness(0, 0, 0, 8) };
        panel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        panel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        panel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var toolbar = Bar();
        toolbar.Children.Add(Button("Detect Files", async (_, _) => await DetectOpenCodeFilesAsync()));
        toolbar.Children.Add(Button("Choose Config", async (_, _) => await ChooseOpenCodeConfigFileAsync()));
        toolbar.Children.Add(Button("Choose Agents Folder", async (_, _) => await ChooseOpenCodeAgentsFolderAsync()));
        toolbar.Children.Add(Button("Refresh", async (_, _) => await RunAsync("Refreshing OpenCode config...", async () => await RefreshOpenCodeAsync())));
        panel.Children.Add(toolbar);

        var configRow = OpenCodePathRow("Config", _openCodeFiles.ConfigPath, out _openCodeConfigPathText);
        Grid.SetRow(configRow, 1);
        panel.Children.Add(configRow);

        var agentsRow = OpenCodePathRow("Agents", _openCodeFiles.AgentsDirectory, out _openCodeAgentsPathText);
        Grid.SetRow(agentsRow, 2);
        panel.Children.Add(agentsRow);
        return panel;
    }

    private Grid OpenCodeModelSection()
    {
        var left = new StackPanel { Margin = new Thickness(0, 6, 10, 6) };
        left.Children.Add(Text("Model", 12, true, true));
        _openCodeModelCombo = new WpfComboBox
        {
            ItemsSource = _viewModel.OpenCode.ModelChoices,
            DisplayMemberPath = nameof(OpenCodeModelEntry.Label),
            MinHeight = 30,
            HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch,
            ToolTip = TooltipText("Models configured in the selected OpenCode provider config.")
        };
        _openCodeModelCombo.SelectionChanged += async (_, _) => await LoadSelectedOpenCodeModelAsync();
        left.Children.Add(_openCodeModelCombo);

        var modelActions = Bar();
        _openCodeSaveModelButton = Button("Update Config", async (_, _) => await SaveOpenCodeModelSnippetAsync());
        _openCodeDeleteModelButton = Button("Delete Config", async (_, _) => await DeleteOpenCodeModelAsync());
        modelActions.Children.Add(_openCodeSaveModelButton);
        modelActions.Children.Add(_openCodeDeleteModelButton);
        left.Children.Add(modelActions);

        var addPanel = new StackPanel { Margin = new Thickness(0, 8, 0, 0), Visibility = Visibility.Collapsed };
        addPanel.Children.Add(Text("Local model", 12, true, true));
        _openCodeLocalModelCombo = new WpfComboBox
        {
            ItemsSource = _viewModel.OpenCode.LocalModelChoices,
            DisplayMemberPath = nameof(ModelRecord.Name),
            SelectedValuePath = nameof(ModelRecord.Id),
            MinHeight = 30,
            HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch,
            ToolTip = TooltipText("Choose one of the app's registered local GGUF models.")
        };
        _openCodeLocalModelCombo.SelectionChanged += async (_, _) => await LoadOpenCodeLocalModelDraftAsync();
        addPanel.Children.Add(_openCodeLocalModelCombo);
        _openCodeAddModelStatusText = Text("", 12, false, true);
        _openCodeAddModelStatusText.Margin = new Thickness(0, 4, 0, 8);
        addPanel.Children.Add(_openCodeAddModelStatusText);

        var addActions = Bar();
        _openCodeAddLocalModelButton = Button("Add", async (_, _) => await SaveOpenCodeLocalModelSnippetAsync(addAsNew: false));
        _openCodeUpdateLocalModelButton = Button("Update", async (_, _) => await SaveOpenCodeLocalModelSnippetAsync(addAsNew: false));
        _openCodeAddAsNewLocalModelButton = Button("Add As New", async (_, _) => await SaveOpenCodeLocalModelSnippetAsync(addAsNew: true));
        addActions.Children.Add(_openCodeAddLocalModelButton);
        addActions.Children.Add(_openCodeUpdateLocalModelButton);
        addActions.Children.Add(_openCodeAddAsNewLocalModelButton);
        addPanel.Children.Add(addActions);
        _openCodeAddModelPanel = addPanel;
        left.Children.Add(addPanel);

        _openCodeModelSnippetBox = OpenCodeEditorBox();
        _openCodeModelSnippetBox.TextChanged += (_, _) =>
        {
            if (!_updatingOpenCodeModelEditor)
                UpdateOpenCodeModelEditorState();
        };
        return OpenCodeSplitSection("OpenCode models", left, _openCodeModelSnippetBox);
    }

    private Grid OpenCodeAgentSection()
    {
        var left = new StackPanel { Margin = new Thickness(0, 6, 10, 6) };
        left.Children.Add(Text("Agent", 12, true, true));
        _openCodeAgentCombo = new WpfComboBox
        {
            ItemsSource = _viewModel.OpenCode.AgentChoices,
            DisplayMemberPath = nameof(OpenCodeAgentEntry.Label),
            MinHeight = 30,
            HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch,
            ToolTip = TooltipText("Agents from the selected OpenCode config and agents folder.")
        };
        _openCodeAgentCombo.SelectionChanged += async (_, _) => await LoadSelectedOpenCodeAgentAsync();
        left.Children.Add(_openCodeAgentCombo);

        var actions = Bar();
        _openCodeSaveAgentButton = Button("Save Agent", async (_, _) => await SaveOpenCodeAgentSnippetAsync());
        _openCodeDeleteAgentButton = Button("Delete Agent", async (_, _) => await DeleteOpenCodeAgentAsync());
        actions.Children.Add(_openCodeSaveAgentButton);
        actions.Children.Add(_openCodeDeleteAgentButton);
        left.Children.Add(actions);

        var addPanel = new StackPanel { Margin = new Thickness(0, 8, 0, 0), Visibility = Visibility.Collapsed };
        addPanel.Children.Add(Text("New agent", 12, true, true));
        _openCodeNewAgentNameBox = new WpfTextBox
        {
            ToolTip = TooltipText("Lowercase id is generated from this name."),
            HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch
        };
        addPanel.Children.Add(_openCodeNewAgentNameBox);
        _openCodeAgentKindCombo = LaunchCombo("config json", "markdown file");
        addPanel.Children.Add(_openCodeAgentKindCombo);
        _openCodeCreateAgentButton = Button("Create Agent", async (_, _) => await CreateOpenCodeAgentAsync());
        addPanel.Children.Add(_openCodeCreateAgentButton);
        _openCodeAddAgentPanel = addPanel;
        left.Children.Add(addPanel);

        _openCodeAgentSnippetBox = OpenCodeEditorBox();
        return OpenCodeSplitSection("OpenCode agents", left, _openCodeAgentSnippetBox);
    }

    private static WpfTextBox OpenCodeEditorBox() => new()
    {
        AcceptsReturn = true,
        AcceptsTab = true,
        FontFamily = new System.Windows.Media.FontFamily("Consolas"),
        TextWrapping = TextWrapping.NoWrap,
        VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
        HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch,
        VerticalAlignment = VerticalAlignment.Stretch,
        MinHeight = 180
    };

    private static Grid OpenCodeSplitSection(string title, UIElement left, UIElement right)
    {
        var section = new Grid { Margin = new Thickness(0, 0, 0, 4) };
        section.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        section.RowDefinitions.Add(new RowDefinition());
        section.Children.Add(new TextBlock
        {
            Text = title,
            FontWeight = FontWeights.SemiBold,
            Foreground = (System.Windows.Media.Brush)WpfApplication.Current.Resources["TextSoft"],
            Margin = new Thickness(2, 0, 0, 0)
        });

        var body = new Grid();
        body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(300), MinWidth = 230 });
        body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8) });
        body.ColumnDefinitions.Add(new ColumnDefinition());
        body.Children.Add(OpenCodePaneFrame(left));
        var splitter = VerticalGridSplitter(1);
        body.Children.Add(splitter);
        Grid.SetColumn(right, 2);
        body.Children.Add(right);
        Grid.SetRow(body, 1);
        section.Children.Add(body);
        return section;
    }

    private static Border OpenCodePaneFrame(UIElement child) => new()
    {
        Background = (System.Windows.Media.Brush)WpfApplication.Current.Resources["InputBack"],
        BorderBrush = (System.Windows.Media.Brush)WpfApplication.Current.Resources["PanelBorder"],
        BorderThickness = new Thickness(1),
        CornerRadius = new CornerRadius(6),
        Margin = new Thickness(0, 6, 0, 6),
        Padding = new Thickness(10, 6, 8, 8),
        Child = child
    };

    private static Grid OpenCodePathRow(string label, string path, out TextBlock pathText)
    {
        var grid = new Grid { Margin = new Thickness(0, 0, 0, 3) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(86) });
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.Children.Add(new TextBlock
        {
            Text = label,
            FontWeight = FontWeights.SemiBold,
            Foreground = (System.Windows.Media.Brush)WpfApplication.Current.Resources["TextSoft"],
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 10, 0)
        });
        pathText = new TextBlock
        {
            Text = path,
            Foreground = (System.Windows.Media.Brush)WpfApplication.Current.Resources["TextMuted"],
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(pathText, 1);
        grid.Children.Add(pathText);
        return grid;
    }
}
