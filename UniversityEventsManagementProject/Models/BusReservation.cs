using System;

namespace UniversityEventsManagement.Models
{
    public class BusReservation
    {
        public int BusReservationID { get; set; }
        public int BusID { get; set; }
        public string? UserID { get; set; }
        public DateTime ReservationDate { get; set; }
        public int PassengerCount { get; set; }
        public string? Status { get; set; } // Confirmed, Cancelled

        // Navigation properties
        public virtual Bus? Bus { get; set; }
        public virtual ApplicationUser? User { get; set; }
    }
}
