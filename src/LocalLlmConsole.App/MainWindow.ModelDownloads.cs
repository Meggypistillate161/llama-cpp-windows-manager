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
    private async Task SearchHuggingFaceAsync()
    {
        var query = _hfQueryBox?.Text.Trim() ?? "";
        await RunAsync("Searching Hugging Face...", async () =>
        {
            ConfigureHfSearchGrid();
            var installed = await InstalledHuggingFaceInventoryAsync();
            _viewModel.HuggingFace.ReplaceSearchResults(await _huggingFace!.SearchAsync(query), installed, _settings.ModelsRoot);
        });
    }

    private async Task DownloadSelectedHfAsync()
    {
        if (_hfShowingDownloadHistory)
        {
            await ResumeSelectedDownloadAsync();
            return;
        }

        if (_hfGrid?.SelectedItem is not UiRow row) return;
        var file = row.Data.Deserialize<HuggingFaceFile>()!;
        await StartHuggingFaceDownloadAsync(file);
    }

    private async Task StartHuggingFaceDownloadAsync(HuggingFaceFile file)
    {
        await RunAsync("Starting download...", async () =>
        {
            var job = await _huggingFace!.StartDownloadAsync(file, _settings);
            await RefreshJobsAsync();
            await RefreshOverviewAsync();
            await ShowDownloadHistoryAsync(job.Id);
            RunBackground(() => MonitorDownloadAsync(job.Id), "Download monitor failed");
            SetStatus($"Download started: {file.Name} ({job.Id})");
        });
    }

    private async void DownloadHfRow_Click(object sender, RoutedEventArgs e)
    {
        await RunEventAsync(async () =>
        {
            if ((sender as FrameworkElement)?.Tag is not UiRow row || !row.B1) return;
            var file = row.Data.Deserialize<HuggingFaceFile>();
            if (file is not null) await StartHuggingFaceDownloadAsync(file);
        });
    }

    private void OpenHuggingFaceModelCardRow_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not UiRow row) return;
        OpenHuggingFaceModelCard(HuggingFaceRepoFromSearchRow(row));
    }

    private void OpenHuggingFaceModelCard(string repo)
    {
        if (!HuggingFaceService.TryCreateModelCardUrl(repo, out var url))
        {
            SetStatus("The selected row does not contain a valid Hugging Face repository.");
            return;
        }

        Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        SetStatus($"Opened Hugging Face model card: {repo}");
    }

    private static string HuggingFaceRepoFromSearchRow(UiRow row)
    {
        try { return row.Data.Deserialize<HuggingFaceFile>()?.Repo ?? row.C1; }
        catch { return row.C1; }
    }
}
