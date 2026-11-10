using System.Diagnostics;
using System.Text.RegularExpressions;

namespace AstroValley.Domain.Entities;

/// <summary>
/// What kind of property a parcel is, from the county's land-use wording, Regrid's structure label and the unit count.
/// Each kind values and sells differently, and several are tax-deed traps (common areas, strips, park lots).
/// Empty = the data does not say; never a guess.
/// shortcut: keyword lists written from FL / IN / MI / UT samples; county wording varies, so extend the patterns as more states are tested.
/// </summary>
public static class HomeKind
{
    public const string SingleFamily = "Single-family";
    public const string SmallMulti = "Duplex-4plex";
    public const string MultiFamily = "Multi-family";     // multi-unit, unit count not given
    public const string Apartments = "5+ units";
    public const string Condo = "Condo / villa / townhouse";
    public const string Mobile = "Mobile home";
    public const string MobilePark = "Mobile home park";
    public const string VacantLot = "Vacant lot";
    public const string CommonArea = "Common area / strip";
    public const string Commercial = "Commercial";
    public const string Agricultural = "Agricultural";

    /// <summary>Kinds valued as a home (house comps); the rest are land, commercial, or not worth valuing.</summary>
    public static bool IsHome(string? kind) => kind is SingleFamily or SmallMulti or MultiFamily or Apartments or Condo or Mobile;
    public static bool IsMultiUnit(string? kind) => kind is SmallMulti or MultiFamily or Apartments;

    private static bool Has(string text, string pattern) => Regex.IsMatch(text, pattern, RegexOptions.IgnoreCase);

    /// <param name="useDescription">The county's land use ("usedesc").</param>
    /// <param name="structureDescription">Regrid's structure label ("lbcs_structure_desc", e.g. "Multifamily structures").</param>
    /// <param name="units">Living units on the parcel, when given.</param>
    /// <param name="condo">The county's condo flag.</param>
    /// <param name="propertyType">StructureKind: "Vacant land", "Structures" or empty.</param>
    public static string From(string? useDescription, string? structureDescription, int? units, bool condo, string? propertyType)
    {
        var t = $"{useDescription} {structureDescription}";

        // parcels that cannot be built on or sold on their own, whatever else they say
        if (Has(t, @"COMMON (AREA|ELEMENT|GROUND)|\bHOA\b|RIGHT[- ]OF[- ]WAY|\bR/?O/?W\b|RETENTION|DETENTION|DRAINAGE|\bCANAL\b|EASEMENT|\bBUFFER\b|SUBMERGED"))
            return CommonArea;
        if (Has(t, @"(MOBILE|MANUFACTURED) HOME PARK|TRAILER PARK|\bMH PARK|\bRV PARK|MOBILE HOME (SUBDIVISION|LOT)"))
            return MobilePark;

        // the building check overrides the label: a "residential" record with nothing standing is land (the house may be gone)
        if (propertyType == StructureKind.Vacant)
            return Has(t, @"\bAG(RI|\b)|FARM|PASTURE|TIMBER|GROVE|ORCHARD|CROP|RANCH") ? Agricultural : VacantLot;

        if (Has(t, @"\bMOBILE\b|MANUFACTURED|\bM/H\b|\bMH\b")) return Mobile;
        if (condo || Has(t, @"CONDO|TOWNHO(USE|ME)|\bVILLA|\bPUD UNIT|ZERO LOT")) return Condo; // attached / zero-lot-line homes share one market
        if (units is >= 5 || Has(t, @"APARTMENT|\b(5|FIVE)\s*(\+|OR MORE)|10 UNITS OR MORE")) return Apartments;
        if (units is >= 2 and <= 4 || Has(t, @"DUPLEX|TRIPLEX|FOURPLEX|QUADPLEX|\b2-4 UNITS")) return SmallMulti;
        if (Has(t, @"MULTI[- ]?FAMILY|MULTIPLE FAMILY")) return MultiFamily;
        if (Has(t, @"COMMERCIAL|\bSTORE|\bRETAIL|OFFICE|WAREHOUSE|INDUSTRIAL|HOTEL|MOTEL|RESTAURANT|SERVICE STATION|SHOPPING")) return Commercial;
        if (Has(t, @"\bAG(RI|\b)|FARM|PASTURE|TIMBER|GROVE|ORCHARD|CROP|RANCH")) return Agricultural;
        if (Has(t, @"SINGLE[- ]?FAMILY|ONE FAMILY|1 FAMILY|\bSFR\b|SINGLE-FAMILY BUILDINGS")) return SingleFamily;
        if (Has(t, @"VACANT|UNIMPROVED")) return VacantLot;
        return "";
    }

    /// <summary>
    /// A warning when the county lists a house but no building stands (the Saginaw case: "RESIDENTIAL-VACANT" with 3 bd / 1 ba, built 1950).
    /// Those facts must not reach the comps, or a vacant lot would be valued as a house. Null = no conflict.
    /// </summary>
    public static string? VacantConflict(string? propertyType, int? beds, int? livingSqft, int? yearBuilt) =>
        propertyType == StructureKind.Vacant && (beds > 0 || livingSqft > 0 || yearBuilt > 0)
            ? "The county record lists a house, but no building was found on the parcel: it may have been torn down. Check the aerial before valuing it as a home."
            : null;

    /// <summary>True when an owner name reads as an estate or heirs: probate and title risk before any sale.</summary>
    public static bool IsEstateOwner(string? owners) =>
        owners is { Length: > 0 } && Has(owners, @"\bEST\b|(?<!REAL\s)\bESTATE\b|\bHEIRS?\b|\bDECEASED\b|\bDEC'?D\b|\bPERSONAL REP|\bEXECUT(OR|RIX)\b"); // not "REAL ESTATE LLC"

    /// <summary>Assert-based check against the sample records in /data (no test project yet).</summary>
    public static void SelfCheck()
    {
        Debug.Assert(From("MULTIFAMILY 2-9 UNITS : 2 LIVING UNITS", "Multifamily structures", 2, false, StructureKind.Structures) == SmallMulti, "Miami-Dade duplex");
        Debug.Assert(From("RES ONE FAMILY PLATTED LOT-510", "Single-family buildings", null, false, StructureKind.Structures) == SingleFamily, "Indiana house");
        Debug.Assert(From("RESIDENTIAL-VACANT", null, null, false, StructureKind.Vacant) == VacantLot, "Saginaw: residential record, nothing standing");
        Debug.Assert(From(null, "Single-family buildings", 1, false, StructureKind.Structures) == SingleFamily, "Utah house");
        Debug.Assert(From("RESIDENTIAL-IMPROVED", "Residential buildings", null, false, StructureKind.Structures) == "", "unknown residential stays unknown");
        Debug.Assert(From("COMMON AREA", null, null, false, StructureKind.Vacant) == CommonArea, "common area beats vacant");
        Debug.Assert(From("MOBILE HOME", null, null, false, StructureKind.Structures) == Mobile, "mobile home");
        Debug.Assert(From("MULTIFAMILY 10 UNITS OR MORE", null, 24, false, StructureKind.Structures) == Apartments, "apartments");
        Debug.Assert(From("CONDOMINIUM - RESIDENTIAL", null, null, true, StructureKind.Structures) == Condo, "condo");
        Debug.Assert(VacantConflict(StructureKind.Vacant, 3, 964, 1950) is not null && VacantConflict(StructureKind.Structures, 3, 964, 1950) is null, "Saginaw conflict");
        Debug.Assert(IsEstateOwner("JOHN SOLOMON; JUANITA FRAZIER EST OF") && IsEstateOwner("HEIRS OF MARY SMITH") && !IsEstateOwner("WESTBROOK LLC") && !IsEstateOwner("SUN REAL ESTATE HOLDINGS LLC"), "estate wording");
    }
}
