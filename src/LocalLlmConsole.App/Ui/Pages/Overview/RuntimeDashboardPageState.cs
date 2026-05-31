using System.Windows.Controls;
using WpfProgressBar = System.Windows.Controls.ProgressBar;
using WpfTextBox = System.Windows.Controls.TextBox;

namespace LocalLlmConsole;

public sealed class RuntimeDashboardPageState
{
    public Grid? ModelMetric { get; private set; }

    public Grid? GpuMetric { get; private set; }

    public Grid? RequestsMetric { get; private set; }

    public Grid? TokensMetric { get; private set; }

    public TextBlock? TokensLastKnown { get; private set; }

    public Grid? MtpTokensMetric { get; private set; }

    public Grid? SlotsMetric { get; private set; }

    public WpfTextBox? RuntimeLogBox { get; private set; }

    public DataGrid? RuntimeMetricsGrid { get; private set; }

    public WpfProgressBar? ModelProgress { get; private set; }

    public void Apply(OverviewPageControls controls)
    {
        ArgumentNullException.ThrowIfNull(controls);

        ModelMetric = controls.RuntimeDashboardModel;
        GpuMetric = controls.RuntimeDashboardGpu;
        RequestsMetric = controls.RuntimeDashboardRequests;
        TokensMetric = controls.RuntimeDashboardTokens;
        TokensLastKnown = controls.RuntimeDashboardTokensLastKnown;
        MtpTokensMetric = controls.RuntimeDashboardMtpTokens;
        SlotsMetric = controls.RuntimeDashboardSlots;
        RuntimeLogBox = controls.RuntimeLogBox;
        RuntimeMetricsGrid = controls.RuntimeMetricsGrid;
        ModelProgress = null;
    }
}
