using Coordinator.Models;
using Microsoft.EntityFrameworkCore;

namespace Coordinator.Data;

public sealed class SchedulerDbContext : DbContext
{
    public SchedulerDbContext(DbContextOptions<SchedulerDbContext> options) : base(options)
    {
    }

    public DbSet<JobEntity> Jobs => Set<JobEntity>();
    public DbSet<RecurringJobEntity> RecurringJobs => Set<RecurringJobEntity>();
    public DbSet<JobRunEntity> JobRuns => Set<JobRunEntity>();
    public DbSet<WorkerHeartbeatEntity> WorkerHeartbeats => Set<WorkerHeartbeatEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<JobEntity>(entity =>
        {
            entity.ToTable("jobs");
            entity.HasKey(job => job.Id);
            entity.Property(job => job.Type).IsRequired();
            entity.Property(job => job.Payload).IsRequired();
            entity.Property(job => job.Status).IsRequired();
            entity.Property(job => job.CreatedAt).IsRequired();
            entity.Property(job => job.UpdatedAt).IsRequired();
            entity.Property(job => job.RunAt).IsRequired();
            entity.Property(job => job.Attempts).IsRequired();
            entity.Property(job => job.MaxAttempts).IsRequired();
            entity.Property(job => job.TimeoutSeconds).IsRequired();
            entity.HasIndex(job => job.Status);
            entity.HasIndex(job => job.RunAt);
            entity.HasIndex(job => job.IdempotencyKey).IsUnique(false);
            entity.HasIndex(job => job.LeaseExpiresAt);
            entity.HasOne(job => job.RecurringJob)
                .WithMany(recurring => recurring.Jobs)
                .HasForeignKey(job => job.RecurringJobId);
        });

        modelBuilder.Entity<JobRunEntity>(entity =>
        {
            entity.ToTable("job_runs");
            entity.HasKey(run => run.Id);
            entity.Property(run => run.Status).IsRequired();
            entity.Property(run => run.WorkerId).IsRequired();
            entity.Property(run => run.StartedAt).IsRequired();
            entity.HasIndex(run => run.JobId);
            entity.HasIndex(run => run.Status);
            entity.HasOne(run => run.Job)
                .WithMany()
                .HasForeignKey(run => run.JobId);
        });

        modelBuilder.Entity<RecurringJobEntity>(entity =>
        {
            entity.ToTable("recurring_jobs");
            entity.HasKey(job => job.Id);
            entity.Property(job => job.Name).IsRequired();
            entity.Property(job => job.CronExpression).IsRequired();
            entity.Property(job => job.Type).IsRequired();
            entity.Property(job => job.Payload).IsRequired();
            entity.Property(job => job.NextRunAt).IsRequired();
            entity.Property(job => job.IsActive).IsRequired();
            entity.HasIndex(job => job.NextRunAt);
            entity.HasIndex(job => job.IdempotencyKey).IsUnique(false);
        });

        modelBuilder.Entity<WorkerHeartbeatEntity>(entity =>
        {
            entity.ToTable("worker_heartbeats");
            entity.HasKey(hb => hb.Id);
            entity.Property(hb => hb.WorkerId).IsRequired();
            entity.Property(hb => hb.LastSeenAt).IsRequired();
            entity.HasIndex(hb => hb.WorkerId).IsUnique();
        });
    }
}
