using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Threading.Tasks;
using UniversityEventsManagement.Data;
using UniversityEventsManagement.Models;

namespace UniversityEventsManagement.Controllers
{
    public class ContactController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ContactController(ApplicationDbContext context)
        {
            _context = context;
        }

        public IActionResult Index()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Submit([Bind("FullName,Email,PhoneNumber,Subject,Message,InquiryType")] Contact contact)
        {
            if (ModelState.IsValid)
            {
                contact.SubmittedDate = DateTime.Now;
                contact.IsResolved = false;

                _context.Add(contact);
                await _context.SaveChangesAsync();

                TempData["Success"] = "تم إرسال رسالتك بنجاح. شكراً لتواصلك معنا!";
                return RedirectToAction(nameof(Index));
            }

            return View("Index", contact);
        }

        [Microsoft.AspNetCore.Authorization.Authorize(Roles = "Admin")]
        public async Task<IActionResult> AdminInquiries()
        {
            var inquiries = await _context.Contacts
                .OrderByDescending(c => c.SubmittedDate)
                .ToListAsync();

            return View(inquiries);
        }

        [Microsoft.AspNetCore.Authorization.Authorize(Roles = "Admin")]
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var contact = await _context.Contacts.FindAsync(id);
            if (contact == null)
            {
                return NotFound();
            }

            return View(contact);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Microsoft.AspNetCore.Authorization.Authorize(Roles = "Admin")]
        public async Task<IActionResult> Respond(int id, [Bind("AdminResponse")] Contact contactResponse)
        {
            var contact = await _context.Contacts.FindAsync(id);
            if (contact == null)
            {
                return NotFound();
            }

            if (!string.IsNullOrWhiteSpace(contactResponse.AdminResponse))
            {
                contact.AdminResponse = contactResponse.AdminResponse;
                contact.ResponseDate = DateTime.Now;
                contact.IsResolved = true;

                _context.Update(contact);
                await _context.SaveChangesAsync();

                TempData["Success"] = "تم إرسال الرد بنجاح";
            }

            return RedirectToAction(nameof(Details), new { id = contact.ContactID });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Microsoft.AspNetCore.Authorization.Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(int id)
        {
            var contact = await _context.Contacts.FindAsync(id);
            if (contact != null)
            {
                _context.Contacts.Remove(contact);
                await _context.SaveChangesAsync();
                TempData["Success"] = "تم حذف الاستفسار بنجاح";
            }

            return RedirectToAction(nameof(AdminInquiries));
        }
    }
}
