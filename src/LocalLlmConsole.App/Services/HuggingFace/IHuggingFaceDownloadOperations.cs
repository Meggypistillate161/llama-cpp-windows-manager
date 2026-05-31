namespace LocalLlmConsole.Services;

public interface IHuggingFaceDownloadOperations
{
    Task ResumeDownloadAsync(JobRecord job, AppSettings settings);
    Task PauseDownloadAsync(JobRecord job);
    Task StopDownloadAsync(JobRecord job);
    bool IsDownloadActive(string jobId);
}
