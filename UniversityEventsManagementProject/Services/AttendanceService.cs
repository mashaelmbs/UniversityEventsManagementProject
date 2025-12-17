#nullable disable
using Microsoft.EntityFrameworkCore;
using UniversityEventsManagement.Data;
using UniversityEventsManagement.Models;

namespace UniversityEventsManagement.Services
{
    public interface IAttendanceService
    {
        Task<Attendance> RecordAttendanceAsync(int eventId, string userId);
        Task<List<Attendance>> GetEventAttendanceAsync(int eventId);
        Task<List<Attendance>> GetUserAttendanceAsync(string userId);
        Task<Attendance> GetAttendanceByIdAsync(int id);
        Task<bool> IsUserAttendedAsync(int eventId, string userId);
        Task<int> GetEventAttendanceCountAsync(int eventId);
        Task<double> GetEventAttendanceRateAsync(int eventId);
        Task<bool> MarkAttendanceAsync(int eventId, string userId, bool isPresent);
    }

    public class AttendanceService : IAttendanceService
    {
        private readonly ApplicationDbContext _context;
        private readonly INotificationService _notificationService;

        public AttendanceService(ApplicationDbContext context, INotificationService notificationService)
        {
            _context = context;
            _notificationService = notificationService;
        }

        public async Task<Attendance> RecordAttendanceAsync(int eventId, string userId)
        {
            var existingAttendance = await _context.Attendances
                .FirstOrDefaultAsync(a => a.EventID == eventId && a.UserID == userId);

            if (existingAttendance != null)
                throw new Exception("تم تسجيل حضورك بالفعل في هذه الفعالية");

            var @event = await _context.Events.FindAsync(eventId);
            if (@event == null)
                throw new Exception("الفعالية غير موجودة");

            var attendance = new Attendance
            {
                EventID = eventId,
                UserID = userId,
                CheckInTime = DateTime.Now,
                IsPresent = true,
                QRCode = Guid.NewGuid().ToString()
            };

            _context.Attendances.Add(attendance);
            await _context.SaveChangesAsync();

            // Send notification
            await _notificationService.SendNotificationAsync(
                userId,
                $"تم تسجيل حضورك في الفعالية: {@event.Title}",
                "AttendanceRecorded",
                eventId);

            return attendance;
        }

        public async Task<List<Attendance>> GetEventAttendanceAsync(int eventId)
        {
            return await _context.Attendances
                .Where(a => a.EventID == eventId)
                .Include(a => a.User)
                .Include(a => a.Event)
                .OrderByDescending(a => a.CheckInTime)
                .ToListAsync();
        }

        public async Task<List<Attendance>> GetUserAttendanceAsync(string userId)
        {
            return await _context.Attendances
                .Where(a => a.UserID == userId && a.IsPresent)
                .Include(a => a.Event)
                .OrderByDescending(a => a.CheckInTime)
                .ToListAsync();
        }

        public async Task<Attendance> GetAttendanceByIdAsync(int id)
        {
            return await _context.Attendances
                .Include(a => a.User)
                .Include(a => a.Event)
                .FirstOrDefaultAsync(a => a.AttendanceID == id);
        }

        public async Task<bool> IsUserAttendedAsync(int eventId, string userId)
        {
            return await _context.Attendances
                .AnyAsync(a => a.EventID == eventId && a.UserID == userId && a.IsPresent);
        }

        public async Task<int> GetEventAttendanceCountAsync(int eventId)
        {
            return await _context.Attendances
                .Where(a => a.EventID == eventId && a.IsPresent)
                .CountAsync();
        }

        public async Task<double> GetEventAttendanceRateAsync(int eventId)
        {
            var totalRegistrations = await _context.Registrations
                .Where(r => r.EventID == eventId && r.Status == "Confirmed")
                .CountAsync();

            if (totalRegistrations == 0)
                return 0;

            var attendanceCount = await GetEventAttendanceCountAsync(eventId);
            return Math.Round((double)attendanceCount / totalRegistrations * 100, 2);
        }

        public async Task<bool> MarkAttendanceAsync(int eventId, string userId, bool isPresent)
        {
            var attendance = await _context.Attendances
                .FirstOrDefaultAsync(a => a.EventID == eventId && a.UserID == userId);

            if (attendance == null)
            {
                var newAttendance = new Attendance
                {
                    EventID = eventId,
                    UserID = userId,
                    CheckInTime = DateTime.Now,
                    IsPresent = isPresent,
                    QRCode = Guid.NewGuid().ToString()
                };
                _context.Attendances.Add(newAttendance);
            }
            else
            {
                attendance.IsPresent = isPresent;
                _context.Attendances.Update(attendance);
            }

            await _context.SaveChangesAsync();
            return true;
        }
    }
}
