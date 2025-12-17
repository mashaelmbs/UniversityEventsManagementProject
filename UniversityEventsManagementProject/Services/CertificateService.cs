#nullable disable
using Microsoft.EntityFrameworkCore;
using UniversityEventsManagement.Data;
using UniversityEventsManagement.Models;

namespace UniversityEventsManagement.Services
{
    public interface ICertificateService
    {
        Task<Certificate> IssueCertificateAsync(int eventId, string userId);
        Task<List<Certificate>> GetUserCertificatesAsync(string userId);
        Task<List<Certificate>> GetEventCertificatesAsync(int eventId);
        Task<Certificate> GetCertificateByIdAsync(int id);
        Task<bool> CertificateExistsAsync(int eventId, string userId);
        Task<int> GetUserCertificateCountAsync(string userId);
        Task<int> GetEventCertificateCountAsync(int eventId);
        Task<bool> MarkCertificateAsDownloadedAsync(int certificateId);
        Task<List<Certificate>> GetDownloadedCertificatesAsync(string userId);
    }

    public class CertificateService : ICertificateService
    {
        private readonly ApplicationDbContext _context;
        private readonly INotificationService _notificationService;

        public CertificateService(ApplicationDbContext context, INotificationService notificationService)
        {
            _context = context;
            _notificationService = notificationService;
        }

        public async Task<Certificate> IssueCertificateAsync(int eventId, string userId)
        {
            var existingCert = await _context.Certificates
                .FirstOrDefaultAsync(c => c.EventID == eventId && c.UserID == userId);

            if (existingCert != null)
                throw new Exception("تم إصدار شهادة لهذا الطالب بالفعل");

            var @event = await _context.Events.FindAsync(eventId);
            var user = await _context.Users.FindAsync(userId);

            if (@event == null || user == null)
                throw new Exception("البيانات غير موجودة");

            var certificate = new Certificate
            {
                UserID = userId,
                EventID = eventId,
                IssueDate = DateTime.Now,
                CertificateNumber = $"CERT-{eventId}-{Guid.NewGuid().ToString().Substring(0, 8)}",
                IsDownloaded = false
            };

            _context.Certificates.Add(certificate);
            await _context.SaveChangesAsync();

            // Send notification
            await _notificationService.SendNotificationAsync(
                userId,
                $"تم إصدار شهادة لك عن الفعالية: {@event.Title}",
                "CertificateIssued",
                eventId);

            return certificate;
        }

        public async Task<List<Certificate>> GetUserCertificatesAsync(string userId)
        {
            return await _context.Certificates
                .Where(c => c.UserID == userId)
                .Include(c => c.Event)
                .Include(c => c.User)
                .OrderByDescending(c => c.IssueDate)
                .ToListAsync();
        }

        public async Task<List<Certificate>> GetEventCertificatesAsync(int eventId)
        {
            return await _context.Certificates
                .Where(c => c.EventID == eventId)
                .Include(c => c.User)
                .OrderByDescending(c => c.IssueDate)
                .ToListAsync();
        }

        public async Task<Certificate> GetCertificateByIdAsync(int id)
        {
            return await _context.Certificates
                .Include(c => c.Event)
                .Include(c => c.User)
                .FirstOrDefaultAsync(c => c.CertificateID == id);
        }

        public async Task<bool> CertificateExistsAsync(int eventId, string userId)
        {
            return await _context.Certificates
                .AnyAsync(c => c.EventID == eventId && c.UserID == userId);
        }

        public async Task<int> GetUserCertificateCountAsync(string userId)
        {
            return await _context.Certificates
                .Where(c => c.UserID == userId)
                .CountAsync();
        }

        public async Task<int> GetEventCertificateCountAsync(int eventId)
        {
            return await _context.Certificates
                .Where(c => c.EventID == eventId)
                .CountAsync();
        }

        public async Task<bool> MarkCertificateAsDownloadedAsync(int certificateId)
        {
            var certificate = await _context.Certificates.FindAsync(certificateId);
            if (certificate == null)
                return false;

            certificate.IsDownloaded = true;
            _context.Certificates.Update(certificate);
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<List<Certificate>> GetDownloadedCertificatesAsync(string userId)
        {
            return await _context.Certificates
                .Where(c => c.UserID == userId && c.IsDownloaded)
                .Include(c => c.Event)
                .OrderByDescending(c => c.IssueDate)
                .ToListAsync();
        }
    }
}
