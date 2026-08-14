namespace ProductionManagement.Application.Abstractions;

/// <summary>Băm mật khẩu. Database chỉ lưu duy nhất <c>password_hash</c> (Step 3 §2.1).</summary>
public interface IPasswordHasher
{
    string Hash(string password);

    bool Verify(string password, string hash);
}

/// <summary>
/// Người dùng đã xác thực, suy ra từ ngữ cảnh xác thực. Client không bao giờ gửi lên id người dùng
/// phục vụ audit như <c>createdBy</c> (Step 4 §3).
/// </summary>
public interface ICurrentUser
{
    bool IsAuthenticated { get; }

    Guid UserId { get; }
}

/// <summary>Trừu tượng hóa đồng hồ để các luật nghiệp vụ phụ thuộc thời gian vẫn test được.</summary>
public interface IClock
{
    /// <summary>Dấu thời gian audit luôn lưu theo UTC (Step 3 §8).</summary>
    DateTimeOffset UtcNow { get; }

    /// <summary>
    /// Ngày nghiệp vụ hiện tại. Ngày nghiệp vụ là giá trị chỉ có ngày, không gắn múi giờ, nên được
    /// tính theo múi giờ nghiệp vụ đã cấu hình chứ không theo UTC.
    /// </summary>
    DateOnly Today { get; }
}
