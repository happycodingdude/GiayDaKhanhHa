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

    private static OrderDerivedValues Compute(
        DateOnly today,
        (DateOnly, int, int)[] plans,
        (DateOnly, int)[] records,
        int quantity = 1000,
        OrderStatus status = OrderStatus.Incomplete)
        => OrderDerivedCalculator.Compute(quantity, status, Due, plans, records, today);

    [Fact]
    public void Totals_and_progress_come_from_the_source_data()
    {
        var result = Compute(
            Day3,
            [(Day1, 100, 100), (Day2, 120, 120), (Day3, 200, 200)],
            [(Day1, 80), (Day2, 130)]);

        Assert.Equal(210, result.TotalActual);
        Assert.Equal(420, result.TotalPlan);
        Assert.Equal(790, result.Remaining);
        Assert.Equal(21m, result.ProgressPercentage);
    }

    [Fact]
    public void The_add_on_part_of_the_plan_is_visible_as_current_plan_above_initial_plan()
    {
        var result = Compute(Day3, [(Day1, 100, 100), (Day2, 140, 120)], []);

        Assert.Equal(240, result.TotalPlan);
        Assert.Equal(220, result.TotalInitialPlan);
    }

    [Fact]
    public void An_order_behind_on_finished_days_reports_the_shortfall()
    {
        var result = Compute(
            Day3,
            [(Day1, 100, 100), (Day2, 120, 120), (Day3, 200, 200)],
            [(Day1, 80), (Day2, 100)]);

        // Ngày 1-2 đã tới hạn: kế hoạch 220 so với thực tế 180.
        Assert.Equal(ScheduleStatus.Behind, result.ScheduleStatus);
        Assert.Equal(40, result.BehindQuantity);
    }

    [Fact]
    public void Today_is_not_counted_as_late_before_its_actual_has_been_entered()
    {
        var result = Compute(Day3, [(Day1, 100, 100), (Day3, 200, 200)], [(Day1, 100)]);

        Assert.Equal(ScheduleStatus.OnSchedule, result.ScheduleStatus);
        Assert.Equal(0, result.BehindQuantity);
    }

    [Fact]
    public void Today_counts_once_its_actual_has_been_entered()
    {
        var result = Compute(Day3, [(Day1, 100, 100), (Day3, 200, 200)], [(Day1, 100), (Day3, 150)]);

        Assert.Equal(ScheduleStatus.Behind, result.ScheduleStatus);
        Assert.Equal(50, result.BehindQuantity);
    }

    [Fact]
    public void Producing_ahead_of_plan_is_on_schedule_rather_than_a_separate_status()
    {
        var result = Compute(Day3, [(Day1, 100, 100)], [(Day1, 130)]);

        Assert.Equal(ScheduleStatus.OnSchedule, result.ScheduleStatus);
        Assert.Equal(0, result.BehindQuantity);
    }

    [Fact]
    public void A_completed_order_reports_the_completed_schedule_status()
    {
        var result = Compute(
            Day3, [(Day1, 100, 100)], [(Day1, 100)], quantity: 100, status: OrderStatus.Completed);

        Assert.Equal(ScheduleStatus.Completed, result.ScheduleStatus);
        Assert.Equal(100m, result.ProgressPercentage);
        Assert.Equal(0, result.Remaining);
    }

    [Fact]
    public void An_incomplete_order_past_its_due_date_is_overdue()
    {
        var result = Compute(Due.AddDays(1), [(Day1, 100, 100)], []);

        Assert.True(result.IsOverdue);
        Assert.Equal(0, result.DaysRemaining);
    }

    [Fact]
    public void Days_remaining_counts_forward_to_the_due_date()
    {
        var result = Compute(Day3, [(Day1, 100, 100)], []);

        Assert.Equal(2, result.DaysRemaining);
        Assert.False(result.IsOverdue);
    }
}
