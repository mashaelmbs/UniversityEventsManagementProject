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
    public class FeedbackController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IValidationService _validationService;

        public FeedbackController(ApplicationDbContext context, IValidationService validationService)
        {
            _context = context;
            _validationService = validationService;
        }

        // GET: Submit Feedback
        public async Task<IActionResult> Create(int eventId)
        {
            if (!User.Identity?.IsAuthenticated ?? true)
            {
                TempData["Error"] = "يجب تسجيل الدخول أولاً";
                return RedirectToAction("Login", "Account");
            }

            var @event = await _context.Events.FindAsync(eventId);
            if (@event == null)
            {
                return NotFound();
            }

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            
            // Validate feedback: check if user attended the event
            var validationResult = await _validationService.ValidateFeedbackAsync(eventId, userId);
            if (!validationResult.IsValid)
            {
                TempData["Error"] = validationResult.Message;
                return RedirectToAction("Details", "Events", new { id = eventId });
            }

            // Create a new Feedback model with EventID set
            var feedback = new Feedback
            {
                EventID = eventId
            };

            // Pass event info via ViewBag for display
            ViewBag.Event = @event;
            ViewBag.EventId = eventId;

            return View(feedback);
        }

        // POST: Submit Feedback
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(int eventId, [Bind("Rating,Comment")] Feedback feedback)
        {
            var @event = await _context.Events.FindAsync(eventId);
            if (@event == null)
            {
                return NotFound();
            }

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            // Validate feedback: check if user attended the event
            var validationResult = await _validationService.ValidateFeedbackAsync(eventId, userId);
            if (!validationResult.IsValid)
            {
                TempData["Error"] = validationResult.Message;
                return RedirectToAction("Details", "Events", new { id = eventId });
            }

            if (ModelState.IsValid)
            {
                feedback.EventID = eventId;
                feedback.UserID = userId;
                feedback.SubmittedDate = DateTime.Now;

                _context.Feedbacks.Add(feedback);
                await _context.SaveChangesAsync();

                TempData["Success"] = "شكراً على تقييمك!";
                return RedirectToAction("Details", "Events", new { id = eventId });
            }

            // If model state is invalid, return view with event info
            ViewBag.Event = @event;
            ViewBag.EventId = eventId;
            return View(feedback);
        }

        // GET: View Feedback (Admin)
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> ViewEventFeedback(int eventId)
        {
            // Get the event first to ensure it exists
            var @event = await _context.Events.FindAsync(eventId);
            if (@event == null)
            {
                return NotFound();
            }

            var feedbacks = await _context.Feedbacks
                .Where(f => f.EventID == eventId)
                .Include(f => f.User)
                .Include(f => f.Event)
                .OrderByDescending(f => f.SubmittedDate)
                .ToListAsync();

            // Create a model that includes both event and feedbacks
            var model = new
            {
                Event = @event,
                Feedbacks = feedbacks
            };

            return View(model);
        }

        // GET: All Feedback (Admin)
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> AllFeedback()
        {
            var feedbacks = await _context.Feedbacks
                .Include(f => f.Event)
                .Include(f => f.User)
                .OrderByDescending(f => f.SubmittedDate)
                .ToListAsync();

            return View(feedbacks);
        }

        // GET: My Feedback
        public async Task<IActionResult> MyFeedback()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var feedbacks = await _context.Feedbacks
                .Where(f => f.UserID == userId)
                .Include(f => f.Event)
                .OrderByDescending(f => f.SubmittedDate)
                .ToListAsync();

            return View(feedbacks);
        }

        // POST: Delete Feedback (Admin)
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(int feedbackId)
        {
            var feedback = await _context.Feedbacks.FindAsync(feedbackId);
            if (feedback == null)
            {
                return NotFound();
            }

            var eventId = feedback.EventID;
            _context.Feedbacks.Remove(feedback);
            await _context.SaveChangesAsync();

            TempData["Success"] = "تم حذف التقييم بنجاح";
            return RedirectToAction(nameof(ViewEventFeedback), new { eventId });
        }
    }
}
