using System;
using System.ComponentModel.DataAnnotations;

namespace UniversityEventsManagement.Models
{
    public class Contact
    {
        [Key]
        public int ContactID { get; set; }

        [Required(ErrorMessage = "الاسم مطلوب")]
        [StringLength(100)]
        public string FullName { get; set; } = string.Empty;

        [Required(ErrorMessage = "البريد الإلكتروني مطلوب")]
        [EmailAddress(ErrorMessage = "البريد الإلكتروني غير صحيح")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "رقم الهاتف مطلوب")]
        [Phone(ErrorMessage = "رقم الهاتف غير صحيح")]
        [StringLength(20)]
        public string PhoneNumber { get; set; } = string.Empty;

        [Required(ErrorMessage = "الموضوع مطلوب")]
        [StringLength(200)]
        public string Subject { get; set; } = string.Empty;

        [Required(ErrorMessage = "الرسالة مطلوبة")]
        [StringLength(2000)]
        public string Message { get; set; } = string.Empty;

        [StringLength(50)]
        public string? InquiryType { get; set; }

        public DateTime SubmittedDate { get; set; } = DateTime.Now;

        [StringLength(500)]
        public string? AdminResponse { get; set; }

        public DateTime? ResponseDate { get; set; }

        public bool IsResolved { get; set; } = false;
    }
}
