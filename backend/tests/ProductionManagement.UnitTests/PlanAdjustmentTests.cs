using ProductionManagement.Domain;
using ProductionManagement.Domain.Entities;
using Xunit;

namespace ProductionManagement.UnitTests;

public class PlanAdjustmentTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 13, 10, 0, 0, TimeSpan.Zero);

    private static PlanAdjustment Apply(int shortage, params (int PlanId, int AddOn)[] targets)
        => PlanAdjustment.Apply(
            TestIds.Of(1),
            shortage,
            AdjustmentType.Manual,
            targets.Select(t => (TestIds.Of(t.PlanId), t.AddOn)).ToList(),
            userId: TestIds.Of(1),
            Now);

    [Fact]
    public void An_applied_adjustment_records_who_applied_it_and_when()
    {
        var adjustment = Apply(20, (2, 20));

        Assert.Equal(AdjustmentStatus.Applied, adjustment.Status);
        Assert.Equal(TestIds.Of(1), adjustment.CreatedBy);
        Assert.Equal(TestIds.Of(1), adjustment.AppliedBy);
        Assert.Equal(Now, adjustment.AppliedAt);
        Assert.Null(adjustment.ReversedAt);
    }

    [Fact]
    public void The_item_total_must_equal_the_shortage()
    {
        var exception = Assert.Throws<BusinessRuleException>(() => Apply(20, (2, 10), (3, 5)));

        Assert.Equal(ErrorCodes.AdjustmentTotalMismatch, exception.Code);
    }

    [Fact]
    public void Multiple_targets_are_allowed_when_they_sum_to_the_shortage()
    {
        var adjustment = Apply(20, (2, 10), (3, 10));

        Assert.Equal(2, adjustment.Items.Count);
        Assert.Equal(20, adjustment.Items.Sum(i => i.AddOnQuantity));
    }

    [Fact]
    public void The_same_target_cannot_appear_twice_in_one_adjustment()
    {
        var exception = Assert.Throws<BusinessRuleException>(() => Apply(20, (2, 10), (2, 10)));

        Assert.Equal(ErrorCodes.DuplicateAdjustmentTarget, exception.Code);
    }

    [Fact]
    public void An_add_on_of_zero_is_rejected()
    {
        var exception = Assert.Throws<BusinessRuleException>(() => Apply(20, (2, 20), (3, 0)));

        Assert.Equal(ErrorCodes.InvalidAdjustmentTarget, exception.Code);
    }

    [Fact]
    public void An_adjustment_requires_a_shortage_greater_than_zero()
    {
        var exception = Assert.Throws<BusinessRuleException>(() => Apply(0, (2, 0)));

        Assert.Equal(ErrorCodes.NoShortage, exception.Code);
    }

    [Fact]
    public void Applied_becomes_reversed_and_records_the_reversing_user()
    {
        var adjustment = Apply(20, (2, 20));

        adjustment.Reverse(userId: TestIds.Of(2), Now.AddHours(1));

        Assert.Equal(AdjustmentStatus.Reversed, adjustment.Status);
        Assert.Equal(TestIds.Of(2), adjustment.ReversedBy);
        Assert.Equal(Now.AddHours(1), adjustment.ReversedAt);
        // Lịch sử không bao giờ bị viết lại: thông tin của lượt apply gốc không bị đụng tới.
        Assert.Equal(Now, adjustment.AppliedAt);
        Assert.Equal(20, adjustment.Items.Sum(i => i.AddOnQuantity));
    }

    [Fact]
    public void An_adjustment_cannot_be_reversed_twice()
    {
        var adjustment = Apply(20, (2, 20));
        adjustment.Reverse(userId: TestIds.Of(1), Now);

        var exception = Assert.Throws<ConflictException>(() => adjustment.Reverse(userId: TestIds.Of(1), Now));

        Assert.Equal(ErrorCodes.AdjustmentNotApplied, exception.Code);
    }
}

public class ProductionPlanAddOnTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 13, 10, 0, 0, TimeSpan.Zero);

    private static ProductionPlan PlanOf(int quantity)
    {
        var order = Order.Create(
            "ORD-001", quantity, new DateOnly(2026, 8, 13), new DateOnly(2026, 8, 13),
            [(new DateOnly(2026, 8, 13), quantity)], Now);

        return order.ProductionPlans.Single();
    }

    [Fact]
    public void An_add_on_increases_the_current_plan_and_leaves_the_initial_plan_untouched()
    {
        var plan = PlanOf(120);

        plan.AddOn(20, Now);

        Assert.Equal(140, plan.PlannedQuantity);
        // InitialPlannedQuantity là bất biến (Step 1 §4).
        Assert.Equal(120, plan.InitialPlannedQuantity);
    }

    [Fact]
    public void Reversing_an_add_on_restores_the_previous_plan()
    {
        var plan = PlanOf(120);
        plan.AddOn(20, Now);

        plan.RemoveAddOn(20, Now);

        Assert.Equal(120, plan.PlannedQuantity);
        Assert.Equal(120, plan.InitialPlannedQuantity);
    }

    [Fact]
    public void Removing_more_than_the_current_plan_is_rejected()
    {
        var plan = PlanOf(10);

        Assert.Throws<ConflictException>(() => plan.RemoveAddOn(20, Now));
    }
}
