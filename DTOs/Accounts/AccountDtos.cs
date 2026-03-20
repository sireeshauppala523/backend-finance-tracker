namespace PersonalFinanceTracker.Api.DTOs.Accounts;

public record AccountRequest(string Name, string Type, decimal OpeningBalance, string? InstitutionName);
public record TransferRequest(Guid SourceAccountId, Guid DestinationAccountId, decimal Amount, DateOnly Date, string? Note);