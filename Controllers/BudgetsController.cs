using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PersonalFinanceTracker.Api.Common;
using PersonalFinanceTracker.Api.Data;
using PersonalFinanceTracker.Api.DTOs.Budgets;
using PersonalFinanceTracker.Api.Entities;
using PersonalFinanceTracker.Api.Extensions;

namespace PersonalFinanceTracker.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/budgets")]
public class BudgetsController(AppDbContext dbContext) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ApiResponse<object>>> Get([FromQuery] int month, [FromQuery] int year, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        var budgets = await dbContext.Budgets
            .Include(x => x.Category)
            .Where(x => x.UserId == userId && x.Month == month && x.Year == year)
            .ToListAsync(cancellationToken);

        var spentByCategory = await dbContext.Transactions
            .Where(x => x.UserId == userId && x.Type == "expense" && x.TransactionDate.Month == month && x.TransactionDate.Year == year && x.CategoryId != null)
            .GroupBy(x => x.CategoryId!.Value)
            .Select(group => new { CategoryId = group.Key, Spent = group.Sum(x => x.Amount) })
            .ToDictionaryAsync(x => x.CategoryId, x => x.Spent, cancellationToken);

        var result = budgets.Select(budget => new
        {
            budget.Id,
            budget.Month,
            budget.Year,
            budget.Amount,
            budget.AlertThresholdPercent,
            category = budget.Category,
            spent = spentByCategory.TryGetValue(budget.CategoryId, out var spent) ? spent : 0,
            progress = spentByCategory.TryGetValue(budget.CategoryId, out var ratioSpent) && budget.Amount > 0 ? decimal.Round(ratioSpent / budget.Amount * 100, 2) : 0
        });

        return Ok(new ApiResponse<object>(true, result));
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<object>>> Create(BudgetRequest request, CancellationToken cancellationToken)
    {
        var budget = new Budget
        {
            UserId = User.GetUserId(),
            CategoryId = request.CategoryId,
            Month = request.Month,
            Year = request.Year,
            Amount = request.Amount,
            AlertThresholdPercent = request.AlertThresholdPercent
        };

        dbContext.Budgets.Add(budget);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(new ApiResponse<object>(true, budget));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ApiResponse<object>>> Update(Guid id, BudgetRequest request, CancellationToken cancellationToken)
    {
        var budget = await dbContext.Budgets.SingleOrDefaultAsync(x => x.Id == id && x.UserId == User.GetUserId(), cancellationToken);
        if (budget is null) return NotFound();

        budget.CategoryId = request.CategoryId;
        budget.Month = request.Month;
        budget.Year = request.Year;
        budget.Amount = request.Amount;
        budget.AlertThresholdPercent = request.AlertThresholdPercent;
        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(new ApiResponse<object>(true, budget));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var budget = await dbContext.Budgets.SingleOrDefaultAsync(x => x.Id == id && x.UserId == User.GetUserId(), cancellationToken);
        if (budget is null) return NotFound();
        dbContext.Budgets.Remove(budget);
        await dbContext.SaveChangesAsync(cancellationToken);
        return NoContent();
    }
}