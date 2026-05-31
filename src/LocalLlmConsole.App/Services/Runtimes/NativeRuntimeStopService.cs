namespace LocalLlmConsole.Services;

public sealed record NativeRuntimeStopResult(
    bool StopRequested,
    bool ExitedAfterPrimaryKill,
    bool ExitedAfterVerificationKill)
{
    public bool Exited => ExitedAfterPrimaryKill || ExitedAfterVerificationKill;
}

public sealed class NativeRuntimeStopService
{
    private const int PrimaryExitWaitMilliseconds = 3000;
    private const int VerificationExitWaitMilliseconds = 1000;

    public NativeRuntimeStopResult Stop(Process? process)
    {
        if (process is null)
            return new NativeRuntimeStopResult(false, true, true);

        if (HasExited(process))
            return new NativeRuntimeStopResult(false, true, true);

        var processId = TryGetProcessId(process);
        var startTime = TryGetStartTime(process);
        var exitedAfterPrimaryKill = KillAndWait(process, PrimaryExitWaitMilliseconds);
        var exitedAfterVerificationKill = exitedAfterPrimaryKill
            || KillVerifiedProcessById(processId, startTime);

        return new NativeRuntimeStopResult(
            true,
            exitedAfterPrimaryKill,
            exitedAfterVerificationKill);
    }

    private static bool KillAndWait(Process process, int timeoutMilliseconds)
    {
        try
        {
            if (process.HasExited)
                return true;

            process.Kill(entireProcessTree: true);
            return process.WaitForExit(timeoutMilliseconds) || HasExited(process);
        }
        catch
        {
            return HasExited(process);
        }
    }

    private static bool KillVerifiedProcessById(int processId, DateTime? expectedStartTime)
    {
        if (processId <= 0)
            return false;

        try
        {
            using var process = Process.GetProcessById(processId);
            if (process.HasExited)
                return true;

            var actualStartTime = TryGetStartTime(process);
            if (expectedStartTime is not null
                && actualStartTime is not null
                && actualStartTime.Value != expectedStartTime.Value)
                return true;

            process.Kill(entireProcessTree: true);
            return process.WaitForExit(VerificationExitWaitMilliseconds) || HasExited(process);
        }
        catch (ArgumentException)
        {
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool HasExited(Process process)
    {
        try { return process.HasExited; }
        catch { return true; }
    }

    private static int TryGetProcessId(Process process)
    {
        try { return process.Id; }
        catch { return 0; }
    }

    private static DateTime? TryGetStartTime(Process process)
    {
        try { return process.StartTime; }
        catch { return null; }
    }
}
