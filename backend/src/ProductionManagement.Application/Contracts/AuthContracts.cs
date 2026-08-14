namespace ProductionManagement.Application.Contracts;

public sealed record LoginRequest(string? Username, string? Password);

public sealed record CurrentUserDto(Guid Id, string Username, string DisplayName, string Status);
