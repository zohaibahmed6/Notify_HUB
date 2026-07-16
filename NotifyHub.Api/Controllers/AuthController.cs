using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NotifyHub.Api.Auth;
using NotifyHub.Api.Auth.Dtos;
using NotifyHub.Domain.Entities;
using NotifyHub.Infrastructure.Persistence;

namespace NotifyHub.Api.Controllers;

/// §6a: the refresh token lives only in an httpOnly cookie — never in a JSON response
/// body or any JS-readable storage — so an XSS payload that hooks fetch/localStorage
/// still can't exfiltrate it. The access token remains in-memory on the frontend, as
/// before; only the refresh token's transport changed (body -> cookie), to make a
/// silent session restore on page load possible without violating "not localStorage."
[ApiController]
[Route("api/auth")]
public class AuthController(
    NotifyHubDbContext db,
    IPasswordHasher<User> passwordHasher,
    JwtTokenService tokenService,
    IWebHostEnvironment env) : ControllerBase
{
    private const string RefreshCookieName = "notifyhub_refresh";

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request)
    {
        var user = await db.Users.SingleOrDefaultAsync(u => u.Username == request.Username);

        if (user is null ||
            passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password)
                == PasswordVerificationResult.Failed)
        {
            return Problem(
                statusCode: StatusCodes.Status401Unauthorized,
                title: "Invalid username or password");
        }

        var (response, rawRefreshToken, refreshTokenExpiresAt) = await IssueTokensAsync(user);
        SetRefreshCookie(rawRefreshToken, refreshTokenExpiresAt);
        return Ok(response);
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthResponse>> Refresh()
    {
        var rawRefreshToken = Request.Cookies[RefreshCookieName];
        if (string.IsNullOrEmpty(rawRefreshToken))
        {
            return Problem(
                statusCode: StatusCodes.Status401Unauthorized,
                title: "Invalid or expired refresh token");
        }

        var tokenHash = tokenService.HashRefreshToken(rawRefreshToken);

        var existing = await db.RefreshTokens
            .Include(rt => rt.User)
            .SingleOrDefaultAsync(rt => rt.TokenHash == tokenHash);

        if (existing is null || !existing.IsActive)
        {
            DeleteRefreshCookie();
            return Problem(
                statusCode: StatusCodes.Status401Unauthorized,
                title: "Invalid or expired refresh token");
        }

        var (response, newRawRefreshToken, refreshTokenExpiresAt) = await IssueTokensAsync(existing.User, supersedes: existing);
        SetRefreshCookie(newRawRefreshToken, refreshTokenExpiresAt);
        return Ok(response);
    }

    /// Without this, client-side logout wouldn't actually end the session — the httpOnly
    /// cookie would still be there to silently restore it on the next page load.
    [HttpPost("logout")]
    [AllowAnonymous]
    public async Task<ActionResult> Logout()
    {
        var rawRefreshToken = Request.Cookies[RefreshCookieName];
        if (!string.IsNullOrEmpty(rawRefreshToken))
        {
            var tokenHash = tokenService.HashRefreshToken(rawRefreshToken);
            var existing = await db.RefreshTokens.SingleOrDefaultAsync(rt => rt.TokenHash == tokenHash);
            if (existing is not null && existing.IsActive)
            {
                existing.RevokedAt = DateTime.UtcNow;
                await db.SaveChangesAsync();
            }
        }

        DeleteRefreshCookie();
        return NoContent();
    }

    /// Proves RBAC/JWT wiring end-to-end: any authenticated user can read their own identity.
    [HttpGet("me")]
    public async Task<ActionResult<AuthUserDto>> Me()
    {
        var id = long.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var username = User.FindFirstValue(ClaimTypes.Name)!;
        var role = User.FindFirstValue(ClaimTypes.Role)!;
        var fullName = await db.Users.Where(u => u.Id == id).Select(u => u.FullName).SingleOrDefaultAsync();

        return Ok(new AuthUserDto { Id = id, Username = username, FullName = fullName, Role = role });
    }

    /// Admin-only diagnostic route proving role-based restriction is enforced server-side.
    [HttpGet("admin-only")]
    [Authorize(Roles = "Admin")]
    public ActionResult AdminOnly() => Ok();

    private async Task<(AuthResponse Response, string RawRefreshToken, DateTime RefreshTokenExpiresAt)> IssueTokensAsync(
        User user, RefreshToken? supersedes = null)
    {
        var (accessToken, accessTokenExpiresAt) = tokenService.GenerateAccessToken(user);

        var rawRefreshToken = tokenService.GenerateOpaqueRefreshToken();
        var refreshTokenExpiresAt = tokenService.RefreshTokenExpiresAt();

        var newToken = new RefreshToken
        {
            UserId = user.Id,
            TokenHash = tokenService.HashRefreshToken(rawRefreshToken),
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = refreshTokenExpiresAt,
        };

        db.RefreshTokens.Add(newToken);

        if (supersedes is not null)
        {
            supersedes.RevokedAt = DateTime.UtcNow;
        }

        await db.SaveChangesAsync();

        if (supersedes is not null)
        {
            supersedes.ReplacedByTokenId = newToken.Id;
            await db.SaveChangesAsync();
        }

        var response = new AuthResponse
        {
            AccessToken = accessToken,
            AccessTokenExpiresAt = accessTokenExpiresAt,
            User = new AuthUserDto
            {
                Id = user.Id,
                Username = user.Username,
                FullName = user.FullName,
                Role = user.Role.ToString(),
            },
        };

        return (response, rawRefreshToken, refreshTokenExpiresAt);
    }

    private void SetRefreshCookie(string rawRefreshToken, DateTime expiresAt)
    {
        Response.Cookies.Append(RefreshCookieName, rawRefreshToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = !env.IsDevelopment(),
            SameSite = SameSiteMode.Lax,
            Path = "/api/auth",
            Expires = expiresAt,
        });
    }

    private void DeleteRefreshCookie()
    {
        Response.Cookies.Delete(RefreshCookieName, new CookieOptions { Path = "/api/auth" });
    }
}
