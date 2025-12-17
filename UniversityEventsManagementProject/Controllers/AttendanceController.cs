#nullable disable
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using UniversityEventsManagement.Data;
using UniversityEventsManagement.Models;
using UniversityEventsManagement.Resources;
using UniversityEventsManagement.Services;

namespace UniversityEventsManagement.Controllers
{
    public class AttendanceController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IUserService _userService;
        private readonly IStringLocalizer<SharedResources> _localizer;

        public AttendanceController(ApplicationDbContext context, IUserService userService, IStringLocalizer<SharedResources> localizer)
        {
            _context = context;
            _userService = userService;
            _localizer = localizer;
        }

        // GET: Scan QR Code (Public - no authorization required)
        // Uses secret instead of eventId for security
        public async Task<IActionResult> ScanQR(string secret)
        {
            if (string.IsNullOrEmpty(secret))
            {
                TempData["Error"] = "رابط QR Code غير صحيح";
                return RedirectToAction("Index", "Events");
            }

            // Find event by secret
            var @event = await _context.Events
                .FirstOrDefaultAsync(e => e.Secret == secret);

            if (@event == null)
            {
                TempData["Error"] = "الفعالية غير موجودة أو الرابط غير صحيح";
                return RedirectToAction("Index", "Events");
            }

            // Check if user is authenticated
            if (!User.Identity?.IsAuthenticated ?? true)
            {
                // Redirect to login with return URL using secret
                TempData["Info"] = "يجب تسجيل الدخول أولاً لتسجيل الحضور";
                return RedirectToAction("Login", "Account", new { returnUrl = $"/Attendance/ScanQR?secret={secret}" });
            }

            // User is authenticated, proceed with attendance
            return await ProcessQRAttendance(@event.EventID);
        }

        // Process QR attendance for authenticated users
        // This method automatically marks attendance when accessed
        private async Task<IActionResult> ProcessQRAttendance(int eventId)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            
            if (string.IsNullOrEmpty(userId))
            {
                TempData["Error"] = "خطأ في التحقق من الهوية";
                return RedirectToAction("Login", "Account");
            }

            var @event = await _context.Events
                .Include(e => e.Registrations)
                .FirstOrDefaultAsync(e => e.EventID == eventId);

            if (@event == null)
            {
                TempData["Error"] = "الفعالية غير موجودة";
                return RedirectToAction("Index", "Events");
            }

            // Check if user is registered
            var registration = await _context.Registrations
                .FirstOrDefaultAsync(r => r.EventID == eventId && r.UserID == userId && r.Status == "Confirmed");

            if (registration == null)
            {
                TempData["Error"] = "يجب أن تكون مسجلاً في هذه الفعالية لتسجيل الحضور";
                return RedirectToAction("Details", "Events", new { id = eventId });
            }

            // Check if event date/time is valid
            // Allow attendance only after event start time
            if (DateTime.Now < @event.EventDate)
            {
                TempData["Error"] = _localizer["CannotCheckInBeforeEvent"];
                return RedirectToAction("CheckIn", new { eventId });
            }

            // Calculate event end time (assuming 1 hour duration, can be made configurable)
            var eventEndTime = @event.EventDate.AddHours(1);
            var attendanceDeadline = eventEndTime.AddMinutes(30); // 30 minutes after event ends

            // Check if attendance deadline has passed
            if (DateTime.Now > attendanceDeadline)
            {
                TempData["Error"] = _localizer["AttendanceDeadlinePassed"];
                return RedirectToAction("CheckIn", new { eventId });
            }

            // Check if already attended - this prevents duplicate attendance
            var existingAttendance = await _context.Attendances
                .FirstOrDefaultAsync(a => a.EventID == eventId && a.UserID == userId && a.IsPresent);

            bool isNewAttendance = false;

            if (existingAttendance != null)
            {
                // User already attended - prevent duplicate attendance
                TempData["Info"] = "تم تسجيل حضورك بالفعل في هذه الفعالية";
                return RedirectToAction("CheckIn", new { eventId });
            }
            else
            {
                // Check if there's an attendance record that's not marked as present
                var existingRecord = await _context.Attendances
                    .FirstOrDefaultAsync(a => a.EventID == eventId && a.UserID == userId);

                if (existingRecord != null)
                {
                    // Update existing record
                    existingRecord.IsPresent = true;
                    existingRecord.CheckInTime = DateTime.Now;
                    _context.Update(existingRecord);
                }
                else
                {
                    // Create new attendance record - automatic attendance marking
                    var attendance = new Attendance
                    {
                        EventID = eventId,
                        UserID = userId,
                        CheckInTime = DateTime.Now,
                        IsPresent = true,
                        QRCode = Guid.NewGuid().ToString()
                    };
                    _context.Attendances.Add(attendance);
                    isNewAttendance = true;
                }

                await _context.SaveChangesAsync();

                // Send notification only for new attendance
                if (isNewAttendance)
                {
                    var notification = new Notification
                    {
                        UserID = userId,
                        Message = $"تم تسجيل حضورك في الفعالية: {@event.Title}. ستحصل على {@event.VolunteerHours} ساعات تطوعية",
                        SentDate = DateTime.Now,
                        IsRead = false,
                        NotificationType = "AttendanceConfirmed",
                        EventID = eventId
                    };
                    _context.Notifications.Add(notification);
                    await _context.SaveChangesAsync();
                }

                TempData["Success"] = $"تم تسجيل حضورك بنجاح! ستحصل على {@event.VolunteerHours} ساعات تطوعية عند إصدار الشهادة";
            }

            return RedirectToAction("CheckIn", new { eventId });
        }

        [Authorize]
        public async Task<IActionResult> CheckIn(int eventId)
        {
            if (!User.Identity?.IsAuthenticated ?? true)
            {
                TempData["Error"] = "يجب تسجيل الدخول أولاً";
                return RedirectToAction("Login", "Account");
            }

            var @event = await _context.Events
                .Include(e => e.Registrations)
                .FirstOrDefaultAsync(e => e.EventID == eventId);
            if (@event == null)
            {
                return NotFound();
            }

            return View(@event);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RecordAttendance(int eventId)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var @event = await _context.Events
                .Include(e => e.Registrations)
                .FirstOrDefaultAsync(e => e.EventID == eventId);

            if (@event == null)
            {
                TempData["Error"] = "الفعالية غير موجودة";
                return RedirectToAction("Index", "Events");
            }

            var registration = await _context.Registrations
                .FirstOrDefaultAsync(r => r.EventID == eventId && r.UserID == userId && r.Status == "Confirmed");

            if (registration == null)
            {
                TempData["Error"] = "يجب أن تكون مسجلاً في هذه الفعالية لتسجيل الحضور";
                return RedirectToAction("Details", "Events", new { id = eventId });
            }

            // Check if event date/time is valid
            // Allow attendance only after event start time
            if (DateTime.Now < @event.EventDate)
            {
                TempData["Error"] = _localizer["CannotCheckInBeforeEvent"];
                return RedirectToAction("CheckIn", new { eventId });
            }

            // Calculate event end time (assuming 1 hour duration, can be made configurable)
            var eventEndTime = @event.EventDate.AddHours(1);
            var attendanceDeadline = eventEndTime.AddMinutes(30); // 30 minutes after event ends

            // Check if attendance deadline has passed
            if (DateTime.Now > attendanceDeadline)
            {
                TempData["Error"] = _localizer["AttendanceDeadlinePassed"];
                return RedirectToAction("CheckIn", new { eventId });
            }

            var existingAttendance = await _context.Attendances
                .FirstOrDefaultAsync(a => a.EventID == eventId && a.UserID == userId);

            if (existingAttendance != null)
            {
                if (existingAttendance.IsPresent)
                {
                    TempData["Error"] = "تم تسجيل حضورك بالفعل في هذه الفعالية";
                    return RedirectToAction("CheckIn", new { eventId });
                }
                else
                {
                    existingAttendance.IsPresent = true;
                    existingAttendance.CheckInTime = DateTime.Now;
                    _context.Update(existingAttendance);
                }
            }
            else
            {
                var attendance = new Attendance
                {
                    EventID = eventId,
                    UserID = userId,
                    CheckInTime = DateTime.Now,
                    IsPresent = true,
                    QRCode = Guid.NewGuid().ToString()
                };
                _context.Attendances.Add(attendance);
            }

            await _context.SaveChangesAsync();

            var user = await _userService.FindByIdAsync(userId);
            var notification = new Notification
            {
                UserID = userId,
                Message = $"تم تسجيل حضورك في الفعالية: {@event.Title}. ستحصل على {@event.VolunteerHours} ساعات تطوعية",
                SentDate = DateTime.Now,
                IsRead = false,
                NotificationType = "AttendanceConfirmed",
                EventID = eventId
            };
            _context.Notifications.Add(notification);
            await _context.SaveChangesAsync();

            TempData["Success"] = $"تم تسجيل حضورك بنجاح! ستحصل على {@event.VolunteerHours} ساعات تطوعية عند إصدار الشهادة";
            return RedirectToAction("CheckIn", new { eventId });
        }

        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> ViewEventAttendance(int eventId)
        {
            var attendances = await _context.Attendances
                .Where(a => a.EventID == eventId)
                .Include(a => a.User)
                .Include(a => a.Event)
                .ToListAsync();

            if (!attendances.Any())
            {
                return NotFound();
            }

            return View(attendances);
        }

        // POST: Mark Attendance (Admin)
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> MarkAttendance(int eventId, string userId, bool isPresent)
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
                _context.Update(attendance);
            }

            await _context.SaveChangesAsync();
            TempData["Success"] = "تم تحديث الحضور بنجاح";
            return RedirectToAction(nameof(ViewEventAttendance), new { eventId });
        }

        // GET: My Attendance
        public async Task<IActionResult> MyAttendance()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var attendances = await _context.Attendances
                .Where(a => a.UserID == userId && a.IsPresent)
                .Include(a => a.Event)
                .OrderByDescending(a => a.CheckInTime)
                .ToListAsync();

            return View(attendances);
        }
    }
}
