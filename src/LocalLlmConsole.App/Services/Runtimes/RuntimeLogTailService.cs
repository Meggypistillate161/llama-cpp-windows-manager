namespace LocalLlmConsole.Services;

public sealed record RuntimeLogTailRequest(
    string LogPath,
    bool IsRuntimeRunning,
    RuntimeSlotSnapshot? SlotSnapshot,
    int MaxCharacters = 16000);

public sealed record RuntimeLogTailResult(
    string Text,
    bool HasActiveLog);

public sealed class RuntimeLogTailService
{
    public RuntimeMtpTokenSnapshot? MtpTokenStats(string logPath, int maxCharacters = 16000)
    {
        if (string.IsNullOrWhiteSpace(logPath) || !File.Exists(logPath))
            return null;

        try
        {
            return RuntimeDashboardService.ParseMtpTokenStats(LogFileService.Tail(logPath, maxCharacters));
        }
        catch
        {
            return null;
        }
    }

    public RuntimeLogTailResult Build(RuntimeLogTailRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.LogPath) || !File.Exists(request.LogPath))
        {
            return new RuntimeLogTailResult(
                request.IsRuntimeRunning
                    ? "Runtime log file has not been created yet."
                    : "No runtime log is active.",
                HasActiveLog: false);
        }

        try
        {
            var heading = request.IsRuntimeRunning
                ? $"Live log: {request.LogPath}"
                : $"Last runtime log: {request.LogPath}";
            var slotStatus = SlotStatus(request.SlotSnapshot);
            var rawTail = LogFileService.Tail(request.LogPath, request.MaxCharacters);
            var logTail = LogFileService.CollapseIdleSlotNoise(rawTail);
            var text = string.IsNullOrWhiteSpace(slotStatus)
                ? $"{heading}{Environment.NewLine}{Environment.NewLine}{logTail}"
                : $"{heading}{Environment.NewLine}{slotStatus}{Environment.NewLine}{Environment.NewLine}{logTail}";
            return new RuntimeLogTailResult(text, HasActiveLog: true);
        }
        catch (Exception ex)
        {
            return new RuntimeLogTailResult(
                $"Could not read runtime log yet.{Environment.NewLine}{Environment.NewLine}{ex.Message}",
                HasActiveLog: false);
        }
    }

    private static string SlotStatus(RuntimeSlotSnapshot? slotSnapshot)
    {
        if (slotSnapshot is null) return "";
        if (!slotSnapshot.IsProcessing) return "Slot status: idle";

        var promptTotal = slotSnapshot.PromptTokens?.ToString("N0") ?? "?";
        return $"Slot status: processing | Prompt {slotSnapshot.PromptTokensProcessed:N0}/{promptTotal} | Gen {slotSnapshot.GeneratedTokens:N0}";
    }
}
