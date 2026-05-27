namespace LocalLlmConsole.Services;

public static class AppPreferenceService
{
    public static readonly string[] MinimizeBehaviorOptionLabels = ["Taskbar only", "Tray only", "Tray + taskbar"];
    public static readonly string[] ModelAccessModeOptionLabels = ["Local only", "LAN access"];
    public static readonly string[] YesNoOptionLabels = ["Yes", "No"];

    public static string ThemeMode(string text)
    {
        var value = (text ?? "").Trim().ToLowerInvariant();
        return value is "light" or "dark" or "system" ? value : "system";
    }

    public static string MinimizeBehavior(string text)
    {
        var value = (text ?? "").Trim()
            .Replace("-", "", StringComparison.OrdinalIgnoreCase)
            .Replace("_", "", StringComparison.OrdinalIgnoreCase)
            .Replace(" ", "", StringComparison.OrdinalIgnoreCase)
            .ToLowerInvariant();
        return value switch
        {
            "tray" or "trayonly" => "trayOnly",
            "traytaskbar" or "trayandtaskbar" or "trayplustaskbar" or "tray+taskbar" or "traywhenrunning" or "running" => "trayAndTaskbar",
            _ => "taskbarOnly"
        };
    }

    public static string MinimizeBehaviorLabel(string text) => MinimizeBehavior(text) switch
    {
        "trayOnly" => "Tray only",
        "trayAndTaskbar" => "Tray + taskbar",
        _ => "Taskbar only"
    };

    public static IEnumerable<string> MinimizeBehaviorOptions() => MinimizeBehaviorOptionLabels;

    public static string ModelAccessMode(string text)
    {
        var value = (text ?? "").Trim()
            .Replace("-", "", StringComparison.OrdinalIgnoreCase)
            .Replace("_", "", StringComparison.OrdinalIgnoreCase)
            .Replace(" ", "", StringComparison.OrdinalIgnoreCase)
            .ToLowerInvariant();
        return value is "lan" or "lanaccess" or "network" or "networkaccess" ? "lan" : "local";
    }

    public static string ModelAccessModeLabel(string text)
        => ModelAccessMode(text) == "lan" ? "LAN access" : "Local only";

    public static IEnumerable<string> ModelAccessModeOptions() => ModelAccessModeOptionLabels;

    public static string YesNoLabel(bool value) => value ? "Yes" : "No";

    public static bool YesNoValue(string text, bool fallback)
    {
        var value = (text ?? "").Trim().ToLowerInvariant();
        return value switch
        {
            "yes" or "true" or "1" or "on" => true,
            "no" or "false" or "0" or "off" => false,
            _ => fallback
        };
    }

    public static IEnumerable<string> YesNoOptions() => YesNoOptionLabels;

    public static string RuntimeHostForAccessMode(string accessMode)
        => ModelAccessMode(accessMode) == "lan" ? "0.0.0.0" : "127.0.0.1";

    public static bool TryIntValue(string text, out int value)
        => int.TryParse((text ?? "").Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out value);

    public static int ClampedIntValue(string text, int fallback, int min, int max)
        => Math.Clamp(TryIntValue(text, out var value) ? value : fallback, min, max);
}
