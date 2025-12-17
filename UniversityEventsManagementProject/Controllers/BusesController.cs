#nullable disable
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using UniversityEventsManagement.Data;
using UniversityEventsManagement.Models;

namespace UniversityEventsManagement.Controllers
{
    [Authorize]
    public class BusesController : Controller
    {
        private readonly ApplicationDbContext _context;

        public BusesController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Buses for Event
        public async Task<IActionResult> Index(int eventId)
        {
            var buses = await _context.Buses
                .Where(b => b.EventID == eventId)
                .Include(b => b.Event)
                .Include(b => b.Reservations)
                .ToListAsync();

            if (!buses.Any())
            {
                return NotFound();
            }

            return View(buses);
        }

        // POST: Reserve Bus
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ReserveBus(int busId, int passengerCount)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var bus = await _context.Buses
                .Include(b => b.Reservations)
                .FirstOrDefaultAsync(b => b.BusID == busId);

            if (bus == null)
            {
                return NotFound();
            }

            // Check if user already reserved
            var existingReservation = await _context.BusReservations
                .FirstOrDefaultAsync(r => r.BusID == busId && r.UserID == userId);

            if (existingReservation != null)
            {
                TempData["Error"] = "لديك حجز بالفعل في هذه الحافلة";
                return RedirectToAction("Index", new { eventId = bus.EventID });
            }

            // Check capacity
            var totalReserved = bus.Reservations.Sum(r => r.PassengerCount);
            if (totalReserved + passengerCount > bus.Capacity)
            {
                TempData["Error"] = "السعة المتاحة غير كافية";
                return RedirectToAction("Index", new { eventId = bus.EventID });
            }

            var reservation = new BusReservation
            {
                BusID = busId,
                UserID = userId,
                ReservationDate = DateTime.Now,
                PassengerCount = passengerCount,
                Status = "Confirmed"
            };

            _context.BusReservations.Add(reservation);
            bus.CurrentPassengers += passengerCount;
            _context.Update(bus);
            await _context.SaveChangesAsync();

            TempData["Success"] = "تم حجز الحافلة بنجاح";
            return RedirectToAction("Index", new { eventId = bus.EventID });
        }

        // POST: Cancel Reservation
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CancelReservation(int reservationId)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var reservation = await _context.BusReservations
                .Include(r => r.Bus)
                .FirstOrDefaultAsync(r => r.BusReservationID == reservationId && r.UserID == userId);

            if (reservation == null)
            {
                return NotFound();
            }

            var eventId = reservation.Bus.EventID;
            reservation.Status = "Cancelled";
            reservation.Bus.CurrentPassengers -= reservation.PassengerCount;

            _context.Update(reservation);
            _context.Update(reservation.Bus);
            await _context.SaveChangesAsync();

            TempData["Success"] = "تم إلغاء الحجز";
            return RedirectToAction("Index", new { eventId });
        }

        // GET: My Bus Reservations
        public async Task<IActionResult> MyReservations()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var reservations = await _context.BusReservations
                .Where(r => r.UserID == userId && r.Status == "Confirmed")
                .Include(r => r.Bus)
                .ThenInclude(b => b.Event)
                .ToListAsync();

            return View(reservations);
        }

        // GET: Create Bus (Admin)
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Create(int eventId)
        {
            var @event = await _context.Events.FindAsync(eventId);
            if (@event == null)
            {
                return NotFound();
            }

            return View(new Bus { EventID = eventId });
        }

        // POST: Create Bus (Admin)
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Create([Bind("EventID,BusNumber,Capacity,DepartureTime,DepartureLocation,DestinationLocation")] Bus bus)
        {
            if (ModelState.IsValid)
            {
                bus.CurrentPassengers = 0;
                _context.Add(bus);
                await _context.SaveChangesAsync();

                TempData["Success"] = "تم إضافة الحافلة بنجاح";
                return RedirectToAction("Index", "Events", new { id = bus.EventID });
            }

            return View(bus);
        }

        // GET: Edit Bus (Admin)
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var bus = await _context.Buses.FindAsync(id);
            if (bus == null)
            {
                return NotFound();
            }

            return View(bus);
        }

        // POST: Edit Bus (Admin)
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Edit(int id, [Bind("BusID,EventID,BusNumber,Capacity,DepartureTime,DepartureLocation,DestinationLocation")] Bus bus)
        {
            if (id != bus.BusID)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    var existingBus = await _context.Buses.FindAsync(id);
                    existingBus.BusNumber = bus.BusNumber;
                    existingBus.Capacity = bus.Capacity;
                    existingBus.DepartureTime = bus.DepartureTime;
                    existingBus.DepartureLocation = bus.DepartureLocation;
                    existingBus.DestinationLocation = bus.DestinationLocation;

                    _context.Update(existingBus);
                    await _context.SaveChangesAsync();

                    TempData["Success"] = "تم تحديث الحافلة بنجاح";
                    return RedirectToAction("Index", "Events", new { id = bus.EventID });
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!BusExists(bus.BusID))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
            }

            return View(bus);
        }

        // GET: Delete Bus (Admin)
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var bus = await _context.Buses
                .Include(b => b.Event)
                .Include(b => b.Reservations)
                .FirstOrDefaultAsync(b => b.BusID == id);

            if (bus == null)
            {
                return NotFound();
            }

            return View(bus);
        }

        // POST: Delete Bus (Admin)
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var bus = await _context.Buses
                .Include(b => b.Reservations)
                .FirstOrDefaultAsync(b => b.BusID == id);

            if (bus != null)
            {
                var eventId = bus.EventID;
                
                // Remove all reservations first
                _context.BusReservations.RemoveRange(bus.Reservations);
                _context.Buses.Remove(bus);
                await _context.SaveChangesAsync();

                TempData["Success"] = "تم حذف الحافلة بنجاح";
                return RedirectToAction("Index", "Events", new { id = eventId });
            }

            return RedirectToAction("Index", "Events");
        }

        private bool BusExists(int id)
        {
            return _context.Buses.Any(b => b.BusID == id);
        }
    }
}
