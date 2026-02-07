namespace AstroValleyAssistant.Core.Services
{
    public interface IRegridSettings
    {
        string RegridUserName { get; set; }
        string RegridPassword { get; set; }
        void Save();
    }
}
