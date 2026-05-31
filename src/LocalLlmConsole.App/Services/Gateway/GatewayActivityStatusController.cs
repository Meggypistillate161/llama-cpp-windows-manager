namespace LocalLlmConsole.Services;

public sealed class GatewayActivityStatusController
{
    private readonly GatewayActivityStatusTracker _tracker;
    private readonly IUiTimerFactory _timerFactory;
    private IUiTimer? _activityTimer;

    public GatewayActivityStatusController(
        GatewayActivityStatusTracker tracker,
        IUiTimerFactory timerFactory)
    {
        _tracker = tracker ?? throw new ArgumentNullException(nameof(tracker));
        _timerFactory = timerFactory ?? throw new ArgumentNullException(nameof(timerFactory));
    }

    public bool HasActivityTimer => _activityTimer is not null;

    public void Start(ModelRecord model, string phase, DateTimeOffset now, Action tick)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(tick);

        StopTimer();
        _tracker.Start(model, phase, now);
        _activityTimer = _timerFactory.Create(TimeSpan.FromSeconds(1));
        _activityTimer.Tick += (_, _) => tick();
        _activityTimer.Start();
        tick();
    }

    public void SetPhase(string phase, Action tick)
    {
        ArgumentNullException.ThrowIfNull(tick);

        _tracker.SetPhase(phase);
        tick();
    }

    public void Complete(Action tick)
    {
        ArgumentNullException.ThrowIfNull(tick);

        StopTimer();
        _tracker.Complete();
        tick();
    }

    public void Fail(string message, Action tick)
    {
        ArgumentNullException.ThrowIfNull(tick);

        StopTimer();
        _tracker.Fail(message);
        tick();
    }

    public GatewayActivityStatusSnapshot Build(
        AppSettings settings,
        bool gatewayListening,
        DateTimeOffset now)
        => _tracker.Build(settings, gatewayListening, now);

    private void StopTimer()
    {
        _activityTimer?.Stop();
        _activityTimer = null;
    }
}
