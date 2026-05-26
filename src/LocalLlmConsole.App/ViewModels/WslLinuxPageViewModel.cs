using System.Collections.ObjectModel;

namespace LocalLlmConsole.ViewModels;

public sealed class WslLinuxPageViewModel
{
    public ObservableCollection<UiRow> Rows { get; } = new();

    public void ReplaceDistroRows(WslEnvironmentReport report, string selectedDistroName)
    {
        Rows.Clear();
        foreach (var distro in report.Distros.OrderByDescending(distro => distro.IsUbuntu).ThenBy(distro => distro.Name, StringComparer.OrdinalIgnoreCase))
        {
            var selected = distro.Name.Equals(selectedDistroName, StringComparison.OrdinalIgnoreCase);
            var notes = distro.IsUbuntu
                ? "Recommended for llama.cpp WSL builds."
                : "Selectable for existing WSL runtimes.";
            Rows.Add(new UiRow
            {
                C1 = distro.IsDefault ? "Yes" : "",
                C2 = distro.Name,
                C3 = distro.State,
                C4 = string.IsNullOrWhiteSpace(distro.Version) ? "" : $"WSL {distro.Version}",
                C5 = notes,
                C6 = selected ? "Selected" : "Use",
                T1 = selected ? "This distro is already selected." : "Use this Linux distro for WSL launches and builds.",
                B1 = !selected,
                Data = new JsonObject
                {
                    ["Name"] = distro.Name,
                    ["IsUbuntu"] = distro.IsUbuntu
                }
            });
        }

        if (report.Distros.Count == 0)
        {
            Rows.Add(new UiRow
            {
                C1 = "",
                C2 = "No Linux distro detected",
                C3 = report.WslExeFound ? "Missing" : "WSL missing",
                C4 = "",
                C5 = report.RecommendedAction,
                C6 = "",
                T1 = "Install WSL and Ubuntu before selecting a distro.",
                B1 = false
            });
        }
    }
}
