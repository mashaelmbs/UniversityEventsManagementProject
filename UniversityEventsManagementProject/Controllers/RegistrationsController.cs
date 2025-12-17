#nullable disable
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using UniversityEventsManagement.Data;
using UniversityEventsManagement.Models;
using UniversityEventsManagement.Services;

namespace UniversityEventsManagement.Controllers
{
    [Authorize]
    public class RegistrationsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IUserService _userService;

        public RegistrationsController(ApplicationDbContext context, IUserService userService)
        {
            _context = context;
            _userService = userService;
        }

        public async Task<IActionResult> MyEvents()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var registrations = await _context.Registrations
                .Where(r => r.UserID == userId)
                .Include(r => r.Event)
                .ToListAsync();

            return View(registrations);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(int eventId, int guestCount = 0)
        {
            if (!User.Identity?.IsAuthenticated ?? true)
            {
                TempData["Error"] = "يجب تسجيل الدخول أولاً";
                return RedirectToAction("Login", "Account");
            }

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
            {
                TempData["Error"] = "خطأ في بيانات المستخدم. يرجى تسجيل الدخول مرة أخرى";
                return RedirectToAction("Login", "Account");
            }

            var existingUser = await _userService.FindByIdAsync(userId);
            if (existingUser == null)
            {
                TempData["Error"] = "خطأ في بيانات المستخدم. يرجى تسجيل الدخول مرة أخرى";
                return RedirectToAction("Login", "Account");
            }

            var @event = await _context.Events.FindAsync(eventId);

            if (@event == null)
            {
                return NotFound();
            }

            var existingRegistration = await _context.Registrations
                .FirstOrDefaultAsync(r => r.EventID == eventId && r.UserID == userId);

            if (existingRegistration != null)
            {
                TempData["Error"] = "أنت مسجل بالفعل في هذه الفعالية";
                return RedirectToAction("Details", "Events", new { id = eventId });
            }

            var registrationCount = await _context.Registrations
                .Where(r => r.EventID == eventId && r.Status == "Confirmed")
                .CountAsync();

            var status = registrationCount >= @event.MaxCapacity ? "Waitlist" : "Confirmed";

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

            var user = await _userService.FindByIdAsync(userId);
            var notification = new Notification
            {
                UserID = userId,
                Message = $"تم تسجيلك في الفعالية: {@event.Title}",
                SentDate = DateTime.Now,
                IsRead = false,
                NotificationType = "RegistrationConfirmed",
                EventID = eventId
            };
            _context.Notifications.Add(notification);
            await _context.SaveChangesAsync();

            TempData["Success"] = status == "Confirmed" 
                ? "تم تسجيلك بنجاح في الفعالية" 
                : "تم إضافتك لقائمة الانتظار";

            return RedirectToAction("Details", "Events", new { id = eventId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CancelRegistration(int registrationId)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var registration = await _context.Registrations
                .Include(r => r.Event)
                .FirstOrDefaultAsync(r => r.RegistrationID == registrationId && r.UserID == userId);

            if (registration == null)
            {
                return NotFound();
            }

            registration.Status = "Cancelled";
            _context.Update(registration);
            await _context.SaveChangesAsync();

            TempData["Success"] = "تم إلغاء التسجيل بنجاح";
            return RedirectToAction(nameof(MyEvents));
        }

        // GET: Registration Details
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var registration = await _context.Registrations
                .Include(r => r.Event)
                .Include(r => r.User)
                .FirstOrDefaultAsync(r => r.RegistrationID == id && r.UserID == userId);

            if (registration == null)
            {
                return NotFound();
            }

            return View(registration);
        }
    }
}
