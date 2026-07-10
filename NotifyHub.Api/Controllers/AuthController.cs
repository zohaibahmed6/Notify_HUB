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

[ApiController]
[Route("api/auth")]
public class AuthController(
    NotifyHubDbContext db,
    IPasswordHasher<User> passwordHasher,
    JwtTokenService tokenService) : ControllerBase
{
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

        var response = await IssueTokensAsync(user);
        return Ok(response);
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthResponse>> Refresh(RefreshRequest request)
    {
        var tokenHash = tokenService.HashRefreshToken(request.RefreshToken);

        var existing = await db.RefreshTokens
            .Include(rt => rt.User)
            .SingleOrDefaultAsync(rt => rt.TokenHash == tokenHash);

        if (existing is null || !existing.IsActive)
        {
            return Problem(
                statusCode: StatusCodes.Status401Unauthorized,
                title: "Invalid or expired refresh token");
        }

        var response = await IssueTokensAsync(existing.User, supersedes: existing);
        return Ok(response);
    }

    /// Proves RBAC/JWT wiring end-to-end: any authenticated user can read their own identity.
    [HttpGet("me")]
    public ActionResult<AuthUserDto> Me()
    {
        var id = long.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var username = User.FindFirstValue(ClaimTypes.Name)!;
        var role = User.FindFirstValue(ClaimTypes.Role)!;

        return Ok(new AuthUserDto { Id = id, Username = username, Role = role });
    }

    /// Admin-only diagnostic route proving role-based restriction is enforced server-side.
    [HttpGet("admin-only")]
    [Authorize(Roles = "Admin")]
    public ActionResult AdminOnly() => Ok();

    private async Task<AuthResponse> IssueTokensAsync(User user, RefreshToken? supersedes = null)
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

        return new AuthResponse
        {
            AccessToken = accessToken,
            AccessTokenExpiresAt = accessTokenExpiresAt,
            RefreshToken = rawRefreshToken,
            RefreshTokenExpiresAt = refreshTokenExpiresAt,
            User = new AuthUserDto
            {
                Id = user.Id,
                Username = user.Username,
                Role = user.Role.ToString(),
            },
        };
    }
}
