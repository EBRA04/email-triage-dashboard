using EmailTriage.Api.Data;
using EmailTriage.Api.Dtos;
using EmailTriage.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EmailTriage.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly JwtTokenService _jwt;

    public AuthController(AppDbContext db, JwtTokenService jwt)
    {
        _db = db;
        _jwt = jwt;
    }

    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest req, CancellationToken ct)
    {
        var user = await _db.Users.Include(u => u.Department)
            .FirstOrDefaultAsync(u => u.Email == req.Email, ct);

        if (user == null || !BCrypt.Net.BCrypt.Verify(req.Password, user.PasswordHash))
            return Unauthorized(new { message = "Invalid email or password." });

        var token = _jwt.GenerateToken(user);
        var dto = new UserDto(user.Id, user.Email, user.Name, user.Role.ToString(), user.DepartmentId, user.Department?.Name);

        return Ok(new LoginResponse(token, dto));
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<IActionResult> Me(CancellationToken ct)
    {
        var subClaim = User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)
            ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
        if (subClaim == null || !Guid.TryParse(subClaim.Value, out var userId))
            return Unauthorized();

        var user = await _db.Users.Include(u => u.Department).FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user == null) return NotFound();

        var dto = new UserDto(user.Id, user.Email, user.Name, user.Role.ToString(), user.DepartmentId, user.Department?.Name);
        return Ok(dto);
    }
}
