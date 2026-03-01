namespace Outbox.Api.Interfaces.IServices;

public interface IHmacSigner
{
    string CreateSignature(string secret, string body, string timestamp);
}
