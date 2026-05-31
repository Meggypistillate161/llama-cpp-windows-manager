namespace LocalLlmConsole.Services;

public sealed class UiAsyncRefreshTimerController
{
    private readonly IUiTimerFactory _timerFactory;
    private IUiTimer? _timer;

    public UiAsyncRefreshTimerController(IUiTimerFactory timerFactory)
    {
        _timerFactory = timerFactory ?? throw new ArgumentNullException(nameof(timerFactory));
    }

    public bool IsRunning => _timer is not null;

    public void Start(
        TimeSpan interval,
        Func<Task> refreshAsync,
        Action<Exception> onError)
    {
        ArgumentNullException.ThrowIfNull(refreshAsync);
        ArgumentNullException.ThrowIfNull(onError);

        Stop();
        _timer = _timerFactory.Create(interval);
        _timer.Tick += async (_, _) => await RunOnceAsync(refreshAsync, onError);
        _timer.Start();
    }

    public void Stop()
    {
        _timer?.Stop();
        _timer = null;
    }

    public static async Task RunOnceAsync(
        Func<Task> refreshAsync,
        Action<Exception> onError)
    {
        ArgumentNullException.ThrowIfNull(refreshAsync);
        ArgumentNullException.ThrowIfNull(onError);

        try
        {
            await refreshAsync();
        }
        catch (Exception ex)
        {
            onError(ex);
        }
    }
}
