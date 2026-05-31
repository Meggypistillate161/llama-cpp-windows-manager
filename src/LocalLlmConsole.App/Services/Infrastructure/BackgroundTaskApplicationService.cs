namespace LocalLlmConsole.Services;

public sealed record BackgroundTaskApplicationActions(
    Action<string> SetStatus,
    Func<Exception, Task> WriteErrorAsync);

public sealed class BackgroundTaskApplicationService
{
    public async Task RunAsync(
        Func<Task> action,
        string failureMessage,
        BackgroundTaskApplicationActions actions)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(actions);
        ArgumentNullException.ThrowIfNull(actions.SetStatus);
        ArgumentNullException.ThrowIfNull(actions.WriteErrorAsync);

        try
        {
            await action();
        }
        catch (OperationCanceledException)
        {
            // Superseded UI refreshes are expected when the user changes selection quickly.
        }
        catch (Exception ex)
        {
            actions.SetStatus($"{failureMessage}: {ex.Message}");
            await actions.WriteErrorAsync(ex);
        }
    }
}
