namespace ProductionManagement.Domain.Services;

/// <summary>
/// Quy tắc chia đều duy nhất của hệ thống (CR-001 §6.6b, BR-N17). Dùng chung cho cả ba chỗ:
/// tầng 1 (đơn → dây chuyền), tầng 2 (dây chuyền → ngày) và phân bổ tự động khi bù sản lượng thiếu.
///
/// Chia không hết thì phần dư <c>r = tổng mod n</c> được cộng 1 vào <c>r</c> phần tử ĐẦU TIÊN.
/// Thứ tự phần tử do bên gọi quyết định: dây chuyền theo <c>sort_order</c> rồi <c>code</c>, ngày
/// theo thứ tự tăng dần.
///
/// Frontend cũng chia đều để điền sẵn ô nhập. Hai bên phải cho ra cùng một dãy số, nếu không quản lý
/// sẽ thấy tổng lệch đúng một đơn vị mà không hiểu vì sao.
/// </summary>
public static class EvenDistribution
{
    /// <summary>1.000 / 3 → [334, 333, 333]; 334 / 5 → [67, 67, 67, 67, 66]; 10 / 4 → [3, 3, 2, 2].</summary>
    public static int[] Split(int total, int count)
    {
        if (count <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(count), count, "Count must be greater than zero.");
        }

        if (total < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(total), total, "Total cannot be negative.");
        }

        var baseShare = total / count;
        var remainder = total % count;

        var shares = new int[count];
        for (var i = 0; i < count; i++)
        {
            shares[i] = baseShare + (i < remainder ? 1 : 0);
        }

        return shares;
    }
}
