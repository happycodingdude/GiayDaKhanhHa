using ProductionManagement.Domain.Services;
using Xunit;

namespace ProductionManagement.UnitTests;

/// <summary>
/// Quy tắc chia đều dùng chung cho cả ba chỗ: tầng 1, tầng 2 và bù tự động (CR-001 §6.6b, BR-N17).
/// Các trường hợp dưới đây lấy nguyên từ danh sách test bắt buộc của CR (§9).
/// </summary>
public class EvenDistributionTests
{
    [Theory]
    [InlineData(1000, 3, new[] { 334, 333, 333 })]
    [InlineData(334, 5, new[] { 67, 67, 67, 67, 66 })]
    [InlineData(10, 4, new[] { 3, 3, 2, 2 })]
    [InlineData(5, 5, new[] { 1, 1, 1, 1, 1 })]
    public void The_remainder_goes_to_the_first_elements(int total, int count, int[] expected)
    {
        Assert.Equal(expected, EvenDistribution.Split(total, count));
    }

    [Fact]
    public void A_total_that_divides_evenly_has_no_remainder_to_place()
    {
        Assert.Equal([250, 250, 250, 250], EvenDistribution.Split(1000, 4));
    }

    [Fact]
    public void Fewer_units_than_elements_leaves_the_tail_at_zero()
    {
        // 2 đôi cho 5 ngày: ba ngày cuối nhận 0. Bên gọi quyết định có tạo dòng cho ô 0 hay không.
        Assert.Equal([1, 1, 0, 0, 0], EvenDistribution.Split(2, 5));
    }

    [Fact]
    public void Splitting_always_preserves_the_total()
    {
        foreach (var total in new[] { 0, 1, 7, 999, 1000, 12345 })
        {
            foreach (var count in new[] { 1, 2, 3, 7, 31 })
            {
                Assert.Equal(total, EvenDistribution.Split(total, count).Sum());
            }
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void A_non_positive_count_is_a_programming_error(int count)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => EvenDistribution.Split(100, count));
    }
}
