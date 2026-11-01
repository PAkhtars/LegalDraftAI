using LegalDraftAI.Models;

namespace LegalDraftAI.Services;

/// <summary>
/// Static catalog of plaint natures and required documents.
/// No AI required — works offline for local development.
/// </summary>
public static class PlaintDocumentCatalog
{
    public static readonly IReadOnlyList<PlaintNatureInfo> PlaintNatures = new List<PlaintNatureInfo>
    {
        new()
        {
            Code = "AncestralLandPossession",
            Label = "Illegal possession of ancestral land",
            Category = "Civil",
            Description = "Suit for declaration of title, recovery of possession and permanent injunction regarding ancestral / joint family land.",
            SuggestedDocumentType = "CivilPlaint"
        },
        new()
        {
            Code = "DeclarationOfTitle",
            Label = "Declaration of title to property",
            Category = "Civil",
            Description = "Suit seeking declaration that plaintiff is owner of the property, with or without consequential relief.",
            SuggestedDocumentType = "CivilPlaint"
        },
        new()
        {
            Code = "PermanentInjunction",
            Label = "Permanent injunction (property interference)",
            Category = "Civil",
            Description = "Suit to restrain the defendant from interfering with plaintiff’s possession or rights in property.",
            SuggestedDocumentType = "CivilPlaint"
        },
        new()
        {
            Code = "RecoveryOfMoney",
            Label = "Recovery of money",
            Category = "Civil",
            Description = "Suit for recovery of debt, loan, or contractual dues.",
            SuggestedDocumentType = "CivilPlaint"
        },
        new()
        {
            Code = "SpecificPerformance",
            Label = "Specific performance of contract",
            Category = "Civil",
            Description = "Suit to compel performance of an agreement to sell or similar contract.",
            SuggestedDocumentType = "CivilPlaint"
        },
        new()
        {
            Code = "Partition",
            Label = "Partition of joint / ancestral property",
            Category = "Civil",
            Description = "Suit for partition and separate possession of joint or ancestral property.",
            SuggestedDocumentType = "CivilPlaint"
        }
    };

    private static readonly Dictionary<string, List<RequiredDocumentItem>> DocsByNature = new(StringComparer.OrdinalIgnoreCase)
    {
        ["AncestralLandPossession"] = new()
        {
            Doc("ID_PROOF", "Aadhaar / PAN / identity proof", "Critical", "Plaintiff identity & KYC", true),
            Doc("VAKALATNAMA", "Signed vakalatnama", "Critical", "Advocate’s authority to appear", true),
            Doc("PROPERTY_SCHEDULE", "Full property description (survey no., area, boundaries, village, tehsil, district)", "Critical", "Identify suit property in plaint schedule", true),
            Doc("TITLE_DEED", "Sale deed / gift deed / will / partition deed in ancestor’s or plaintiff’s favour", "Critical", "Prove source of title", true),
            Doc("REVENUE_RECORD", "Jamabandi / RTC / 7-12 / Khatauni / Khewat (relevant years)", "Critical", "Revenue ownership / cultivation trail", true),
            Doc("MUTATION", "Mutation entries / orders", "High", "How name came on revenue record", false),
            Doc("GENEALOGY", "Family tree + death certificates of deceased ancestors", "High", "Prove ancestral character and share", false),
            Doc("POSSESSION_PROOF", "Proof of earlier possession (photos, bills, crop records, witness list)", "High", "Show plaintiff was in possession", false),
            Doc("DISPOSSESSION_FACTS", "Date and manner of illegal dispossession (written chronology)", "Critical", "Cause of action & limitation", true),
            Doc("OPP_PARTY_DETAILS", "Name, parentage and full address of person in illegal possession", "Critical", "Cause title and summons", true),
            Doc("SITE_MAP", "Revenue / cadastral map or site plan", "High", "Identify land on map", false),
            Doc("LEGAL_NOTICE", "Pre-suit legal notice + proof of service", "Medium", "Show demand before suit", false),
            Doc("VALUATION", "Circle rate / market value proof for court fee", "Critical", "Court fee calculation", true),
            Doc("PRIOR_CASES", "Orders/papers of any earlier case on same land", "Medium", "Avoid conflicting decrees", false)
        },
        ["DeclarationOfTitle"] = new()
        {
            Doc("ID_PROOF", "Aadhaar / PAN / identity proof", "Critical", "Plaintiff identity", true),
            Doc("VAKALATNAMA", "Signed vakalatnama", "Critical", "Authority to appear", true),
            Doc("PROPERTY_SCHEDULE", "Full property description with boundaries", "Critical", "Suit schedule", true),
            Doc("TITLE_DEED", "Primary title document", "Critical", "Ownership claim", true),
            Doc("REVENUE_RECORD", "Current and historical revenue records", "High", "Support title", false),
            Doc("MUTATION", "Mutation entries", "High", "Name on record", false),
            Doc("OPP_PARTY_DETAILS", "Defendant name and address", "Critical", "Cause title", true),
            Doc("VALUATION", "Valuation for court fee", "Critical", "Court fee", true),
            Doc("LEGAL_NOTICE", "Notice and service proof (if any)", "Medium", "Pre-suit demand", false)
        },
        ["PermanentInjunction"] = new()
        {
            Doc("ID_PROOF", "Identity proof", "Critical", "Plaintiff identity", true),
            Doc("VAKALATNAMA", "Signed vakalatnama", "Critical", "Authority", true),
            Doc("PROPERTY_SCHEDULE", "Property description and boundaries", "Critical", "Suit schedule", true),
            Doc("POSSESSION_PROOF", "Proof of current possession", "Critical", "Injunction based on possession", true),
            Doc("THREAT_FACTS", "Facts showing threat of interference", "Critical", "Cause of action", true),
            Doc("TITLE_DEED", "Title or better right documents", "High", "Strengthen claim", false),
            Doc("OPP_PARTY_DETAILS", "Defendant details", "Critical", "Cause title", true),
            Doc("PHOTOS_VIDEOS", "Photos/videos of interference or property", "High", "Evidence", false),
            Doc("VALUATION", "Valuation for court fee", "Critical", "Court fee", true)
        },
        ["RecoveryOfMoney"] = new()
        {
            Doc("ID_PROOF", "Identity proof", "Critical", "Plaintiff identity", true),
            Doc("VAKALATNAMA", "Signed vakalatnama", "Critical", "Authority", true),
            Doc("CONTRACT_OR_PROMISSORY", "Agreement / promissory note / invoice / loan document", "Critical", "Basis of debt", true),
            Doc("LEDGER_STATEMENTS", "Account ledger / bank statements", "High", "Prove outstanding amount", false),
            Doc("NOTICES", "Demand notice + service proof", "High", "Pre-suit demand", false),
            Doc("OPP_PARTY_DETAILS", "Defendant name and address", "Critical", "Cause title", true),
            Doc("AMOUNT_BREAKUP", "Principal, interest, calculation sheet", "Critical", "Relief claimed", true),
            Doc("VALUATION", "Suit valuation (usually amount claimed)", "Critical", "Court fee", true)
        },
        ["SpecificPerformance"] = new()
        {
            Doc("ID_PROOF", "Identity proof", "Critical", "Plaintiff identity", true),
            Doc("VAKALATNAMA", "Signed vakalatnama", "Critical", "Authority", true),
            Doc("AGREEMENT_TO_SELL", "Agreement to sell / contract", "Critical", "Contract to be enforced", true),
            Doc("PAYMENT_PROOF", "Receipts of earnest money / part payments", "Critical", "Readiness and willingness", true),
            Doc("PROPERTY_SCHEDULE", "Property description in agreement", "Critical", "Subject matter", true),
            Doc("TITLE_OF_SELLER", "Seller’s title documents (if available)", "High", "Show seller can convey", false),
            Doc("NOTICES", "Notice calling upon performance + service proof", "High", "Breach and demand", false),
            Doc("OPP_PARTY_DETAILS", "Defendant details", "Critical", "Cause title", true),
            Doc("VALUATION", "Valuation for court fee", "Critical", "Court fee", true)
        },
        ["Partition"] = new()
        {
            Doc("ID_PROOF", "Identity proof of plaintiff", "Critical", "Identity", true),
            Doc("VAKALATNAMA", "Signed vakalatnama", "Critical", "Authority", true),
            Doc("PROPERTY_SCHEDULE", "Full description of joint / ancestral properties", "Critical", "Properties to be partitioned", true),
            Doc("TITLE_DEED", "Title documents of joint property", "Critical", "Ownership of estate", true),
            Doc("REVENUE_RECORD", "Revenue records showing joint holding", "High", "Joint character", false),
            Doc("GENEALOGY", "Family tree and heirship proof", "Critical", "Shares of parties", true),
            Doc("PRIOR_PARTITION", "Earlier partition / settlement (if any)", "High", "What remains joint", false),
            Doc("OPP_PARTY_DETAILS", "All co-sharers’ names and addresses", "Critical", "Necessary parties", true),
            Doc("VALUATION", "Valuation of share / estate for court fee", "Critical", "Court fee", true)
        }
    };

    public static PlaintNatureInfo? GetNature(string code) =>
        PlaintNatures.FirstOrDefault(n => n.Code.Equals(code, StringComparison.OrdinalIgnoreCase));

    public static IReadOnlyList<RequiredDocumentItem> GetDocuments(string plaintNatureCode)
    {
        if (DocsByNature.TryGetValue(plaintNatureCode, out var list))
            return list;
        return Array.Empty<RequiredDocumentItem>();
    }

    /// <summary>
    /// Simple keyword match when client only gives free text (no AI required).
    /// </summary>
    public static string? InferNatureFromFacts(string? facts)
    {
        if (string.IsNullOrWhiteSpace(facts))
            return null;

        var t = facts.ToLowerInvariant();

        if (t.Contains("ancestral") || (t.Contains("illegal possession") && t.Contains("land")) ||
            (t.Contains("possession") && t.Contains("ancestor")))
            return "AncestralLandPossession";

        if (t.Contains("partition") || t.Contains("separate possession") || t.Contains("co-sharer"))
            return "Partition";

        if (t.Contains("specific performance") || t.Contains("agreement to sell") || t.Contains("earnest"))
            return "SpecificPerformance";

        if (t.Contains("injunction") || t.Contains("restrain") || t.Contains("interference"))
            return "PermanentInjunction";

        if (t.Contains("recover") && (t.Contains("money") || t.Contains("loan") || t.Contains("debt") || t.Contains("rupee")))
            return "RecoveryOfMoney";

        if (t.Contains("declaration") && t.Contains("title"))
            return "DeclarationOfTitle";

        if (t.Contains("possession") && t.Contains("land"))
            return "AncestralLandPossession";

        return null;
    }

    private static RequiredDocumentItem Doc(
        string code, string label, string priority, string purpose, bool mandatory) => new()
    {
        Code = code,
        Label = label,
        Priority = priority,
        Purpose = purpose,
        IsMandatoryForFiling = mandatory
    };
}
