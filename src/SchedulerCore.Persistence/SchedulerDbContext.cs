using Microsoft.EntityFrameworkCore;
using SchedulerCore.Domain.Entities;

namespace SchedulerCore.Persistence;

public class SchedulerDbContext : DbContext
{
    public SchedulerDbContext(DbContextOptions<SchedulerDbContext> options) : base(options)
    {
    }

    public DbSet<Job> Jobs { get; set; } = null!;
    public DbSet<Worker> Workers { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Job>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => new { e.Status, e.Priority });
            entity.HasIndex(e => e.LeaseExpiresAt);
            entity.HasIndex(e => e.WorkerId);
        });

        modelBuilder.Entity<Worker>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Name).IsUnique();
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.LastHeartbeat);
        });
    }
}
