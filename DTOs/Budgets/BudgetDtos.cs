namespace PersonalFinanceTracker.Api.DTOs.Budgets;

public record BudgetRequest(Guid CategoryId, int Month, int Year, decimal Amount, int AlertThresholdPercent);