namespace LocalLlmConsole.Services;

public sealed class SettingsPageDefinitionService
{
    public IReadOnlyList<SettingRowDefinition> BuildRows(AppSettings settings) =>
    [
        new("Storage", "Cache", "cache", $"{DisplayFormatService.BytesOrZero(CacheMaintenanceService.Size(settings.CacheRoot))} in {settings.CacheRoot}", "readonly", Action: "Clear",
            ToolTip: "Disposable app cache used for downloads, runtime packages, source archives, and temporary build files. Clear only removes safe cache contents while no cache job is running."),
        new("Window", "Minimize behavior", "minimizeBehavior", AppPreferenceService.MinimizeBehaviorLabel(settings.MinimizeBehavior), "choice", AppPreferenceService.MinimizeBehaviorOptions(),
            ToolTip: "Controls where the app goes when minimized: taskbar only, tray only, or both tray and taskbar."),
        new("Window", "Start with Windows", "startWithWindows", AppPreferenceService.YesNoLabel(settings.StartWithWindows), "choice", AppPreferenceService.YesNoOptions(),
            ToolTip: "Registers or removes this app from the current user's Windows startup apps."),
        new("OpenCode", "Sync on launch save", "autoSaveOpenCodeOnLaunchSettingsSave", AppPreferenceService.YesNoLabel(settings.AutoSaveOpenCodeOnLaunchSettingsSave), "choice", AppPreferenceService.YesNoOptions(),
            ToolTip: "Automatically rewrites OpenCode local model entries after saved model launch settings or saved variants change."),
        new("Model", "Auto unload idle min", "autoUnloadIdleMinutes", settings.AutoUnloadIdleMinutes.ToString(CultureInfo.InvariantCulture),
            ToolTip: "Whole number of idle minutes before quiet loaded models are stopped automatically. Use 0 to disable idle auto-unload."),
        new("Runtime", "Delete source after build", "deleteRuntimeSourceAfterSuccessfulBuild", AppPreferenceService.YesNoLabel(settings.DeleteRuntimeSourceAfterSuccessfulBuild), "choice", AppPreferenceService.YesNoOptions(),
            ToolTip: "Deletes downloaded source trees after successful source builds. Official prebuilt runtimes are not affected."),
        new("Network", "LAN exposure", "modelAccessMode", AppPreferenceService.ModelAccessModeLabel(settings.ModelAccessMode), "choice", AppPreferenceService.ModelAccessModeOptions(),
            ToolTip: "Controls which serving endpoints accept trusted LAN clients: gateway only, direct model ports only, both, or local-only loopback."),
        new("Network", "Auto-load gateway", "autoLoadGatewayEnabled", AppPreferenceService.YesNoLabel(settings.AutoLoadGatewayEnabled), "choice", AppPreferenceService.YesNoOptions(),
            ToolTip: "Enables one shared OpenAI-compatible /v1 endpoint for clients like OpenCode. Requests still load each model on its own direct runtime port, then the gateway proxies to that port."),
        new("Network", "Gateway port", "autoLoadGatewayPort", settings.AutoLoadGatewayPort.ToString(CultureInfo.InvariantCulture),
            ToolTip: "Whole number from 1 to 65535 for the shared auto-load gateway. Keep this separate from direct model ports."),
        new("Network", "Gateway policy", "autoLoadGatewayPolicy", AppPreferenceService.GatewaySwapPolicyLabel(settings.AutoLoadGatewayPolicy), "choice", AppPreferenceService.GatewaySwapPolicyOptions(),
            ToolTip: "Prefer keeping loaded models leaves existing sessions running. Single active model unloads other models before loading the requested model to free VRAM."),
        new("Network", "API key", "modelApiKey", settings.ModelApiKey, "secret", Action: "Generate",
            ToolTip: "Bearer key required by local OpenAI-compatible endpoints. Must be at least 32 non-whitespace characters; leaving it blank generates a new key on save."),
        new("Logs", "Max log file MB", "maxLogFileSizeMb", settings.MaxLogFileSizeMb.ToString(CultureInfo.InvariantCulture),
            ToolTip: "Whole number from 1 to 4096. Limits each app, runtime, or job log file before rotation or trimming.")
    ];
}
