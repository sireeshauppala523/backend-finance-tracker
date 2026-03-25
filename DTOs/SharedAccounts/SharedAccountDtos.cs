namespace PersonalFinanceTracker.Api.DTOs.SharedAccounts;

public record SharedAccountMemberRequest(Guid AccountId, string Email, string Role);

public record SharedAccountMemberResponse(
    Guid Id,
    Guid AccountId,
    Guid UserId,
    string DisplayName,
    string Email,
    string Role,
    DateTime CreatedAt);
