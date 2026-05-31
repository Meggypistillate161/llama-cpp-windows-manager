namespace LocalLlmConsole.Services;

public enum AppShutdownConfirmationKind
{
    RunningModels,
    ActiveDownloads
}

public sealed record AppShutdownConfirmationPrompt(
    AppShutdownConfirmationKind Kind,
    string Title,
    string Message);

public sealed record AppShutdownDecision(
    IReadOnlyList<AppShutdownConfirmationPrompt> Confirmations,
    string ClosingStatus);

public sealed class AppShutdownDecisionService
{
    public AppShutdownDecision Build(int runningModelSessions, int activeDownloads)
    {
        runningModelSessions = Math.Max(0, runningModelSessions);
        activeDownloads = Math.Max(0, activeDownloads);

        var confirmations = new List<AppShutdownConfirmationPrompt>();
        if (runningModelSessions > 0)
        {
            confirmations.Add(new AppShutdownConfirmationPrompt(
                AppShutdownConfirmationKind.RunningModels,
                "Models are running",
                $"{runningModelSessions} model session{(runningModelSessions == 1 ? " is" : "s are")} running.\n\nClosing the app will stop all loaded models and free their runtime resources.\n\nClose and stop loaded models?"));
        }

        if (activeDownloads > 0)
        {
            var downloadText = activeDownloads == 1
                ? "1 model download is"
                : $"{activeDownloads} model downloads are";
            confirmations.Add(new AppShutdownConfirmationPrompt(
                AppShutdownConfirmationKind.ActiveDownloads,
                "Downloads in progress",
                $"{downloadText} still running.\n\nClosing the app will pause active downloads and save the partial files so they can be resumed from History next time.\n\nClose and pause downloads?"));
        }

        var closingStatus = runningModelSessions > 0
            ? "Stopping runtimes and closing..."
            : activeDownloads > 0
                ? "Pausing active downloads and closing..."
                : "Closing...";
        return new AppShutdownDecision(confirmations, closingStatus);
    }
}
