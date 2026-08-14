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
