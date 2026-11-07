using System.Text.Json;
using System.Text.RegularExpressions;
using LegalDraftAI.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace LegalDraftAI.Services;

public class PdfExportService : IPdfExportService
{
    private static readonly Regex NumberedParagraphPrefixRegex = new(
        @"^(?:\s*(?:\d+(?:\.\d+)+|[०-९]+(?:\.[०-९]+)+|\d+[.)]|[०-९]+[.)])\s*)+",
        RegexOptions.CultureInvariant);

    private static readonly Regex PrayerClauseMarkerRegex = new(
        @"\((?:[a-z]|ए|बी|सी|डी|ई|एफ|जी|एच|आई|जे|के|एल|एम|एन|ओ|पी|क्यू|आर|एस|टी|यू|वी|डब्ल्यू|एक्स|वाई|जेड)\)(?=\s|$)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    public PdfExportService()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public byte[] GenerateBailApplicationPdf(BailApplicationDraft d)
    {
        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(40);
                page.DefaultTextStyle(x => x.FontSize(11).FontFamily("Lato", "Noto Sans Devanagari"));

                page.Header().Column(col =>
                {
                    col.Item().AlignCenter().Text("IN THE COURT OF " + d.CourtName.ToUpper())
                        .Bold().FontSize(13);
                    col.Item().AlignCenter().Text(d.CaseTitle).FontSize(12);
                    if (!string.IsNullOrWhiteSpace(d.CaseNumber))
                        col.Item().AlignCenter().Text($"Case No.: {d.CaseNumber}").FontSize(11);
                    col.Item().PaddingTop(8).LineHorizontal(1);
                });

                page.Content().PaddingVertical(15).Column(col =>
                {
                    col.Spacing(10);

                    col.Item().Text(text =>
                    {
                        text.Span("Applicant: ").Bold();
                        text.Span(d.ApplicantName);
                    });

                    col.Item().Text(text =>
                    {
                        text.Span("Respondent: ").Bold();
                        text.Span(d.RespondentName);
                    });

                    if (!string.IsNullOrWhiteSpace(d.FirNumber))
                    {
                        col.Item().Text(text =>
                        {
                            text.Span("FIR No.: ").Bold();
                            text.Span(d.FirNumber);
                        });
                    }

                    if (!string.IsNullOrWhiteSpace(d.PoliceStation) || !string.IsNullOrWhiteSpace(d.District))
                    {
                        col.Item().Text(text =>
                        {
                            if (!string.IsNullOrWhiteSpace(d.PoliceStation))
                            {
                                text.Span("Police Station (Thana): ").Bold();
                                text.Span(d.PoliceStation);
                            }
                            if (!string.IsNullOrWhiteSpace(d.District))
                            {
                                if (!string.IsNullOrWhiteSpace(d.PoliceStation))
                                    text.Span("  |  ");
                                text.Span("District: ").Bold();
                                text.Span(d.District);
                            }
                        });
                    }

                    if (d.Sections.Any())
                    {
                        col.Item().Text(text =>
                        {
                            text.Span("Section(s): ").Bold();
                            text.Span(string.Join(", ", d.Sections));
                        });
                    }

                    col.Item().PaddingTop(8).Text("APPLICATION FOR GRANT OF BAIL")
                        .Bold().FontSize(12).AlignCenter();

                    col.Item().PaddingTop(10).Text("Most Respectfully Showeth:").Bold();

                    for (int i = 0; i < d.Grounds.Count; i++)
                    {
                        col.Item().PaddingLeft(15).Text($"{i + 1}. {d.Grounds[i]}");
                    }

                    col.Item().PaddingTop(15).Text("PRAYER").Bold().FontSize(12);
                    col.Item().Text(d.Prayer);

                    col.Item().PaddingTop(40).Row(row =>
                    {
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text($"Place: {d.Place}");
                            c.Item().Text($"Date: {d.Date}");
                        });

                        row.RelativeItem().AlignRight().Column(c =>
                        {
                            c.Item().Text("Advocate for the Applicant").Bold();
                            c.Item().Text(d.AdvocateName);
                            if (!string.IsNullOrWhiteSpace(d.AdvocateEnrollmentNo))
                                c.Item().Text($"Enrollment No.: {d.AdvocateEnrollmentNo}");
                        });
                    });
                });

                page.Footer().AlignCenter().Text(x =>
                {
                    x.Span("Page ");
                    x.CurrentPageNumber();
                    x.Span(" of ");
                    x.TotalPages();
                });
            });
        });

        return document.GeneratePdf();
    }

    public byte[] GenerateAdvocateBillPdf(AdvocateBillDraft d)
    {
        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(40);
                page.DefaultTextStyle(x => x.FontSize(11).FontFamily("Lato", "Noto Sans Devanagari"));

                page.Header().Column(col =>
                {
                    col.Item().AlignCenter().Text("ADVOCATE'S BILL / MEMO OF FEES")
                        .Bold().FontSize(14);
                    col.Item().PaddingTop(6).LineHorizontal(1);
                });

                page.Content().PaddingVertical(15).Column(col =>
                {
                    col.Spacing(8);

                    col.Item().Text(text =>
                    {
                        text.Span("Bill No.: ").Bold();
                        text.Span(d.BillNumber);
                    });

                    if (!string.IsNullOrWhiteSpace(d.CourtName))
                    {
                        col.Item().Text(text =>
                        {
                            text.Span("Court: ").Bold();
                            text.Span(d.CourtName);
                        });
                    }

                    if (!string.IsNullOrWhiteSpace(d.CaseNumber))
                    {
                        col.Item().Text(text =>
                        {
                            text.Span("Case No.: ").Bold();
                            text.Span(d.CaseNumber);
                        });
                    }

                    col.Item().Text(text =>
                    {
                        text.Span("Client: ").Bold();
                        text.Span(d.ClientName);
                    });

                    col.Item().Text(text =>
                    {
                        text.Span("Case: ").Bold();
                        text.Span(d.CaseTitle);
                    });

                    col.Item().Text(text =>
                    {
                        text.Span("Period: ").Bold();
                        text.Span($"{d.PeriodFrom} to {d.PeriodTo}");
                    });

                    col.Item().PaddingTop(12).Text("Particulars").Bold().FontSize(12);

                    col.Item().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(3);
                            columns.RelativeColumn(1);
                            columns.RelativeColumn(1);
                        });

                        table.Header(header =>
                        {
                            header.Cell().BorderBottom(1).Padding(4).Text("Description").Bold();
                            header.Cell().BorderBottom(1).Padding(4).Text("Date").Bold();
                            header.Cell().BorderBottom(1).Padding(4).AlignRight().Text("Amount (₹)").Bold();
                        });

                        foreach (var item in d.Items)
                        {
                            table.Cell().BorderBottom(0.5f).Padding(4).Text(item.Description);
                            table.Cell().BorderBottom(0.5f).Padding(4).Text(item.Date);
                            table.Cell().BorderBottom(0.5f).Padding(4).AlignRight().Text(item.Amount.ToString("N2"));
                        }
                    });

                    col.Item().PaddingTop(10).AlignRight().Text(text =>
                    {
                        text.Span("Total: ₹ ").Bold();
                        text.Span(d.TotalAmount.ToString("N2")).Bold();
                    });

                    if (!string.IsNullOrWhiteSpace(d.BankDetails))
                    {
                        col.Item().PaddingTop(15).Text("Bank / Payment Details:").Bold();
                        col.Item().Text(d.BankDetails);
                    }

                    col.Item().PaddingTop(40).AlignRight().Column(c =>
                    {
                        c.Item().Text(d.AdvocateName).Bold();
                        c.Item().Text("Advocate");
                    });
                });

                page.Footer().AlignCenter().Text(x =>
                {
                    x.Span("Page ");
                    x.CurrentPageNumber();
                    x.Span(" of ");
                    x.TotalPages();
                });
            });
        });

        return document.GeneratePdf();
    }

    public byte[] GenerateAbsenceApplicationPdf(AbsenceApplicationDraft d)
    {
        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(40);
                page.DefaultTextStyle(x => x.FontSize(11).FontFamily("Lato", "Noto Sans Devanagari"));

                page.Header().Column(col =>
                {
                    col.Item().AlignCenter().Text("IN THE COURT OF " + d.CourtName.ToUpper())
                        .Bold().FontSize(13);
                    col.Item().AlignCenter().Text(d.CaseTitle).FontSize(12);
                    if (!string.IsNullOrWhiteSpace(d.CaseNumber))
                        col.Item().AlignCenter().Text($"Case No.: {d.CaseNumber}").FontSize(11);
                    col.Item().PaddingTop(8).LineHorizontal(1);
                });

                page.Content().PaddingVertical(15).Column(col =>
                {
                    col.Spacing(10);

                    col.Item().Text(text =>
                    {
                        text.Span("Applicant: ").Bold();
                        text.Span($"{d.ApplicantName} ({d.ApplicantStatus})");
                    });

                    col.Item().Text(text =>
                    {
                        text.Span("Opposite Party: ").Bold();
                        text.Span(d.OppositePartyName);
                    });

                    col.Item().Text(text =>
                    {
                        text.Span("Date of Hearing: ").Bold();
                        text.Span(d.HearingDate);
                    });

                    if (!string.IsNullOrWhiteSpace(d.FirNumber))
                    {
                        col.Item().Text(text =>
                        {
                            text.Span("FIR No.: ").Bold();
                            text.Span(d.FirNumber);
                        });
                    }

                    if (!string.IsNullOrWhiteSpace(d.PoliceStation) || !string.IsNullOrWhiteSpace(d.District))
                    {
                        col.Item().Text(text =>
                        {
                            if (!string.IsNullOrWhiteSpace(d.PoliceStation))
                            {
                                text.Span("Police Station (Thana): ").Bold();
                                text.Span(d.PoliceStation);
                            }
                            if (!string.IsNullOrWhiteSpace(d.District))
                            {
                                if (!string.IsNullOrWhiteSpace(d.PoliceStation))
                                    text.Span("  |  ");
                                text.Span("District: ").Bold();
                                text.Span(d.District);
                            }
                        });
                    }

                    if (d.Sections != null && d.Sections.Any())
                    {
                        col.Item().Text(text =>
                        {
                            text.Span("Section(s): ").Bold();
                            text.Span(string.Join(", ", d.Sections));
                        });
                    }

                    col.Item().PaddingTop(10).Text("APPLICATION FOR EXEMPTION FROM PERSONAL APPEARANCE / ABSENCE ON HEARING DATE")
                        .Bold().FontSize(12).AlignCenter();

                    col.Item().PaddingTop(8).Text("Most Respectfully Showeth:").Bold();

                    col.Item().PaddingLeft(10).Text($"1. That the above-mentioned case is fixed for hearing on {d.HearingDate}.");

                    // Prefer dropdown-selected grounds if present
                    var displayGrounds = d.SelectedGroundLabels.Any()
                        ? d.SelectedGroundLabels
                        : d.Grounds;

                    if (displayGrounds.Any())
                    {
                        col.Item().PaddingLeft(10).Text("2. That the Applicant is unable to appear before this Hon'ble Court on the said date on the following ground(s):");
                        for (int i = 0; i < displayGrounds.Count; i++)
                        {
                            col.Item().PaddingLeft(20).Text($"{(char)('a' + i)}) {displayGrounds[i]}");
                        }
                    }
                    else if (!string.IsNullOrWhiteSpace(d.ReasonForAbsence))
                    {
                        col.Item().PaddingLeft(10).Text($"2. That the Applicant is unable to appear before this Hon'ble Court on the said date for the following reason: {d.ReasonForAbsence}");
                    }

                    // Additional detailed paragraphs (if any)
                    if (d.Grounds.Any() && d.SelectedGroundLabels.Any())
                    {
                        col.Item().PaddingTop(6).Text("Further details:").Bold();
                        for (int i = 0; i < d.Grounds.Count; i++)
                        {
                            col.Item().PaddingLeft(15).Text($"{i + 1}. {d.Grounds[i]}");
                        }
                    }

                    col.Item().PaddingTop(12).Text("PRAYER").Bold().FontSize(12);
                    col.Item().Text(d.Prayer);

                    col.Item().PaddingTop(8).Text("It is therefore most respectfully prayed that this Hon'ble Court may be pleased to:")
                        .Italic();
                    col.Item().PaddingLeft(15).Text("a) Exempt the Applicant from personal appearance on the hearing date mentioned above;");
                    col.Item().PaddingLeft(15).Text("b) Pass any other order(s) as this Hon'ble Court may deem fit and proper in the facts and circumstances of the case.");

                    col.Item().PaddingTop(30).Row(row =>
                    {
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text($"Place: {d.Place}");
                            c.Item().Text($"Date: {d.Date}");
                        });

                        row.RelativeItem().AlignRight().Column(c =>
                        {
                            c.Item().Text("Advocate for the Applicant").Bold();
                            c.Item().Text(d.AdvocateName);
                            if (!string.IsNullOrWhiteSpace(d.AdvocateEnrollmentNo))
                                c.Item().Text($"Enrollment No.: {d.AdvocateEnrollmentNo}");
                        });
                    });
                });

                page.Footer().AlignCenter().Text(x =>
                {
                    x.Span("Page ");
                    x.CurrentPageNumber();
                    x.Span(" of ");
                    x.TotalPages();
                });
            });
        });

        return document.GeneratePdf();
    }


    public byte[] GenerateSummonApplicationPdf(SummonApplicationDraft d)
    {
        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(40);
                page.DefaultTextStyle(x => x.FontSize(11).FontFamily("Lato", "Noto Sans Devanagari"));

                page.Header().Column(col =>
                {
                    col.Item().AlignCenter().Text("IN THE COURT OF " + d.CourtName.ToUpper())
                        .Bold().FontSize(13);
                    col.Item().AlignCenter().Text(d.CaseTitle).FontSize(12);
                    if (!string.IsNullOrWhiteSpace(d.CaseNumber))
                        col.Item().AlignCenter().Text($"Case No.: {d.CaseNumber}").FontSize(11);
                    col.Item().PaddingTop(8).LineHorizontal(1);
                });

                page.Content().PaddingVertical(15).Column(col =>
                {
                    col.Spacing(10);

                    col.Item().Text(text =>
                    {
                        text.Span("Applicant: ").Bold();
                        text.Span($"{d.ApplicantName} ({d.ApplicantStatus})");
                    });

                    col.Item().Text(text =>
                    {
                        text.Span("Opposite Party to be summoned: ").Bold();
                        text.Span($"{d.OppositePartyName}" +
                                  (string.IsNullOrWhiteSpace(d.OppositePartyStatus) ? "" : $" ({d.OppositePartyStatus})"));
                    });

                    if (!string.IsNullOrWhiteSpace(d.OppositePartyAddress))
                    {
                        col.Item().Text(text =>
                        {
                            text.Span("Address of Opposite Party: ").Bold();
                            text.Span(d.OppositePartyAddress);
                        });
                    }

                    if (!string.IsNullOrWhiteSpace(d.NextHearingDate))
                    {
                        col.Item().Text(text =>
                        {
                            text.Span("Next Date of Hearing: ").Bold();
                            text.Span(d.NextHearingDate);
                        });
                    }

                    if (!string.IsNullOrWhiteSpace(d.FirNumber))
                    {
                        col.Item().Text(text =>
                        {
                            text.Span("FIR No.: ").Bold();
                            text.Span(d.FirNumber);
                        });
                    }

                    if (!string.IsNullOrWhiteSpace(d.PoliceStation) || !string.IsNullOrWhiteSpace(d.District))
                    {
                        col.Item().Text(text =>
                        {
                            if (!string.IsNullOrWhiteSpace(d.PoliceStation))
                            {
                                text.Span("Police Station (Thana): ").Bold();
                                text.Span(d.PoliceStation);
                            }
                            if (!string.IsNullOrWhiteSpace(d.District))
                            {
                                if (!string.IsNullOrWhiteSpace(d.PoliceStation))
                                    text.Span("  |  ");
                                text.Span("District: ").Bold();
                                text.Span(d.District);
                            }
                        });
                    }

                    if (d.Sections != null && d.Sections.Any())
                    {
                        col.Item().Text(text =>
                        {
                            text.Span("Section(s): ").Bold();
                            text.Span(string.Join(", ", d.Sections));
                        });
                    }

                    col.Item().PaddingTop(10).Text("APPLICATION FOR ISSUANCE OF SUMMONS / SUMMONING THE OPPOSITE PARTY")
                        .Bold().FontSize(12).AlignCenter();

                    col.Item().PaddingTop(8).Text("Most Respectfully Showeth:").Bold();

                    col.Item().PaddingLeft(10).Text($"1. That the Applicant has filed the present case against the Opposite Party and the same is pending before this Hon'ble Court.");

                    if (d.Grounds.Any())
                    {
                        col.Item().PaddingTop(4).Text("2. Grounds for issuance of summons:").Bold();
                        for (int i = 0; i < d.Grounds.Count; i++)
                        {
                            col.Item().PaddingLeft(15).Text($"{i + 1}. {d.Grounds[i]}");
                        }
                    }
                    else
                    {
                        col.Item().PaddingLeft(10).Text("2. That the Opposite Party has not appeared despite earlier opportunities and it is necessary that summons be issued to secure their presence.");
                    }

                    col.Item().PaddingTop(12).Text("PRAYER").Bold().FontSize(12);
                    col.Item().Text(string.IsNullOrWhiteSpace(d.Prayer)
                        ? "It is therefore most respectfully prayed that this Hon'ble Court may be pleased to issue summons to the Opposite Party at the address mentioned above and/or by such other mode as this Hon'ble Court may deem fit and proper, and pass any other order(s) as may be deemed just and proper in the facts and circumstances of the case."
                        : d.Prayer);

                    col.Item().PaddingTop(30).Row(row =>
                    {
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text($"Place: {d.Place}");
                            c.Item().Text($"Date: {d.Date}");
                        });

                        row.RelativeItem().AlignRight().Column(c =>
                        {
                            c.Item().Text("Advocate for the Applicant").Bold();
                            c.Item().Text(d.AdvocateName);
                            if (!string.IsNullOrWhiteSpace(d.AdvocateEnrollmentNo))
                                c.Item().Text($"Enrollment No.: {d.AdvocateEnrollmentNo}");
                        });
                    });
                });

                page.Footer().AlignCenter().Text(x =>
                {
                    x.Span("Page ");
                    x.CurrentPageNumber();
                    x.Span(" of ");
                    x.TotalPages();
                });
            });
        });

        return document.GeneratePdf();
    }


    public byte[] GenerateCertifiedCopyApplicationPdf(CertifiedCopyApplicationDraft d)
    {
        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(40);
                page.DefaultTextStyle(x => x.FontSize(11).FontFamily("Lato", "Noto Sans Devanagari"));

                page.Header().Column(col =>
                {
                    col.Item().AlignCenter().Text("IN THE COURT OF " + d.CourtName.ToUpper())
                        .Bold().FontSize(13);
                    col.Item().AlignCenter().Text(d.CaseTitle).FontSize(12);
                    if (!string.IsNullOrWhiteSpace(d.CaseNumber))
                        col.Item().AlignCenter().Text($"Case No.: {d.CaseNumber}").FontSize(11);
                    col.Item().PaddingTop(8).LineHorizontal(1);
                });

                page.Content().PaddingVertical(15).Column(col =>
                {
                    col.Spacing(10);

                    col.Item().Text(text =>
                    {
                        text.Span("Applicant: ").Bold();
                        text.Span($"{d.ApplicantName} ({d.ApplicantStatus})");
                    });

                    if (!string.IsNullOrWhiteSpace(d.OppositePartyName))
                    {
                        col.Item().Text(text =>
                        {
                            text.Span("Opposite Party: ").Bold();
                            text.Span(d.OppositePartyName);
                        });
                    }

                    if (!string.IsNullOrWhiteSpace(d.FirNumber))
                    {
                        col.Item().Text(text =>
                        {
                            text.Span("FIR No.: ").Bold();
                            text.Span(d.FirNumber);
                        });
                    }

                    if (!string.IsNullOrWhiteSpace(d.PoliceStation) || !string.IsNullOrWhiteSpace(d.District))
                    {
                        col.Item().Text(text =>
                        {
                            if (!string.IsNullOrWhiteSpace(d.PoliceStation))
                            {
                                text.Span("Police Station (Thana): ").Bold();
                                text.Span(d.PoliceStation);
                            }
                            if (!string.IsNullOrWhiteSpace(d.District))
                            {
                                if (!string.IsNullOrWhiteSpace(d.PoliceStation))
                                    text.Span("  |  ");
                                text.Span("District: ").Bold();
                                text.Span(d.District);
                            }
                        });
                    }

                    if (d.Sections != null && d.Sections.Any())
                    {
                        col.Item().Text(text =>
                        {
                            text.Span("Section(s): ").Bold();
                            text.Span(string.Join(", ", d.Sections));
                        });
                    }

                    col.Item().PaddingTop(10).Text("APPLICATION FOR CERTIFIED COPIES / SUPPLY OF COURT DOCUMENTS")
                        .Bold().FontSize(12).AlignCenter();

                    col.Item().PaddingTop(8).Text("Most Respectfully Showeth:").Bold();

                    col.Item().PaddingLeft(10).Text("1. That the above-mentioned case is pending / has been decided by this Hon'ble Court.");

                    // Documents requested
                    var docs = d.SelectedDocumentLabels.Any()
                        ? d.SelectedDocumentLabels
                        : new List<string>();

                    if (docs.Any() || !string.IsNullOrWhiteSpace(d.AdditionalDocuments))
                    {
                        col.Item().PaddingLeft(10).Text("2. That the Applicant requires certified copies of the following documents on record:");
                        int idx = 1;
                        foreach (var doc in docs)
                        {
                            col.Item().PaddingLeft(20).Text($"{idx}. {doc}");
                            idx++;
                        }
                        if (!string.IsNullOrWhiteSpace(d.AdditionalDocuments))
                        {
                            col.Item().PaddingLeft(20).Text($"{idx}. {d.AdditionalDocuments}");
                        }
                    }

                    if (!string.IsNullOrWhiteSpace(d.Purpose))
                    {
                        col.Item().PaddingLeft(10).Text($"3. That the said copies are required for the following purpose: {d.Purpose}");
                    }

                    if (d.Grounds.Any())
                    {
                        col.Item().PaddingTop(4).Text("Additional grounds:").Bold();
                        for (int i = 0; i < d.Grounds.Count; i++)
                        {
                            col.Item().PaddingLeft(15).Text($"{i + 1}. {d.Grounds[i]}");
                        }
                    }

                    col.Item().PaddingTop(12).Text("PRAYER").Bold().FontSize(12);
                    col.Item().Text(string.IsNullOrWhiteSpace(d.Prayer)
                        ? "It is therefore most respectfully prayed that this Hon'ble Court may be pleased to direct the concerned official to supply certified copies of the documents mentioned above to the Applicant on the usual terms and conditions, and pass any other order(s) as this Hon'ble Court may deem fit and proper."
                        : d.Prayer);

                    col.Item().PaddingTop(30).Row(row =>
                    {
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text($"Place: {d.Place}");
                            c.Item().Text($"Date: {d.Date}");
                        });

                        row.RelativeItem().AlignRight().Column(c =>
                        {
                            c.Item().Text("Advocate for the Applicant").Bold();
                            c.Item().Text(d.AdvocateName);
                            if (!string.IsNullOrWhiteSpace(d.AdvocateEnrollmentNo))
                                c.Item().Text($"Enrollment No.: {d.AdvocateEnrollmentNo}");
                        });
                    });
                });

                page.Footer().AlignCenter().Text(x =>
                {
                    x.Span("Page ");
                    x.CurrentPageNumber();
                    x.Span(" of ");
                    x.TotalPages();
                });
            });
        });

        return document.GeneratePdf();
    }


    public byte[] GenerateCurativePetitionPdf(CurativePetitionDraft d)
    {
        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(40);
                page.DefaultTextStyle(x => x.FontSize(11).FontFamily("Lato", "Noto Sans Devanagari"));

                page.Header().Column(col =>
                {
                    col.Item().AlignCenter().Text("IN THE SUPREME COURT OF INDIA")
                        .Bold().FontSize(14);
                    col.Item().AlignCenter().Text("CURATIVE PETITION")
                        .Bold().FontSize(13);
                    col.Item().AlignCenter().Text(d.CaseTitle).FontSize(12);
                    if (!string.IsNullOrWhiteSpace(d.CaseNumber))
                        col.Item().AlignCenter().Text($"Case No.: {d.CaseNumber}").FontSize(11);
                    if (!string.IsNullOrWhiteSpace(d.ReviewPetitionNumber))
                        col.Item().AlignCenter().Text($"Review Petition No.: {d.ReviewPetitionNumber}").FontSize(11);
                    col.Item().PaddingTop(8).LineHorizontal(1);
                });

                page.Content().PaddingVertical(15).Column(col =>
                {
                    col.Spacing(10);

                    col.Item().Text(text =>
                    {
                        text.Span("Petitioner: ").Bold();
                        text.Span(d.PetitionerName);
                    });

                    col.Item().Text(text =>
                    {
                        text.Span("Respondent: ").Bold();
                        text.Span(d.RespondentName);
                    });

                    if (!string.IsNullOrWhiteSpace(d.ReviewDismissalDate))
                    {
                        col.Item().Text(text =>
                        {
                            text.Span("Date of Dismissal of Review Petition: ").Bold();
                            text.Span(d.ReviewDismissalDate);
                        });
                    }

                    if (!string.IsNullOrWhiteSpace(d.FirNumber))
                    {
                        col.Item().Text(text =>
                        {
                            text.Span("FIR No.: ").Bold();
                            text.Span(d.FirNumber);
                        });
                    }

                    if (!string.IsNullOrWhiteSpace(d.PoliceStation) || !string.IsNullOrWhiteSpace(d.District))
                    {
                        col.Item().Text(text =>
                        {
                            if (!string.IsNullOrWhiteSpace(d.PoliceStation))
                            {
                                text.Span("Police Station (Thana): ").Bold();
                                text.Span(d.PoliceStation);
                            }
                            if (!string.IsNullOrWhiteSpace(d.District))
                            {
                                if (!string.IsNullOrWhiteSpace(d.PoliceStation))
                                    text.Span("  |  ");
                                text.Span("District: ").Bold();
                                text.Span(d.District);
                            }
                        });
                    }

                    if (d.Sections != null && d.Sections.Any())
                    {
                        col.Item().Text(text =>
                        {
                            text.Span("Section(s): ").Bold();
                            text.Span(string.Join(", ", d.Sections));
                        });
                    }

                    col.Item().PaddingTop(10).Text("CURATIVE PETITION UNDER THE INHERENT POWERS OF THIS HON'BLE COURT")
                        .Bold().FontSize(12).AlignCenter();

                    col.Item().PaddingTop(8).Text("Most Respectfully Showeth:").Bold();

                    col.Item().PaddingLeft(10).Text("1. That the Petitioner had earlier preferred a Review Petition before this Hon'ble Court which came to be dismissed.");

                    if (d.Grounds.Any())
                    {
                        col.Item().PaddingTop(4).Text("2. Grounds for filing the present Curative Petition:").Bold();
                        for (int i = 0; i < d.Grounds.Count; i++)
                        {
                            col.Item().PaddingLeft(15).Text($"{i + 1}. {d.Grounds[i]}");
                        }
                    }
                    else
                    {
                        col.Item().PaddingLeft(10).Text("2. That the present Curative Petition is being preferred on the ground of violation of principles of natural justice / bias, as laid down by this Hon'ble Court in Rupa Ashok Hurra v. Ashok Hurra.");
                    }

                    col.Item().PaddingTop(12).Text("PRAYER").Bold().FontSize(12);
                    col.Item().Text(string.IsNullOrWhiteSpace(d.Prayer)
                        ? "It is therefore most respectfully prayed that this Hon'ble Court may be pleased to:\n(a) Entertain the present Curative Petition;\n(b) Set aside / modify the judgment and order under challenge;\n(c) Pass any other order(s) as this Hon'ble Court may deem fit and proper in the facts and circumstances of the case."
                        : d.Prayer);

                    col.Item().PaddingTop(30).Row(row =>
                    {
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text($"Place: {d.Place}");
                            c.Item().Text($"Date: {d.Date}");
                        });

                        row.RelativeItem().AlignRight().Column(c =>
                        {
                            c.Item().Text("Advocate for the Petitioner").Bold();
                            c.Item().Text(d.AdvocateName);
                            if (!string.IsNullOrWhiteSpace(d.AdvocateEnrollmentNo))
                                c.Item().Text($"Enrollment No.: {d.AdvocateEnrollmentNo}");
                            if (!string.IsNullOrWhiteSpace(d.AdvocateOnRecord))
                                c.Item().Text($"Advocate-on-Record: {d.AdvocateOnRecord}");
                        });
                    });
                });

                page.Footer().AlignCenter().Text(x =>
                {
                    x.Span("Page ");
                    x.CurrentPageNumber();
                    x.Span(" of ");
                    x.TotalPages();
                });
            });
        });

        return document.GeneratePdf();
    }


    public byte[] GenerateComplaintNonRegistrationFirPdf(ComplaintNonRegistrationFirDraft d)
    {
        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(40);
                page.DefaultTextStyle(x => x.FontSize(11).FontFamily("Lato", "Noto Sans Devanagari"));

                page.Header().Column(col =>
                {
                    col.Item().AlignCenter().Text("IN THE COURT OF " + d.CourtName.ToUpper())
                        .Bold().FontSize(13);
                    col.Item().AlignCenter().Text(d.CaseTitle).FontSize(12);
                    if (!string.IsNullOrWhiteSpace(d.CaseNumber))
                        col.Item().AlignCenter().Text($"Case No.: {d.CaseNumber}").FontSize(11);
                    col.Item().PaddingTop(8).LineHorizontal(1);
                });

                page.Content().PaddingVertical(15).Column(col =>
                {
                    col.Spacing(8);

                    col.Item().Text(text =>
                    {
                        text.Span("Complainant: ").Bold();
                        text.Span(d.ComplainantName);
                    });
                    if (!string.IsNullOrWhiteSpace(d.ComplainantAddress))
                        col.Item().PaddingLeft(10).Text(d.ComplainantAddress);

                    col.Item().Text(text =>
                    {
                        text.Span("Accused: ").Bold();
                        text.Span(d.AccusedName);
                    });
                    if (!string.IsNullOrWhiteSpace(d.AccusedAddress))
                        col.Item().PaddingLeft(10).Text(d.AccusedAddress);

                    col.Item().Text(text =>
                    {
                        text.Span("Police Station (Thana): ").Bold();
                        text.Span(d.PoliceStation);
                        text.Span("  |  District: ").Bold();
                        text.Span(d.District);
                    });

                    if (d.Sections.Any())
                    {
                        col.Item().Text(text =>
                        {
                            text.Span("Alleged Section(s): ").Bold();
                            text.Span(string.Join(", ", d.Sections));
                        });
                    }

                    if (!string.IsNullOrWhiteSpace(d.DateOfIncident))
                    {
                        col.Item().Text(text =>
                        {
                            text.Span("Date of Incident: ").Bold();
                            text.Span(d.DateOfIncident);
                        });
                    }

                    col.Item().PaddingTop(10).Text("COMPLAINT / APPLICATION UNDER SECTION 156(3) BNSS / CrPC FOR REGISTRATION OF FIR")
                        .Bold().FontSize(11).AlignCenter();

                    col.Item().PaddingTop(8).Text("Most Respectfully Showeth:").Bold();

                    col.Item().PaddingLeft(10).Text("1. That the Complainant is a law-abiding citizen and is constrained to approach this Hon'ble Court on account of non-registration of FIR by the concerned Police Station.");

                    if (!string.IsNullOrWhiteSpace(d.DateOfComplaintToPolice))
                    {
                        col.Item().PaddingLeft(10).Text($"2. That the Complainant had submitted a written complaint to the Police Station {d.PoliceStation} on {d.DateOfComplaintToPolice} regarding the commission of cognizable offences.");
                    }

                    if (!string.IsNullOrWhiteSpace(d.DateOfRefusal))
                    {
                        col.Item().PaddingLeft(10).Text($"3. That despite the said complaint, the police have failed / refused to register an FIR till {d.DateOfRefusal}.");
                    }

                    if (d.Facts.Any())
                    {
                        col.Item().PaddingTop(4).Text("4. Brief facts of the case:").Bold();
                        for (int i = 0; i < d.Facts.Count; i++)
                        {
                            col.Item().PaddingLeft(15).Text($"{i + 1}. {d.Facts[i]}");
                        }
                    }

                    if (d.Grounds.Any())
                    {
                        col.Item().PaddingTop(4).Text("5. Grounds for seeking direction for registration of FIR:").Bold();
                        for (int i = 0; i < d.Grounds.Count; i++)
                        {
                            col.Item().PaddingLeft(15).Text($"{i + 1}. {d.Grounds[i]}");
                        }
                    }

                    col.Item().PaddingTop(12).Text("PRAYER").Bold().FontSize(12);
                    col.Item().Text(string.IsNullOrWhiteSpace(d.Prayer)
                        ? "It is therefore most respectfully prayed that this Hon'ble Court may be pleased to:\n(a) Direct the Station House Officer of the concerned Police Station to register an FIR on the basis of the complaint of the Complainant and investigate the same in accordance with law;\n(b) Pass any other order(s) as this Hon'ble Court may deem fit and proper in the facts and circumstances of the case."
                        : d.Prayer);

                    col.Item().PaddingTop(30).Row(row =>
                    {
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text($"Place: {d.Place}");
                            c.Item().Text($"Date: {d.Date}");
                        });

                        row.RelativeItem().AlignRight().Column(c =>
                        {
                            c.Item().Text("Advocate for the Complainant").Bold();
                            c.Item().Text(d.AdvocateName);
                            if (!string.IsNullOrWhiteSpace(d.AdvocateEnrollmentNo))
                                c.Item().Text($"Enrollment No.: {d.AdvocateEnrollmentNo}");
                        });
                    });
                });

                page.Footer().AlignCenter().Text(x =>
                {
                    x.Span("Page ");
                    x.CurrentPageNumber();
                    x.Span(" of ");
                    x.TotalPages();
                });
            });
        });

        return document.GeneratePdf();
    }


    public byte[] GenerateCivilPlaintPdf(CivilPlaintDraft d, string outputLanguage = "English")
    {
        var isHindi = string.Equals(outputLanguage, "Hindi", StringComparison.OrdinalIgnoreCase);
        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(40);
                page.DefaultTextStyle(x => x.FontSize(11).FontFamily("Lato", "Noto Sans Devanagari"));

                page.Header().Column(col =>
                {
                    col.Item().AlignCenter().Text(
                        $"{(isHindi ? "न्यायालय" : "IN THE COURT OF")} {d.CourtName.ToUpperInvariant()}")
                        .Bold().FontSize(13);
                    col.Item().AlignCenter().Text(
                        string.IsNullOrWhiteSpace(d.CaseNumber)
                            ? isHindi ? "दीवानी वाद संख्या _______ सन् 20__" : "Civil Suit No. _______ of 20__"
                            : $"{(isHindi ? "दीवानी वाद संख्या" : "Civil Suit No.")} {d.CaseNumber}").FontSize(11);
                    col.Item().PaddingTop(8).LineHorizontal(1);
                });

                page.Content().PaddingVertical(12).Column(col =>
                {
                    col.Spacing(8);

                    col.Item().PaddingTop(6).Text(isHindi ? "विषय:" : "IN THE MATTER OF:").Bold();
                    col.Item().Text(d.PlaintiffName).Bold();
                    col.Item().Text(isHindi ? "पुत्र ______________________________" : "S/o ______________________________");
                    if (!string.IsNullOrWhiteSpace(d.PlaintiffAddress))
                        col.Item().Text($"{(isHindi ? "निवासी" : "R/o")} {d.PlaintiffAddress}");
                    col.Item().AlignRight().Text(isHindi ? "… वादी" : "… Plaintiff").Bold();

                    col.Item().AlignCenter().PaddingVertical(4).Text(isHindi ? "बनाम" : "VERSUS").Bold();

                    col.Item().Text(d.DefendantName).Bold();
                    if (!string.IsNullOrWhiteSpace(d.DefendantAddress))
                        col.Item().Text(d.DefendantAddress);
                    col.Item().AlignRight().Text(isHindi ? "… प्रतिवादी" : "… Defendant").Bold();

                    var suitTitle = !string.IsNullOrWhiteSpace(d.SuitTitle)
                        ? d.SuitTitle
                        : !string.IsNullOrWhiteSpace(d.PlaintNature)
                            ? d.PlaintNature
                            : isHindi ? "____________________ हेतु वाद" : "SUIT FOR ____________________";
                    col.Item().PaddingTop(8).AlignCenter().Text(suitTitle.ToUpperInvariant())
                        .Bold().FontSize(13);
                    col.Item().AlignCenter().Text(
                        $"({(isHindi ? "राशि" : "Amount")}: {d.Valuation ?? "Rs. ____________"})").Bold();

                    col.Item().PaddingTop(6).Text(
                        isHindi
                            ? "उपर्युक्त वादी सादर निवेदन करता है कि:"
                            : "The Plaintiff above named most respectfully showeth:—").Bold();

                    col.Item().PaddingTop(6).Text(isHindi ? "1. पक्षकार" : "1. PARTIES").Bold();
                    col.Item().PaddingLeft(10).Text(isHindi
                        ? "1.1 वादी का विवरण ऊपर दिया गया है।"
                        : "1.1 The Plaintiff is as described above.");
                    col.Item().PaddingLeft(10).Text(isHindi
                        ? "1.2 प्रतिवादी का विवरण ऊपर दिया गया है।"
                        : "1.2 The Defendant is as described above.");

                    if (d.Facts.Any())
                    {
                        col.Item().PaddingTop(4).Text(isHindi ? "2. मामले के तथ्य" : "2. FACTS OF THE CASE").Bold();
                        for (int i = 0; i < d.Facts.Count; i++)
                            col.Item().PaddingLeft(10).Text($"2.{i + 1} {RemoveLeadingParagraphNumber(d.Facts[i])}");
                    }

                    if (d.Grounds.Any())
                    {
                        col.Item().PaddingTop(4).Text(isHindi ? "3. विधिक आधार" : "3. LEGAL GROUNDS").Bold();
                        for (int i = 0; i < d.Grounds.Count; i++)
                            col.Item().PaddingLeft(10).Text($"3.{i + 1} {RemoveLeadingParagraphNumber(d.Grounds[i])}");
                    }

                    if (!string.IsNullOrWhiteSpace(d.SuitPropertyDescription) ||
                        !string.IsNullOrWhiteSpace(d.SurveyNumber) ||
                        !string.IsNullOrWhiteSpace(d.Village))
                    {
                        col.Item().PaddingTop(4).Text(isHindi
                            ? "वादग्रस्त संपत्ति की अनुसूची"
                            : "SCHEDULE OF SUIT PROPERTY").Bold();
                        if (!string.IsNullOrWhiteSpace(d.SuitPropertyDescription))
                            col.Item().Text(d.SuitPropertyDescription);
                        var parts = new List<string>();
                        if (!string.IsNullOrWhiteSpace(d.SurveyNumber))
                            parts.Add($"{(isHindi ? "सर्वेक्षण संख्या" : "Survey No.")}: {d.SurveyNumber}");
                        if (!string.IsNullOrWhiteSpace(d.Area))
                            parts.Add($"{(isHindi ? "क्षेत्रफल" : "Area")}: {d.Area}");
                        if (!string.IsNullOrWhiteSpace(d.Village))
                            parts.Add($"{(isHindi ? "गाँव" : "Village")}: {d.Village}");
                        if (!string.IsNullOrWhiteSpace(d.Tehsil))
                            parts.Add($"{(isHindi ? "तहसील" : "Tehsil")}: {d.Tehsil}");
                        if (!string.IsNullOrWhiteSpace(d.District))
                            parts.Add($"{(isHindi ? "ज़िला" : "District")}: {d.District}");
                        if (parts.Count > 0)
                            col.Item().Text(string.Join(" | ", parts));
                    }

                    col.Item().PaddingTop(6).Text(isHindi ? "4. वाद-हेतुक" : "4. CAUSE OF ACTION").Bold();
                    col.Item().PaddingLeft(10).Text($"4.1 {d.CauseOfAction ?? (isHindi
                        ? "वाद-हेतुक ______________________________ को उत्पन्न हुआ और, यदि लागू हो, तो ऊपर वर्णित तथ्यों के अनुसार जारी है।"
                        : "The cause of action arose on ______________________________ and continues, if applicable, as stated in the facts above.")}");
                    if (!string.IsNullOrWhiteSpace(d.CauseOfActionDate))
                        col.Item().PaddingLeft(10).Text($"{(isHindi ? "दिनांक" : "Date(s)")}: {d.CauseOfActionDate}");

                    col.Item().PaddingTop(6).Text(isHindi ? "5. अधिकारिता" : "5. JURISDICTION").Bold();
                    col.Item().PaddingLeft(10).Text($"5.1 {d.TerritorialJurisdiction ?? (isHindi
                        ? "क्षेत्रीय अधिकारिता के तथ्य निम्नलिखित हैं: ______________________________।"
                        : "The territorial jurisdiction facts are: ______________________________.")}");
                    col.Item().PaddingLeft(10).Text(isHindi
                        ? $"5.2 अधिकारिता और न्यायालय शुल्क के लिए वाद का मूल्य {d.Valuation ?? "________________"} है।"
                        : $"5.2 The value of the suit for jurisdiction and court fee is {d.Valuation ?? "________________"}.");

                    col.Item().PaddingTop(6).Text(isHindi ? "6. परिसीमा" : "6. LIMITATION").Bold();
                    col.Item().PaddingLeft(10).Text($"6.1 {d.LimitationPleading ?? (isHindi
                        ? "परिसीमा: ______________________________ (दाखिल करने से पहले सत्यापित करें)।"
                        : "Limitation: ______________________________ (to be verified before filing).")}");

                    col.Item().PaddingTop(6).Text(isHindi ? "7. न्यायालय शुल्क" : "7. COURT FEE").Bold();
                    col.Item().PaddingLeft(10).Text($"7.1 {d.CourtFeePleading ?? (isHindi
                        ? "अदा किया गया / देय न्यायालय शुल्क: ______________________________ (दाखिल करने से पहले सत्यापित करें)।"
                        : "Court fee paid / payable: ______________________________ (verify before filing).")}");

                    col.Item().PaddingTop(8).Text(isHindi ? "8. राहतें / प्रार्थना" : "8. RELIEFS / PRAYER").Bold();
                    if (d.ReliefsSought.Any())
                    {
                        for (int i = 0; i < d.ReliefsSought.Count; i++)
                            col.Item().PaddingLeft(10).Text($"{(char)('a' + i)}) {d.ReliefsSought[i]}");
                    }
                    var prayer = string.IsNullOrWhiteSpace(d.Prayer)
                        ? isHindi
                            ? "अतः सादर प्रार्थना है कि यह माननीय न्यायालय उपर्युक्त राहतें प्रदान करने, वाद का खर्च दिलाने तथा मामले के तथ्यों और परिस्थितियों में उचित एवं न्यायसंगत अन्य आदेश पारित करने की कृपा करे।"
                            : "It is therefore most respectfully prayed that this Hon'ble Court may be pleased to grant the reliefs sought above, award costs of the suit, and pass any other order(s) as this Hon'ble Court may deem fit and proper in the facts and circumstances of the case."
                        : d.Prayer;
                    AddPrayerParagraphs(col, prayer);

                    col.Item().PaddingTop(8).Text(isHindi
                        ? "अतः इस कृपा के लिए वादी सदैव आपका आभारी रहेगा।"
                        : "And for this act of kindness, the Plaintiff as in duty bound shall ever pray.").Italic();

                    col.Item().PaddingTop(16).Row(row =>
                    {
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text($"{(isHindi ? "स्थान" : "Place")}: {d.Place ?? "__________"}");
                            c.Item().Text($"{(isHindi ? "दिनांक" : "Date")}: {d.Date ?? "__________"}");
                        });
                        row.RelativeItem().AlignRight().Column(c =>
                        {
                            c.Item().Text(isHindi ? "वादी" : "Plaintiff").Bold();
                            c.Item().Text(isHindi ? "के माध्यम से" : "Through");
                            c.Item().Text(isHindi ? "अधिवक्ता" : "Advocate").Bold();
                            c.Item().Text(d.AdvocateName);
                            if (!string.IsNullOrWhiteSpace(d.AdvocateEnrollmentNo))
                                c.Item().Text($"{(isHindi ? "नामांकन संख्या" : "Enrollment No.")}: {d.AdvocateEnrollmentNo}");
                        });
                    });

                    col.Item().PaddingTop(16).Text(isHindi ? "सत्यापन" : "VERIFICATION").Bold();
                    col.Item().Text(isHindi
                        ? $"मैं, {d.PlaintiffName}, उपर्युक्त वादी, एतद्द्वारा सत्यापित करता/करती हूँ कि अनुच्छेद ______ से ______ तक की विषयवस्तु मेरे व्यक्तिगत ज्ञान पर आधारित सत्य है तथा अनुच्छेद ______ से ______ तक की विषयवस्तु प्राप्त जानकारी और उसे सत्य मानने के आधार पर सत्य है। इसमें कोई भी बात असत्य नहीं है और कोई महत्वपूर्ण तथ्य छिपाया नहीं गया है।"
                        : $"I, {d.PlaintiffName}, the Plaintiff above named, do hereby verify that the contents of paragraphs ______ to ______ are true to my personal knowledge and those of paragraphs ______ to ______ are based on information received and believed to be true. No part of it is false and nothing material has been concealed therefrom.");
                    col.Item().PaddingTop(6).Text(isHindi
                        ? "आज दिनांक ___ __________, 20__ को __________ में सत्यापित।"
                        : "Verified at __________ on this ___ day of __________, 20__.");
                    col.Item().PaddingTop(16).AlignRight().Text($"{(isHindi ? "वादी" : "Plaintiff")}\n{d.PlaintiffName}");
                });

                page.Footer().AlignCenter().Text(x =>
                {
                    x.Span(isHindi ? "पृष्ठ " : "Page ");
                    x.CurrentPageNumber();
                    x.Span(isHindi ? " में से " : " of ");
                    x.TotalPages();
                });
            });
        });

        return document.GeneratePdf();
    }

    public byte[] GenerateFromJson(string documentType, string json, string outputLanguage = "English")
    {
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

        switch (documentType)
        {
            case "BailApplication":
            {
                var draft = JsonSerializer.Deserialize<BailApplicationDraft>(json, options)
                            ?? throw new InvalidOperationException("Invalid BailApplication JSON");
                return GenerateBailApplicationPdf(draft);
            }
            case "AdvocateBill":
            {
                var draft = JsonSerializer.Deserialize<AdvocateBillDraft>(json, options)
                            ?? throw new InvalidOperationException("Invalid AdvocateBill JSON");
                return GenerateAdvocateBillPdf(draft);
            }
            case "AbsenceApplication":
            {
                var draft = JsonSerializer.Deserialize<AbsenceApplicationDraft>(json, options)
                            ?? throw new InvalidOperationException("Invalid AbsenceApplication JSON");
                return GenerateAbsenceApplicationPdf(draft);
            }
            case "SummonApplication":
            {
                var draft = JsonSerializer.Deserialize<SummonApplicationDraft>(json, options)
                            ?? throw new InvalidOperationException("Invalid SummonApplication JSON");
                return GenerateSummonApplicationPdf(draft);
            }
            case "CertifiedCopyApplication":
            {
                var draft = JsonSerializer.Deserialize<CertifiedCopyApplicationDraft>(json, options)
                            ?? throw new InvalidOperationException("Invalid CertifiedCopyApplication JSON");
                return GenerateCertifiedCopyApplicationPdf(draft);
            }
            case "CurativePetition":
            {
                var draft = JsonSerializer.Deserialize<CurativePetitionDraft>(json, options)
                            ?? throw new InvalidOperationException("Invalid CurativePetition JSON");
                return GenerateCurativePetitionPdf(draft);
            }
            case "ComplaintNonRegistrationFIR":
            {
                var draft = JsonSerializer.Deserialize<ComplaintNonRegistrationFirDraft>(json, options)
                            ?? throw new InvalidOperationException("Invalid ComplaintNonRegistrationFIR JSON");
                return GenerateComplaintNonRegistrationFirPdf(draft);
            }
            case "CivilPlaint":
            {
                var draft = JsonSerializer.Deserialize<CivilPlaintDraft>(json, options)
                            ?? throw new InvalidOperationException("Invalid CivilPlaint JSON");
                return GenerateCivilPlaintPdf(draft, outputLanguage);
            }
            case "CivilStageAApplication":
            {
                var draft = JsonSerializer.Deserialize<CivilStageAApplicationDraft>(json, options)
                            ?? throw new InvalidOperationException("Invalid CivilStageAApplication JSON");
                return GenerateCivilStageAApplicationPdf(draft);
            }
            case "CivilStageAProposedNotice":
            {
                var draft = JsonSerializer.Deserialize<CivilStageAProposedNoticeDraft>(json, options)
                            ?? throw new InvalidOperationException("Invalid CivilStageAProposedNotice JSON");
                return GenerateCivilStageAProposedNoticePdf(draft);
            }
            case "CivilStageBServiceApplication":
            {
                var draft = JsonSerializer.Deserialize<CivilStageBServiceApplicationDraft>(json, options)
                            ?? throw new InvalidOperationException("Invalid CivilStageBServiceApplication JSON");
                return GenerateCivilStageBServiceApplicationPdf(draft);
            }
            case "CivilStageCDefaultProcedureApplication":
            {
                var draft = JsonSerializer.Deserialize<CivilStageAApplicationDraft>(json, options)
                            ?? throw new InvalidOperationException("Invalid CivilStageCDefaultProcedureApplication JSON");
                return GenerateCivilStageAApplicationPdf(draft);
            }
            default:
                throw new NotSupportedException($"PDF template for '{documentType}' is not implemented yet.");
        }
    }

    public byte[] GenerateAffidavitPdf(string affidavitText)
    {
        if (string.IsNullOrWhiteSpace(affidavitText))
            throw new ArgumentException("Affidavit text is required.", nameof(affidavitText));

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(48);
                page.DefaultTextStyle(style => style.FontSize(11).FontFamily("Lato", "Noto Sans Devanagari"));
                page.Content()
                    .PaddingVertical(12)
                    .Text(affidavitText)
                    .LineHeight(1.4f);
                page.Footer()
                    .AlignCenter()
                    .Text(text =>
                    {
                        text.Span("Page ");
                        text.CurrentPageNumber();
                        text.Span(" of ");
                        text.TotalPages();
                    });
            });
        });

        return document.GeneratePdf();
    }

    public byte[] GenerateCivilStageAApplicationPdf(CivilStageAApplicationDraft draft)
        => GenerateCivilApplicationPdf(draft, isStageB: false);

    public byte[] GenerateCivilStageAProposedNoticePdf(CivilStageAProposedNoticeDraft draft)
    {
        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(48);
                page.DefaultTextStyle(style => style.FontSize(11).FontFamily("Lato", "Noto Sans Devanagari"));
                page.Content().Column(column =>
                {
                    column.Spacing(10);
                    if (!string.IsNullOrWhiteSpace(draft.CourtName))
                        column.Item().AlignCenter().Text($"IN THE COURT OF {draft.CourtName}")
                            .Bold().FontSize(13);
                    if (!string.IsNullOrWhiteSpace(draft.CaseNumber))
                        column.Item().AlignCenter().Text(draft.CaseNumber);
                    if (!string.IsNullOrWhiteSpace(draft.CaseTitle))
                        column.Item().AlignCenter().Text(draft.CaseTitle).Bold();

                    column.Item().PaddingTop(10).AlignCenter()
                        .Text(string.IsNullOrWhiteSpace(draft.NoticeTitle)
                            ? "PROPOSED NOTICE"
                            : draft.NoticeTitle)
                        .Bold().FontSize(13);

                    if (!string.IsNullOrWhiteSpace(draft.PlaintiffName))
                        column.Item().Text(text =>
                        {
                            text.Span("Plaintiff: ").Bold();
                            text.Span(draft.PlaintiffName);
                        });
                    if (!string.IsNullOrWhiteSpace(draft.DefendantName))
                        column.Item().Text(text =>
                        {
                            text.Span("To / Defendant: ").Bold();
                            text.Span(draft.DefendantName);
                        });
                    if (!string.IsNullOrWhiteSpace(draft.DefendantAddress))
                        column.Item().Text(text =>
                        {
                            text.Span("Address: ").Bold();
                            text.Span(draft.DefendantAddress);
                        });
                    if (!string.IsNullOrWhiteSpace(draft.Subject))
                        column.Item().PaddingTop(5).Text(text =>
                        {
                            text.Span("Subject: ").Bold();
                            text.Span(draft.Subject);
                        });

                    for (var index = 0; index < draft.NoticeParagraphs.Count; index++)
                        column.Item().PaddingTop(5).Text($"{index + 1}. {draft.NoticeParagraphs[index]}");

                    if (draft.EvidenceReferences.Count > 0)
                    {
                        column.Item().PaddingTop(8).Text("EVIDENCE REFERRED TO").Bold();
                        foreach (var reference in draft.EvidenceReferences)
                            column.Item().PaddingLeft(12).Text($"• {reference}");
                    }

                    if (!string.IsNullOrWhiteSpace(draft.ResponseDirection))
                        column.Item().PaddingTop(8).Text(draft.ResponseDirection);

                    column.Item().PaddingTop(24).Row(row =>
                    {
                        row.RelativeItem().Column(details =>
                        {
                            if (!string.IsNullOrWhiteSpace(draft.Place))
                                details.Item().Text($"Place: {draft.Place}");
                            if (!string.IsNullOrWhiteSpace(draft.Date))
                                details.Item().Text($"Date: {draft.Date}");
                        });
                        row.RelativeItem().AlignRight().Column(signature =>
                        {
                            signature.Item().Text("Plaintiff / Counsel").Bold();
                            if (!string.IsNullOrWhiteSpace(draft.AdvocateName))
                                signature.Item().Text(draft.AdvocateName);
                            if (!string.IsNullOrWhiteSpace(draft.AdvocateEnrollmentNo))
                                signature.Item().Text($"Enrollment No.: {draft.AdvocateEnrollmentNo}");
                        });
                    });
                });

                page.Footer().AlignCenter().Text(text =>
                {
                    text.Span("PROPOSED DRAFT · Page ");
                    text.CurrentPageNumber();
                    text.Span(" of ");
                    text.TotalPages();
                });
            });
        });

        return document.GeneratePdf();
    }

    private byte[] GenerateCivilApplicationPdf(CivilStageAApplicationDraft draft, bool isStageB)
    {
        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(48);
                page.DefaultTextStyle(style => style.FontSize(11).FontFamily("Lato", "Noto Sans Devanagari"));

                page.Content().Column(column =>
                {
                    column.Spacing(9);
                    if (!string.IsNullOrWhiteSpace(draft.CourtName))
                        column.Item().AlignCenter().Text($"IN THE COURT OF {draft.CourtName}")
                            .Bold().FontSize(13);
                    if (!string.IsNullOrWhiteSpace(draft.CaseNumber))
                        column.Item().AlignCenter().Text(draft.CaseNumber).FontSize(11);
                    if (!string.IsNullOrWhiteSpace(draft.CaseTitle))
                        column.Item().AlignCenter().Text(draft.CaseTitle).Bold();

                    if (!string.IsNullOrWhiteSpace(draft.ApplicantName) ||
                        !string.IsNullOrWhiteSpace(draft.RespondentName))
                    {
                        column.Item().PaddingTop(8).AlignCenter().Text("IN THE MATTER OF:").Bold();
                        if (!string.IsNullOrWhiteSpace(draft.ApplicantName))
                            column.Item().AlignCenter().Text(
                                $"{draft.ApplicantName} ... {(isStageB ? "Plaintiff" : "Applicant")}");
                        column.Item().AlignCenter().Text("VERSUS").Bold();
                        if (!string.IsNullOrWhiteSpace(draft.RespondentName))
                            column.Item().AlignCenter().Text(
                                $"{draft.RespondentName} ... {(isStageB ? "Defendant" : "Respondent")}");
                    }

                    column.Item().PaddingTop(10).AlignCenter()
                        .Text(string.IsNullOrWhiteSpace(draft.ApplicationTitle)
                            ? "APPLICATION"
                            : draft.ApplicationTitle)
                        .Bold().FontSize(13);

                    if (!string.IsNullOrWhiteSpace(draft.Provision))
                        column.Item().AlignCenter().Text(draft.Provision).Italic();

                    if (isStageB)
                    {
                        column.Item().PaddingTop(8).Text("MOST RESPECTFULLY SHOWETH:").Bold();
                        if (draft is CivilStageBServiceApplicationDraft serviceDraft &&
                            !string.IsNullOrWhiteSpace(serviceDraft.DefendantServiceAddress))
                        {
                            column.Item().PaddingTop(6).Text(text =>
                            {
                                text.Span("Defendant's address for service: ").Bold();
                                text.Span(serviceDraft.DefendantServiceAddress);
                            });
                        }
                    }

                    AddNumberedSection(column, "FACTUAL BACKGROUND", draft.FactualBackground);
                    AddNumberedSection(column, "GROUNDS", draft.Grounds);

                    if (draft.DocumentsReliedOn.Count > 0)
                    {
                        column.Item().PaddingTop(8).Text("DOCUMENTS RELIED ON").Bold();
                        foreach (var item in draft.DocumentsReliedOn)
                            column.Item().PaddingLeft(12).Text($"• {item}");
                    }

                    if (!string.IsNullOrWhiteSpace(draft.SupportingAffidavit))
                    {
                        column.Item().PaddingTop(8).Text("SUPPORTING AFFIDAVIT").Bold();
                        column.Item().Text(draft.SupportingAffidavit);
                    }

                    if (!string.IsNullOrWhiteSpace(draft.Prayer))
                    {
                        column.Item().PaddingTop(10).Text("PRAYER").Bold();
                        column.Item().Text(draft.Prayer);
                    }

                    if (isStageB &&
                        string.Equals(draft.ApplicationType, "InitialSummons", StringComparison.OrdinalIgnoreCase))
                        column.Item().PaddingTop(8).Text("AND FOR THIS ACT OF KINDNESS, THE PLAINTIFF AS IN DUTY BOUND SHALL EVER PRAY.").Bold();

                    column.Item().PaddingTop(28).Row(row =>
                    {
                        row.RelativeItem().Column(details =>
                        {
                            if (!string.IsNullOrWhiteSpace(draft.Place))
                                details.Item().Text($"Place: {draft.Place}");
                            if (!string.IsNullOrWhiteSpace(draft.Date))
                                details.Item().Text($"Date: {draft.Date}");
                        });
                        row.RelativeItem().AlignRight().Column(signature =>
                        {
                            signature.Item().Text(isStageB ? "Plaintiff" : "Applicant").Bold();
                            if (!string.IsNullOrWhiteSpace(draft.AdvocateName))
                            {
                                signature.Item().Text("Through Counsel").Bold();
                                signature.Item().Text(draft.AdvocateName);
                            }
                            if (!string.IsNullOrWhiteSpace(draft.AdvocateEnrollmentNo))
                                signature.Item().Text($"Enrollment No.: {draft.AdvocateEnrollmentNo}");
                        });
                    });
                });

                page.Footer().AlignCenter().Text(text =>
                {
                    text.Span("Page ");
                    text.CurrentPageNumber();
                    text.Span(" of ");
                    text.TotalPages();
                });
            });
        });

        return document.GeneratePdf();
    }

    public byte[] GenerateCivilStageBServiceApplicationPdf(CivilStageBServiceApplicationDraft draft) =>
        GenerateCivilApplicationPdf(draft, isStageB: true);

    private static void AddNumberedSection(
        ColumnDescriptor column,
        string title,
        IReadOnlyList<string> paragraphs)
    {
        if (paragraphs.Count == 0)
            return;

        column.Item().PaddingTop(8).Text(title).Bold();
        for (var index = 0; index < paragraphs.Count; index++)
            column.Item().PaddingLeft(12).Text($"{index + 1}. {paragraphs[index]}");
    }

    private static string RemoveLeadingParagraphNumber(string paragraph) =>
        NumberedParagraphPrefixRegex.Replace(paragraph, string.Empty);

    private static void AddPrayerParagraphs(ColumnDescriptor column, string prayer)
    {
        var markers = PrayerClauseMarkerRegex.Matches(prayer);
        if (markers.Count == 0)
        {
            column.Item().Text(prayer);
            return;
        }

        var firstMarker = markers[0];
        var introduction = prayer[..firstMarker.Index].Trim();
        if (introduction.Length > 0)
            column.Item().Text(introduction);

        for (var index = 0; index < markers.Count; index++)
        {
            var marker = markers[index];
            var end = index + 1 < markers.Count ? markers[index + 1].Index : prayer.Length;
            var clause = prayer[marker.Index..end].Trim();
            if (clause.Length > 0)
                column.Item().PaddingLeft(10).Text(clause);
        }
    }
}
