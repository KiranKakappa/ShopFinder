using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace ShopFinder.Api.Services;

/// <summary>PBKDF2-SHA256 password hashing. Stored as "iterations.saltBase64.hashBase64".</summary>
public static class Passwords
{
    private const int Iterations = 100_000;
    private const int SaltSize = 16;
    private const int HashSize = 32;

    public static string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, HashSize);
        return $"{Iterations}.{Convert.ToBase64String(salt)}.{Convert.ToBase64String(hash)}";
    }

    public static bool Verify(string password, string stored)
    {
        var parts = stored.Split('.');
        if (parts.Length != 3 || !int.TryParse(parts[0], out var iterations)) return false;
        try
        {
            var salt = Convert.FromBase64String(parts[1]);
            var expected = Convert.FromBase64String(parts[2]);
            var actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, expected.Length);
            return CryptographicOperations.FixedTimeEquals(actual, expected);
        }
        catch (FormatException)
        {
            return false;
        }
    }
}

/// <summary>Issues JWTs for shop owners. The token carries the shop id in a "shopId" claim.</summary>
public class TokenService
{
    private readonly IConfiguration config;
    public TokenService(IConfiguration config) => this.config = config;

    public const string ShopIdClaim = "shopId";

    public static SymmetricSecurityKey SigningKey(IConfiguration config)
    {
        var key = config["Jwt:Key"];
        if (string.IsNullOrWhiteSpace(key) || key.Length < 32)
            throw new InvalidOperationException("Jwt:Key must be set to a secret of at least 32 characters.");
        return new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key));
    }

    public string CreateToken(int shopId, string shopName)
    {
        var days = int.TryParse(config["Jwt:ExpiryDays"], out var d) ? d : 30;
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = config["Jwt:Issuer"],
            Audience = config["Jwt:Audience"],
            Subject = new ClaimsIdentity(new[]
            {
                new Claim(ShopIdClaim, shopId.ToString()),
                new Claim("name", shopName)
            }),
            Expires = DateTime.UtcNow.AddDays(days),
            SigningCredentials = new SigningCredentials(SigningKey(config), SecurityAlgorithms.HmacSha256)
        };
        return new JsonWebTokenHandler().CreateToken(descriptor);
    }

    public static int? GetShopId(ClaimsPrincipal user) =>
        int.TryParse(user.FindFirst(ShopIdClaim)?.Value, out var id) ? id : null;
}
