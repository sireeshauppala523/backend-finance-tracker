namespace PersonalFinanceTracker.Api.Common;

public record ApiResponse<T>(bool Success, T Data, string? Message = null);