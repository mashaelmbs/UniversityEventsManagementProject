using System;

namespace UniversityEventsManagement.Models
{
    public class Feedback
    {
        public int FeedbackID { get; set; }
        public int EventID { get; set; }
        public string? UserID { get; set; }
        public int Rating { get; set; } // 1-5 stars
        public string? Comment { get; set; }
        public DateTime SubmittedDate { get; set; }

        // Navigation properties
        public virtual Event? Event { get; set; }
        public virtual ApplicationUser? User { get; set; }
    }
}
