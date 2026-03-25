using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PersonalFinanceTracker.Api.Common;
using PersonalFinanceTracker.Api.Data;
using PersonalFinanceTracker.Api.DTOs.Goals;
using PersonalFinanceTracker.Api.Entities;
using PersonalFinanceTracker.Api.Extensions;
using PersonalFinanceTracker.Api.Services.Interfaces;

namespace PersonalFinanceTracker.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/goals")]
public class GoalsController(AppDbContext dbContext, IAccountAccessService accountAccessService) : ControllerBase
{
    private async Task<bool> CanEditGoalAsync(Goal goal, Guid userId, CancellationToken cancellationToken)
    {
        if (goal.LinkedAccountId.HasValue)
        {
            return await accountAccessService.CanEditAccountAsync(userId, goal.LinkedAccountId.Value, cancellationToken);
        }

        return goal.UserId == userId;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<object>>> Get(CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        var accessibleAccountIds = await accountAccessService.GetAccessibleAccountIdsAsync(userId, cancellationToken);
        var goals = await dbContext.Goals
            .Where(x => (x.LinkedAccountId == null && x.UserId == userId) || (x.LinkedAccountId != null && accessibleAccountIds.Contains(x.LinkedAccountId.Value)))
            .ToListAsync(cancellationToken);
        return Ok(new ApiResponse<object>(true, goals));
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<object>>> Create(GoalRequest request, CancellationToken cancellationToken)
    {
        if (request.LinkedAccountId.HasValue && !await accountAccessService.CanEditAccountAsync(User.GetUserId(), request.LinkedAccountId.Value, cancellationToken))
        {
            return Forbid();
        }

        var goal = new Goal
        {
            UserId = User.GetUserId(),
            Name = request.Name,
            TargetAmount = request.TargetAmount,
            CurrentAmount = request.CurrentAmount,
            TargetDate = request.TargetDate,
            LinkedAccountId = request.LinkedAccountId,
            Icon = request.Icon,
            Color = request.Color,
            Status = request.Status
        };

        dbContext.Goals.Add(goal);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(new ApiResponse<object>(true, goal));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ApiResponse<object>>> Update(Guid id, GoalRequest request, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        var accessibleAccountIds = await accountAccessService.GetAccessibleAccountIdsAsync(userId, cancellationToken);
        var goal = await dbContext.Goals.SingleOrDefaultAsync(
            x => x.Id == id && ((x.LinkedAccountId == null && x.UserId == userId) || (x.LinkedAccountId != null && accessibleAccountIds.Contains(x.LinkedAccountId.Value))),
            cancellationToken);
        if (goal is null) return NotFound();
        if (!await CanEditGoalAsync(goal, userId, cancellationToken)) return Forbid();

        if (request.LinkedAccountId.HasValue && !await accountAccessService.CanEditAccountAsync(userId, request.LinkedAccountId.Value, cancellationToken))
        {
            return Forbid();
        }

        goal.Name = request.Name;
        goal.TargetAmount = request.TargetAmount;
        goal.CurrentAmount = request.CurrentAmount;
        goal.TargetDate = request.TargetDate;
        goal.LinkedAccountId = request.LinkedAccountId;
        goal.Icon = request.Icon;
        goal.Color = request.Color;
        goal.Status = request.Status;
        await dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new ApiResponse<object>(true, goal));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        var accessibleAccountIds = await accountAccessService.GetAccessibleAccountIdsAsync(userId, cancellationToken);
        var goal = await dbContext.Goals.SingleOrDefaultAsync(
            x => x.Id == id && ((x.LinkedAccountId == null && x.UserId == userId) || (x.LinkedAccountId != null && accessibleAccountIds.Contains(x.LinkedAccountId.Value))),
            cancellationToken);
        if (goal is null) return NotFound();
        if (!await CanEditGoalAsync(goal, userId, cancellationToken)) return Forbid();

        dbContext.Goals.Remove(goal);
        await dbContext.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:guid}/contribute")]
    public async Task<ActionResult<ApiResponse<object>>> Contribute(Guid id, GoalContributionRequest request, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        var accessibleAccountIds = await accountAccessService.GetAccessibleAccountIdsAsync(userId, cancellationToken);
        var goal = await dbContext.Goals.SingleOrDefaultAsync(
            x => x.Id == id && ((x.LinkedAccountId == null && x.UserId == userId) || (x.LinkedAccountId != null && accessibleAccountIds.Contains(x.LinkedAccountId.Value))),
            cancellationToken);
        if (goal is null) return NotFound();
        if (!await CanEditGoalAsync(goal, userId, cancellationToken)) return Forbid();

        if (request.SourceAccountId.HasValue)
        {
            if (!await accountAccessService.CanEditAccountAsync(userId, request.SourceAccountId.Value, cancellationToken))
            {
                return Forbid();
            }

            var sourceAccount = await dbContext.Accounts.SingleOrDefaultAsync(x => x.Id == request.SourceAccountId.Value, cancellationToken);
            if (sourceAccount is null) return BadRequest(new ApiResponse<string>(false, string.Empty, "Invalid source account."));
            if (sourceAccount.CurrentBalance < request.Amount) return BadRequest(new ApiResponse<string>(false, string.Empty, "Insufficient funds."));

            sourceAccount.CurrentBalance -= request.Amount;
            sourceAccount.LastUpdatedAt = DateTime.UtcNow;
            dbContext.Transactions.Add(new Transaction
            {
                UserId = goal.UserId,
                AccountId = sourceAccount.Id,
                Type = "expense",
                Amount = request.Amount,
                TransactionDate = DateOnly.FromDateTime(DateTime.UtcNow),
                Merchant = $"Goal contribution: {goal.Name}",
                Note = "Contribution moved into goal",
            });
        }

        goal.CurrentAmount += request.Amount;
        if (goal.CurrentAmount >= goal.TargetAmount) goal.Status = "completed";
        await dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new ApiResponse<object>(true, goal));
    }

    [HttpPost("{id:guid}/withdraw")]
    public async Task<ActionResult<ApiResponse<object>>> Withdraw(Guid id, GoalContributionRequest request, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        var accessibleAccountIds = await accountAccessService.GetAccessibleAccountIdsAsync(userId, cancellationToken);
        var goal = await dbContext.Goals.SingleOrDefaultAsync(
            x => x.Id == id && ((x.LinkedAccountId == null && x.UserId == userId) || (x.LinkedAccountId != null && accessibleAccountIds.Contains(x.LinkedAccountId.Value))),
            cancellationToken);
        if (goal is null) return NotFound();
        if (!await CanEditGoalAsync(goal, userId, cancellationToken)) return Forbid();

        goal.CurrentAmount = Math.Max(0, goal.CurrentAmount - request.Amount);
        if (request.SourceAccountId.HasValue)
        {
            if (!await accountAccessService.CanEditAccountAsync(userId, request.SourceAccountId.Value, cancellationToken))
            {
                return Forbid();
            }

            var destinationAccount = await dbContext.Accounts.SingleOrDefaultAsync(x => x.Id == request.SourceAccountId.Value, cancellationToken);
            if (destinationAccount is null) return BadRequest(new ApiResponse<string>(false, string.Empty, "Invalid destination account."));

            destinationAccount.CurrentBalance += request.Amount;
            destinationAccount.LastUpdatedAt = DateTime.UtcNow;
            dbContext.Transactions.Add(new Transaction
            {
                UserId = goal.UserId,
                AccountId = destinationAccount.Id,
                Type = "income",
                Amount = request.Amount,
                TransactionDate = DateOnly.FromDateTime(DateTime.UtcNow),
                Merchant = $"Goal withdrawal: {goal.Name}",
                Note = "Funds moved out of goal",
            });
        }
        await dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new ApiResponse<object>(true, goal));
    }
}
