using ProductionManagement.Domain;
using ProductionManagement.Domain.Entities;
using ProductionManagement.Domain.Services;
using Xunit;

namespace ProductionManagement.UnitTests;

public class ProductionEntryTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 13, 10, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void An_entry_of_zero_or_less_is_rejected(int quantity)
    {
        // Ghi nhận bằng 0 là vô nghĩa: "cả ngày không sản xuất được" thể hiện bằng Xuất hàng với
        // 0 lần ghi nhận (CR-01 §5.2, N-01).
        var exception = Assert.Throws<ValidationException>(() =>
            ProductionEntry.Create(TestIds.Of(1), quantity, null, TestIds.Of(1), Now));

        Assert.Contains(exception.Failures, f => f.Field == "quantity");
    }

    [Fact]
    public void A_fresh_entry_is_not_marked_as_edited()
    {
        var entry = ProductionEntry.Create(TestIds.Of(1), 15, "  Tổ 2  ", TestIds.Of(1), Now);

        Assert.Equal(15, entry.Quantity);
        Assert.Equal("Tổ 2", entry.Note);
        Assert.False(entry.IsEdited);
        Assert.False(entry.IsDeleted);
    }

    [Fact]
    public void Updating_replaces_the_quantity_and_marks_the_entry_as_edited()
    {
        var entry = ProductionEntry.Create(TestIds.Of(1), 25, null, TestIds.Of(1), Now);

        entry.Update(10, null, TestIds.Of(2), Now.AddHours(1));

        Assert.Equal(10, entry.Quantity);
        Assert.True(entry.IsEdited);
        Assert.Equal(TestIds.Of(2), entry.UpdatedBy);
        // Người tạo được giữ nguyên; chỉ người sửa thay đổi.
        Assert.Equal(TestIds.Of(1), entry.CreatedBy);
    }

    [Fact]
    public void Deleting_is_soft_and_keeps_the_quantity_for_the_audit_trail()
    {
        var entry = ProductionEntry.Create(TestIds.Of(1), 25, null, TestIds.Of(1), Now);

        entry.Delete(TestIds.Of(2), Now.AddHours(1));

        Assert.True(entry.IsDeleted);
        Assert.Equal(25, entry.Quantity);
    }

    [Fact]
    public void An_empty_note_is_stored_as_null_rather_than_an_empty_string()
    {
        Assert.Null(ProductionEntry.Create(TestIds.Of(1), 5, "   ", TestIds.Of(1), Now).Note);
    }
}

public class ProductionDayLifecycleTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 13, 10, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Date = new(2026, 8, 13);

    private static ProductionDay OpenDay() => ProductionDay.Open(TestIds.Of(1), Date, TestIds.Of(1), Now);

    [Fact]
    public void A_new_day_is_open_and_has_no_official_quantity_yet()
    {
        var day = OpenDay();

        Assert.False(day.IsClosed);
        Assert.Null(day.ActualQuantity);
        Assert.Null(day.ClosedAt);
        Assert.Null(day.ClosedBy);
    }

    [Fact]
    public void Closing_snapshots_the_quantity_and_records_who_closed_it()
    {
        var day = OpenDay();

        day.Close(160, TestIds.Of(2), Now.AddHours(7));

        Assert.True(day.IsClosed);
        Assert.Equal(160, day.ActualQuantity);
        Assert.Equal(Now.AddHours(7), day.ClosedAt);
        Assert.Equal(TestIds.Of(2), day.ClosedBy);
    }

    [Fact]
    public void Closing_with_zero_is_valid()
    {
        var day = OpenDay();

        day.Close(0, TestIds.Of(1), Now);

        Assert.Equal(0, day.ActualQuantity);
    }

    [Fact]
    public void A_closed_day_can_never_be_closed_again_or_changed()
    {
        // Close là một chiều: không có reopen (CR-01 N-06).
        var day = OpenDay();
        day.Close(100, TestIds.Of(1), Now);

        Assert.Equal(ErrorCodes.DayAlreadyClosed,
            Assert.Throws<ConflictException>(() => day.Close(120, TestIds.Of(1), Now)).Code);
        Assert.Equal(ErrorCodes.DayAlreadyClosed,
            Assert.Throws<ConflictException>(day.EnsureOpen).Code);
    }
}

public class ShortageCalculationTests
{
    [Theory]
    [InlineData(100, 80, 20)]
    [InlineData(100, 100, 0)]
    [InlineData(100, 0, 100)]
    public void Shortage_is_the_plan_minus_the_closed_actual(int planned, int actual, int expected)
    {
        Assert.Equal(expected, ProductionCalculations.Shortage(planned, actual));
    }

    [Fact]
    public void An_open_day_has_no_shortage_and_no_difference()
    {
        // Test quan trọng nhất của CR-01: null, KHÔNG phải 0. Nhầm hai giá trị này sẽ khiến dashboard
        // báo "đạt kế hoạch" cho những ngày còn đang sản xuất (CR-01 §14.8).
        Assert.Null(ProductionCalculations.Shortage(100, null));
        Assert.Null(ProductionCalculations.Difference(100, null));
    }

    [Theory]
    [InlineData(100, 80, -20)]
    [InlineData(100, 100, 0)]
    public void Difference_is_the_closed_actual_minus_the_current_plan(int planned, int actual, int expected)
    {
        Assert.Equal(expected, ProductionCalculations.Difference(planned, actual));
    }

    [Theory]
    // (kế hoạch ngày, đã nhập trong ngày, số lượng đơn, tổng đã nhập) -> trần còn được nhập
    [InlineData(120, 90, 1000, 500, 30)]   // trần ngày chặt hơn
    [InlineData(120, 90, 500, 485, 15)]    // trần đơn hàng chặt hơn
    [InlineData(120, 120, 500, 200, 0)]    // đã kín kế hoạch ngày
    [InlineData(120, 130, 500, 200, 0)]    // dữ liệu cũ vượt kế hoạch: không bao giờ trả số âm
    public void The_remaining_allowance_is_the_tighter_of_the_two_caps(
        int planned, int dayActual, int orderQuantity, int totalActual, int expected)
    {
        Assert.Equal(expected,
            ProductionCalculations.RemainingAllowance(planned, dayActual, orderQuantity, totalActual));
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
