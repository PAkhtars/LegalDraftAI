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
            PlaintGroup = "1.1 Real Estate & Immovable Property",
            Description = "Suit for declaration of title, recovery of possession and permanent injunction regarding ancestral / joint family land.",
            SuggestedDocumentType = "CivilPlaint"
        },
        new()
        {
            Code = "DeclarationOfTitle",
            Label = "Suit for Declaration of Title / Ownership (Sec 34, Specific Relief Act)",
            Category = "Civil",
            PlaintGroup = "1.1 Real Estate & Immovable Property",
            Description = "Suit seeking declaration that plaintiff is owner of the property, with or without consequential relief.",
            SuggestedDocumentType = "CivilPlaint"
        },
        new()
        {
            Code = "PossessionEvictionMesneProfits",
            Label = "Suit for Possession / Eviction & Mesne Profits",
            Category = "Civil",
            PlaintGroup = "1.1 Real Estate & Immovable Property",
            Description = "Suit seeking possession or eviction, with a claim for mesne profits where applicable.",
            SuggestedDocumentType = "CivilPlaint"
        },
        new()
        {
            Code = "Partition",
            Label = "Suit for Partition & Separate Possession of Ancestral / Joint Property",
            Category = "Civil",
            PlaintGroup = "1.1 Real Estate & Immovable Property",
            Description = "Suit for partition and separate possession of joint or ancestral property.",
            SuggestedDocumentType = "CivilPlaint"
        },
        new()
        {
            Code = "PermanentInjunction",
            Label = "Suit for Injunction (Permanent / Mandatory / Temporary)",
            Category = "Civil",
            PlaintGroup = "1.1 Real Estate & Immovable Property",
            Description = "Suit to restrain the defendant from interfering with plaintiff’s possession or rights in property.",
            SuggestedDocumentType = "CivilPlaint"
        },
        new()
        {
            Code = "EasementaryRights",
            Label = "Suit for Easementary Rights (Right of Way, Light, Air)",
            Category = "Civil",
            PlaintGroup = "1.1 Real Estate & Immovable Property",
            Description = "Suit concerning an asserted easementary right, such as a right of way, light, or air.",
            SuggestedDocumentType = "CivilPlaint"
        },
        new()
        {
            Code = "RecoveryOfMoney",
            Label = "Suit for Recovery of Money / Debt",
            Category = "Civil",
            PlaintGroup = "1.2 Contractual & Money Matters",
            Description = "Suit for recovery of debt, loan, or contractual dues.",
            SuggestedDocumentType = "CivilPlaint"
        },
        new()
        {
            Code = "SummarySuitOrder37",
            Label = "Summary Suit under Order 37 CPC (Negotiable instruments, written contracts)",
            Category = "Civil",
            PlaintGroup = "1.2 Contractual & Money Matters",
            Description = "Summary suit based on a qualifying negotiable instrument or written contract.",
            SuggestedDocumentType = "CivilPlaint"
        },
        new()
        {
            Code = "SpecificPerformance",
            Label = "Suit for Specific Performance of Contract",
            Category = "Civil",
            PlaintGroup = "1.2 Contractual & Money Matters",
            Description = "Suit to compel performance of an agreement to sell or similar contract.",
            SuggestedDocumentType = "CivilPlaint"
        },
        new()
        {
            Code = "CancellationRescissionInstruments",
            Label = "Cancellation / Rescission of Instruments / Sale Deeds",
            Category = "Civil",
            PlaintGroup = "1.2 Contractual & Money Matters",
            Description = "Suit seeking cancellation or rescission of an instrument or sale deed.",
            SuggestedDocumentType = "CivilPlaint"
        },
        Nature("CommercialCourtsActDisputes", "Commercial Disputes under the Commercial Courts Act", "1.3 Commercial & Financial Matters", "Commercial disputes under the applicable commercial courts jurisdiction."),
        Nature("PartnershipDissolutionAccounts", "Partnership Disputes & Dissolution of Firm / Accounts", "1.3 Commercial & Financial Matters", "Partnership disputes, dissolution, and claims for accounts."),
        Nature("IntellectualPropertyInfringement", "Intellectual Property Infringement (Trademark, Copyright, Patent Suits)", "1.3 Commercial & Financial Matters", "Civil claims concerning alleged trademark, copyright, or patent infringement."),
        Nature("BankingMortgageForeclosureRedemption", "Banking & Mortgage Suits (Foreclosure / Redemption)", "1.3 Commercial & Financial Matters", "Banking and mortgage disputes, including foreclosure or redemption claims."),
        Nature("DissolutionOfMarriage", "Petition for Dissolution of Marriage (Divorce)", "2.1 Marital Status & Reliefs", "Petition seeking dissolution of a marriage under applicable law."),
        Nature("JudicialSeparation", "Petition for Judicial Separation", "2.1 Marital Status & Reliefs", "Petition seeking judicial separation under applicable law."),
        Nature("RestitutionOfConjugalRights", "Petition for Restitution of Conjugal Rights", "2.1 Marital Status & Reliefs", "Petition seeking restitution of conjugal rights under applicable law."),
        Nature("NullityOfMarriage", "Suit / Petition for Declaration of Nullity of Marriage (Void / Voidable)", "2.1 Marital Status & Reliefs", "Petition seeking a declaration that a marriage is void or voidable under applicable law."),
        Nature("SpousalMaintenanceAlimony", "Claim for Spousal Maintenance / Alimony", "2.2 Maintenance & Financial Support", "Claim seeking spousal maintenance or alimony under applicable law."),
        Nature("InterimMaintenanceExpenses", "Application for Interim Maintenance & Litigation Expenses", "2.2 Maintenance & Financial Support", "Application seeking interim maintenance and litigation expenses."),
        Nature("ChildMaintenance", "Claim for Child Maintenance", "2.2 Maintenance & Financial Support", "Claim seeking financial support for a child under applicable law."),
        Nature("ChildCustodyVisitation", "Petition for Child Custody & Visitation Rights", "2.3 Custody & Guardianship", "Petition concerning child custody, access, or visitation arrangements."),
        Nature("GuardianshipApplication", "Guardianship Application under the Guardians and Wards Act", "2.3 Custody & Guardianship", "Application concerning appointment or declaration of a guardian."),
        Nature("ReturnOfStridhanBelongings", "Claim for Return of Stridhan / Personal Belongings", "2.4 Property & Family Claims", "Claim seeking return of stridhan or other personal belongings."),
        Nature("FamilySettlementMatrimonialPartition", "Family Settlement & Partition of Matrimonial Assets", "2.4 Property & Family Claims", "Claim concerning a family settlement or division of matrimonial assets."),
        Nature("DefamationDamages", "Suit for Damages for Defamation (Libel / Slander)", "3.1 Reputation & Personal Rights", "Civil claim for damages arising from alleged libel or slander."),
        Nature("MaliciousProsecutionDamages", "Suit for Damages for Malicious Prosecution", "3.1 Reputation & Personal Rights", "Civil claim for damages arising from alleged malicious prosecution."),
        Nature("BreachOfPrivacy", "Action for Breach of Privacy", "3.1 Reputation & Personal Rights", "Civil action concerning an alleged breach of privacy or personal rights."),
        Nature("MedicalNegligence", "Action for Medical Negligence / Professional Malpractice", "3.2 Negligence & Bodily Injury", "Civil claim alleging medical negligence or professional malpractice."),
        Nature("MotorAccidentCompensation", "Motor Accident Claims (Torts / Compensation)", "3.2 Negligence & Bodily Injury", "Claim for compensation arising from a motor accident, subject to applicable forum and procedure."),
        Nature("OccupierWorkplaceLiability", "Occupier's Liability & Workplace Hazards", "3.2 Negligence & Bodily Injury", "Civil claim concerning alleged occupier liability or workplace hazards."),
        Nature("PrivatePublicNuisance", "Suit for Private / Public Nuisance", "3.3 Nuisance & Trespass", "Suit concerning alleged private or public nuisance."),
        Nature("TrespassToLand", "Suit for Trespass to Land / Property", "3.3 Nuisance & Trespass", "Suit concerning alleged trespass to land or other property."),
        Nature("TrespassConversionOfGoods", "Suit for Trespass to Goods / Conversion of Goods", "3.3 Nuisance & Trespass", "Civil claim concerning alleged interference with or conversion of goods."),
        Nature("GrantOfProbate", "Application for Grant of Probate of Will", "4. Testamentary & Probate Suits", "Application seeking a grant of probate for a will."),
        Nature("LettersOfAdministration", "Petition for Letters of Administration", "4. Testamentary & Probate Suits", "Petition seeking letters of administration for an estate."),
        Nature("SuccessionCertificate", "Application for Succession Certificate", "4. Testamentary & Probate Suits", "Application seeking a succession certificate for qualifying debts or securities."),
        Nature("ProbateWillChallengeRevocation", "Challenge / Revocation of Probate or Will", "4. Testamentary & Probate Suits", "Proceeding challenging a will or seeking revocation of a probate grant."),
        Nature("SuitAgainstGovernmentPublicOfficer", "Suit against Public Officers / Government (Sec 80 CPC Notice-backed suits)", "5. Miscellaneous & Constitutional Civil Suits", "Civil suit against the Government or a public officer, including applicable notice requirements."),
        Nature("RepresentativeSuitPIL", "Representative Suit / Public Interest Litigation (Order 1 Rule 8 CPC)", "5. Miscellaneous & Constitutional Civil Suits", "Representative civil proceeding or public interest matter, subject to applicable jurisdiction and procedure."),
        Nature("WritCivilRevisionReview", "Writs / Civil Revision / Review Petitions", "5. Miscellaneous & Constitutional Civil Suits", "Constitutional or supervisory petition, civil revision, or review proceeding before the competent forum.")
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
        },
        ["PossessionEvictionMesneProfits"] = new()
        {
            Doc("ID_PROOF", "Identity proof", "Critical", "Plaintiff identity", true),
            Doc("VAKALATNAMA", "Signed vakalatnama", "Critical", "Authority to appear", true),
            Doc("PROPERTY_SCHEDULE", "Full property description and boundaries", "Critical", "Identify the property", true),
            Doc("TITLE_OR_TENANCY", "Title deed, lease, tenancy, or other right-to-possession records", "Critical", "Establish the claimed right to possession", true),
            Doc("POSSESSION_EVICTION_FACTS", "Possession history and facts of occupation, dispossession, or eviction", "Critical", "Establish the possession claim and cause of action", true),
            Doc("MESNE_PROFITS_CALCULATION", "Period and calculation of claimed mesne profits", "High", "Support the monetary claim", false),
            Doc("OPP_PARTY_DETAILS", "Defendant name and address", "Critical", "Cause title", true),
            Doc("VALUATION", "Suit valuation and court-fee calculation", "Critical", "Court fee", true)
        },
        ["EasementaryRights"] = new()
        {
            Doc("ID_PROOF", "Identity proof", "Critical", "Plaintiff identity", true),
            Doc("VAKALATNAMA", "Signed vakalatnama", "Critical", "Authority to appear", true),
            Doc("PROPERTY_SCHEDULE", "Description of affected properties and route / area", "Critical", "Identify the claimed easement", true),
            Doc("TITLE_OR_POSSESSION", "Title or possession records for the dominant property", "Critical", "Establish the property interest", true),
            Doc("EASEMENT_USE_EVIDENCE", "Evidence of the claimed use or easement (maps, records, photographs, witnesses)", "High", "Support the nature and extent of the claimed right", false),
            Doc("INTERFERENCE_FACTS", "Facts and dates of obstruction or interference", "Critical", "Establish cause of action", true),
            Doc("OPP_PARTY_DETAILS", "Defendant name and address", "Critical", "Cause title", true),
            Doc("VALUATION", "Suit valuation and court-fee calculation", "Critical", "Court fee", true)
        },
        ["SummarySuitOrder37"] = new()
        {
            Doc("ID_PROOF", "Identity proof", "Critical", "Plaintiff identity", true),
            Doc("VAKALATNAMA", "Signed vakalatnama", "Critical", "Authority to appear", true),
            Doc("WRITTEN_INSTRUMENT", "Negotiable instrument or written contract supporting the claim", "Critical", "Basis for the summary suit", true),
            Doc("AMOUNT_BREAKUP", "Principal, interest, and amount calculation", "Critical", "Quantify the claim", true),
            Doc("LEDGER_STATEMENTS", "Account ledger, invoices, or bank statements", "High", "Corroborate the outstanding amount", false),
            Doc("OPP_PARTY_DETAILS", "Defendant name and address", "Critical", "Cause title and service", true),
            Doc("VALUATION", "Suit valuation and court-fee calculation", "Critical", "Court fee", true)
        },
        ["CancellationRescissionInstruments"] = new()
        {
            Doc("ID_PROOF", "Identity proof", "Critical", "Plaintiff identity", true),
            Doc("VAKALATNAMA", "Signed vakalatnama", "Critical", "Authority to appear", true),
            Doc("INSTRUMENT_OR_SALE_DEED", "Original or certified copy of instrument / sale deed", "Critical", "Identify the instrument challenged", true),
            Doc("CANCELLATION_GROUNDS", "Facts and supporting records for cancellation or rescission", "Critical", "Set out the grounds and cause of action", true),
            Doc("OPP_PARTY_DETAILS", "Names and addresses of parties to the instrument", "Critical", "Cause title and necessary parties", true),
            Doc("VALUATION", "Suit valuation and court-fee calculation", "Critical", "Court fee", true),
            Doc("LEGAL_NOTICE", "Pre-suit notice and service proof, if any", "Medium", "Document prior demand", false)
        }
    };

    public static PlaintNatureInfo? GetNature(string code) =>
        PlaintNatures.FirstOrDefault(n => n.Code.Equals(code, StringComparison.OrdinalIgnoreCase));

    public static IReadOnlyList<RequiredDocumentItem> GetDocuments(string plaintNatureCode)
    {
        if (DocsByNature.TryGetValue(plaintNatureCode, out var list))
            return list;

        var nature = GetNature(plaintNatureCode);
        if (nature is not null)
            return GetGeneralDocuments(nature);

        return Array.Empty<RequiredDocumentItem>();
    }

    private static IReadOnlyList<RequiredDocumentItem> GetGeneralDocuments(PlaintNatureInfo nature)
    {
        var matterEvidence = nature.PlaintGroup switch
        {
            "1.3 Commercial & Financial Matters" => "Contracts, transaction records, invoices, and account statements",
            "2.1 Marital Status & Reliefs" => "Marriage records and supporting facts for the requested matrimonial relief",
            "2.2 Maintenance & Financial Support" => "Relationship records, income, assets, liabilities, and expense evidence",
            "2.3 Custody & Guardianship" => "Child identity, care, schooling, health, and current custody records",
            "2.4 Property & Family Claims" => "Asset inventory, ownership or purchase records, and family settlement documents",
            "3.1 Reputation & Personal Rights" => "Copies or other evidence of the alleged publication, conduct, or resulting loss",
            "3.2 Negligence & Bodily Injury" => "Incident reports, medical or loss records, and evidence relevant to liability",
            "3.3 Nuisance & Trespass" => "Property or goods ownership records and evidence of the alleged interference",
            "4. Testamentary & Probate Suits" => "Will or estate records, death certificate, and heir or beneficiary details",
            "5. Miscellaneous & Constitutional Civil Suits" => "Impugned order or action, statutory notice where applicable, and supporting records",
            _ => $"Documents and evidence relevant to: {nature.Label}"
        };

        return new List<RequiredDocumentItem>
        {
            Doc("ID_PROOF", "Identity proof", "Critical", "Identify the plaintiff or applicant", true),
            Doc("VAKALATNAMA", "Signed vakalatnama or authority to appear", "Critical", "Establish authority to appear", true),
            Doc("MATTER_EVIDENCE", matterEvidence, "Critical", nature.Description, true),
            Doc("PARTY_DETAILS", "Names and addresses of opposite parties / necessary parties", "Critical", "Prepare the cause title and service", true),
            Doc("VALUATION_AND_FEE", "Valuation, court-fee, and jurisdiction information", "Critical", "Assess applicable filing requirements", true),
            Doc("PRIOR_PROCEEDINGS", "Related proceedings, orders, or notices, if any", "High", "Disclose related matters", false)
        };
    }

    private static PlaintNatureInfo Nature(string code, string label, string group, string description) => new()
    {
        Code = code,
        Label = label,
        Category = "Civil",
        PlaintGroup = group,
        Description = description,
        SuggestedDocumentType = "CivilPlaint"
    };

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
