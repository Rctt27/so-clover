using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;

namespace SoClover.Infrastructure;

public interface IHmacValidator
{
    bool IsValid(HttpRequest request, string? secret);
}

/// <summary>
/// Very small HMAC validator for system-to-system calls. Computes HMAC SHA256 of a canonical string
/// "{method}\n{path}\n{timestamp}\n{contentHash}" using a shared secret. Client must send headers:
/// - X-System-Timestamp: unix seconds (to prevent replay basicly)
/// - X-System-Content-Hash: SHA256 hex of body (or empty string if no body)
/// - X-System-Signature: hex of HMACSHA256(secret, canonicalString)
/// This is intentionally simple; can be replaced by proper auth later.
/// </summary>
public sealed class HmacValidator : IHmacValidator
{
    public bool IsValid(HttpRequest request, string? secret)
    {
        if (string.IsNullOrWhiteSpace(secret)) return false;

        var ts = request.Headers["X-System-Timestamp"].ToString();
        var contentHash = request.Headers["X-System-Content-Hash"].ToString();
        var signature = request.Headers["X-System-Signature"].ToString();
        if (string.IsNullOrWhiteSpace(ts) || string.IsNullOrWhiteSpace(signature)) return false;

        // Normalize empty content hash
        contentHash ??= string.Empty;

        var canonical = string.Join("\n", new[]
        {
            request.Method.ToUpperInvariant(),
            request.Path.ToString(),
            ts,
            contentHash
        });

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var mac = hmac.ComputeHash(Encoding.UTF8.GetBytes(canonical));
        var hex = Convert.ToHexString(mac);
        // timing-safe-ish compare
        return CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(hex), Encoding.UTF8.GetBytes(signature));
    }
}
