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
    private void StartRuntimeDashboardRefreshTimer()
    {
        _runtimeDashboardTimer ??= new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _runtimeDashboardTimer.Interval = TimeSpan.FromSeconds(1);
        _runtimeDashboardTimer.Tick -= RuntimeDashboardTimer_Tick;
        _runtimeDashboardTimer.Tick += RuntimeDashboardTimer_Tick;
        _runtimeDashboardTimer.Start();
    }

    private void StopRuntimeDashboardRefreshTimer()
    {
        _runtimeDashboardTimer?.Stop();
    }

    private async void RuntimeDashboardTimer_Tick(object? sender, EventArgs e)
    {
        if (_viewModel.CurrentPage != "Overview" && !_sessions.HasRunningSessions) return;
        try
        {
            await RefreshRuntimeMetricsAsync();
        }
        catch (Exception ex)
        {
            SetStatus($"Runtime refresh failed: {ex.Message}");
        }
    }

    private async Task DeleteSelectedRuntimeAsync()
    {
        var runtime = SelectedRuntime();
        if (runtime is null || _stateStore is null) return;
        if (ThemedMessageBox.Show(this, $"Remove runtime registration?\n\n{runtime.Name}", "Remove runtime", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
        await _stateStore.DeleteRuntimeAsync(runtime.Id);
        await RefreshRuntimesAsync();
        await RefreshOverviewAsync();
    }

    private async void DownloadRuntimePresetRow_Click(object sender, RoutedEventArgs e)
    {
        await RunEventAsync(async () =>
        {
            if ((sender as FrameworkElement)?.Tag is RuntimeBuildPresetRow { IsCustomAdd: true } row)
            {
                await AddCustomRuntimeRepositoryFromRowAsync(row);
                return;
            }

            var preset = RuntimeBuildPresetFromRowButton(sender);
            if (preset is not null) await DownloadRuntimeSourceAsync(preset);
        });
    }

    private async void InstallRuntimePackageRow_Click(object sender, RoutedEventArgs e)
    {
        await RunEventAsync(async () =>
        {
            var preset = RuntimePackagePresetFromRowButton(sender);
            if (preset is not null) await InstallRuntimePackageAsync(preset);
        });
    }

    private async void CheckRuntimePackageUpdateRow_Click(object sender, RoutedEventArgs e)
    {
        await RunEventAsync(async () =>
        {
            var row = (sender as FrameworkElement)?.Tag as RuntimePackagePresetRow;
            var preset = RuntimePackagePresetFromRowButton(sender);
            if (preset is not null) await CheckRuntimePackageUpdateAsync(preset, row);
        });
    }

    private async void DeleteRuntimePackageRow_Click(object sender, RoutedEventArgs e)
    {
        await RunEventAsync(async () =>
        {
            var preset = RuntimePackagePresetFromRowButton(sender);
            if (preset is not null) await DeleteRuntimePackageBuildsAsync(preset);
        });
    }

    private async void CheckRuntimePresetUpdateRow_Click(object sender, RoutedEventArgs e)
    {
        await RunEventAsync(async () =>
        {
            var row = (sender as FrameworkElement)?.Tag as RuntimeBuildPresetRow;
            var preset = RuntimeBuildPresetFromRowButton(sender);
            if (preset is not null) await CheckRuntimePresetUpdateAsync(preset, row);
        });
    }

    private async void DeleteRuntimePresetRow_Click(object sender, RoutedEventArgs e)
    {
        await RunEventAsync(async () =>
        {
            var preset = RuntimeBuildPresetFromRowButton(sender);
            if (preset is not null) await DeleteAllRuntimePresetBuildsAsync(preset);
        });
    }

    private async void BuildRuntimeRow_Click(object sender, RoutedEventArgs e)
    {
        await RunEventAsync(async () =>
        {
            var source = RuntimeSourceFromRowButton(sender);
            if (source is not null) await BuildRuntimeSourceAsync(source);
        });
    }

    private async void DeleteRuntimeRow_Click(object sender, RoutedEventArgs e)
    {
        await RunEventAsync(async () =>
        {
            var source = RuntimeSourceFromRowButton(sender);
            if (source is not null)
            {
                await DeleteRuntimeSourceAsync(source);
                return;
            }

            var runtime = RuntimeFromRowButton(sender);
            if (runtime is not null) await DeleteRuntimeBuildAsync(runtime);
        });
    }

    private void RuntimeGrid_PreviewMouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (FindParent<WpfButton>(e.OriginalSource as DependencyObject) is not null) return;
        var row = FindParent<DataGridRow>(e.OriginalSource as DependencyObject);
        if (row?.IsSelected == true && _runtimeGrid is not null)
        {
            _runtimeGrid.SelectedItem = null;
            e.Handled = true;
        }
    }

}
