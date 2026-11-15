using AstroValley.Presentation.Services.Interfaces;
using MaterialDesignThemes.Wpf;

namespace AstroValley.Presentation.Services;

public sealed class SnackbarService : ISnackbarService
{
    private readonly SnackbarMessageQueue _queue = new(TimeSpan.FromSeconds(3));

    public ISnackbarMessageQueue MessageQueue => _queue;

    public void Show(string message) => _queue.Enqueue(message);

    public void Show(string message, string actionText, Action action) => _queue.Enqueue(message, actionText, action);
}
