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
    private void ShowLifetime()
    {
        SetPage("Lifetime", "Persisted prompt and generated token totals.");
        var root = Dock();

        _lifetimeMetricsGrid = GridFor(("Model", "C1", 2.4), ("Prompt", "C2", .8), ("Generated", "C3", .8), ("Total", "C4", .8), ("Updated", "C5", 1.1));
        AddButtonColumn(_lifetimeMetricsGrid, "Reset", "C6", "B1", ResetLifetimeRow_Click, .55, tooltipBinding: "T1");
        _lifetimeMetricsGrid.ItemsSource = _viewModel.LifetimeMetrics.Rows;
        root.Children.Add(GridSection("Lifetime token usage", _lifetimeMetricsGrid));
        PageHost.Content = root;
        RunBackground(RefreshLifetimeMetricsAsync, "Lifetime metrics refresh failed");
    }

    private async Task RefreshModelsAsync()
    {
        if (_stateStore is null) return;
        var selectedId = SelectedModel()?.Id;
        var models = await _stateStore.ListModelsAsync();
        _viewModel.Models.ReplaceModels(models, IsModelActive);
        if (_modelsGrid is not null && _viewModel.Models.Rows.Count > 0)
        {
            _modelsGrid.SelectedItem = _viewModel.Models.Rows.FirstOrDefault(row => string.Equals(row.Model.Id, selectedId, StringComparison.OrdinalIgnoreCase))
                ?? _viewModel.Models.Rows.First();
        }
        await RenderSelectedModelLaunchSettingsAsync();
        UpdateModelActionButtons();
        RefreshOverviewModelChoices(models);
    }

    private async Task RefreshRuntimesAsync()
    {
        if (_stateStore is null) return;
        var selectedId = SelectedRuntime()?.Id;
        var runtimes = await _stateStore.ListRuntimesAsync();
        var sources = await Task.Run(() => RuntimeSources().ToList());
        var modelsByRuntime = await ModelsByRuntimeAsync();
        _viewModel.Runtimes.ReplaceRuntimes(runtimes, sources, modelsByRuntime, IsRuntimeActivelyUsed);
        if (_runtimeGrid is not null)
        {
            _runtimeGrid.SelectedItem = string.IsNullOrWhiteSpace(selectedId)
                ? null
                : _viewModel.Runtimes.Rows.FirstOrDefault(row => string.Equals(row.Runtime?.Id, selectedId, StringComparison.OrdinalIgnoreCase));
        }
        RefreshRuntimeBuildPresets(runtimes, sources);
        await RefreshRuntimeSelectorAsync(runtimes: runtimes);
    }

    private async Task RefreshLifetimeMetricsAsync()
    {
        if (_stateStore is null) return;
        var rows = await _stateStore.ListTokenUsageAsync();
        _viewModel.LifetimeMetrics.ReplaceRows(rows);
        _lifetimeMetricsGrid?.Items.Refresh();
    }

    private async void ResetLifetimeRow_Click(object sender, RoutedEventArgs e)
    {
        await RunEventAsync(async () =>
        {
            if ((sender as FrameworkElement)?.Tag is not UiRow row) return;
            if (string.Equals(row.Data["Kind"]?.ToString(), "total", StringComparison.OrdinalIgnoreCase))
            {
                await ResetAllLifetimeMetricsAsync();
                return;
            }

            await ResetLifetimeMetricRowAsync(row);
        });
    }

    private async Task ResetLifetimeMetricRowAsync(UiRow row)
    {
        if (_stateStore is null) return;
        if (!row.B1)
        {
            SetStatus("Only model rows can be reset individually.");
            return;
        }

        var modelId = row.Data["ModelId"]?.ToString();
        var modelName = row.Data["ModelName"]?.ToString() ?? row.C1;
        if (string.IsNullOrWhiteSpace(modelId))
        {
            SetStatus("Only model rows can be reset individually.");
            return;
        }

        if (ThemedMessageBox.Show(this, $"Reset lifetime token metrics for:\n\n{modelName}", "Reset lifetime metrics", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
            return;

        await _stateStore.DeleteTokenUsageAsync(modelId);
        await RefreshLifetimeMetricsAsync();
        SetStatus($"Lifetime metrics reset for {modelName}.");
    }

    private async Task ResetAllLifetimeMetricsAsync()
    {
        if (_stateStore is null) return;
        if (ThemedMessageBox.Show(this, "Reset lifetime token metrics for all models?", "Reset all lifetime metrics", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
            return;

        await _stateStore.DeleteAllTokenUsageAsync();
        ResetLifetimeCounters();
        await RefreshLifetimeMetricsAsync();
        SetStatus("All lifetime metrics reset.");
    }
}
