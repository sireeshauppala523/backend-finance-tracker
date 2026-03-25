using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PersonalFinanceTracker.Api.Common;
using PersonalFinanceTracker.Api.Data;
using PersonalFinanceTracker.Api.DTOs.Rules;
using PersonalFinanceTracker.Api.Entities;
using PersonalFinanceTracker.Api.Extensions;

namespace PersonalFinanceTracker.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/rules")]
public class RulesController(AppDbContext dbContext) : ControllerBase
{
    private static readonly HashSet<string> AllowedFields = ["merchant", "amount", "category"];
    private static readonly HashSet<string> AllowedOperators = ["equals", "contains", "greaterThan", "lessThan"];
    private static readonly HashSet<string> AllowedActionTypes = ["categorize", "tag", "alert"];

    private static string? ValidateRequest(RuleRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name)) return "Rule name is required.";
        if (string.IsNullOrWhiteSpace(request.Condition.Field) || !AllowedFields.Contains(request.Condition.Field.Trim())) return "Condition field is invalid.";
        if (string.IsNullOrWhiteSpace(request.Condition.Operator) || !AllowedOperators.Contains(request.Condition.Operator.Trim())) return "Condition operator is invalid.";
        if (string.IsNullOrWhiteSpace(request.Condition.Value)) return "Condition value is required.";
        if (string.IsNullOrWhiteSpace(request.Action.Type) || !AllowedActionTypes.Contains(request.Action.Type.Trim())) return "Action type is invalid.";
        if (string.IsNullOrWhiteSpace(request.Action.Value)) return "Action value is required.";
        return null;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<RuleResponse>>>> Get(CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        var rules = await dbContext.Rules
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new RuleResponse(
                x.Id,
                x.Name,
                new { field = x.ConditionField, @operator = x.ConditionOperator, value = x.ConditionValue },
                new { type = x.ActionType, value = x.ActionValue },
                x.IsActive,
                x.CreatedAt,
                x.UpdatedAt))
            .ToListAsync(cancellationToken);

        return Ok(new ApiResponse<IReadOnlyList<RuleResponse>>(true, rules));
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<RuleResponse>>> Create(RuleRequest request, CancellationToken cancellationToken)
    {
        var validationError = ValidateRequest(request);
        if (validationError is not null)
        {
            return BadRequest(new ApiResponse<string>(false, string.Empty, validationError));
        }

        var rule = new Rule
        {
            UserId = User.GetUserId(),
            Name = request.Name.Trim(),
            ConditionField = request.Condition.Field.Trim(),
            ConditionOperator = request.Condition.Operator.Trim(),
            ConditionValue = request.Condition.Value.Trim(),
            ActionType = request.Action.Type.Trim(),
            ActionValue = request.Action.Value.Trim(),
            IsActive = request.IsActive,
        };

        dbContext.Rules.Add(rule);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(new ApiResponse<RuleResponse>(true, ToResponse(rule)));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ApiResponse<RuleResponse>>> Update(Guid id, RuleRequest request, CancellationToken cancellationToken)
    {
        var validationError = ValidateRequest(request);
        if (validationError is not null)
        {
            return BadRequest(new ApiResponse<string>(false, string.Empty, validationError));
        }

        var rule = await dbContext.Rules.SingleOrDefaultAsync(x => x.Id == id && x.UserId == User.GetUserId(), cancellationToken);
        if (rule is null) return NotFound();

        rule.Name = request.Name.Trim();
        rule.ConditionField = request.Condition.Field.Trim();
        rule.ConditionOperator = request.Condition.Operator.Trim();
        rule.ConditionValue = request.Condition.Value.Trim();
        rule.ActionType = request.Action.Type.Trim();
        rule.ActionValue = request.Action.Value.Trim();
        rule.IsActive = request.IsActive;
        rule.UpdatedAt = DateTime.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(new ApiResponse<RuleResponse>(true, ToResponse(rule)));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var rule = await dbContext.Rules.SingleOrDefaultAsync(x => x.Id == id && x.UserId == User.GetUserId(), cancellationToken);
        if (rule is null) return NotFound();

        dbContext.Rules.Remove(rule);
        await dbContext.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    private static RuleResponse ToResponse(Rule rule)
        => new(
            rule.Id,
            rule.Name,
            new { field = rule.ConditionField, @operator = rule.ConditionOperator, value = rule.ConditionValue },
            new { type = rule.ActionType, value = rule.ActionValue },
            rule.IsActive,
            rule.CreatedAt,
            rule.UpdatedAt);
}
