namespace ProductionManagement.Api.Errors;

/// <summary>
/// The single error shape returned by every endpoint (Step 4 §4).
/// </summary>
public sealed record ApiError(string Code, string Message, object? Details);

public sealed record ApiValidationDetail(string Field, string Code, string Message);
