using MaterialDesignThemes.Wpf;

namespace AstroValley.Presentation.Services.Interfaces;

/// <summary>
/// Short, non-blocking messages shown at the bottom of the window.
/// </summary>
public interface ISnackbarService
{
    /// <summary>The queue the shell's Snackbar control binds to.</summary>
    ISnackbarMessageQueue MessageQueue { get; }

    void Show(string message);

    void Show(string message, string actionText, Action action);
}
