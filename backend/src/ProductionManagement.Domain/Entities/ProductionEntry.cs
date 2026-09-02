namespace ProductionManagement.Domain.Entities;

/// <summary>
/// Một lần ghi nhận sản lượng trong ngày. Sản lượng thực tế là số cộng thêm: một ngày có N lần ghi
/// nhận, và sản lượng của ngày là tổng các lần chưa xoá (CR-01 OV-1, OV-2).
///
/// Xoá là xoá mềm để lịch sử "đã nhập những gì" vẫn dựng lại được sau khi sửa/xoá giữa chừng.
/// </summary>
public sealed class ProductionEntry
{
    private ProductionEntry() { }

    public Guid Id { get; private set; }
    public Guid ProductionDayId { get; private set; }
    public ProductionDay ProductionDay { get; private set; } = null!;
    public int Quantity { get; private set; }
    public DateTimeOffset RecordedAt { get; private set; }
    public string? Note { get; private set; }
    public DateTimeOffset? DeletedAt { get; private set; }
    public Guid CreatedBy { get; private set; }
    public Guid UpdatedBy { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public bool IsDeleted => DeletedAt is not null;

    /// <summary>Sửa số lượng ít nhất một lần thì dòng lịch sử được đánh dấu là đã sửa.</summary>
    public bool IsEdited => UpdatedAt != CreatedAt;

    public static ProductionEntry Create(
        Guid productionDayId, int quantity, string? note, Guid userId, DateTimeOffset now)
    {
        GuardQuantity(quantity);
        note = NormalizeNote(note);

        return new ProductionEntry
        {
            Id = Guid.CreateVersion7(),
            ProductionDayId = productionDayId,
            Quantity = quantity,
            RecordedAt = now,
            Note = note,
            CreatedBy = userId,
            UpdatedBy = userId,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    public void Update(int quantity, string? note, Guid userId, DateTimeOffset now)
    {
        GuardQuantity(quantity);

        Quantity = quantity;
        Note = NormalizeNote(note);
        UpdatedBy = userId;
        UpdatedAt = now;
    }

    public void Delete(Guid userId, DateTimeOffset now)
    {
        if (IsDeleted)
        {
            return;
        }

        DeletedAt = now;
        UpdatedBy = userId;
        UpdatedAt = now;
    }

    /// <summary>
    /// Ghi nhận bằng 0 là vô nghĩa. "Cả ngày không sản xuất được" thể hiện bằng Xuất hàng với 0 lần
    /// ghi nhận, chứ không phải bằng một lần ghi nhận bằng 0 (CR-01 §5.2, N-01).
    /// </summary>
    private static void GuardQuantity(int quantity)
    {
        if (quantity <= 0)
        {
            throw new ValidationException(
                "quantity", "MUST_BE_GREATER_THAN_ZERO", "Quantity must be greater than zero.");
        }
    }

    private static string? NormalizeNote(string? note)
    {
        note = note?.Trim();
        if (string.IsNullOrEmpty(note))
        {
            return null;
        }

        if (note.Length > 255)
        {
            throw new ValidationException("note", "MAX_LENGTH_EXCEEDED", "Note must be at most 255 characters.");
        }

        return note;
    }
}
