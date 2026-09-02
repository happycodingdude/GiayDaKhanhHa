using ProductionManagement.Domain;
using ProductionManagement.Domain.Services;
using Xunit;

namespace ProductionManagement.UnitTests;

public class EvenDistributionAllocationStrategyTests
{
    private readonly EvenDistributionAllocationStrategy _strategy = new();

    private static IReadOnlyList<AllocationCandidate> Days(int count, int startDay = 14)
        => Enumerable.Range(0, count)
            .Select(i => new AllocationCandidate(TestIds.Of(i + 1), new DateOnly(2026, 8, startDay + i), 20))
            .ToList();

    [Fact]
    public void A_shortage_that_divides_evenly_is_split_equally()
    {
        var result = _strategy.Allocate(20, Days(4));

        Assert.Equal([5, 5, 5, 5], result.Select(r => r.AddOnQuantity));
    }

    [Fact]
    public void A_remainder_is_distributed_starting_from_the_nearest_day()
    {
        // Chia 10 cho 3 ngày ra 4 / 3 / 3 (Option 2 spec §4.5).
        var result = _strategy.Allocate(10, Days(3));

        Assert.Equal([4, 3, 3], result.Select(r => r.AddOnQuantity));
    }

    [Fact]
    public void A_remainder_of_two_adds_one_to_each_of_the_two_nearest_days()
    {
        var result = _strategy.Allocate(11, Days(3));

        Assert.Equal([4, 4, 3], result.Select(r => r.AddOnQuantity));
    }

    [Fact]
    public void The_documented_example_of_twenty_three_over_four_days_matches()
    {
        var result = _strategy.Allocate(23, Days(4));

        Assert.Equal([6, 6, 6, 5], result.Select(r => r.AddOnQuantity));
    }

    [Fact]
    public void The_total_allocated_always_equals_the_shortage()
    {
        foreach (var shortage in Enumerable.Range(1, 60))
        {
            foreach (var dayCount in Enumerable.Range(1, 7))
            {
                var result = _strategy.Allocate(shortage, Days(dayCount));
                Assert.Equal(shortage, result.Sum(r => r.AddOnQuantity));
            }
        }
    }

    [Fact]
    public void Days_that_would_receive_nothing_are_left_out_of_the_proposal()
    {
        // add_on_quantity > 0 là ràng buộc CHECK của database, nên ngày chia được 0 sẽ bị loại.
        var result = _strategy.Allocate(2, Days(5));

        Assert.Equal(2, result.Count);
        Assert.All(result, item => Assert.True(item.AddOnQuantity > 0));
    }

    [Fact]
    public void Allocation_is_ordered_by_production_date()
    {
        var unordered = Days(3).Reverse().ToList();

        var result = _strategy.Allocate(6, unordered);

        Assert.Equal(result.OrderBy(r => r.ProductionDate).Select(r => r.ProductionDate), result.Select(r => r.ProductionDate));
    }

    [Fact]
    public void Allocating_without_any_remaining_day_is_rejected()
    {
        var exception = Assert.Throws<BusinessRuleException>(() => _strategy.Allocate(10, []));

        Assert.Equal(ErrorCodes.NoEligibleTargetDay, exception.Code);
    }

    [Fact]
    public void Allocating_without_a_shortage_is_rejected()
    {
        var exception = Assert.Throws<BusinessRuleException>(() => _strategy.Allocate(0, Days(3)));

        Assert.Equal(ErrorCodes.NoShortage, exception.Code);
    }
}
