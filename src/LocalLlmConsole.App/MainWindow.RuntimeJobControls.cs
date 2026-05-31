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
    private async Task CancelRuntimeBuildJobAsync(JobRecord job)
    {
        var buildJobApplication = RuntimeServices.RuntimeBuildJobApplication;
        if (buildJobApplication is null) return;
        await buildJobApplication.CancelAsync(job, _settings, MaxLogBytes(), RuntimeBuildJobApplicationActions());
    }

    private async Task RetryRuntimeBuildJobAsync(JobRecord job)
    {
        var buildJobApplication = RuntimeServices.RuntimeBuildJobApplication;
        if (buildJobApplication is null) return;
        await buildJobApplication.RetryAsync(job, RuntimeBuildJobApplicationActions());
    }

    private async Task ClearRuntimeBuildJobAsync(JobRecord job)
    {
        var buildJobApplication = RuntimeServices.RuntimeBuildJobApplication;
        if (buildJobApplication is null) return;
        await buildJobApplication.ClearAsync(job, RuntimeBuildJobApplicationActions());
    }

    private RuntimeBuildJobApplicationActions RuntimeBuildJobApplicationActions()
        => new(
            confirmation => _coreServices.App.Dialogs.Confirm(
                this,
                confirmation.Message,
                confirmation.Title,
                MessageBoxImage.Warning),
            RunAsync,
            RefreshJobsAsync,
            retry => BuildManagedRuntimeAsync(retry.Preset!, retry.Update, retry.Source),
            SetStatus);
}
