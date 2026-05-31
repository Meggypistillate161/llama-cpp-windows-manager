using System.Windows;

namespace LocalLlmConsole;

public sealed record LifetimePageActionControllerActions(
    Func<UiRow?, Task> ResetLifetimeMetricAsync,
    Func<Func<Task>, Task> RunEventAsync);

public sealed class LifetimePageActionController
{
    private readonly LifetimePageActionControllerActions _actions;

    public LifetimePageActionController(LifetimePageActionControllerActions actions)
    {
        _actions = actions;
    }

    public LifetimePageActions Build()
        => new(ResetLifetimeRow_Click);

    private async void ResetLifetimeRow_Click(object sender, RoutedEventArgs e)
    {
        await _actions.RunEventAsync(async () =>
        {
            await _actions.ResetLifetimeMetricAsync((sender as FrameworkElement)?.Tag as UiRow);
        });
    }
}
