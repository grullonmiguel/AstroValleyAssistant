namespace AstroValleyAssistant.Core.Services
{
    public class SettingsService : IRegridSettings, IRealAuctionSettings
    {        
        public string RegridUserName
        {
            get => Properties.Settings.Default.Regrid_UserName;
            set => Properties.Settings.Default.Regrid_UserName = value;
        }

        public string RegridPassword
        {
            get => Properties.Settings.Default.Regrid_Password;
            set => Properties.Settings.Default.Regrid_Password = value;
        }

        public string Url
        {
            get => Properties.Settings.Default.Real_URL;
            set => Properties.Settings.Default.Real_URL = value;
        }

        public string State
        {
            get => Properties.Settings.Default.Real_State;
            set => Properties.Settings.Default.Real_State = value;
        }

        public string County
        {
            get => Properties.Settings.Default.Real_County;
            set => Properties.Settings.Default.Real_County = value;
        }

        public string LastAuctionDate
        {
            get => Properties.Settings.Default.Real_Date;
            set => Properties.Settings.Default.Real_Date = value;
        }

        public void Save() => Properties.Settings.Default.Save();
    }
}