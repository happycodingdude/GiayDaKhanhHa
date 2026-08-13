using System.Security.Claims;
using ProductionManagement.Application.Abstractions;

namespace ProductionManagement.Api.Auth;

/// <summary>
/// The current user always comes from the authentication context; the client never supplies
/// audit user ids (Step 4 §3).
/// </summary>
public sealed class HttpContextCurrentUser(IHttpContextAccessor httpContextAccessor) : ICurrentUser
{
    public bool IsAuthenticated => TryGetUserId(out _);

    public long UserId => TryGetUserId(out var userId)
        ? userId
        : throw new InvalidOperationException("There is no authenticated user on the current request.");

    private bool TryGetUserId(out long userId)
    {
        userId = 0;

        var principal = httpContextAccessor.HttpContext?.User;
        if (principal?.Identity?.IsAuthenticated != true)
        {
            return false;
        }

        var value = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        return long.TryParse(value, out userId);
    }
}
