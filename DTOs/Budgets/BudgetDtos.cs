namespace PersonalFinanceTracker.Api.DTOs.Budgets;

public record BudgetRequest(Guid CategoryId, Guid? AccountId, int Month, int Year, decimal Amount, int AlertThresholdPercent);
