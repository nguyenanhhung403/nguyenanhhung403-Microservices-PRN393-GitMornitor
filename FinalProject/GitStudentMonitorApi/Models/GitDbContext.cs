using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace GitStudentMonitorApi.Models;

public partial class GitDbContext : DbContext
{
    public GitDbContext()
    {
    }

    public GitDbContext(DbContextOptions<GitDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<ClassRoom> ClassRooms { get; set; }

    public virtual DbSet<Student> Students { get; set; }

    public virtual DbSet<StudentGroup> StudentGroups { get; set; }

    public virtual DbSet<SyncHistory> SyncHistories { get; set; }

    public virtual DbSet<Teacher> Teachers { get; set; }


    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ClassRoom>(entity =>
        {
            entity.HasIndex(e => e.TeacherId, "IX_ClassRooms_TeacherId");

            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("DATETIME");
            entity.Property(e => e.IsActive)
                .HasDefaultValue(true)
                .HasColumnType("BOOLEAN");

            entity.HasOne(d => d.Teacher).WithMany(p => p.ClassRooms).HasForeignKey(d => d.TeacherId);
        });

        modelBuilder.Entity<Student>(entity =>
        {
            entity.HasIndex(e => e.GitHubUsername, "IX_Students_GitHubUsername");

            entity.HasIndex(e => e.GroupId, "IX_Students_GroupId");

            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("DATETIME");
            entity.Property(e => e.IsLeader).HasColumnType("BOOLEAN");

            entity.HasOne(d => d.Group).WithMany(p => p.Students).HasForeignKey(d => d.GroupId);
        });

        modelBuilder.Entity<StudentGroup>(entity =>
        {
            entity.HasIndex(e => e.ClassRoomId, "IX_StudentGroups_ClassRoomId");

            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("DATETIME");
            entity.Property(e => e.Status).HasDefaultValue(0);

            entity.HasOne(d => d.ClassRoom).WithMany(p => p.StudentGroups).HasForeignKey(d => d.ClassRoomId);
        });

        modelBuilder.Entity<SyncHistory>(entity =>
        {
            entity.ToTable("SyncHistory");

            entity.HasIndex(e => new { e.StudentId, e.SyncTime }, "IX_SyncHistory_StudentId_SyncTime").IsDescending(false, true);

            entity.Property(e => e.LastCommitDate).HasColumnType("DATETIME");
            entity.Property(e => e.SyncTime)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("DATETIME");

            entity.HasOne(d => d.Student).WithMany(p => p.SyncHistories).HasForeignKey(d => d.StudentId);
        });

        modelBuilder.Entity<Teacher>(entity =>
        {
            entity.HasIndex(e => e.Username, "IX_Teachers_Username").IsUnique();

            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("DATETIME");
            entity.Property(e => e.LastLogin).HasColumnType("DATETIME");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
