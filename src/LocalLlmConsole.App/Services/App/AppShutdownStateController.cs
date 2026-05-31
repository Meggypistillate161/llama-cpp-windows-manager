namespace LocalLlmConsole.Services;

public enum AppShutdownCloseAdmission
{
    AllowClose,
    CancelAlreadyInProgress,
    CancelAndStartCleanup
}

public sealed class AppShutdownStateController
{
    public bool ShutdownRequested { get; private set; }

    public bool CleanupComplete { get; private set; }

    public AppShutdownCloseAdmission BeginClosing()
    {
        if (CleanupComplete) return AppShutdownCloseAdmission.AllowClose;
        if (ShutdownRequested) return AppShutdownCloseAdmission.CancelAlreadyInProgress;

        ShutdownRequested = true;
        return AppShutdownCloseAdmission.CancelAndStartCleanup;
    }

    public void ResetRequest()
        => ShutdownRequested = false;

    public void MarkCleanupComplete()
    {
        CleanupComplete = true;
        ShutdownRequested = false;
    }
}
