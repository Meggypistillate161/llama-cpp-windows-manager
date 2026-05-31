namespace LocalLlmConsole.Services;

public interface IUiTimer
{
    event EventHandler? Tick;

    void Start();

    void Stop();
}

public interface IUiTimerFactory
{
    IUiTimer Create(TimeSpan interval);
}

public sealed class DispatcherUiTimerFactory : IUiTimerFactory
{
    public IUiTimer Create(TimeSpan interval)
        => new DispatcherUiTimer(interval);

    private sealed class DispatcherUiTimer : IUiTimer
    {
        private readonly System.Windows.Threading.DispatcherTimer _timer;

        public DispatcherUiTimer(TimeSpan interval)
        {
            _timer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = interval
            };
            _timer.Tick += (_, args) => Tick?.Invoke(this, args);
        }

        public event EventHandler? Tick;

        public void Start()
            => _timer.Start();

        public void Stop()
            => _timer.Stop();
    }
}
