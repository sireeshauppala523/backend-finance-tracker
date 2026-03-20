using Microsoft.EntityFrameworkCore;
using PersonalFinanceTracker.Api.Data;
using PersonalFinanceTracker.Api.Services.Interfaces;

namespace PersonalFinanceTracker.Api.Services.Implementations;

public class ReportService(AppDbContext dbContext) : IReportService
{
    public async Task<object> GetCategorySpendAsync(Guid userId, DateOnly? from, DateOnly? to, CancellationToken cancellationToken)
    {
        var query = dbContext.Transactions.Where(x => x.UserId == userId && x.Type == "expense");

        if (from.HasValue) query = query.Where(x => x.TransactionDate >= from.Value);
        if (to.HasValue) query = query.Where(x => x.TransactionDate <= to.Value);

        return await query
            .Include(x => x.Category)
            .GroupBy(x => x.Category != null ? x.Category.Name : "Uncategorized")
            .Select(group => new { category = group.Key, amount = group.Sum(x => x.Amount) })
            .OrderByDescending(x => x.amount)
            .ToListAsync(cancellationToken);
    }

    public async Task<object> GetIncomeVsExpenseAsync(Guid userId, DateOnly? from, DateOnly? to, CancellationToken cancellationToken)
    {
        var query = dbContext.Transactions.Where(x => x.UserId == userId);

        if (from.HasValue) query = query.Where(x => x.TransactionDate >= from.Value);
        if (to.HasValue) query = query.Where(x => x.TransactionDate <= to.Value);

        return await query
            .GroupBy(x => new { x.TransactionDate.Year, x.TransactionDate.Month, x.Type })
            .Select(group => new
            {
                group.Key.Year,
                group.Key.Month,
                group.Key.Type,
                amount = group.Sum(x => x.Amount)
            })
            .OrderBy(x => x.Year)
            .ThenBy(x => x.Month)
            .ToListAsync(cancellationToken);
    }

    public async Task<object> GetAccountBalanceTrendAsync(Guid userId, CancellationToken cancellationToken)
    {
        return await dbContext.Accounts
            .Where(x => x.UserId == userId)
            .Select(x => new { x.Name, x.CurrentBalance, x.LastUpdatedAt })
            .OrderByDescending(x => x.LastUpdatedAt)
            .ToListAsync(cancellationToken);
    }
}