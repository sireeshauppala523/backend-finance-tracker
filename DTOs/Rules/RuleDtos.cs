namespace PersonalFinanceTracker.Api.DTOs.Rules;

public record RuleConditionRequest(string Field, string Operator, string Value);
public record RuleActionRequest(string Type, string Value);
public record RuleRequest(string Name, RuleConditionRequest Condition, RuleActionRequest Action, string? AppliesToType, bool IsActive);

public record RuleResponse(
    Guid Id,
    string Name,
    object Condition,
    object Action,
    string AppliesToType,
    bool IsActive,
    DateTime CreatedAt,
    DateTime UpdatedAt);
