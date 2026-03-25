using Microsoft.EntityFrameworkCore;
using PersonalFinanceTracker.Api.Data;
using PersonalFinanceTracker.Api.DTOs.Forecast;
using PersonalFinanceTracker.Api.Entities;
using PersonalFinanceTracker.Api.Services.Interfaces;

namespace PersonalFinanceTracker.Api.Services.Implementations;

public class ForecastService(AppDbContext dbContext, IAccountAccessService accountAccessService) : IForecastService
{
    public async Task<ForecastMonthResponse> GetMonthForecastAsync(Guid userId, CancellationToken cancellationToken)
    {
        var snapshot = await BuildSnapshotAsync(userId, cancellationToken);

        return new ForecastMonthResponse(
            snapshot.CurrentBalance,
            snapshot.ForecastedEndBalance,
            snapshot.ProjectedIncome,
            snapshot.ProjectedExpense,
            snapshot.SafeToSpendPerDay,
            snapshot.DaysRemaining,
            snapshot.Warnings,
            snapshot.UpcomingKnownExpenses);
    }

    public async Task<IReadOnlyList<ForecastDailyPoint>> GetDailyForecastAsync(Guid userId, CancellationToken cancellationToken)
    {
        var snapshot = await BuildSnapshotAsync(userId, cancellationToken);
        return snapshot.DailyPoints;
    }

    private async Task<ForecastSnapshot> BuildSnapshotAsync(Guid userId, CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow.Date);
        var monthEnd = new DateOnly(today.Year, today.Month, DateTime.DaysInMonth(today.Year, today.Month));
        var firstDay = new DateOnly(today.Year, today.Month, 1);
        var sampleStart = today.AddMonths(-3);
        var daysRemaining = Math.Max(monthEnd.DayNumber - today.DayNumber, 0);
        var accessibleAccountIds = await accountAccessService.GetAccessibleAccountIdsAsync(userId, cancellationToken);

        var currentBalance = await dbContext.Accounts
            .Where(x => accessibleAccountIds.Contains(x.Id))
            .SumAsync(x => x.CurrentBalance, cancellationToken);

        var historicalTransactions = await dbContext.Transactions
            .Where(x => accessibleAccountIds.Contains(x.AccountId) && x.TransactionDate >= sampleStart && x.TransactionDate <= today)
            .ToListAsync(cancellationToken);

        var monthTransactions = historicalTransactions
            .Where(x => x.TransactionDate >= firstDay)
            .ToList();

        var activeRecurring = await dbContext.RecurringTransactions
            .Where(x => x.UserId == userId && !x.IsPaused && x.NextRunDate <= monthEnd && (x.EndDate == null || x.EndDate >= today))
            .OrderBy(x => x.NextRunDate)
            .ToListAsync(cancellationToken);

        var recurringSchedule = ExpandRecurringItems(activeRecurring, today, monthEnd);
        var nonRecurringSample = historicalTransactions.Where(x => x.RecurringTransactionId == null).ToList();
        var sampleDays = Math.Max((today.DayNumber - sampleStart.DayNumber) + 1, 1);

        var averageDailyIncome = nonRecurringSample
            .Where(x => x.Type == "income")
            .Sum(x => x.Amount) / sampleDays;

        var averageDailyExpense = nonRecurringSample
            .Where(x => x.Type == "expense")
            .Sum(x => x.Amount) / sampleDays;

        if (nonRecurringSample.Count == 0)
        {
            averageDailyIncome = 0;
            averageDailyExpense = 0;
        }

        var dailyPoints = new List<ForecastDailyPoint>();
        var runningBalance = currentBalance;

        for (var date = today; date <= monthEnd; date = date.AddDays(1))
        {
            decimal projectedIncome = 0;
            decimal projectedExpense = 0;

            if (date > today)
            {
                projectedIncome += averageDailyIncome;
                projectedExpense += averageDailyExpense;
            }

            var dueRecurring = recurringSchedule.Where(x => x.Date == date).ToList();
            projectedIncome += dueRecurring.Where(x => x.Item.Type == "income").Sum(x => x.Item.Amount);
            projectedExpense += dueRecurring.Where(x => x.Item.Type == "expense").Sum(x => x.Item.Amount);

            if (date > today)
            {
                runningBalance += projectedIncome - projectedExpense;
            }

            dailyPoints.Add(new ForecastDailyPoint(
                date,
                decimal.Round(runningBalance, 2),
                decimal.Round(projectedIncome, 2),
                decimal.Round(projectedExpense, 2)));
        }

        var projectedRecurringIncome = recurringSchedule.Where(x => x.Item.Type == "income").Sum(x => x.Item.Amount);
        var projectedRecurringExpense = recurringSchedule.Where(x => x.Item.Type == "expense").Sum(x => x.Item.Amount);
        var projectedPatternIncome = decimal.Round(averageDailyIncome * daysRemaining, 2);
        var projectedPatternExpense = decimal.Round(averageDailyExpense * daysRemaining, 2);
        var forecastedEndBalance = dailyPoints.LastOrDefault()?.Balance ?? currentBalance;

        var upcomingKnownExpenses = recurringSchedule
            .Where(x => x.Item.Type == "expense")
            .OrderBy(x => x.Date)
            .Select(x => new ForecastUpcomingExpenseItem(
                x.Item.Title,
                decimal.Round(x.Item.Amount, 2),
                x.Date,
                "recurring"))
            .ToList();

        var warnings = BuildWarnings(dailyPoints, forecastedEndBalance, averageDailyExpense);
        var recommendedBuffer = decimal.Round(Math.Max(averageDailyExpense * 3, 0), 2);
        var safeToSpendPerDay = daysRemaining > 0
            ? decimal.Round(Math.Max(forecastedEndBalance - recommendedBuffer, 0) / daysRemaining, 2)
            : 0;

        return new ForecastSnapshot(
            decimal.Round(currentBalance, 2),
            decimal.Round(forecastedEndBalance, 2),
            decimal.Round(projectedRecurringIncome + projectedPatternIncome, 2),
            decimal.Round(projectedRecurringExpense + projectedPatternExpense, 2),
            safeToSpendPerDay,
            daysRemaining,
            warnings,
            upcomingKnownExpenses,
            dailyPoints);
    }

    private static IReadOnlyList<(RecurringTransaction Item, DateOnly Date)> ExpandRecurringItems(
        IEnumerable<RecurringTransaction> items,
        DateOnly today,
        DateOnly monthEnd)
    {
        var schedule = new List<(RecurringTransaction Item, DateOnly Date)>();

        foreach (var item in items)
        {
            var next = item.NextRunDate < today ? today : item.NextRunDate;
            while (next <= monthEnd && (item.EndDate == null || next <= item.EndDate.Value))
            {
                if (next >= today)
                {
                    schedule.Add((item, next));
                }

                next = item.Frequency.ToLowerInvariant() switch
                {
                    "daily" => next.AddDays(1),
                    "weekly" => next.AddDays(7),
                    "quarterly" => next.AddMonths(3),
                    "yearly" => next.AddYears(1),
                    _ => next.AddMonths(1)
                };
            }
        }

        return schedule;
    }

    private static List<string> BuildWarnings(IReadOnlyList<ForecastDailyPoint> dailyPoints, decimal forecastedEndBalance, decimal averageDailyExpense)
    {
        var warnings = new List<string>();

        if (dailyPoints.Any(x => x.Balance < 0))
        {
            warnings.Add("Negative balance likely before month end.");
        }

        if (forecastedEndBalance < 0)
        {
            warnings.Add("Projected end-of-month balance is below zero.");
        }
        else if (averageDailyExpense > 0 && forecastedEndBalance < averageDailyExpense * 3)
        {
            warnings.Add("Projected month-end buffer is lower than three days of average spending.");
        }

        if (warnings.Count == 0)
        {
            warnings.Add("Forecast looks stable based on current balances and known patterns.");
        }

        return warnings;
    }

    private sealed record ForecastSnapshot(
        decimal CurrentBalance,
        decimal ForecastedEndBalance,
        decimal ProjectedIncome,
        decimal ProjectedExpense,
        decimal SafeToSpendPerDay,
        int DaysRemaining,
        IReadOnlyList<string> Warnings,
        IReadOnlyList<ForecastUpcomingExpenseItem> UpcomingKnownExpenses,
        IReadOnlyList<ForecastDailyPoint> DailyPoints);
}
