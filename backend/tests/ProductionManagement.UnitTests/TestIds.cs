namespace ProductionManagement.UnitTests;

/// <summary>
/// Id thay thế cho dễ đọc. Trong domain id là Guid, nhưng các test này vẫn suy nghĩ theo kiểu
/// "kế hoạch 2" và "người dùng 1", nên một số seed sẽ ánh xạ sang một Guid cố định và nhìn là biết
/// giả. Dùng khuôn cố định thay cho <see cref="Guid.NewGuid"/> giúp lỗi tái hiện được.
/// </summary>
internal static class TestIds
{
    public static Guid Of(int seed) => new($"00000000-0000-0000-0000-{seed:D12}");
}
