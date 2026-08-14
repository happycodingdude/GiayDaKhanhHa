using System.Security.Claims;
using ProductionManagement.Application.Abstractions;
using ProductionManagement.Domain;

namespace ProductionManagement.Api.Auth;

/// <summary>
/// Người dùng hiện tại luôn lấy từ ngữ cảnh xác thực; client không bao giờ tự gửi lên id người
/// dùng phục vụ audit (Step 4 §3).
/// </summary>
public sealed class HttpContextCurrentUser(IHttpContextAccessor httpContextAccessor) : ICurrentUser
{
    public bool IsAuthenticated => TryGetUserId(out _);

    /// <summary>
    /// Một principal đã được cookie middleware chấp nhận vẫn có thể mang id mà ứng dụng này không
    /// dùng được — ví dụ cookie phát hành hồi id còn là kiểu số. Đó là credential cũ chứ không phải
    /// lỗi server, nên phải trả về 401 thay vì 500.
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
