using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using LegalDraftAI.Models;
using Microsoft.Extensions.Options;

namespace LegalDraftAI.Services;

public class GroqDraftService : IGroqDraftService
{
    private readonly HttpClient _http;
    private readonly GroqOptions _options;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public GroqDraftService(HttpClient http, IOptions<GroqOptions> options)
    {
        _http = http;
        _options = options.Value;

        if (!string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            _http.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", _options.ApiKey);
        }
    }

    public async Task<DraftResult> GenerateStructuredDraftAsync(
        DraftRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            return new DraftResult
            {
                Success = false,
                Error = "Groq API key is not configured. Set it using .NET User Secrets or the Groq__ApiKey environment variable."
            };
        }

        var systemPrompt = BuildSystemPrompt(
            request.DocumentType,
            request.Jurisdiction,
            request.OutputLanguage);
        var userPrompt = BuildUserPrompt(request);

        var body = new
        {
            model = _options.Model,
            messages = new[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = userPrompt }
            },
            response_format = new { type = "json_object" },
            temperature = 0.2,
            max_tokens = 8192
        };

        var json = JsonSerializer.Serialize(body, JsonOpts);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");

        using var response = await _http.PostAsync("chat/completions", content, ct);
        var responseText = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            return new DraftResult
            {
                Success = false,
                Error = $"Groq API error {(int)response.StatusCode}: {responseText}"
            };
        }

        using var doc = JsonDocument.Parse(responseText);
        var root = doc.RootElement;

        var choice = root.GetProperty("choices")[0];
        var messageContent = choice.GetProperty("message").GetProperty("content").GetString();

        var usage = root.GetProperty("usage");
        var promptTokens = usage.GetProperty("prompt_tokens").GetInt32();
        var completionTokens = usage.GetProperty("completion_tokens").GetInt32();

        try
        {
            using var structured = JsonDocument.Parse(messageContent!);
            return new DraftResult
            {
                Success = true,
                RawJson = messageContent,
                StructuredData = JsonSerializer.Deserialize<object>(messageContent!),
                PromptTokens = promptTokens,
                CompletionTokens = completionTokens
            };
        }
        catch (JsonException ex)
        {
            return new DraftResult
            {
                Success = false,
                Error = $"Model returned invalid JSON: {ex.Message}",
                RawJson = messageContent,
                PromptTokens = promptTokens,
                CompletionTokens = completionTokens
            };
        }
    }

    public async Task<string> TranslateToHindiAsync(string text, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            throw new InvalidOperationException(
                "Groq API key is not configured. Set it using .NET User Secrets or the Groq__ApiKey environment variable.");
        }

        var translatedChunks = new List<string>();
        foreach (var chunk in SplitTranslationText(text, 10000))
            translatedChunks.Add(await TranslateChunkToHindiAsync(chunk, ct));

        return string.Join(Environment.NewLine, translatedChunks);
    }

    public async Task<string> RefineCivilPlaintTextAsync(
        string field,
        string text,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            throw new InvalidOperationException(
                "Groq API key is not configured. Set it using .NET User Secrets or the Groq__ApiKey environment variable.");
        }

        var fieldInstructions = field switch
        {
            "facts" => "Reorder the supplied facts into a clear, chronological factual narrative.",
            "propertyDescription" => "Organize the supplied property-identification details into a clear, coherent description.",
            "grounds" => "Improve the organization and grammar of only the legal grounds already supplied. Do not add legal grounds, authorities, or citations.",
            "reliefsSought" => "Organize only the requested reliefs already supplied into a clear, logically ordered list.",
            "prayer" => "Improve the clarity and grammar of the supplied prayer while preserving exactly the reliefs requested.",
            _ => throw new ArgumentException("Unsupported civil plaint field.", nameof(field))
        };

        var body = new
        {
            model = _options.Model,
            messages = new[]
            {
                new
                {
                    role = "system",
                    content = $"""
                        You are editing one user-supplied field for a civil plaint.
                        {fieldInstructions}
                        Correct grammar and improve logical flow without changing the meaning.
                        Do not add, infer, or remove facts, names, dates, amounts, property identifiers, requested reliefs, legal conclusions, statutes, or citations.
                        Preserve the language of the input. Do not translate it.
                        Return only the refined field text, with no heading, explanation, markdown, or quotation marks.
                        """
                },
                new { role = "user", content = text }
            },
            temperature = 0.1,
            max_tokens = 4096
        };

        var json = JsonSerializer.Serialize(body, JsonOpts);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        using var response = await _http.PostAsync("chat/completions", content, ct);
        var responseText = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"Civil plaint text refinement failed (HTTP {(int)response.StatusCode}): {responseText}",
                null,
                response.StatusCode);
        }

        using var document = JsonDocument.Parse(responseText);
        var choice = document.RootElement.GetProperty("choices")[0];
        if (choice.TryGetProperty("finish_reason", out var finishReason) &&
            finishReason.GetString() == "length")
        {
            throw new InvalidOperationException(
                "The AI response was truncated. Reduce the field text and try again.");
        }

        var refinedText = choice.GetProperty("message").GetProperty("content").GetString();
        if (string.IsNullOrWhiteSpace(refinedText))
            throw new InvalidOperationException("The AI returned an empty refinement.");

        return refinedText.Trim();
    }

    private async Task<string> TranslateChunkToHindiAsync(string text, CancellationToken ct)
    {
        var body = new
        {
            model = _options.Model,
            messages = new[]
            {
                new
                {
                    role = "system",
                    content = """
                        Translate the supplied legal-document text into clear, faithful Hindi.
                        Preserve paragraph breaks, numbering, names, dates, amounts, addresses, legal citations, and document codes.
                        Do not summarize, add facts, give legal advice, or omit content.
                        Return only the Hindi translation, without commentary or markdown fences.
                        """
                },
                new { role = "user", content = text }
            },
            temperature = 0.1,
            max_tokens = 8192
        };

        var json = JsonSerializer.Serialize(body, JsonOpts);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        using var response = await _http.PostAsync("chat/completions", content, ct);
        var responseText = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"Groq Hindi translation failed (HTTP {(int)response.StatusCode}): {responseText}",
                null,
                response.StatusCode);
        }

        using var document = JsonDocument.Parse(responseText);
        var choice = document.RootElement.GetProperty("choices")[0];
        if (choice.TryGetProperty("finish_reason", out var finishReason) &&
            finishReason.GetString() == "length")
        {
            throw new InvalidOperationException(
                "Groq truncated the Hindi translation. Reduce the document size and try again.");
        }

        var translation = choice.GetProperty("message").GetProperty("content").GetString();
        if (string.IsNullOrWhiteSpace(translation))
            throw new InvalidOperationException("Groq returned an empty Hindi translation.");

        return translation.Trim();
    }

    private static IEnumerable<string> SplitTranslationText(string text, int maxChunkLength)
    {
        var offset = 0;
        while (offset < text.Length)
        {
            var chunkLength = Math.Min(maxChunkLength, text.Length - offset);
            var end = offset + chunkLength;
            if (end < text.Length)
            {
                var newline = text.LastIndexOf('\n', end - 1, chunkLength);
                if (newline >= offset + maxChunkLength / 2)
                    chunkLength = newline - offset + 1;
            }

            yield return text.Substring(offset, chunkLength);
            offset += chunkLength;
        }
    }

    internal static string BuildSystemPrompt(
        string documentType,
        string jurisdiction,
        string outputLanguage)
    {
        return $"""
            You are an expert Indian advocate drafting court-ready legal documents.
            Jurisdiction: {jurisdiction}.
            Document type: {documentType}.
            Output language: {outputLanguage}.

            Output MUST be a single valid JSON object only. No markdown, no explanation, no extra text.

            Follow these strict rules:
            1. Use formal legal language appropriate for Indian courts.
            2. Write all human-readable document content in the requested output language. For Hindi, use clear formal Hindi in Devanagari; preserve proper names, addresses, identifiers, and statutory citations in their original form where appropriate.
            3. Keep JSON property names exactly as shown in the schema, regardless of output language.
            4. Include all standard sections for this document type.
            5. Leave fields that cannot be filled from the provided facts as null.
            6. Never invent case numbers, dates, or party names that are not in the facts.
            7. Cite BNS / BNSS / relevant statutes only when the facts support it.
            8. Structure the JSON exactly as shown in the user message example.
            """;
    }

    internal static string BuildUserPrompt(DraftRequest request)
    {
        var schemaExample = request.DocumentType switch
        {
            "BailApplication" => """
                {
                  "courtName": "string",
                  "caseTitle": "string",
                  "caseNumber": "string",
                  "bailType": "string",
                  "applicantName": "string",
                  "applicantAddress": "string|null",
                  "respondentName": "string",
                  "firNumber": "string|null",
                  "policeStation": "string|null",
                  "district": "string|null",
                  "sections": ["string"],
                  "factualBackground": ["string"],
                  "custodyStatus": "string|null",
                  "grounds": ["string"],
                  "prayer": "string",
                  "place": "string",
                  "date": "YYYY-MM-DD",
                  "advocateName": "string",
                  "advocateEnrollmentNo": "string|null"
                }
                """,
            "AdvocateBill" => """
                {
                  "billNumber": "string",
                  "courtName": "string",
                  "caseNumber": "string",
                  "clientName": "string",
                  "caseTitle": "string",
                  "periodFrom": "YYYY-MM-DD",
                  "periodTo": "YYYY-MM-DD",
                  "items": [
                    { "description": "string", "date": "YYYY-MM-DD", "amount": 0 }
                  ],
                  "totalAmount": 0,
                  "advocateName": "string",
                  "bankDetails": "string|null"
                }
                """,
            "AbsenceApplication" => """
                {
                  "courtName": "string",
                  "caseTitle": "string",
                  "caseNumber": "string",
                  "applicantName": "string",
                  "applicantStatus": "string",
                  "oppositePartyName": "string",
                  "firNumber": "string|null",
                  "policeStation": "string|null",
                  "district": "string|null",
                  "sections": ["string"],
                  "hearingDate": "YYYY-MM-DD",
                  "selectedGroundCodes": ["string"],
                  "selectedGroundLabels": ["string"],
                  "reasonForAbsence": "string",
                  "grounds": ["string"],
                  "prayer": "string",
                  "place": "string",
                  "date": "YYYY-MM-DD",
                  "advocateName": "string",
                  "advocateEnrollmentNo": "string|null"
                }
                """,
            "SummonApplication" => """
                {
                  "courtName": "string",
                  "caseTitle": "string",
                  "caseNumber": "string",
                  "applicantName": "string",
                  "applicantStatus": "string",
                  "oppositePartyName": "string",
                  "oppositePartyAddress": "string",
                  "oppositePartyStatus": "string|null",
                  "firNumber": "string|null",
                  "policeStation": "string|null",
                  "district": "string|null",
                  "sections": ["string"],
                  "nextHearingDate": "YYYY-MM-DD",
                  "grounds": ["string"],
                  "prayer": "string",
                  "place": "string",
                  "date": "YYYY-MM-DD",
                  "advocateName": "string",
                  "advocateEnrollmentNo": "string|null"
                }
                """,
            "CertifiedCopyApplication" => """
                {
                  "courtName": "string",
                  "caseTitle": "string",
                  "caseNumber": "string",
                  "applicantName": "string",
                  "applicantStatus": "string",
                  "oppositePartyName": "string",
                  "firNumber": "string|null",
                  "policeStation": "string|null",
                  "district": "string|null",
                  "sections": ["string"],
                  "selectedDocumentCodes": ["string"],
                  "selectedDocumentLabels": ["string"],
                  "additionalDocuments": "string|null",
                  "purpose": "string",
                  "grounds": ["string"],
                  "prayer": "string",
                  "place": "string",
                  "date": "YYYY-MM-DD",
                  "advocateName": "string",
                  "advocateEnrollmentNo": "string|null"
                }
                """,
            "CurativePetition" => """
                {
                  "courtName": "Supreme Court of India",
                  "caseTitle": "string",
                  "caseNumber": "string",
                  "reviewPetitionNumber": "string|null",
                  "reviewDismissalDate": "YYYY-MM-DD|null",
                  "petitionerName": "string",
                  "respondentName": "string",
                  "firNumber": "string|null",
                  "policeStation": "string|null",
                  "district": "string|null",
                  "sections": ["string"],
                  "grounds": ["string"],
                  "prayer": "string",
                  "place": "string",
                  "date": "YYYY-MM-DD",
                  "advocateName": "string",
                  "advocateEnrollmentNo": "string|null",
                  "advocateOnRecord": "string|null"
                }
                """,
            "CivilPlaint" => """
                {
                  "courtName": "string",
                  "caseTitle": "string",
                  "caseNumber": "string|null",
                  "plaintNature": "string",
                  "suitTitle": "string",
                  "plaintiffName": "string",
                  "plaintiffAddress": "string",
                  "defendantName": "string",
                  "defendantAddress": "string",
                  "suitPropertyDescription": "string|null",
                  "surveyNumber": "string|null",
                  "village": "string|null",
                  "tehsil": "string|null",
                  "district": "string|null",
                  "area": "string|null",
                  "facts": ["string"],
                  "grounds": ["string"],
                  "reliefsSought": ["string"],
                  "prayer": "string",
                  "valuation": "string|null",
                  "causeOfActionDate": "YYYY-MM-DD|null",
                  "causeOfAction": "string|null",
                  "territorialJurisdiction": "string|null",
                  "limitationPleading": "string|null",
                  "courtFeePleading": "string|null",
                  "place": "string",
                  "date": "YYYY-MM-DD",
                  "advocateName": "string",
                  "advocateEnrollmentNo": "string|null"
                }
                """,
            "CivilStageAApplication" => """
                {
                  "courtName": "string|null",
                  "caseTitle": "string|null",
                  "caseNumber": "string|null",
                  "applicationTitle": "string",
                  "provision": "string|null",
                  "applicantName": "string|null",
                  "respondentName": "string|null",
                  "applicationType": "string",
                  "factualBackground": ["string"],
                  "grounds": ["string"],
                  "documentsReliedOn": ["string"],
                  "prayer": "string",
                  "supportingAffidavit": "string|null",
                  "place": "string|null",
                  "date": "YYYY-MM-DD|null",
                  "advocateName": "string|null",
                  "advocateEnrollmentNo": "string|null"
                }
                """,
            "CivilStageAProposedNotice" => """
                {
                  "courtName": "string|null",
                  "caseTitle": "string|null",
                  "caseNumber": "string|null",
                  "plaintiffName": "string|null",
                  "defendantName": "string|null",
                  "defendantAddress": "string|null",
                  "noticeTitle": "string",
                  "subject": "string|null",
                  "noticeParagraphs": ["string"],
                  "evidenceReferences": ["string"],
                  "responseDirection": "string|null",
                  "place": "string|null",
                  "date": "YYYY-MM-DD|null",
                  "advocateName": "string|null",
                  "advocateEnrollmentNo": "string|null"
                }
                """,
            "CivilStageBServiceApplication" => """
                {
                  "courtName": "string|null",
                  "caseTitle": "string|null",
                  "caseNumber": "string|null",
                  "applicationTitle": "string",
                  "provision": "string|null",
                  "applicantName": "string|null",
                  "respondentName": "string|null",
                  "applicationType": "string",
                  "defendantServiceAddress": "string|null",
                  "factualBackground": ["string"],
                  "grounds": ["string"],
                  "documentsReliedOn": ["string"],
                  "prayer": "string",
                  "supportingAffidavit": "string|null",
                  "place": "string|null",
                  "date": "YYYY-MM-DD|null",
                  "advocateName": "string|null",
                  "advocateEnrollmentNo": "string|null"
                }
                """,
            "CivilStageCDefaultProcedureApplication" => """
                {
                  "courtName": "string|null",
                  "caseTitle": "string|null",
                  "caseNumber": "string|null",
                  "applicationTitle": "string",
                  "provision": "string|null",
                  "applicantName": "string|null",
                  "respondentName": "string|null",
                  "applicationType": "string",
                  "factualBackground": ["string"],
                  "grounds": ["string"],
                  "documentsReliedOn": ["string"],
                  "prayer": "string",
                  "supportingAffidavit": "string|null",
                  "place": "string|null",
                  "date": "YYYY-MM-DD|null",
                  "advocateName": "string|null",
                  "advocateEnrollmentNo": "string|null"
                }
                """,
            "CivilSupportingAffidavit" => """
                {
                  "affidavitText": "string"
                }
                """,
            "ComplaintNonRegistrationFIR" => """
                {
                  "courtName": "string",
                  "caseTitle": "string",
                  "caseNumber": "string|null",
                  "complainantName": "string",
                  "complainantAddress": "string",
                  "accusedName": "string",
                  "accusedAddress": "string|null",
                  "policeStation": "string",
                  "district": "string",
                  "dateOfIncident": "YYYY-MM-DD|null",
                  "dateOfComplaintToPolice": "YYYY-MM-DD|null",
                  "dateOfRefusal": "YYYY-MM-DD|null",
                  "sections": ["string"],
                  "facts": ["string"],
                  "grounds": ["string"],
                  "prayer": "string",
                  "place": "string",
                  "date": "YYYY-MM-DD",
                  "advocateName": "string",
                  "advocateEnrollmentNo": "string|null"
                }
                """,
            _ => """
                {
                  "title": "string",
                  "parties": {},
                  "bodySections": {},
                  "prayer": "string",
                  "date": "YYYY-MM-DD"
                }
                """
        };

        var extra = request.ExtraFields is null
            ? ""
            : "\nAdditional fields provided by user:\n" +
              string.Join("\n", request.ExtraFields.Select(kv => $"{kv.Key}: {kv.Value}"));

        // Special guidance when user has already chosen options from dropdowns
        var extraGuidance = "";
        if (request.DocumentType == "AbsenceApplication" && request.ExtraFields != null)
        {
            if (request.ExtraFields.TryGetValue("selectedGroundCodes", out var codes) && !string.IsNullOrWhiteSpace(codes))
            {
                extraGuidance = $"""
                    
                    IMPORTANT – The user has already selected the following ground codes from the standard dropdown:
                    {codes}
                    Use these codes in "selectedGroundCodes". Also fill "selectedGroundLabels" with the corresponding human-readable labels.
                    Expand them into proper formal paragraphs in the "grounds" array.
                    If a code is "OTHER", rely on the free-text reason provided.
                    """;
            }
        }
        if (request.DocumentType == "CertifiedCopyApplication" && request.ExtraFields != null)
        {
            if (request.ExtraFields.TryGetValue("selectedDocumentCodes", out var docCodes) && !string.IsNullOrWhiteSpace(docCodes))
            {
                extraGuidance += $"""
                    
                    IMPORTANT – The user has already selected the following document codes from the standard dropdown:
                    {docCodes}
                    Use these codes in "selectedDocumentCodes". Also fill "selectedDocumentLabels" with the corresponding human-readable labels.
                    If a code is "OTHER", use the free-text additionalDocuments field.
                    """;
            }
        }

        var documentGuidance = request.DocumentType switch
        {
                        "BailApplication" => """
                            Draft the selected bail application type using the supplied case particulars, allegations, custody status, grounds, prayer, and supporting-document text.
                            Preserve names, court, case/FIR numbers, police station, district, dates, and statutory sections exactly as supplied. Do not invent arrest/custody events, criminal antecedents, procedural history, legal provisions, or facts from an uploaded record that are not present in the supplied text.
                            Organize the supplied bail grounds into clear, case-specific paragraphs. If a necessary fact is absent or uncertain, leave it blank or use a clear placeholder for advocate review rather than assuming it.
                            """,
            "CivilPlaint" => """
              For a CivilPlaint, format the content as an Indian civil suit plaint with:
              - suitTitle as a concise heading such as "SUIT FOR RECOVERY OF MONEY"; do not invent an amount.
              - facts as individually numbered factual paragraphs in chronological order.
              - causeOfAction as a concise pleading based only on the supplied events; include every supported event/date.
              - territorialJurisdiction as a pleading only where the supplied facts support it.
              - limitationPleading and courtFeePleading only where supported; otherwise return null, not an unsupported legal conclusion.
              - reliefsSought as separate, specific reliefs; prayer as the formal concluding request.
              Do not invent amounts, party capacities, dates, acknowledgements, notices, payments, jurisdictional facts, limitation conclusions, or court-fee payment.
              """,
            "CivilStageAApplication" => """
              Draft the selected Stage A civil procedural application using only the supplied plaint, case facts, user-supplied application facts, and documents.
              Use the exact application type and provision supplied by the user; do not substitute another remedy or invent legal provisions.
              Do not assert that evidence exists, that notice was given, or that a document is annexed unless the supplied facts or uploaded case documents establish it.
              Treat annexure suggestions as case-dependent, not universal filing requirements. Identify only documents actually available in "documentsReliedOn"; leave unsupported details null or explicitly state what the advocate must complete.
              For an application for attachment before judgment, do not treat non-payment alone as proof of an intent to obstruct or delay execution.
              """,
            "CivilStageAProposedNotice" => """
              Prepare a proposed court notice to the defendant concerning the plaintiff's urgent interim-relief application in the pending civil suit.
              Use the supplied plaint, attached supporting affidavit, and especially the extracted text from the uploaded "Evidence of the threatened or continuing harm" document. Extract and accurately summarize only the relevant events, dates, persons, and threatened or continuing harm supported by that text. Refer to the evidence by its supplied filename or description when available.
              Do not invent a hearing date, deadline, court direction, service mode, address, event, or finding. Use a clear placeholder or omit a detail that is not in the records. Do not write as if the court has granted relief or determined the merits. Keep the notice neutral and state that it is proposed for advocate/court review.
              Return concise numbered notice paragraphs, evidenceReferences listing only documents actually supplied, and responseDirection with a placeholder where the court has not fixed a date or direction. Do not include unsupported legal conclusions or fabricated citations.
              """,
            "CivilStageBServiceApplication" => """
              Draft an application or request for directions about service of summons in an existing civil suit, using only the selected Stage B step and supplied case facts and records.
              Use the exact service step and provision supplied; do not invent court orders, service attempts, returned summons, addresses, or a defendant's evasion.
              For first-time issuance of summons, follow the structure specified in the supplied instructions, use Plaintiff/Defendant labels and include the supplied complete service address in "defendantServiceAddress". Do not claim prior service failure, request re-issuance, or invent case number, counsel details, amount, dates, or a service mode.
              For substituted service under Order V Rule 20, state the specific ordinary-service attempts and their documented results; leave unsupported particulars as placeholders and ask the court for an order. Do not imply that substituted service is automatic.
              For alternative modes, request permission/directions where applicable and do not assert service by an unapproved method.
              Refer to service records and documents only when uploaded or otherwise established. Never claim that copies of the plaint or annexures are ready merely because a plaint draft exists.
              """,
            "CivilStageCDefaultProcedureApplication" => """
              Draft only the selected Stage C procedural application using the supplied scenario, provision, plaint background, user-supplied procedural facts, and case records. Use the exact procedure and provision supplied; do not switch remedies.
              For a request to proceed ex parte under Order IX Rule 6, state only the supported service and appearance facts. Do not imply that an ex parte decree follows automatically; the court must be satisfied that the applicable requirements are met.
              For participation after an ex parte order under Order IX Rule 7, use only the supplied explanation for non-appearance and current procedural status. For an application under Order IX Rule 13, use only supplied decree, summons/service, knowledge, limitation, and sufficient-cause facts. For restoration under Order IX Rule 9, use only supported dismissal and sufficient-cause facts. For delay condonation, explain only the supplied chronology and do not assume Section 5 applies or that the request is maintainable.
              Do not invent service, attendance, dates, knowledge of an order, reasons for absence, evidence, court findings, or limitation conclusions. Identify documents relied on only when present in the supplied records. Leave unknown particulars as clear placeholders and avoid unsupported legal conclusions or citations.
              """,
            "CivilSupportingAffidavit" => """
              Produce a supporting affidavit in a formal Indian civil-court format. The affidavit must be consistent with the case title, parties, court, application draft, and supplied facts.
              Put the entire court heading, case number, matter/party title, AFFIDAVIT heading, deponent introduction, numbered paragraphs, DEPONENT signature line, VERIFICATION, and verification signature line into the single "affidavitText" string.
              Do not copy illustrative sample facts. Never invent parentage, age, address, dates, amounts, service, evidence, personal knowledge, or events. Use clear bracketed placeholders for missing filing particulars and omit unsupported factual assertions. Keep the deponent's knowledge and verification appropriately limited to supplied facts. Do not add citation markers, markdown, commentary, or unsupported legal conclusions.
              """,
            _ => ""
        };

        return $"""
            Client facts extracted from uploaded documents:
            ---
            {request.ExtractedClientFacts}
            ---
            {extra}
            {extraGuidance}
            {documentGuidance}

            Output-language requirement: Write every human-readable string value in {request.OutputLanguage}. When Hindi is requested, use formal Hindi in Devanagari. Keep all JSON property names exactly as shown in the schema.

            Produce a complete {request.DocumentType} as JSON matching this exact structure:
            {schemaExample}
            """;
    }
}
