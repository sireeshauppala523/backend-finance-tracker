using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PersonalFinanceTracker.Api.Common;
using PersonalFinanceTracker.Api.Data;
using PersonalFinanceTracker.Api.DTOs.Recurring;
using PersonalFinanceTracker.Api.Entities;
using PersonalFinanceTracker.Api.Extensions;

namespace PersonalFinanceTracker.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/recurring")]
public class RecurringController(AppDbContext dbContext) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ApiResponse<object>>> Get(CancellationToken cancellationToken)
    {
        var items = await dbContext.RecurringTransactions.Where(x => x.UserId == User.GetUserId()).ToListAsync(cancellationToken);
        return Ok(new ApiResponse<object>(true, items));
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<object>>> Create(RecurringRequest request, CancellationToken cancellationToken)
    {
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
        var item = await dbContext.RecurringTransactions.SingleOrDefaultAsync(x => x.Id == id && x.UserId == User.GetUserId(), cancellationToken);
        if (item is null) return NotFound();

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
        var item = await dbContext.RecurringTransactions.SingleOrDefaultAsync(x => x.Id == id && x.UserId == User.GetUserId(), cancellationToken);
        if (item is null) return NotFound();
        dbContext.RecurringTransactions.Remove(item);
        await dbContext.SaveChangesAsync(cancellationToken);
        return NoContent();
    }
}