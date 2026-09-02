namespace ProductionManagement.Domain.Services;

/// <summary>
/// Các giá trị sản xuất suy ra. Không giá trị nào được lưu xuống (Step 3 §13).
/// </summary>
public static class ProductionCalculations
{
    /// <summary>
    /// Phần thiếu của một ngày = max(PlannedQuantity - ActualQuantity, 0).
    ///
    /// Phần thiếu CHỈ tồn tại khi ngày đã Xuất hàng: ngày còn mở trả về null, không phải 0 (CR-01
    /// OV-5, N-07). Nhầm null thành 0 sẽ khiến dashboard báo "đạt kế hoạch" cho ngày đang sản xuất.
    /// </summary>
    public static int? Shortage(int plannedQuantity, int? closedActualQuantity)
    {
        if (closedActualQuantity is null)
        {
            return null;
        }

        return Math.Max(plannedQuantity - closedActualQuantity.Value, 0);
    }

    /// <summary>
    /// Chênh lệch ngày = Thực tế - Kế hoạch hiện tại, chỉ tính cho ngày đã Xuất hàng. Vì tổng ghi
    /// nhận trong ngày không được vượt kế hoạch (CR-01 OV-3), giá trị này luôn &lt;= 0.
    /// </summary>
    public static int? Difference(int plannedQuantity, int? closedActualQuantity)
    {
        return closedActualQuantity is null ? null : closedActualQuantity.Value - plannedQuantity;
    }

    /// <summary>
    /// Số còn được ghi nhận cho một ngày = MIN(trần kế hoạch ngày, trần số lượng đơn hàng),
    /// không bao giờ âm (CR-01 N-03).
    /// </summary>
    public static int RemainingAllowance(int plannedQuantity, int dayActual, int orderQuantity, int totalActual)
        => Math.Max(Math.Min(plannedQuantity - dayActual, orderQuantity - totalActual), 0);

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
