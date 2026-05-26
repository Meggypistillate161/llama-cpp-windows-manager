using System.Collections.ObjectModel;

namespace LocalLlmConsole.ViewModels;

public sealed record RuntimeChoice(string Id, string Label, RuntimeBackend Backend);

public sealed class LaunchSettingsViewModel
{
    public ObservableCollection<RuntimeChoice> RuntimeChoices { get; } = new();

    public void ReplaceRuntimeChoices(IEnumerable<RuntimeRecord> runtimes)
    {
        RuntimeChoices.Clear();
        foreach (var runtime in runtimes)
        {
            var label = $"{runtime.Name} ({runtime.Mode}, {runtime.Backend})";
            RuntimeChoices.Add(new RuntimeChoice(runtime.Id, label, runtime.Backend));
        }
    }
}
