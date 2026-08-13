using ProductionManagement.Domain;
using ProductionManagement.Domain.Entities;
using ProductionManagement.Domain.Services;
using Xunit;

namespace ProductionManagement.UnitTests;

public class ProductionRecordTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 13, 10, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Date = new(2026, 8, 13);

    [Fact]
    public void An_explicit_zero_actual_is_valid()
    {
        var record = ProductionRecord.Create(1, Date, 0, userId: 1, Now);

        Assert.Equal(0, record.ActualQuantity);
    }

    [Fact]
    public void A_negative_actual_is_rejected()
    {
        var exception = Assert.Throws<ValidationException>(() =>
            ProductionRecord.Create(1, Date, -1, userId: 1, Now));

        Assert.Contains(exception.Failures, f => f.Field == "actualQuantity");
    }

    [Fact]
    public void Editing_replaces_the_value_instead_of_accumulating_it()
    {
        var record = ProductionRecord.Create(1, Date, 80, userId: 1, Now);

        record.UpdateActual(75, userId: 2, Now.AddHours(1));

        Assert.Equal(75, record.ActualQuantity);
        Assert.Equal(2, record.UpdatedBy);
        // The creator is preserved; only the editor changes.
        Assert.Equal(1, record.CreatedBy);
    }
}

public class ShortageCalculationTests
{
    [Theory]
    [InlineData(100, 80, 20)]
    [InlineData(100, 100, 0)]
    [InlineData(100, 120, 0)]
    [InlineData(100, 0, 100)]
    public void Shortage_is_the_plan_minus_the_actual_floored_at_zero(int planned, int actual, int expected)
    {
        Assert.Equal(expected, ProductionCalculations.Shortage(planned, actual));
    }

    [Fact]
    public void A_day_with_no_actual_recorded_has_no_shortage()
    {
        // No record means "not entered yet", which is not the same as an actual of 0.
        Assert.Equal(0, ProductionCalculations.Shortage(100, null));
        Assert.Null(ProductionCalculations.Difference(100, null));
    }

    [Theory]
    [InlineData(100, 80, -20)]
    [InlineData(100, 130, 30)]
    [InlineData(100, 100, 0)]
    public void Difference_is_the_actual_minus_the_current_plan(int planned, int actual, int expected)
    {
        Assert.Equal(expected, ProductionCalculations.Difference(planned, actual));
    }

    [Fact]
    public void Remaining_never_goes_negative()
    {
        Assert.Equal(0, ProductionCalculations.Remaining(100, 100));
        Assert.Equal(20, ProductionCalculations.Remaining(100, 80));
    }

    [Fact]
    public void Progress_is_the_total_actual_over_the_order_quantity()
    {
        Assert.Equal(65m, ProductionCalculations.ProgressPercentage(1000, 650));
        Assert.Equal(0m, ProductionCalculations.ProgressPercentage(0, 0));
    }
}
