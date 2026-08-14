namespace ProductionManagement.Api.Errors;

/// <summary>
/// Khuôn lỗi duy nhất mà mọi endpoint trả về (Step 4 §4).
/// </summary>
public sealed record ApiError(string Code, string Message, object? Details);

public sealed record ApiValidationDetail(string Field, string Code, string Message);
