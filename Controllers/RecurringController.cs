using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PersonalFinanceTracker.Api.Common;
using PersonalFinanceTracker.Api.Data;
using PersonalFinanceTracker.Api.DTOs.Recurring;
using PersonalFinanceTracker.Api.Entities;
using PersonalFinanceTracker.Api.Extensions;
using PersonalFinanceTracker.Api.Services.Interfaces;

namespace PersonalFinanceTracker.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/recurring")]
public class RecurringController(AppDbContext dbContext, IAccountAccessService accountAccessService) : ControllerBase
{
    private async Task<bool> CanEditRecurringAsync(RecurringTransaction item, Guid userId, CancellationToken cancellationToken)
    {
        if (item.AccountId.HasValue)
        {
            return await accountAccessService.CanEditAccountAsync(userId, item.AccountId.Value, cancellationToken);
        }

        return item.UserId == userId;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<object>>> Get(CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        var accessibleAccountIds = await accountAccessService.GetAccessibleAccountIdsAsync(userId, cancellationToken);
        var items = await dbContext.RecurringTransactions
            .Where(x => (x.AccountId == null && x.UserId == userId) || (x.AccountId != null && accessibleAccountIds.Contains(x.AccountId.Value)))
            .ToListAsync(cancellationToken);
        return Ok(new ApiResponse<object>(true, items));
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<object>>> Create(RecurringRequest request, CancellationToken cancellationToken)
    {
        if (request.AccountId.HasValue && !await accountAccessService.CanEditAccountAsync(User.GetUserId(), request.AccountId.Value, cancellationToken))
        {
            return Forbid();
        }

        var item = new RecurringTransaction
        {
            UserId = User.GetUserId(),
            Title = request.Title,
            Type = request.Type,
            Amount = request.Amount,
            CategoryId = request.CategoryId,
            AccountId = request.AccountId,
            Frequency = request.Frequency,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            NextRunDate = request.NextRunDate,
            AutoCreateTransaction = request.AutoCreateTransaction,
            IsPaused = request.IsPaused
        };

        dbContext.RecurringTransactions.Add(item);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(new ApiResponse<object>(true, item));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ApiResponse<object>>> Update(Guid id, RecurringRequest request, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        var accessibleAccountIds = await accountAccessService.GetAccessibleAccountIdsAsync(userId, cancellationToken);
        var item = await dbContext.RecurringTransactions.SingleOrDefaultAsync(
            x => x.Id == id && ((x.AccountId == null && x.UserId == userId) || (x.AccountId != null && accessibleAccountIds.Contains(x.AccountId.Value))),
            cancellationToken);
        if (item is null) return NotFound();
        if (!await CanEditRecurringAsync(item, userId, cancellationToken)) return Forbid();

        if (request.AccountId.HasValue && !await accountAccessService.CanEditAccountAsync(userId, request.AccountId.Value, cancellationToken))
        {
            return Forbid();
        }

        item.Title = request.Title;
        item.Type = request.Type;
        item.Amount = request.Amount;
        item.CategoryId = request.CategoryId;
        item.AccountId = request.AccountId;
        item.Frequency = request.Frequency;
        item.StartDate = request.StartDate;
        item.EndDate = request.EndDate;
        item.NextRunDate = request.NextRunDate;
        item.AutoCreateTransaction = request.AutoCreateTransaction;
        item.IsPaused = request.IsPaused;
        await dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new ApiResponse<object>(true, item));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        var accessibleAccountIds = await accountAccessService.GetAccessibleAccountIdsAsync(userId, cancellationToken);
        var item = await dbContext.RecurringTransactions.SingleOrDefaultAsync(
            x => x.Id == id && ((x.AccountId == null && x.UserId == userId) || (x.AccountId != null && accessibleAccountIds.Contains(x.AccountId.Value))),
            cancellationToken);
        if (item is null) return NotFound();
        if (!await CanEditRecurringAsync(item, userId, cancellationToken)) return Forbid();
        dbContext.RecurringTransactions.Remove(item);
        await dbContext.SaveChangesAsync(cancellationToken);
        return NoContent();
    }
}
