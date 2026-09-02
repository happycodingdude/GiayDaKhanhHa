using System.Text.Json;
using System.Text.Json.Serialization;
using ProductionManagement.Domain;

namespace ProductionManagement.Api.Errors;

/// <summary>
/// Ánh xạ exception của domain/application sang ngữ nghĩa HTTP đã duyệt (Step 4 §4).
/// Chi tiết kỹ thuật của exception không bao giờ bị lộ ra client (Step 5 §12).
/// </summary>
public sealed class ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            var (status, error) = Map(ex);

            if (status >= StatusCodes.Status500InternalServerError)
            {
                logger.LogError(ex, "Unhandled exception while processing {Method} {Path}",
                    context.Request.Method, context.Request.Path);
            }
            else
            {
                logger.LogInformation("{Method} {Path} rejected with {Status} ({Code})",
                    context.Request.Method, context.Request.Path, status, error.Code);
            }

            if (context.Response.HasStarted)
            {
                throw;
            }

            context.Response.Clear();
            context.Response.StatusCode = status;
            context.Response.ContentType = "application/json";

            await context.Response.WriteAsync(JsonSerializer.Serialize(error, SerializerOptions));
        }
    }

    private static (int Status, ApiError Error) Map(Exception exception) => exception switch
    {
        ValidationException ex => (
            StatusCodes.Status400BadRequest,
            new ApiError(
                ErrorCodes.ValidationError,
                "One or more validation errors occurred.",
                ex.Failures.Select(f => new ApiValidationDetail(f.Field, f.Code, f.Message)).ToList())),

        UnauthenticatedException ex => (
            StatusCodes.Status401Unauthorized, new ApiError(ex.Code, ex.Message, null)),

        ForbiddenException ex => (
            StatusCodes.Status403Forbidden, new ApiError(ex.Code, ex.Message, null)),

        NotFoundException ex => (
            StatusCodes.Status404NotFound, new ApiError(ex.Code, ex.Message, null)),

        ConflictException ex => (
            StatusCodes.Status409Conflict, new ApiError(ex.Code, ex.Message, null)),

        BusinessRuleException ex => (
            StatusCodes.Status422UnprocessableEntity,
            new ApiError(
                ex.Code,
                ex.Message,
                ex.Details.Count == 0
                    ? null
                    : ex.Details.Select(f => new ApiValidationDetail(f.Field, f.Code, f.Message)).ToList())),

        _ => (
            StatusCodes.Status500InternalServerError,
            new ApiError(ErrorCodes.InternalError, "An unexpected error occurred.", null))
    };
}
