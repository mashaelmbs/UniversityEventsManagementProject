#nullable disable
using Microsoft.EntityFrameworkCore;
using UniversityEventsManagement.Data;
using UniversityEventsManagement.Models;

namespace UniversityEventsManagement.Services
{
    public interface IReportService
    {
        Task<EventReportDto> GetEventReportAsync(int eventId);
        Task<List<UserReportDto>> GetUserReportsAsync();
        Task<DashboardStatisticsDto> GetDashboardStatisticsAsync();
        Task<List<EventStatisticsDto>> GetEventStatisticsAsync();
    }

    public class ReportService : IReportService
    {
        private readonly ApplicationDbContext _context;

        public ReportService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<EventReportDto> GetEventReportAsync(int eventId)
        {
            var @event = await _context.Events
                .Include(e => e.Registrations)
                .Include(e => e.Attendances)
                .Include(e => e.Feedbacks)
                .Include(e => e.Certificates)
                .FirstOrDefaultAsync(e => e.EventID == eventId);

            if (@event == null)
                return null;

            var report = new EventReportDto
            {
                EventID = @event.EventID,
                Title = @event.Title,
                EventDate = @event.EventDate,
                TotalRegistrations = @event.Registrations.Count,
                ConfirmedRegistrations = @event.Registrations.Count(r => r.Status == "Confirmed"),
                WaitlistCount = @event.Registrations.Count(r => r.Status == "Waitlist"),
                TotalAttendance = @event.Attendances.Count(a => a.IsPresent),
                AttendanceRate = @event.Registrations.Count > 0 
                    ? Math.Round((double)@event.Attendances.Count(a => a.IsPresent) / @event.Registrations.Count * 100, 2)
                    : 0,
                TotalFeedback = @event.Feedbacks.Count,
                AverageRating = @event.Feedbacks.Any() ? Math.Round(@event.Feedbacks.Average(f => f.Rating), 2) : 0,
                CertificatesIssued = @event.Certificates.Count,
                VolunteerHours = @event.VolunteerHours
            };

            return report;
        }

        public async Task<List<UserReportDto>> GetUserReportsAsync()
        {
            var users = await _context.Users
                .Include(u => u.Registrations)
                .Include(u => u.Attendances)
                .Include(u => u.Certificates)
                .ToListAsync();

            var reports = users.Select(u => new UserReportDto
            {
                UserId = u.Id,
                FullName = $"{u.FirstName} {u.LastName}",
                Email = u.Email,
                UniversityID = u.UniversityID,
                UserType = u.UserType,
                TotalRegistrations = u.Registrations.Count,
                TotalAttendance = u.Attendances.Count(a => a.IsPresent),
                TotalCertificates = u.Certificates.Count,
                TotalVolunteerHours = u.TotalVolunteerHours,
                JoinDate = u.JoinDate
            }).ToList();

            return reports;
        }

        public async Task<DashboardStatisticsDto> GetDashboardStatisticsAsync()
        {
            var totalEvents = await _context.Events.CountAsync();
            var totalUsers = await _context.Users.CountAsync();
            var totalRegistrations = await _context.Registrations.CountAsync();
            var totalAttendance = await _context.Attendances.CountAsync(a => a.IsPresent);
            var totalCertificates = await _context.Certificates.CountAsync();
            var totalFeedback = await _context.Feedbacks.CountAsync();

            var approvedEvents = await _context.Events.CountAsync(e => e.IsApproved);
            var pendingEvents = await _context.Events.CountAsync(e => !e.IsApproved);

            var upcomingEvents = await _context.Events
                .CountAsync(e => e.EventDate >= DateTime.Now && e.IsApproved);

            var pastEvents = await _context.Events
                .CountAsync(e => e.EventDate < DateTime.Now && e.IsApproved);

            var averageAttendanceRate = totalRegistrations > 0 
                ? Math.Round((double)totalAttendance / totalRegistrations * 100, 2)
                : 0;

            var averageRating = totalFeedback > 0
                ? Math.Round(await _context.Feedbacks.AverageAsync(f => f.Rating), 2)
                : 0;

            return new DashboardStatisticsDto
            {
                TotalEvents = totalEvents,
                ApprovedEvents = approvedEvents,
                PendingEvents = pendingEvents,
                UpcomingEvents = upcomingEvents,
                PastEvents = pastEvents,
                TotalUsers = totalUsers,
                TotalRegistrations = totalRegistrations,
                TotalAttendance = totalAttendance,
                AttendanceRate = averageAttendanceRate,
                TotalCertificates = totalCertificates,
                TotalFeedback = totalFeedback,
                AverageRating = averageRating
            };
        }

        public async Task<List<EventStatisticsDto>> GetEventStatisticsAsync()
        {
            var events = await _context.Events
                .Include(e => e.Registrations)
                .Include(e => e.Attendances)
                .Include(e => e.Feedbacks)
                .OrderByDescending(e => e.EventDate)
                .ToListAsync();

            var statistics = events.Select(e => new EventStatisticsDto
            {
                EventID = e.EventID,
                Title = e.Title,
                EventDate = e.EventDate,
                TotalRegistrations = e.Registrations.Count,
                TotalAttendance = e.Attendances.Count(a => a.IsPresent),
                AttendanceRate = e.Registrations.Count > 0
                    ? Math.Round((double)e.Attendances.Count(a => a.IsPresent) / e.Registrations.Count * 100, 2)
                    : 0,
                AverageRating = e.Feedbacks.Any() ? Math.Round(e.Feedbacks.Average(f => f.Rating), 2) : 0,
                FeedbackCount = e.Feedbacks.Count
            }).ToList();

            return statistics;
        }
    }

    // DTOs
    public class EventReportDto
    {
        public int EventID { get; set; }
        public string Title { get; set; }
        public DateTime EventDate { get; set; }
        public int TotalRegistrations { get; set; }
        public int ConfirmedRegistrations { get; set; }
        public int WaitlistCount { get; set; }
        public int TotalAttendance { get; set; }
        public double AttendanceRate { get; set; }
        public int TotalFeedback { get; set; }
        public double AverageRating { get; set; }
        public int CertificatesIssued { get; set; }
        public int VolunteerHours { get; set; }
    }

    public class UserReportDto
    {
        public string UserId { get; set; }
        public string FullName { get; set; }
        public string Email { get; set; }
        public string UniversityID { get; set; }
        public string UserType { get; set; }
        public int TotalRegistrations { get; set; }
        public int TotalAttendance { get; set; }
        public int TotalCertificates { get; set; }
        public int TotalVolunteerHours { get; set; }
        public DateTime JoinDate { get; set; }
    }

    public class DashboardStatisticsDto
    {
        public int TotalEvents { get; set; }
        public int ApprovedEvents { get; set; }
        public int PendingEvents { get; set; }
        public int UpcomingEvents { get; set; }
        public int PastEvents { get; set; }
        public int TotalUsers { get; set; }
        public int TotalRegistrations { get; set; }
        public int TotalAttendance { get; set; }
        public double AttendanceRate { get; set; }
        public int TotalCertificates { get; set; }
        public int TotalFeedback { get; set; }
        public double AverageRating { get; set; }
    }

    public class EventStatisticsDto
    {
        public int EventID { get; set; }
        public string Title { get; set; }
        public DateTime EventDate { get; set; }
        public int TotalRegistrations { get; set; }
        public int TotalAttendance { get; set; }
        public double AttendanceRate { get; set; }
        public double AverageRating { get; set; }
        public int FeedbackCount { get; set; }
    }
}
