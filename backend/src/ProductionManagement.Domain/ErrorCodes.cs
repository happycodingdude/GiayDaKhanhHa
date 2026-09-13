namespace ProductionManagement.Domain;

/// <summary>
/// Mã lỗi nghiệp vụ trả về trong hợp đồng lỗi của API (Step 4 §4).
/// </summary>
public static class ErrorCodes
{
    public const string ValidationError = "VALIDATION_ERROR";
    public const string InternalError = "INTERNAL_ERROR";

    // Xác thực
    public const string InvalidCredentials = "INVALID_CREDENTIALS";
    public const string UserInactive = "USER_INACTIVE";
    public const string NotAuthenticated = "NOT_AUTHENTICATED";

    // Đơn hàng
    public const string OrderNotFound = "ORDER_NOT_FOUND";
    public const string ShoeCodeAlreadyExists = "SHOE_CODE_ALREADY_EXISTS";
    public const string OrderOverdue = "ORDER_OVERDUE";
    public const string OrderNotDeletable = "ORDER_NOT_DELETABLE";
    public const string InitialPlanTotalMismatch = "INITIAL_PLAN_TOTAL_MISMATCH";

    // Ảnh mẫu của đơn hàng (CR-001 §6.4, BR-N02, BR-N03)
    public const string ImageNotFound = "IMAGE_NOT_FOUND";
    public const string ImageTypeNotSupported = "IMAGE_TYPE_NOT_SUPPORTED";
    public const string ImageTooLarge = "IMAGE_TOO_LARGE";

    // Lập tiến độ (CR-001 §6.6, §6.10)
    public const string OrderScheduleAlreadyExists = "ORDER_SCHEDULE_ALREADY_EXISTS";
    public const string OrderNotScheduled = "ORDER_NOT_SCHEDULED";
    public const string LineAllocationMismatch = "LINE_ALLOCATION_MISMATCH";
    public const string LinePlanTotalMismatch = "LINE_PLAN_TOTAL_MISMATCH";

    // Dây chuyền sản xuất
    public const string ProductionLineNotFound = "PRODUCTION_LINE_NOT_FOUND";
    public const string ProductionLineInactive = "PRODUCTION_LINE_INACTIVE";
    public const string ProductionLineCodeAlreadyExists = "PRODUCTION_LINE_CODE_ALREADY_EXISTS";
    public const string ProductionLineEmptyAllocation = "PRODUCTION_LINE_EMPTY_ALLOCATION";

    // Kế hoạch sản xuất
    public const string ProductionPlanNotFound = "PRODUCTION_PLAN_NOT_FOUND";

    // Ngày sản xuất & các lần ghi nhận (CR-01 §6.4, §6.6)
    public const string ProductionEntryNotFound = "PRODUCTION_ENTRY_NOT_FOUND";
    public const string DayHasNoPlan = "DAY_HAS_NO_PLAN";
    public const string DayAlreadyClosed = "DAY_ALREADY_CLOSED";
    public const string FutureDateNotAllowed = "FUTURE_DATE_NOT_ALLOWED";
    public const string EntryExceedsDailyPlan = "ENTRY_EXCEEDS_DAILY_PLAN";
    public const string ActualExceedsOrderQuantity = "ACTUAL_EXCEEDS_ORDER_QUANTITY";
    public const string OrderAlreadyCompleted = "ORDER_ALREADY_COMPLETED";

    // Điều chỉnh
    public const string AdjustmentNotFound = "ADJUSTMENT_NOT_FOUND";
    public const string AdjustmentOutdated = "ADJUSTMENT_OUTDATED";
    public const string ActiveAdjustmentExists = "ACTIVE_ADJUSTMENT_EXISTS";
    public const string AdjustmentNotApplied = "ADJUSTMENT_NOT_APPLIED";
    public const string NoShortage = "NO_SHORTAGE";
    public const string InvalidAdjustmentTarget = "INVALID_ADJUSTMENT_TARGET";
    public const string DuplicateAdjustmentTarget = "DUPLICATE_ADJUSTMENT_TARGET";
    public const string AdjustmentTotalMismatch = "ADJUSTMENT_TOTAL_MISMATCH";
    public const string NoEligibleTargetDay = "NO_ELIGIBLE_TARGET_DAY";
    public const string SourceDayNotClosed = "SOURCE_DAY_NOT_CLOSED";
    public const string TargetDayClosed = "TARGET_DAY_CLOSED";
    public const string TargetDateInPast = "TARGET_DATE_IN_PAST";

    /// <summary>Bù sản lượng chỉ được thực hiện trong phạm vi cùng một dây chuyền (CR-001 BR-N12).</summary>
    public const string AdjustmentTargetLineMismatch = "ADJUSTMENT_TARGET_LINE_MISMATCH";
}
