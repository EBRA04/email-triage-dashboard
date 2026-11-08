namespace EmailTriage.Api.Models;

/// The routed work item a department actually works from.
public class Ticket
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid RawEmailId { get; set; }
    public RawEmail RawEmail { get; set; } = null!;

    public Guid? DepartmentId { get; set; }
    public Department? Department { get; set; }

    public Guid? AssignedToId { get; set; }
    public User? AssignedTo { get; set; }

    public TicketStatus Status { get; set; } = TicketStatus.New;
    public string? Priority { get; set; } // "low" | "medium" | "high" | "urgent"

    // --- AI classification output ---
    public string? AiDepartmentGuess { get; set; }
    public double? AiConfidence { get; set; }
    public string? AiSummary { get; set; }
    public string? AiReasoning { get; set; }
    public DateTime? ClassifiedAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public List<AuditLog> AuditLogs { get; set; } = new();
}
