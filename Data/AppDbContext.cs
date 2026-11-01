using LegalDraftAI.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace LegalDraftAI.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<CaseRecord> Cases => Set<CaseRecord>();
    public DbSet<CaseDocument> CaseDocuments => Set<CaseDocument>();
    public DbSet<CaseDraft> CaseDrafts => Set<CaseDraft>();
    public DbSet<StageApplication> StageApplications => Set<StageApplication>();
    public DbSet<CivilPlaintCourtAndSuitRecord> CivilPlaintCourtAndSuitRecords => Set<CivilPlaintCourtAndSuitRecord>();
    public DbSet<CivilPlaintSuitPropertyRecord> CivilPlaintSuitPropertyRecords => Set<CivilPlaintSuitPropertyRecord>();
    public DbSet<CivilPlaintFactsAndReliefRecord> CivilPlaintFactsAndReliefRecords => Set<CivilPlaintFactsAndReliefRecord>();
    public DbSet<CivilPlaintFilingDetailsRecord> CivilPlaintFilingDetailsRecords => Set<CivilPlaintFilingDetailsRecord>();
    public DbSet<DraftFormatMaster> DraftFormatMasters => Set<DraftFormatMaster>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<CaseRecord>(e =>
        {
            e.ToTable("Cases");
            e.HasKey(x => x.Id);
            e.Property(x => x.Title).HasMaxLength(500).IsRequired();
            e.Property(x => x.CaseCategory).HasMaxLength(50);
            e.Property(x => x.PlaintNature).HasMaxLength(100);
            e.Property(x => x.Jurisdiction).HasMaxLength(100);
            e.Property(x => x.CourtName).HasMaxLength(300);
            e.Property(x => x.CaseNumber).HasMaxLength(100);
            e.Property(x => x.Status).HasMaxLength(50);
            e.Property(x => x.PlaintiffName).HasMaxLength(300);
            e.Property(x => x.DefendantName).HasMaxLength(300);
            e.HasIndex(x => x.CaseNumber);
            e.HasIndex(x => x.Status);
            e.HasIndex(x => x.CreatedAtUtc);
        });

        modelBuilder.Entity<DraftFormatMaster>(e =>
        {
            e.ToTable("DraftFormatMasters");
            e.HasKey(x => x.Id);
            e.Property(x => x.Category).HasMaxLength(20).IsRequired();
            e.Property(x => x.FormatName).HasMaxLength(200).IsRequired();
            e.Property(x => x.Description).HasMaxLength(1000);
            e.Property(x => x.DraftFormatPath).HasMaxLength(1000).IsRequired();
            e.Property(x => x.OriginalFileName).HasMaxLength(500).IsRequired();
            e.Property(x => x.ContentType).HasMaxLength(200).IsRequired();
            e.HasIndex(x => new { x.Category, x.FormatName })
                .IsUnique()
                .HasDatabaseName("IX_DraftFormatMasters_Category_FormatName");
        });

        ConfigureCivilPlaintSection<CivilPlaintCourtAndSuitRecord>(
            modelBuilder, "CivilPlaintCourtAndSuitRecords");
        ConfigureCivilPlaintSection<CivilPlaintSuitPropertyRecord>(
            modelBuilder, "CivilPlaintSuitPropertyRecords");
        ConfigureCivilPlaintSection<CivilPlaintFactsAndReliefRecord>(
            modelBuilder, "CivilPlaintFactsAndReliefRecords");
        ConfigureCivilPlaintSection<CivilPlaintFilingDetailsRecord>(
            modelBuilder, "CivilPlaintFilingDetailsRecords");

        modelBuilder.Entity<CivilPlaintCourtAndSuitRecord>(e =>
        {
            e.HasOne(x => x.Case).WithOne(x => x.CivilPlaintCourtAndSuit)
                .HasForeignKey<CivilPlaintCourtAndSuitRecord>(x => x.CaseId)
                .OnDelete(DeleteBehavior.Cascade);
            e.Property(x => x.CourtName).HasMaxLength(300);
            e.Property(x => x.CaseTitle).HasMaxLength(500);
            e.Property(x => x.CaseNumber).HasMaxLength(100);
            e.Property(x => x.CauseOfActionDate).HasMaxLength(50);
            e.Property(x => x.PlaintiffName).HasMaxLength(300);
            e.Property(x => x.DefendantName).HasMaxLength(300);
            e.Property(x => x.PlaintiffAddress).HasMaxLength(2000);
            e.Property(x => x.DefendantAddress).HasMaxLength(2000);
        });
        modelBuilder.Entity<CivilPlaintSuitPropertyRecord>(e =>
        {
            e.HasOne(x => x.Case).WithOne(x => x.CivilPlaintSuitProperty)
                .HasForeignKey<CivilPlaintSuitPropertyRecord>(x => x.CaseId)
                .OnDelete(DeleteBehavior.Cascade);
            e.Property(x => x.PropertyDescription).HasMaxLength(5000);
            e.Property(x => x.SurveyPlotNumber).HasMaxLength(200);
            e.Property(x => x.Area).HasMaxLength(200);
            e.Property(x => x.Village).HasMaxLength(200);
            e.Property(x => x.Tehsil).HasMaxLength(200);
            e.Property(x => x.District).HasMaxLength(200);
            e.Property(x => x.SuitValuationCourtFee).HasMaxLength(500);
        });
        modelBuilder.Entity<CivilPlaintFactsAndReliefRecord>(e =>
        {
            e.HasOne(x => x.Case).WithOne(x => x.CivilPlaintFactsAndRelief)
                .HasForeignKey<CivilPlaintFactsAndReliefRecord>(x => x.CaseId)
                .OnDelete(DeleteBehavior.Cascade);
            e.Property(x => x.Facts).HasColumnType("nvarchar(max)");
            e.Property(x => x.LegalGrounds).HasMaxLength(10000);
            e.Property(x => x.ReliefsSought).HasMaxLength(10000);
            e.Property(x => x.Prayer).HasMaxLength(5000);
        });
        modelBuilder.Entity<CivilPlaintFilingDetailsRecord>(e =>
        {
            e.HasOne(x => x.Case).WithOne(x => x.CivilPlaintFilingDetails)
                .HasForeignKey<CivilPlaintFilingDetailsRecord>(x => x.CaseId)
                .OnDelete(DeleteBehavior.Cascade);
            e.Property(x => x.Place).HasMaxLength(200);
            e.Property(x => x.Date).HasMaxLength(50);
            e.Property(x => x.AdvocateName).HasMaxLength(300);
            e.Property(x => x.EnrollmentNumber).HasMaxLength(100);
        });

        modelBuilder.Entity<StageApplication>(e =>
        {
            e.ToTable("StageApplications");
            e.HasKey(x => x.Id);
            e.Property(x => x.StageCode).HasMaxLength(20).IsRequired();
            e.Property(x => x.ApplicationCode).HasMaxLength(100).IsRequired();
            e.Property(x => x.DraftContent).HasColumnType("nvarchar(max)");
            e.HasOne(x => x.Case)
                .WithMany(c => c.StageApplications)
                .HasForeignKey(x => x.CaseId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_StageApplications_Cases");
            e.HasIndex(x => new { x.CaseId, x.StageCode, x.ApplicationCode })
                .IsUnique()
                .HasDatabaseName("IX_StageApplications_CaseId_StageCode_ApplicationCode");
        });

        modelBuilder.Entity<CaseDocument>(e =>
        {
            e.ToTable("CaseDocuments");
            e.HasKey(x => x.Id);
            e.Property(x => x.DocumentCode).HasMaxLength(50).IsRequired();
            e.Property(x => x.OriginalFileName).HasMaxLength(500);
            e.Property(x => x.ContentType).HasMaxLength(200);
            e.Property(x => x.StoragePath).HasMaxLength(1000);
            e.Property(x => x.FileContent).HasColumnType("varbinary(max)");
            e.HasOne(x => x.StageApplication)
                .WithMany(application => application.Documents)
                .HasForeignKey(x => x.StageApplicationId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_CaseDocuments_StageApplications");
            e.HasOne<CaseDraft>()
                .WithMany()
                .HasForeignKey(x => x.CaseDraftId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_CaseDocuments_CaseDrafts");
            e.HasIndex(x => x.CaseDraftId);
            e.HasOne(x => x.Case)
                .WithMany(c => c.Documents)
                .HasForeignKey(x => x.CaseId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => x.CaseId);
            e.HasIndex(x => new { x.CaseId, x.DocumentCode });
        });

        modelBuilder.Entity<CaseDraft>(e =>
        {
            e.ToTable("CaseDrafts");
            e.HasKey(x => x.Id);
            e.Property(x => x.DocumentType).HasMaxLength(100).IsRequired();
            e.Property(x => x.ApplicationCode).HasMaxLength(100);
            e.Property(x => x.Status).HasMaxLength(50);
            e.Property(x => x.PdfContent).HasColumnType("varbinary(max)");
            e.Property(x => x.PdfStoragePath).HasMaxLength(1000);
            e.HasOne(x => x.StageApplication)
                .WithMany(application => application.Drafts)
                .HasForeignKey(x => x.StageApplicationId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_CaseDrafts_StageApplications");
            // JsonContent is nvarchar(max) by default
            e.HasOne(x => x.Case)
                .WithMany(c => c.Drafts)
                .HasForeignKey(x => x.CaseId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => x.CaseId);
            e.HasIndex(x => new { x.CaseId, x.DocumentType, x.Version });
            e.HasIndex(x => x.StageApplicationId);
        });

        modelBuilder.Entity<CaseDocument>()
            .HasIndex(x => x.StageApplicationId);
    }

    private static void ConfigureCivilPlaintSection<T>(
        ModelBuilder modelBuilder,
        string tableName)
        where T : class
    {
        modelBuilder.Entity<T>(entity =>
        {
            entity.ToTable(tableName);
            entity.HasKey("CaseId");
        });
    }
}
