# Legal Draft AI

ASP.NET Core 8 Web API that helps advocates generate court-ready legal documents using Groq.

## Features

- **Document research (intake)** – required documents by plaint nature + gap analysis
- **Document extraction** – Upload PDF / DOCX / TXT and common image files (PNG, JPG, TIFF, BMP, GIF, WebP); images use local English + Hindi OCR
- **Hindi translation** – Translate extracted supporting-document text into Hindi while keeping the original text unchanged
- **AI drafting** – Groq generates structured JSON for Bail Application, Advocate Bill, etc.
- **Stage A civil applications** – Select a saved civil plaint, review an application-specific supporting-document checklist, upload outstanding documents, and save a separate procedural-application draft to the case.
- **Stage B summons service** – Prepare a first summons, fresh service, substituted service, or service-directions application linked to a saved civil suit. Review a formatted DOCX preview, omit selected generated fields, and download the resulting DOCX.
- **Stage C non-appearance/default** – Prepare an ex parte, participation, set-aside, restoration, or delay-condonation application with a tailored evidence checklist.
- **PDF export** – Convert the structured (and optionally edited) JSON into a clean A4 PDF

## Quick Start

### 1. Prerequisites

- .NET 8 SDK
- Groq API key → https://console.groq.com/keys

The API project includes English and Hindi Tesseract OCR data files in `tessdata/`; keep these files alongside the project when publishing. OCR runs locally and does not send images to the drafting provider.
The trained data files are from the official [tessdata_fast repository](https://github.com/tesseract-ocr/tessdata_fast) and are distributed under Apache 2.0.

### 2. Configure the API key

Edit `appsettings.json` (or better, use User Secrets / environment variables):

```json
"Groq": {
  "ApiKey": "your-groq-api-key",
  "Model": "openai/gpt-oss-120b"
}
```

Or set the environment variable:

```bash
export Groq__ApiKey=your-groq-api-key
```

### 3. Run

```bash
dotnet run
```

Swagger UI will be available at:  
http://localhost:5000/swagger (or the port shown in the console)

## API Endpoints

| Method | Endpoint                        | Description                                      |
|--------|---------------------------------|--------------------------------------------------|
| POST   | `/api/drafts/generate`          | Generate draft from plain text facts             |
| POST   | `/api/drafts/generate-from-files` | Upload files → extract text → generate draft   |
| GET    | `/api/drafts/absence-grounds`   | Dropdown options for exemption grounds           |
| GET    | `/api/intake/plaint-natures`    | List civil plaint types for intake               |
| GET    | `/api/intake/documents/{nature}`| Document checklist for a plaint nature           |
| POST   | `/api/intake/research`          | Required docs + gap analysis before drafting     |
| GET    | `/api/drafts/court-documents`   | Multi-select options for certified copy docs     |
| POST   | `/api/drafts/export-pdf`        | Convert final JSON into a downloadable PDF       |
| POST   | `/api/drafts/export-stage-b-service-docx` | Export a reviewed Stage B service application as DOCX |
| POST   | `/api/drafts/export-affidavit-pdf` | Convert reviewed affidavit text into a downloadable PDF |

### Example – Generate from text

```http
POST /api/drafts/generate
Content-Type: application/json

{
  "documentType": "BailApplication",
  "jurisdiction": "Delhi",
  "extractedClientFacts": "Applicant Ramesh Kumar, FIR No. 123/2025 at PS Connaught Place under sections 420, 406 BNS. Arrested on 10-Oct-2025. No previous criminal record. Permanent resident of Delhi..."
}
```

### Example – Generate from files

```http
POST /api/drafts/generate-from-files
Content-Type: multipart/form-data

documentType=BailApplication
jurisdiction=Delhi
files=@client_statement.pdf
files=@fir_copy.pdf
```

### Example – Export PDF

```http
POST /api/drafts/export-pdf
Content-Type: application/json

{
  "documentType": "BailApplication",
  "json": "{ ... the final edited JSON from previous step ... }"
}
```

## Supported Document Types (current)

- `BailApplication`
- `AdvocateBill`
- `AbsenceApplication` (Application for Exemption from Personal Appearance / Absence on Hearing Date)
- `SummonApplication` (Application for Summoning the Opposite / Counter Party)
- `CertifiedCopyApplication` (Application for Certified Copies of FIR / Chargesheet / Plaint etc.)
- `CurativePetition` (Curative Petition before the Supreme Court of India)
- `ComplaintNonRegistrationFIR` (Complaint before Magistrate for non-registration of FIR – Sec 156(3) BNSS/CrPC)
- `CivilPlaint` (Civil suit plaint – title, possession, injunction, money, partition, etc.)
- `CivilStageAApplication` (post-plaint, pre-summons applications listed on the Stage A page; exportable as PDF)
- `CivilStageBServiceApplication` (first-time or fresh summons, substituted service, alternative service mode, or directions concerning service; exportable as PDF)
- `CivilStageCDefaultProcedureApplication` (proceed ex parte, defendant's participation after an ex parte order, setting aside an ex parte decree, restoration after dismissal for default, or condonation of delay; exportable as PDF)

The Stage A page is available at `/CivilApplications`. Its checklist is a research aid based on the supplied Stage A source document and the cited Code of Civil Procedure provisions; state amendments, court directions, and local filing rules may change what must accompany a filing. Already-uploaded case documents are identified and are not requested again. The workflow generates the supporting affidavit first, lets the user review/edit and download it as a PDF or attach it to the case, and only then allows application generation. For urgent interim relief, a separate proposed notice can be generated from extracted text in the uploaded evidence of threatened or continuing harm and downloaded as PDF. The notice remains a proposed draft, not a court-issued notice or order. Unavailable affidavit particulars remain placeholders. Review every assertion, the specific provision, and local rules before filing or swearing an affidavit.

The Stage B summons-service page is available at `/CivilSummonsService`. It selects an existing civil suit, researches a service-specific document checklist, supports upload/removal of service records, and saves the generated application to the case. The generated output is shown in a formatted DOCX preview alongside every JSON output field and its display name. Selecting a field to exclude removes it from both the preview and downloaded DOCX; the saved source draft remains unchanged. The original PDF export is also available. First-time issuance of summons is a separate option from re-issuance after failed service and requires the defendant's complete service address. The checklist is a research aid; the court decides whether substituted or other alternative service is justified and permitted.

The Stage C default-procedure page is available at `/CivilDefaultProcedure`. It supports Order IX Rule 6 requests to proceed ex parte, Order IX Rule 7 participation requests, Order IX Rule 13 applications to set aside an ex parte decree, Order IX Rule 9 restoration applications, and a separate Section 5 Limitation Act delay-condonation option where applicable. The workflow supports application-specific document upload/removal, per-document context exclusions, draft generation, and PDF export. It uses the existing `StageApplications`, `CaseDrafts`, and `CaseDocuments` schema; StageC records are linked by stage and application code. Non-appearance does not automatically result in an ex parte decree, and limitation and maintainability require case-specific verification.

You can easily add more by extending the system prompt + JSON schema in `GroqDraftService` and adding a PDF template in `PdfExportService`.


## Case storage (MSSQL)

Cases, uploaded documents, and generated drafts are persisted.

| Method | Endpoint | Purpose |
|--------|----------|---------|
| POST | `/api/cases` | Create case |
| GET | `/api/cases` | List cases |
| GET | `/api/cases/{id}` | Case detail + docs + drafts |
| GET    | `/api/cases?documentType=CivilPlaint` | Civil cases with a saved plaint draft |
| PUT | `/api/cases/{id}` | Update metadata |
| POST | `/api/cases/{id}/documents` | Upload file (`documentCode` + file) |
| DELETE | `/api/cases/{id}/documents/{documentId}` | Remove an attached document and any legacy stored file |
| POST | `/api/cases/{id}/research` | Checklist / gaps from stored docs |
| POST | `/api/cases/{id}/drafts` | Generate & save draft (+ optional PDF) |
| GET | `/api/cases/{id}/drafts/{draftId}` | Saved JSON |
| GET | `/api/cases/{id}/drafts/{draftId}/pdf` | Download PDF |

**Connection string** in `appsettings.json`:

```json
"ConnectionStrings": {
  "DefaultConnection": "Server=localhost,1433;Database=LegalDraftAI;User Id=sa;Password=...;TrustServerCertificate=True;Encrypt=False"
}
```

Schema is created on startup via EF `EnsureCreated()`. Startup also adds the SQL Server columns for document bytes, generated PDF bytes, application codes, and stage-application relationships to an existing database. Alternatively, run `Data/SqlServer_Schema.sql` manually.

Stage-wise application data is stored in `dbo.StageApplications`, keyed by case, stage (`StageA` or `StageB`), and catalog application code. `dbo.CaseDocuments.StageApplicationId` associates each uploaded supporting document with its stage application; `dbo.CaseDrafts.StageApplicationId` associates generated drafts with the same application. Generated stage PDFs are saved physically under `App_Data/storage/cases/{caseId}/drafts/` (or `Storage:RootPath`) and the path relative to the configured storage root is stored in `CaseDrafts.PdfStoragePath`. A matching `CaseDocuments` row uses `DocumentCode = GENERATED_DRAFT_PDF`, stores the PDF bytes in `FileContent`, the linked draft in `CaseDraftId`, and the same file path in `StoragePath`. Older stage drafts without a physical PDF are generated and saved on first download. Case-detail responses include the stage application records and association IDs on each document and draft.


## Project Structure

```
LegalDraftAI/
├── Controllers/
│   └── DraftsController.cs
├── Models/
│   ├── GroqOptions.cs
│   └── DraftModels.cs
├── Services/
│   ├── DocumentExtractionService.cs
│   ├── GroqDraftService.cs
│   └── PdfExportService.cs
├── Program.cs
├── appsettings.json
└── README.md
```

## Important Notes for Production

1. **Never** put the Groq API key in source control. Use Azure Key Vault, AWS Secrets Manager, or environment variables.
2. Always keep a **human-in-the-loop** review step – the advocate must verify the generated draft before filing.
3. Log token usage per client so you can bill for AI costs.
4. For scanned (image-only) PDFs you will need an OCR service (Azure Document Intelligence, Google Vision, or Tesseract).
5. Tighten CORS and add authentication (JWT / Azure AD) before deploying publicly.

## License

This sample is provided as-is for educational and internal use.
#   L e g a l D r a f t A I  
 