using Microsoft.EntityFrameworkCore;
using PersonalFinanceTracker.Api.Data;
using PersonalFinanceTracker.Api.Services.Interfaces;

namespace PersonalFinanceTracker.Api.Services.Implementations;

public class AccountAccessService(AppDbContext dbContext) : IAccountAccessService
{
    public async Task<HashSet<Guid>> GetAccessibleAccountIdsAsync(Guid userId, CancellationToken cancellationToken)
    {
        var ownedIds = await dbContext.Accounts
            .Where(x => x.UserId == userId)
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);

        var sharedIds = await dbContext.SharedAccountMembers
            .Where(x => x.UserId == userId)
            .Select(x => x.AccountId)
            .ToListAsync(cancellationToken);

        return ownedIds.Concat(sharedIds).ToHashSet();
    }

    public async Task<bool> CanViewAccountAsync(Guid userId, Guid accountId, CancellationToken cancellationToken)
    {
        var accessibleIds = await GetAccessibleAccountIdsAsync(userId, cancellationToken);
        return accessibleIds.Contains(accountId);
    }

    public async Task<bool> CanEditAccountAsync(Guid userId, Guid accountId, CancellationToken cancellationToken)
    {
        var isOwner = await dbContext.Accounts.AnyAsync(x => x.Id == accountId && x.UserId == userId, cancellationToken);
        if (isOwner)
        {
            return true;
        }

        return await dbContext.SharedAccountMembers.AnyAsync(
            x => x.AccountId == accountId && x.UserId == userId && (x.Role == "owner" || x.Role == "editor"),
            cancellationToken);
    }

    public Task<bool> IsOwnerAsync(Guid userId, Guid accountId, CancellationToken cancellationToken)
        => dbContext.Accounts.AnyAsync(x => x.Id == accountId && x.UserId == userId, cancellationToken);
}
