using FireStopEvacTracker.Models;
using Microsoft.EntityFrameworkCore;

namespace FireStopEvacTracker.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<EvacJob> EvacJobs => Set<EvacJob>();
    public DbSet<JobNote> JobNotes => Set<JobNote>();
    public DbSet<JobApproval> JobApprovals => Set<JobApproval>();
    public DbSet<User> Users => Set<User>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<EvacJob>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.ClientName).HasMaxLength(200).IsRequired();
            entity.Property(e => e.SiteAddress).HasMaxLength(300).IsRequired();
            entity.Property(e => e.JobName).HasMaxLength(250).IsRequired();
            entity.Property(e => e.Status).HasMaxLength(50).IsRequired();
            entity.Property(e => e.ShareCode).HasMaxLength(50);
            entity.HasIndex(e => e.JobName).IsUnique();
            entity.HasMany(e => e.JobNotes)
                .WithOne(n => n.EvacJob)
                .HasForeignKey(n => n.EvacJobId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(e => e.Approvals)
                .WithOne(a => a.Job)
                .HasForeignKey(a => a.JobId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<JobNote>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Content).IsRequired();
        });

        modelBuilder.Entity<JobApproval>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.ClientEmail).HasMaxLength(255).IsRequired();
            entity.Property(e => e.ClientName).HasMaxLength(200).IsRequired();
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Username).HasMaxLength(100).IsRequired();
            entity.Property(e => e.Email).HasMaxLength(255).IsRequired();
            entity.Property(e => e.FullName).HasMaxLength(200).IsRequired();
            entity.Property(e => e.PasswordHash).IsRequired();
            entity.Property(e => e.Role).HasMaxLength(50).IsRequired();
            entity.HasIndex(e => e.Username).IsUnique();
            entity.HasIndex(e => e.Email).IsUnique();
        });
    }
}
