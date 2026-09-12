using Microsoft.EntityFrameworkCore;

namespace ProposalEval.Data;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Rfq> Rfqs => Set<Rfq>();
    public DbSet<RfqDocument> RfqDocuments => Set<RfqDocument>();
    public DbSet<Vendor> Vendors => Set<Vendor>();
    public DbSet<VendorDocument> VendorDocuments => Set<VendorDocument>();
    public DbSet<EvaluationRun> EvaluationRuns => Set<EvaluationRun>();
    public DbSet<EvaluationJob> EvaluationJobs => Set<EvaluationJob>();
    public DbSet<EvaluationJobEvent> EvaluationJobEvents => Set<EvaluationJobEvent>();
    public DbSet<ProposalSummary> ProposalSummaries => Set<ProposalSummary>();
    public DbSet<RfqCriterion> RfqCriteria => Set<RfqCriterion>();
    public DbSet<VendorEvaluation> VendorEvaluations => Set<VendorEvaluation>();
    public DbSet<VendorScore> VendorScores => Set<VendorScore>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.Entity<Rfq>(e =>
        {
            e.ToTable("tbl_Rfqs");
            e.Property(x => x.Number).HasMaxLength(64).IsRequired();
            e.Property(x => x.Title).HasMaxLength(500).IsRequired();
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(64);
            e.HasIndex(x => x.Number);
        });

        model.Entity<RfqDocument>(e =>
        {
            e.ToTable("tbl_RfqDocuments");
            e.Property(x => x.Kind).HasConversion<string>().HasMaxLength(64);
            e.Property(x => x.FileName).HasMaxLength(512).IsRequired();
            e.Property(x => x.ContentType).HasMaxLength(256).IsRequired();
            e.Property(x => x.StorageKey).HasMaxLength(1024).IsRequired();
            e.HasIndex(x => new { x.RfqId, x.Kind }).IsUnique();
            e.HasOne(x => x.Rfq).WithMany(x => x.Documents).HasForeignKey(x => x.RfqId);
        });

        model.Entity<Vendor>(e =>
        {
            e.ToTable("tbl_Vendors");
            e.Property(x => x.Name).HasMaxLength(256).IsRequired();
            e.Property(x => x.ExternalProjectId).HasMaxLength(64);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(64);
            e.HasIndex(x => new { x.RfqId, x.Name });
            e.HasOne(x => x.Rfq).WithMany(x => x.Vendors).HasForeignKey(x => x.RfqId);
        });

        model.Entity<VendorDocument>(e =>
        {
            e.ToTable("tbl_VendorDocuments");
            e.Property(x => x.Kind).HasConversion<string>().HasMaxLength(64);
            e.Property(x => x.FileName).HasMaxLength(512).IsRequired();
            e.Property(x => x.ContentType).HasMaxLength(256).IsRequired();
            e.Property(x => x.StorageKey).HasMaxLength(1024).IsRequired();
            e.HasOne(x => x.Vendor).WithMany(x => x.Documents).HasForeignKey(x => x.VendorId);
        });

        model.Entity<EvaluationRun>(e =>
        {
            e.ToTable("tbl_EvaluationRuns");
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(64);
            e.Property(x => x.StatusMessage).HasMaxLength(1000).IsRequired();
            e.Property(x => x.RequestedBy).HasMaxLength(256);
            e.HasIndex(x => new { x.RfqId, x.Status });
            e.HasOne(x => x.Rfq).WithMany(x => x.Runs).HasForeignKey(x => x.RfqId);
        });

        model.Entity<EvaluationJob>(e =>
        {
            e.ToTable("tbl_EvaluationJobs");
            e.Property(x => x.JobType).HasConversion<string>().HasMaxLength(64);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(64);
            e.Property(x => x.StatusMessage).HasMaxLength(1000);
            e.HasIndex(x => new { x.Status, x.JobType, x.CreatedAt });
            e.HasOne(x => x.Run).WithMany(x => x.Jobs).HasForeignKey(x => x.RunId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Rfq).WithMany().HasForeignKey(x => x.RfqId).OnDelete(DeleteBehavior.NoAction);
            e.HasOne(x => x.Vendor).WithMany().HasForeignKey(x => x.VendorId).OnDelete(DeleteBehavior.NoAction);
        });

        model.Entity<EvaluationJobEvent>(e =>
        {
            e.ToTable("tbl_EvaluationJobEvents");
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(64);
            e.Property(x => x.Message).HasMaxLength(1000);
            e.HasIndex(x => new { x.JobId, x.At });
            e.HasOne(x => x.Job).WithMany(x => x.Events).HasForeignKey(x => x.JobId);
        });

        model.Entity<ProposalSummary>(e =>
        {
            e.ToTable("tbl_ProposalSummaries");
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(64);
            e.Property(x => x.RejectionReason).HasMaxLength(2000);
            e.HasOne(x => x.Job).WithMany().HasForeignKey(x => x.JobId).OnDelete(DeleteBehavior.NoAction);
            e.HasOne(x => x.Vendor).WithMany(x => x.Summaries).HasForeignKey(x => x.VendorId);
        });

        model.Entity<RfqCriterion>(e =>
        {
            e.ToTable("tbl_RfqCriteria");
            e.Property(x => x.Code).HasMaxLength(32).IsRequired();
            e.Property(x => x.Name).HasMaxLength(256).IsRequired();
            e.Property(x => x.Weight).HasPrecision(5, 2);
            e.Property(x => x.GroupCode).HasMaxLength(32);
            e.Property(x => x.GroupWeight).HasPrecision(5, 2);
            e.HasIndex(x => new { x.RfqId, x.Code }).IsUnique();
            e.HasOne(x => x.Rfq).WithMany(x => x.Criteria).HasForeignKey(x => x.RfqId);
        });

        model.Entity<VendorEvaluation>(e =>
        {
            e.ToTable("tbl_VendorEvaluations");
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(64);
            e.HasIndex(x => new { x.JobId, x.VendorId }).IsUnique();
            e.Ignore(x => x.Tws);
            e.Ignore(x => x.TechnicalPass);
            e.HasOne(x => x.Job).WithMany().HasForeignKey(x => x.JobId).OnDelete(DeleteBehavior.NoAction);
            e.HasOne(x => x.Vendor).WithMany(x => x.Evaluations).HasForeignKey(x => x.VendorId);
        });

        model.Entity<VendorScore>(e =>
        {
            e.ToTable("tbl_VendorScores");
            e.Property(x => x.Score).HasPrecision(5, 2);
            e.HasIndex(x => new { x.VendorEvaluationId, x.RfqCriterionId }).IsUnique();
            e.HasOne(x => x.Evaluation).WithMany(x => x.Scores).HasForeignKey(x => x.VendorEvaluationId);
            e.HasOne(x => x.Criterion).WithMany(x => x.Scores).HasForeignKey(x => x.RfqCriterionId).OnDelete(DeleteBehavior.NoAction);
        });
    }
}
