using PersonalFinanceTracker.Api.Data;
using PersonalFinanceTracker.Api.Entities;
using PersonalFinanceTracker.Api.Services.Interfaces;

namespace PersonalFinanceTracker.Api.Services.Implementations;

public class NotificationService(AppDbContext dbContext) : INotificationService
{
    public async Task CreateAsync(Guid userId, string type, string title, string message, string? relatedEntityType = null, Guid? relatedEntityId = null, CancellationToken cancellationToken = default)
    {
        dbContext.Notifications.Add(new Notification
        {
            UserId = userId,
            Type = string.IsNullOrWhiteSpace(type) ? "info" : type.Trim().ToLowerInvariant(),
            Title = title.Trim(),
            Message = message.Trim(),
            RelatedEntityType = string.IsNullOrWhiteSpace(relatedEntityType) ? null : relatedEntityType.Trim(),
            RelatedEntityId = relatedEntityId
        });

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
