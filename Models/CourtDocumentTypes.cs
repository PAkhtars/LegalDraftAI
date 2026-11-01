namespace LegalDraftAI.Models;

/// <summary>
/// Common court documents that parties request certified copies of.
/// Used to populate a multi-select dropdown.
/// </summary>
public static class CourtDocumentTypes
{
    public static readonly IReadOnlyList<CourtDocumentOption> Options = new List<CourtDocumentOption>
    {
        new("FIR", "FIR (First Information Report)"),
        new("CHARGESHEET", "Chargesheet / Final Report"),
        new("PLAINT", "Plaint / Petition"),
        new("WRITTEN_STATEMENT", "Written Statement / Reply"),
        new("ORDER_SHEET", "Order-sheets / Proceedings"),
        new("JUDGMENT", "Judgment / Final Order"),
        new("EVIDENCE", "Evidence / Depositions / Statements"),
        new("EXHIBITS", "Exhibits / Documents on record"),
        new("BAIL_ORDER", "Bail Order / Interim Orders"),
        new("SUMMONS", "Summons / Notices issued by Court"),
        new("VAKALATNAMA", "Vakalatnama / Power of Attorney"),
        new("APPLICATIONS", "Applications filed by parties"),
        new("OTHER", "Any other document (please specify)")
    };

    public static string GetLabel(string code)
    {
        return Options.FirstOrDefault(o => o.Code.Equals(code, StringComparison.OrdinalIgnoreCase))?.Label
               ?? code;
    }
}

public record CourtDocumentOption(string Code, string Label);
