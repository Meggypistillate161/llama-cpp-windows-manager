using System.Collections.ObjectModel;

namespace LocalLlmConsole.ViewModels;

public sealed record RuntimeChoice(string Id, string Label, RuntimeBackend Backend);

public sealed class LaunchSettingsViewModel
{
    public ObservableCollection<RuntimeChoice> RuntimeChoices { get; } = new();

    public void ReplaceRuntimeChoices(IEnumerable<RuntimeRecord> runtimes)
        => ApplyRuntimeSelectorState(new LaunchRuntimeSelectorState(runtimes.ToArray(), null, null));

    public void ApplyRuntimeSelectorState(LaunchRuntimeSelectorState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        RuntimeChoices.Clear();
        foreach (var runtime in state.Runtimes)
            RuntimeChoices.Add(ChoiceFor(runtime));

        if (!string.IsNullOrWhiteSpace(state.MissingRuntimeId))
            RuntimeChoices.Insert(0, new RuntimeChoice(
                state.MissingRuntimeId,
                $"Missing runtime ({state.MissingRuntimeId})",
                RuntimeBackend.Cpu));
    }

    private static RuntimeChoice ChoiceFor(RuntimeRecord runtime)
        => new(runtime.Id, $"{runtime.Name} ({runtime.Mode}, {runtime.Backend})", runtime.Backend);
}
