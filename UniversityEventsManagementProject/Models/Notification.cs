using System;

namespace UniversityEventsManagement.Models
{
    public class Notification
    {
        public int NotificationID { get; set; }
        public string? UserID { get; set; }
        public string? Message { get; set; }
        public DateTime SentDate { get; set; }
        public bool IsRead { get; set; }
        public string? NotificationType { get; set; } // EventReminder, EventApproved, etc.
        public int? EventID { get; set; }

        // Navigation properties
        public virtual ApplicationUser? User { get; set; }
        public virtual Event? Event { get; set; }
    }
}
