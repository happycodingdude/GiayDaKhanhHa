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
        // Business dates serialise as YYYY-MM-DD; enums travel as their names, not their ordinals.
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.Never;
    });

// HttpOnly cookie authentication — explicitly chosen over JWT for Phase 1 (Step 4 §2).
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

        // This is an API: unauthenticated requests get a JSON 401/403, never an HTML redirect.
        options.Events.OnRedirectToLogin = context => WriteAuthError(
            context.Response, StatusCodes.Status401Unauthorized,
            ErrorCodes.NotAuthenticated, "Authentication is required.");

        options.Events.OnRedirectToAccessDenied = context => WriteAuthError(
            context.Response, StatusCodes.Status403Forbidden,
            "FORBIDDEN", "You are not allowed to perform this action.");
    });

// Every business endpoint requires authentication; only the login/logout actions opt out.
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

/// <summary>Exposed so the integration test host can reference this entry point.</summary>
public partial class Program;
