using System.Security.Cryptography;
using System.Text;

namespace TradeTest.Api;

public sealed class ApiAccess
{
    private readonly byte[]? _tokenHash;
    public bool RequiresToken => _tokenHash is not null;

    public ApiAccess(string? token)
    {
        if (token is not null && token.Length < 32)
            throw new ArgumentException("TRADETEST_API_TOKEN must contain at least 32 characters.");
        _tokenHash = token is null ? null : SHA256.HashData(Encoding.UTF8.GetBytes(token));
    }

    public bool Allows(string? authorization)
    {
        if (_tokenHash is null) return true;
        if (authorization is null || !authorization.StartsWith("Bearer ", StringComparison.Ordinal) || authorization.Length > 4096)
            return false;
        byte[] supplied = SHA256.HashData(Encoding.UTF8.GetBytes(authorization[7..]));
        return CryptographicOperations.FixedTimeEquals(supplied, _tokenHash);
    }

    public void ValidateBindings(string urls, bool privateData)
    {
        if (privateData && !RequiresToken)
            throw new ArgumentException("A private database requires TRADETEST_API_TOKEN, including on localhost.");
        foreach (string binding in urls.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!Uri.TryCreate(binding, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https"))
                throw new ArgumentException("Use explicit HTTP(S) service bindings.");
            if (!uri.IsLoopback && !RequiresToken)
                throw new ArgumentException("Non-loopback service bindings require TRADETEST_API_TOKEN.");
        }
    }
}
