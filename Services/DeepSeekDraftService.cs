using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using LegalDraftAI.Models;
using Microsoft.Extensions.Options;

namespace LegalDraftAI.Services;

public class DeepSeekDraftService : IDeepSeekDraftService
{
    private readonly HttpClient _http;
    private readonly DeepSeekOptions _options;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public DeepSeekDraftService(HttpClient http, IOptions<DeepSeekOptions> options)
    {
        _http = http;
        _options = options.Value;

        if (string.IsNullOrWhiteSpace(_options.ApiKey) || _options.ApiKey == "YOUR_DEEPSEEK_API_KEY")
        {
            // Will fail at runtime with a clear message if key is missing
        }

        _http.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", _options.ApiKey);
    }

    public async Task<DraftResult> GenerateStructuredDraftAsync(
        DraftRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey) || _options.ApiKey == "YOUR_DEEPSEEK_API_KEY")
        {
            return new DraftResult
            {
                Success = false,
                Error = "DeepSeek API key is not configured. Set DeepSeek:ApiKey in appsettings.json or environment variables."
            };
        }

        var systemPrompt = BuildSystemPrompt(request.DocumentType, request.Jurisdiction);
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
            max_tokens = 8192,
            thinking = new { type = "disabled" }
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
                Error = $"DeepSeek API error {(int)response.StatusCode}: {responseText}"
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

    private static string BuildSystemPrompt(string documentType, string jurisdiction)
    {
        return $"""
            You are an expert Indian advocate drafting court-ready legal documents.
            Jurisdiction: {jurisdiction}.
            Document type: {documentType}.

            Output MUST be a single valid JSON object only. No markdown, no explanation, no extra text.

            Follow these strict rules:
            1. Use formal legal language appropriate for Indian courts.
            2. Include all standard sections for this document type.
            3. Leave fields that cannot be filled from the provided facts as null.
            4. Never invent case numbers, dates, or party names that are not in the facts.
            5. Cite BNS / BNSS / relevant statutes only when the facts support it.
            6. Structure the JSON exactly as shown in the user message example.
            """;
    }

    private static string BuildUserPrompt(DraftRequest request)
    {
        var schemaExample = request.DocumentType switch
        {
            "BailApplication" => """
                {
                  "courtName": "string",
                  "caseTitle": "string",
                  "caseNumber": "string",
                  "applicantName": "string",
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

        var documentGuidance = request.DocumentType == "CivilPlaint"
            ? """
              For a CivilPlaint, format the content as an Indian civil suit plaint with:
              - suitTitle as a concise heading such as "SUIT FOR RECOVERY OF MONEY"; do not invent an amount.
              - facts as individually numbered factual paragraphs in chronological order.
              - causeOfAction as a concise pleading based only on the supplied events; include every supported event/date.
              - territorialJurisdiction as a pleading only where the supplied facts support it.
              - limitationPleading and courtFeePleading only where supported; otherwise return null, not an unsupported legal conclusion.
              - reliefsSought as separate, specific reliefs; prayer as the formal concluding request.
              Do not invent amounts, party capacities, dates, acknowledgements, notices, payments, jurisdictional facts, limitation conclusions, or court-fee payment.
              """
            : "";

        return $"""
            Client facts extracted from uploaded documents:
            ---
            {request.ExtractedClientFacts}
            ---
            {extra}
            {extraGuidance}
            {documentGuidance}

            Produce a complete {request.DocumentType} as JSON matching this exact structure:
            {schemaExample}
            """;
    }
}
