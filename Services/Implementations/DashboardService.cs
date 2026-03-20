using Microsoft.EntityFrameworkCore;
using PersonalFinanceTracker.Api.Data;
using PersonalFinanceTracker.Api.Services.Interfaces;

namespace PersonalFinanceTracker.Api.Services.Implementations;

public class DashboardService(AppDbContext dbContext) : IDashboardService
{
    public async Task<object> GetSummaryAsync(Guid userId, CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var firstDay = new DateOnly(today.Year, today.Month, 1);

        var monthTransactions = await dbContext.Transactions
            .Where(x => x.UserId == userId && x.TransactionDate >= firstDay)
            .Include(x => x.Category)
            .OrderByDescending(x => x.TransactionDate)
            .ToListAsync(cancellationToken);

        var income = monthTransactions.Where(x => x.Type == "income").Sum(x => x.Amount);
        var expense = monthTransactions.Where(x => x.Type == "expense").Sum(x => x.Amount);
        var recentTransactions = monthTransactions.Take(5).ToList();

        var upcoming = await dbContext.RecurringTransactions
            .Where(x => x.UserId == userId && !x.IsPaused)
            .OrderBy(x => x.NextRunDate)
            .Take(5)
            .ToListAsync(cancellationToken);

        var goals = await dbContext.Goals
            .Where(x => x.UserId == userId)
            .Take(4)
            .ToListAsync(cancellationToken);

        return new
        {
            income,
            expense,
            netBalance = income - expense,
            recentTransactions = recentTransactions.Select(x => new
            {
                x.Id,
                x.Merchant,
                x.Amount,
                x.Type,
                x.TransactionDate,
                category = x.Category != null ? x.Category.Name : null
            }),
            upcomingRecurringPayments = upcoming,
            goals
        };
    }
}