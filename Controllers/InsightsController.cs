using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PersonalFinanceTracker.Api.Common;
using PersonalFinanceTracker.Api.DTOs.Insights;
using PersonalFinanceTracker.Api.Extensions;
using PersonalFinanceTracker.Api.Services.Interfaces;

namespace PersonalFinanceTracker.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/insights")]
public class InsightsController(IInsightsService insightsService) : ControllerBase
{
    [HttpGet("health-score")]
    public async Task<ActionResult<ApiResponse<FinancialHealthScoreResponse>>> GetHealthScore(CancellationToken cancellationToken)
        => Ok(new ApiResponse<FinancialHealthScoreResponse>(true, await insightsService.GetHealthScoreAsync(User.GetUserId(), cancellationToken)));
}
