using AstroValley.Presentation.Models;
using AstroValley.Presentation.Services.Interfaces;
using MaterialDesignThemes.Wpf;

namespace AstroValley.Presentation.Services;

public sealed class SnackbarService : ISnackbarService
{
    private readonly SnackbarMessageQueue _queue = new(TimeSpan.FromSeconds(4));

    public ISnackbarMessageQueue MessageQueue => _queue;

    public void Show(string message, SnackbarSeverity severity = SnackbarSeverity.Info) =>
        Show(message, "DISMISS", () => { }, severity);

    public void Show(string message, string actionText, Action action, SnackbarSeverity severity = SnackbarSeverity.Info) =>
        _queue.Enqueue(new SnackbarNotice(message, severity), actionText, _ => action(), (object?)null,
            promote: false, neverConsiderToBeDuplicate: true, durationOverride: DurationFor(severity));

    // Problems stay up longer so they are not missed; the button closes any message early.
    private static TimeSpan DurationFor(SnackbarSeverity severity) =>
        severity is SnackbarSeverity.Warning or SnackbarSeverity.Error ? TimeSpan.FromSeconds(10) : TimeSpan.FromSeconds(4);
}
