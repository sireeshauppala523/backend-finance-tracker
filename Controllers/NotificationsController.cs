using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PersonalFinanceTracker.Api.Common;
using PersonalFinanceTracker.Api.Data;
using PersonalFinanceTracker.Api.Extensions;

namespace PersonalFinanceTracker.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/notifications")]
public class NotificationsController(AppDbContext dbContext) : ControllerBase
{
    public record NotificationResponse(Guid Id, string Type, string Title, string Message, string? RelatedEntityType, Guid? RelatedEntityId, bool IsRead, DateTime CreatedAt);
    public record NotificationListResponse(IReadOnlyList<NotificationResponse> Items, int UnreadCount);

    [HttpGet]
    public async Task<ActionResult<ApiResponse<NotificationListResponse>>> Get(CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        var items = await dbContext.Notifications
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.CreatedAt)
            .Take(25)
            .Select(x => new NotificationResponse(x.Id, x.Type, x.Title, x.Message, x.RelatedEntityType, x.RelatedEntityId, x.IsRead, x.CreatedAt))
            .ToListAsync(cancellationToken);

        var unreadCount = await dbContext.Notifications.CountAsync(x => x.UserId == userId && !x.IsRead, cancellationToken);
        return Ok(new ApiResponse<NotificationListResponse>(true, new NotificationListResponse(items, unreadCount)));
    }

    [HttpPost("{id:guid}/read")]
    public async Task<IActionResult> MarkRead(Guid id, CancellationToken cancellationToken)
    {
        var notification = await dbContext.Notifications.SingleOrDefaultAsync(x => x.Id == id && x.UserId == User.GetUserId(), cancellationToken);
        if (notification is null) return NotFound();

        if (!notification.IsRead)
        {
            notification.IsRead = true;
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return NoContent();
    }

    [HttpPost("read-all")]
    public async Task<IActionResult> MarkAllRead(CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        var notifications = await dbContext.Notifications
            .Where(x => x.UserId == userId && !x.IsRead)
            .ToListAsync(cancellationToken);

        if (notifications.Count == 0) return NoContent();

        foreach (var notification in notifications)
        {
            notification.IsRead = true;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return NoContent();
    }
}
