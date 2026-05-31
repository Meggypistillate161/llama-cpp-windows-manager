using LocalLlmConsole.Models;

namespace LocalLlmConsole.Services;

public enum WslDistroSelectionApplicationOutcome
{
    Ignored,
    Applied
}

public sealed record WslDistroSelectionApplicationResult(
    WslDistroSelectionApplicationOutcome Outcome,
    AppSettings Settings,
    string DistroName,
    string StatusMessage);

public sealed record WslDistroSelectionApplicationActions(
    Func<AppSettings, Task<AppSettings>> PersistSettingsAsync,
    Func<Task> RefreshWslLinuxAsync,
    Action<string> SetStatus);

public sealed class WslDistroSelectionApplicationService
{
    public async Task<WslDistroSelectionApplicationResult> SelectAsync(
        AppSettings settings,
        UiRow? row,
        WslDistroSelectionApplicationActions actions)
    {
        ArgumentNullException.ThrowIfNull(settings);
        Validate(actions);

        var distro = DistroName(row);
        if (string.IsNullOrWhiteSpace(distro))
            return new WslDistroSelectionApplicationResult(WslDistroSelectionApplicationOutcome.Ignored, settings, "", "");

        var updated = settings with { WslDistro = distro };
        var persisted = await actions.PersistSettingsAsync(updated);
        await actions.RefreshWslLinuxAsync();

        var status = $"WSL distro set to {distro}.";
        actions.SetStatus(status);
        return new WslDistroSelectionApplicationResult(WslDistroSelectionApplicationOutcome.Applied, persisted, distro, status);
    }

    public static string DistroName(UiRow? row)
        => row?.Data["Name"]?.ToString() ?? row?.C2 ?? "";

    public static string PreferredUbuntuDistroName(
        UiRow? selectedRow,
        IEnumerable<UiRow> rows,
        string configuredDistro)
    {
        ArgumentNullException.ThrowIfNull(rows);

        if (IsUbuntuDistroRow(selectedRow))
            return DistroName(selectedRow);

        var configured = rows.FirstOrDefault(row =>
            string.Equals(DistroName(row), configuredDistro, StringComparison.OrdinalIgnoreCase)
            && IsUbuntuDistroRow(row));
        if (configured is not null)
            return DistroName(configured);

        var ubuntu = rows.FirstOrDefault(IsUbuntuDistroRow);
        return ubuntu is null ? "" : DistroName(ubuntu);
    }

    private static bool IsUbuntuDistroRow(UiRow? row)
    {
        if (row is null) return false;
        var name = DistroName(row);
        return row.Data["IsUbuntu"]?.GetValue<bool?>() ?? name.Contains("ubuntu", StringComparison.OrdinalIgnoreCase);
    }

    private static void Validate(WslDistroSelectionApplicationActions actions)
    {
        ArgumentNullException.ThrowIfNull(actions);
        ArgumentNullException.ThrowIfNull(actions.PersistSettingsAsync);
        ArgumentNullException.ThrowIfNull(actions.RefreshWslLinuxAsync);
        ArgumentNullException.ThrowIfNull(actions.SetStatus);
    }
}
