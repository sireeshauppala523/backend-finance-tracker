namespace PersonalFinanceTracker.Api.Services.Interfaces;

public interface IDashboardService
{
    Task<object> GetSummaryAsync(Guid userId, CancellationToken cancellationToken);
}