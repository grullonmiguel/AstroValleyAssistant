namespace AstroValley.Application.Interfaces.Settings;

public interface IRegridSettings
{
    string RegridUserName { get; set; }
    string RegridPassword { get; set; }
    void Save();
}
