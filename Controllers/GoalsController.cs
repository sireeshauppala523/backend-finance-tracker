using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PersonalFinanceTracker.Api.Common;
using PersonalFinanceTracker.Api.Data;
using PersonalFinanceTracker.Api.DTOs.Goals;
using PersonalFinanceTracker.Api.Entities;
using PersonalFinanceTracker.Api.Extensions;

namespace PersonalFinanceTracker.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/goals")]
public class GoalsController(AppDbContext dbContext) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ApiResponse<object>>> Get(CancellationToken cancellationToken)
    {
        var goals = await dbContext.Goals.Where(x => x.UserId == User.GetUserId()).ToListAsync(cancellationToken);
        return Ok(new ApiResponse<object>(true, goals));
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<object>>> Create(GoalRequest request, CancellationToken cancellationToken)
    {
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
        var goal = await dbContext.Goals.SingleOrDefaultAsync(x => x.Id == id && x.UserId == User.GetUserId(), cancellationToken);
        if (goal is null) return NotFound();

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
        var goal = await dbContext.Goals.SingleOrDefaultAsync(x => x.Id == id && x.UserId == User.GetUserId(), cancellationToken);
        if (goal is null) return NotFound();

        dbContext.Goals.Remove(goal);
        await dbContext.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:guid}/contribute")]
    public async Task<ActionResult<ApiResponse<object>>> Contribute(Guid id, GoalContributionRequest request, CancellationToken cancellationToken)
    {
        var goal = await dbContext.Goals.SingleOrDefaultAsync(x => x.Id == id && x.UserId == User.GetUserId(), cancellationToken);
        if (goal is null) return NotFound();

        goal.CurrentAmount += request.Amount;
        if (goal.CurrentAmount >= goal.TargetAmount) goal.Status = "completed";
        await dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new ApiResponse<object>(true, goal));
    }

    [HttpPost("{id:guid}/withdraw")]
    public async Task<ActionResult<ApiResponse<object>>> Withdraw(Guid id, GoalContributionRequest request, CancellationToken cancellationToken)
    {
        var goal = await dbContext.Goals.SingleOrDefaultAsync(x => x.Id == id && x.UserId == User.GetUserId(), cancellationToken);
        if (goal is null) return NotFound();

        goal.CurrentAmount = Math.Max(0, goal.CurrentAmount - request.Amount);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new ApiResponse<object>(true, goal));
    }
}