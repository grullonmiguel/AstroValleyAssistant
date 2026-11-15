using System.Windows.Media;

namespace AstroValley.Presentation.Services.Interfaces
{
    /// <summary>
    /// Changes the app's brand color at runtime through Material Design's palette.
    /// </summary>
    public interface IThemeService
    {
        Color PrimaryColor { get; }

        /// <summary>
        /// Applies the saved color (or the default). Call once at startup, before the first window shows.
        /// </summary>
        void Initialize();

        /// <summary>
        /// Applies a new primary color everywhere immediately and saves it.
        /// </summary>
        void SetPrimaryColor(Color color);
    }
}
