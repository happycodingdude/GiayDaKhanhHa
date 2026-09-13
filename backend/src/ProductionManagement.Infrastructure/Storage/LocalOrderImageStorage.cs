using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ProductionManagement.Application.Abstractions;

namespace ProductionManagement.Infrastructure.Storage;

/// <summary>
/// Ảnh mẫu lưu trên local disk. Thư mục gốc lấy từ cấu hình <c>Storage:OrderImagesPath</c> và được
/// tạo lúc khởi tạo nếu chưa có (CR-001 §5.8).
///
/// Tên file do server sinh từ id đơn hàng + một hậu tố ngẫu nhiên, KHÔNG dùng tên file của client:
/// tên do client đặt là dữ liệu không tin được và là đường vào kinh điển của path traversal.
/// Hậu tố ngẫu nhiên còn khiến ảnh cũ không thể bị đoán ra sau khi bị thay.
/// </summary>
public sealed class LocalOrderImageStorage : IOrderImageStorage
{
    private readonly string _rootPath;
    private readonly ILogger<LocalOrderImageStorage> _logger;

    public LocalOrderImageStorage(IConfiguration configuration, ILogger<LocalOrderImageStorage> logger)
    {
        _logger = logger;

        var configured = configuration["Storage:OrderImagesPath"];
        _rootPath = Path.GetFullPath(
            string.IsNullOrWhiteSpace(configured)
                ? Path.Combine(AppContext.BaseDirectory, "App_Data", "order-images")
                : configured);

        Directory.CreateDirectory(_rootPath);
    }

    public async Task<string> SaveAsync(
        Guid orderId, string extension, Stream content, CancellationToken ct = default)
    {
        // Rải theo thư mục con để một thư mục không phình ra hàng chục nghìn file.
        var folder = orderId.ToString("N")[..2];
        Directory.CreateDirectory(Path.Combine(_rootPath, folder));

        var relativePath = Path.Combine(folder, $"{orderId:N}-{Guid.NewGuid():N}{extension}");

        await using var file = File.Create(Path.Combine(_rootPath, relativePath));
        await content.CopyToAsync(file, ct);

        // Lưu với dấu '/' để đường dẫn trong database không phụ thuộc hệ điều hành.
        return relativePath.Replace(Path.DirectorySeparatorChar, '/');
    }

    public Stream? Open(string relativePath)
    {
        var fullPath = Resolve(relativePath);
        if (fullPath is null || !File.Exists(fullPath))
        {
            return null;
        }

        return File.OpenRead(fullPath);
    }

    public void Delete(string relativePath)
    {
        var fullPath = Resolve(relativePath);
        if (fullPath is null)
        {
            return;
        }

        try
        {
            File.Delete(fullPath);
        }
        catch (IOException ex)
        {
            // Ảnh cũ còn nằm lại trên disk không làm hỏng dữ liệu — database đã trỏ sang ảnh mới.
            // Ghi log để job dọn file mồ côi xử lý sau (CR-001 §10).
            _logger.LogWarning(ex, "Could not delete order image file {RelativePath}.", relativePath);
        }
    }

    /// <summary>
    /// Chặn path traversal: đường dẫn lấy từ database vẫn phải nằm trong thư mục gốc sau khi chuẩn
    /// hoá. Rẻ, và là lớp phòng vệ cuối nếu một đường dẫn xấu lọt được vào database.
    /// </summary>
    private string? Resolve(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            return null;
        }

        var fullPath = Path.GetFullPath(Path.Combine(_rootPath, relativePath));
        var root = _rootPath.EndsWith(Path.DirectorySeparatorChar)
            ? _rootPath
            : _rootPath + Path.DirectorySeparatorChar;

        return fullPath.StartsWith(root, StringComparison.Ordinal) ? fullPath : null;
    }
}
