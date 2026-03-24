namespace PersonalFinanceTracker.Api.DTOs.Auth;

public record RegisterRequest(string Email, string Password, string DisplayName, string PreferredCurrency);
public record LoginRequest(string Email, string Password);
public record RefreshRequest(string RefreshToken);
public record ForgotPasswordRequest(string Email);
public record ResetPasswordRequest(string Email, string NewPassword, string Token);
public record AuthResponse(string AccessToken, string RefreshToken, DateTime ExpiresAt, string DisplayName, string Email, string? AvatarUrl, string PreferredCurrency);
