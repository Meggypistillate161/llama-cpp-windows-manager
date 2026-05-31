using System.Windows;

namespace LocalLlmConsole.Services;

public sealed class DialogService
{
    private readonly Func<Window?, string, string, MessageBoxButton, MessageBoxImage, MessageBoxResult> _show;

    public DialogService(
        Func<Window?, string, string, MessageBoxButton, MessageBoxImage, MessageBoxResult> show)
    {
        _show = show ?? throw new ArgumentNullException(nameof(show));
    }

    public MessageBoxResult Show(
        Window? owner,
        string message,
        string title,
        MessageBoxButton buttons = MessageBoxButton.OK,
        MessageBoxImage image = MessageBoxImage.None)
        => _show(owner, message, title, buttons, image);

    public bool Confirm(
        Window? owner,
        string message,
        string title,
        MessageBoxImage image = MessageBoxImage.Warning)
        => Show(owner, message, title, MessageBoxButton.YesNo, image) == MessageBoxResult.Yes;

    public void Notify(
        Window? owner,
        string message,
        string title,
        MessageBoxImage image = MessageBoxImage.Information)
        => Show(owner, message, title, MessageBoxButton.OK, image);
}
