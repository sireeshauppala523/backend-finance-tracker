namespace PersonalFinanceTracker.Api.DTOs.Profile;

public record ProfileResponse(string DisplayName, string Email, string? AvatarUrl, string PreferredCurrency);
public record UpdateProfileRequest(string DisplayName, string? AvatarUrl, string PreferredCurrency);
