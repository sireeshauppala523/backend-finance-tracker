namespace PersonalFinanceTracker.Api.DTOs.Forecast;

public record ForecastMonthResponse(
    decimal CurrentBalance,
    decimal ForecastedEndBalance,
    decimal ProjectedIncome,
    decimal ProjectedExpense,
    decimal SafeToSpendPerDay,
    int DaysRemaining,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<ForecastUpcomingExpenseItem> UpcomingKnownExpenses);

public record ForecastDailyPoint(
    DateOnly Date,
    decimal Balance,
    decimal Income,
    decimal Expense);

public record ForecastUpcomingExpenseItem(
    string Title,
    decimal Amount,
    DateOnly Date,
    string Source);
