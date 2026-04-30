using BusinessLayer.DTOs;
using DataLayer.Models;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace BusinessLayer.Services
{
    public interface INotificationService
    {
        Task SendAsync(Guid userId, string title, string body, NotificationType type, Guid? relatedItemId = null);
        Task<PagedResult<NotificationDTO>> GetUserNotificationsAsync(Guid userId, int page = 1, int pageSize = 20);
        Task MarkAllReadAsync(Guid userId);
        Task MarkReadAsync(Guid notificationId, Guid userId);
        Task<int> GetUnreadCountAsync(Guid userId);
    }

    public class NotificationService : INotificationService
    {
        private readonly DBContext _db;
        private readonly IHubContext<NotificationHub> _hubContext;

        public NotificationService(DBContext db, IHubContext<NotificationHub> hubContext)
        {
            _db = db;
            _hubContext = hubContext;
        }

        public async Task SendAsync(Guid userId, string title, string body, NotificationType type, Guid? relatedItemId = null)
        {
            var notification = new Notification
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Title = title,
                Body = body,
                Type = type,
                RelatedItemId = relatedItemId,
                IsRead = false,
                CreatedAt = DateTime.UtcNow
            };

            _db.Notifications.Add(notification);
            await _db.SaveChangesAsync();

            // Push real-time via SignalR
            var dto = new NotificationDTO
            {
                Id = notification.Id,
                Title = title,
                Body = body,
                Type = type.ToString(),
                RelatedItemId = relatedItemId,
                IsRead = false,
                CreatedAt = notification.CreatedAt
            };

            await _hubContext.Clients.Group($"user:{userId}")
                .SendAsync("ReceiveNotification", dto);
        }

        public async Task<PagedResult<NotificationDTO>> GetUserNotificationsAsync(Guid userId, int page = 1, int pageSize = 20)
        {
            var query = _db.Notifications
                .Where(n => n.UserId == userId)
                .OrderByDescending(n => n.CreatedAt);

            var total = await query.CountAsync();
            var items = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(n => new NotificationDTO
                {
                    Id = n.Id,
                    Title = n.Title,
                    Body = n.Body,
                    Type = n.Type.ToString(),
                    RelatedItemId = n.RelatedItemId,
                    IsRead = n.IsRead,
                    CreatedAt = n.CreatedAt
                })
                .ToListAsync();

            return new PagedResult<NotificationDTO>
            {
                Items = items,
                TotalCount = total,
                Page = page,
                PageSize = pageSize
            };
        }

        public async Task MarkAllReadAsync(Guid userId)
        {
            await _db.Notifications
                .Where(n => n.UserId == userId && !n.IsRead)
                .ExecuteUpdateAsync(s => s.SetProperty(n => n.IsRead, true));
        }

        public async Task MarkReadAsync(Guid notificationId, Guid userId)
        {
            await _db.Notifications
                .Where(n => n.Id == notificationId && n.UserId == userId)
                .ExecuteUpdateAsync(s => s.SetProperty(n => n.IsRead, true));
        }

        public async Task<int> GetUnreadCountAsync(Guid userId)
        {
            return await _db.Notifications.CountAsync(n => n.UserId == userId && !n.IsRead);
        }
    }
}