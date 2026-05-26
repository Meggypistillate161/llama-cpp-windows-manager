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
    private async Task RecoverActiveRuntimeSessionAsync()
    {
        if (_stateStore is null) return;
        var session = await _activeSessions.TryReadAsync();
        if (session is null) return;

        try
        {
            if (session is null || string.IsNullOrWhiteSpace(session.ModelId) || string.IsNullOrWhiteSpace(session.RuntimeId))
            {
                ClearActiveRuntimeSession();
                return;
            }

            var models = await _stateStore.ListModelsAsync();
            var model = models.FirstOrDefault(item => string.Equals(item.Id, session.ModelId, StringComparison.OrdinalIgnoreCase));
            var runtimes = await _stateStore.ListRuntimesAsync();
            var runtime = runtimes.FirstOrDefault(item => string.Equals(item.Id, session.RuntimeId, StringComparison.OrdinalIgnoreCase));
            if (model is null || runtime is null)
            {
                ClearActiveRuntimeSession();
                return;
            }

            if (runtime.Mode == RuntimeMode.Wsl && string.IsNullOrWhiteSpace(session.ProcessMarker))
            {
                ClearActiveRuntimeSession();
                return;
            }
            if (runtime.Mode == RuntimeMode.Native && !NativeRuntimeProcessMatches(session, runtime))
            {
                ClearActiveRuntimeSession();
                return;
            }

            var servedModels = await RuntimeServedModelsAsync(session.LaunchSettings);
            if (servedModels.Count > 0 && !servedModels.Any(served => RuntimeEndpointService.ServedModelMatches(model, served)))
            {
                ClearActiveRuntimeSession();
                return;
            }

            if (!await RuntimeEndpointAliveAsync(session.LaunchSettings))
            {
                if (!await RuntimeEndpointRespondingAsync(session.LaunchSettings))
                {
                    ClearActiveRuntimeSession();
                    return;
                }

                _llama.AttachExisting(runtime, model.Id, session.LaunchSettings, session.LogPath, LlamaRuntimeState.Loading, session.ProcessMarker);
                _activeRuntimeSettings = session.LaunchSettings;
                StartModelLoadingTimer(model.Name, session.LaunchSettings);
                StartRuntimeReadinessMonitor(model, session.LaunchSettings);
                StartRuntimeDashboardRefreshTimer();
                await RefreshOverviewModelSelectorAsync();
                await RefreshRuntimeMetricsAsync();
                return;
            }

            _llama.AttachExisting(runtime, model.Id, session.LaunchSettings, session.LogPath, processMarker: session.ProcessMarker);
            _activeRuntimeSettings = session.LaunchSettings;
            SetStatus($"Recovered running model {model.Name} at {RuntimeEndpointService.EndpointDisplay(session.LaunchSettings)}.");
            StartRuntimeDashboardRefreshTimer();
            await RefreshOverviewModelSelectorAsync();
            await RefreshRuntimeMetricsAsync();
        }
        catch
        {
            ClearActiveRuntimeSession();
        }
    }

    private static bool NativeRuntimeProcessMatches(ActiveRuntimeSession session, RuntimeRecord runtime)
    {
        if (session.ProcessId <= 0) return false;
        try
        {
            using var process = Process.GetProcessById(session.ProcessId);
            if (process.HasExited) return false;
            var processPath = process.MainModule?.FileName;
            if (string.IsNullOrWhiteSpace(processPath)) return false;
            return string.Equals(Path.GetFullPath(processPath), Path.GetFullPath(runtime.ExecutablePath), StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private async Task MarkRuntimeLoadedIfReadyAsync(AppSettings launchSettings)
    {
        if (_llama.State != LlamaRuntimeState.Loading || !_llama.IsRunning) return;
        if (!await RuntimeEndpointAliveAsync(launchSettings)) return;

        if (!_llama.MarkLoadedIfRunning()) return;
        var modelName = await ActiveModelDisplayNameAsync(_llama.ActiveModelId);
        StopModelLoadingTimer(showLoadedDuration: true, loadedModelName: modelName);
        SetStatus($"Loaded {modelName} at {RuntimeEndpointService.EndpointDisplay(launchSettings)}.");
        UpdateModelActionButtons();
        UpdateOverviewModelActions();
    }

    private async Task<bool> RuntimeEndpointAliveAsync(AppSettings launchSettings)
    {
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(1.5) };
        foreach (var path in new[] { "health", "v1/models" })
        {
            try
            {
                using var request = RuntimeEndpointService.RuntimeGetRequest($"{RuntimeEndpointService.LocalServerBaseUrl(launchSettings)}/{path}", launchSettings);
                using var response = await client.SendAsync(request);
                if (response.IsSuccessStatusCode) return true;
            }
            catch
            {
                // Try the next endpoint.
            }
        }

        return false;
    }

    private async Task<bool> RuntimeEndpointRespondingAsync(AppSettings launchSettings)
    {
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(1.5) };
        foreach (var path in new[] { "health", "v1/models", "metrics" })
        {
            try
            {
                using var request = RuntimeEndpointService.RuntimeGetRequest($"{RuntimeEndpointService.LocalServerBaseUrl(launchSettings)}/{path}", launchSettings);
                using var _ = await client.SendAsync(request);
                return true;
            }
            catch
            {
                // Try the next endpoint.
            }
        }

        return false;
    }

    private async Task<IReadOnlyList<string>> RuntimeServedModelsAsync(AppSettings launchSettings)
    {
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(1.5) };
        try
        {
            var json = await RuntimeEndpointService.RuntimeGetStringAsync(client, $"{RuntimeEndpointService.LocalOpenAiBaseUrl(launchSettings)}/models", launchSettings);
            return RuntimeEndpointService.ExtractServedModelIds(json).ToArray();
        }
        catch
        {
            return [];
        }
    }

    private async Task SaveActiveRuntimeSessionAsync(RuntimeRecord runtime, ModelRecord model, AppSettings launchSettings)
        => await SaveActiveRuntimeSessionAsync(runtime.Id, model.Id, launchSettings, _llama.LogPath);

    private async Task SaveActiveRuntimeSessionAsync(string runtimeId, string modelId, AppSettings launchSettings, string logPath)
    {
        var session = new ActiveRuntimeSession(modelId, runtimeId, launchSettings, logPath, DateTimeOffset.UtcNow, _llama.WslProcessMarker, _llama.ProcessId);
        await _activeSessions.SaveAsync(session);
    }

    private void ClearActiveRuntimeSession() => _activeSessions.Clear();
}
