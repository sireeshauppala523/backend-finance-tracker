using Microsoft.EntityFrameworkCore;
using PersonalFinanceTracker.Api.Data;
using PersonalFinanceTracker.Api.DTOs.Insights;
using PersonalFinanceTracker.Api.Services.Interfaces;

namespace PersonalFinanceTracker.Api.Services.Implementations;

public class InsightsService(AppDbContext dbContext, IAccountAccessService accountAccessService) : IInsightsService
{
    public async Task<FinancialHealthScoreResponse> GetHealthScoreAsync(Guid userId, CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow.Date);
        var monthStart = new DateOnly(today.Year, today.Month, 1);
        var accessibleAccountIds = await accountAccessService.GetAccessibleAccountIdsAsync(userId, cancellationToken);

        var monthTransactions = await dbContext.Transactions
            .Where(x => accessibleAccountIds.Contains(x.AccountId) && x.TransactionDate >= monthStart)
            .ToListAsync(cancellationToken);

        var expenseHistory = await dbContext.Transactions
            .Where(x => accessibleAccountIds.Contains(x.AccountId) && x.Type == "expense" && x.TransactionDate >= today.AddMonths(-6))
            .GroupBy(x => new { x.TransactionDate.Year, x.TransactionDate.Month })
            .Select(group => group.Sum(x => x.Amount))
            .ToListAsync(cancellationToken);

        var currentMonthBudgets = await dbContext.Budgets
            .Where(x => x.UserId == userId && x.Month == today.Month && x.Year == today.Year)
            .ToListAsync(cancellationToken);

        var currentMonthBudgetSpend = await dbContext.Transactions
            .Where(x => accessibleAccountIds.Contains(x.AccountId) && x.Type == "expense" && x.TransactionDate.Month == today.Month && x.TransactionDate.Year == today.Year && x.CategoryId != null)
            .GroupBy(x => x.CategoryId!.Value)
            .Select(group => new { CategoryId = group.Key, Spent = group.Sum(x => x.Amount) })
            .ToDictionaryAsync(x => x.CategoryId, x => x.Spent, cancellationToken);

        var currentBalance = await dbContext.Accounts
            .Where(x => accessibleAccountIds.Contains(x.Id))
            .SumAsync(x => x.CurrentBalance, cancellationToken);

        var income = monthTransactions.Where(x => x.Type == "income").Sum(x => x.Amount);
        var expense = monthTransactions.Where(x => x.Type == "expense").Sum(x => x.Amount);
        var savingsRate = income > 0 ? Math.Max((income - expense) / income, 0) : 0;
        var savingsScore = Clamp((int)Math.Round(savingsRate / 0.30m * 100m));

        var averageExpense = expenseHistory.Count > 0 ? expenseHistory.Average() : 0;
        var expenseSpread = expenseHistory.Count > 1 ? expenseHistory.Max() - expenseHistory.Min() : 0;
        var stabilityRatio = averageExpense > 0 ? expenseSpread / averageExpense : 0.30m;
        var expenseStabilityScore = expenseHistory.Count > 1 ? Clamp((int)Math.Round(100m - stabilityRatio * 45m)) : 68;

        var budgetRatios = currentMonthBudgets
            .Select(budget => budget.Amount > 0 && currentMonthBudgetSpend.TryGetValue(budget.CategoryId, out var spent) ? spent / budget.Amount : 0)
            .ToList();
        var averageBudgetRatio = budgetRatios.Count > 0 ? budgetRatios.Average() : 0;
        var budgetAdherenceScore = budgetRatios.Count > 0 ? Clamp((int)Math.Round(115m - averageBudgetRatio * 60m)) : 72;

        var cashBufferMonths = averageExpense > 0 ? currentBalance / averageExpense : 1;
        var cashBufferScore = Clamp((int)Math.Round(cashBufferMonths / 2.5m * 100m));

        var factors = new List<HealthFactorResponse>
        {
            new("savingsRate", "Savings rate", savingsScore, income > 0 ? $"{Math.Round(savingsRate * 100m)}% of this month's income is staying available." : "Add a few income entries to calculate your savings rate with confidence."),
            new("expenseStability", "Expense stability", expenseStabilityScore, expenseHistory.Count > 1 ? "Recent monthly expenses are being compared for volatility." : "Using a starter score until enough monthly history is available."),
            new("budgetAdherence", "Budget adherence", budgetAdherenceScore, currentMonthBudgets.Count > 0 ? $"{currentMonthBudgets.Count(budget => currentMonthBudgetSpend.GetValueOrDefault(budget.CategoryId) <= budget.Amount)} of {currentMonthBudgets.Count} active budgets are on track." : "Set category budgets to strengthen this part of the score."),
            new("cashBuffer", "Cash buffer", cashBufferScore, averageExpense > 0 ? $"{cashBufferMonths:F1} month(s) of typical expense coverage at current balances." : "Add spending history to estimate your cash buffer."),
        };

        var weightedScore = Clamp((int)Math.Round(
            savingsScore * 0.30m +
            expenseStabilityScore * 0.20m +
            budgetAdherenceScore * 0.25m +
            cashBufferScore * 0.25m));

        var band = weightedScore >= 85 ? "Excellent"
            : weightedScore >= 70 ? "Healthy"
            : weightedScore >= 55 ? "Fair"
            : "Watch";

        var suggestions = factors
            .Where(x => x.Score < 70)
            .OrderBy(x => x.Score)
            .Take(3)
            .Select(x => x.Key switch
            {
                "savingsRate" => "Shift a little more of each paycheck into savings or trim one flexible expense category.",
                "expenseStability" => "Review recent spikes and turn irregular large spends into planned budget buckets.",
                "budgetAdherence" => "Tighten the categories that are crossing budget fastest and add alert thresholds.",
                "cashBuffer" => "Build a stronger cash cushion so forecast dips do not put the month under pressure.",
                _ => "Keep logging activity so the score can become more precise."
            })
            .ToList();

        var summary = band switch
        {
            "Excellent" => "Your balances, spending rhythm, and budget control are all supporting a strong month.",
            "Healthy" => "You are in a steady position, with a few opportunities to make the month feel even safer.",
            "Fair" => "You are staying afloat, but one or two pressure points deserve attention before month-end.",
            _ => "The current mix of balances and spending suggests closer watch is needed this month."
        };

        return new FinancialHealthScoreResponse(weightedScore, band, summary, suggestions, factors);
    }

    private static int Clamp(int value, int min = 0, int max = 100)
        => Math.Max(min, Math.Min(max, value));
}
