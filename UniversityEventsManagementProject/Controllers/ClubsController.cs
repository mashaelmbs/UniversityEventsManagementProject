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
    public class ClubsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IUserService _userService;

        public ClubsController(ApplicationDbContext context, IUserService userService)
        {
            _context = context;
            _userService = userService;
        }

        [AllowAnonymous]
        public async Task<IActionResult> Index()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var isAdmin = User.IsInRole("Admin");
            
            // للأدمن: عرض جميع الأندية (نشطة وغير نشطة)
            // للطلاب: عرض الأندية النشطة فقط
            var allClubs = await _context.Clubs
                .Where(c => isAdmin || c.IsActive)
                .Include(c => c.AdminUser)
                .Include(c => c.Members)
                .ThenInclude(m => m.User)
                .OrderBy(c => c.ClubName)
                .ToListAsync();

            // للأدمن: إرجاع جميع الأندية بدون تقسيم
            if (isAdmin)
            {
                var adminModel = new
                {
                    AllClubs = allClubs,
                    IsAdmin = true
                };
                return View(adminModel);
            }

            // للطلاب: تقسيم الأندية إلى منضم إليها ومتاحة
            List<ClubMember> userMemberships = new List<ClubMember>();
            if (!string.IsNullOrEmpty(userId))
            {
                userMemberships = await _context.ClubMembers
                    .Where(m => m.UserID == userId)
                    .Include(m => m.Club)
                    .ThenInclude(c => c.Members)
                    .ToListAsync();
            }

            // للطلاب: عرض فقط الأندية المعتمدة (Approved) في قسم "منضم إليها"
            var approvedMemberships = userMemberships.Where(m => m.Status == "Approved").ToList();
            var joinedClubIds = approvedMemberships.Select(m => m.ClubID).ToList();
            var joinedClubs = allClubs.Where(c => joinedClubIds.Contains(c.ClubID)).ToList();
            var availableClubs = allClubs.Where(c => !joinedClubIds.Contains(c.ClubID)).ToList();

            var studentModel = new
            {
                JoinedClubs = joinedClubs,
                AvailableClubs = availableClubs,
                UserMemberships = userMemberships,
                IsAdmin = false
            };

            return View(studentModel);
        }

        [AllowAnonymous]
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var club = await _context.Clubs
                .Include(c => c.AdminUser)
                .Include(c => c.Members)
                .ThenInclude(m => m.User)
                .FirstOrDefaultAsync(c => c.ClubID == id);

            if (club == null)
            {
                return NotFound();
            }

            // عرض الأعضاء المعتمدين فقط للطلاب
            if (!User.IsInRole("Admin"))
            {
                club.Members = club.Members?.Where(m => m.Status == "Approved").ToList();
            }

            return View(club);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> JoinClub(int clubId)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var club = await _context.Clubs.FindAsync(clubId);

            if (club == null)
            {
                return NotFound();
            }

            // منع الأدمن من الانضمام للنادي لأنه هو منشئه
            if (User.IsInRole("Admin") || club.AdminUserID == userId)
            {
                TempData["Error"] = "لا يمكنك الانضمام إلى النادي لأنك منشئه";
                return RedirectToAction("Details", new { id = clubId });
            }

            var existingMember = await _context.ClubMembers
                .FirstOrDefaultAsync(m => m.ClubID == clubId && m.UserID == userId);

            if (existingMember != null)
            {
                if (existingMember.Status == "Pending")
                {
                    TempData["Error"] = "لديك طلب انتظار موافقة قيد المراجعة";
                }
                else if (existingMember.Status == "Approved")
                {
                    TempData["Error"] = "أنت عضو في هذا النادي بالفعل";
                }
                else if (existingMember.Status == "Rejected")
                {
                    // يمكن إعادة الطلب بعد الرفض
                    existingMember.Status = "Pending";
                    existingMember.JoinDate = DateTime.Now;
                    _context.Update(existingMember);
                    await _context.SaveChangesAsync();
                    TempData["Success"] = "تم إرسال طلب الانضمام مرة أخرى، في انتظار موافقة الأدمن";
                }
                return RedirectToAction("Details", new { id = clubId });
            }

            var member = new ClubMember
            {
                ClubID = clubId,
                UserID = userId,
                JoinDate = DateTime.Now,
                Role = "Member",
                Status = "Pending" // طلب انتظار موافقة
            };

            _context.ClubMembers.Add(member);
            await _context.SaveChangesAsync();

            TempData["Success"] = "تم إرسال طلب الانضمام، في انتظار موافقة الأدمن";
            return RedirectToAction("Details", new { id = clubId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> LeaveClub(int clubId)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var member = await _context.ClubMembers
                .FirstOrDefaultAsync(m => m.ClubID == clubId && m.UserID == userId);

            if (member == null)
            {
                return NotFound();
            }

            _context.ClubMembers.Remove(member);
            await _context.SaveChangesAsync();

            TempData["Success"] = "تم مغادرة النادي";
            return RedirectToAction("Index");
        }

        // GET: My Clubs
        public async Task<IActionResult> MyClubs()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var clubs = await _context.ClubMembers
                .Where(m => m.UserID == userId)
                .Include(m => m.Club)
                .ToListAsync();

            return View(clubs);
        }

        [Authorize(Roles = "Admin")]
        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Create([Bind("ClubName,Description,LogoUrl")] Club club)
        {
            if (ModelState.IsValid)
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                club.AdminUserID = userId;
                club.CreatedDate = DateTime.Now;
                club.IsActive = true;

                _context.Add(club);
                await _context.SaveChangesAsync();

                TempData["Success"] = "تم إنشاء النادي بنجاح";
                return RedirectToAction(nameof(Index));
            }
            return View(club);
        }

        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var club = await _context.Clubs
                .Include(c => c.AdminUser)
                .Include(c => c.Members)
                .FirstOrDefaultAsync(c => c.ClubID == id);
            
            if (club == null)
            {
                return NotFound();
            }

            return View(club);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Edit(int id, [Bind("ClubID,ClubName,Description,LogoUrl,IsActive")] Club club)
        {
            if (id != club.ClubID)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    var existingClub = await _context.Clubs.FindAsync(id);
                    existingClub.ClubName = club.ClubName;
                    existingClub.Description = club.Description;
                    existingClub.LogoUrl = club.LogoUrl;
                    existingClub.IsActive = club.IsActive;

                    _context.Update(existingClub);
                    await _context.SaveChangesAsync();

                    TempData["Success"] = "تم تحديث النادي بنجاح";
                    return RedirectToAction(nameof(Index));
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!ClubExists(club.ClubID))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
            }
            return View(club);
        }

        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var club = await _context.Clubs
                .Include(c => c.Members)
                .FirstOrDefaultAsync(c => c.ClubID == id);

            if (club == null)
            {
                return NotFound();
            }

            return View(club);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var club = await _context.Clubs
                .Include(c => c.Members)
                .FirstOrDefaultAsync(c => c.ClubID == id);

            if (club != null)
            {
                // Remove all members first
                _context.ClubMembers.RemoveRange(club.Members);
                _context.Clubs.Remove(club);
                await _context.SaveChangesAsync();

                TempData["Success"] = "تم حذف النادي بنجاح";
            }

            return RedirectToAction(nameof(Index));
        }

        private bool ClubExists(int id)
        {
            return _context.Clubs.Any(c => c.ClubID == id);
        }
    }
}
