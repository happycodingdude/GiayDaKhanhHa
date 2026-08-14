namespace ProductionManagement.Domain.Services;

/// <summary>
/// Các giá trị sản xuất suy ra. Không giá trị nào được lưu xuống (Step 3 §13).
/// </summary>
public static class ProductionCalculations
{
    /// <summary>
    /// Phần thiếu của một ngày = max(PlannedQuantity - ActualQuantity, 0).
    /// Ngày chưa có bản ghi sản xuất là chưa nhập, không phải là thiếu.
    /// </summary>
    public static int Shortage(int plannedQuantity, int? actualQuantity)
    {
        if (actualQuantity is null)
        {
            return 0;
        }

        return Math.Max(plannedQuantity - actualQuantity.Value, 0);
    }

    /// <summary>Chênh lệch ngày = Thực tế - Kế hoạch hiện tại. Null khi chưa nhập thực tế.</summary>
    public static int? Difference(int plannedQuantity, int? actualQuantity)
    {
        return actualQuantity is null ? null : actualQuantity.Value - plannedQuantity;
    }

    /// <summary>Còn lại = Order.Quantity - TotalActual, không bao giờ âm.</summary>
    public static int Remaining(int orderQuantity, int totalActual) => Math.Max(orderQuantity - totalActual, 0);

    /// <summary>Tiến độ = TotalActual / Order.Quantity, theo phần trăm làm tròn một chữ số thập phân.</summary>
    public static decimal ProgressPercentage(int orderQuantity, int totalActual)
    {
        if (orderQuantity <= 0)
        {
            return 0m;
        }

        return Math.Round(totalActual * 100m / orderQuantity, 1, MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// Đơn hàng chậm tiến độ bao nhiêu: kế hoạch lũy kế tính tới hết hôm nay trừ thực tế lũy kế trên
    /// cùng những ngày đó, chặn dưới ở 0 (master summary §5).
    /// </summary>
    public static int BehindScheduleQuantity(int cumulativePlanToDate, int cumulativeActualToDate)
        => Math.Max(cumulativePlanToDate - cumulativeActualToDate, 0);
}
