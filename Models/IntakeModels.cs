namespace LegalDraftAI.Models;

public class CaseIntakeRequest
{
    /// <summary>e.g. Delhi, Punjab, Maharashtra</summary>
    public string Jurisdiction { get; set; } = "Delhi";

    /// <summary>Civil or Criminal</summary>
    public string CaseCategory { get; set; } = "Civil";

    /// <summary>
    /// Known plaint nature code, e.g. AncestralLandPossession.
    /// If empty, system uses ClientFacts for a basic match against catalog labels.
    /// </summary>
    public string? PlaintNature { get; set; }

    /// <summary>Free-text facts from client when nature is not selected</summary>
    public string? ClientFacts { get; set; }

    /// <summary>Document codes the client already has (from checklist)</summary>
    public List<string>? AlreadyHaveDocumentCodes { get; set; }
}

public class RequiredDocumentItem
{
    public string Code { get; set; } = "";
    public string Label { get; set; } = "";
    public string Priority { get; set; } = ""; // Critical | High | Medium
    public string Purpose { get; set; } = "";
    public bool IsMandatoryForFiling { get; set; }
}

public class PlaintNatureInfo
{
    public string Code { get; set; } = "";
    public string Label { get; set; } = "";
    public string Category { get; set; } = "Civil";
    public string Description { get; set; } = "";
    public string SuggestedDocumentType { get; set; } = "CivilPlaint";
}

public class DocumentResearchResult
{
    public string PlaintNature { get; set; } = "";
    public string PlaintNatureLabel { get; set; } = "";
    public string SuggestedDocumentType { get; set; } = "";
    public string Summary { get; set; } = "";
    public List<RequiredDocumentItem> RequiredDocuments { get; set; } = new();
    public List<string> MissingMandatory { get; set; } = new();
    public List<string> MissingRecommended { get; set; } = new();
    public List<string> AlreadyHave { get; set; } = new();
    public bool CanFileWithMinimumSet { get; set; }
    public List<string> NextSteps { get; set; } = new();
}
