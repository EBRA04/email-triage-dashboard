using EmailTriage.Api.Data;
using EmailTriage.Api.Dtos;
using EmailTriage.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace EmailTriage.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class TicketsController : ControllerBase
{
    private readonly AppDbContext _db;

    public TicketsController(AppDbContext db)
    {
        _db = db;
    }

    private bool IsSuperAdmin => User.FindFirst(ClaimTypes.Role)?.Value == Role.SuperAdmin.ToString();

    private Guid? CurrentDepartmentId =>
        Guid.TryParse(User.FindFirst("departmentId")?.Value, out var id) ? id : (Guid?)null;

    private Guid CurrentUserId =>
        Guid.Parse(User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)!.Value);

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var query = _db.Tickets
            .Include(t => t.Department)
            .Include(t => t.RawEmail)
            .Include(t => t.AssignedTo)
            .AsQueryable();

        if (!IsSuperAdmin)
        {
            var deptId = CurrentDepartmentId;
            query = query.Where(t => t.DepartmentId == deptId);
        }

        var tickets = await query
            .OrderByDescending(t => t.CreatedAt)
            .Select(t => new TicketListItemDto(
                t.Id,
                t.Department != null ? t.Department.Slug : null,
                t.Department != null ? t.Department.Name : null,
                t.Status.ToString(),
                t.Priority,
                t.AiDepartmentGuess,
                t.AiConfidence,
                t.AiSummary,
                t.RawEmail.From,
                t.RawEmail.Subject,
                t.RawEmail.ReceivedAt,
                t.AssignedTo != null ? t.AssignedTo.Name : null,
                t.CreatedAt
            ))
            .ToListAsync(ct);

        return Ok(tickets);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var ticket = await _db.Tickets
            .Include(t => t.Department)
            .Include(t => t.RawEmail)
            .Include(t => t.AssignedTo)
            .Include(t => t.AuditLogs).ThenInclude(a => a.Actor)
            .FirstOrDefaultAsync(t => t.Id == id, ct);

        if (ticket == null) return NotFound();
        if (!IsSuperAdmin && ticket.DepartmentId != CurrentDepartmentId) return Forbid();

        var dto = new TicketDetailDto(
            ticket.Id,
            ticket.Department?.Slug,
            ticket.Department?.Name,
            ticket.Status.ToString(),
            ticket.Priority,
            ticket.AiDepartmentGuess,
            ticket.AiConfidence,
            ticket.AiSummary,
            ticket.AiReasoning,
            ticket.AssignedTo?.Name,
            ticket.AssignedToId,
            ticket.CreatedAt,
            new RawEmailDto(
                ticket.RawEmail.From,
                ticket.RawEmail.FromName,
                ticket.RawEmail.To,
                ticket.RawEmail.Subject,
                ticket.RawEmail.BodyText,
                ticket.RawEmail.BodyHtml,
                ticket.RawEmail.ReceivedAt
            ),
            ticket.AuditLogs
                .OrderByDescending(a => a.CreatedAt)
                .Select(a => new AuditLogDto(a.Action.ToString(), a.Actor != null ? a.Actor.Name : "System (AI)", a.Detail, a.CreatedAt))
                .ToList()
        );

        return Ok(dto);
    }

    [HttpPut("{id}/status")]
    public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] UpdateStatusRequest req, CancellationToken ct)
    {
        var ticket = await _db.Tickets.FirstOrDefaultAsync(t => t.Id == id, ct);
        if (ticket == null) return NotFound();
        if (!IsSuperAdmin && ticket.DepartmentId != CurrentDepartmentId) return Forbid();

        if (!Enum.TryParse<TicketStatus>(req.Status, true, out var newStatus))
            return BadRequest(new { message = "Invalid status value." });

        var oldStatus = ticket.Status;
        ticket.Status = newStatus;
        ticket.UpdatedAt = DateTime.UtcNow;

        _db.AuditLogs.Add(new AuditLog
        {
            TicketId = ticket.Id,
            ActorId = CurrentUserId,
            Action = AuditAction.StatusChanged,
            Detail = $"Status changed from {oldStatus} to {newStatus}"
        });

        await _db.SaveChangesAsync(ct);
        return Ok(new { ticket.Id, Status = ticket.Status.ToString() });
    }

    [HttpPut("{id}/department")]
    public async Task<IActionResult> ReassignDepartment(Guid id, [FromBody] ReassignDepartmentRequest req, CancellationToken ct)
    {
        var ticket = await _db.Tickets.Include(t => t.Department).FirstOrDefaultAsync(t => t.Id == id, ct);
        if (ticket == null) return NotFound();
        if (!IsSuperAdmin && ticket.DepartmentId != CurrentDepartmentId) return Forbid();

        var oldDeptName = ticket.Department?.Name ?? "(unassigned)";
        Department? newDept = null;
        if (req.DepartmentId.HasValue)
        {
            newDept = await _db.Departments.FindAsync(new object[] { req.DepartmentId.Value }, ct);
            if (newDept == null) return BadRequest(new { message = "Department not found." });
        }

        ticket.DepartmentId = req.DepartmentId;
        ticket.UpdatedAt = DateTime.UtcNow;
        if (ticket.Status == TicketStatus.NeedsReview)
            ticket.Status = TicketStatus.New;

        _db.AuditLogs.Add(new AuditLog
        {
            TicketId = ticket.Id,
            ActorId = CurrentUserId,
            Action = AuditAction.ReassignedDepartment,
            Detail = $"Reassigned from {oldDeptName} to {newDept?.Name ?? "(unassigned)"}"
        });

        await _db.SaveChangesAsync(ct);
        return Ok(new { ticket.Id, DepartmentId = ticket.DepartmentId });
    }

    [HttpPut("{id}/assign")]
    public async Task<IActionResult> AssignAgent(Guid id, [FromBody] AssignAgentRequest req, CancellationToken ct)
    {
        var ticket = await _db.Tickets.Include(t => t.AssignedTo).FirstOrDefaultAsync(t => t.Id == id, ct);
        if (ticket == null) return NotFound();
        if (!IsSuperAdmin && ticket.DepartmentId != CurrentDepartmentId) return Forbid();

        var oldAssignee = ticket.AssignedTo?.Name ?? "(unassigned)";
        User? newAssignee = null;
        if (req.UserId.HasValue)
        {
            newAssignee = await _db.Users.FindAsync(new object[] { req.UserId.Value }, ct);
            if (newAssignee == null) return BadRequest(new { message = "User not found." });
        }

        ticket.AssignedToId = req.UserId;
        ticket.UpdatedAt = DateTime.UtcNow;

        _db.AuditLogs.Add(new AuditLog
        {
            TicketId = ticket.Id,
            ActorId = CurrentUserId,
            Action = AuditAction.AssignedAgent,
            Detail = $"Assigned from {oldAssignee} to {newAssignee?.Name ?? "(unassigned)"}"
        });

        await _db.SaveChangesAsync(ct);
        return Ok(new { ticket.Id, AssignedToId = ticket.AssignedToId });
    }
}
