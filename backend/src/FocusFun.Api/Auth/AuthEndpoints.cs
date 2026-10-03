using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using FocusFun.Api.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace FocusFun.Api.Auth;

public sealed class AuthOptions
{
    public const string Section = "Auth";

    /// <summary>HMAC signing key, at least 32 characters. Keep it in user-secrets or the host's environment.</summary>
    public string JwtKey { get; set; } = string.Empty;
    public string Issuer { get; set; } = "focusfun";
    public TimeSpan TokenLifetime { get; set; } = TimeSpan.FromDays(30);

    /// <summary>The first account can always register; after that only when this is true.</summary>
    public bool AllowRegistration { get; set; }

    public SymmetricSecurityKey SigningKey() => new(Encoding.UTF8.GetBytes(JwtKey));
}

public static class AuthEndpoints
{
    private const int MinPasswordLength = 10;

    public const string AdminClaim = "admin";
    public const string AdminPolicy = "admin";

    public static void MapAuthEndpoints(this IEndpointRouteBuilder api)
    {
        var auth = api.MapGroup("/auth");

        auth.MapPost("/register", async (Credentials body, FocusFunDb db, IOptions<AuthOptions> options, IPasswordHasher<Parent> hasher) =>
        {
            string email = Normalise(body.Email);
            if (!email.Contains('@') || body.Password.Length < MinPasswordLength)
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["credentials"] = [$"Use a valid email and a password of at least {MinPasswordLength} characters."],
                });

            if (!options.Value.AllowRegistration && await db.Parents.AnyAsync())
                return Results.Problem("Registration is closed.", statusCode: StatusCodes.Status403Forbidden);
            if (await db.Parents.AnyAsync(p => p.Email == email))
                return Results.Conflict(new { error = "That email already has an account." });

            var parent = new Parent { Email = email, PasswordHash = string.Empty };
            parent.PasswordHash = hasher.HashPassword(parent, body.Password);
            db.Parents.Add(parent);
            await db.SaveChangesAsync();
            return Results.Ok(new TokenResponse(CreateToken(parent, options.Value), parent.IsAdmin));
        });

        auth.MapPost("/login", async (Credentials body, FocusFunDb db, IOptions<AuthOptions> options, IPasswordHasher<Parent> hasher) =>
        {
            string email = Normalise(body.Email);
            var parent = await db.Parents.SingleOrDefaultAsync(p => p.Email == email);
            if (parent is null || hasher.VerifyHashedPassword(parent, parent.PasswordHash, body.Password) == PasswordVerificationResult.Failed)
                return Results.Unauthorized();
            return Results.Ok(new TokenResponse(CreateToken(parent, options.Value), parent.IsAdmin));
        });
    }

    public static Guid ParentId(this ClaimsPrincipal user) =>
        Guid.Parse(user.FindFirstValue(JwtRegisteredClaimNames.Sub) ?? user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new InvalidOperationException("Token has no subject."));

    private static string CreateToken(Parent parent, AuthOptions options)
    {
        var token = new JwtSecurityToken(
            issuer: options.Issuer,
            audience: options.Issuer,
            claims: parent.IsAdmin
                ? [new Claim(JwtRegisteredClaimNames.Sub, parent.Id.ToString()), new Claim(AdminClaim, "true")]
                : [new Claim(JwtRegisteredClaimNames.Sub, parent.Id.ToString())],
            expires: DateTime.UtcNow.Add(options.TokenLifetime),
            signingCredentials: new SigningCredentials(options.SigningKey(), SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public static string Normalise(string login) => login.Trim().ToLowerInvariant();

    private sealed record Credentials(string Email, string Password);

    private sealed record TokenResponse(string Token, bool IsAdmin);
}
