namespace EmailTriage.Api.Dtos;

public record LoginRequest(string Email, string Password);

public record UserDto(Guid Id, string Email, string Name, string Role, Guid? DepartmentId, string? DepartmentName);

public record LoginResponse(string Token, UserDto User);
