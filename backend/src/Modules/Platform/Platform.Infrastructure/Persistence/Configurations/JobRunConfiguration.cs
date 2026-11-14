using Construction.Platform.Core.Domain.Jobs;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Construction.Platform.Infrastructure.Persistence.Configurations;

public sealed class JobRunConfiguration : IEntityTypeConfiguration<JobRun>
{
    public void Configure(EntityTypeBuilder<JobRun> builder)
    {
        builder.ToTable("job_runs", PlatformDbContext.Schema);

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").IsRequired();
        builder.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(x => x.Job).HasColumnName("job").IsRequired();
        builder.Property(x => x.RunKey).HasColumnName("run_key").IsRequired();
        builder.Property(x => x.Trigger).HasColumnName("trigger").IsRequired();
        builder.Property(x => x.Queue).HasColumnName("queue").IsRequired();
        builder.Property(x => x.StartedAt).HasColumnName("started_at").HasColumnType("timestamp with time zone").IsRequired();
        builder.Property(x => x.FinishedAt).HasColumnName("finished_at").HasColumnType("timestamp with time zone");
        builder.Property(x => x.Status).HasColumnName("status").IsRequired();
        builder.Property(x => x.ErrorCode).HasColumnName("error_code");
        builder.Property(x => x.Attempts).HasColumnName("attempts").HasDefaultValue(1).IsRequired();

        builder.HasIndex(x => new { x.TenantId, x.Job, x.StartedAt })
            .HasDatabaseName("ix_job_runs_tenant_job_started");

        // BR-PLT-140: a second enqueue of the same window is a no-op.
        builder.HasIndex(x => new { x.TenantId, x.Job, x.RunKey })
            .HasDatabaseName("uq_job_runs_tenant_job_run_key")
            .IsUnique();
    }
}
