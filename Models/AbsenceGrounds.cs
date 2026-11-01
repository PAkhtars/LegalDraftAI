namespace LegalDraftAI.Models;

/// <summary>
/// Predefined grounds for Exemption from Personal Appearance (dropdown options).
/// </summary>
public static class AbsenceGrounds
{
    public static readonly IReadOnlyList<AbsenceGroundOption> Options = new List<AbsenceGroundOption>
    {
        new("MEDICAL", "Medical grounds / Illness of the applicant"),
        new("MEDICAL_FAMILY", "Medical emergency / Illness of close family member"),
        new("OUT_OF_STATION", "Applicant is out of station / travelling"),
        new("ANOTHER_COURT", "Engagement in another court on the same date"),
        new("PERSONAL_WORK", "Urgent personal / family work or function"),
        new("OFFICIAL_DUTY", "Official duty / professional commitment"),
        new("OLD_AGE", "Old age / physical disability / difficulty in travelling"),
        new("COVID", "Suffering from contagious disease / COVID related"),
        new("BEREAVEMENT", "Death / bereavement in the family"),
        new("EXAMINATION", "Appearance in examination / competitive test"),
        new("OTHER", "Any other sufficient cause (please specify)")
    };

    public static string GetLabel(string code)
    {
        return Options.FirstOrDefault(o => o.Code.Equals(code, StringComparison.OrdinalIgnoreCase))?.Label
               ?? code;
    }
}

public record AbsenceGroundOption(string Code, string Label);
