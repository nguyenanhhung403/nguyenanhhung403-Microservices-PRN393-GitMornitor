using Monitoring.API.Entities;
using Microsoft.EntityFrameworkCore;

namespace Monitoring.API.Data;

public class MonitoringDbContext : DbContext
{
    public MonitoringDbContext(DbContextOptions<MonitoringDbContext> options) : base(options) { }

    public DbSet<SyncHistory> SyncHistories { get; set; }
    
    // Shared tables used by dashboard (read-only in this service)
    public DbSet<ClassRoom> ClassRooms { get; set; }
    public DbSet<StudentGroup> StudentGroups { get; set; }
    public DbSet<Student> Students { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SyncHistory>().HasKey(s => s.Id);
        modelBuilder.Entity<SyncHistory>().HasIndex(s => new { s.StudentId, s.SyncTime }).IsDescending(false, true);

        // Map shared models
        modelBuilder.Entity<ClassRoom>().HasKey(c => c.Id);
        modelBuilder.Entity<StudentGroup>().HasKey(g => g.Id);
        modelBuilder.Entity<StudentGroup>().HasOne(g => g.ClassRoom).WithMany(c => c.StudentGroups).HasForeignKey(g => g.ClassRoomId);
        
        modelBuilder.Entity<Student>().HasKey(s => s.Id);
        modelBuilder.Entity<Student>().HasOne<StudentGroup>().WithMany(g => g.Students).HasForeignKey(s => s.GroupId);

        base.OnModelCreating(modelBuilder);
    }
}
