#nullable disable
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using UniversityEventsManagement.Data;
using UniversityEventsManagement.Models;
using UniversityEventsManagement.Services;
using UniversityEventsManagementProject.Models;

namespace UniversityEventsManagement.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly ApplicationDbContext _context;
        private readonly IUserService _userService;
        private readonly IStringLocalizer<HomeController> _localizer;

        public HomeController(ILogger<HomeController> logger, ApplicationDbContext context, IUserService userService, IStringLocalizer<HomeController> localizer)
        {
            _logger = logger;
            _context = context;
            _userService = userService;
            _localizer = localizer;
        }

        public async Task<IActionResult> Index()
        {
            // جلب الفعاليات القادمة أولاً
            var upcomingEvents = await _context.Events
                .Where(e => e.IsApproved && e.EventDate >= DateTime.Now)
                .Include(e => e.CreatedByUser)
                .Include(e => e.Registrations)
                .OrderBy(e => e.EventDate)
                .Take(6)
                .ToListAsync();

            // إذا لم تكن هناك فعاليات قادمة، اعرض أحدث الفعاليات
            if (upcomingEvents.Count == 0)
            {
                upcomingEvents = await _context.Events
                    .Where(e => e.IsApproved)
                    .Include(e => e.CreatedByUser)
                    .Include(e => e.Registrations)
                    .OrderByDescending(e => e.EventDate)
                    .Take(6)
                    .ToListAsync();
            }

            // إذا لا زالت فارغة، اعرض أي فعاليات متاحة
            if (upcomingEvents.Count == 0)
            {
                upcomingEvents = await _context.Events
                    .Include(e => e.CreatedByUser)
                    .Include(e => e.Registrations)
                    .OrderByDescending(e => e.CreatedDate)
                    .Take(6)
                    .ToListAsync();
            }

            var model = new
            {
                UpcomingEvents = upcomingEvents,
                TotalEvents = await _context.Events.CountAsync(),
                TotalUsers = await _context.Users.CountAsync(),
                TotalRegistrations = await _context.Registrations.CountAsync()
            };

            return View(model);
        }

        [Authorize]
        public async Task<IActionResult> Dashboard()
        {
            if (!User.Identity.IsAuthenticated)
            {
                return RedirectToAction("Login", "Account");
            }

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
            {
                return RedirectToAction("Login", "Account");
            }

            var user = await _userService.FindByIdAsync(userId);

            if (user == null)
            {
                return RedirectToAction("Login", "Account");
            }

            // إعادة توجيه الأدمن إلى لوحة التحكم الخاصة به
            if (user.UserType == "Admin")
            {
                return RedirectToAction("Dashboard", "Admin");
            }

            try
            {
                var userRegistrations = await _context.Registrations
                    .Where(r => r.UserID == userId)
                    .Include(r => r.Event)
                    .ToListAsync();

                var userCertificates = await _context.Certificates
                    .Where(c => c.UserID == userId)
                    .Include(c => c.Event)
                    .ToListAsync();

                var userNotifications = await _context.Notifications
                    .Where(n => n.UserID == userId)
                    .OrderByDescending(n => n.SentDate)
                    .Take(5)
                    .ToListAsync();

                // Get user's club memberships
                var userClubs = await _context.ClubMembers
                    .Where(cm => cm.UserID == userId)
                    .Include(cm => cm.Club)
                    .ToListAsync();

                // Get upcoming events
                var upcomingEvents = userRegistrations
                    .Where(r => r.Event != null && r.Event.EventDate >= DateTime.Now)
                    .Select(r => r.Event)
                    .OrderBy(e => e.EventDate)
                    .Take(3)
                    .ToList();

                // Get completed events
                var completedEvents = userRegistrations
                    .Where(r => r.Event != null && r.Event.EventDate < DateTime.Now)
                    .Count();

                // Calculate attendance rate
                var totalAttended = await _context.Attendances
                    .Where(a => a.UserID == userId && a.IsPresent)
                    .CountAsync();
                var totalRegistered = userRegistrations.Count;
                var attendanceRate = totalRegistered > 0 ? (int)((double)totalAttended / totalRegistered * 100) : 0;

                var model = new
                {
                    User = user,
                    RegisteredEvents = userRegistrations ?? new List<Registration>(),
                    Certificates = userCertificates ?? new List<Certificate>(),
                    Notifications = userNotifications ?? new List<Notification>(),
                    Clubs = userClubs ?? new List<ClubMember>(),
                    UpcomingEvents = upcomingEvents ?? new List<Event>(),
                    TotalVolunteerHours = user.TotalVolunteerHours,
                    UnreadNotifications = await _context.Notifications
                        .Where(n => n.UserID == userId && !n.IsRead)
                        .CountAsync(),
                    CompletedEvents = completedEvents,
                    AttendanceRate = attendanceRate
                };

                return View(model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading dashboard for user {UserId}", userId);
                TempData["Error"] = "حدث خطأ أثناء تحميل لوحة التحكم. يرجى المحاولة مرة أخرى.";
                return RedirectToAction("Index", "Home");
            }
        }

        [Authorize]
        public async Task<IActionResult> EventHistory()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var user = await _userService.FindByIdAsync(userId);

            if (user == null)
            {
                return NotFound();
            }

            var allRegistrations = await _context.Registrations
                .Where(r => r.UserID == userId)
                .Include(r => r.Event)
                .ThenInclude(e => e.Attendances)
                .Include(r => r.Event)
                .ThenInclude(e => e.Certificates)
                .ToListAsync();

            var completedEvents = allRegistrations
                .Where(r => r.Event.EventDate < DateTime.Now)
                .ToList();

            var upcomingEvents = allRegistrations
                .Where(r => r.Event.EventDate >= DateTime.Now)
                .ToList();

            var model = new
            {
                TotalEvents = allRegistrations.Count,
                CompletedEvents = completedEvents.Count,
                UpcomingEvents = upcomingEvents.Count,
                TotalVolunteerHours = user.TotalVolunteerHours,
                AllEvents = allRegistrations,
                CompletedEventsList = completedEvents,
                UpcomingEventsList = upcomingEvents
            };

            return View(model);
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new UniversityEventsManagementProject.Models.ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }

        [HttpPost]
        public IActionResult SetLanguage(string culture, string returnUrl)
        {
            Response.Cookies.Append(
                CookieRequestCultureProvider.DefaultCookieName,
                CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(culture)),
                new CookieOptions { Expires = DateTimeOffset.UtcNow.AddYears(1) }
            );

            return LocalRedirect(returnUrl);
        }
    }
}
