#nullable disable
using Microsoft.EntityFrameworkCore;
using UniversityEventsManagement.Data;
using UniversityEventsManagement.Models;

namespace UniversityEventsManagement.Services
{
    public interface IEventService
    {
        Task<Event> GetEventByIdAsync(int id);
        Task<List<Event>> GetAllEventsAsync();
        Task<List<Event>> GetApprovedEventsAsync();
        Task<List<Event>> GetUpcomingEventsAsync();
        Task<List<Event>> GetEventsByTypeAsync(string eventType);
        Task<List<Event>> SearchEventsAsync(string searchTerm);
        Task<Event> CreateEventAsync(Event @event);
        Task<Event> UpdateEventAsync(Event @event);
        Task<bool> DeleteEventAsync(int id);
        Task<List<Event>> GetEventsByUserAsync(string userId);
        Task<int> GetEventRegistrationCountAsync(int eventId);
        Task<int> GetEventAttendanceCountAsync(int eventId);
        Task<double> GetEventAverageRatingAsync(int eventId);
    }

    public class EventService : IEventService
    {
        private readonly ApplicationDbContext _context;
        private readonly INotificationService _notificationService;

        public EventService(ApplicationDbContext context, INotificationService notificationService)
        {
            _context = context;
            _notificationService = notificationService;
        }

        public async Task<Event> GetEventByIdAsync(int id)
        {
            return await _context.Events
                .Include(e => e.CreatedByUser)
                .Include(e => e.Registrations)
                .Include(e => e.Attendances)
                .Include(e => e.Feedbacks)
                .FirstOrDefaultAsync(e => e.EventID == id);
        }

        public async Task<List<Event>> GetAllEventsAsync()
        {
            return await _context.Events
                .Include(e => e.CreatedByUser)
                .OrderByDescending(e => e.EventDate)
                .ToListAsync();
        }

        public async Task<List<Event>> GetApprovedEventsAsync()
        {
            return await _context.Events
                .Where(e => e.IsApproved)
                .Include(e => e.CreatedByUser)
                .OrderByDescending(e => e.EventDate)
                .ToListAsync();
        }

        public async Task<List<Event>> GetUpcomingEventsAsync()
        {
            return await _context.Events
                .Where(e => e.IsApproved && e.EventDate >= DateTime.Now)
                .Include(e => e.CreatedByUser)
                .OrderBy(e => e.EventDate)
                .ToListAsync();
        }

        public async Task<List<Event>> GetEventsByTypeAsync(string eventType)
        {
            return await _context.Events
                .Where(e => e.IsApproved && e.EventType == eventType)
                .Include(e => e.CreatedByUser)
                .OrderByDescending(e => e.EventDate)
                .ToListAsync();
        }

        public async Task<List<Event>> SearchEventsAsync(string searchTerm)
        {
            return await _context.Events
                .Where(e => e.IsApproved && (e.Title.Contains(searchTerm) || e.Description.Contains(searchTerm)))
                .Include(e => e.CreatedByUser)
                .OrderByDescending(e => e.EventDate)
                .ToListAsync();
        }

        public async Task<Event> CreateEventAsync(Event @event)
        {
            @event.CreatedDate = DateTime.Now;
            _context.Events.Add(@event);
            await _context.SaveChangesAsync();

            // Send notification to all users
            await _notificationService.SendNotificationToAllAsync(
                $"فعالية جديدة: {@event.Title}",
                "EventCreated",
                @event.EventID);

            return @event;
        }

        public async Task<Event> UpdateEventAsync(Event @event)
        {
            _context.Events.Update(@event);
            await _context.SaveChangesAsync();
            return @event;
        }

        public async Task<bool> DeleteEventAsync(int id)
        {
            var @event = await GetEventByIdAsync(id);
            if (@event == null)
                return false;

            _context.Events.Remove(@event);
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<List<Event>> GetEventsByUserAsync(string userId)
        {
            return await _context.Events
                .Where(e => e.CreatedByUserID == userId)
                .Include(e => e.Registrations)
                .OrderByDescending(e => e.EventDate)
                .ToListAsync();
        }

        public async Task<int> GetEventRegistrationCountAsync(int eventId)
        {
            return await _context.Registrations
                .Where(r => r.EventID == eventId && r.Status == "Confirmed")
                .CountAsync();
        }

        public async Task<int> GetEventAttendanceCountAsync(int eventId)
        {
            return await _context.Attendances
                .Where(a => a.EventID == eventId && a.IsPresent)
                .CountAsync();
        }

        public async Task<double> GetEventAverageRatingAsync(int eventId)
        {
            var feedbacks = await _context.Feedbacks
                .Where(f => f.EventID == eventId)
                .ToListAsync();

            return feedbacks.Any() ? feedbacks.Average(f => f.Rating) : 0;
        }
    }
}
