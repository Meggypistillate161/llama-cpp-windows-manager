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
    private async Task TrackLifetimeTokenDeltasAsync(IReadOnlyList<RuntimeMetricPollResult> pollResults)
    {
        var lifetimeMetrics = AppServices.LifetimeMetricsApplication;
        if (lifetimeMetrics is null)
        {
            ResetLifetimeCounters();
            return;
        }

        foreach (var delta in _coreServices.Runtime.RuntimeTelemetryApplication.ObserveLifetimeTokenDeltas(pollResults))
            await lifetimeMetrics.AddUsageAsync(delta);

        if (_viewModel.CurrentPage == "Lifetime") await RefreshLifetimeMetricsAsync();
    }

    private void ResetLifetimeCounters()
    {
        _coreServices.Runtime.RuntimeTelemetryApplication.ResetLifetimeCounters();
    }

    private void ResetLifetimeCounters(LoadedModelSessionSnapshot? session)
    {
        _coreServices.Runtime.RuntimeTelemetryApplication.ResetLifetimeCounters(session);
    }

    private async Task ApplyIdleUnloadPoliciesAsync(IReadOnlyList<RuntimeMetricPollResult> pollResults)
    {
        await _coreServices.Runtime.RuntimeTelemetryApplication.ApplyIdleUnloadPoliciesAsync(
            pollResults,
            _settings.AutoUnloadIdleMinutes,
            DateTimeOffset.UtcNow,
            RuntimeIdleUnloadActions());
    }

    private RuntimeIdleUnloadApplicationActions RuntimeIdleUnloadActions()
        => new(FindModelByIdAsync, StopModelRuntimeAsync, SetStatus);

    private void ResetIdleCounters()
    {
        _coreServices.Runtime.RuntimeTelemetryApplication.ResetIdleCounters();
    }

    private void ResetIdleCounters(LoadedModelSessionSnapshot? session)
    {
        _coreServices.Runtime.RuntimeTelemetryApplication.ResetIdleCounters(session);
    }
}
