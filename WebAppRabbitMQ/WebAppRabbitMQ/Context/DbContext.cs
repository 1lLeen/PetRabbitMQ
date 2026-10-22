using Microsoft.EntityFrameworkCore;
using WebAppRabbitMQ.Models;

namespace WebAppRabbitMQ;

public class TaskDbContext(DbContextOptions<TaskDbContext> options) : DbContext(options)
{
    public DbSet<TaskItem> Tasks => Set<TaskItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TaskItem>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Title).IsRequired().HasMaxLength(200);
            e.Property(x => x.Priority).HasConversion<string>().HasMaxLength(16);
            e.Property(x => x.Version).IsRowVersion(); // -> xmin в PostgreSQL
            e.Property(x => x.CreatedAt).IsRequired();
            e.Property(x => x.CompletedAt);
        });
    }
}