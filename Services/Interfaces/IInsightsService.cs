using PersonalFinanceTracker.Api.DTOs.Insights;

namespace PersonalFinanceTracker.Api.Services.Interfaces;

public interface IInsightsService
{
    Task<FinancialHealthScoreResponse> GetHealthScoreAsync(Guid userId, CancellationToken cancellationToken);
}
