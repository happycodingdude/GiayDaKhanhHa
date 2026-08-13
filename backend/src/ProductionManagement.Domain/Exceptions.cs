namespace ProductionManagement.Domain;

/// <summary>A single field-level validation failure (Step 4 §4 validation error <c>details</c>).</summary>
public sealed record ValidationFailure(string Field, string Code, string Message);

/// <summary>Maps to HTTP 400 — the request itself is malformed or fails field validation.</summary>
public sealed class ValidationException : Exception
{
    public IReadOnlyList<ValidationFailure> Failures { get; }

    public ValidationException(IReadOnlyList<ValidationFailure> failures)
        : base("One or more validation errors occurred.")
    {
        Failures = failures;
    }

    public ValidationException(string field, string code, string message)
        : this([new ValidationFailure(field, code, message)])
    {
    }
}

/// <summary>Maps to HTTP 422 — the request is well formed but violates a business rule.</summary>
public sealed class BusinessRuleException : Exception
{
    public string Code { get; }

    public BusinessRuleException(string code, string message) : base(message) => Code = code;
}

/// <summary>Maps to HTTP 409 — the operation conflicts with the current server state.</summary>
public sealed class ConflictException : Exception
{
    public string Code { get; }

    public ConflictException(string code, string message) : base(message) => Code = code;
}

/// <summary>Maps to HTTP 404.</summary>
public sealed class NotFoundException : Exception
{
    public string Code { get; }

    public NotFoundException(string code, string message) : base(message) => Code = code;
}

/// <summary>Maps to HTTP 401.</summary>
public sealed class UnauthenticatedException : Exception
{
    public string Code { get; }

    public UnauthenticatedException(string code, string message) : base(message) => Code = code;
}

/// <summary>Maps to HTTP 403.</summary>
public sealed class ForbiddenException : Exception
{
    public string Code { get; }

    public ForbiddenException(string code, string message) : base(message) => Code = code;
}
