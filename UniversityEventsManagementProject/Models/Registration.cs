using System;

namespace UniversityEventsManagement.Models
{
    public class Registration
    {
        public int RegistrationID { get; set; }
        public int EventID { get; set; }
        public string? UserID { get; set; }
        public DateTime RegistrationDate { get; set; }
        public string? Status { get; set; } // Confirmed, Waitlist, Cancelled
        public int? GuestCount { get; set; }

        // Navigation properties
        public virtual Event? Event { get; set; }
        public virtual ApplicationUser? User { get; set; }
    }
}
