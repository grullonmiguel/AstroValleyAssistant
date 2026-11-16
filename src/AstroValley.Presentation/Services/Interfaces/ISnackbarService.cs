using AstroValley.Presentation.Models;
using MaterialDesignThemes.Wpf;

namespace AstroValley.Presentation.Services.Interfaces;

/// <summary>
/// Short, non-blocking messages shown at the bottom of the window, colored by severity.
/// Every message has an action button (Dismiss by default) that closes it early.
/// </summary>
public interface ISnackbarService
{
    /// <summary>The queue the shell's Snackbar control binds to.</summary>
    ISnackbarMessageQueue MessageQueue { get; }

    void Show(string message, SnackbarSeverity severity = SnackbarSeverity.Info);

    /// <summary>A message whose button runs <paramref name="action"/> (e.g. "Undo") instead of just dismissing.</summary>
    void Show(string message, string actionText, Action action, SnackbarSeverity severity = SnackbarSeverity.Info);
}
