namespace AstroValley.Presentation.Models;

/// <summary>How a snackbar message is colored and which icon it gets (Styles/Controls/Snackbar.xaml).</summary>
public enum SnackbarSeverity
{
    Info,
    Success,
    Warning,
    Error
}

/// <summary>One snackbar message (not MaterialDesign's SnackbarMessage control): the text and its severity.</summary>
public sealed record SnackbarNotice(string Text, SnackbarSeverity Severity);
