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
    private void ShowHelp()
    {
        SetPage("Help", "First-run setup steps.");

        var panel = new StackPanel { Margin = new Thickness(0, 0, 10, 0) };
        panel.Children.Add(Text("First Steps", 22, true));
        panel.Children.Add(Text("Start with an official prebuilt runtime. Build tools and source builds are advanced options for custom forks, patches, or release targets that do not have an official package.", 13, muted: true));

        panel.Children.Add(HelpStep(
            "Step 1",
            "Install an official runtime",
            "Open Runtimes and install the official prebuilt llama.cpp runtime for your target: Windows or WSL, then CUDA, CPU, Vulkan, or Intel Arc SYCL.",
            ("Open Runtimes", (_, _) => NavigateFromHelp("runtime-download"))));

        panel.Children.Add(HelpStep(
            "Step 2",
            "Download a model",
            "Open Models, search Hugging Face, select the model file you want, and click Download on the row.",
            ("Open Models", (_, _) => NavigateFromHelp("model-download"))));

        panel.Children.Add(HelpStep(
            "Step 3",
            "Save model launch settings",
            "Downloaded models are registered automatically. Use Scan Models Folder only if you copied a model manually or the downloaded model does not appear. Select the runtime, keep or change the model port, adjust launch settings, then click Save For Model.",
            ("Open Models", (_, _) => NavigateFromHelp("launch-settings"))));

        panel.Children.Add(HelpStep(
            "Step 4",
            "Load the model",
            "Open Overview, choose the model from the dropdown at the top, then click Load. Loaded model sessions stay available on their saved per-model ports, so more than one model can serve at the same time when the hardware has room.",
            ("Open Overview", (_, _) => NavigateFromHelp("overview-load"))));

        panel.Children.Add(HelpStep(
            "Step 5",
            "Add the model to OpenCode",
            "Open OpenCode, choose Add New in the OpenCode Models dropdown, select the local model in the second dropdown, then click Add. Each saved model keeps its own endpoint.",
            ("Open OpenCode", (_, _) => NavigateFromHelp("opencode"))));

        PageHost.Content = Scroll(panel, new Thickness(16));
    }

    private static Border HelpStep(string step, string title, string body, params (string Text, RoutedEventHandler Click)[] actions)
    {
        var container = new Border
        {
            Background = (System.Windows.Media.Brush)WpfApplication.Current.Resources["PanelBackAlt"],
            BorderBrush = (System.Windows.Media.Brush)WpfApplication.Current.Resources["PanelBorder"],
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(12, 10, 12, 10),
            Margin = new Thickness(0, 0, 0, 10)
        };

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(86) });
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        var badge = new TextBlock
        {
            Text = step,
            FontWeight = FontWeights.SemiBold,
            Foreground = (System.Windows.Media.Brush)WpfApplication.Current.Resources["Accent"],
            Margin = new Thickness(0, 2, 12, 0)
        };
        grid.Children.Add(badge);

        var stack = new StackPanel();
        var heading = Text(title, 15, true);
        heading.Margin = new Thickness(0, 0, 0, 4);
        stack.Children.Add(heading);
        var description = Text(body, 13, muted: true);
        description.Margin = new Thickness(0, 0, 0, 8);
        stack.Children.Add(description);

        if (actions.Length > 0)
        {
            var buttons = Bar();
            buttons.Margin = new Thickness(0);
            foreach (var action in actions)
                buttons.Children.Add(Button(action.Text, action.Click));
            stack.Children.Add(buttons);
        }

        Grid.SetColumn(stack, 1);
        grid.Children.Add(stack);
        container.Child = grid;
        return container;
    }

    private void NavigateFromHelp(string target)
    {
        _helpFocusTarget = target;
        switch (target)
        {
            case "runtime-download":
                ShowRuntimes();
                SetStatus("Help: in Runtime Downloads, choose the official Windows or WSL package for CUDA, CPU, Vulkan, or Intel Arc SYCL, then click Install.");
                break;
            case "model-download":
                ShowModels();
                _hfQueryBox?.Focus();
                SetStatus("Help: search Hugging Face, then click Download on the selected model row.");
                break;
            case "launch-settings":
                ShowModels();
                _modelsGrid?.Focus();
                SetStatus("Help: select a model, tune launch settings, then click Save For Model.");
                break;
            case "overview-load":
                ShowOverview();
                _overviewModelCombo?.Focus();
                SetStatus("Help: choose a model from the top dropdown, then click Load.");
                break;
            case "opencode":
                ShowOpenCode();
                _openCodeModelCombo?.Focus();
                SetStatus("Help: choose Add New in OpenCode models, select a local model, then click Add.");
                break;
        }
    }

    private void ApplyPendingHelpFocus()
    {
        if (_viewModel.CurrentPage == "Windows")
        {
            ClearWindowsHelpHighlights();
            return;
        }

        if (_viewModel.CurrentPage != "WSL Linux") return;
        ClearWslHelpHighlights();
    }

    private void ClearWslHelpHighlights()
    {
        foreach (var button in new[]
        {
            _wslInstallButton, _wslCheckUpdatesButton, _wslInstallUbuntuButton, _wslCheckUbuntuUpdatesButton,
            _wslInstallBuildToolsButton, _wslInstallCudaToolkitButton, _wslInstallVulkanToolsButton,
            _wslInstallSyclRuntimeButton, _wslInstallSyclOneApiButton
        })
        {
            if (button is not null) button.Tag = null;
        }
    }

    private void ClearWindowsHelpHighlights()
    {
        foreach (var button in new[]
        {
            _windowsInstallCpuToolsButton, _windowsInstallCudaToolkitButton, _windowsInstallVulkanToolsButton,
            _windowsInstallSyclToolsButton
        })
        {
            if (button is not null) button.Tag = null;
        }
    }

    private void HighlightFirstVisibleHelpButton(params WpfButton?[] buttons)
    {
        foreach (var button in buttons)
        {
            if (button is null || button.Visibility != Visibility.Visible) continue;
            HighlightHelpButton(button, focus: true);
            return;
        }
    }

    private static void HighlightHelpButton(WpfButton? button, bool focus)
    {
        if (button is null || button.Visibility != Visibility.Visible) return;
        button.Tag = "Active";
        if (!focus) return;
        button.Dispatcher.BeginInvoke(new Action(() =>
        {
            button.Focus();
            button.BringIntoView();
        }), System.Windows.Threading.DispatcherPriority.ContextIdle);
    }
}
