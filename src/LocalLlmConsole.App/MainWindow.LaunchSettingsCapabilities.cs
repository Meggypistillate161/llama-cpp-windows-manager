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
    private async Task ApplyModelCapabilitiesAsync(ModelRecord? model, CancellationToken cancellationToken = default)
    {
        var capabilities = model is null
            ? ModelCapabilityService.Empty()
            : await CachedModelCapabilitiesAsync(model, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        if (model is not null && !string.Equals(SelectedModel()?.Id, model.Id, StringComparison.OrdinalIgnoreCase)) return;

        _selectedModelCapabilities = capabilities;
        if (_modelCapabilityText is not null)
        {
            var text = model is null ? "No model selected" : ModelCapabilityService.SummaryText(_selectedModelCapabilities);
            _modelCapabilityText.Text = text;
            _modelCapabilityText.ToolTip = TooltipText(text);
        }
        UpdateLaunchControlVisibility();
    }

    private async Task<ModelCapabilitySummary> CachedModelCapabilitiesAsync(ModelRecord model, CancellationToken cancellationToken = default)
    {
        var cacheKey = await Task.Run(() => ModelCapabilityService.CacheKey(model), cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        if (_modelCapabilityCache.TryGetValue(cacheKey, out var cached)) return cached;
        var inspected = await Task.Run(() => ModelCapabilityService.Inspect(model), cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        _modelCapabilityCache[cacheKey] = inspected;
        return inspected;
    }

    private void UpdateLaunchControlVisibility()
    {
        foreach (var section in _advancedLaunchSections)
            section.Visibility = _showAdvancedLaunchSettings ? Visibility.Visible : Visibility.Collapsed;

        var backend = SelectedLaunchRuntimeBackend();
        var gpuRuntime = backend is RuntimeBackend.Cuda or RuntimeBackend.Vulkan or RuntimeBackend.Metal;
        SetLaunchSettingVisible("GPU layers", gpuRuntime);
        SetLaunchSettingVisible("Vision", _selectedModelCapabilities.LikelyVision);
        SetLaunchSettingVisible("Image min", _selectedModelCapabilities.LikelyVision);
        SetLaunchSettingVisible("Image max", _selectedModelCapabilities.LikelyVision);
        SetLaunchSettingVisible("Reasoning", true);
        SetLaunchSettingVisible("Reason format", true);
        SetLaunchSettingVisible("Reason budget", true);
        SetLaunchSettingVisible("Jinja chat", true);

        if (_gpuLayersBox is not null)
            _gpuLayersBox.IsEnabled = gpuRuntime;
        if (_visionCombo is not null)
            _visionCombo.IsEnabled = _selectedModelCapabilities.LikelyVision;
        if (_visionImageMinTokensBox is not null)
            _visionImageMinTokensBox.IsEnabled = _selectedModelCapabilities.LikelyVision;
        if (_visionImageMaxTokensBox is not null)
            _visionImageMaxTokensBox.IsEnabled = _selectedModelCapabilities.LikelyVision;
        UpdateSpeculativeControlsEnabled();
    }

    private void SetLaunchSettingVisible(string label, bool visible)
    {
        if (!_launchSettingElements.TryGetValue(label, out var elements)) return;
        foreach (var element in elements)
            element.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
    }

    private void UpdateSpeculativeControlsEnabled()
    {
        var type = ComboValue(_speculativeTypeCombo);
        var draftEnabled = type.StartsWith("draft-", StringComparison.OrdinalIgnoreCase);
        foreach (var label in new[]
        {
            "Draft model", "Draft GPU", "Draft K cache", "Draft V cache",
            "Draft max", "Draft min", "Split prob", "Min prob"
        })
        {
            SetLaunchSettingEnabled(label, draftEnabled);
        }
    }

    private void SetLaunchSettingEnabled(string label, bool enabled)
    {
        if (!_launchSettingElements.TryGetValue(label, out var elements)) return;
        foreach (var element in elements)
            element.IsEnabled = enabled;
    }
}
