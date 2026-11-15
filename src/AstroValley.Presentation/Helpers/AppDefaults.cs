using System.IO;

namespace AstroValley.Presentation.Helpers;

/// <summary>
/// App-wide constants. Change the name and brand color here when you start a new project from the template.
/// </summary>
public static class AppDefaults
{
    /// <summary>
    /// Shown in the brand block, the window title and used for the settings/log folder name.
    /// </summary>
    public const string AppName = "Astro Assistant";

    /// <summary>
    /// Default primary (brand) color. Any hex color works. At runtime the Settings page can change it,
    /// and the choice is saved to the user's settings file.
    /// </summary>
    public const string PrimaryColorHex = "#534B80";

    /// <summary>
    /// The primary colors offered on the Settings page, each with the background and secondary emphasis
    /// color that ThemeService applies alongside it. The first entry is the fallback for any other primary.
    /// </summary>
    public static readonly IReadOnlyList<(string Name, string Primary, string Background, string SecondaryEmphasis)> Palettes =
    [
        ("Purple", "#534B80", "#EDEBF2", "#DBD8E9"),
        ("Teal", "#00796B", "#EAF3F1", "#D2E7E3"),
        ("Slate", "#2C3444", "#EFF0F3", "#DADDE4"),
        ("Blue", "#0D47A1", "#EAF0F8", "#D5E0F1"),
        ("Green", "#2E7D32", "#EEF4ED", "#DCE9DA"),
        ("Orange", "#E65100", "#F8F0EA", "#F2DDCE"),
        ("Crimson", "#B71C1C", "#F7EEEE", "#EED8D8"),
    ];

    /// <summary>
    /// Identifier of the DialogHost in ShellWindow. IDialogService shows every dialog on it.
    /// </summary>
    public const string RootDialogIdentifier = "RootDialog";

    /// <summary>
    /// Folder under %LocalAppData% for settings and logs.
    /// </summary>
    public static string DataFolder => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), AppName);
}
