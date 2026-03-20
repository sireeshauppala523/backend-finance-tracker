using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PersonalFinanceTracker.Api.Common;
using PersonalFinanceTracker.Api.Extensions;
using PersonalFinanceTracker.Api.Services.Interfaces;

namespace PersonalFinanceTracker.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/reports")]
public class ReportsController(IReportService reportService) : ControllerBase
{
    [HttpGet("category-spend")]
    public async Task<ActionResult<ApiResponse<object>>> CategorySpend([FromQuery] DateOnly? from, [FromQuery] DateOnly? to, CancellationToken cancellationToken)
        => Ok(new ApiResponse<object>(true, await reportService.GetCategorySpendAsync(User.GetUserId(), from, to, cancellationToken)));

    [HttpGet("income-vs-expense")]
    public async Task<ActionResult<ApiResponse<object>>> IncomeVsExpense([FromQuery] DateOnly? from, [FromQuery] DateOnly? to, CancellationToken cancellationToken)
        => Ok(new ApiResponse<object>(true, await reportService.GetIncomeVsExpenseAsync(User.GetUserId(), from, to, cancellationToken)));

    [HttpGet("account-balance-trend")]
    public async Task<ActionResult<ApiResponse<object>>> AccountBalanceTrend(CancellationToken cancellationToken)
        => Ok(new ApiResponse<object>(true, await reportService.GetAccountBalanceTrendAsync(User.GetUserId(), cancellationToken)));
}