namespace EmailTriage.Api.Dtos;

public record TicketListItemDto(
    Guid Id,
    string? DepartmentSlug,
    string? DepartmentName,
    string Status,
    string? Priority,
    string? AiDepartmentGuess,
    double? AiConfidence,
    string? AiSummary,
    string EmailFrom,
    string EmailSubject,
    DateTime EmailReceivedAt,
    string? AssignedToName,
    DateTime CreatedAt
);

public record RawEmailDto(
    string From,
    string? FromName,
    string To,
    string Subject,
    string BodyText,
    string? BodyHtml,
    DateTime ReceivedAt
);

public record AuditLogDto(
    string Action,
    string? ActorName,
    string? Detail,
    DateTime CreatedAt
);

public record TicketDetailDto(
    Guid Id,
    string? DepartmentSlug,
    string? DepartmentName,
    string Status,
    string? Priority,
    string? AiDepartmentGuess,
    double? AiConfidence,
    string? AiSummary,
    string? AiReasoning,
    string? AssignedToName,
    Guid? AssignedToId,
    DateTime CreatedAt,
    RawEmailDto Email,
    List<AuditLogDto> AuditLog
);

public record UpdateStatusRequest(string Status);
public record ReassignDepartmentRequest(Guid? DepartmentId);
public record AssignAgentRequest(Guid? UserId);
