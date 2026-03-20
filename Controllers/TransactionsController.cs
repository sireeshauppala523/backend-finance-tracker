using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PersonalFinanceTracker.Api.Common;
using PersonalFinanceTracker.Api.Data;
using PersonalFinanceTracker.Api.DTOs.Transactions;
using PersonalFinanceTracker.Api.Entities;
using PersonalFinanceTracker.Api.Extensions;

namespace PersonalFinanceTracker.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/transactions")]
public class TransactionsController(AppDbContext dbContext) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ApiResponse<object>>> Get([FromQuery] string? type, [FromQuery] Guid? accountId, [FromQuery] Guid? categoryId, [FromQuery] string? search, CancellationToken cancellationToken)
    {
        var query = dbContext.Transactions
            .Include(x => x.Category)
            .Include(x => x.Account)
            .Where(x => x.UserId == User.GetUserId());

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
            .SingleOrDefaultAsync(x => x.Id == id && x.UserId == User.GetUserId(), cancellationToken);

        return transaction is null ? NotFound() : Ok(new ApiResponse<object>(true, transaction));
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<object>>> Create(TransactionRequest request, CancellationToken cancellationToken)
    {
        if (request.Amount <= 0) return BadRequest(new ApiResponse<string>(false, string.Empty, "Amount must be greater than 0."));

        var userId = User.GetUserId();
        var account = await dbContext.Accounts.SingleOrDefaultAsync(x => x.Id == request.AccountId && x.UserId == userId, cancellationToken);
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

        dbContext.Transactions.Add(transaction);
        ApplyBalance(account, request.Type, request.Amount);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(new ApiResponse<object>(true, transaction, "Transaction saved."));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ApiResponse<object>>> Update(Guid id, TransactionRequest request, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        var transaction = await dbContext.Transactions.SingleOrDefaultAsync(x => x.Id == id && x.UserId == userId, cancellationToken);
        if (transaction is null) return NotFound();

        var account = await dbContext.Accounts.SingleOrDefaultAsync(x => x.Id == transaction.AccountId && x.UserId == userId, cancellationToken);
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

        var nextAccount = await dbContext.Accounts.SingleAsync(x => x.Id == request.AccountId && x.UserId == userId, cancellationToken);
        ApplyBalance(nextAccount, request.Type, request.Amount);

        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(new ApiResponse<object>(true, transaction));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        var transaction = await dbContext.Transactions.SingleOrDefaultAsync(x => x.Id == id && x.UserId == userId, cancellationToken);
        if (transaction is null) return NotFound();

        var account = await dbContext.Accounts.SingleOrDefaultAsync(x => x.Id == transaction.AccountId && x.UserId == userId, cancellationToken);
        if (account is not null)
        {
            ApplyBalance(account, transaction.Type, -transaction.Amount);
        }

        dbContext.Transactions.Remove(transaction);
        await dbContext.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    private static void ApplyBalance(Account account, string type, decimal amountDelta)
    {
        if (type == "income") account.CurrentBalance += amountDelta;
        else if (type == "expense") account.CurrentBalance -= amountDelta;
        account.LastUpdatedAt = DateTime.UtcNow;
    }
}