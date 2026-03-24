using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PersonalFinanceTracker.Api.Common;
using PersonalFinanceTracker.Api.DTOs.Forecast;
using PersonalFinanceTracker.Api.Extensions;
using PersonalFinanceTracker.Api.Services.Interfaces;

namespace PersonalFinanceTracker.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/forecast")]
public class ForecastController(IForecastService forecastService) : ControllerBase
{
    [HttpGet("month")]
    public async Task<ActionResult<ApiResponse<ForecastMonthResponse>>> GetMonth(CancellationToken cancellationToken)
        => Ok(new ApiResponse<ForecastMonthResponse>(true, await forecastService.GetMonthForecastAsync(User.GetUserId(), cancellationToken)));

    [HttpGet("daily")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<ForecastDailyPoint>>>> GetDaily(CancellationToken cancellationToken)
        => Ok(new ApiResponse<IReadOnlyList<ForecastDailyPoint>>(true, await forecastService.GetDailyForecastAsync(User.GetUserId(), cancellationToken)));
}
