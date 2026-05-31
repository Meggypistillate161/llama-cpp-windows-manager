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
    private void ShowOpenCode()
    {
        SetPage("OpenCode", "Model and agent config.");
        _openCodeFileSet.Set(_coreServices.OpenCodeServices.OpenCodeFileSetApplication.LoadOrDetect());

        var page = OpenCodePageFactory.Create(new OpenCodePageRequest(
            _viewModel,
            _openCodeFileSet.Current,
            _pageControllers.OpenCode.Build(),
            ButtonToolTip));
        ApplyOpenCodePageControls(page);
        PageHost.Content = page.Root;
        RunBackground(() => RunAsync("Loading OpenCode config...", () => RefreshOpenCodeAsync()), "OpenCode config load failed");
    }

    private void ApplyOpenCodePageControls(OpenCodePageControls page)
    {
        _openCodePage.Apply(page);
    }
}
