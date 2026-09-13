namespace ProductionManagement.Domain;

/// <summary>
/// Lưu dưới dạng varchar + ràng buộc CHECK (Step 3 §5). Không dùng enum gốc của PostgreSQL.
/// </summary>
public enum UserStatus
{
    Active,
    Inactive
}

/// <summary>
/// Vòng đời đơn hàng sau CR-001: nhập hàng xong là <c>Pending</c> (chưa lập tiến độ), lập tiến độ
/// xong chuyển <c>Incomplete</c>, đủ sản lượng thì <c>Completed</c>. Không bao giờ quay lại
/// <c>Pending</c> (CR-001 §4.1).
/// </summary>
public enum OrderStatus
{
    Pending,
    Incomplete,
    Completed
}

/// <summary>Dây chuyền không xoá cứng, chỉ bật/tắt (CR-001 §2 QĐ-9).</summary>
public enum ProductionLineStatus
{
    Active,
    Inactive
}

/// <summary>
/// Cách quản lý phân bổ số lượng đơn cho các dây chuyền ở tầng 1. Chỉ là công cụ nhập liệu phía UI,
/// không lưu vào database — backend luôn validate con số thật được gửi lên (CR-001 BR-N16, §6.6).
/// </summary>
public enum AllocationMode
{
    Even,
    Manual
}

/// <summary>Vòng đời của một ngày sản xuất. Close là một chiều — không có reopen (CR-01 N-06).</summary>
public enum ProductionDayStatus
{
    Open,
    Closed
}

public enum ProductionEntryLogAction
{
    Create,
    Update,
    Delete
}

/// <summary>
/// Trạng thái hiển thị của một ngày. Chỉ tồn tại ở tầng DTO, không bao giờ lưu xuống: khi một khoản
/// bù làm kế hoạch của ngày từ 0 thành 40 thì trạng thái lưu cứng sẽ lệch (CR-01 §4.3, §14.3).
/// </summary>
public enum ProductionDayDisplayStatus
{
    NoPlan,
    NotStarted,
    InProduction,
    Closed
}

/// <summary>Ràng buộc nào đang chặn ô "Còn được nhập", để UI chọn đúng câu thông báo.</summary>
public enum RemainingAllowanceReason
{
    DailyPlan,
    OrderQuantity
}

public enum AdjustmentType
{
    /// <summary>Option 1 — quản lý tự chọn (các) ngày sản xuất đích.</summary>
    Manual,

    /// <summary>Option 2 — hệ thống chia đều phần thiếu cho các ngày còn lại.</summary>
    Automatic
}

public enum AdjustmentStatus
{
    Applied,
    Reversed
}
