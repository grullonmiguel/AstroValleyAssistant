namespace AstroValleyAssistant.Core.Services
{
    /// <summary>
    /// Defines a contract for launching URLs in the system browser.
    /// </summary>
    public interface IBrowserService
    {
        /// <summary>
        /// Launches the given URL using the system's default browser.
        /// </summary>
        /// <param name="url">The URL to open.</param>
        void Launch(string url);
    }
}