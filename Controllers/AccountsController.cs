using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PersonalFinanceTracker.Api.Common;
using PersonalFinanceTracker.Api.Data;
using PersonalFinanceTracker.Api.DTOs.Accounts;
using PersonalFinanceTracker.Api.Entities;
using PersonalFinanceTracker.Api.Extensions;
using PersonalFinanceTracker.Api.Services.Interfaces;

namespace PersonalFinanceTracker.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/accounts")]
public class AccountsController(AppDbContext dbContext, IAccountAccessService accountAccessService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ApiResponse<object>>> Get(CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        var accessibleIds = await accountAccessService.GetAccessibleAccountIdsAsync(userId, cancellationToken);
        var sharedRoles = await dbContext.SharedAccountMembers
            .Where(x => x.UserId == userId)
            .ToDictionaryAsync(x => x.AccountId, x => x.Role, cancellationToken);

        var accounts = await dbContext.Accounts
            .Where(x => accessibleIds.Contains(x.Id))
            .ToListAsync(cancellationToken);

        var result = accounts.Select(x => new
        {
            x.Id,
            x.Name,
            x.Type,
            x.OpeningBalance,
            x.CurrentBalance,
            x.InstitutionName,
            accessRole = x.UserId == userId ? "owner" : (sharedRoles.TryGetValue(x.Id, out var role) ? role : "viewer"),
            isShared = x.UserId != userId
        });

        return Ok(new ApiResponse<object>(true, result));
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<object>>> Create(AccountRequest request, CancellationToken cancellationToken)
    {
        var account = new Account
        {
            UserId = User.GetUserId(),
            Name = request.Name,
            Type = request.Type,
            OpeningBalance = request.OpeningBalance,
            CurrentBalance = request.OpeningBalance,
            InstitutionName = request.InstitutionName
        };

        dbContext.Accounts.Add(account);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(new ApiResponse<object>(true, account));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ApiResponse<object>>> Update(Guid id, AccountRequest request, CancellationToken cancellationToken)
    {
        var account = await dbContext.Accounts.SingleOrDefaultAsync(x => x.Id == id && x.UserId == User.GetUserId(), cancellationToken);
        if (account is null) return NotFound();

        account.Name = request.Name;
        account.Type = request.Type;
        account.InstitutionName = request.InstitutionName;
        account.LastUpdatedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new ApiResponse<object>(true, account));
    }

    [HttpPost("transfer")]
    public async Task<ActionResult<ApiResponse<object>>> Transfer(TransferRequest request, CancellationToken cancellationToken)
    {
        if (request.Amount <= 0) return BadRequest(new ApiResponse<string>(false, string.Empty, "Transfer amount must be greater than 0."));

        var userId = User.GetUserId();
        if (!await accountAccessService.CanEditAccountAsync(userId, request.SourceAccountId, cancellationToken) ||
            !await accountAccessService.CanEditAccountAsync(userId, request.DestinationAccountId, cancellationToken))
        {
            return Forbid();
        }

        var source = await dbContext.Accounts.SingleOrDefaultAsync(x => x.Id == request.SourceAccountId, cancellationToken);
        var destination = await dbContext.Accounts.SingleOrDefaultAsync(x => x.Id == request.DestinationAccountId, cancellationToken);

        if (source is null || destination is null) return NotFound();
        if (source.CurrentBalance < request.Amount) return BadRequest(new ApiResponse<string>(false, string.Empty, "Insufficient funds."));

        source.CurrentBalance -= request.Amount;
        destination.CurrentBalance += request.Amount;
        source.LastUpdatedAt = DateTime.UtcNow;
        destination.LastUpdatedAt = DateTime.UtcNow;

        dbContext.Transactions.AddRange(
            new Transaction { UserId = userId, AccountId = source.Id, Type = "transfer", Amount = request.Amount, TransactionDate = request.Date, Note = request.Note, Merchant = $"Transfer to {destination.Name}" },
            new Transaction { UserId = userId, AccountId = destination.Id, Type = "transfer", Amount = request.Amount, TransactionDate = request.Date, Note = request.Note, Merchant = $"Transfer from {source.Name}" });

        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(new ApiResponse<object>(true, new { source, destination }));
    }
}
