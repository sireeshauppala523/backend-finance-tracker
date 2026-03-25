using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PersonalFinanceTracker.Api.Common;
using PersonalFinanceTracker.Api.Data;
using PersonalFinanceTracker.Api.DTOs.Budgets;
using PersonalFinanceTracker.Api.Entities;
using PersonalFinanceTracker.Api.Extensions;
using PersonalFinanceTracker.Api.Services.Interfaces;

namespace PersonalFinanceTracker.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/budgets")]
public class BudgetsController(AppDbContext dbContext, IAccountAccessService accountAccessService) : ControllerBase
{
    private async Task<bool> CanEditBudgetAsync(Budget budget, Guid userId, CancellationToken cancellationToken)
    {
        if (budget.AccountId.HasValue)
        {
            return await accountAccessService.CanEditAccountAsync(userId, budget.AccountId.Value, cancellationToken);
        }

        return budget.UserId == userId;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<object>>> Get([FromQuery] int month, [FromQuery] int year, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        var accessibleAccountIds = await accountAccessService.GetAccessibleAccountIdsAsync(userId, cancellationToken);
        var budgets = await dbContext.Budgets
            .Include(x => x.Category)
            .Include(x => x.Account)
            .Where(x =>
                x.Month == month &&
                x.Year == year &&
                (
                    (x.AccountId == null && x.UserId == userId) ||
                    (x.AccountId != null && accessibleAccountIds.Contains(x.AccountId.Value))
                ))
            .ToListAsync(cancellationToken);

        var spentByCategory = await dbContext.Transactions
            .Where(x => accessibleAccountIds.Contains(x.AccountId) && x.Type == "expense" && x.TransactionDate.Month == month && x.TransactionDate.Year == year && x.CategoryId != null)
            .GroupBy(x => new { x.CategoryId!.Value, x.AccountId })
            .Select(group => new { group.Key.Value, group.Key.AccountId, Spent = group.Sum(x => x.Amount) })
            .ToListAsync(cancellationToken);

        var result = budgets.Select(budget => new
        {
            budget.Id,
            budget.Month,
            budget.Year,
            budget.Amount,
            budget.AlertThresholdPercent,
            budget.AccountId,
            account = budget.Account != null ? new
            {
                budget.Account.Id,
                budget.Account.Name,
                budget.Account.Type
            } : null,
            category = budget.Category,
            spent = spentByCategory
                .Where(x => x.Value == budget.CategoryId && x.AccountId == (budget.AccountId ?? x.AccountId))
                .Sum(x => x.Spent),
            progress = budget.Amount > 0
                ? decimal.Round(
                    spentByCategory
                        .Where(x => x.Value == budget.CategoryId && x.AccountId == (budget.AccountId ?? x.AccountId))
                        .Sum(x => x.Spent) / budget.Amount * 100,
                    2)
                : 0
        });

        return Ok(new ApiResponse<object>(true, result));
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<object>>> Create(BudgetRequest request, CancellationToken cancellationToken)
    {
        if (request.AccountId.HasValue && !await accountAccessService.CanEditAccountAsync(User.GetUserId(), request.AccountId.Value, cancellationToken))
        {
            return Forbid();
        }

        var budget = new Budget
        {
            UserId = User.GetUserId(),
            AccountId = request.AccountId,
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
        var userId = User.GetUserId();
        var accessibleAccountIds = await accountAccessService.GetAccessibleAccountIdsAsync(userId, cancellationToken);
        var budget = await dbContext.Budgets.SingleOrDefaultAsync(
            x => x.Id == id && ((x.AccountId == null && x.UserId == userId) || (x.AccountId != null && accessibleAccountIds.Contains(x.AccountId.Value))),
            cancellationToken);
        if (budget is null) return NotFound();
        if (!await CanEditBudgetAsync(budget, userId, cancellationToken)) return Forbid();

        if (request.AccountId.HasValue && !await accountAccessService.CanEditAccountAsync(userId, request.AccountId.Value, cancellationToken))
        {
            return Forbid();
        }

        budget.AccountId = request.AccountId;
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
        var userId = User.GetUserId();
        var accessibleAccountIds = await accountAccessService.GetAccessibleAccountIdsAsync(userId, cancellationToken);
        var budget = await dbContext.Budgets.SingleOrDefaultAsync(
            x => x.Id == id && ((x.AccountId == null && x.UserId == userId) || (x.AccountId != null && accessibleAccountIds.Contains(x.AccountId.Value))),
            cancellationToken);
        if (budget is null) return NotFound();
        if (!await CanEditBudgetAsync(budget, userId, cancellationToken)) return Forbid();
        dbContext.Budgets.Remove(budget);
        await dbContext.SaveChangesAsync(cancellationToken);
        return NoContent();
    }
}
