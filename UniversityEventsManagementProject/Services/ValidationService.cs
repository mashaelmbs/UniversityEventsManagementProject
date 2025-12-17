#nullable disable
using UniversityEventsManagement.Data;
using UniversityEventsManagement.Models;
using Microsoft.EntityFrameworkCore;

namespace UniversityEventsManagement.Services
{
    public interface IValidationService
    {
        Task<(bool IsValid, string Message)> ValidateEventRegistrationAsync(int eventId, string userId);
        Task<(bool IsValid, string Message)> ValidateAttendanceAsync(int eventId, string userId);
        Task<(bool IsValid, string Message)> ValidateBusReservationAsync(int busId, int passengerCount);
        Task<(bool IsValid, string Message)> ValidateFeedbackAsync(int eventId, string userId);
        Task<(bool IsValid, string Message)> ValidateClubJoinAsync(int clubId, string userId);
    }

    public class ValidationService : IValidationService
    {
        private readonly ApplicationDbContext _context;

        public ValidationService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<(bool IsValid, string Message)> ValidateEventRegistrationAsync(int eventId, string userId)
        {
            // Check if event exists
            var @event = await _context.Events.FindAsync(eventId);
            if (@event == null)
                return (false, "الفعالية غير موجودة");

            // Check if event is approved
            if (!@event.IsApproved)
                return (false, "الفعالية غير معتمدة");

            // Check if event date is in the future
            if (@event.EventDate < DateTime.Now)
                return (false, "الفعالية قد انتهت");

            // Check if user already registered
            var existingRegistration = await _context.Registrations
                .FirstOrDefaultAsync(r => r.EventID == eventId && r.UserID == userId && r.Status != "Cancelled");
            
            if (existingRegistration != null)
                return (false, "أنت مسجل بالفعل في هذه الفعالية");

            return (true, "صحيح");
        }

        public async Task<(bool IsValid, string Message)> ValidateAttendanceAsync(int eventId, string userId)
        {
            // Check if event exists
            var @event = await _context.Events.FindAsync(eventId);
            if (@event == null)
                return (false, "الفعالية غير موجودة");

            // Check if user is registered
            var registration = await _context.Registrations
                .FirstOrDefaultAsync(r => r.EventID == eventId && r.UserID == userId && r.Status == "Confirmed");
            
            if (registration == null)
                return (false, "أنت غير مسجل في هذه الفعالية");

            // Check if already checked in
            var existingAttendance = await _context.Attendances
                .FirstOrDefaultAsync(a => a.EventID == eventId && a.UserID == userId);
            
            if (existingAttendance != null)
                return (false, "تم تسجيل حضورك بالفعل");

            return (true, "صحيح");
        }

        public async Task<(bool IsValid, string Message)> ValidateBusReservationAsync(int busId, int passengerCount)
        {
            // Check if bus exists
            var bus = await _context.Buses
                .Include(b => b.Reservations)
                .FirstOrDefaultAsync(b => b.BusID == busId);
            
            if (bus == null)
                return (false, "الحافلة غير موجودة");

            // Check capacity
            var totalReserved = bus.Reservations.Sum(r => r.PassengerCount);
            if (totalReserved + passengerCount > bus.Capacity)
                return (false, $"السعة المتاحة {bus.Capacity - totalReserved} فقط");

            return (true, "صحيح");
        }

        public async Task<(bool IsValid, string Message)> ValidateFeedbackAsync(int eventId, string userId)
        {
            // Check if event exists
            var @event = await _context.Events.FindAsync(eventId);
            if (@event == null)
                return (false, "الفعالية غير موجودة");

            // Check if user attended
            var attendance = await _context.Attendances
                .FirstOrDefaultAsync(a => a.EventID == eventId && a.UserID == userId && a.IsPresent);
            
            if (attendance == null)
                return (false, "يجب أن تحضر الفعالية لتقييمها");

            // Check if already provided feedback
            var existingFeedback = await _context.Feedbacks
                .FirstOrDefaultAsync(f => f.EventID == eventId && f.UserID == userId);
            
            if (existingFeedback != null)
                return (false, "لقد قمت بتقييم هذه الفعالية بالفعل");

            return (true, "صحيح");
        }

        public async Task<(bool IsValid, string Message)> ValidateClubJoinAsync(int clubId, string userId)
        {
            // Check if club exists
            var club = await _context.Clubs.FindAsync(clubId);
            if (club == null)
                return (false, "النادي غير موجود");

            // Check if club is active
            if (!club.IsActive)
                return (false, "النادي غير نشط");

            // Check if already member
            var existingMember = await _context.ClubMembers
                .FirstOrDefaultAsync(m => m.ClubID == clubId && m.UserID == userId);
            
            if (existingMember != null)
                return (false, "أنت عضو في هذا النادي بالفعل");

            return (true, "صحيح");
        }
    }
}
