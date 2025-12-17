#nullable disable
using System;
using System.Collections.Generic;

namespace UniversityEventsManagement.Models
{
    public class ApplicationUser
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();

        public string Email { get; set; }
        public string PasswordHash { get; set; }
        public string PhoneNumber { get; set; }
        public bool EmailConfirmed { get; set; }

        public bool TwoFactorEnabled { get; set; }
        public string TwoFactorSecret { get; set; }
        public string TwoFactorBackupCodes { get; set; }

        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string UniversityID { get; set; }
        public string UserType { get; set; }
        public DateTime JoinDate { get; set; }
        public int TotalVolunteerHours { get; set; }
        public bool IsActive { get; set; }
        public string Department { get; set; }
        public virtual ICollection<Event> CreatedEvents { get; set; }
        public virtual ICollection<Registration> Registrations { get; set; }
        public virtual ICollection<Attendance> Attendances { get; set; }
        public virtual ICollection<Certificate> Certificates { get; set; }
        public virtual ICollection<Notification> Notifications { get; set; }
        public virtual ICollection<Feedback> Feedbacks { get; set; }
        public virtual ICollection<Club> AdministeredClubs { get; set; }
        public virtual ICollection<ClubMember> ClubMemberships { get; set; }
        public virtual ICollection<BusReservation> BusReservations { get; set; }
    }
}
