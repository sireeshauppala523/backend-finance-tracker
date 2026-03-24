using PersonalFinanceTracker.Api.DTOs.Forecast;

namespace PersonalFinanceTracker.Api.Services.Interfaces;

public interface IForecastService
{
    Task<ForecastMonthResponse> GetMonthForecastAsync(Guid userId, CancellationToken cancellationToken);
    Task<IReadOnlyList<ForecastDailyPoint>> GetDailyForecastAsync(Guid userId, CancellationToken cancellationToken);
}
