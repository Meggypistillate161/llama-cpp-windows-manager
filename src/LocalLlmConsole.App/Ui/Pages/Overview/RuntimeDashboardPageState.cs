using System.Windows.Controls;
using WpfProgressBar = System.Windows.Controls.ProgressBar;
using WpfTextBox = System.Windows.Controls.TextBox;

namespace LocalLlmConsole;

public sealed class RuntimeDashboardPageState
{
    public Grid? ModelMetric { get; private set; }

    public Grid? RuntimeMetric { get; private set; }

    public Grid? RequestsMetric { get; private set; }

    public Grid? GenerationRateMetric { get; private set; }

    public TextBlock? GenerationRateLastKnown { get; private set; }

    public Grid? TotalTokensMetric { get; private set; }

    public Grid? GpuMetric { get; private set; }

    public WpfTextBox? RuntimeLogBox { get; private set; }

    public DataGrid? RuntimeMetricsGrid { get; private set; }

    public WpfProgressBar? ModelProgress { get; private set; }

    public void Apply(OverviewPageControls controls)
    {
        ArgumentNullException.ThrowIfNull(controls);

        ModelMetric = controls.RuntimeDashboardModel;
        RuntimeMetric = controls.RuntimeDashboardRuntime;
        RequestsMetric = controls.RuntimeDashboardRequests;
        GenerationRateMetric = controls.RuntimeDashboardGenerationRate;
        GenerationRateLastKnown = controls.RuntimeDashboardGenerationRateLastKnown;
        TotalTokensMetric = controls.RuntimeDashboardTotalTokens;
        GpuMetric = controls.RuntimeDashboardGpu;
        RuntimeLogBox = controls.RuntimeLogBox;
        RuntimeMetricsGrid = controls.RuntimeMetricsGrid;
        ModelProgress = null;
    }
}
