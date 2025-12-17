#nullable disable
using Microsoft.EntityFrameworkCore;
using UniversityEventsManagement.Data;
using UniversityEventsManagement.Models;

namespace UniversityEventsManagement.Services
{
    public interface INotificationService
    {
        Task SendNotificationAsync(string userId, string message, string notificationType, int? eventId = null);
        Task SendNotificationToAllAsync(string message, string notificationType, int? eventId = null);
        Task SendEventNotificationAsync(Event @event, string message);
        Task<int> GetUnreadCountAsync(string userId);
    }

    public class NotificationService : INotificationService
    {
        private readonly ApplicationDbContext _context;

        public NotificationService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task SendNotificationAsync(string userId, string message, string notificationType, int? eventId = null)
        {
            var notification = new Notification
            {
                UserID = userId,
                Message = message,
                SentDate = DateTime.Now,
                IsRead = false,
                NotificationType = notificationType,
                EventID = eventId
            };

            _context.Notifications.Add(notification);
            await _context.SaveChangesAsync();
        }

        public async Task SendNotificationToAllAsync(string message, string notificationType, int? eventId = null)
        {
            var users = await _context.Users.ToListAsync();

            foreach (var user in users)
            {
                var notification = new Notification
                {
                    UserID = user.Id,
                    Message = message,
                    SentDate = DateTime.Now,
                    IsRead = false,
                    NotificationType = notificationType,
                    EventID = eventId
                };
                _context.Notifications.Add(notification);
            }

            await _context.SaveChangesAsync();
        }

        public async Task SendEventNotificationAsync(Event @event, string message)
        {
            var registeredUsers = await _context.Registrations
                .Where(r => r.EventID == @event.EventID)
                .Select(r => r.UserID)
                .Distinct()
                .ToListAsync();

            foreach (var userId in registeredUsers)
            {
                await SendNotificationAsync(userId, message, "EventUpdate", @event.EventID);
            }
        }

        public async Task<int> GetUnreadCountAsync(string userId)
        {
            return await _context.Notifications
                .Where(n => n.UserID == userId && !n.IsRead)
                .CountAsync();
        }
    }
}
