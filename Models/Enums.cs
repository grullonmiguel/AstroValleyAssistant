using System.ComponentModel;

namespace AstroValleyAssistant.Models
{
    public enum MenuOption
    {
        RealAuction,
        Regrid,
        Themes
    }

    public enum TaxSaleType
    {
        [Description("Tax Lien")]
        TaxLien,

        [Description("Tax Deed")]
        TaxDeed,

        [Description("Redeemable Deed")]
        RedeemableDeed,

        [Description("Hybrid")]
        Hybrid
    }

    public enum ScrapeStatus
    {
        Pending,
        Loading,
        Success,
        NotFound,       // No parcel found for that ID/Address
        MultipleMatches, // Ambiguous results from Regrid
        Error           // Network or server issues (like the 423 Locked)
    }

}
