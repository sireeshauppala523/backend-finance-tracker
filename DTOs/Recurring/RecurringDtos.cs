namespace PersonalFinanceTracker.Api.DTOs.Recurring;

public record RecurringRequest(string Title, string Type, decimal Amount, Guid? CategoryId, Guid? AccountId, string Frequency, DateOnly StartDate, DateOnly? EndDate, DateOnly NextRunDate, bool AutoCreateTransaction, bool IsPaused);