using System.Security.Cryptography;
using ProductionManagement.Application.Abstractions;

namespace ProductionManagement.Infrastructure.Security;

/// <summary>
/// Băm mật khẩu bằng PBKDF2-HMAC-SHA256. Chỉ phần hash được lưu xuống (Step 3 §2.1).
/// Định dạng: <c>pbkdf2$sha256$&lt;iterations&gt;$&lt;saltBase64&gt;$&lt;hashBase64&gt;</c>.
/// </summary>
public sealed class Pbkdf2PasswordHasher : IPasswordHasher
{
    private const int SaltSize = 16;
    private const int KeySize = 32;
    // OWASP khuyến nghị 600.000 vòng cho PBKDF2-HMAC-SHA256. Hash cũ (210.000 vòng) vẫn verify được
    // vì số vòng đọc từ chính hash, và được băm lại khi đăng nhập thành công (NeedsRehash).
    private const int Iterations = 600_000;
    private const string Prefix = "pbkdf2$sha256";

    public bool NeedsRehash(string hash)
    {
        var parts = hash.Split('$');
        return parts.Length != 5
               || $"{parts[0]}${parts[1]}" != Prefix
               || !int.TryParse(parts[2], out var iterations)
               || iterations < Iterations;
    }

    public string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var key = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, KeySize);

        return $"{Prefix}${Iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(key)}";
    }

    public bool Verify(string password, string hash)
    {
        var parts = hash.Split('$');
        if (parts.Length != 5 || parts[0] != "pbkdf2" || parts[1] != "sha256")
        {
            return false;
        }

        if (!int.TryParse(parts[2], out var iterations) || iterations <= 0)
        {
            return false;
        }

        byte[] salt;
        byte[] expected;
        try
        {
            salt = Convert.FromBase64String(parts[3]);
            expected = Convert.FromBase64String(parts[4]);
        }
        catch (FormatException)
        {
            return false;
        }

        var actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, expected.Length);

        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }
}
