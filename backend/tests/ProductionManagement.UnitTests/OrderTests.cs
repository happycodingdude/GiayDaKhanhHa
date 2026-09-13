using ProductionManagement.Domain;
using ProductionManagement.Domain.Entities;
using Xunit;

namespace ProductionManagement.UnitTests;

/// <summary>Bước Nhập hàng: đơn ra đời ở <c>Pending</c>, chưa có gì thuộc về tiến độ (CR-001 §4.1).</summary>
public class OrderReceiptTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 7, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void A_received_order_is_pending_with_no_schedule_data()
    {
        var order = Order.Receive("SH-2026-001", 1000, Now);

        Assert.Equal(OrderStatus.Pending, order.Status);
        Assert.False(order.IsScheduled);
        Assert.Null(order.StartDate);
        Assert.Null(order.DueDate);
        Assert.Empty(order.ProductionLines);
        Assert.Empty(order.ProductionPlans);
        Assert.False(order.HasImage);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    public void Receive_rejects_a_non_positive_quantity(int quantity)
    {
        var exception = Assert.Throws<ValidationException>(() => Order.Receive("SH-2026-001", quantity, Now));

        Assert.Contains(exception.Failures, f => f.Code == "MUST_BE_GREATER_THAN_ZERO");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Receive_requires_a_shoe_code(string? shoeCode)
    {
        var exception = Assert.Throws<ValidationException>(() => Order.Receive(shoeCode, 100, Now));

        Assert.Contains(exception.Failures, f => f.Field == "shoeCode" && f.Code == "REQUIRED");
    }

    [Fact]
    public void A_new_image_replaces_the_old_one_and_hands_back_the_old_path()
    {
        var order = Order.Receive("SH-2026-001", 100, Now);
        order.AttachImage(new OrderImage("ab/first.jpg", "first.jpg", "image/jpeg", 1024), Now);

        var previous = order.AttachImage(
            new OrderImage("ab/second.png", "second.png", "image/png", 2048), Now);

        Assert.Equal("ab/first.jpg", previous);
        Assert.Equal("ab/second.png", order.ImagePath);
    }

    [Fact]
    public void Removing_the_image_clears_all_four_columns_together()
    {
        var order = Order.Receive("SH-2026-001", 100, Now);
        order.AttachImage(new OrderImage("ab/first.jpg", "first.jpg", "image/jpeg", 1024), Now);

        var removed = order.RemoveImage(Now);

        Assert.Equal("ab/first.jpg", removed);
        Assert.False(order.HasImage);
        Assert.Null(order.ImagePath);
        Assert.Null(order.ImageFileName);
        Assert.Null(order.ImageContentType);
        Assert.Null(order.ImageSizeBytes);
    }

    [Fact]
    public void The_quantity_is_frozen_once_the_order_has_a_schedule()
    {
        var order = OrderScheduleTests.ScheduledOrder();

        // Số lượng đơn là mốc đối chiếu của phân bổ hai tầng; đổi một mình nó sẽ phá BR-N08.
        var exception = Assert.Throws<BusinessRuleException>(
            () => order.UpdateReceipt("SH-2026-001", 900, Now));

        Assert.Equal(ErrorCodes.OrderScheduleAlreadyExists, exception.Code);
    }

    [Fact]
    public void The_shoe_code_can_still_be_corrected_after_scheduling()
    {
        var order = OrderScheduleTests.ScheduledOrder();

        order.UpdateReceipt("SH-2026-001-B", order.Quantity, Now);

        Assert.Equal("SH-2026-001-B", order.ShoeCode);
    }

    [Fact]
    public void A_pending_order_can_be_deleted()
    {
        var order = Order.Receive("SH-2026-001", 1000, Now);

        order.EnsureDeletable();
    }

    [Fact]
    public void A_scheduled_order_can_no_longer_be_deleted()
    {
        var order = OrderScheduleTests.ScheduledOrder();

        var exception = Assert.Throws<BusinessRuleException>(order.EnsureDeletable);

        Assert.Equal(ErrorCodes.OrderNotDeletable, exception.Code);
    }
}

/// <summary>Bước Lập tiến độ: phân bổ hai tầng và các bất biến của nó (CR-001 §6.6, §9).</summary>
public class OrderScheduleTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 7, 10, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Start = new(2026, 9, 10);
    private static readonly DateOnly Due = new(2026, 9, 12);

    private static readonly Guid Line1 = TestIds.Of(1);
    private static readonly Guid Line2 = TestIds.Of(2);

    private static ScheduleLine Line(Guid id, int allocated, params (DateOnly, int)[] plans)
        => new(id, allocated, plans.ToList());

    /// <summary>1.000 đôi trên hai dây chuyền, ba ngày — cấu hình chuẩn của mọi test bên dưới.</summary>
    internal static Order ScheduledOrder()
    {
        var order = Order.Receive("SH-2026-001", 1000, Now);

        order.Schedule(Start, Due,
        [
            Line(Line1, 600, (Start, 200), (Start.AddDays(1), 200), (Start.AddDays(2), 200)),
            Line(Line2, 400, (Start, 100), (Start.AddDays(1), 150), (Start.AddDays(2), 150)),
        ], Now);

        return order;
    }

    [Fact]
    public void Scheduling_moves_the_order_to_incomplete_and_creates_every_cell()
    {
        var order = ScheduledOrder();

        Assert.Equal(OrderStatus.Incomplete, order.Status);
        Assert.Equal(Start, order.StartDate);
        Assert.Equal(Due, order.DueDate);
        Assert.Equal(2, order.ProductionLines.Count);
        Assert.Equal(6, order.ProductionPlans.Count);
    }

    [Fact]
    public void Tier_one_requires_the_line_allocations_to_equal_the_order_quantity()
    {
        var order = Order.Receive("SH-2026-001", 1000, Now);

        var exception = Assert.Throws<BusinessRuleException>(() => order.Schedule(Start, Due,
        [
            Line(Line1, 500, (Start, 500)),
            Line(Line2, 400, (Start, 400)),
        ], Now));

        Assert.Equal(ErrorCodes.LineAllocationMismatch, exception.Code);
        Assert.Equal(OrderStatus.Pending, order.Status);
    }

    [Fact]
    public void Tier_two_requires_each_line_daily_plan_to_equal_its_own_allocation()
    {
        var order = Order.Receive("SH-2026-001", 1000, Now);

        var exception = Assert.Throws<BusinessRuleException>(() => order.Schedule(Start, Due,
        [
            Line(Line1, 600, (Start, 200), (Start.AddDays(1), 200), (Start.AddDays(2), 200)),
            // Tổng của dây chuyền 2 chỉ có 350 trong khi được phân 400.
            Line(Line2, 400, (Start, 100), (Start.AddDays(1), 150), (Start.AddDays(2), 100)),
        ], Now));

        Assert.Equal(ErrorCodes.LinePlanTotalMismatch, exception.Code);

        // details phải chỉ đúng dây chuyền lệch để UI tô đỏ đúng cột (CR-001 §6.6 bước 8).
        var detail = Assert.Single(exception.Details);
        Assert.Equal(Line2.ToString(), detail.Field);
        Assert.Equal("350/400", detail.Message);
    }

    [Fact]
    public void Tier_one_and_tier_two_together_keep_the_baseline_invariant()
    {
        var order = ScheduledOrder();

        // BR-N08c: hệ quả của hai tầng là tổng kế hoạch ban đầu bằng đúng số lượng đơn.
        Assert.Equal(order.Quantity, order.ProductionPlans.Sum(p => p.InitialPlannedQuantity));
        Assert.Equal(order.Quantity, order.ProductionLines.Sum(l => l.AllocatedQuantity));
    }

    [Fact]
    public void A_selected_line_cannot_be_allocated_zero()
    {
        var order = Order.Receive("SH-2026-001", 1000, Now);

        var exception = Assert.Throws<BusinessRuleException>(() => order.Schedule(Start, Due,
        [
            Line(Line1, 1000, (Start, 1000)),
            Line(Line2, 0),
        ], Now));

        Assert.Equal(ErrorCodes.ProductionLineEmptyAllocation, exception.Code);
    }

    [Fact]
    public void A_cell_planned_for_zero_creates_no_plan_row_at_all()
    {
        var order = Order.Receive("SH-2026-001", 300, Now);

        order.Schedule(Start, Due,
        [
            // Dây chuyền nghỉ ngày giữa: ô đó không được tạo dòng plan (CR-001 §6.6b, BR-N10).
            Line(Line1, 300, (Start, 150), (Start.AddDays(1), 0), (Start.AddDays(2), 150)),
        ], Now);

        Assert.Equal(2, order.ProductionPlans.Count);
        Assert.DoesNotContain(order.ProductionPlans, p => p.ProductionDate == Start.AddDays(1));
    }

    [Fact]
    public void Scheduling_is_a_one_time_operation()
    {
        var order = ScheduledOrder();

        var exception = Assert.Throws<ConflictException>(() => order.Schedule(Start, Due,
        [
            Line(Line1, 1000, (Start, 1000)),
        ], Now));

        Assert.Equal(ErrorCodes.OrderScheduleAlreadyExists, exception.Code);
    }

    [Fact]
    public void Scheduling_rejects_a_due_date_before_the_start_date()
    {
        var order = Order.Receive("SH-2026-001", 100, Now);

        var exception = Assert.Throws<ValidationException>(() => order.Schedule(Due, Start,
        [
            Line(Line1, 100, (Start, 100)),
        ], Now));

        Assert.Contains(exception.Failures, f => f.Code == "DUE_DATE_BEFORE_START_DATE");
    }

    [Fact]
    public void Scheduling_rejects_a_date_outside_the_production_period()
    {
        var order = Order.Receive("SH-2026-001", 100, Now);

        var exception = Assert.Throws<ValidationException>(() => order.Schedule(Start, Due,
        [
            Line(Line1, 100, (Due.AddDays(1), 100)),
        ], Now));

        Assert.Contains(exception.Failures, f => f.Code == "OUT_OF_PRODUCTION_PERIOD");
    }

    [Fact]
    public void Scheduling_rejects_the_same_date_twice_on_one_line()
    {
        var order = Order.Receive("SH-2026-001", 100, Now);

        var exception = Assert.Throws<ValidationException>(() => order.Schedule(Start, Due,
        [
            Line(Line1, 100, (Start, 50), (Start, 50)),
        ], Now));

        Assert.Contains(exception.Failures, f => f.Code == "DUPLICATE_PRODUCTION_DATE");
    }

    [Fact]
    public void The_same_date_on_two_different_lines_is_perfectly_valid()
    {
        // Chính là điểm mấu chốt của CR-001: khoá là (ngày, dây chuyền), không phải ngày.
        var order = ScheduledOrder();

        Assert.Equal(2, order.ProductionPlans.Count(p => p.ProductionDate == Start));
    }

    [Fact]
    public void Scheduling_rejects_the_same_line_twice()
    {
        var order = Order.Receive("SH-2026-001", 100, Now);

        var exception = Assert.Throws<ValidationException>(() => order.Schedule(Start, Due,
        [
            Line(Line1, 50, (Start, 50)),
            Line(Line1, 50, (Start, 50)),
        ], Now));

        Assert.Contains(exception.Failures, f => f.Code == "DUPLICATE_PRODUCTION_LINE");
    }

    [Fact]
    public void Scheduling_requires_at_least_one_line()
    {
        var order = Order.Receive("SH-2026-001", 100, Now);

        var exception = Assert.Throws<ValidationException>(() => order.Schedule(Start, Due, [], Now));

        Assert.Contains(exception.Failures, f => f.Field == "lines" && f.Code == "REQUIRED");
    }
}

public class OrderStatusTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 7, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void RecalculateStatus_completes_the_order_when_the_total_actual_reaches_the_quantity()
    {
        var order = OrderScheduleTests.ScheduledOrder();

        order.RecalculateStatus(1000, Now);

        Assert.Equal(OrderStatus.Completed, order.Status);
    }

    [Fact]
    public void RecalculateStatus_keeps_the_order_incomplete_below_the_quantity()
    {
        var order = OrderScheduleTests.ScheduledOrder();

        order.RecalculateStatus(999, Now);

        Assert.Equal(OrderStatus.Incomplete, order.Status);
    }

    [Fact]
    public void RecalculateStatus_returns_a_completed_order_to_incomplete_when_the_total_drops()
    {
        var order = OrderScheduleTests.ScheduledOrder();
        order.RecalculateStatus(1000, Now);

        // Sửa thực tế giảm xuống phải mở lại được đơn hàng (Step 1 §13).
        order.RecalculateStatus(800, Now);

        Assert.Equal(OrderStatus.Incomplete, order.Status);
    }

    [Fact]
    public void RecalculateStatus_never_moves_a_pending_order_out_of_pending()
    {
        var order = Order.Receive("SH-2026-001", 1000, Now);

        // Chỉ Schedule() mới đưa đơn ra khỏi Pending; đơn không bao giờ quay lại Pending (CR-001 §4.1).
        order.RecalculateStatus(0, Now);
        order.RecalculateStatus(1000, Now);

        Assert.Equal(OrderStatus.Pending, order.Status);
    }

    [Fact]
    public void The_full_lifecycle_is_pending_then_incomplete_then_completed_then_incomplete()
    {
        var order = Order.Receive("SH-2026-001", 1000, Now);
        Assert.Equal(OrderStatus.Pending, order.Status);

        order = OrderScheduleTests.ScheduledOrder();
        Assert.Equal(OrderStatus.Incomplete, order.Status);

        order.RecalculateStatus(1000, Now);
        Assert.Equal(OrderStatus.Completed, order.Status);

        order.RecalculateStatus(950, Now);
        Assert.Equal(OrderStatus.Incomplete, order.Status);
    }
}

public class OrderOverdueTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 7, 10, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Start = new(2026, 9, 10);
    private static readonly DateOnly Due = new(2026, 9, 12);

    [Fact]
    public void A_pending_order_is_never_overdue_because_it_has_no_due_date_yet()
    {
        var order = Order.Receive("SH-2026-001", 1000, Now);

        Assert.False(order.IsOverdueOn(new DateOnly(2030, 1, 1)));
        Assert.False(order.IsPastDueDateOn(new DateOnly(2030, 1, 1)));
    }

    [Fact]
    public void IsOverdueOn_is_false_up_to_and_including_the_due_date()
    {
        var order = OrderScheduleTests.ScheduledOrder();

        Assert.False(order.IsOverdueOn(Start));
        Assert.False(order.IsOverdueOn(Due));
    }

    [Fact]
    public void IsOverdueOn_is_true_the_day_after_the_due_date()
    {
        Assert.True(OrderScheduleTests.ScheduledOrder().IsOverdueOn(Due.AddDays(1)));
    }

    [Fact]
    public void IsOverdueOn_is_false_for_a_completed_order_however_late_it_is()
    {
        var order = OrderScheduleTests.ScheduledOrder();
        order.RecalculateStatus(1000, Now);

        Assert.False(order.IsOverdueOn(Due.AddDays(365)));
    }

    [Fact]
    public void IsOverdueOn_turns_true_again_when_a_late_completed_order_reopens()
    {
        var order = OrderScheduleTests.ScheduledOrder();
        order.RecalculateStatus(1000, Now);
        order.RecalculateStatus(800, Now);

        Assert.True(order.IsOverdueOn(Due.AddDays(1)));
    }

    [Fact]
    public void IsPastDueDateOn_ignores_the_status_unlike_IsOverdueOn()
    {
        var order = OrderScheduleTests.ScheduledOrder();
        order.RecalculateStatus(1000, Now);

        // Đã giao đủ nên không trễ — nhưng kỳ sản xuất đã kết thúc nên dữ liệu bị đóng băng.
        Assert.False(order.IsOverdueOn(Due.AddDays(1)));
        Assert.True(order.IsPastDueDateOn(Due.AddDays(1)));
    }
}
