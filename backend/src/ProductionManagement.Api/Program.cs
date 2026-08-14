using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using ProductionManagement.Api.Auth;
using ProductionManagement.Api.Errors;
using ProductionManagement.Api.Setup;
using ProductionManagement.Application.Abstractions;
using ProductionManagement.Domain;
using ProductionManagement.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

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
    });

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
    });

// Mọi endpoint nghiệp vụ đều yêu cầu xác thực; chỉ action login/logout được miễn.
builder.Services.AddAuthorization(options =>
    options.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());

var app = builder.Build();

app.UseMiddleware<ExceptionHandlingMiddleware>();

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
