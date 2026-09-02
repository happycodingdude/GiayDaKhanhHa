namespace ProductionManagement.Domain.Entities;

/// <summary>
/// Vết thay đổi của một lần ghi nhận sản lượng. Sửa/xoá trong ngày còn mở không bắt buộc nhập lý do
/// — màn hình dùng 8–10 lần mỗi ngày nên phải nhanh — nhưng vẫn phải truy vết được (CR-01 §12.1).
/// </summary>
public sealed class ProductionEntryLog
{
    private ProductionEntryLog() { }

    public Guid Id { get; private set; }
    public Guid ProductionEntryId { get; private set; }
    public ProductionEntryLogAction Action { get; private set; }
    public int? OldQuantity { get; private set; }
    public int? NewQuantity { get; private set; }
    public string? OldNote { get; private set; }
    public string? NewNote { get; private set; }
    public Guid ChangedBy { get; private set; }
    public DateTimeOffset ChangedAt { get; private set; }

    public static ProductionEntryLog Created(ProductionEntry entry, Guid userId, DateTimeOffset now)
        => New(entry.Id, ProductionEntryLogAction.Create, null, entry.Quantity, null, entry.Note, userId, now);

    public static ProductionEntryLog Updated(
        Guid entryId, int oldQuantity, string? oldNote, int newQuantity, string? newNote,
        Guid userId, DateTimeOffset now)
        => New(entryId, ProductionEntryLogAction.Update, oldQuantity, newQuantity, oldNote, newNote, userId, now);

    public static ProductionEntryLog Deleted(ProductionEntry entry, Guid userId, DateTimeOffset now)
        => New(entry.Id, ProductionEntryLogAction.Delete, entry.Quantity, null, entry.Note, null, userId, now);

    private static ProductionEntryLog New(
        Guid entryId, ProductionEntryLogAction action, int? oldQuantity, int? newQuantity,
        string? oldNote, string? newNote, Guid userId, DateTimeOffset now)
        => new()
        {
            Id = Guid.CreateVersion7(),
            ProductionEntryId = entryId,
            Action = action,
            OldQuantity = oldQuantity,
            NewQuantity = newQuantity,
            OldNote = oldNote,
            NewNote = newNote,
            ChangedBy = userId,
            ChangedAt = now
        };
}
