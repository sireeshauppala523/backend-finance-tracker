using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PersonalFinanceTracker.Api.Common;
using PersonalFinanceTracker.Api.Data;
using PersonalFinanceTracker.Api.DTOs.Transactions;
using PersonalFinanceTracker.Api.Entities;
using PersonalFinanceTracker.Api.Extensions;
using PersonalFinanceTracker.Api.Services.Interfaces;

namespace PersonalFinanceTracker.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/transactions")]
public class TransactionsController(AppDbContext dbContext, IAccountAccessService accountAccessService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ApiResponse<object>>> Get([FromQuery] string? type, [FromQuery] Guid? accountId, [FromQuery] Guid? categoryId, [FromQuery] string? search, CancellationToken cancellationToken)
    {
        var accessibleIds = await accountAccessService.GetAccessibleAccountIdsAsync(User.GetUserId(), cancellationToken);
        var query = dbContext.Transactions
            .Include(x => x.Category)
            .Include(x => x.Account)
            .Where(x => accessibleIds.Contains(x.AccountId));

        if (!string.IsNullOrWhiteSpace(type)) query = query.Where(x => x.Type == type);
        if (accountId.HasValue) query = query.Where(x => x.AccountId == accountId);
        if (categoryId.HasValue) query = query.Where(x => x.CategoryId == categoryId);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var normalizedSearch = search.Trim().ToLower();
            query = query.Where(x =>
                (x.Merchant != null && x.Merchant.ToLower().Contains(normalizedSearch)) ||
                (x.Note != null && x.Note.ToLower().Contains(normalizedSearch)));
        }

        var result = await query.OrderByDescending(x => x.TransactionDate).ThenByDescending(x => x.CreatedAt).Take(200).ToListAsync(cancellationToken);
        return Ok(new ApiResponse<object>(true, result));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<object>>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var transaction = await dbContext.Transactions
            .Include(x => x.Category)
            .Include(x => x.Account)
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (transaction is not null && !await accountAccessService.CanViewAccountAsync(User.GetUserId(), transaction.AccountId, cancellationToken))
        {
            return NotFound();
        }

        return transaction is null ? NotFound() : Ok(new ApiResponse<object>(true, transaction));
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<object>>> Create(TransactionRequest request, CancellationToken cancellationToken)
    {
        if (request.Amount <= 0) return BadRequest(new ApiResponse<string>(false, string.Empty, "Amount must be greater than 0."));

        var userId = User.GetUserId();
        if (!await accountAccessService.CanEditAccountAsync(userId, request.AccountId, cancellationToken))
        {
            return Forbid();
        }

        var account = await dbContext.Accounts.SingleOrDefaultAsync(x => x.Id == request.AccountId, cancellationToken);
        if (account is null) return BadRequest(new ApiResponse<string>(false, string.Empty, "Invalid account."));

        var transaction = new Transaction
        {
            UserId = userId,
            AccountId = request.AccountId,
            CategoryId = request.CategoryId,
            Type = request.Type,
            Amount = request.Amount,
            TransactionDate = request.Date,
            Merchant = request.Merchant,
            Note = request.Note,
            PaymentMethod = request.PaymentMethod,
            Tags = request.Tags ?? [],
            RecurringTransactionId = request.RecurringTransactionId
        };

        var ruleMessages = await ApplyRulesAsync(userId, transaction, cancellationToken);
        dbContext.Transactions.Add(transaction);
        ApplyBalance(account, request.Type, request.Amount);
        await dbContext.SaveChangesAsync(cancellationToken);
        var message = ruleMessages.Count > 0
            ? $"Transaction saved. {string.Join(" ", ruleMessages)}"
            : "Transaction saved.";
        return Ok(new ApiResponse<object>(true, transaction, message));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ApiResponse<object>>> Update(Guid id, TransactionRequest request, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        var transaction = await dbContext.Transactions.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (transaction is null || !await accountAccessService.CanEditAccountAsync(userId, transaction.AccountId, cancellationToken)) return NotFound();

        var account = await dbContext.Accounts.SingleOrDefaultAsync(x => x.Id == transaction.AccountId, cancellationToken);
        if (account is null) return BadRequest();

        ApplyBalance(account, transaction.Type, -transaction.Amount);

        transaction.AccountId = request.AccountId;
        transaction.CategoryId = request.CategoryId;
        transaction.Type = request.Type;
        transaction.Amount = request.Amount;
        transaction.TransactionDate = request.Date;
        transaction.Merchant = request.Merchant;
        transaction.Note = request.Note;
        transaction.PaymentMethod = request.PaymentMethod;
        transaction.Tags = request.Tags ?? [];
        transaction.UpdatedAt = DateTime.UtcNow;

        if (!await accountAccessService.CanEditAccountAsync(userId, request.AccountId, cancellationToken))
        {
            return Forbid();
        }

        var nextAccount = await dbContext.Accounts.SingleAsync(x => x.Id == request.AccountId, cancellationToken);
        var ruleMessages = await ApplyRulesAsync(userId, transaction, cancellationToken);
        ApplyBalance(nextAccount, request.Type, request.Amount);

        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(new ApiResponse<object>(true, transaction, ruleMessages.Count > 0 ? string.Join(" ", ruleMessages) : null));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        var transaction = await dbContext.Transactions.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (transaction is null || !await accountAccessService.CanEditAccountAsync(userId, transaction.AccountId, cancellationToken)) return NotFound();

        var account = await dbContext.Accounts.SingleOrDefaultAsync(x => x.Id == transaction.AccountId, cancellationToken);
        if (account is not null)
        {
            ApplyBalance(account, transaction.Type, -transaction.Amount);
        }

        dbContext.Transactions.Remove(transaction);
        await dbContext.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    private async Task<List<string>> ApplyRulesAsync(Guid userId, Transaction transaction, CancellationToken cancellationToken)
    {
        var messages = new List<string>();
        var rules = await dbContext.Rules
            .Where(x => x.UserId == userId && x.IsActive)
            .OrderBy(x => x.CreatedAt)
            .ToListAsync(cancellationToken);
        var categoryName = transaction.CategoryId.HasValue
            ? await dbContext.Categories
                .Where(x => x.Id == transaction.CategoryId.Value)
                .Select(x => x.Name)
                .FirstOrDefaultAsync(cancellationToken)
            : null;

        foreach (var rule in rules)
        {
            if (!RuleMatches(rule, transaction, categoryName))
            {
                continue;
            }

            if (rule.ActionType == "categorize")
            {
                var normalizedName = rule.ActionValue.Trim().ToLower();
                var category = await dbContext.Categories
                    .Where(x => x.Name.ToLower() == normalizedName && (x.UserId == userId || x.UserId == null))
                    .OrderByDescending(x => x.UserId == userId)
                    .FirstOrDefaultAsync(cancellationToken);

                if (category is not null)
                {
                    transaction.CategoryId = category.Id;
                }
            }
            else if (rule.ActionType == "tag")
            {
                var tags = transaction.Tags.ToHashSet(StringComparer.OrdinalIgnoreCase);
                tags.Add(rule.ActionValue.Trim());
                transaction.Tags = tags.ToArray();
            }
            else if (rule.ActionType == "alert")
            {
                messages.Add($"Alert: {rule.ActionValue.Trim()}");
            }
        }

        return messages;
    }

    private static bool RuleMatches(Rule rule, Transaction transaction, string? categoryName)
    {
        return rule.ConditionField switch
        {
            "merchant" => CompareText(transaction.Merchant, rule.ConditionOperator, rule.ConditionValue),
            "category" => CompareText(categoryName, rule.ConditionOperator, rule.ConditionValue),
            "amount" => CompareNumber(transaction.Amount, rule.ConditionOperator, rule.ConditionValue),
            "forecastBalance" => false,
            _ => false
        };
    }

    private static bool CompareText(string? source, string conditionOperator, string target)
    {
        if (string.IsNullOrWhiteSpace(source))
        {
            return false;
        }

        var left = source.Trim().ToLowerInvariant();
        var right = target.Trim().ToLowerInvariant();
        return conditionOperator switch
        {
            "equals" => left == right,
            "contains" => left.Contains(right),
            _ => false
        };
    }

    private static bool CompareNumber(decimal value, string conditionOperator, string target)
    {
        if (!decimal.TryParse(target, out var parsed))
        {
            return false;
        }

        return conditionOperator switch
        {
            "greaterThan" => value > parsed,
            "lessThan" => value < parsed,
            "equals" => value == parsed,
            _ => false
        };
    }

    private static void ApplyBalance(Account account, string type, decimal amountDelta)
    {
        if (type == "income") account.CurrentBalance += amountDelta;
        else if (type == "expense") account.CurrentBalance -= amountDelta;
        account.LastUpdatedAt = DateTime.UtcNow;
    }
}
