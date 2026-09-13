namespace ProductionManagement.Application.Abstractions;

/// <summary>
/// Nơi lưu ảnh mẫu của đơn hàng. Ảnh nằm trên local disk của server, database chỉ giữ đường dẫn
/// TƯƠNG ĐỐI (CR-001 §2 QĐ-6). Đường dẫn vật lý không bao giờ ra khỏi tầng này.
/// </summary>
public interface IOrderImageStorage
{
    /// <summary>Ghi file và trả về đường dẫn tương đối để lưu xuống database.</summary>
    Task<string> SaveAsync(Guid orderId, string extension, Stream content, CancellationToken ct = default);

    /// <summary>Mở file để stream ra response. Null khi file không còn trên disk.</summary>
    Stream? Open(string relativePath);

    /// <summary>
    /// Xoá file. Không ném khi file đã biến mất: bên gọi luôn xoá SAU khi transaction đã commit, nên
    /// một lần xoá lặp lại không được phép làm hỏng request đã thành công.
    /// </summary>
    void Delete(string relativePath);
}
