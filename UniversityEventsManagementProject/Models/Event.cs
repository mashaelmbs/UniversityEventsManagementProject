using System;
using System.Collections.Generic;

namespace UniversityEventsManagement.Models
{
    public class Event
    {
        public int EventID { get; set; }
        public string? Title { get; set; }
        public string? Description { get; set; }
        public DateTime EventDate { get; set; }
        public string? Venue { get; set; }
        public string? CreatedByUserID { get; set; }
        public bool IsApproved { get; set; }
        public int MaxCapacity { get; set; }
        public string? EventType { get; set; } // Workshop, Seminar, Social, etc.
        public DateTime CreatedDate { get; set; }
        public string? ImageUrl { get; set; }
        public int VolunteerHours { get; set; }
        public string? Secret { get; set; } // Unique secret for QR code

        // Navigation properties
        public virtual ApplicationUser? CreatedByUser { get; set; }
        public virtual ICollection<Registration>? Registrations { get; set; }
        public virtual ICollection<Attendance>? Attendances { get; set; }
        public virtual ICollection<Certificate>? Certificates { get; set; }
        public virtual ICollection<Feedback>? Feedbacks { get; set; }
    }
}
