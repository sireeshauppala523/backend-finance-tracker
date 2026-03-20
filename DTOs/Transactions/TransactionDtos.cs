namespace PersonalFinanceTracker.Api.DTOs.Transactions;

public record TransactionRequest(
    Guid AccountId,
    Guid? CategoryId,
    string Type,
    decimal Amount,
    DateOnly Date,
    string? Merchant,
    string? Note,
    string? PaymentMethod,
    string[]? Tags,
    Guid? RecurringTransactionId);