namespace ProductionManagement.Domain.Entities;

/// <summary>
/// Một dây chuyền được gán cho tiến độ của đơn hàng, kèm số lượng phân bổ ở tầng 1 (CR-001 §5.3).
///
/// Bảng này giữ hai vai trò không thay thế được bằng suy diễn:
/// 1. Xác định dây chuyền nào thuộc tiến độ — không suy ra từ <c>production_plans</c>, vì một dây
///    chuyền có thể không có ô nào khác 0.
/// 2. Giữ <see cref="AllocatedQuantity"/> làm mốc đối chiếu gốc. Không suy ngược được từ tổng
///    <c>planned_quantity</c> vì adjustment sẽ làm con số đó tăng lên (BR-N18).
/// </summary>
public sealed class OrderProductionLine
{
    private OrderProductionLine() { }

    public Guid OrderId { get; private set; }
    public Order Order { get; private set; } = null!;
    public Guid ProductionLineId { get; private set; }
    public ProductionLine ProductionLine { get; private set; } = null!;

    /// <summary>
    /// Chỉ đổi được khi sửa tiến độ chưa chốt; đã chốt thì bất biến. Adjustment không bao giờ đụng
    /// tới nó (BR-N18).
    /// </summary>
    public int AllocatedQuantity { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    internal static OrderProductionLine Create(
        Order order, Guid productionLineId, int allocatedQuantity, DateTimeOffset now)
        => new()
        {
            Order = order,
            ProductionLineId = productionLineId,
            AllocatedQuantity = allocatedQuantity,
            CreatedAt = now
        };

    /// <summary>Chỉ <see cref="Order.Reschedule"/> gọi, khi tiến độ còn chưa chốt.</summary>
    internal void ChangeAllocation(int allocatedQuantity) => AllocatedQuantity = allocatedQuantity;
}
