using ProductionManagement.Domain;

namespace ProductionManagement.Application.Features.Orders;

/// <summary>Ảnh đã kiểm hợp lệ, sẵn sàng ghi xuống disk.</summary>
public sealed record ValidatedImage(byte[] Content, string Extension, string ContentType, string FileName);

/// <summary>
/// Luật của ảnh mẫu: jpg/jpeg/png/webp, tối đa 5MB (CR-001 BR-N03).
///
/// Định dạng được xác định bằng CHỮ KÝ BYTE của file, không phải bằng phần mở rộng hay
/// <c>Content-Type</c> do client khai báo — cả hai thứ đó client đặt gì cũng được, nên một file PDF
/// đổi tên thành .jpg sẽ lọt qua nếu chỉ tin vào chúng (test checklist §9, Slice 2 #5).
/// </summary>
public static class OrderImageRules
{
    public const int MaxSizeBytes = 5 * 1024 * 1024;

    public static async Task<ValidatedImage> ValidateAsync(Contracts.ImageUpload upload, CancellationToken ct = default)
    {
        // Chặn theo độ dài khai báo trước khi đọc: không nạp 500MB vào bộ nhớ chỉ để rồi từ chối.
        if (upload.SizeBytes > MaxSizeBytes)
        {
            throw new ValidationException(
                "image", ErrorCodes.ImageTooLarge, "The image must be 5MB or smaller.");
        }

        if (upload.SizeBytes <= 0)
        {
            throw new ValidationException("image", "REQUIRED", "The image file is empty.");
        }

        using var buffer = new MemoryStream(capacity: (int)upload.SizeBytes);
        await upload.Content.CopyToAsync(buffer, ct);

        // Content-Length do client khai báo cũng không đáng tin; kiểm lại theo số byte thật sự đọc.
        if (buffer.Length > MaxSizeBytes)
        {
            throw new ValidationException(
                "image", ErrorCodes.ImageTooLarge, "The image must be 5MB or smaller.");
        }

        var content = buffer.ToArray();
        var (extension, contentType) = DetectFormat(content)
            ?? throw new ValidationException(
                "image", ErrorCodes.ImageTypeNotSupported,
                "Only JPG, PNG and WEBP images are supported.");

        return new ValidatedImage(content, extension, contentType, SafeFileName(upload.FileName, extension));
    }

    private static (string Extension, string ContentType)? DetectFormat(ReadOnlySpan<byte> content)
    {
        if (content.Length >= 3 && content[0] == 0xFF && content[1] == 0xD8 && content[2] == 0xFF)
        {
            return (".jpg", "image/jpeg");
        }

        if (content.Length >= 8 &&
            content[0] == 0x89 && content[1] == 0x50 && content[2] == 0x4E && content[3] == 0x47 &&
            content[4] == 0x0D && content[5] == 0x0A && content[6] == 0x1A && content[7] == 0x0A)
        {
            return (".png", "image/png");
        }

        // WEBP là một container RIFF: "RIFF" <4 byte độ dài> "WEBP".
        if (content.Length >= 12 &&
            content[0] == (byte)'R' && content[1] == (byte)'I' && content[2] == (byte)'F' && content[3] == (byte)'F' &&
            content[8] == (byte)'W' && content[9] == (byte)'E' && content[10] == (byte)'B' && content[11] == (byte)'P')
        {
            return (".webp", "image/webp");
        }

        return null;
    }

    /// <summary>
    /// Tên file chỉ để hiển thị lại cho người dùng và đặt vào <c>Content-Disposition</c>. Bỏ mọi
    /// thành phần đường dẫn và ký tự điều khiển; phần mở rộng lấy theo định dạng thật đã nhận diện.
    /// </summary>
    private static string SafeFileName(string? fileName, string extension)
    {
        var name = Path.GetFileNameWithoutExtension(fileName ?? string.Empty).Trim();
        name = new string(name.Where(c => !Path.GetInvalidFileNameChars().Contains(c) && !char.IsControl(c)).ToArray());

        if (name.Length == 0)
        {
            name = "image";
        }
        else if (name.Length > 200)
        {
            name = name[..200];
        }

        return name + extension;
    }
}
