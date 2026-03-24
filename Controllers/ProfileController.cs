using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PersonalFinanceTracker.Api.Common;
using PersonalFinanceTracker.Api.Data;
using PersonalFinanceTracker.Api.DTOs.Profile;
using PersonalFinanceTracker.Api.Extensions;

namespace PersonalFinanceTracker.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/profile")]
public class ProfileController(AppDbContext dbContext) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ApiResponse<ProfileResponse>>> Get(CancellationToken cancellationToken)
    {
        var user = await dbContext.Users
            .Where(x => x.Id == User.GetUserId())
            .Select(x => new ProfileResponse(x.DisplayName, x.Email, x.AvatarUrl, x.PreferredCurrency))
            .SingleAsync(cancellationToken);

        return Ok(new ApiResponse<ProfileResponse>(true, user));
    }

    [HttpPut]
    public async Task<ActionResult<ApiResponse<ProfileResponse>>> Update(UpdateProfileRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.DisplayName))
        {
            return BadRequest(new ApiResponse<string>(false, string.Empty, "Display name is required."));
        }

        var preferredCurrency = string.IsNullOrWhiteSpace(request.PreferredCurrency) ? "INR" : request.PreferredCurrency.Trim().ToUpperInvariant();
        var allowedCurrencies = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "INR", "USD", "EUR", "GBP", "AED" };
        if (!allowedCurrencies.Contains(preferredCurrency))
        {
            return BadRequest(new ApiResponse<string>(false, string.Empty, "Preferred currency is invalid."));
        }

        if (!string.IsNullOrWhiteSpace(request.AvatarUrl) && request.AvatarUrl.Length > 1_000_000)
        {
            return BadRequest(new ApiResponse<string>(false, string.Empty, "Profile image is too large."));
        }

        var user = await dbContext.Users.SingleAsync(x => x.Id == User.GetUserId(), cancellationToken);
        user.DisplayName = request.DisplayName.Trim();
        user.AvatarUrl = string.IsNullOrWhiteSpace(request.AvatarUrl) ? null : request.AvatarUrl;
        user.PreferredCurrency = preferredCurrency;

        await dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new ApiResponse<ProfileResponse>(true, new ProfileResponse(user.DisplayName, user.Email, user.AvatarUrl, user.PreferredCurrency)));
    }
}
