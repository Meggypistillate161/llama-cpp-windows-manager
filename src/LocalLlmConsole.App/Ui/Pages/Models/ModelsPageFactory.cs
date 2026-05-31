using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using WpfApplication = System.Windows.Application;
using WpfBrush = System.Windows.Media.Brush;
using WpfButton = System.Windows.Controls.Button;
using WpfTextBox = System.Windows.Controls.TextBox;

namespace LocalLlmConsole;

public sealed record ModelsPageActions(
    Func<Task> ScanModelsFolderAsync,
    Func<Task> ChooseModelsFolderAsync,
    Action OpenModelsFolder,
    Action<DataGrid, DataGrid?> SelectModelGridRow,
    RoutedEventHandler OpenModelFolderRowClick,
    RoutedEventHandler DeleteModelRowClick,
    Func<Task> SearchHuggingFaceAsync,
    Func<Task> ShowDownloadHistoryAsync,
    Action<DataGrid> ConfigureModelGridColumnSizing);

public sealed record ModelsPageRequest(
    MainWindowViewModel ViewModel,
    string ModelsRoot,
    UIElement LaunchSettingsPanel,
    ModelsPageActions Actions);

public sealed record ModelsPageControls(
    Grid Root,
    TextBlock ModelsFolderText,
    DataGrid ModelsGrid,
    DataGrid ModelVariantsGrid,
    WpfTextBox HuggingFaceQueryBox,
    DataGrid HuggingFaceGrid);

public static class ModelsPageFactory
{
    public const string ModelFilesTitle = "Model Files";
    public const string SavedModelVariantsTitle = "Saved Model Variants";
    public const string ModelFilesDescription = "Physical GGUF files discovered in the model folder or imported from another folder.";
    public const string SavedModelVariantsDescription = "Loadable aliases created from launch settings. They share the same GGUF file but keep separate names, model ids, ports, and profiles.";

    public static ModelsPageControls Create(ModelsPageRequest request)
    {
        var root = new Grid { Margin = new Thickness(16) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star), MinHeight = 260 });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(8) });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(230), MinHeight = 120 });

        var folderStrip = FolderStripActionsFirst(
            "Models folder",
            request.ModelsRoot,
            out var modelsFolderText,
            ("Scan Models Folder", request.Actions.ScanModelsFolderAsync),
            ("Choose", request.Actions.ChooseModelsFolderAsync),
            ("Open", () =>
            {
                request.Actions.OpenModelsFolder();
                return Task.CompletedTask;
            }
        ));
        Grid.SetRow(folderStrip, 0);
        root.Children.Add(folderStrip);

        var body = new Grid();
        body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.05, GridUnitType.Star), MinWidth = 330 });
        body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8) });
        body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(.95, GridUnitType.Star), MinWidth = 380 });

        var (modelLists, modelsGrid, modelVariantsGrid) = ModelLists(request);
        body.Children.Add(modelLists);
        body.Children.Add(PageSectionFactory.VerticalGridSplitter(1));
        Grid.SetColumn(request.LaunchSettingsPanel, 2);
        body.Children.Add(request.LaunchSettingsPanel);
        Grid.SetRow(body, 1);
        root.Children.Add(body);
        root.Children.Add(PageSectionFactory.HorizontalGridSplitter(2));

        var (huggingFaceSection, huggingFaceQueryBox, huggingFaceGrid) = HuggingFaceSearch(request);
        Grid.SetRow(huggingFaceSection, 3);
        root.Children.Add(huggingFaceSection);

        return new ModelsPageControls(root, modelsFolderText, modelsGrid, modelVariantsGrid, huggingFaceQueryBox, huggingFaceGrid);
    }

    private static (Grid Section, DataGrid ModelsGrid, DataGrid ModelVariantsGrid) ModelLists(ModelsPageRequest request)
    {
        var modelLists = new Grid();
        modelLists.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star), MinHeight = 130 });
        modelLists.RowDefinitions.Add(new RowDefinition { Height = new GridLength(8) });
        modelLists.RowDefinitions.Add(new RowDefinition { Height = new GridLength(.58, GridUnitType.Star), MinHeight = 96 });

        var modelsGrid = PageSectionFactory.GridFor(
            ("Name", nameof(ModelGridRow.Name), 2.35),
            ("Quant", nameof(ModelGridRow.Quant), .6),
            ("Size", nameof(ModelGridRow.Size), .65));
        PageSectionFactory.AddButtonColumn(modelsGrid, "Open Folder", nameof(ModelGridRow.OpenFolderAction), nameof(ModelGridRow.CanOpenFolder), request.Actions.OpenModelFolderRowClick, .85, tooltipBinding: nameof(ModelGridRow.OpenFolderToolTip));
        PageSectionFactory.AddButtonColumn(modelsGrid, "Delete", nameof(ModelGridRow.DeleteAction), nameof(ModelGridRow.CanDelete), request.Actions.DeleteModelRowClick, .65, tooltipBinding: nameof(ModelGridRow.DeleteToolTip));
        request.Actions.ConfigureModelGridColumnSizing(modelsGrid);
        modelsGrid.ItemsSource = request.ViewModel.Models.Rows;

        var modelVariantsGrid = PageSectionFactory.GridFor(
            ("Name", nameof(ModelGridRow.Name), 1.35),
            ("Base model", nameof(ModelGridRow.BaseModel), 1.35),
            ("Port", nameof(ModelGridRow.Port), .45));
        PageSectionFactory.AddButtonColumn(modelVariantsGrid, "Open Folder", nameof(ModelGridRow.OpenFolderAction), nameof(ModelGridRow.CanOpenFolder), request.Actions.OpenModelFolderRowClick, .75, tooltipBinding: nameof(ModelGridRow.OpenFolderToolTip));
        PageSectionFactory.AddButtonColumn(modelVariantsGrid, "Remove", nameof(ModelGridRow.DeleteAction), nameof(ModelGridRow.CanDelete), request.Actions.DeleteModelRowClick, .68, tooltipBinding: nameof(ModelGridRow.DeleteToolTip));
        modelVariantsGrid.ItemsSource = request.ViewModel.Models.VariantRows;

        modelsGrid.SelectionChanged += (_, _) => request.Actions.SelectModelGridRow(modelsGrid, modelVariantsGrid);
        modelVariantsGrid.SelectionChanged += (_, _) => request.Actions.SelectModelGridRow(modelVariantsGrid, modelsGrid);

        modelLists.Children.Add(PageSectionFactory.GridSection(ModelFilesTitle, modelsGrid, ModelFilesDescription));
        modelLists.Children.Add(PageSectionFactory.HorizontalGridSplitter(1));
        var variantsSection = PageSectionFactory.GridSection(SavedModelVariantsTitle, modelVariantsGrid, SavedModelVariantsDescription);
        Grid.SetRow(variantsSection, 2);
        modelLists.Children.Add(variantsSection);
        return (modelLists, modelsGrid, modelVariantsGrid);
    }

    private static (Grid Section, WpfTextBox QueryBox, DataGrid SearchGrid) HuggingFaceSearch(ModelsPageRequest request)
    {
        var hf = new Grid { Margin = new Thickness(0, 10, 0, 0) };
        hf.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        hf.RowDefinitions.Add(new RowDefinition());
        var hfBar = Bar();
        var hfQueryBox = new WpfTextBox { Width = 280, ToolTip = "Hugging Face search term, repo id, or model file URL" };
        hfBar.Children.Add(hfQueryBox);
        hfBar.Children.Add(Button("Search Hugging Face", request.Actions.SearchHuggingFaceAsync));
        hfBar.Children.Add(Button("History", request.Actions.ShowDownloadHistoryAsync));
        hf.Children.Add(hfBar);
        var hfGrid = new DataGrid();
        PageSectionFactory.PolishGrid(hfGrid);
        var hfGridFrame = PageSectionFactory.GridFrame(hfGrid);
        Grid.SetRow(hfGridFrame, 1);
        hf.Children.Add(hfGridFrame);
        return (hf, hfQueryBox, hfGrid);
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

    private static WrapPanel Bar()
        => new() { Orientation = System.Windows.Controls.Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 10) };

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
            "Scan Models Folder" => "Scan the models folder for local GGUF files.",
            "Choose" => "Choose a folder.",
            "Open" => "Open this folder.",
            "Search Hugging Face" => "Search Hugging Face for GGUF model files.",
            "History" => "Show model download history and controls.",
            var label => string.IsNullOrWhiteSpace(label) ? "" : $"Run {label}."
        };

    private static string TooltipText(string text) => text;
}
