using System.Security.Claims;
using ProductionManagement.Application.Abstractions;
using ProductionManagement.Domain;

namespace ProductionManagement.Api.Auth;

/// <summary>
/// The current user always comes from the authentication context; the client never supplies
/// audit user ids (Step 4 §3).
/// </summary>
public sealed class HttpContextCurrentUser(IHttpContextAccessor httpContextAccessor) : ICurrentUser
{
    public bool IsAuthenticated => TryGetUserId(out _);

    /// <summary>
    /// A principal the cookie middleware accepted can still carry an id this application cannot
    /// use — a cookie issued while ids were still numeric, for instance. That is a stale
    /// credential, not a server fault, so it has to surface as 401 rather than 500.
    /// </summary>
    public Guid UserId => TryGetUserId(out var userId)
        ? userId
        : throw new UnauthenticatedException(
            ErrorCodes.NotAuthenticated, "Authentication is required.");

    private bool TryGetUserId(out Guid userId)
    {
        userId = Guid.Empty;

        var principal = httpContextAccessor.HttpContext?.User;
        if (principal?.Identity?.IsAuthenticated != true)
        {
            return false;
        }

        var value = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(value, out userId);
    }
}
