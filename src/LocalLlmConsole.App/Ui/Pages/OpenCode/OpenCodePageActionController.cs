namespace LocalLlmConsole;

public sealed record OpenCodePageActionControllerActions(
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
    Func<bool> IsModelSnippetProgrammaticUpdate,
    Action UpdateModelEditorState,
    Func<Task> LoadSelectedAgentAsync,
    Func<Task> SaveAgentSnippetAsync,
    Func<Task> DeleteAgentAsync,
    Func<Task> CreateAgentAsync);

public sealed class OpenCodePageActionController
{
    private readonly OpenCodePageActionControllerActions _actions;

    public OpenCodePageActionController(OpenCodePageActionControllerActions actions)
    {
        _actions = actions;
    }

    public OpenCodePageActions Build()
        => new(
            _actions.DetectFilesAsync,
            _actions.ChooseConfigAsync,
            _actions.ChooseAgentsFolderAsync,
            _actions.RefreshAsync,
            _actions.LoadSelectedModelAsync,
            _actions.LoadLocalModelDraftAsync,
            _actions.SaveModelSnippetAsync,
            _actions.DeleteModelAsync,
            _actions.AddLocalModelAsync,
            _actions.UpdateLocalModelAsync,
            _actions.AddLocalModelAsNewAsync,
            ModelSnippetChanged,
            _actions.LoadSelectedAgentAsync,
            _actions.SaveAgentSnippetAsync,
            _actions.DeleteAgentAsync,
            _actions.CreateAgentAsync);

    private void ModelSnippetChanged()
    {
        if (!_actions.IsModelSnippetProgrammaticUpdate())
            _actions.UpdateModelEditorState();
    }
}
