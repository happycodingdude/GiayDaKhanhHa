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
    public const string OrderCodeAlreadyExists = "ORDER_CODE_ALREADY_EXISTS";
    public const string OrderCompleted = "ORDER_COMPLETED";
    public const string OrderOverdue = "ORDER_OVERDUE";
    public const string InitialPlanTotalMismatch = "INITIAL_PLAN_TOTAL_MISMATCH";

    // Kế hoạch / bản ghi sản xuất
    public const string ProductionPlanNotFound = "PRODUCTION_PLAN_NOT_FOUND";
    public const string ProductionRecordNotFound = "PRODUCTION_RECORD_NOT_FOUND";
    public const string ProductionRecordAlreadyExists = "PRODUCTION_RECORD_ALREADY_EXISTS";
    public const string NoProductionPlanForDate = "NO_PRODUCTION_PLAN_FOR_DATE";
    public const string PlanQuantityIsZero = "PLAN_QUANTITY_IS_ZERO";
    public const string FutureProductionDate = "FUTURE_PRODUCTION_DATE";
    public const string ActualExceedsOrderQuantity = "ACTUAL_EXCEEDS_ORDER_QUANTITY";

    // Điều chỉnh
    public const string AdjustmentNotFound = "ADJUSTMENT_NOT_FOUND";
    public const string AdjustmentOutdated = "ADJUSTMENT_OUTDATED";
    public const string ActiveAdjustmentExists = "ACTIVE_ADJUSTMENT_EXISTS";
    public const string AdjustmentNotApplied = "ADJUSTMENT_NOT_APPLIED";
    public const string NoShortage = "NO_SHORTAGE";
    public const string InvalidAdjustmentTarget = "INVALID_ADJUSTMENT_TARGET";
    public const string DuplicateAdjustmentTarget = "DUPLICATE_ADJUSTMENT_TARGET";
    public const string AdjustmentTotalMismatch = "ADJUSTMENT_TOTAL_MISMATCH";
    public const string NoEligibleTargetPlans = "NO_ELIGIBLE_TARGET_PLANS";
}
