namespace PersonalFinanceTracker.Api.Services.Interfaces;

public interface IReportService
{
    Task<object> GetCategorySpendAsync(Guid userId, DateOnly? from, DateOnly? to, CancellationToken cancellationToken);
    Task<object> GetIncomeVsExpenseAsync(Guid userId, DateOnly? from, DateOnly? to, CancellationToken cancellationToken);
    Task<object> GetAccountBalanceTrendAsync(Guid userId, CancellationToken cancellationToken);
}