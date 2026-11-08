using EmailTriage.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace EmailTriage.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Department> Departments => Set<Department>();
    public DbSet<User> Users => Set<User>();
    public DbSet<RawEmail> RawEmails => Set<RawEmail>();
    public DbSet<Ticket> Tickets => Set<Ticket>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Department>(e =>
        {
            e.HasIndex(d => d.Name).IsUnique();
            e.HasIndex(d => d.Slug).IsUnique();
        });

        modelBuilder.Entity<User>(e =>
        {
            e.HasIndex(u => u.Email).IsUnique();
            e.HasOne(u => u.Department)
                .WithMany(d => d.Users)
                .HasForeignKey(u => u.DepartmentId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<RawEmail>(e =>
        {
            e.HasIndex(r => r.GraphMessageId).IsUnique();
        });

        modelBuilder.Entity<Ticket>(e =>
        {
            e.HasOne(t => t.RawEmail)
                .WithOne(r => r.Ticket)
                .HasForeignKey<Ticket>(t => t.RawEmailId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(t => t.Department)
                .WithMany(d => d.Tickets)
                .HasForeignKey(t => t.DepartmentId)
                .OnDelete(DeleteBehavior.SetNull);

            e.HasOne(t => t.AssignedTo)
                .WithMany(u => u.AssignedTickets)
                .HasForeignKey(t => t.AssignedToId)
                .OnDelete(DeleteBehavior.SetNull);

            e.HasIndex(t => new { t.DepartmentId, t.Status });
        });

        modelBuilder.Entity<AuditLog>(e =>
        {
            e.HasOne(a => a.Ticket)
                .WithMany(t => t.AuditLogs)
                .HasForeignKey(a => a.TicketId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(a => a.Actor)
                .WithMany(u => u.AuditEntries)
                .HasForeignKey(a => a.ActorId)
                .OnDelete(DeleteBehavior.SetNull);

            e.HasIndex(a => a.TicketId);
        });
    }
}
