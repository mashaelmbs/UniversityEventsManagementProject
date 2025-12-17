#nullable disable
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QRCoder;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using UniversityEventsManagement.Data;
using UniversityEventsManagement.Models;
using UniversityEventsManagement.Services;
using UniversityEventsManagement.ViewModels;

namespace UniversityEventsManagement.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IUserService _userService;
        private readonly IReportService _reportService;

        public AdminController(ApplicationDbContext context, IUserService userService, IReportService reportService)
        {
            _context = context;
            _userService = userService;
            _reportService = reportService;
        }

        // GET: Admin Dashboard
        public async Task<IActionResult> Dashboard()
        {
            var totalEvents = await _context.Events.CountAsync();
            var totalRegistrations = await _context.Registrations.CountAsync();
            var totalUsers = await _context.Users.CountAsync();
            var totalAttendance = await _context.Attendances.CountAsync();
            var totalCertificates = await _context.Certificates.CountAsync();

            var upcomingEvents = await _context.Events
                .Where(e => e.EventDate >= DateTime.Now && e.IsApproved)
                .Include(e => e.Registrations)
                .OrderBy(e => e.EventDate)
                .Take(5)
                .ToListAsync();

            var model = new
            {
                TotalEvents = totalEvents,
                TotalRegistrations = totalRegistrations,
                TotalUsers = totalUsers,
                TotalAttendance = totalAttendance,
                TotalCertificates = totalCertificates,
                ApprovedEvents = await _context.Events.Where(e => e.IsApproved).CountAsync(),
                UpcomingEvents = upcomingEvents
            };

            return View(model);
        }

        // GET: Manage Events
        public async Task<IActionResult> ManageEvents()
        {
            var events = await _context.Events
                .Include(e => e.CreatedByUser)
                .Include(e => e.Registrations)
                .OrderByDescending(e => e.CreatedDate)
                .ToListAsync();

            return View(events);
        }

        // GET: Manage Users
        public async Task<IActionResult> ManageUsers(string searchString, string statusFilter, string userTypeFilter, DateTime? dateFrom, DateTime? dateTo)
        {
            var usersQuery = _context.Users.AsQueryable();

            // Search filter
            if (!string.IsNullOrEmpty(searchString))
            {
                usersQuery = usersQuery.Where(u =>
                    (u.FirstName != null && u.FirstName.Contains(searchString)) ||
                    (u.LastName != null && u.LastName.Contains(searchString)) ||
                    (u.Email != null && u.Email.Contains(searchString)) ||
                    (u.UniversityID != null && u.UniversityID.Contains(searchString)) ||
                    (u.PhoneNumber != null && u.PhoneNumber.Contains(searchString))
                );
            }

            // Status filter
            if (!string.IsNullOrEmpty(statusFilter) && statusFilter != "الكل")
            {
                if (statusFilter == "نشط")
                {
                    usersQuery = usersQuery.Where(u => u.IsActive);
                }
                else if (statusFilter == "غير نشط")
                {
                    usersQuery = usersQuery.Where(u => !u.IsActive);
                }
            }

            // User type filter
            if (!string.IsNullOrEmpty(userTypeFilter) && userTypeFilter != "الكل")
            {
                var userTypeMap = new Dictionary<string, string>
                {
                    { "طالب", "Student" },
                    { "إداري", "Admin" }
                };

                if (userTypeMap.ContainsKey(userTypeFilter))
                {
                    usersQuery = usersQuery.Where(u => u.UserType == userTypeMap[userTypeFilter]);
                }
            }

            // Date range filter
            if (dateFrom.HasValue)
            {
                usersQuery = usersQuery.Where(u => u.JoinDate >= dateFrom.Value);
            }

            if (dateTo.HasValue)
            {
                usersQuery = usersQuery.Where(u => u.JoinDate <= dateTo.Value.AddDays(1));
            }

            var users = await usersQuery.OrderByDescending(u => u.JoinDate).ToListAsync();

            ViewBag.SearchString = searchString;
            ViewBag.StatusFilter = statusFilter;
            ViewBag.UserTypeFilter = userTypeFilter;
            ViewBag.DateFrom = dateFrom;
            ViewBag.DateTo = dateTo;

            return View(users);
        }

        // POST: Change User Role
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangeUserRole(string userId, string newRole)
        {
            var user = await _userService.FindByIdAsync(userId);
            if (user == null)
            {
                return NotFound();
            }

            user.UserType = newRole;
            var result = await _userService.UpdateUserAsync(user);

            if (result)
            {
                TempData["Success"] = "تم تحديث دور المستخدم بنجاح";
            }
            else
            {
                TempData["Error"] = "حدث خطأ أثناء تحديث دور المستخدم";
            }

            return RedirectToAction(nameof(ManageUsers));
        }

        // GET: View User
        public async Task<IActionResult> ViewUser(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return NotFound();
            }

            var user = await _userService.FindByIdAsync(id);
            if (user == null)
            {
                return NotFound();
            }

            var userRole = await _userService.GetUserRoleAsync(user);
            var userRoles = string.IsNullOrEmpty(userRole) ? new List<string>() : new List<string> { userRole };
            var userRegistrations = await _context.Registrations
                .Where(r => r.UserID == id)
                .Include(r => r.Event)
                .ToListAsync();

            var userAttendances = await _context.Attendances
                .Where(a => a.UserID == id && a.IsPresent)
                .Include(a => a.Event)
                .ToListAsync();

            var userCertificates = await _context.Certificates
                .Where(c => c.UserID == id)
                .Include(c => c.Event)
                .ToListAsync();

            var model = new
            {
                User = user,
                Roles = userRoles,
                Registrations = userRegistrations,
                Attendances = userAttendances,
                Certificates = userCertificates
            };

            return View(model);
        }

        // GET: Edit User
        public async Task<IActionResult> EditUser(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return NotFound();
            }

            var user = await _userService.FindByIdAsync(id);
            if (user == null)
            {
                return NotFound();
            }

            var userRole = await _userService.GetUserRoleAsync(user);
            ViewBag.AllRoles = new[] { "Admin", "Student" };
            ViewBag.CurrentRoles = string.IsNullOrEmpty(userRole) ? new List<string>() : new List<string> { userRole };

            return View(user);
        }

        // POST: Edit User
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditUser(string id, ApplicationUser model)
        {
            if (id != model.Id)
            {
                return NotFound();
            }

            var user = await _userService.FindByIdAsync(id);
            if (user == null)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                user.FirstName = model.FirstName;
                user.LastName = model.LastName;
                user.Email = model.Email;
                user.PhoneNumber = model.PhoneNumber;
                user.UniversityID = model.UniversityID;
                user.Department = model.Department;
                user.UserType = model.UserType;
                user.IsActive = model.IsActive;

                var result = await _userService.UpdateUserAsync(user);
                if (result)
                {
                    TempData["Success"] = "تم تحديث بيانات المستخدم بنجاح";
                    return RedirectToAction(nameof(ManageUsers));
                }
                else
                {
                    ModelState.AddModelError(string.Empty, "حدث خطأ أثناء تحديث البيانات");
                }
            }

            var userRole = await _userService.GetUserRoleAsync(user);
            ViewBag.AllRoles = new[] { "Admin", "Student" };
            ViewBag.CurrentRoles = string.IsNullOrEmpty(userRole) ? new List<string>() : new List<string> { userRole };

            return View(user);
        }

        // GET: Create User
        public IActionResult CreateUser()
        {
            ViewBag.AllRoles = new[] { "Admin", "Student" };
            return View();
        }

        // POST: Create User
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateUser(CreateUserViewModel model)
        {
            if (ModelState.IsValid)
            {
                // Check if email already exists
                var existingUser = await _userService.FindByEmailAsync(model.Email);
                if (existingUser != null)
                {
                    ModelState.AddModelError("Email", "البريد الإلكتروني مستخدم بالفعل");
                    ViewBag.AllRoles = new[] { "Admin", "Student" };
                    return View(model);
                }

                // Check if UniversityID already exists (if provided)
                if (!string.IsNullOrEmpty(model.UniversityID))
                {
                    var existingUserByID = await _context.Users
                        .FirstOrDefaultAsync(u => u.UniversityID == model.UniversityID);
                    if (existingUserByID != null)
                    {
                        ModelState.AddModelError("UniversityID", "الرقم الجامعي مستخدم بالفعل");
                        ViewBag.AllRoles = new[] { "Admin", "Student" };
                        return View(model);
                    }
                }

                var user = new ApplicationUser
                {
                    Id = Guid.NewGuid().ToString(),
                    Email = model.Email?.ToLowerInvariant().Trim() ?? "",
                    FirstName = model.FirstName,
                    LastName = model.LastName,
                    UniversityID = model.UniversityID?.Trim(),
                    UserType = model.UserType,
                    Department = model.Department ?? "",
                    PhoneNumber = model.PhoneNumber,
                    JoinDate = DateTime.Now,
                    IsActive = true,
                    EmailConfirmed = true, // Admin creates users as confirmed
                    TotalVolunteerHours = 0
                };

                var result = await _userService.CreateUserAsync(user, model.Password);
                if (result)
                {
                    TempData["Success"] = $"تم إنشاء المستخدم ({model.UserType}) بنجاح";
                    return RedirectToAction(nameof(ManageUsers));
                }
                else
                {
                    ModelState.AddModelError(string.Empty, "حدث خطأ أثناء إنشاء المستخدم. يرجى المحاولة مرة أخرى.");
                }
            }

            ViewBag.AllRoles = new[] { "Admin", "Student" };
            return View(model);
        }

        // POST: Delete User
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteUser(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return NotFound();
            }

            var user = await _userService.FindByIdAsync(id);
            if (user == null)
            {
                return NotFound();
            }

            // Prevent deleting the current user
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (id == currentUserId)
            {
                TempData["Error"] = "لا يمكنك حذف حسابك الخاص";
                return RedirectToAction(nameof(ManageUsers));
            }

            var result = await _userService.DeleteUserAsync(user);
            if (result)
            {
                TempData["Success"] = "تم حذف المستخدم بنجاح";
            }
            else
            {
                TempData["Error"] = "حدث خطأ أثناء حذف المستخدم";
            }

            return RedirectToAction(nameof(ManageUsers));
        }

        // GET: Select Event for Attendance
        public async Task<IActionResult> SelectEventForAttendance()
        {
            var events = await _context.Events
                .Where(e => e.IsApproved)
                .OrderByDescending(e => e.EventDate)
                .ToListAsync();
            return View(events);
        }

        // GET: View Attendance for Event
        public async Task<IActionResult> ViewAttendance(int eventId)
        {
            var @event = await _context.Events
                .Include(e => e.Registrations)
                    .ThenInclude(r => r.User)
                .Include(e => e.Attendances)
                    .ThenInclude(a => a.User)
                .FirstOrDefaultAsync(e => e.EventID == eventId);

            if (@event == null)
            {
                return NotFound();
            }

            return View(@event);
        }

        // GET: Issue Certificates
        public async Task<IActionResult> IssueCertificates(int eventId)
        {
            var @event = await _context.Events
                .Include(e => e.Attendances)
                .ThenInclude(a => a.User)
                .FirstOrDefaultAsync(e => e.EventID == eventId);

            if (@event == null)
            {
                return NotFound();
            }

            return View(@event);
        }

        // POST: Issue Certificates
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> IssueCertificatesConfirm(int eventId)
        {
            var @event = await _context.Events
                .Include(e => e.Attendances)
                .FirstOrDefaultAsync(e => e.EventID == eventId);

            if (@event == null)
            {
                return NotFound();
            }

            foreach (var attendance in @event.Attendances.Where(a => a.IsPresent))
            {
                var existingCert = await _context.Certificates
                    .FirstOrDefaultAsync(c => c.UserID == attendance.UserID && c.EventID == eventId);

                if (existingCert == null)
                {
                    var certificate = new Certificate
                    {
                        UserID = attendance.UserID,
                        EventID = eventId,
                        IssueDate = DateTime.Now,
                        CertificateNumber = $"CERT-{eventId}-{Guid.NewGuid().ToString().Substring(0, 8)}",
                        IsDownloaded = false
                    };

                    _context.Certificates.Add(certificate);

                    // Update volunteer hours
                    var user = await _userService.FindByIdAsync(attendance.UserID);
                    if (user != null)
                    {
                        user.TotalVolunteerHours += @event.VolunteerHours;
                        await _userService.UpdateUserAsync(user);
                    }
                }
            }

            await _context.SaveChangesAsync();
            TempData["Success"] = "تم إصدار الشهادات بنجاح";
            return RedirectToAction(nameof(ManageEvents));
        }

        // GET: View Reports
        public async Task<IActionResult> Reports()
        {
            var events = await _context.Events
                .Include(e => e.Registrations)
                .Include(e => e.Attendances)
                .Include(e => e.Feedbacks)
                .OrderByDescending(e => e.EventDate)
                .ToListAsync();

            return View(events);
        }

        // GET: Event Statistics
        public async Task<IActionResult> EventStatistics(int eventId)
        {
            var @event = await _context.Events
                .Include(e => e.Registrations)
                .Include(e => e.Attendances)
                .Include(e => e.Feedbacks)
                .FirstOrDefaultAsync(e => e.EventID == eventId);

            if (@event == null)
            {
                return NotFound();
            }

            var model = new
            {
                Event = @event,
                TotalRegistrations = @event.Registrations.Count,
                ConfirmedRegistrations = @event.Registrations.Count(r => r.Status == "Confirmed"),
                WaitlistCount = @event.Registrations.Count(r => r.Status == "Waitlist"),
                AttendanceCount = @event.Attendances.Count(a => a.IsPresent),
                AverageRating = @event.Feedbacks.Any() ? @event.Feedbacks.Average(f => f.Rating) : 0
            };

            return View(model);
        }

        // GET: Send Notifications
        public async Task<IActionResult> SendNotifications()
        {
            var events = await _context.Events.ToListAsync();
            return View(events);
        }

        // POST: Send Notifications
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SendNotificationsConfirm(string message, int? eventId)
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
                    NotificationType = "AdminNotification",
                    EventID = eventId
                };
                _context.Notifications.Add(notification);
            }

            await _context.SaveChangesAsync();
            TempData["Success"] = "تم إرسال الإشعارات بنجاح";
            return RedirectToAction(nameof(Dashboard));
        }

        // GET: Manage Notifications
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> ManageNotifications()
        {
            var allNotifications = await _context.Notifications
                .Include(n => n.User)
                .Include(n => n.Event)
                .OrderByDescending(n => n.SentDate)
                .ToListAsync();

            // Group notifications by message and date to count recipients
            var notificationGroups = allNotifications
                .GroupBy(n => new { n.Message, Date = n.SentDate.Date })
                .ToList();

            var totalNotifications = notificationGroups.Count;
            var sentNotifications = notificationGroups.Count(g => g.First().SentDate <= DateTime.Now);
            var pendingNotifications = notificationGroups.Count(g => g.First().SentDate > DateTime.Now);
            var failedNotifications = 0; // يمكن إضافة منطق لتحديد الإشعارات الفاشلة لاحقاً

            ViewBag.TotalNotifications = totalNotifications;
            ViewBag.SentNotifications = sentNotifications;
            ViewBag.PendingNotifications = pendingNotifications;
            ViewBag.FailedNotifications = failedNotifications;
            ViewBag.NotificationGroups = notificationGroups;

            return View(allNotifications);
        }

        // GET: Select Event for QR Code Generation
        public async Task<IActionResult> SelectEventForQR()
        {
            var events = await _context.Events
                .Where(e => e.IsApproved)
                .OrderByDescending(e => e.EventDate)
                .ToListAsync();
            return View(events);
        }

        // GET: Select Event for Statistics
        public async Task<IActionResult> SelectEventForStats()
        {
            var events = await _context.Events
                .Where(e => e.IsApproved)
                .OrderByDescending(e => e.EventDate)
                .ToListAsync();
            return View(events);
        }

        // GET: Generate QR Code for Event
        public async Task<IActionResult> GenerateEventQR(int eventId)
        {
            var @event = await _context.Events
                .FirstOrDefaultAsync(e => e.EventID == eventId);

            if (@event == null)
            {
                return NotFound();
            }

            // Ensure event has a secret (for existing events without secret)
            if (string.IsNullOrEmpty(@event.Secret))
            {
                @event.Secret = Guid.NewGuid().ToString("N");
                _context.Update(@event);
                await _context.SaveChangesAsync();
            }

            // Generate QR code URL using secret instead of eventId
            var baseUrl = $"{Request.Scheme}://{Request.Host}";
            var qrUrl = $"{baseUrl}/Attendance/ScanQR?secret={@event.Secret}";

            // Generate QR code using QRCoder
            using (QRCodeGenerator qrGenerator = new QRCodeGenerator())
            {
                QRCodeData qrData = qrGenerator.CreateQrCode(qrUrl, QRCodeGenerator.ECCLevel.Q);
                using (PngByteQRCode qrCode = new PngByteQRCode(qrData))
                {
                    byte[] qrBytes = qrCode.GetGraphic(20);
                    string qrBase64 = Convert.ToBase64String(qrBytes);
                    ViewBag.QRCodeBase64 = $"data:image/png;base64,{qrBase64}";
                }
            }

            ViewBag.QRUrl = qrUrl;
            return View(@event);
        }

        // GET: Get QR Code Image Only (for printing)
        public async Task<IActionResult> GetQRCodeImage(int eventId)
        {
            var @event = await _context.Events
                .FirstOrDefaultAsync(e => e.EventID == eventId);

            if (@event == null)
            {
                return NotFound();
            }

            // Ensure event has a secret
            if (string.IsNullOrEmpty(@event.Secret))
            {
                @event.Secret = Guid.NewGuid().ToString("N");
                _context.Update(@event);
                await _context.SaveChangesAsync();
            }

            // Generate QR code URL using secret
            var baseUrl = $"{Request.Scheme}://{Request.Host}";
            var qrUrl = $"{baseUrl}/Attendance/ScanQR?secret={@event.Secret}";

            // Generate QR code using QRCoder
            using (QRCodeGenerator qrGenerator = new QRCodeGenerator())
            {
                QRCodeData qrData = qrGenerator.CreateQrCode(qrUrl, QRCodeGenerator.ECCLevel.Q);
                using (PngByteQRCode qrCode = new PngByteQRCode(qrData))
                {
                    byte[] qrBytes = qrCode.GetGraphic(20);
                    return File(qrBytes, "image/png", $"QRCode-{@event.EventID}.png");
                }
            }
        }

        // GET: Manage Club Members (Approve/Reject Join Requests)
        public async Task<IActionResult> ManageClubMembers()
        {
            var pendingMembers = await _context.ClubMembers
                .Where(m => m.Status == "Pending")
                .Include(m => m.Club)
                .Include(m => m.User)
                .OrderByDescending(m => m.JoinDate)
                .ToListAsync();

            var clubsWithPending = await _context.Clubs
                .Include(c => c.Members)
                .ThenInclude(m => m.User)
                .Where(c => c.Members != null && c.Members.Any(m => m.Status == "Pending"))
                .ToListAsync();

            var model = new ManageClubMembersViewModel
            {
                PendingMembers = pendingMembers,
                Clubs = clubsWithPending
            };

            return View(model);
        }

        // POST: Approve Club Member
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApproveClubMember(int memberId)
        {
            var member = await _context.ClubMembers
                .Include(m => m.User)
                .Include(m => m.Club)
                .FirstOrDefaultAsync(m => m.ClubMemberID == memberId);

            if (member == null)
            {
                return NotFound();
            }

            member.Status = "Approved";
            _context.Update(member);
            await _context.SaveChangesAsync();

            TempData["Success"] = $"تم قبول طلب الانضمام للطالب {member.User?.FirstName} {member.User?.LastName}";
            return RedirectToAction(nameof(ManageClubMembers));
        }

        // POST: Reject Club Member
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RejectClubMember(int memberId)
        {
            var member = await _context.ClubMembers
                .Include(m => m.User)
                .Include(m => m.Club)
                .FirstOrDefaultAsync(m => m.ClubMemberID == memberId);

            if (member == null)
            {
                return NotFound();
            }

            member.Status = "Rejected";
            _context.Update(member);
            await _context.SaveChangesAsync();

            TempData["Success"] = $"تم رفض طلب الانضمام للطالب {member.User?.FirstName} {member.User?.LastName}";
            return RedirectToAction(nameof(ManageClubMembers));
        }

        // GET: View Club Members by Club
        public async Task<IActionResult> ViewClubMembers(int clubId)
        {
            var club = await _context.Clubs
                .Include(c => c.Members)
                .ThenInclude(m => m.User)
                .FirstOrDefaultAsync(c => c.ClubID == clubId);

            if (club == null)
            {
                return NotFound();
            }

            var membersList = club.Members ?? new List<ClubMember>();
            var pendingList = membersList.Where(m => m.Status == "Pending").ToList();
            var approvedList = membersList.Where(m => m.Status == "Approved").ToList();
            var rejectedList = membersList.Where(m => m.Status == "Rejected").ToList();

            var model = new ViewClubMembersViewModel
            {
                Club = club,
                PendingMembers = pendingList,
                ApprovedMembers = approvedList,
                RejectedMembers = rejectedList
            };

            return View(model);
        }
    }

    public class CreateUserViewModel
    {
        [Required(ErrorMessage = "الاسم الأول مطلوب")]
        public string FirstName { get; set; }

        [Required(ErrorMessage = "اسم العائلة مطلوب")]
        public string LastName { get; set; }

        [Required(ErrorMessage = "البريد الإلكتروني مطلوب")]
        [EmailAddress(ErrorMessage = "البريد الإلكتروني غير صحيح")]
        public string Email { get; set; }

        public string UniversityID { get; set; }

        [Required(ErrorMessage = "كلمة المرور مطلوبة")]
        [StringLength(100, MinimumLength = 6, ErrorMessage = "كلمة المرور يجب أن تكون بين 6 و 100 حرف")]
        public string Password { get; set; }

        [Required(ErrorMessage = "تأكيد كلمة المرور مطلوب")]
        [Compare("Password", ErrorMessage = "كلمات المرور غير متطابقة")]
        public string ConfirmPassword { get; set; }

        [Required(ErrorMessage = "نوع المستخدم مطلوب")]
        public string UserType { get; set; }

        public string Department { get; set; }

        public string PhoneNumber { get; set; }
    }
}
