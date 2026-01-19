namespace AstroValleyAssistant.Core.Abstract
{
    public interface IRegridSettings
    {
        string RegridUserName { get; set; }
        string RegridPassword { get; set; }
        void Save();
    }
}
