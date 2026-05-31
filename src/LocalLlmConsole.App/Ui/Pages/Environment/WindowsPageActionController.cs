namespace LocalLlmConsole;

public sealed record WindowsPageActionControllerActions(
    Func<Task> RefreshAsync,
    Func<Task> InstallCpuToolsAsync,
    Func<Task> InstallCudaToolkitAsync,
    Func<Task> InstallVulkanToolsAsync,
    Func<Task> InstallSyclToolsAsync);

public sealed class WindowsPageActionController
{
    private readonly WindowsPageActionControllerActions _actions;

    public WindowsPageActionController(WindowsPageActionControllerActions actions)
    {
        _actions = actions;
    }

    public WindowsPageActions Build()
        => new(
            _actions.RefreshAsync,
            _actions.InstallCpuToolsAsync,
            _actions.InstallCudaToolkitAsync,
            _actions.InstallVulkanToolsAsync,
            _actions.InstallSyclToolsAsync);
}
