namespace ProductionManagement.Domain;

/// <summary>
/// Lưu dưới dạng varchar + ràng buộc CHECK (Step 3 §5). Không dùng enum gốc của PostgreSQL.
/// </summary>
public enum UserStatus
{
    Active,
    Inactive
}

public enum OrderStatus
{
    Incomplete,
    Completed
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
