namespace AstroValleyAssistant.Core.Services
{
    public interface IThemeService
    {
        /// <summary>
        /// Initializes the theme service by loading the saved theme from settings.
        /// </summary>
        void Initialize();

        /// <summary>
        /// Applies a new theme to the application and saves the choice.
        /// </summary>
        /// <param name="themeName">The name of the theme, e.g., "Light-Purple".</param>
        void SetTheme(string themeName);

        /// <summary>
        /// Gets the name of the currently active theme.
        /// </summary>
        /// <returns>The current theme name.</returns>
        string GetCurrentTheme();
    }
}
