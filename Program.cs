using LegalDraftAI.Data;
using LegalDraftAI.Models;
using LegalDraftAI.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Legal Draft AI API",
        Version = "1.0.0",
        Description = "Case files, document research, and court-ready legal drafts (DeepSeek / Groq compatible)."
    });
    options.CustomSchemaIds(type => type.FullName?.Replace("+", ".") ?? type.Name);
});

// ---------- MSSQL ----------
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        sql => sql.EnableRetryOnFailure(3)));

builder.Services.Configure<GroqOptions>(
    builder.Configuration.GetSection(GroqOptions.SectionName));

builder.Services.AddHttpClient<IGroqDraftService, GroqDraftService>(client =>
{
    client.BaseAddress = new Uri("https://api.groq.com/openai/v1/");
    client.Timeout = TimeSpan.FromMinutes(3);
});

builder.Services.AddScoped<IDocumentExtractionService, DocumentExtractionService>();
builder.Services.AddScoped<IPdfExportService, PdfExportService>();
builder.Services.AddScoped<IDocxExportService, DocxExportService>();
builder.Services.AddScoped<IDocumentResearchService, DocumentResearchService>();
builder.Services.AddSingleton<IFileStorageService, LocalFileStorageService>();
builder.Services.AddScoped<ICaseService, CaseService>();
builder.Services.AddScoped<IDraftFormatMasterService, DraftFormatMasterService>();

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod());
});

var app = builder.Build();

// Create / update schema on startup (dev-friendly). Prefer migrations in production.
using (var scope = app.Services.CreateScope())
{
    try
    {
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Database.EnsureCreated();
        db.Database.ExecuteSqlRaw("""
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
            END;
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
            END;
            IF OBJECT_ID(N'dbo.CivilPlaintFactsAndReliefRecords', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.CivilPlaintFactsAndReliefRecords (
                    CaseId UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
                    Facts NVARCHAR(MAX) NULL,
                    LegalGrounds NVARCHAR(MAX) NULL,
                    ReliefsSought NVARCHAR(MAX) NULL,
                    Prayer NVARCHAR(5000) NULL,
                    CONSTRAINT FK_CivilPlaintFactsAndReliefRecords_Cases
                        FOREIGN KEY (CaseId) REFERENCES dbo.Cases(Id) ON DELETE CASCADE
                );
            END;
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
            END;
            IF COL_LENGTH(N'dbo.CaseDocuments', N'FileContent') IS NULL
                ALTER TABLE dbo.CaseDocuments ADD FileContent VARBINARY(MAX) NULL;
            IF COL_LENGTH(N'dbo.CaseDrafts', N'ApplicationCode') IS NULL
                ALTER TABLE dbo.CaseDrafts ADD ApplicationCode NVARCHAR(100) NULL;
            IF COL_LENGTH(N'dbo.CaseDrafts', N'PdfContent') IS NULL
                ALTER TABLE dbo.CaseDrafts ADD PdfContent VARBINARY(MAX) NULL;
            IF OBJECT_ID(N'dbo.StageApplications', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.StageApplications (
                    Id UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
                    CaseId UNIQUEIDENTIFIER NOT NULL,
                    StageCode NVARCHAR(20) NOT NULL,
                    ApplicationCode NVARCHAR(100) NOT NULL,
                    DraftContent NVARCHAR(MAX) NULL,
                    CreatedAtUtc DATETIME2 NOT NULL,
                    UpdatedAtUtc DATETIME2 NULL,
                    CONSTRAINT FK_StageApplications_Cases
                        FOREIGN KEY (CaseId) REFERENCES dbo.Cases(Id) ON DELETE CASCADE
                );
                CREATE UNIQUE INDEX IX_StageApplications_CaseId_StageCode_ApplicationCode
                    ON dbo.StageApplications(CaseId, StageCode, ApplicationCode);
            END;
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
            END;
            IF COL_LENGTH(N'dbo.DraftFormatMasters', N'DraftFormatPath') IS NULL
                ALTER TABLE dbo.DraftFormatMasters ADD DraftFormatPath NVARCHAR(1000) NOT NULL
                    CONSTRAINT DF_DraftFormatMasters_DraftFormatPath DEFAULT N'';
            IF COL_LENGTH(N'dbo.DraftFormatMasters', N'OriginalFileName') IS NULL
                ALTER TABLE dbo.DraftFormatMasters ADD OriginalFileName NVARCHAR(500) NOT NULL
                    CONSTRAINT DF_DraftFormatMasters_OriginalFileName DEFAULT N'';
            IF COL_LENGTH(N'dbo.DraftFormatMasters', N'ContentType') IS NULL
                ALTER TABLE dbo.DraftFormatMasters ADD ContentType NVARCHAR(200) NOT NULL
                    CONSTRAINT DF_DraftFormatMasters_ContentType DEFAULT N'application/vnd.openxmlformats-officedocument.wordprocessingml.document';
            IF COL_LENGTH(N'dbo.DraftFormatMasters', N'FileSizeBytes') IS NULL
                ALTER TABLE dbo.DraftFormatMasters ADD FileSizeBytes BIGINT NOT NULL
                    CONSTRAINT DF_DraftFormatMasters_FileSizeBytes DEFAULT 0;
            IF COL_LENGTH(N'dbo.DraftFormatMasters', N'DraftContent') IS NOT NULL
                ALTER TABLE dbo.DraftFormatMasters ALTER COLUMN DraftContent NVARCHAR(MAX) NULL;
            IF NOT EXISTS (
                SELECT 1 FROM sys.indexes
                WHERE object_id = OBJECT_ID(N'dbo.DraftFormatMasters')
                    AND name = N'IX_DraftFormatMasters_Category_FormatName')
                CREATE UNIQUE INDEX IX_DraftFormatMasters_Category_FormatName
                    ON dbo.DraftFormatMasters(Category, FormatName);
            IF COL_LENGTH(N'dbo.StageApplications', N'DraftContent') IS NULL
                ALTER TABLE dbo.StageApplications ADD DraftContent NVARCHAR(MAX) NULL;
            IF NOT EXISTS (
                SELECT 1 FROM sys.indexes
                WHERE object_id = OBJECT_ID(N'dbo.StageApplications')
                    AND name = N'IX_StageApplications_CaseId_StageCode_ApplicationCode')
                CREATE UNIQUE INDEX IX_StageApplications_CaseId_StageCode_ApplicationCode
                    ON dbo.StageApplications(CaseId, StageCode, ApplicationCode);
            IF COL_LENGTH(N'dbo.CaseDocuments', N'StageApplicationId') IS NULL
                ALTER TABLE dbo.CaseDocuments ADD StageApplicationId UNIQUEIDENTIFIER NULL;
            IF COL_LENGTH(N'dbo.CaseDocuments', N'CaseDraftId') IS NULL
                ALTER TABLE dbo.CaseDocuments ADD CaseDraftId UNIQUEIDENTIFIER NULL;
            IF COL_LENGTH(N'dbo.CaseDrafts', N'StageApplicationId') IS NULL
                ALTER TABLE dbo.CaseDrafts ADD StageApplicationId UNIQUEIDENTIFIER NULL;
            IF OBJECT_ID(N'dbo.FK_CaseDocuments_StageApplications', N'F') IS NULL
                ALTER TABLE dbo.CaseDocuments ADD CONSTRAINT FK_CaseDocuments_StageApplications
                    FOREIGN KEY (StageApplicationId) REFERENCES dbo.StageApplications(Id);
            IF OBJECT_ID(N'dbo.FK_CaseDrafts_StageApplications', N'F') IS NULL
                ALTER TABLE dbo.CaseDrafts ADD CONSTRAINT FK_CaseDrafts_StageApplications
                    FOREIGN KEY (StageApplicationId) REFERENCES dbo.StageApplications(Id);
            IF OBJECT_ID(N'dbo.FK_CaseDocuments_CaseDrafts', N'F') IS NULL
                ALTER TABLE dbo.CaseDocuments ADD CONSTRAINT FK_CaseDocuments_CaseDrafts
                    FOREIGN KEY (CaseDraftId) REFERENCES dbo.CaseDrafts(Id);
            IF NOT EXISTS (
                SELECT 1 FROM sys.indexes
                WHERE object_id = OBJECT_ID(N'dbo.CaseDocuments')
                    AND name = N'IX_CaseDocuments_StageApplicationId')
                CREATE INDEX IX_CaseDocuments_StageApplicationId
                    ON dbo.CaseDocuments(StageApplicationId);
            IF NOT EXISTS (
                SELECT 1 FROM sys.indexes
                WHERE object_id = OBJECT_ID(N'dbo.CaseDrafts')
                    AND name = N'IX_CaseDrafts_StageApplicationId')
                CREATE INDEX IX_CaseDrafts_StageApplicationId
                    ON dbo.CaseDrafts(StageApplicationId);
            IF NOT EXISTS (
                SELECT 1 FROM sys.indexes
                WHERE object_id = OBJECT_ID(N'dbo.CaseDocuments')
                    AND name = N'IX_CaseDocuments_CaseDraftId')
                CREATE INDEX IX_CaseDocuments_CaseDraftId
                    ON dbo.CaseDocuments(CaseDraftId);
            """);
        db.Database.ExecuteSqlRaw("""
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
            """);
    }
    catch (Exception ex)
    {
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("Startup");
        logger.LogWarning(ex,
            "Could not connect to SQL Server. Set ConnectionStrings:DefaultConnection. Schema script: Data/SqlServer_Schema.sql");
    }
}

app.UseCors();

app.UseSwagger(c =>
{
    c.RouteTemplate = "swagger/{documentName}/swagger.json";
});

app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Legal Draft AI API v1");
    c.RoutePrefix = "swagger";
    c.DocumentTitle = "Legal Draft AI API";
    c.EnableTryItOutByDefault();
    c.DisplayRequestDuration();
});

app.MapControllers();
app.MapGet("/", () => Results.Redirect("/swagger/index.html"));

app.Run();
