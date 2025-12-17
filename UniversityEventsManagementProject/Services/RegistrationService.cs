#nullable disable
using Microsoft.EntityFrameworkCore;
using UniversityEventsManagement.Data;
using UniversityEventsManagement.Models;

namespace UniversityEventsManagement.Services
{
    public interface IRegistrationService
    {
        Task<Registration> RegisterUserAsync(int eventId, string userId, int guestCount = 0);
        Task<bool> CancelRegistrationAsync(int registrationId);
        Task<List<Registration>> GetUserRegistrationsAsync(string userId);
        Task<List<Registration>> GetEventRegistrationsAsync(int eventId);
        Task<Registration> GetRegistrationByIdAsync(int id);
        Task<bool> IsUserRegisteredAsync(int eventId, string userId);
        Task<int> GetWaitlistCountAsync(int eventId);
        Task<List<Registration>> GetConfirmedRegistrationsAsync(int eventId);
        Task<List<Registration>> GetWaitlistAsync(int eventId);
    }

    public class RegistrationService : IRegistrationService
    {
        private readonly ApplicationDbContext _context;
        private readonly INotificationService _notificationService;

        public RegistrationService(ApplicationDbContext context, INotificationService notificationService)
        {
            _context = context;
            _notificationService = notificationService;
        }

        public async Task<Registration> RegisterUserAsync(int eventId, string userId, int guestCount = 0)
        {
            var @event = await _context.Events.FindAsync(eventId);
            if (@event == null)
                throw new Exception("الفعالية غير موجودة");

            var existingRegistration = await _context.Registrations
                .FirstOrDefaultAsync(r => r.EventID == eventId && r.UserID == userId && r.Status != "Cancelled");

            if (existingRegistration != null)
                throw new Exception("أنت مسجل بالفعل في هذه الفعالية");

            var confirmedCount = await _context.Registrations
                .Where(r => r.EventID == eventId && r.Status == "Confirmed")
                .CountAsync();

            var status = confirmedCount >= @event.MaxCapacity ? "Waitlist" : "Confirmed";

            var registration = new Registration
            {
                EventID = eventId,
                UserID = userId,
                RegistrationDate = DateTime.Now,
                Status = status,
                GuestCount = guestCount
            };

            _context.Registrations.Add(registration);
            await _context.SaveChangesAsync();

            var message = status == "Confirmed"
                ? $"تم تسجيلك بنجاح في الفعالية: {@event.Title}"
                : $"تم إضافتك لقائمة الانتظار في الفعالية: {@event.Title}";

            await _notificationService.SendNotificationAsync(userId, message, "RegistrationConfirmed", eventId);

            return registration;
        }

        public async Task<bool> CancelRegistrationAsync(int registrationId)
        {
            var registration = await _context.Registrations.FindAsync(registrationId);
            if (registration == null)
                return false;

            registration.Status = "Cancelled";
            _context.Registrations.Update(registration);
            await _context.SaveChangesAsync();

            await _notificationService.SendNotificationAsync(
                registration.UserID,
                "تم إلغاء تسجيلك في الفعالية",
                "RegistrationCancelled",
                registration.EventID);

            return true;
        }

        public async Task<List<Registration>> GetUserRegistrationsAsync(string userId)
        {
            return await _context.Registrations
                .Where(r => r.UserID == userId)
                .Include(r => r.Event)
                .OrderByDescending(r => r.RegistrationDate)
                .ToListAsync();
        }

        public async Task<List<Registration>> GetEventRegistrationsAsync(int eventId)
        {
            return await _context.Registrations
                .Where(r => r.EventID == eventId)
                .Include(r => r.User)
                .OrderByDescending(r => r.RegistrationDate)
                .ToListAsync();
        }

        public async Task<Registration> GetRegistrationByIdAsync(int id)
        {
            return await _context.Registrations
                .Include(r => r.Event)
                .Include(r => r.User)
                .FirstOrDefaultAsync(r => r.RegistrationID == id);
        }

        public async Task<bool> IsUserRegisteredAsync(int eventId, string userId)
        {
            return await _context.Registrations
                .AnyAsync(r => r.EventID == eventId && r.UserID == userId && r.Status != "Cancelled");
        }

        public async Task<int> GetWaitlistCountAsync(int eventId)
        {
            return await _context.Registrations
                .Where(r => r.EventID == eventId && r.Status == "Waitlist")
                .CountAsync();
        }

        public async Task<List<Registration>> GetConfirmedRegistrationsAsync(int eventId)
        {
            return await _context.Registrations
                .Where(r => r.EventID == eventId && r.Status == "Confirmed")
                .Include(r => r.User)
                .ToListAsync();
        }

        public async Task<List<Registration>> GetWaitlistAsync(int eventId)
        {
            return await _context.Registrations
                .Where(r => r.EventID == eventId && r.Status == "Waitlist")
                .Include(r => r.User)
                .OrderBy(r => r.RegistrationDate)
                .ToListAsync();
        }
    }
}
