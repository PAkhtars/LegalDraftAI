-- LegalDraftAI PostgreSQL schema.
-- Create the database first (for example: CREATE DATABASE "LegalDraftAI";),
-- connect to it, then execute this file. Identifiers are quoted to match EF Core's
-- default PascalCase table and column names.

CREATE TABLE IF NOT EXISTS "Cases" (
    "Id" uuid NOT NULL PRIMARY KEY,
    "Title" character varying(500) NOT NULL,
    "CaseCategory" character varying(50) NOT NULL DEFAULT 'Civil',
    "PlaintNature" character varying(100),
    "Jurisdiction" character varying(100) NOT NULL DEFAULT '',
    "CourtName" character varying(300),
    "CaseNumber" character varying(100),
    "Status" character varying(50) NOT NULL DEFAULT 'Open',
    "PlaintiffName" character varying(300),
    "PlaintiffAddress" text,
    "DefendantName" character varying(300),
    "DefendantAddress" text,
    "ClientFacts" text,
    "Notes" text,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    "UpdatedAtUtc" timestamp with time zone
);
CREATE INDEX IF NOT EXISTS "IX_Cases_CaseNumber" ON "Cases" ("CaseNumber");
CREATE INDEX IF NOT EXISTS "IX_Cases_Status" ON "Cases" ("Status");
CREATE INDEX IF NOT EXISTS "IX_Cases_CreatedAtUtc" ON "Cases" ("CreatedAtUtc");

CREATE TABLE IF NOT EXISTS "DraftFormatMasters" (
    "Id" uuid NOT NULL PRIMARY KEY,
    "Category" character varying(20) NOT NULL,
    "FormatName" character varying(200) NOT NULL,
    "Description" character varying(1000),
    "DraftFormatPath" character varying(1000) NOT NULL,
    "OriginalFileName" character varying(500) NOT NULL,
    "ContentType" character varying(200) NOT NULL,
    "FileSizeBytes" bigint NOT NULL,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    "UpdatedAtUtc" timestamp with time zone
);
CREATE UNIQUE INDEX IF NOT EXISTS "IX_DraftFormatMasters_Category_FormatName"
    ON "DraftFormatMasters" ("Category", "FormatName");

CREATE TABLE IF NOT EXISTS "CivilPlaintCourtAndSuitRecords" (
    "CaseId" uuid NOT NULL PRIMARY KEY,
    "CourtName" character varying(300),
    "CaseTitle" character varying(500),
    "CaseNumber" character varying(100),
    "CauseOfActionDate" character varying(50),
    "PlaintiffName" character varying(300),
    "DefendantName" character varying(300),
    "PlaintiffAddress" character varying(2000),
    "DefendantAddress" character varying(2000),
    CONSTRAINT "FK_CivilPlaintCourtAndSuitRecords_Cases"
        FOREIGN KEY ("CaseId") REFERENCES "Cases" ("Id") ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS "CivilPlaintSuitPropertyRecords" (
    "CaseId" uuid NOT NULL PRIMARY KEY,
    "PropertyDescription" character varying(5000),
    "SurveyPlotNumber" character varying(200),
    "Area" character varying(200),
    "Village" character varying(200),
    "Tehsil" character varying(200),
    "District" character varying(200),
    "SuitValuationCourtFee" character varying(500),
    CONSTRAINT "FK_CivilPlaintSuitPropertyRecords_Cases"
        FOREIGN KEY ("CaseId") REFERENCES "Cases" ("Id") ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS "CivilPlaintFactsAndReliefRecords" (
    "CaseId" uuid NOT NULL PRIMARY KEY,
    "Facts" text,
    "LegalGrounds" character varying(10000),
    "ReliefsSought" character varying(10000),
    "Prayer" character varying(5000),
    CONSTRAINT "FK_CivilPlaintFactsAndReliefRecords_Cases"
        FOREIGN KEY ("CaseId") REFERENCES "Cases" ("Id") ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS "CivilPlaintFilingDetailsRecords" (
    "CaseId" uuid NOT NULL PRIMARY KEY,
    "Place" character varying(200),
    "Date" character varying(50),
    "AdvocateName" character varying(300),
    "EnrollmentNumber" character varying(100),
    CONSTRAINT "FK_CivilPlaintFilingDetailsRecords_Cases"
        FOREIGN KEY ("CaseId") REFERENCES "Cases" ("Id") ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS "StageApplications" (
    "Id" uuid NOT NULL PRIMARY KEY,
    "CaseId" uuid NOT NULL,
    "StageCode" character varying(20) NOT NULL,
    "ApplicationCode" character varying(100) NOT NULL,
    "DraftContent" text,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    "UpdatedAtUtc" timestamp with time zone,
    CONSTRAINT "FK_StageApplications_Cases"
        FOREIGN KEY ("CaseId") REFERENCES "Cases" ("Id") ON DELETE CASCADE
);
CREATE UNIQUE INDEX IF NOT EXISTS "IX_StageApplications_CaseId_StageCode_ApplicationCode"
    ON "StageApplications" ("CaseId", "StageCode", "ApplicationCode");

CREATE TABLE IF NOT EXISTS "CaseDrafts" (
    "Id" uuid NOT NULL PRIMARY KEY,
    "CaseId" uuid NOT NULL,
    "StageApplicationId" uuid,
    "DocumentType" character varying(100) NOT NULL,
    "Version" integer NOT NULL DEFAULT 1,
    "Status" character varying(50) NOT NULL DEFAULT 'Draft',
    "ApplicationCode" character varying(100),
    "JsonContent" text NOT NULL,
    "PdfContent" bytea,
    "PdfStoragePath" character varying(1000),
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    "UpdatedAtUtc" timestamp with time zone,
    CONSTRAINT "FK_CaseDrafts_Cases"
        FOREIGN KEY ("CaseId") REFERENCES "Cases" ("Id") ON DELETE CASCADE,
    CONSTRAINT "FK_CaseDrafts_StageApplications"
        FOREIGN KEY ("StageApplicationId") REFERENCES "StageApplications" ("Id") ON DELETE NO ACTION
);
CREATE INDEX IF NOT EXISTS "IX_CaseDrafts_CaseId" ON "CaseDrafts" ("CaseId");
CREATE INDEX IF NOT EXISTS "IX_CaseDrafts_CaseId_DocumentType_Version"
    ON "CaseDrafts" ("CaseId", "DocumentType", "Version");
CREATE INDEX IF NOT EXISTS "IX_CaseDrafts_StageApplicationId" ON "CaseDrafts" ("StageApplicationId");

CREATE TABLE IF NOT EXISTS "CaseDocuments" (
    "Id" uuid NOT NULL PRIMARY KEY,
    "CaseId" uuid NOT NULL,
    "StageApplicationId" uuid,
    "CaseDraftId" uuid,
    "DocumentCode" character varying(50) NOT NULL,
    "OriginalFileName" character varying(500) NOT NULL,
    "ContentType" character varying(200) NOT NULL,
    "FileSizeBytes" bigint NOT NULL,
    "StoragePath" character varying(1000) NOT NULL,
    "FileContent" bytea,
    "ExtractedText" text,
    "UploadedAtUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "FK_CaseDocuments_Cases"
        FOREIGN KEY ("CaseId") REFERENCES "Cases" ("Id") ON DELETE CASCADE,
    CONSTRAINT "FK_CaseDocuments_StageApplications"
        FOREIGN KEY ("StageApplicationId") REFERENCES "StageApplications" ("Id") ON DELETE NO ACTION,
    CONSTRAINT "FK_CaseDocuments_CaseDrafts"
        FOREIGN KEY ("CaseDraftId") REFERENCES "CaseDrafts" ("Id") ON DELETE NO ACTION
);
CREATE INDEX IF NOT EXISTS "IX_CaseDocuments_CaseId" ON "CaseDocuments" ("CaseId");
CREATE INDEX IF NOT EXISTS "IX_CaseDocuments_CaseId_DocumentCode"
    ON "CaseDocuments" ("CaseId", "DocumentCode");
CREATE INDEX IF NOT EXISTS "IX_CaseDocuments_CaseDraftId" ON "CaseDocuments" ("CaseDraftId");
CREATE INDEX IF NOT EXISTS "IX_CaseDocuments_StageApplicationId"
    ON "CaseDocuments" ("StageApplicationId");

-- Bring forward older Stage A draft content into the application row, if needed.
WITH latest_stage_a_application_draft AS (
    SELECT
        draft."CaseId",
        draft."ApplicationCode",
        draft."JsonContent",
        ROW_NUMBER() OVER (
            PARTITION BY draft."CaseId", draft."ApplicationCode"
            ORDER BY draft."CreatedAtUtc" DESC, draft."Id" DESC) AS row_number
    FROM "CaseDrafts" AS draft
    WHERE draft."StageApplicationId" IS NOT NULL
      AND draft."DocumentType" = 'CivilStageAApplication'
      AND draft."ApplicationCode" IS NOT NULL
)
UPDATE "StageApplications" AS application
SET "DraftContent" = latest."JsonContent"
FROM latest_stage_a_application_draft AS latest
WHERE latest."CaseId" = application."CaseId"
  AND latest."ApplicationCode" = application."ApplicationCode"
  AND latest.row_number = 1
  AND application."StageCode" = 'StageA'
  AND application."DraftContent" IS NULL;
