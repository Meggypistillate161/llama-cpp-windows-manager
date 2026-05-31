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
    private void TitleBar_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            ToggleWindowState();
            return;
        }

        if (e.ButtonState != System.Windows.Input.MouseButtonState.Pressed) return;
        try
        {
            DragMove();
        }
        catch (InvalidOperationException)
        {
            // DragMove can throw if Windows has already ended the mouse operation.
        }
    }

    private void MinimizeButton_Click(object sender, RoutedEventArgs e)
    {
        var plan = _coreServices.Ui.TrayWindowState.BuildMinimizePlan(_settings.MinimizeBehavior);
        if (plan.Action == TrayMinimizeAction.TrayOnly)
        {
            MinimizeToTray();
            return;
        }

        if (plan.Action == TrayMinimizeAction.TrayAndTaskbar)
            ShowTrayIcon();
        else
            HideTrayIcon();
        if (!string.IsNullOrWhiteSpace(plan.StatusMessage))
            SetStatus(plan.StatusMessage);
        WindowState = WindowState.Minimized;
    }

    private void Window_StateChanged(object? sender, EventArgs e)
    {
        ApplyWindowWorkAreaBounds();
        var action = _coreServices.Ui.TrayWindowState.WindowStateChangedAction(WindowState, _settings.MinimizeBehavior);
        if (action == TrayMinimizeAction.TrayOnly)
        {
            MinimizeToTray();
            return;
        }

        if (action == TrayMinimizeAction.TrayAndTaskbar)
        {
            ShowTrayIcon();
            return;
        }

        if (WindowState != WindowState.Minimized)
            HideTrayIcon();
    }

    private void ApplyWindowWorkAreaBounds()
    {
        if (WindowState != WindowState.Maximized)
        {
            MaxWidth = double.PositiveInfinity;
            MaxHeight = double.PositiveInfinity;
            return;
        }

        var handle = new System.Windows.Interop.WindowInteropHelper(this).Handle;
        var workingArea = Forms.Screen.FromHandle(handle).WorkingArea;
        var source = PresentationSource.FromVisual(this);
        if (source?.CompositionTarget is null)
        {
            MaxWidth = workingArea.Width;
            MaxHeight = workingArea.Height;
            return;
        }

        var topLeft = source.CompositionTarget.TransformFromDevice.Transform(new System.Windows.Point(workingArea.Left, workingArea.Top));
        var bottomRight = source.CompositionTarget.TransformFromDevice.Transform(new System.Windows.Point(workingArea.Right, workingArea.Bottom));
        MaxWidth = Math.Max(MinWidth, bottomRight.X - topLeft.X);
        MaxHeight = Math.Max(MinHeight, bottomRight.Y - topLeft.Y);
    }

    private void MaximizeButton_Click(object sender, RoutedEventArgs e) => ToggleWindowState();
    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

    private void ToggleWindowState()
    {
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    }

    private void InitializeTrayIcon()
    {
        var menu = new Forms.ContextMenuStrip();
        var showItem = new Forms.ToolStripMenuItem("Show llama.cpp Windows Manager");
        showItem.Click += (_, _) => Dispatcher.Invoke(RestoreFromTray);
        var exitItem = new Forms.ToolStripMenuItem("Exit");
        exitItem.Click += (_, _) => Dispatcher.Invoke(() =>
        {
            RestoreFromTray();
            Close();
        });
        menu.Items.Add(showItem);
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add(exitItem);

        _trayIcon = new Forms.NotifyIcon
        {
            Icon = CreateTrayIcon(),
            Text = AppDisplayName,
            ContextMenuStrip = menu,
            Visible = false
        };
        _trayIcon.DoubleClick += (_, _) => Dispatcher.Invoke(RestoreFromTray);
    }

    private static System.Drawing.Icon CreateTrayIcon()
    {
        try
        {
            var executable = Process.GetCurrentProcess().MainModule?.FileName;
            if (!string.IsNullOrWhiteSpace(executable))
            {
                var icon = System.Drawing.Icon.ExtractAssociatedIcon(executable);
                if (icon is not null) return icon;
            }
        }
        catch
        {
        }

        return System.Drawing.SystemIcons.Application;
    }

    private void ShowTrayIcon()
    {
        if (_trayIcon is null) InitializeTrayIcon();
        if (_trayIcon is not null) _trayIcon.Visible = true;
    }

    private void HideTrayIcon()
    {
        if (_trayIcon is not null) _trayIcon.Visible = false;
    }

    private void MinimizeToTray()
    {
        var plan = _coreServices.Ui.TrayWindowState.BeginHideToTray(WindowState);
        if (!plan.ShouldApply) return;
        try
        {
            ShowTrayIcon();
            ShowInTaskbar = false;
            Hide();

            if (plan.ShouldShowHint)
                _trayIcon?.ShowBalloonTip(1800, AppDisplayName, "Still running in the tray. Double-click the tray icon to restore.", Forms.ToolTipIcon.Info);

            SetStatus(plan.StatusMessage);
        }
        finally
        {
            _coreServices.Ui.TrayWindowState.CompleteHideToTray();
        }
    }

    private void RestoreFromTray()
    {
        var plan = _coreServices.Ui.TrayWindowState.BuildRestorePlan();
        HideTrayIcon();
        ShowInTaskbar = true;
        Show();
        WindowState = plan.RestoreState;
        Activate();
    }

    private void DisposeTrayIcon()
    {
        if (_trayIcon is null) return;
        try
        {
            _trayIcon.Visible = false;
            _trayIcon.Dispose();
        }
        catch { }
        _trayIcon = null;
    }
}
