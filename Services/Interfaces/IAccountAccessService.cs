namespace PersonalFinanceTracker.Api.Services.Interfaces;

public interface IAccountAccessService
{
    Task<HashSet<Guid>> GetAccessibleAccountIdsAsync(Guid userId, CancellationToken cancellationToken);
    Task<bool> CanViewAccountAsync(Guid userId, Guid accountId, CancellationToken cancellationToken);
    Task<bool> CanEditAccountAsync(Guid userId, Guid accountId, CancellationToken cancellationToken);
    Task<bool> IsOwnerAsync(Guid userId, Guid accountId, CancellationToken cancellationToken);
}
