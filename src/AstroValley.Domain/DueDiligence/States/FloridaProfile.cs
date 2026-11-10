using AstroValley.Domain.Entities;

namespace AstroValley.Domain.DueDiligence;

/// <summary>
/// Florida: only what differs from the common profile. A new state is a new class like this one, registered in <see cref="Jurisdictions"/>.
/// shortcut: rules written from one Miami-Dade case and general Florida tax-deed practice; confirm with a Florida attorney or title company.
/// </summary>
public class FloridaProfile : JurisdictionProfile
{
    public override string Name => "Florida";

    protected override IEnumerable<IRiskRule> LocalRules => [new FloridaHomesteadRule()];

    protected override IEnumerable<DueDiligenceItem> LocalChecklist =>
    [
        new() { Key = "fl-municipal-lien-letter", Office = Offices.City, Title = "Municipal lien search letter from the city",
            Hint = "Florida cities sell a lien search by folio (the City of Miami charges about $200); it lists code liens and violations not always in the official records." },
    ];
}

/// <summary>Florida homestead at a tax deed sale: the opening bid includes half of the assessed value, so it can sit far above the taxes owed.</summary>
public sealed class FloridaHomesteadRule : IRiskRule
{
    public string Code => "homestead"; // replaces the common homestead rule
    public RiskFlag? Evaluate(PropertyRecord r, DateTime today) =>
        r.Homestead == true
            ? new(Code, RiskSeverity.Caution, "Homestead property (Florida)",
                "For homestead property Florida adds half of the assessed value to the opening bid"
                + (r.AssessedValue is > 0 ? $" (about {Fmt.Money(r.AssessedValue / 2)} here)" : "")
                + ", so the bid can be far above the taxes owed. Expect the home to be occupied.")
            : null;
}
