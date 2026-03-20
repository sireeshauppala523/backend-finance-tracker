using BCrypt.Net;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PersonalFinanceTracker.Api.Common;
using PersonalFinanceTracker.Api.Data;
using PersonalFinanceTracker.Api.DTOs.Auth;
using PersonalFinanceTracker.Api.Entities;
using PersonalFinanceTracker.Api.Options;
using PersonalFinanceTracker.Api.Services.Interfaces;

namespace PersonalFinanceTracker.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(AppDbContext dbContext, ITokenService tokenService, IOptions<JwtOptions> jwtOptions) : ControllerBase
{
    [HttpPost("register")]
    public async Task<ActionResult<ApiResponse<AuthResponse>>> Register(RegisterRequest request, CancellationToken cancellationToken)
    {
        if (request.Password.Length < 8 || !request.Password.Any(char.IsUpper) || !request.Password.Any(char.IsLower) || !request.Password.Any(char.IsDigit))
        {
            return BadRequest(new ApiResponse<string>(false, string.Empty, "Password must include upper, lower, number and be at least 8 characters."));
        }

        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        var exists = await dbContext.Users.AnyAsync(x => x.Email == normalizedEmail, cancellationToken);
        if (exists)
        {
            return Conflict(new ApiResponse<string>(false, string.Empty, "Email already exists."));
        }

        var user = new User
        {
            Email = normalizedEmail,
            DisplayName = request.DisplayName.Trim(),
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password)
        };

        dbContext.Users.Add(user);
        dbContext.Accounts.Add(new Account
        {
            UserId = user.Id,
            Name = "Main Account",
            Type = "bank account",
            OpeningBalance = 0,
            CurrentBalance = 0
        });

        var refreshToken = tokenService.CreateRefreshToken(user.Id);
        dbContext.RefreshTokens.Add(refreshToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new ApiResponse<AuthResponse>(true, new AuthResponse(
            tokenService.CreateAccessToken(user),
            refreshToken.Token,
            DateTime.UtcNow.AddMinutes(jwtOptions.Value.AccessTokenMinutes),
            user.DisplayName,
            user.Email)));
    }

    [HttpPost("login")]
    public async Task<ActionResult<ApiResponse<AuthResponse>>> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        var user = await dbContext.Users.SingleOrDefaultAsync(x => x.Email == request.Email.Trim().ToLowerInvariant(), cancellationToken);
        if (user is null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
        {
            return Unauthorized(new ApiResponse<string>(false, string.Empty, "Invalid email or password."));
        }

        var refreshToken = tokenService.CreateRefreshToken(user.Id);
        dbContext.RefreshTokens.Add(refreshToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new ApiResponse<AuthResponse>(true, new AuthResponse(
            tokenService.CreateAccessToken(user),
            refreshToken.Token,
            DateTime.UtcNow.AddMinutes(jwtOptions.Value.AccessTokenMinutes),
            user.DisplayName,
            user.Email)));
    }

    [HttpPost("refresh")]
    public async Task<ActionResult<ApiResponse<AuthResponse>>> Refresh(RefreshRequest request, CancellationToken cancellationToken)
    {
        var token = await dbContext.RefreshTokens.Include(x => x.User)
            .SingleOrDefaultAsync(x => x.Token == request.RefreshToken && !x.IsRevoked, cancellationToken);

        if (token is null || token.ExpiresAt < DateTime.UtcNow || token.User is null)
        {
            return Unauthorized(new ApiResponse<string>(false, string.Empty, "Refresh token is invalid or expired."));
        }

        token.IsRevoked = true;
        var nextRefresh = tokenService.CreateRefreshToken(token.UserId);
        dbContext.RefreshTokens.Add(nextRefresh);
        await dbContext.SaveChangesAsync(cancellationToken);

        var accessTokenMinutes = jwtOptions.Value?.AccessTokenMinutes ?? 60;

        return Ok(new ApiResponse<AuthResponse>(true, new AuthResponse(
            tokenService.CreateAccessToken(token.User),
            nextRefresh.Token,
            DateTime.UtcNow.AddMinutes(accessTokenMinutes),
            token.User.DisplayName,
            token.User.Email)));
    }

    [HttpPost("forgot-password")]
    public ActionResult<ApiResponse<object>> ForgotPassword(ForgotPasswordRequest request)
        => Ok(new ApiResponse<object>(true, new { request.Email }, "Forgot password flow stubbed for V1."));

    [HttpPost("reset-password")]
    public ActionResult<ApiResponse<object>> ResetPassword(ResetPasswordRequest request)
        => Ok(new ApiResponse<object>(true, new { request.Email }, "Reset password flow stubbed for V1."));
}