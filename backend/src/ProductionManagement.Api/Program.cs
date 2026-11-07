using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProductionManagement.Api.Auth;
using ProductionManagement.Api.Controllers;
using ProductionManagement.Api.Errors;
using ProductionManagement.Api.Setup;
using ProductionManagement.Application.Abstractions;
using ProductionManagement.Domain;
using ProductionManagement.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// Body JSON lớn nhất hợp lệ (một ma trận tiến độ) chỉ vài trăm KB. Endpoint upload ảnh tự nâng giới
// hạn của riêng nó bằng [RequestSizeLimit].
builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = 1024 * 1024);

builder.Services.AddProductionManagement(builder.Configuration);

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, HttpContextCurrentUser>();

builder.Services
    .AddControllers()
    .AddJsonOptions(options =>
    {
        // Ngày nghiệp vụ serialize theo dạng YYYY-MM-DD; enum truyền theo tên chứ không phải số thứ tự.
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.Never;
        // Không để message của System.Text.Json (có tên kiểu nội bộ) lọt ra client.
        options.AllowInputFormatterExceptionMessages = false;
    })
    // Body sai định dạng cũng trả về đúng khuôn lỗi { code, message, details } như mọi lỗi khác.
    .ConfigureApiBehaviorOptions(options => options.InvalidModelStateResponseFactory = context =>
        new BadRequestObjectResult(new ApiError(
            ErrorCodes.ValidationError,
            "One or more validation errors occurred.",
            context.ModelState
                .Where(entry => entry.Value?.Errors.Count > 0)
                .Select(entry => new ApiValidationDetail(
                    entry.Key.StartsWith("$.", StringComparison.Ordinal) ? entry.Key[2..] : entry.Key,
                    "INVALID_FORMAT",
                    "The value is invalid."))
                .ToList())));

// Xác thực bằng cookie HttpOnly — chủ đích chọn thay cho JWT ở Phase 1 (Step 4 §2).
builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "pm.auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
            ? CookieSecurePolicy.SameAsRequest
            : CookieSecurePolicy.Always;
        options.ExpireTimeSpan = TimeSpan.FromHours(12);
        options.SlidingExpiration = true;

        // Đây là API: request chưa xác thực nhận JSON 401/403, không bao giờ là redirect HTML.
        options.Events.OnRedirectToLogin = context => WriteAuthError(
            context.Response, StatusCodes.Status401Unauthorized,
            ErrorCodes.NotAuthenticated, "Authentication is required.");

        options.Events.OnRedirectToAccessDenied = context => WriteAuthError(
            context.Response, StatusCodes.Status403Forbidden,
            "FORBIDDEN", "You are not allowed to perform this action.");

        // Cookie còn hạn chưa đủ: tài khoản phải còn tồn tại và còn Active ở mỗi request, để khoá tài
        // khoản (Inactive) có hiệu lực ngay chứ không đợi cookie trượt hết hạn.
        options.Events.OnValidatePrincipal = async context =>
        {
            var db = context.HttpContext.RequestServices.GetRequiredService<IAppDbContext>();
            var isActiveUser =
                Guid.TryParse(context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)
                && await db.Users.AsNoTracking().AnyAsync(
                    u => u.Id == userId && u.Status == UserStatus.Active, context.HttpContext.RequestAborted);

            if (!isActiveUser)
            {
                context.RejectPrincipal();
                await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            }
        };
    });

// Chống dò mật khẩu: mỗi địa chỉ IP được thử đăng nhập tối đa 5 lần mỗi phút.
builder.Services.AddRateLimiter(options =>
{
    options.AddPolicy(AuthController.LoginRateLimitPolicy, context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 5, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));

    options.OnRejected = (context, _) => new ValueTask(WriteAuthError(
        context.HttpContext.Response, StatusCodes.Status429TooManyRequests,
        ErrorCodes.TooManyLoginAttempts, "Too many login attempts. Try again later."));
});

// Cookie đăng nhập được mã hoá bằng khoá DataProtection. Mặc định khoá nằm trong thư mục profile của
// tiến trình; trong container đó là filesystem tạm, nên mỗi lần tạo lại container mọi phiên đăng nhập
// đều mất. Đặt DataProtection:KeysPath (một volume) để khoá sống qua các lần deploy.
var dataProtectionKeysPath = builder.Configuration["DataProtection:KeysPath"];
if (!string.IsNullOrWhiteSpace(dataProtectionKeysPath))
{
    builder.Services.AddDataProtection().PersistKeysToFileSystem(new DirectoryInfo(dataProtectionKeysPath));
}

// Mọi endpoint nghiệp vụ đều yêu cầu xác thực; chỉ action login/logout được miễn.
builder.Services.AddAuthorization(options =>
    options.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());

var app = builder.Build();

// Đặt lúc response bắt đầu gửi để header còn nguyên kể cả khi ExceptionHandlingMiddleware Clear() response.
app.Use((context, next) =>
{
    context.Response.OnStarting(() =>
    {
        context.Response.Headers.XContentTypeOptions = "nosniff";
        return Task.CompletedTask;
    });
    return next(context);
});

app.UseMiddleware<ExceptionHandlingMiddleware>();

// Chống CSRF: request ghi dữ liệu phải xuất phát từ chính origin của app. SameSite=Lax vẫn gửi cookie
// cho trang khác cổng trên cùng host, nên dựa thêm vào header Sec-Fetch-Site mà trình duyệt tự đặt
// (trang không sửa được). Client không phải trình duyệt không gửi header này thì không bị chặn.
app.Use(async (context, next) =>
{
    var method = context.Request.Method;
    var isUnsafe = !(HttpMethods.IsGet(method) || HttpMethods.IsHead(method) || HttpMethods.IsOptions(method));

    if (isUnsafe
        && context.Request.Headers.TryGetValue("Sec-Fetch-Site", out var site)
        && site != "same-origin"
        && site != "none")
    {
        await WriteAuthError(context.Response, StatusCodes.Status403Forbidden,
            ErrorCodes.CrossSiteRequestBlocked, "Cross-site requests are not allowed.");
        return;
    }

    await next(context);
});

app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

await DatabaseInitializer.InitializeAsync(app.Services, app.Configuration, app.Logger);

app.Run();

static Task WriteAuthError(HttpResponse response, int statusCode, string code, string message)
{
    response.StatusCode = statusCode;
    response.ContentType = "application/json";

    var payload = JsonSerializer.Serialize(
        new ApiError(code, message, null),
        new JsonSerializerOptions(JsonSerializerDefaults.Web));

    return response.WriteAsync(payload);
}

/// <summary>Để lộ ra để host của integration test tham chiếu được entry point này.</summary>
public partial class Program;
