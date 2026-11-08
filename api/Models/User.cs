namespace EmailTriage.Api.Models;

public class User
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public Role Role { get; set; } = Role.Agent;

    public Guid? DepartmentId { get; set; }
    public Department? Department { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public List<Ticket> AssignedTickets { get; set; } = new();
    public List<AuditLog> AuditEntries { get; set; } = new();
}
