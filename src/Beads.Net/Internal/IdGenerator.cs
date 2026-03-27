using System.Security.Cryptography;
using System.Text;

namespace Beads.Net.Internal;

public static class IdGenerator
{
    private const string Base36Chars = "0123456789abcdefghijklmnopqrstuvwxyz";
    private const int DefaultLength = 6;

    public static string Generate(string prefix, string title, DateTime createdAt, Func<string, bool> exists)
    {
        for (var nonce = 0; nonce < 1000; nonce++)
        {
            var material = $"{title}\n{createdAt:O}\n{nonce}";
            var hash = SHA256.HashData(Encoding.UTF8.GetBytes(material));
            var b36 = ToBase36(hash, DefaultLength);
            var id = $"{prefix}-{b36}";

            if (!exists(id))
                return id;
        }

        // Extremely unlikely: fall back to longer hash
        var fallbackMaterial = $"{title}\n{createdAt:O}\n{Guid.NewGuid()}";
        var fallbackHash = SHA256.HashData(Encoding.UTF8.GetBytes(fallbackMaterial));
        return $"{prefix}-{ToBase36(fallbackHash, 10)}";
    }

    internal static string ToBase36(byte[] bytes, int length)
    {
        var sb = new StringBuilder(length);
        for (var i = 0; i < length && i < bytes.Length; i++)
        {
            sb.Append(Base36Chars[bytes[i] % 36]);
        }
        return sb.ToString();
    }
}
