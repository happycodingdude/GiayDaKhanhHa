using ProductionManagement.Application.Common;
using ProductionManagement.Application.Contracts;
using ProductionManagement.Domain;
using Xunit;

namespace ProductionManagement.UnitTests;

public class OrderDerivedCalculatorTests
{
    private static readonly DateOnly Day1 = new(2026, 8, 11);
    private static readonly DateOnly Day2 = new(2026, 8, 12);
    private static readonly DateOnly Day3 = new(2026, 8, 13);
    private static readonly DateOnly Due = new(2026, 8, 15);

    /// <summary>Hầu hết các test ở đây chỉ cần một dây chuyền; phần theo dây chuyền có test riêng.</summary>
    private static readonly Guid LineA = TestIds.Of(1);
    private static readonly Guid LineB = TestIds.Of(2);

    private static PlanCell Plan(DateOnly date, int planned, int initial, Guid? line = null)
        => new(date, line ?? LineA, planned, initial);

    /// <summary>
    /// Ô sản xuất mặc định là đã Xuất hàng — đó là trạng thái của mọi ngày đã qua trong các test
    /// này. Ô còn mở được viết tường minh bằng <see cref="Open"/>.
    /// </summary>
    private static ActualCell Closed(DateOnly date, int actual, Guid? line = null)
        => new(date, line ?? LineA, actual, true);

    private static ActualCell Open(DateOnly date, int actual, Guid? line = null)
        => new(date, line ?? LineA, actual, false);

    private static OrderDerivedValues Compute(
        DateOnly today,
        PlanCell[] plans,
        ActualCell[] days,
        int quantity = 1000,
        OrderStatus status = OrderStatus.Incomplete)
        => OrderDerivedCalculator.Compute(quantity, status, Due, plans, days, today);

    [Fact]
    public void Totals_and_progress_come_from_the_source_data()
    {
        var result = Compute(
            Day3,
            [Plan(Day1, 100, 100), Plan(Day2, 120, 120), Plan(Day3, 200, 200)],
            [Closed(Day1, 80), Closed(Day2, 120)]);

        Assert.Equal(200, result.TotalActual);
        Assert.Equal(420, result.TotalPlan);
        Assert.Equal(800, result.Remaining);
        Assert.Equal(20m, result.ProgressPercentage);
    }

    [Fact]
    public void The_add_on_part_of_the_plan_is_visible_as_current_plan_above_initial_plan()
    {
        var result = Compute(Day3, [Plan(Day1, 100, 100), Plan(Day2, 140, 120)], []);

        Assert.Equal(240, result.TotalPlan);
        Assert.Equal(220, result.TotalInitialPlan);
    }

    [Fact]
    public void An_order_behind_on_finished_days_reports_the_shortfall()
    {
        var result = Compute(
            Day3,
            [Plan(Day1, 100, 100), Plan(Day2, 120, 120), Plan(Day3, 200, 200)],
            [Closed(Day1, 80), Closed(Day2, 100)]);

        // Ngày 1-2 đã tới hạn: kế hoạch 220 so với thực tế 180.
        Assert.Equal(ScheduleStatus.Behind, result.ScheduleStatus);
        Assert.Equal(40, result.BehindQuantity);
    }

    [Fact]
    public void Today_is_not_counted_as_late_while_it_is_still_being_produced()
    {
        // Sản lượng của ngày còn mở là số tạm tính và còn tăng tiếp, nên đơn hàng không bị coi là
        // trễ vì ngày hôm nay chưa chốt sổ (CR-01 §4.5).
        var result = Compute(Day3, [Plan(Day1, 100, 100), Plan(Day3, 200, 200)], [Closed(Day1, 100), Open(Day3, 150)]);

        Assert.Equal(ScheduleStatus.OnSchedule, result.ScheduleStatus);
        Assert.Equal(0, result.BehindQuantity);
        // Tổng vẫn tính cả số tạm tính của ngày đang mở.
        Assert.Equal(250, result.TotalActual);
    }

    [Fact]
    public void Today_counts_once_it_has_been_closed()
    {
        var result = Compute(Day3, [Plan(Day1, 100, 100), Plan(Day3, 200, 200)], [Closed(Day1, 100), Closed(Day3, 150)]);

        Assert.Equal(ScheduleStatus.Behind, result.ScheduleStatus);
        Assert.Equal(50, result.BehindQuantity);
    }

    [Fact]
    public void Meeting_the_plan_exactly_is_on_schedule()
    {
        // Sau CR-01, tổng ghi nhận của một ngày không được vượt kế hoạch ngày (OV-3), nên "vượt kế
        // hoạch" không còn là trạng thái đạt tới được.
        var result = Compute(Day3, [Plan(Day1, 100, 100)], [Closed(Day1, 100)]);

        Assert.Equal(ScheduleStatus.OnSchedule, result.ScheduleStatus);
        Assert.Equal(0, result.BehindQuantity);
    }

    [Fact]
    public void A_completed_order_reports_the_completed_schedule_status()
    {
        var result = Compute(
            Day3, [Plan(Day1, 100, 100)], [Closed(Day1, 100)], quantity: 100, status: OrderStatus.Completed);

        Assert.Equal(ScheduleStatus.Completed, result.ScheduleStatus);
        Assert.Equal(100m, result.ProgressPercentage);
        Assert.Equal(0, result.Remaining);
    }

    [Fact]
    public void An_incomplete_order_past_its_due_date_is_overdue()
    {
        var result = Compute(Due.AddDays(1), [Plan(Day1, 100, 100)], []);

        Assert.True(result.IsOverdue);
        Assert.Equal(0, result.DaysRemaining);
    }

    [Fact]
    public void Days_remaining_counts_forward_to_the_due_date()
    {
        var result = Compute(Day3, [Plan(Day1, 100, 100)], []);

        Assert.Equal(2, result.DaysRemaining);
        Assert.False(result.IsOverdue);
    }

    [Fact]
    public void Only_the_lines_that_closed_today_count_towards_being_behind()
    {
        // Cùng một ngày, dây chuyền A đã Xuất hàng còn B thì chưa. Chỉ phần của A được tính vào kế
        // hoạch tới hạn — nếu tính cả B, đơn sẽ hiện "chậm" chỉ vì B chưa chốt sổ (CR-001 QĐ-3).
        var result = Compute(
            Day3,
            [Plan(Day3, 200, 200, LineA), Plan(Day3, 300, 300, LineB)],
            [Closed(Day3, 150, LineA), Open(Day3, 100, LineB)]);

        Assert.Equal(ScheduleStatus.Behind, result.ScheduleStatus);
        // 200 (chỉ dây chuyền A) - 150 = 50. Phần 300 của B chưa tới hạn.
        Assert.Equal(50, result.BehindQuantity);
        // Tổng thực tế vẫn cộng cả số tạm tính của B.
        Assert.Equal(250, result.TotalActual);
    }

    [Fact]
    public void Cells_on_the_same_date_but_different_lines_are_counted_separately()
    {
        var result = Compute(
            Day3,
            [Plan(Day1, 100, 100, LineA), Plan(Day1, 150, 150, LineB)],
            [Closed(Day1, 90, LineA), Closed(Day1, 140, LineB)]);

        Assert.Equal(250, result.TotalPlan);
        Assert.Equal(230, result.TotalActual);
        Assert.Equal(20, result.BehindQuantity);
    }

    [Fact]
    public void An_order_without_a_due_date_has_no_days_remaining_and_is_never_overdue()
    {
        // Đơn Pending chưa cam kết ngày nào nên không thể lỡ hẹn (CR-001 BR-N04).
        var result = OrderDerivedCalculator.Compute(
            1000, OrderStatus.Pending, null, [], [], new DateOnly(2030, 1, 1));

        Assert.False(result.IsOverdue);
        Assert.False(result.IsPastDueDate);
        Assert.Equal(0, result.DaysRemaining);
        Assert.Equal(1000, result.Remaining);
    }
}
