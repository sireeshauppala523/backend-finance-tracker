namespace PersonalFinanceTracker.Api.DTOs.Goals;

public record GoalRequest(string Name, decimal TargetAmount, decimal CurrentAmount, DateOnly? TargetDate, Guid? LinkedAccountId, string Icon, string Color, string Status);
public record GoalContributionRequest(decimal Amount, Guid? SourceAccountId);