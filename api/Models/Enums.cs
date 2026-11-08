namespace EmailTriage.Api.Models;

public enum Role
{
    Agent,
    DeptAdmin,
    SuperAdmin
}

public enum TicketStatus
{
    New,
    NeedsReview, // low-confidence classification, awaiting human triage
    InProgress,
    Resolved,
    Closed
}

public enum AuditAction
{
    Classified,
    ReassignedDepartment,
    StatusChanged,
    AssignedAgent,
    Comment
}
