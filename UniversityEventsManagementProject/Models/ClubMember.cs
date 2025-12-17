using System;

namespace UniversityEventsManagement.Models
{
    public class ClubMember
    {
        public int ClubMemberID { get; set; }
        public int ClubID { get; set; }
        public string? UserID { get; set; }
        public DateTime JoinDate { get; set; }
        public string? Role { get; set; } // Member, Officer, President
        public string? Status { get; set; } // Pending, Approved, Rejected

        // Navigation properties
        public virtual Club? Club { get; set; }
        public virtual ApplicationUser? User { get; set; }
    }
}
