using System.Threading;
using System.Windows;

namespace LocalLlmConsole;

public partial class App : System.Windows.Application
{
    private Mutex? _singleInstanceMutex;
    private bool _ownsSingleInstanceMutex;

    protected override void OnStartup(StartupEventArgs e)
    {
        _singleInstanceMutex = new Mutex(true, @"Local\llama.cpp-console-single-instance", out var isFirstInstance);
        if (!isFirstInstance)
        {
            ThemedMessageBox.Show("llama.cpp Console is already running.", "llama.cpp Console", MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown();
            return;
        }
        _ownsSingleInstanceMutex = true;

        base.OnStartup(e);
        var window = new MainWindow();
        window.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (_ownsSingleInstanceMutex) _singleInstanceMutex?.ReleaseMutex();
        _singleInstanceMutex?.Dispose();
        base.OnExit(e);
    }
}
