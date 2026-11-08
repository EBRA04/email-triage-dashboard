namespace EmailTriage.Api.Models;

/// Every change made to a ticket, who made it, and when.
public class AuditLog
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid TicketId { get; set; }
    public Ticket Ticket { get; set; } = null!;

    public Guid? ActorId { get; set; } // null = system/AI action
    public User? Actor { get; set; }

    public AuditAction Action { get; set; }
    public string? Detail { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
