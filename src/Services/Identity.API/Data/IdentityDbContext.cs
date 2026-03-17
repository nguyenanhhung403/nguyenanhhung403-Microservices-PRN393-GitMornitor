using Identity.API.Entities;
using Microsoft.EntityFrameworkCore;

namespace Identity.API.Data;

public class IdentityDbContext : DbContext
{
    public IdentityDbContext(DbContextOptions<IdentityDbContext> options) : base(options) { }

    public DbSet<Teacher> Teachers { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Teacher>().HasKey(t => t.Id);
        modelBuilder.Entity<Teacher>().Property(t => t.Username).IsRequired().HasMaxLength(50);
        modelBuilder.Entity<Teacher>().HasIndex(t => t.Username).IsUnique();
        
        base.OnModelCreating(modelBuilder);
    }
}
