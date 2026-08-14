namespace ProductionManagement.Domain;

/// <summary>Một lỗi validation ở mức field (Step 4 §4, phần <c>details</c> của lỗi validation).</summary>
public sealed record ValidationFailure(string Field, string Code, string Message);

/// <summary>Ánh xạ sang HTTP 400 — request sai định dạng hoặc không qua được validation field.</summary>
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

/// <summary>Ánh xạ sang HTTP 422 — request đúng định dạng nhưng vi phạm luật nghiệp vụ.</summary>
public sealed class BusinessRuleException : Exception
{
    public string Code { get; }

    public BusinessRuleException(string code, string message) : base(message) => Code = code;
}

/// <summary>Ánh xạ sang HTTP 409 — thao tác xung đột với trạng thái hiện tại của server.</summary>
public sealed class ConflictException : Exception
{
    public string Code { get; }

    public ConflictException(string code, string message) : base(message) => Code = code;
}

/// <summary>Ánh xạ sang HTTP 404.</summary>
public sealed class NotFoundException : Exception
{
    public string Code { get; }

    public NotFoundException(string code, string message) : base(message) => Code = code;
}

/// <summary>Ánh xạ sang HTTP 401.</summary>
public sealed class UnauthenticatedException : Exception
{
    public string Code { get; }

    public UnauthenticatedException(string code, string message) : base(message) => Code = code;
}

/// <summary>Ánh xạ sang HTTP 403.</summary>
public sealed class ForbiddenException : Exception
{
    public string Code { get; }

    public ForbiddenException(string code, string message) : base(message) => Code = code;
}
