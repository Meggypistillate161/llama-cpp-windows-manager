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
    private async Task RunAsync(string message, Func<Task> action)
    {
        if (!TryBeginUiBusy(message)) return;
        try
        {
            SetStatus(message);
            await System.Windows.Threading.Dispatcher.Yield(System.Windows.Threading.DispatcherPriority.Background);
            await action();
            if (_viewModel.StatusText == message) SetStatus("");
        }
        catch (Exception ex)
        {
            SetStatus(ex.Message);
            await WriteAppLogAsync(ex);
            ThemedMessageBox.Show(this, ex.Message, AppDisplayName, MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            EndUiBusy();
        }
    }

    private async Task RunEventAsync(Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (Exception ex)
        {
            SetStatus(ex.Message);
            await WriteAppLogAsync(ex);
            ThemedMessageBox.Show(this, ex.Message, AppDisplayName, MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void RunBackground(Func<Task> action, string failureMessage)
    {
        _ = RunBackgroundAsync(action, failureMessage);
    }

    private async Task RunBackgroundAsync(Func<Task> action, string failureMessage)
    {
        try
        {
            await action();
        }
        catch (OperationCanceledException)
        {
            // Superseded UI refreshes are expected when the user changes selection quickly.
        }
        catch (Exception ex)
        {
            SetStatus($"{failureMessage}: {ex.Message}");
            await WriteAppLogAsync(ex);
        }
    }
}
