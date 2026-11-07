-- LegalDraftAI – MSSQL schema (also created by EF EnsureCreated / Migrate)
-- Database: LegalDraftAI

IF NOT EXISTS (SELECT * FROM sys.databases WHERE name = N'LegalDraftAI')
BEGIN
    CREATE DATABASE LegalDraftAI;
END
GO

USE LegalDraftAI;
GO

IF OBJECT_ID(N'dbo.Cases', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Cases (
        Id                UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        Title             NVARCHAR(500)  NOT NULL,
        CaseCategory      NVARCHAR(50)   NOT NULL DEFAULT N'Civil',
        PlaintNature      NVARCHAR(100)  NULL,
        Jurisdiction      NVARCHAR(100)  NOT NULL DEFAULT N'',
        CourtName         NVARCHAR(300)  NULL,
        CaseNumber        NVARCHAR(100)  NULL,
        Status            NVARCHAR(50)   NOT NULL DEFAULT N'Open',
        PlaintiffName     NVARCHAR(300)  NULL,
        PlaintiffAddress  NVARCHAR(MAX)  NULL,
        DefendantName     NVARCHAR(300)  NULL,
        DefendantAddress  NVARCHAR(MAX)  NULL,
        ClientFacts       NVARCHAR(MAX)  NULL,
        Notes             NVARCHAR(MAX)  NULL,
        CreatedAtUtc      DATETIME2      NOT NULL,
        UpdatedAtUtc      DATETIME2      NULL
    );
    CREATE INDEX IX_Cases_CaseNumber ON dbo.Cases(CaseNumber);
    CREATE INDEX IX_Cases_Status ON dbo.Cases(Status);
    CREATE INDEX IX_Cases_CreatedAtUtc ON dbo.Cases(CreatedAtUtc);
END
GO

IF OBJECT_ID(N'dbo.DraftFormatMasters', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.DraftFormatMasters (
        Id UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        Category NVARCHAR(20) NOT NULL,
        FormatName NVARCHAR(200) NOT NULL,
        Description NVARCHAR(1000) NULL,
        DraftFormatPath NVARCHAR(1000) NOT NULL,
        OriginalFileName NVARCHAR(500) NOT NULL,
        ContentType NVARCHAR(200) NOT NULL,
        FileSizeBytes BIGINT NOT NULL,
        CreatedAtUtc DATETIMEOFFSET NOT NULL,
        UpdatedAtUtc DATETIMEOFFSET NULL
    );
    CREATE UNIQUE INDEX IX_DraftFormatMasters_Category_FormatName
        ON dbo.DraftFormatMasters(Category, FormatName);
END
GO

IF COL_LENGTH(N'dbo.DraftFormatMasters', N'DraftFormatPath') IS NULL
    ALTER TABLE dbo.DraftFormatMasters ADD DraftFormatPath NVARCHAR(1000) NOT NULL
        CONSTRAINT DF_DraftFormatMasters_DraftFormatPath DEFAULT N'';
GO

IF COL_LENGTH(N'dbo.DraftFormatMasters', N'OriginalFileName') IS NULL
    ALTER TABLE dbo.DraftFormatMasters ADD OriginalFileName NVARCHAR(500) NOT NULL
        CONSTRAINT DF_DraftFormatMasters_OriginalFileName DEFAULT N'';
GO

IF COL_LENGTH(N'dbo.DraftFormatMasters', N'ContentType') IS NULL
    ALTER TABLE dbo.DraftFormatMasters ADD ContentType NVARCHAR(200) NOT NULL
        CONSTRAINT DF_DraftFormatMasters_ContentType
        DEFAULT N'application/vnd.openxmlformats-officedocument.wordprocessingml.document';
GO

IF COL_LENGTH(N'dbo.DraftFormatMasters', N'FileSizeBytes') IS NULL
    ALTER TABLE dbo.DraftFormatMasters ADD FileSizeBytes BIGINT NOT NULL
        CONSTRAINT DF_DraftFormatMasters_FileSizeBytes DEFAULT 0;
GO

IF COL_LENGTH(N'dbo.DraftFormatMasters', N'DraftContent') IS NOT NULL
    ALTER TABLE dbo.DraftFormatMasters ALTER COLUMN DraftContent NVARCHAR(MAX) NULL;
GO

IF OBJECT_ID(N'dbo.CivilPlaintCourtAndSuitRecords', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.CivilPlaintCourtAndSuitRecords (
        CaseId UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        CourtName NVARCHAR(300) NULL,
        CaseTitle NVARCHAR(500) NULL,
        CaseNumber NVARCHAR(100) NULL,
        CauseOfActionDate NVARCHAR(50) NULL,
        PlaintiffName NVARCHAR(300) NULL,
        DefendantName NVARCHAR(300) NULL,
        PlaintiffAddress NVARCHAR(2000) NULL,
        DefendantAddress NVARCHAR(2000) NULL,
        CONSTRAINT FK_CivilPlaintCourtAndSuitRecords_Cases
            FOREIGN KEY (CaseId) REFERENCES dbo.Cases(Id) ON DELETE CASCADE
    );
END
GO

IF OBJECT_ID(N'dbo.CivilPlaintSuitPropertyRecords', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.CivilPlaintSuitPropertyRecords (
        CaseId UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        PropertyDescription NVARCHAR(MAX) NULL,
        SurveyPlotNumber NVARCHAR(200) NULL,
        Area NVARCHAR(200) NULL,
        Village NVARCHAR(200) NULL,
        Tehsil NVARCHAR(200) NULL,
        District NVARCHAR(200) NULL,
        SuitValuationCourtFee NVARCHAR(500) NULL,
        CONSTRAINT FK_CivilPlaintSuitPropertyRecords_Cases
            FOREIGN KEY (CaseId) REFERENCES dbo.Cases(Id) ON DELETE CASCADE
    );
END
GO

IF OBJECT_ID(N'dbo.CivilPlaintFactsAndReliefRecords', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.CivilPlaintFactsAndReliefRecords (
        CaseId UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        Facts NVARCHAR(MAX) NULL,
        LegalGrounds NVARCHAR(MAX) NULL,
        ReliefsSought NVARCHAR(MAX) NULL,
        Prayer NVARCHAR(MAX) NULL,
        CONSTRAINT FK_CivilPlaintFactsAndReliefRecords_Cases
            FOREIGN KEY (CaseId) REFERENCES dbo.Cases(Id) ON DELETE CASCADE
    );
END
GO

IF OBJECT_ID(N'dbo.CivilPlaintFilingDetailsRecords', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.CivilPlaintFilingDetailsRecords (
        CaseId UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        Place NVARCHAR(200) NULL,
        [Date] NVARCHAR(50) NULL,
        AdvocateName NVARCHAR(300) NULL,
        EnrollmentNumber NVARCHAR(100) NULL,
        CONSTRAINT FK_CivilPlaintFilingDetailsRecords_Cases
            FOREIGN KEY (CaseId) REFERENCES dbo.Cases(Id) ON DELETE CASCADE
    );
END
GO

IF OBJECT_ID(N'dbo.StageApplications', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.StageApplications (
        Id              UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        CaseId          UNIQUEIDENTIFIER NOT NULL,
        StageCode       NVARCHAR(20)  NOT NULL,
        ApplicationCode NVARCHAR(100) NOT NULL,
        DraftContent    NVARCHAR(MAX) NULL,
        CreatedAtUtc    DATETIME2     NOT NULL,
        UpdatedAtUtc    DATETIME2     NULL,
        CONSTRAINT FK_StageApplications_Cases
            FOREIGN KEY (CaseId) REFERENCES dbo.Cases(Id) ON DELETE CASCADE
    );
    CREATE UNIQUE INDEX IX_StageApplications_CaseId_StageCode_ApplicationCode
        ON dbo.StageApplications(CaseId, StageCode, ApplicationCode);
END
GO

IF COL_LENGTH(N'dbo.StageApplications', N'DraftContent') IS NULL
    ALTER TABLE dbo.StageApplications ADD DraftContent NVARCHAR(MAX) NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.StageApplications')
        AND name = N'IX_StageApplications_CaseId_StageCode_ApplicationCode')
    CREATE UNIQUE INDEX IX_StageApplications_CaseId_StageCode_ApplicationCode
        ON dbo.StageApplications(CaseId, StageCode, ApplicationCode);
GO

IF OBJECT_ID(N'dbo.CaseDocuments', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.CaseDocuments (
        Id               UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        CaseId           UNIQUEIDENTIFIER NOT NULL,
        StageApplicationId UNIQUEIDENTIFIER NULL,
        CaseDraftId      UNIQUEIDENTIFIER NULL,
        DocumentCode     NVARCHAR(50)  NOT NULL,
        OriginalFileName NVARCHAR(500) NOT NULL,
        ContentType      NVARCHAR(200) NOT NULL,
        FileSizeBytes    BIGINT        NOT NULL,
        StoragePath      NVARCHAR(1000) NOT NULL,
        FileContent      VARBINARY(MAX) NULL,
        ExtractedText    NVARCHAR(MAX) NULL,
        UploadedAtUtc    DATETIME2     NOT NULL,
        CONSTRAINT FK_CaseDocuments_Cases FOREIGN KEY (CaseId) REFERENCES dbo.Cases(Id) ON DELETE CASCADE
    );
    CREATE INDEX IX_CaseDocuments_CaseId ON dbo.CaseDocuments(CaseId);
    CREATE INDEX IX_CaseDocuments_CaseId_Code ON dbo.CaseDocuments(CaseId, DocumentCode);
    CREATE INDEX IX_CaseDocuments_CaseDraftId ON dbo.CaseDocuments(CaseDraftId);
END
GO

IF OBJECT_ID(N'dbo.CaseDrafts', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.CaseDrafts (
        Id             UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        CaseId         UNIQUEIDENTIFIER NOT NULL,
        StageApplicationId UNIQUEIDENTIFIER NULL,
        DocumentType   NVARCHAR(100)  NOT NULL,
        Version        INT            NOT NULL DEFAULT 1,
        Status         NVARCHAR(50)   NOT NULL DEFAULT N'Draft',
        ApplicationCode NVARCHAR(100) NULL,
        JsonContent    NVARCHAR(MAX)  NOT NULL,
        PdfContent     VARBINARY(MAX) NULL,
        PdfStoragePath NVARCHAR(1000) NULL,
        CreatedAtUtc   DATETIME2      NOT NULL,
        UpdatedAtUtc   DATETIME2      NULL,
        CONSTRAINT FK_CaseDrafts_Cases FOREIGN KEY (CaseId) REFERENCES dbo.Cases(Id) ON DELETE CASCADE
    );
    CREATE INDEX IX_CaseDrafts_CaseId ON dbo.CaseDrafts(CaseId);
    CREATE INDEX IX_CaseDrafts_Case_Type_Ver ON dbo.CaseDrafts(CaseId, DocumentType, Version);
END
GO

IF COL_LENGTH(N'dbo.CaseDocuments', N'FileContent') IS NULL
    ALTER TABLE dbo.CaseDocuments ADD FileContent VARBINARY(MAX) NULL;
GO

IF COL_LENGTH(N'dbo.CaseDrafts', N'ApplicationCode') IS NULL
    ALTER TABLE dbo.CaseDrafts ADD ApplicationCode NVARCHAR(100) NULL;
GO

IF COL_LENGTH(N'dbo.CaseDrafts', N'PdfContent') IS NULL
    ALTER TABLE dbo.CaseDrafts ADD PdfContent VARBINARY(MAX) NULL;
GO

IF COL_LENGTH(N'dbo.CaseDocuments', N'StageApplicationId') IS NULL
    ALTER TABLE dbo.CaseDocuments ADD StageApplicationId UNIQUEIDENTIFIER NULL;
GO

IF COL_LENGTH(N'dbo.CaseDocuments', N'CaseDraftId') IS NULL
    ALTER TABLE dbo.CaseDocuments ADD CaseDraftId UNIQUEIDENTIFIER NULL;
GO

IF COL_LENGTH(N'dbo.CaseDrafts', N'StageApplicationId') IS NULL
    ALTER TABLE dbo.CaseDrafts ADD StageApplicationId UNIQUEIDENTIFIER NULL;
GO

;WITH LatestStageAApplicationDraft AS (
    SELECT
        draft.CaseId,
        draft.ApplicationCode,
        draft.JsonContent,
        ROW_NUMBER() OVER (
            PARTITION BY draft.CaseId, draft.ApplicationCode
            ORDER BY draft.CreatedAtUtc DESC, draft.Id DESC) AS RowNumber
    FROM dbo.CaseDrafts AS draft
    WHERE draft.StageApplicationId IS NOT NULL
        AND draft.DocumentType = N'CivilStageAApplication'
        AND draft.ApplicationCode IS NOT NULL
)
UPDATE application
SET DraftContent = latest.JsonContent
FROM dbo.StageApplications AS application
INNER JOIN LatestStageAApplicationDraft AS latest
    ON latest.CaseId = application.CaseId
    AND latest.ApplicationCode = application.ApplicationCode
    AND latest.RowNumber = 1
WHERE application.StageCode = N'StageA'
    AND application.DraftContent IS NULL;
GO

IF OBJECT_ID(N'dbo.FK_CaseDocuments_StageApplications', N'F') IS NULL
    ALTER TABLE dbo.CaseDocuments ADD CONSTRAINT FK_CaseDocuments_StageApplications
        FOREIGN KEY (StageApplicationId) REFERENCES dbo.StageApplications(Id);
GO

IF OBJECT_ID(N'dbo.FK_CaseDrafts_StageApplications', N'F') IS NULL
    ALTER TABLE dbo.CaseDrafts ADD CONSTRAINT FK_CaseDrafts_StageApplications
        FOREIGN KEY (StageApplicationId) REFERENCES dbo.StageApplications(Id);
GO

IF OBJECT_ID(N'dbo.FK_CaseDocuments_CaseDrafts', N'F') IS NULL
    ALTER TABLE dbo.CaseDocuments ADD CONSTRAINT FK_CaseDocuments_CaseDrafts
        FOREIGN KEY (CaseDraftId) REFERENCES dbo.CaseDrafts(Id);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.CaseDocuments')
        AND name = N'IX_CaseDocuments_StageApplicationId')
    CREATE INDEX IX_CaseDocuments_StageApplicationId
        ON dbo.CaseDocuments(StageApplicationId);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.CaseDrafts')
        AND name = N'IX_CaseDrafts_StageApplicationId')
    CREATE INDEX IX_CaseDrafts_StageApplicationId
        ON dbo.CaseDrafts(StageApplicationId);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.CaseDocuments')
        AND name = N'IX_CaseDocuments_CaseDraftId')
    CREATE INDEX IX_CaseDocuments_CaseDraftId
        ON dbo.CaseDocuments(CaseDraftId);
GO
