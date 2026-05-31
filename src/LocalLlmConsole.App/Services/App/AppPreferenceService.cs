namespace LocalLlmConsole.Services;

public static class AppPreferenceService
{
    public static readonly string[] MinimizeBehaviorOptionLabels = ["Taskbar only", "Tray only", "Tray + taskbar"];
    public static readonly string[] ModelAccessModeOptionLabels =
    [
        "Local only",
        "Gateway LAN only",
        "Direct models LAN only",
        "Gateway + direct LAN"
    ];
    public static readonly string[] GatewaySwapPolicyOptionLabels = ["Prefer keeping loaded models", "Single active model"];
    public static readonly string[] CudaPackagePreferenceOptionLabels = ["Latest", "Compatibility"];
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
            .Replace("+", "", StringComparison.OrdinalIgnoreCase)
            .Replace(" ", "", StringComparison.OrdinalIgnoreCase)
            .ToLowerInvariant();
        return value switch
        {
            "gateway" or "gatewaylan" or "gatewayonly" or "gatewaylanonly" or "router" or "routerlan" or "routeronly" or "routerlanonly" => "gateway",
            "models" or "modellan" or "modelsnetwork" or "modelsaccess" or "modelsnetworkaccess" or "direct" or "directlan" or "directonly" or "directlanonly" or "directmodels" or "directmodelslan" or "directmodelsonly" or "directmodelslanonly" => "models",
            "both" or "all" or "gatewaydirect" or "gatewaydirectlan" or "routerdirect" or "routerdirectlan" or "gatewaymodels" or "gatewaymodelslan" => "both",
            "lan" or "lanaccess" or "network" or "networkaccess" => "both",
            _ => "local"
        };
    }

    public static string ModelAccessModeLabel(string text) => ModelAccessMode(text) switch
    {
        "gateway" => "Gateway LAN only",
        "models" => "Direct models LAN only",
        "both" => "Gateway + direct LAN",
        _ => "Local only"
    };

    public static IEnumerable<string> ModelAccessModeOptions() => ModelAccessModeOptionLabels;

    public static bool GatewayAllowsLanAccess(string text)
    {
        var mode = ModelAccessMode(text);
        return mode is "gateway" or "both";
    }

    public static bool DirectModelsAllowLanAccess(string text)
    {
        var mode = ModelAccessMode(text);
        return mode is "models" or "both";
    }

    public static string GatewaySwapPolicy(string text)
    {
        var value = (text ?? "").Trim()
            .Replace("-", "", StringComparison.OrdinalIgnoreCase)
            .Replace("_", "", StringComparison.OrdinalIgnoreCase)
            .Replace(" ", "", StringComparison.OrdinalIgnoreCase)
            .ToLowerInvariant();
        return value is "single" or "singleactive" or "singleactivemodel" or "swap" or "swaponrequest"
            ? "singleActive"
            : "keepLoaded";
    }

    public static string GatewaySwapPolicyLabel(string text)
        => GatewaySwapPolicy(text) == "singleActive" ? "Single active model" : "Prefer keeping loaded models";

    public static IEnumerable<string> GatewaySwapPolicyOptions() => GatewaySwapPolicyOptionLabels;

    public static string CudaPackagePreference(string text)
    {
        var value = (text ?? "").Trim()
            .Replace("-", "", StringComparison.OrdinalIgnoreCase)
            .Replace("_", "", StringComparison.OrdinalIgnoreCase)
            .Replace(" ", "", StringComparison.OrdinalIgnoreCase)
            .ToLowerInvariant();
        return value is "compatibility" or "compatible" or "cuda12" or "cuda12compatibility" ? "compatibility" : "latest";
    }

    public static string CudaPackagePreferenceLabel(string text)
        => CudaPackagePreference(text) == "compatibility" ? "Compatibility" : "Latest";

    public static IEnumerable<string> CudaPackagePreferenceOptions() => CudaPackagePreferenceOptionLabels;

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
        => DirectModelsAllowLanAccess(accessMode) ? "0.0.0.0" : "127.0.0.1";

    public static bool TryIntValue(string text, out int value)
        => int.TryParse((text ?? "").Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out value);

    public static int ClampedIntValue(string text, int fallback, int min, int max)
        => Math.Clamp(TryIntValue(text, out var value) ? value : fallback, min, max);
}
