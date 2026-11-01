using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MyPoGoCompanion.Data.Operations;

namespace MyPoGoCompanion.Data.Model;

// Records of running the system outlive the users they mention: the user reference becomes null.

internal sealed class JobRunConfiguration : IEntityTypeConfiguration<JobRun>
{
    public void Configure(EntityTypeBuilder<JobRun> builder)
    {
        builder.ToTable("job_runs", table => table.HasCheckConstraint("ck_job_runs_finished",
            "(status = 'Running') = (finished_at is null)"));
        builder.Property(run => run.Summary).HasColumnType("jsonb");
        builder.HasIndex(run => new { run.Job, run.StartedAt });
        builder.HasOne(run => run.RequestedBy).WithMany().HasForeignKey(run => run.RequestedByUserId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

internal sealed class AiUsageConfiguration : IEntityTypeConfiguration<AiUsage>
{
    public void Configure(EntityTypeBuilder<AiUsage> builder)
    {
        builder.ToTable("ai_usage", table => table.HasCheckConstraint("ck_ai_usage_amounts",
            "input_tokens >= 0 and output_tokens >= 0 and cost_usd >= 0"));
        builder.Property(usage => usage.CostUsd).HasPrecision(12, 6);
        builder.HasIndex(usage => usage.At);
        builder.HasIndex(usage => new { usage.UserId, usage.At });
        builder.HasOne(usage => usage.User).WithMany().OnDelete(DeleteBehavior.SetNull);
        builder.HasOne(usage => usage.JobRun).WithMany().OnDelete(DeleteBehavior.SetNull);
    }
}

internal sealed class AdminFixConfiguration : IEntityTypeConfiguration<AdminFix>
{
    public void Configure(EntityTypeBuilder<AdminFix> builder)
    {
        builder.ToTable("admin_fixes");
        builder.Property(fix => fix.Before).HasColumnType("jsonb");
        builder.Property(fix => fix.After).HasColumnType("jsonb");
        builder.HasIndex(fix => new { fix.EntryTable, fix.EntryId });
        builder.HasOne(fix => fix.User).WithMany().OnDelete(DeleteBehavior.SetNull);
    }
}
