using Classroom.API.Entities;
using Microsoft.EntityFrameworkCore;

namespace Classroom.API.Data;

public class ClassroomDbContext : DbContext
{
    public ClassroomDbContext(DbContextOptions<ClassroomDbContext> options) : base(options) { }

    public DbSet<ClassRoom> ClassRooms { get; set; }
    public DbSet<StudentGroup> StudentGroups { get; set; }
    public DbSet<Student> Students { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ClassRoom>().HasKey(c => c.Id);
        
        modelBuilder.Entity<StudentGroup>().HasKey(g => g.Id);
        modelBuilder.Entity<StudentGroup>()
            .HasOne(g => g.ClassRoom)
            .WithMany(c => c.StudentGroups)
            .HasForeignKey(g => g.ClassRoomId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Student>().HasKey(s => s.Id);
        modelBuilder.Entity<Student>().Property(s => s.StudentCode).IsRequired().HasMaxLength(20);
        modelBuilder.Entity<Student>().HasIndex(s => s.StudentCode).IsUnique();
        
        modelBuilder.Entity<Student>()
            .HasOne(s => s.Group)
            .WithMany(g => g.Students)
            .HasForeignKey(s => s.GroupId)
            .OnDelete(DeleteBehavior.Cascade);

        base.OnModelCreating(modelBuilder);
    }
}
