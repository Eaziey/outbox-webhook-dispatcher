using Outbox.Api.Interfaces.IServices;
using System.Security.Cryptography;
using System.Text;

namespace Outbox.Api.Services;

public class HmacSigner : IHmacSigner
{
    public string CreateSignature(string secret, string body, string timestamp)
    {
        var data = $"{timestamp}.{body}";
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(data));

        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}

