using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using OrduCep.Application.Services;
using OrduCep.Domain.Entities;

namespace OrduCep.API.Auth;

public static class AuthSchemes
{
    public const string Bearer = "Bearer";
}

public sealed record JwtTokenResult(string Token, DateTime ExpiresAtUtc);

public interface IJwtTokenService
{
    JwtTokenResult CreateUserToken(MilitaryIdentityUser user);
    JwtTokenResult CreateAdminToken(string adminIdentity);
    ClaimsPrincipal? ValidateToken(string token);
}

public sealed class JwtTokenService : IJwtTokenService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly IConfiguration _configuration;

    public JwtTokenService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public JwtTokenResult CreateUserToken(MilitaryIdentityUser user)
    {
        var expiresAtUtc = DateTime.UtcNow.AddHours(GetExpiryHours());
        var payload = new Dictionary<string, object?>
        {
            ["sub"] = user.Id.ToString(),
            ["role"] = "User",
            ["name"] = BuildFullName(user.FirstName, user.LastName),
            ["canUseFacilities"] = PersonnelAccessRules.CanUseFacilities(user.OwnerRank),
            ["iat"] = ToUnixSeconds(DateTime.UtcNow),
            ["exp"] = ToUnixSeconds(expiresAtUtc)
        };

        return new JwtTokenResult(Sign(payload), expiresAtUtc);
    }

    public JwtTokenResult CreateAdminToken(string adminIdentity)
    {
        var expiresAtUtc = DateTime.UtcNow.AddHours(GetExpiryHours());
        var payload = new Dictionary<string, object?>
        {
            ["sub"] = adminIdentity,
            ["role"] = "Admin",
            ["name"] = "Admin",
            ["canUseFacilities"] = true,
            ["iat"] = ToUnixSeconds(DateTime.UtcNow),
            ["exp"] = ToUnixSeconds(expiresAtUtc)
        };

        return new JwtTokenResult(Sign(payload), expiresAtUtc);
    }

    public ClaimsPrincipal? ValidateToken(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return null;

        var parts = token.Split('.');
        if (parts.Length != 3)
            return null;

        var headerJson = DecodeBase64Url(parts[0]);
        var payloadJson = DecodeBase64Url(parts[1]);
        if (headerJson == null || payloadJson == null)
            return null;

        using var header = JsonDocument.Parse(headerJson);
        var alg = header.RootElement.TryGetProperty("alg", out var algElement)
            ? algElement.GetString()
            : null;
        if (!string.Equals(alg, "HS256", StringComparison.Ordinal))
            return null;

        var signed = $"{parts[0]}.{parts[1]}";
        var expectedSignature = Base64UrlEncode(HMACSHA256.HashData(GetSecretBytes(), Encoding.UTF8.GetBytes(signed)));
        if (!CryptographicOperations.FixedTimeEquals(
                Encoding.ASCII.GetBytes(parts[2]),
                Encoding.ASCII.GetBytes(expectedSignature)))
            return null;

        using var payload = JsonDocument.Parse(payloadJson);
        var root = payload.RootElement;

        if (!TryGetString(root, "sub", out var subject) || string.IsNullOrWhiteSpace(subject))
            return null;

        if (!TryGetString(root, "role", out var role) || string.IsNullOrWhiteSpace(role))
            return null;

        if (!root.TryGetProperty("exp", out var expElement) || !expElement.TryGetInt64(out var expSeconds))
            return null;

        if (DateTimeOffset.FromUnixTimeSeconds(expSeconds).UtcDateTime <= DateTime.UtcNow)
            return null;

        TryGetString(root, "name", out var displayName);
        var claims = new List<Claim>
        {
            new("sub", subject),
            new(ClaimTypes.NameIdentifier, subject),
            new(ClaimTypes.Role, role),
            new(ClaimTypes.Name, displayName ?? subject)
        };

        if (root.TryGetProperty("canUseFacilities", out var canUseElement) &&
            (canUseElement.ValueKind == JsonValueKind.True || canUseElement.ValueKind == JsonValueKind.False))
        {
            claims.Add(new Claim("canUseFacilities", canUseElement.GetBoolean().ToString()));
        }

        return new ClaimsPrincipal(new ClaimsIdentity(claims, AuthSchemes.Bearer));
    }

    private string Sign(Dictionary<string, object?> payload)
    {
        var header = new Dictionary<string, object?>
        {
            ["alg"] = "HS256",
            ["typ"] = "JWT"
        };

        var encodedHeader = Base64UrlEncode(JsonSerializer.SerializeToUtf8Bytes(header, JsonOptions));
        var encodedPayload = Base64UrlEncode(JsonSerializer.SerializeToUtf8Bytes(payload, JsonOptions));
        var signed = $"{encodedHeader}.{encodedPayload}";
        var signature = Base64UrlEncode(HMACSHA256.HashData(GetSecretBytes(), Encoding.UTF8.GetBytes(signed)));
        return $"{signed}.{signature}";
    }

    private byte[] GetSecretBytes()
    {
        var secret = _configuration["JWT_SECRET"] ?? _configuration["AUTH_JWT_SECRET"];
        if (string.IsNullOrWhiteSpace(secret))
            secret = "orducep-local-dev-jwt-secret-change-before-production-2026";

        return Encoding.UTF8.GetBytes(secret);
    }

    private int GetExpiryHours()
    {
        return int.TryParse(_configuration["JWT_EXPIRY_HOURS"], out var hours) && hours is > 0 and <= 168
            ? hours
            : 12;
    }

    private static long ToUnixSeconds(DateTime value)
    {
        return new DateTimeOffset(DateTime.SpecifyKind(value, DateTimeKind.Utc)).ToUnixTimeSeconds();
    }

    private static bool TryGetString(JsonElement root, string propertyName, out string? value)
    {
        value = root.TryGetProperty(propertyName, out var element) && element.ValueKind == JsonValueKind.String
            ? element.GetString()
            : null;
        return value != null;
    }

    private static string? DecodeBase64Url(string value)
    {
        try
        {
            return Encoding.UTF8.GetString(Base64UrlDecode(value));
        }
        catch
        {
            return null;
        }
    }

    private static byte[] Base64UrlDecode(string value)
    {
        var base64 = value.Replace('-', '+').Replace('_', '/');
        base64 = base64.PadRight(base64.Length + (4 - base64.Length % 4) % 4, '=');
        return Convert.FromBase64String(base64);
    }

    private static string Base64UrlEncode(byte[] bytes)
    {
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    private static string BuildFullName(params string[] parts)
    {
        return string.Join(" ", parts.Where(part => !string.IsNullOrWhiteSpace(part)).Select(part => part.Trim()));
    }
}

public sealed class SimpleJwtAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    private readonly IJwtTokenService _jwtTokenService;

    public SimpleJwtAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IJwtTokenService jwtTokenService)
        : base(options, logger, encoder)
    {
        _jwtTokenService = jwtTokenService;
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var authorization = Request.Headers.Authorization.ToString();
        if (string.IsNullOrWhiteSpace(authorization))
            return Task.FromResult(AuthenticateResult.NoResult());

        const string bearerPrefix = "Bearer ";
        if (!authorization.StartsWith(bearerPrefix, StringComparison.OrdinalIgnoreCase))
            return Task.FromResult(AuthenticateResult.NoResult());

        var token = authorization[bearerPrefix.Length..].Trim();
        var principal = _jwtTokenService.ValidateToken(token);
        if (principal == null)
            return Task.FromResult(AuthenticateResult.Fail("Geçersiz veya süresi dolmuş token."));

        var ticket = new AuthenticationTicket(principal, AuthSchemes.Bearer);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
