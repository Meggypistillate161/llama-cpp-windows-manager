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
    private async Task<ModelLaunchSettings?> ReadModelLaunchProfileAsync(ModelRecord model)
        => _stateStore is null ? null : await _stateStore.GetModelLaunchSettingsAsync(model.Id);

    private async Task<ModelLaunchSettings> DraftModelLaunchProfileAsync(ModelRecord model)
    {
        var profile = await ReadModelLaunchProfileAsync(model);
        if (profile is not null) return profile;

        var port = await NextAvailableModelPortAsync(model.Id);
        return ModelLaunchSettings.FromAppSettings(_settings) with { Port = port };
    }

    private async Task<ModelLaunchSettings?> EnsureModelLaunchProfileAsync(ModelRecord model)
    {
        if (_stateStore is null) return null;

        var profile = await ReadModelLaunchProfileAsync(model);
        if (profile is { Port: >= 1 and <= 65535 }
            && await ModelLaunchPortAvailableAsync(model.Id, profile.Port))
            return profile;

        var next = (profile ?? await DraftModelLaunchProfileAsync(model)) with { Port = await NextAvailableModelPortAsync(model.Id) };
        await _stateStore.SaveModelLaunchSettingsAsync(model.Id, next);
        return next;
    }

    private async Task<bool> ModelLaunchPortAvailableAsync(string modelId, int port)
    {
        if (_stateStore is null || port is < 1 or > 65535) return false;

        foreach (var session in _sessions.Snapshots())
        {
            if (string.Equals(session.ModelId, modelId, StringComparison.OrdinalIgnoreCase)) continue;
            if (session.LaunchSettings.Port == port) return false;
        }

        foreach (var model in await _stateStore.ListModelsAsync())
        {
            if (string.Equals(model.Id, modelId, StringComparison.OrdinalIgnoreCase)) continue;
            var profile = await _stateStore.GetModelLaunchSettingsAsync(model.Id);
            if (profile?.Port == port) return false;
        }

        return true;
    }

    private async Task<int> NextAvailableModelPortAsync(string modelId)
    {
        if (_stateStore is null) return _settings.Port;

        var used = new List<int>();
        foreach (var session in _sessions.Snapshots())
        {
            if (!string.Equals(session.ModelId, modelId, StringComparison.OrdinalIgnoreCase))
                used.Add(session.LaunchSettings.Port);
        }

        foreach (var model in await _stateStore.ListModelsAsync())
        {
            if (string.Equals(model.Id, modelId, StringComparison.OrdinalIgnoreCase)) continue;
            var profile = await _stateStore.GetModelLaunchSettingsAsync(model.Id);
            if (profile is not null)
                used.Add(profile.Port);
        }

        return ModelPortAllocator.NextAvailable(_settings.Port, used);
    }
}
