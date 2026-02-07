using System.Diagnostics;

namespace AstroValleyAssistant.Core.Services
{
    /// <summary>
    /// Concrete implementation of IBrowserService.
    /// Responsible for interacting with the operating system to open URLs.
    /// </summary>
    public class BrowserService : IBrowserService
    {
        /// <summary>
        /// Opens the specified URL in the system's default browser.
        /// </summary>
        public void Launch(string url)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(url)) return;

                // ProcessStartInfo with UseShellExecute=true ensures the OS
                // uses the default browser instead of requiring an executable path.
                Process.Start(new ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error launching {url}: {ex.Message}");
            }
        }
    }
}