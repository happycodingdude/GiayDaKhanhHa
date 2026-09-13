namespace ProductionManagement.Domain.Entities;

/// <summary>
/// Một dây chuyền sản xuất của xưởng. Đây là danh mục cấu hình, không phải dữ liệu nghiệp vụ phát
/// sinh: kế hoạch và sản lượng tham chiếu tới nó, nên nó không bao giờ bị xoá cứng — chỉ chuyển
/// <see cref="ProductionLineStatus.Inactive"/> để không xuất hiện trong lựa chọn mới (CR-001 §2 QĐ-9,
/// BR-N14, BR-N15).
///
/// Phase này chưa có năng suất/ngày. <see cref="SortOrder"/> quyết định thứ tự chia đều ở tầng 1
/// (BR-N17), nên nó là dữ liệu nghiệp vụ chứ không chỉ là thứ tự hiển thị.
/// </summary>
public sealed class ProductionLine
{
    private ProductionLine() { }

    public Guid Id { get; private set; }
    public string Code { get; private set; } = null!;
    public string Name { get; private set; } = null!;
    public ProductionLineStatus Status { get; private set; }
    public int SortOrder { get; private set; }
    public string? Note { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public bool IsActive => Status == ProductionLineStatus.Active;

    public static ProductionLine Create(string? code, string? name, int sortOrder, string? note, DateTimeOffset now)
    {
        var (normalizedCode, normalizedName, normalizedNote) = Validate(code, name, sortOrder, note);

        return new ProductionLine
        {
            Id = Guid.CreateVersion7(),
            Code = normalizedCode,
            Name = normalizedName,
            // Dây chuyền vừa cấu hình là dây chuyền dùng được ngay; không có bước kích hoạt riêng.
            Status = ProductionLineStatus.Active,
            SortOrder = sortOrder,
            Note = normalizedNote,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    public void Update(string? code, string? name, int sortOrder, string? note, DateTimeOffset now)
    {
        var (normalizedCode, normalizedName, normalizedNote) = Validate(code, name, sortOrder, note);

        Code = normalizedCode;
        Name = normalizedName;
        SortOrder = sortOrder;
        Note = normalizedNote;
        UpdatedAt = now;
    }

    /// <summary>
    /// Chuyển sang Inactive luôn được phép, kể cả khi dây chuyền đang chạy đơn dở: nó chỉ ảnh hưởng
    /// tới lựa chọn mới, dữ liệu lịch sử vẫn hiển thị bình thường (CR-001 §6.3, BR-N14).
    /// </summary>
    public void ChangeStatus(ProductionLineStatus status, DateTimeOffset now)
    {
        if (Status == status)
        {
            return;
        }

        Status = status;
        UpdatedAt = now;
    }

    private static (string Code, string Name, string? Note) Validate(
        string? code, string? name, int sortOrder, string? note)
    {
        var failures = new List<ValidationFailure>();

        code = code?.Trim() ?? string.Empty;
        if (code.Length == 0)
        {
            failures.Add(new ValidationFailure("code", "REQUIRED", "Production line code is required."));
        }
        else if (code.Length > 30)
        {
            failures.Add(new ValidationFailure("code", "MAX_LENGTH_EXCEEDED", "Production line code must be at most 30 characters."));
        }

        name = name?.Trim() ?? string.Empty;
        if (name.Length == 0)
        {
            failures.Add(new ValidationFailure("name", "REQUIRED", "Production line name is required."));
        }
        else if (name.Length > 100)
        {
            failures.Add(new ValidationFailure("name", "MAX_LENGTH_EXCEEDED", "Production line name must be at most 100 characters."));
        }

        if (sortOrder < 0)
        {
            failures.Add(new ValidationFailure("sortOrder", "MUST_BE_GREATER_THAN_OR_EQUAL_TO_ZERO", "Sort order cannot be negative."));
        }

        note = note?.Trim();
        note = string.IsNullOrEmpty(note) ? null : note;
        if (note is { Length: > 500 })
        {
            failures.Add(new ValidationFailure("note", "MAX_LENGTH_EXCEEDED", "Note must be at most 500 characters."));
        }

        if (failures.Count > 0)
        {
            throw new ValidationException(failures);
        }

        return (code, name, note);
    }
}
