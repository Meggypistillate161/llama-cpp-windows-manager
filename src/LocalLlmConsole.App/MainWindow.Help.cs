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
    private const string HelpFocusWsl = "wsl";
    private const string HelpFocusUbuntu = "ubuntu";
    private const string HelpFocusTools = "tools";

    private void ShowHelp()
    {
        SetPage("Help", "First-run setup steps.");

        var panel = new StackPanel { Margin = new Thickness(0, 0, 10, 0) };
        panel.Children.Add(Text("First Steps", 22, true));
        panel.Children.Add(Text("Work through these in order. The buttons jump to the page where the action lives; setup and install actions still run from their own workflow page.", 13, muted: true));

        panel.Children.Add(HelpStep(
            "Step 1",
            "Install WSL",
            "Open WSL Linux and install Windows Subsystem for Linux. If WSL is already installed, use the update action when needed.",
            ("Open WSL Linux", (_, _) => NavigateFromHelp("wsl"))));

        panel.Children.Add(HelpStep(
            "Step 2",
            "Install Ubuntu",
            "Open WSL Linux and install the recommended Ubuntu distro. Ubuntu is the supported path for guided llama.cpp source builds.",
            ("Open WSL Linux", (_, _) => NavigateFromHelp("ubuntu"))));

        panel.Children.Add(HelpStep(
            "Step 3",
            "Install build tools for your system",
            "Open WSL Linux and install the tools required to build llama.cpp: use Install CPU Tools for CPU builds, Install CUDA for NVIDIA, or Install Vulkan for AMD/Vulkan builds.",
            ("Open WSL Linux", (_, _) => NavigateFromHelp("tools"))));

        panel.Children.Add(HelpStep(
            "Step 4",
            "Download llama.cpp source",
            "Open Runtimes and download the llama.cpp repository that matches the toolkit you plan to use. Prefer the official CPU, CUDA, or Vulkan preset for your hardware.",
            ("Open Runtimes", (_, _) => NavigateFromHelp("runtime-download"))));

        panel.Children.Add(HelpStep(
            "Step 5",
            "Build the runtime",
            "Open Runtimes and click Build for the downloaded llama.cpp source. The completed build is what the app uses to launch models.",
            ("Open Runtimes", (_, _) => NavigateFromHelp("runtime-build"))));

        panel.Children.Add(HelpStep(
            "Step 6",
            "Download a model",
            "Open Models, search Hugging Face, select the model file you want, and click Download on the row.",
            ("Open Models", (_, _) => NavigateFromHelp("model-download"))));

        panel.Children.Add(HelpStep(
            "Step 7",
            "Save model launch settings",
            "Downloaded models are registered automatically. Use Scan Models Folder only if you copied a model manually or the downloaded model does not appear. Select the model, adjust the launch settings on the right, then click Save For Model.",
            ("Open Models", (_, _) => NavigateFromHelp("launch-settings"))));

        panel.Children.Add(HelpStep(
            "Step 8",
            "Load the model",
            "Open Overview, choose the model from the dropdown at the top, then click Load.",
            ("Open Overview", (_, _) => NavigateFromHelp("overview-load"))));

        panel.Children.Add(HelpStep(
            "Step 9",
            "Add the model to OpenCode",
            "Open OpenCode, choose Add New in the OpenCode Models dropdown, select the local model in the second dropdown, then click Add.",
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
            case HelpFocusWsl:
                ShowWslLinux();
                SetStatus("Help: use the highlighted WSL action.");
                break;
            case HelpFocusUbuntu:
                ShowWslLinux();
                SetStatus("Help: use the highlighted Ubuntu action.");
                break;
            case HelpFocusTools:
                ShowWslLinux();
                SetStatus("Help: choose the highlighted CPU, CUDA, or Vulkan tool action for your hardware.");
                break;
            case "runtime-download":
                ShowRuntimes();
                SetStatus("Help: in Runtime Repositories, choose the official CPU, CUDA, or Vulkan preset and click Download.");
                break;
            case "runtime-build":
                ShowRuntimes();
                SetStatus("Help: in Installed Local Builds, click Build on a downloaded llama.cpp source row.");
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
        if (_viewModel.CurrentPage != "WSL Linux") return;
        ClearWslHelpHighlights();

        switch (_helpFocusTarget)
        {
            case HelpFocusWsl:
                HighlightFirstVisibleHelpButton(_wslInstallButton, _wslCheckUpdatesButton);
                break;
            case HelpFocusUbuntu:
                HighlightFirstVisibleHelpButton(_wslInstallUbuntuButton, _wslCheckUbuntuUpdatesButton);
                break;
            case HelpFocusTools:
                HighlightHelpButton(_wslInstallBuildToolsButton, focus: true);
                HighlightHelpButton(_wslInstallCudaToolkitButton, focus: false);
                HighlightHelpButton(_wslInstallVulkanToolsButton, focus: false);
                break;
        }
    }

    private void ClearWslHelpHighlights()
    {
        foreach (var button in new[]
        {
            _wslInstallButton, _wslCheckUpdatesButton, _wslInstallUbuntuButton, _wslCheckUbuntuUpdatesButton,
            _wslInstallBuildToolsButton, _wslInstallCudaToolkitButton, _wslInstallVulkanToolsButton
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
