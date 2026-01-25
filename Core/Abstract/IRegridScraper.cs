using AstroValleyAssistant.Models;

namespace AstroValleyAssistant.Core.Abstract
{
    public interface IRegridScraper
    {
        Task<RegridParcelResult?> GetPropertyDetailsAsync(string data,  CancellationToken ct = default);
        
        Task<bool> AuthenticateAsync(string email, string password, CancellationToken ct = default);
    }
}
