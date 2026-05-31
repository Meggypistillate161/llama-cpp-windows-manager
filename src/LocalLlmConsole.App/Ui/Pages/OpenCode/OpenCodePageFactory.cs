using System.Windows;
using System.Windows.Controls;
using WpfApplication = System.Windows.Application;
using WpfBrush = System.Windows.Media.Brush;
using WpfButton = System.Windows.Controls.Button;
using WpfComboBox = System.Windows.Controls.ComboBox;
using WpfTextBox = System.Windows.Controls.TextBox;

namespace LocalLlmConsole;

public sealed record OpenCodePageActions(
    Func<Task> DetectFilesAsync,
    Func<Task> ChooseConfigAsync,
    Func<Task> ChooseAgentsFolderAsync,
    Func<Task> RefreshAsync,
    Func<Task> LoadSelectedModelAsync,
    Func<Task> LoadLocalModelDraftAsync,
    Func<Task> SaveModelSnippetAsync,
    Func<Task> DeleteModelAsync,
    Func<Task> AddLocalModelAsync,
    Func<Task> UpdateLocalModelAsync,
    Func<Task> AddLocalModelAsNewAsync,
    Action ModelSnippetChanged,
    Func<Task> LoadSelectedAgentAsync,
    Func<Task> SaveAgentSnippetAsync,
    Func<Task> DeleteAgentAsync,
    Func<Task> CreateAgentAsync);

public sealed record OpenCodePageRequest(
    MainWindowViewModel ViewModel,
    OpenCodeFileSet Files,
    OpenCodePageActions Actions,
    Func<string, string> ButtonToolTip);

public sealed record OpenCodePageControls(
    Grid Root,
    TextBlock ConfigPathText,
    TextBlock AgentsPathText,
    TextBlock HealthText,
    WpfComboBox ModelCombo,
    WpfButton SaveModelButton,
    WpfButton DeleteModelButton,
    WpfComboBox LocalModelCombo,
    TextBlock AddModelStatusText,
    WpfButton AddLocalModelButton,
    WpfButton UpdateLocalModelButton,
    WpfButton AddAsNewLocalModelButton,
    FrameworkElement AddModelPanel,
    WpfTextBox ModelSnippetBox,
    WpfComboBox AgentCombo,
    WpfButton SaveAgentButton,
    WpfButton DeleteAgentButton,
    WpfTextBox NewAgentNameBox,
    WpfComboBox AgentKindCombo,
    WpfButton CreateAgentButton,
    FrameworkElement AddAgentPanel,
    WpfTextBox AgentSnippetBox);

public static class OpenCodePageFactory
{
    public static OpenCodePageControls Create(OpenCodePageRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.ViewModel);
        ArgumentNullException.ThrowIfNull(request.Actions);
        ArgumentNullException.ThrowIfNull(request.ButtonToolTip);

        var root = new Grid { Margin = new Thickness(16) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star), MinHeight = 210 });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(8) });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star), MinHeight = 210 });

        var filesPanel = FilesPanel(request, out var configPathText, out var agentsPathText, out var healthText);
        Grid.SetRow(filesPanel, 0);
        root.Children.Add(filesPanel);

        var modelSection = ModelSection(request, out var modelControls);
        Grid.SetRow(modelSection, 1);
        root.Children.Add(modelSection);
        root.Children.Add(PageSectionFactory.HorizontalGridSplitter(2));

        var agentSection = AgentSection(request, out var agentControls);
        Grid.SetRow(agentSection, 3);
        root.Children.Add(agentSection);

        return new OpenCodePageControls(
            root,
            configPathText,
            agentsPathText,
            healthText,
            modelControls.ModelCombo,
            modelControls.SaveModelButton,
            modelControls.DeleteModelButton,
            modelControls.LocalModelCombo,
            modelControls.AddModelStatusText,
            modelControls.AddLocalModelButton,
            modelControls.UpdateLocalModelButton,
            modelControls.AddAsNewLocalModelButton,
            modelControls.AddModelPanel,
            modelControls.ModelSnippetBox,
            agentControls.AgentCombo,
            agentControls.SaveAgentButton,
            agentControls.DeleteAgentButton,
            agentControls.NewAgentNameBox,
            agentControls.AgentKindCombo,
            agentControls.CreateAgentButton,
            agentControls.AddAgentPanel,
            agentControls.AgentSnippetBox);
    }

    private static Grid FilesPanel(
        OpenCodePageRequest request,
        out TextBlock configPathText,
        out TextBlock agentsPathText,
        out TextBlock healthText)
    {
        var panel = new Grid { Margin = new Thickness(0, 0, 0, 8) };
        panel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        panel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        panel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        panel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var toolbar = Bar();
        toolbar.Children.Add(Button("Detect Files", request.Actions.DetectFilesAsync, request.ButtonToolTip));
        toolbar.Children.Add(Button("Choose Config", request.Actions.ChooseConfigAsync, request.ButtonToolTip));
        toolbar.Children.Add(Button("Choose Agents Folder", request.Actions.ChooseAgentsFolderAsync, request.ButtonToolTip));
        toolbar.Children.Add(Button("Refresh", request.Actions.RefreshAsync, request.ButtonToolTip));
        panel.Children.Add(toolbar);

        var configRow = PathRow("Config", request.Files.ConfigPath, out configPathText);
        Grid.SetRow(configRow, 1);
        panel.Children.Add(configRow);

        var agentsRow = PathRow("Agents", request.Files.AgentsDirectory, out agentsPathText);
        Grid.SetRow(agentsRow, 2);
        panel.Children.Add(agentsRow);

        healthText = Text("", 12, false, true);
        healthText.Margin = new Thickness(0, 2, 0, 6);
        Grid.SetRow(healthText, 3);
        panel.Children.Add(healthText);
        return panel;
    }

    private static Grid ModelSection(OpenCodePageRequest request, out OpenCodeModelControls controls)
    {
        var left = new StackPanel { Margin = new Thickness(0, 6, 10, 6) };
        left.Children.Add(Text("Model", 12, true, true));
        var modelCombo = new WpfComboBox
        {
            ItemsSource = request.ViewModel.OpenCode.ModelChoices,
            DisplayMemberPath = nameof(OpenCodeModelEntry.Label),
            MinHeight = 30,
            HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch,
            ToolTip = TooltipText("Models configured in the selected OpenCode provider config.")
        };
        modelCombo.SelectionChanged += async (_, _) => await request.Actions.LoadSelectedModelAsync();
        left.Children.Add(modelCombo);

        var modelActions = Bar();
        var saveModelButton = Button("Update Config", request.Actions.SaveModelSnippetAsync, request.ButtonToolTip);
        var deleteModelButton = Button("Delete Config", request.Actions.DeleteModelAsync, request.ButtonToolTip);
        modelActions.Children.Add(saveModelButton);
        modelActions.Children.Add(deleteModelButton);
        left.Children.Add(modelActions);

        var addPanel = new StackPanel { Margin = new Thickness(0, 8, 0, 0), Visibility = Visibility.Collapsed };
        addPanel.Children.Add(Text("Local model", 12, true, true));
        var localModelCombo = new WpfComboBox
        {
            ItemsSource = request.ViewModel.OpenCode.LocalModelChoices,
            DisplayMemberPath = nameof(ModelRecord.Name),
            SelectedValuePath = nameof(ModelRecord.Id),
            MinHeight = 30,
            HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch,
            ToolTip = TooltipText("Choose one of the app's registered local GGUF models.")
        };
        localModelCombo.SelectionChanged += async (_, _) => await request.Actions.LoadLocalModelDraftAsync();
        addPanel.Children.Add(localModelCombo);
        var addModelStatusText = Text("", 12, false, true);
        addModelStatusText.Margin = new Thickness(0, 4, 0, 8);
        addPanel.Children.Add(addModelStatusText);

        var addActions = Bar();
        var addLocalModelButton = Button("Add", request.Actions.AddLocalModelAsync, request.ButtonToolTip);
        var updateLocalModelButton = Button("Update", request.Actions.UpdateLocalModelAsync, request.ButtonToolTip);
        var addAsNewLocalModelButton = Button("Add As New", request.Actions.AddLocalModelAsNewAsync, request.ButtonToolTip);
        addActions.Children.Add(addLocalModelButton);
        addActions.Children.Add(updateLocalModelButton);
        addActions.Children.Add(addAsNewLocalModelButton);
        addPanel.Children.Add(addActions);
        left.Children.Add(addPanel);

        var modelSnippetBox = EditorBox();
        modelSnippetBox.TextChanged += (_, _) => request.Actions.ModelSnippetChanged();
        controls = new OpenCodeModelControls(
            modelCombo,
            saveModelButton,
            deleteModelButton,
            localModelCombo,
            addModelStatusText,
            addLocalModelButton,
            updateLocalModelButton,
            addAsNewLocalModelButton,
            addPanel,
            modelSnippetBox);
        return SplitSection("OpenCode models", left, modelSnippetBox);
    }

    private static Grid AgentSection(OpenCodePageRequest request, out OpenCodeAgentControls controls)
    {
        var left = new StackPanel { Margin = new Thickness(0, 6, 10, 6) };
        left.Children.Add(Text("Agent", 12, true, true));
        var agentCombo = new WpfComboBox
        {
            ItemsSource = request.ViewModel.OpenCode.AgentChoices,
            DisplayMemberPath = nameof(OpenCodeAgentEntry.Label),
            MinHeight = 30,
            HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch,
            ToolTip = TooltipText("Agents from the selected OpenCode config and agents folder.")
        };
        agentCombo.SelectionChanged += async (_, _) => await request.Actions.LoadSelectedAgentAsync();
        left.Children.Add(agentCombo);

        var actions = Bar();
        var saveAgentButton = Button("Save Agent", request.Actions.SaveAgentSnippetAsync, request.ButtonToolTip);
        var deleteAgentButton = Button("Delete Agent", request.Actions.DeleteAgentAsync, request.ButtonToolTip);
        actions.Children.Add(saveAgentButton);
        actions.Children.Add(deleteAgentButton);
        left.Children.Add(actions);

        var addPanel = new StackPanel { Margin = new Thickness(0, 8, 0, 0), Visibility = Visibility.Collapsed };
        addPanel.Children.Add(Text("New agent", 12, true, true));
        var newAgentNameBox = new WpfTextBox
        {
            ToolTip = TooltipText("Lowercase id is generated from this name."),
            HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch
        };
        addPanel.Children.Add(newAgentNameBox);
        var agentKindCombo = Combo("config json", "markdown file");
        addPanel.Children.Add(agentKindCombo);
        var createAgentButton = Button("Create Agent", request.Actions.CreateAgentAsync, request.ButtonToolTip);
        addPanel.Children.Add(createAgentButton);
        left.Children.Add(addPanel);

        var agentSnippetBox = EditorBox();
        controls = new OpenCodeAgentControls(
            agentCombo,
            saveAgentButton,
            deleteAgentButton,
            newAgentNameBox,
            agentKindCombo,
            createAgentButton,
            addPanel,
            agentSnippetBox);
        return SplitSection("OpenCode agents", left, agentSnippetBox);
    }

    private static WpfTextBox EditorBox() => new()
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

    private static Grid SplitSection(string title, UIElement left, UIElement right)
    {
        var section = new Grid { Margin = new Thickness(0, 0, 0, 4) };
        section.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        section.RowDefinitions.Add(new RowDefinition());
        section.Children.Add(new TextBlock
        {
            Text = title,
            FontWeight = FontWeights.SemiBold,
            Foreground = (WpfBrush)WpfApplication.Current.Resources["TextSoft"],
            Margin = new Thickness(2, 0, 0, 0)
        });

        var body = new Grid();
        body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(300), MinWidth = 230 });
        body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8) });
        body.ColumnDefinitions.Add(new ColumnDefinition());
        body.Children.Add(PaneFrame(left));
        body.Children.Add(PageSectionFactory.VerticalGridSplitter(1));
        Grid.SetColumn(right, 2);
        body.Children.Add(right);
        Grid.SetRow(body, 1);
        section.Children.Add(body);
        return section;
    }

    private static Border PaneFrame(UIElement child) => new()
    {
        Background = (WpfBrush)WpfApplication.Current.Resources["InputBack"],
        BorderBrush = (WpfBrush)WpfApplication.Current.Resources["PanelBorder"],
        BorderThickness = new Thickness(1),
        CornerRadius = new CornerRadius(6),
        Margin = new Thickness(0, 6, 0, 6),
        Padding = new Thickness(10, 6, 8, 8),
        Child = child
    };

    private static Grid PathRow(string label, string path, out TextBlock pathText)
    {
        var grid = new Grid { Margin = new Thickness(0, 0, 0, 3) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(86) });
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.Children.Add(new TextBlock
        {
            Text = label,
            FontWeight = FontWeights.SemiBold,
            Foreground = (WpfBrush)WpfApplication.Current.Resources["TextSoft"],
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 10, 0)
        });
        pathText = new TextBlock
        {
            Text = path,
            Foreground = (WpfBrush)WpfApplication.Current.Resources["TextMuted"],
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(pathText, 1);
        grid.Children.Add(pathText);
        return grid;
    }

    private static WrapPanel Bar()
        => new() { Orientation = System.Windows.Controls.Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 10) };

    private static WpfButton Button(string text, Func<Task> click, Func<string, string> toolTip)
    {
        var button = new WpfButton
        {
            Content = text,
            ToolTip = TooltipText(toolTip(text))
        };
        ToolTipService.SetShowOnDisabled(button, true);
        button.Click += async (_, _) => await click();
        return button;
    }

    private static WpfComboBox Combo(params string[] values)
    {
        var combo = new WpfComboBox { MinHeight = 30 };
        foreach (var value in values)
            combo.Items.Add(value);
        combo.SelectedIndex = values.Length > 0 ? 0 : -1;
        return combo;
    }

    private static TextBlock Text(string text, int size = 13, bool bold = false, bool muted = false) => new()
    {
        Text = text,
        FontSize = size,
        FontWeight = bold ? FontWeights.SemiBold : FontWeights.Normal,
        Foreground = muted ? (WpfBrush)WpfApplication.Current.Resources["TextMuted"] : (WpfBrush)WpfApplication.Current.Resources["TextMain"],
        TextWrapping = TextWrapping.Wrap,
        Margin = new Thickness(0, size >= 18 ? 10 : 0, 0, size >= 18 ? 10 : 8)
    };

    private static string TooltipText(string text) => text;

    private sealed record OpenCodeModelControls(
        WpfComboBox ModelCombo,
        WpfButton SaveModelButton,
        WpfButton DeleteModelButton,
        WpfComboBox LocalModelCombo,
        TextBlock AddModelStatusText,
        WpfButton AddLocalModelButton,
        WpfButton UpdateLocalModelButton,
        WpfButton AddAsNewLocalModelButton,
        FrameworkElement AddModelPanel,
        WpfTextBox ModelSnippetBox);

    private sealed record OpenCodeAgentControls(
        WpfComboBox AgentCombo,
        WpfButton SaveAgentButton,
        WpfButton DeleteAgentButton,
        WpfTextBox NewAgentNameBox,
        WpfComboBox AgentKindCombo,
        WpfButton CreateAgentButton,
        FrameworkElement AddAgentPanel,
        WpfTextBox AgentSnippetBox);
}
