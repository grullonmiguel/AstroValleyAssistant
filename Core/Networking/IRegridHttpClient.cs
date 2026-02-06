namespace AstroValleyAssistant.Core.Networking
{
    public interface IRegridHttpClient
    {
        Task<byte[]> DownloadImageAsync(string url);
    }
}
