using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AquaFarm.Api.Models;
using Microsoft.Extensions.Options;

namespace AquaFarm.Api.Security;

public sealed class SimpleTokenService
{
    private readonly byte[] _key;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public SimpleTokenService(IOptions<JwtSettings> jwtOptions)
    {
        var configuredSecret = jwtOptions.Value?.Secret;
        var effectiveSecret = string.IsNullOrWhiteSpace(configuredSecret)
            ? "DevelopmentJwtSecretKey123!"
            : configuredSecret;
        _key = Encoding.UTF8.GetBytes(effectiveSecret);
    }

    public string CreateToken(Guid userId, string userName, string role)
    {
        var header = new TokenHeader("HS256", "JWT");
        var payload = new TokenPayload(
            UserId: userId,
            UserName: userName,
            Role: role,
            Sub: userName,
            Exp: DateTimeOffset.UtcNow.AddHours(8).ToUnixTimeSeconds());

        var headerJson = JsonSerializer.Serialize(header, JsonOptions);
        var payloadJson = JsonSerializer.Serialize(payload, JsonOptions);
        var headerEncoded = Base64UrlEncode(Encoding.UTF8.GetBytes(headerJson));
        var payloadEncoded = Base64UrlEncode(Encoding.UTF8.GetBytes(payloadJson));
        var signingInput = $"{headerEncoded}.{payloadEncoded}";
        var signature = Sign(signingInput);

        return $"{signingInput}.{signature}";
    }

    public bool TryValidateToken(string token, out ClaimsPrincipal? principal)
    {
        principal = null;
        if (string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        var parts = token.Split('.');
        if (parts.Length != 3)
        {
            return false;
        }

        var headerEncoded = parts[0];
        var payloadEncoded = parts[1];
        var providedSignature = parts[2];
        var signingInput = $"{headerEncoded}.{payloadEncoded}";
        var expectedSignature = Sign(signingInput);

        if (!FixedTimeEquals(providedSignature, expectedSignature))
        {
            return false;
        }

        TokenPayload? payload;
        try
        {
            var payloadJson = Encoding.UTF8.GetString(Base64UrlDecode(payloadEncoded));
            payload = JsonSerializer.Deserialize<TokenPayload>(payloadJson, JsonOptions);
        }
        catch
        {
            return false;
        }

        if (payload is null || payload.Exp <= DateTimeOffset.UtcNow.ToUnixTimeSeconds())
        {
            return false;
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, payload.UserId.ToString()),
            new(ClaimTypes.Name, payload.UserName),
            new(ClaimTypes.Role, payload.Role),
            new("sub", payload.Sub)
        };

        var identity = new ClaimsIdentity(claims, "Bearer");
        principal = new ClaimsPrincipal(identity);
        return true;
    }

    private string Sign(string payloadEncoded)
    {
        using var hmac = new HMACSHA256(_key);
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payloadEncoded));
        return Base64UrlEncode(hash);
    }

    private static bool FixedTimeEquals(string left, string right)
    {
        var leftBytes = Encoding.UTF8.GetBytes(left);
        var rightBytes = Encoding.UTF8.GetBytes(right);
        return CryptographicOperations.FixedTimeEquals(leftBytes, rightBytes);
    }

    private static string Base64UrlEncode(byte[] bytes)
    {
        return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    private static byte[] Base64UrlDecode(string value)
    {
        var incoming = value.Replace('-', '+').Replace('_', '/');
        var padding = incoming.Length % 4;
        if (padding == 2)
        {
            incoming += "==";
        }
        else if (padding == 3)
        {
            incoming += "=";
        }
        else if (padding != 0)
        {
            throw new FormatException("Invalid base64url input.");
        }

        return Convert.FromBase64String(incoming);
    }

    private sealed record TokenHeader(string Alg, string Typ);
    private sealed record TokenPayload(Guid UserId, string UserName, string Role, string Sub, long Exp);
}
