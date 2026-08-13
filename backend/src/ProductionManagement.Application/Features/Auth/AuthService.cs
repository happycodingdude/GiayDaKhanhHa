using Microsoft.EntityFrameworkCore;
using ProductionManagement.Application.Abstractions;
using ProductionManagement.Application.Contracts;
using ProductionManagement.Domain;
using ProductionManagement.Domain.Entities;

namespace ProductionManagement.Application.Features.Auth;

/// <summary>
/// Username + password authentication (Step 4 §2). The API layer turns a successful login into an
/// HttpOnly authentication cookie; no token is ever handed to JavaScript.
/// </summary>
public sealed class AuthService(IAppDbContext db, IPasswordHasher passwordHasher, ICurrentUser currentUser)
{
    public async Task<(User User, CurrentUserDto Dto)> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        var failures = new List<ValidationFailure>();
        if (string.IsNullOrWhiteSpace(request.Username))
        {
            failures.Add(new ValidationFailure("username", "REQUIRED", "Username is required."));
        }

        if (string.IsNullOrWhiteSpace(request.Password))
        {
            failures.Add(new ValidationFailure("password", "REQUIRED", "Password is required."));
        }

        if (failures.Count > 0)
        {
            throw new ValidationException(failures);
        }

        var username = request.Username!.Trim();
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Username == username, ct);

        // An unknown username and a wrong password are reported identically so the response does
        // not reveal which usernames exist.
        if (user is null || !passwordHasher.Verify(request.Password!, user.PasswordHash))
        {
            throw new UnauthenticatedException(ErrorCodes.InvalidCredentials, "Invalid username or password.");
        }

        if (!user.IsActive)
        {
            throw new ForbiddenException(ErrorCodes.UserInactive, "This account is inactive.");
        }

        return (user, ToDto(user));
    }

    public async Task<CurrentUserDto> GetCurrentUserAsync(CancellationToken ct = default)
    {
        if (!currentUser.IsAuthenticated)
        {
            throw new UnauthenticatedException(ErrorCodes.NotAuthenticated, "Authentication is required.");
        }

        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == currentUser.UserId, ct)
                   ?? throw new UnauthenticatedException(ErrorCodes.NotAuthenticated, "Authentication is required.");

        if (!user.IsActive)
        {
            throw new ForbiddenException(ErrorCodes.UserInactive, "This account is inactive.");
        }

        return ToDto(user);
    }

    private static CurrentUserDto ToDto(User user)
        => new(user.Id, user.Username, user.DisplayName, user.Status.ToString());
}
