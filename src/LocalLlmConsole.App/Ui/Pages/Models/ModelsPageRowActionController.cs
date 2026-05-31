using System.Windows;

namespace LocalLlmConsole;

public sealed record ModelsPageRowActionControllerActions(
    Func<object, ModelRecord?> ModelFromRowButton,
    Func<ModelFolderApplicationActions> ModelFolderActions,
    Func<ModelRecord, Task> DeleteModelAsync,
    Func<HuggingFaceFile, Task> StartHuggingFaceDownloadAsync,
    Func<HuggingFaceModelCardApplicationActions> ModelCardActions,
    Func<Func<Task>, Task> RunEventAsync);

public sealed class ModelsPageRowActionController
{
    private readonly ModelFolderApplicationService _modelFolders;
    private readonly HuggingFaceModelCardApplicationService _modelCards;
    private readonly ModelsPageRowActionControllerActions _actions;

    public ModelsPageRowActionController(
        ModelFolderApplicationService modelFolders,
        HuggingFaceModelCardApplicationService modelCards,
        ModelsPageRowActionControllerActions actions)
    {
        _modelFolders = modelFolders;
        _modelCards = modelCards;
        _actions = actions;
    }

    public void OpenModelFolderRow_Click(object sender, RoutedEventArgs e)
        => _modelFolders.Open(_actions.ModelFromRowButton(sender), _actions.ModelFolderActions());

    public async void DeleteModelRow_Click(object sender, RoutedEventArgs e)
    {
        await _actions.RunEventAsync(async () =>
        {
            var model = _actions.ModelFromRowButton(sender);
            if (model is not null) await _actions.DeleteModelAsync(model);
        });
    }

    public async void DownloadHfRow_Click(object sender, RoutedEventArgs e)
    {
        await _actions.RunEventAsync(async () =>
        {
            if ((sender as FrameworkElement)?.Tag is not UiRow row || !row.B1) return;
            var file = row.Data.Deserialize<HuggingFaceFile>();
            if (file is not null) await _actions.StartHuggingFaceDownloadAsync(file);
        });
    }

    public void OpenHuggingFaceModelCardRow_Click(object sender, RoutedEventArgs e)
    {
        _modelCards.OpenFromRow(
            (sender as FrameworkElement)?.Tag as UiRow,
            _actions.ModelCardActions());
    }
}
