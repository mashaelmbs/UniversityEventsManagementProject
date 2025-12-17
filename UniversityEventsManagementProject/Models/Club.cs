using System;
using System.Collections.Generic;

namespace UniversityEventsManagement.Models
{
    public class Club
    {
        public int ClubID { get; set; }
        public string? ClubName { get; set; }
        public string? Description { get; set; }
        public string? AdminUserID { get; set; }
        public DateTime CreatedDate { get; set; }
        public string? LogoUrl { get; set; }
        public bool IsActive { get; set; }

        // Navigation properties
        public virtual ApplicationUser? AdminUser { get; set; }
        public virtual ICollection<ClubMember>? Members { get; set; }
    }
}
