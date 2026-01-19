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

}
