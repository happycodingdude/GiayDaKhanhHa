using ProductionManagement.Domain;
using ProductionManagement.Domain.Entities;
using Xunit;

namespace ProductionManagement.UnitTests;

public class OrderTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 13, 10, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Start = new(2026, 8, 13);
    private static readonly DateOnly Due = new(2026, 8, 17);

    private static Order CreateOrder(int quantity = 100, params (DateOnly, int)[] plans)
    {
        var initialPlans = plans.Length > 0
            ? plans.ToList()
            : [(Start, 20), (Start.AddDays(1), 20), (Start.AddDays(2), 20), (Start.AddDays(3), 20), (Start.AddDays(4), 20)];

        return Order.Create("ORD-001", quantity, Start, Due, initialPlans, Now);
    }

    [Fact]
    public void Create_builds_the_initial_plans_with_matching_totals()
    {
        var order = CreateOrder();

        Assert.Equal(OrderStatus.Incomplete, order.Status);
        Assert.Equal(5, order.ProductionPlans.Count);
        Assert.Equal(100, order.ProductionPlans.Sum(p => p.InitialPlannedQuantity));
        // The current plan starts equal to the initial plan.
        Assert.Equal(100, order.ProductionPlans.Sum(p => p.PlannedQuantity));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Create_rejects_a_non_positive_quantity(int quantity)
    {
        var exception = Assert.Throws<ValidationException>(() =>
            Order.Create("ORD-001", quantity, Start, Due, [(Start, 0)], Now));

        Assert.Contains(exception.Failures, f => f.Code == "MUST_BE_GREATER_THAN_ZERO");
    }

    [Fact]
    public void Create_rejects_a_due_date_before_the_start_date()
    {
        var exception = Assert.Throws<ValidationException>(() =>
            Order.Create("ORD-001", 10, Due, Start, [(Start, 10)], Now));

        Assert.Contains(exception.Failures, f => f.Code == "DUE_DATE_BEFORE_START_DATE");
    }

    [Theory]
    [InlineData(90)]
    [InlineData(110)]
    public void Create_rejects_an_initial_plan_total_that_differs_from_the_order_quantity(int planned)
    {
        var exception = Assert.Throws<BusinessRuleException>(() =>
            Order.Create("ORD-001", 100, Start, Due, [(Start, planned)], Now));

        Assert.Equal(ErrorCodes.InitialPlanTotalMismatch, exception.Code);
    }

    [Fact]
    public void Create_allows_a_production_day_planned_for_zero()
    {
        var order = CreateOrder(100, (Start, 100), (Start.AddDays(1), 0));

        Assert.Equal(2, order.ProductionPlans.Count);
        Assert.Contains(order.ProductionPlans, p => p.PlannedQuantity == 0);
    }

    [Fact]
    public void Create_rejects_a_duplicate_production_date()
    {
        var exception = Assert.Throws<ValidationException>(() =>
            Order.Create("ORD-001", 100, Start, Due, [(Start, 50), (Start, 50)], Now));

        Assert.Contains(exception.Failures, f => f.Code == "DUPLICATE_PRODUCTION_DATE");
    }

    [Fact]
    public void Create_rejects_a_production_date_outside_the_production_period()
    {
        var exception = Assert.Throws<ValidationException>(() =>
            Order.Create("ORD-001", 100, Start, Due, [(Due.AddDays(1), 100)], Now));

        Assert.Contains(exception.Failures, f => f.Code == "OUT_OF_PRODUCTION_PERIOD");
    }

    [Fact]
    public void RecalculateStatus_completes_the_order_when_the_total_actual_reaches_the_quantity()
    {
        var order = CreateOrder();

        order.RecalculateStatus(100, Now);

        Assert.Equal(OrderStatus.Completed, order.Status);
    }

    [Fact]
    public void RecalculateStatus_keeps_the_order_incomplete_below_the_quantity()
    {
        var order = CreateOrder();

        order.RecalculateStatus(99, Now);

        Assert.Equal(OrderStatus.Incomplete, order.Status);
    }

    [Fact]
    public void RecalculateStatus_returns_a_completed_order_to_incomplete_when_the_total_drops()
    {
        var order = CreateOrder();
        order.RecalculateStatus(100, Now);

        // Editing an actual downwards must be able to reopen the order (Step 1 §13).
        order.RecalculateStatus(80, Now);

        Assert.Equal(OrderStatus.Incomplete, order.Status);
    }
}
