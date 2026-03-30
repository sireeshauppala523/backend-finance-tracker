namespace PersonalFinanceTracker.Api.Services.Interfaces;

public interface INotificationService
{
    Task CreateAsync(Guid userId, string type, string title, string message, string? relatedEntityType = null, Guid? relatedEntityId = null, CancellationToken cancellationToken = default);
}
