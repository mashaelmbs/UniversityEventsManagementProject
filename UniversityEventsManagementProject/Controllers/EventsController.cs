#nullable disable
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
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
    public class EventsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IUserService _userService;
        private readonly IImageUploadService _imageUploadService;
        private readonly ILogger<EventsController> _logger;

        public EventsController(ApplicationDbContext context, IUserService userService, IImageUploadService imageUploadService, ILogger<EventsController> logger)
        {
            _context = context;
            _userService = userService;
            _imageUploadService = imageUploadService;
            _logger = logger;
        }

        public async Task<IActionResult> Index(string searchString, string filterType)
        {
            ViewBag.SearchString = searchString ?? "";
            ViewBag.FilterType = filterType ?? "";

            var events = _context.Events
                .Where(e => e.IsApproved)
                .Include(e => e.CreatedByUser)
                .Include(e => e.Registrations)
                .AsQueryable();

            if (!string.IsNullOrEmpty(searchString))
            {
                events = events.Where(e => e.Title.Contains(searchString) || e.Description.Contains(searchString));
            }

            if (!string.IsNullOrEmpty(filterType))
            {
                events = events.Where(e => e.EventType == filterType);
            }

            events = events.OrderByDescending(e => e.EventDate);
            return View(await events.ToListAsync());
        }

        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var @event = await _context.Events
                .Include(e => e.CreatedByUser)
                .Include(e => e.Registrations)
                .Include(e => e.Feedbacks)
                .Include(e => e.Attendances)
                .FirstOrDefaultAsync(m => m.EventID == id);

            if (@event == null)
            {
                return NotFound();
            }

            // Check if current user attended this event (for showing feedback button)
            if (User.Identity?.IsAuthenticated ?? false)
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                var userAttendance = await _context.Attendances
                    .FirstOrDefaultAsync(a => a.EventID == id && a.UserID == userId && a.IsPresent);
                ViewBag.UserAttended = userAttendance != null;
            }
            else
            {
                ViewBag.UserAttended = false;
            }

            return View(@event);
        }

        [Authorize(Roles = "Admin")]
        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Create([Bind("Title,Description,EventDate,Venue,MaxCapacity,EventType,ImageUrl,VolunteerHours")] Event @event, IFormFile eventImage)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                    @event.CreatedByUserID = userId;
                    @event.CreatedDate = DateTime.Now;
                    @event.IsApproved = true;
                    
                    // Generate unique secret for QR code
                    @event.Secret = Guid.NewGuid().ToString("N");

                    _context.Add(@event);
                    await _context.SaveChangesAsync();

                    if (eventImage != null && eventImage.Length > 0)
                    {
                        @event.ImageUrl = await _imageUploadService.UploadEventImageAsync(eventImage, @event.EventID);
                        _context.Update(@event);
                        await _context.SaveChangesAsync();
                    }

                    await NotifyUsersOfNewEvent(@event);

                    TempData["Success"] = "تم إنشاء الفعالية بنجاح";
                    return RedirectToAction(nameof(Index));
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError("", $"خطأ أثناء إنشاء الفعالية: {ex.Message}");
                    return View(@event);
                }
            }
            return View(@event);
        }

        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var @event = await _context.Events.FindAsync(id);
            if (@event == null)
            {
                return NotFound();
            }
            return View(@event);
        }

        // POST: Events/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Edit(int id, [Bind("EventID,Title,Description,EventDate,Venue,MaxCapacity,EventType,ImageUrl,VolunteerHours,IsApproved")] Event @event, IFormFile eventImage)
        {
            if (id != @event.EventID)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    var existingEvent = await _context.Events.FindAsync(id);
                    if (existingEvent == null)
                    {
                        return NotFound();
                    }

                    var currentImageUrl = existingEvent.ImageUrl;

                    existingEvent.Title = @event.Title;
                    existingEvent.Description = @event.Description;
                    existingEvent.EventDate = @event.EventDate;
                    existingEvent.Venue = @event.Venue;
                    existingEvent.MaxCapacity = @event.MaxCapacity;
                    existingEvent.EventType = @event.EventType;
                    existingEvent.VolunteerHours = @event.VolunteerHours;
                    existingEvent.IsApproved = @event.IsApproved;

                    if (eventImage != null && eventImage.Length > 0)
                    {
                        try
                        {
                            if (!string.IsNullOrEmpty(currentImageUrl))
                            {
                                await _imageUploadService.DeleteImageAsync(currentImageUrl);
                            }

                            var uploadedImageUrl = await _imageUploadService.UploadEventImageAsync(eventImage, id);
                            if (!string.IsNullOrEmpty(uploadedImageUrl))
                            {
                                existingEvent.ImageUrl = uploadedImageUrl;
                            }
                        }
                        catch (Exception imgEx)
                        {
                            _logger.LogError($"Image upload error: {imgEx.Message}");
                            ModelState.AddModelError("", $"خطأ في رفع الصورة: {imgEx.Message}");
                            existingEvent.ImageUrl = currentImageUrl;
                            return View(existingEvent);
                        }
                    }
                    else
                    {
                        if (!string.IsNullOrEmpty(@event.ImageUrl))
                        {
                            existingEvent.ImageUrl = @event.ImageUrl;
                        }
                        else if (string.IsNullOrEmpty(existingEvent.ImageUrl))
                        {
                            existingEvent.ImageUrl = currentImageUrl;
                        }
                    }

                    _context.Entry(existingEvent).State = EntityState.Modified;
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!EventExists(@event.EventID))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Error updating event: {ex.Message}");
                    ModelState.AddModelError("", $"خطأ أثناء تحديث الفعالية: {ex.Message}");
                    var eventToReturn = await _context.Events.FindAsync(id);
                    if (eventToReturn != null)
                    {
                        return View(eventToReturn);
                    }
                    return View(@event);
                }
                TempData["Success"] = "تم تحديث الفعالية بنجاح";
                return RedirectToAction(nameof(Index));
            }
            var eventWithData = await _context.Events.FindAsync(id);
            if (eventWithData != null)
            {
                eventWithData.Title = @event.Title;
                eventWithData.Description = @event.Description;
                eventWithData.EventDate = @event.EventDate;
                eventWithData.Venue = @event.Venue;
                eventWithData.MaxCapacity = @event.MaxCapacity;
                eventWithData.EventType = @event.EventType;
                eventWithData.VolunteerHours = @event.VolunteerHours;
                eventWithData.IsApproved = @event.IsApproved;
                return View(eventWithData);
            }
            return View(@event);
        }

        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var @event = await _context.Events
                .FirstOrDefaultAsync(m => m.EventID == id);
            if (@event == null)
            {
                return NotFound();
            }

            return View(@event);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var @event = await _context.Events.FindAsync(id);
            if (@event == null)
            {
                return NotFound();
            }
            
            _context.Events.Remove(@event);
            await _context.SaveChangesAsync();
            
            TempData["Success"] = "تم حذف الفعالية بنجاح";
            return RedirectToAction("ManageEvents", "Admin");
        }

        private bool EventExists(int id)
        {
            return _context.Events.Any(e => e.EventID == id);
        }

        private async Task NotifyUsersOfNewEvent(Event @event)
        {
            var users = await _context.Users.ToListAsync();
            foreach (var user in users)
            {
                var notification = new Notification
                {
                    UserID = user.Id,
                    Message = $"فعالية جديدة: {@event.Title}",
                    SentDate = DateTime.Now,
                    IsRead = false,
                    NotificationType = "EventCreated",
                    EventID = @event.EventID
                };
                _context.Notifications.Add(notification);
            }
            await _context.SaveChangesAsync();
        }
    }
}
