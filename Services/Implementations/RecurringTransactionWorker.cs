using Microsoft.EntityFrameworkCore;
using PersonalFinanceTracker.Api.Data;
using PersonalFinanceTracker.Api.Entities;

namespace PersonalFinanceTracker.Api.Services.Implementations;

public class RecurringTransactionWorker(IServiceScopeFactory scopeFactory, ILogger<RecurringTransactionWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await ProcessRecurringTransactionsAsync(stoppingToken);
            await Task.Delay(TimeSpan.FromHours(6), stoppingToken);
        }
    }

    private async Task ProcessRecurringTransactionsAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var dueItems = await dbContext.RecurringTransactions
            .Where(x => x.AutoCreateTransaction && !x.IsPaused && x.NextRunDate <= today)
            .ToListAsync(cancellationToken);

        foreach (var recurring in dueItems)
        {
            if (recurring.AccountId is null)
            {
                continue;
            }

            dbContext.Transactions.Add(new Transaction
            {
                UserId = recurring.UserId,
                AccountId = recurring.AccountId.Value,
                CategoryId = recurring.CategoryId,
                Type = recurring.Type,
                Amount = recurring.Amount,
                TransactionDate = today,
                Merchant = recurring.Title,
                Note = "Auto-generated from recurring schedule",
                RecurringTransactionId = recurring.Id
            });

            recurring.NextRunDate = recurring.Frequency switch
            {
                "daily" => recurring.NextRunDate.AddDays(1),
                "weekly" => recurring.NextRunDate.AddDays(7),
                "yearly" => recurring.NextRunDate.AddYears(1),
                _ => recurring.NextRunDate.AddMonths(1)
            };
        }

        if (dueItems.Count > 0)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Processed {Count} recurring transactions", dueItems.Count);
        }
    }
}