using System;

namespace UniversityEventsManagement.Models
{
    public class Attendance
    {
        public int AttendanceID { get; set; }
        public int EventID { get; set; }
        public string? UserID { get; set; }
        public DateTime CheckInTime { get; set; }
        public string? QRCode { get; set; }
        public bool IsPresent { get; set; }

        // Navigation properties
        public virtual Event? Event { get; set; }
        public virtual ApplicationUser? User { get; set; }
    }
}
