using System;

namespace UniversityEventsManagement.Models
{
    public class Certificate
    {
        public int CertificateID { get; set; }
        public string? UserID { get; set; }
        public int EventID { get; set; }
        public DateTime IssueDate { get; set; }
        public string? CertificateURL { get; set; }
        public string? CertificateNumber { get; set; }
        public bool IsDownloaded { get; set; }

        // Navigation properties
        public virtual ApplicationUser? User { get; set; }
        public virtual Event? Event { get; set; }
    }
}
