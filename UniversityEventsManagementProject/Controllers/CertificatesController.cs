#nullable disable
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using UniversityEventsManagement.Data;
using UniversityEventsManagement.Models;
using iText.Kernel.Pdf;
using iText.Layout;
using iText.Layout.Element;
using iText.Layout.Properties;
using iText.Kernel.Geom;
using iText.Kernel.Colors;
using iText.IO.Font;
using iText.Kernel.Font;
using iText.IO.Font.Constants;

namespace UniversityEventsManagement.Controllers
{
    [Authorize]
    public class CertificatesController : Controller
    {
        private readonly ApplicationDbContext _context;

        public CertificatesController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> MyCertificates()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var certificates = await _context.Certificates
                .Where(c => c.UserID == userId)
                .Include(c => c.Event)
                .Include(c => c.User)
                .OrderByDescending(c => c.IssueDate)
                .ToListAsync();

            return View(certificates);
        }

        [Route("Certificates/Details/{id:int}")]
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var isAdmin = User.IsInRole("Admin");
            
            // الأدمن يمكنه مشاهدة أي شهادة، المستخدم العادي فقط شهاداته
            var certificate = await _context.Certificates
                .Include(c => c.Event)
                .Include(c => c.User)
                .FirstOrDefaultAsync(c => c.CertificateID == id && (isAdmin || c.UserID == userId));

            if (certificate == null)
            {
                // إذا لم تُوجد الشهادة، نرجع لصفحة الشهادات مع رسالة خطأ
                TempData["Error"] = "الشهادة غير موجودة أو ليس لديك صلاحية للوصول إليها";
                return RedirectToAction("MyCertificates");
            }

            return View(certificate);
        }

        [Route("Certificates/Download/{id:int}")]
        public async Task<IActionResult> Download(int? id, string template = "classic", string language = "en")
        {
            if (id == null)
            {
                return NotFound();
            }

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var isAdmin = User.IsInRole("Admin");
            
            // الأدمن يمكنه تحميل أي شهادة، المستخدم العادي فقط شهاداته
            var certificate = await _context.Certificates
                .Include(c => c.Event)
                .Include(c => c.User)
                .FirstOrDefaultAsync(c => c.CertificateID == id && (isAdmin || c.UserID == userId));

            if (certificate == null)
            {
                return NotFound();
            }

            // التحقق من وجود البيانات المطلوبة
            if (certificate.User == null || certificate.Event == null)
            {
                TempData["Error"] = "بيانات الشهادة غير مكتملة";
                return RedirectToAction("MyCertificates");
            }

            byte[] pdfContent;
            try
            {
                if (template == "modern")
                {
                    pdfContent = language == "ar" 
                        ? GenerateModernCertificatePDFArabic(certificate)
                        : GenerateModernCertificatePDF(certificate);
                }
                else
                {
                    pdfContent = language == "ar"
                        ? GenerateClassicCertificatePDFArabic(certificate)
                        : GenerateClassicCertificatePDF(certificate);
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"خطأ في إنشاء الشهادة: {ex.Message}";
                return RedirectToAction("MyCertificates");
            }
            
            certificate.IsDownloaded = true;
            _context.Update(certificate);
            await _context.SaveChangesAsync();

            return File(pdfContent, "application/pdf", $"Certificate_{certificate.CertificateNumber}_{language}.pdf");
        }

        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> AllCertificates()
        {
            var certificates = await _context.Certificates
                .Include(c => c.Event)
                .Include(c => c.User)
                .OrderByDescending(c => c.IssueDate)
                .ToListAsync();

            return View(certificates);
        }

        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> IssueCertificate()
        {
            var now = DateTime.Now;
            // Get all approved events that have ended (EventDate is in the past)
            var events = await _context.Events
                .Where(e => e.IsApproved && e.EventDate < now)
                .Include(e => e.Attendances)
                .ThenInclude(a => a.User)
                .Include(e => e.Registrations)
                .OrderByDescending(e => e.EventDate)
                .ToListAsync();

            return View(events);
        }

        // GET: Issue Certificate for Specific Event (Admin)
        [Authorize(Roles = "Admin")]
        [Route("Certificates/IssueCertificateForEvent/{eventId:int}")]
        public async Task<IActionResult> IssueCertificateForEvent(int eventId)
        {
            var @event = await _context.Events
                .Include(e => e.Attendances)
                .ThenInclude(a => a.User)
                .FirstOrDefaultAsync(e => e.EventID == eventId);

            if (@event == null)
            {
                return NotFound();
            }

            // Get students who attended the event
            var attendedStudents = @event.Attendances
                .Where(a => a.IsPresent)
                .Select(a => a.User)
                .ToList();

            ViewBag.Event = @event;
            return View(attendedStudents);
        }

        // POST: Issue Certificate (Admin)
        [HttpPost]
        [Authorize(Roles = "Admin")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> IssueCertificateConfirm(int eventId, string userId)
        {
            var @event = await _context.Events.FindAsync(eventId);
            var user = await _context.Users.FindAsync(userId);

            if (@event == null || user == null)
            {
                return NotFound();
            }

            var attendance = await _context.Attendances
                .FirstOrDefaultAsync(a => a.EventID == eventId && a.UserID == userId && a.IsPresent);

            if (attendance == null)
            {
                TempData["Error"] = "الطالب لم يحضر هذه الفعالية";
                return RedirectToAction("IssueCertificateForEvent", new { eventId });
            }

            var existingCert = await _context.Certificates
                .FirstOrDefaultAsync(c => c.EventID == eventId && c.UserID == userId);

            if (existingCert != null)
            {
                TempData["Error"] = "تم إصدار شهادة لهذا الطالب بالفعل";
                return RedirectToAction("IssueCertificateForEvent", new { eventId });
            }

            var certificate = new Certificate
            {
                UserID = userId,
                EventID = eventId,
                IssueDate = DateTime.Now,
                CertificateNumber = $"CERT-{DateTime.Now:yyyyMMdd}-{Guid.NewGuid().ToString().Substring(0, 8).ToUpper()}",
                IsDownloaded = false,
                CertificateURL = ""
            };

            _context.Certificates.Add(certificate);
            
            // Update volunteer hours
            user.TotalVolunteerHours += @event.VolunteerHours;
            await _context.SaveChangesAsync();

            TempData["Success"] = $"تم إصدار الشهادة بنجاح للطالب {user.FirstName} {user.LastName}";
            return RedirectToAction("IssueCertificateForEvent", new { eventId });
        }

        private byte[] GenerateClassicCertificatePDF(Certificate certificate)
        {
            // استخراج البيانات مع null checks
            var userName = certificate.User != null 
                ? $"{certificate.User.FirstName ?? ""} {certificate.User.LastName ?? ""}" 
                : "Unknown";
            var eventTitle = certificate.Event?.Title ?? "Unknown Event";
            var eventDate = certificate.Event?.EventDate ?? DateTime.Now;
            var volunteerHours = certificate.Event?.VolunteerHours ?? 0;
            var certNumber = certificate.CertificateNumber ?? "N/A";
            var issueDate = certificate.IssueDate;

            using (var memoryStream = new System.IO.MemoryStream())
            {
                var writer = new PdfWriter(memoryStream);
                var pdf = new PdfDocument(writer);
                var document = new Document(pdf, PageSize.A4.Rotate());
                document.SetMargins(35, 35, 35, 35);

                PdfFont font = PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD);
                var fontRegular = PdfFontFactory.CreateFont(StandardFonts.HELVETICA);

                var titleParagraph = new Paragraph("CERTIFICATE OF PARTICIPATION")
                    .SetFont(font)
                    .SetFontSize(36)
                    .SetTextAlignment(TextAlignment.CENTER)
                    .SetFontColor(new DeviceRgb(111, 78, 55))
                    .SetMarginTop(20);
                document.Add(titleParagraph);

                // Get page for decorations
                var pdfPage = pdf.GetFirstPage();
                var pageSize = pdfPage.GetPageSize();
                var canvas = new iText.Kernel.Pdf.Canvas.PdfCanvas(pdfPage);

                canvas.SetStrokeColor(new DeviceRgb(111, 78, 55))
                    .SetLineWidth(4)
                    .Rectangle(15, 15, pageSize.GetWidth() - 30, pageSize.GetHeight() - 30)
                    .Stroke();

                // Inner decorative border
                canvas.SetStrokeColor(new DeviceRgb(200, 180, 120))
                    .SetLineWidth(2)
                    .Rectangle(25, 25, pageSize.GetWidth() - 50, pageSize.GetHeight() - 50)
                    .Stroke();

                var corners = new[] {
                    new { x = 25f, y = pageSize.GetHeight() - 25 }, // Top-left
                    new { x = pageSize.GetWidth() - 25, y = pageSize.GetHeight() - 25 }, // Top-right
                    new { x = 25f, y = 25f }, // Bottom-left
                    new { x = pageSize.GetWidth() - 25, y = 25f } // Bottom-right
                };

                canvas.SetFillColor(new DeviceRgb(111, 78, 55));
                foreach (var corner in corners)
                {
                    canvas.Circle(corner.x, corner.y, 3).Fill();
                }

                document.Add(new Paragraph("\n"));

                var subtitleParagraph = new Paragraph("This is to certify that")
                    .SetFont(fontRegular)
                    .SetFontSize(16)
                    .SetTextAlignment(TextAlignment.CENTER)
                    .SetFontColor(new DeviceRgb(60, 60, 60));
                document.Add(subtitleParagraph);

                document.Add(new Paragraph("\n"));

                var nameParagraph = new Paragraph(userName)
                    .SetFont(font)
                    .SetFontSize(28)
                    .SetTextAlignment(TextAlignment.CENTER)
                    .SetFontColor(new DeviceRgb(200, 140, 40));
                document.Add(nameParagraph);

                document.Add(new Paragraph("\n"));

                var bodyParagraph = new Paragraph("has successfully participated in")
                    .SetFont(fontRegular)
                    .SetFontSize(15)
                    .SetTextAlignment(TextAlignment.CENTER)
                    .SetFontColor(new DeviceRgb(60, 60, 60));
                document.Add(bodyParagraph);

                document.Add(new Paragraph("\n"));

                var eventParagraph = new Paragraph(eventTitle)
                    .SetFont(font)
                    .SetFontSize(20)
                    .SetTextAlignment(TextAlignment.CENTER)
                    .SetFontColor(new DeviceRgb(111, 78, 55));
                document.Add(eventParagraph);

                document.Add(new Paragraph("\n"));

                var dateParagraph = new Paragraph($"on {eventDate:MMMM dd, yyyy}")
                    .SetFont(fontRegular)
                    .SetFontSize(13)
                    .SetTextAlignment(TextAlignment.CENTER)
                    .SetFontColor(new DeviceRgb(80, 80, 80));
                document.Add(dateParagraph);

                document.Add(new Paragraph("\n\n"));

                var table = new Table(2)
                    .SetWidth(450)
                    .SetHorizontalAlignment(HorizontalAlignment.CENTER)
                    .SetBorderCollapse(BorderCollapsePropertyValue.COLLAPSE);

                var headerColor = new DeviceRgb(25, 55, 109);
                var cellPadding = 12;

                var certCell1 = new Cell()
                    .SetBackgroundColor(headerColor)
                    .SetPadding(cellPadding)
                    .Add(new Paragraph("Certificate Number").SetFont(font).SetFontColor(ColorConstants.WHITE).SetFontSize(11));
                var certCell2 = new Cell()
                    .SetPadding(cellPadding)
                    .SetBackgroundColor(new DeviceRgb(245, 245, 245))
                    .Add(new Paragraph(certNumber).SetFont(fontRegular).SetFontSize(11));
                table.AddCell(certCell1);
                table.AddCell(certCell2);

                // Issued Date
                var dateCell1 = new Cell()
                    .SetBackgroundColor(headerColor)
                    .SetPadding(cellPadding)
                    .Add(new Paragraph("Issued Date").SetFont(font).SetFontColor(ColorConstants.WHITE).SetFontSize(11));
                var dateCell2 = new Cell()
                    .SetPadding(cellPadding)
                    .SetBackgroundColor(new DeviceRgb(245, 245, 245))
                    .Add(new Paragraph(issueDate.ToString("MMMM dd, yyyy")).SetFont(fontRegular).SetFontSize(11));
                table.AddCell(dateCell1);
                table.AddCell(dateCell2);

                // Volunteer Hours
                var hoursCell1 = new Cell()
                    .SetBackgroundColor(headerColor)
                    .SetPadding(cellPadding)
                    .Add(new Paragraph("Volunteer Hours").SetFont(font).SetFontColor(ColorConstants.WHITE).SetFontSize(11));
                var hoursCell2 = new Cell()
                    .SetPadding(cellPadding)
                    .SetBackgroundColor(new DeviceRgb(245, 245, 245))
                    .Add(new Paragraph(volunteerHours.ToString()).SetFont(fontRegular).SetFontSize(11));
                table.AddCell(hoursCell1);
                table.AddCell(hoursCell2);

                document.Add(table);

                document.Add(new Paragraph("\n\n"));

                // Signature line
                var signatureCanvas = new iText.Kernel.Pdf.Canvas.PdfCanvas(pdfPage);
                signatureCanvas.SetStrokeColor(new DeviceRgb(100, 100, 100))
                    .SetLineWidth(1)
                    .MoveTo(100, 80)
                    .LineTo(200, 80)
                    .Stroke();

                var signatureParagraph = new Paragraph("Authorized Signature")
                    .SetFont(fontRegular)
                    .SetFontSize(10)
                    .SetTextAlignment(TextAlignment.CENTER)
                    .SetFontColor(new DeviceRgb(100, 100, 100));
                document.Add(signatureParagraph);

                document.Add(new Paragraph("\n"));

                var footerParagraph = new Paragraph("University of Hafr Al-Batin\nEvents Management System")
                    .SetFont(fontRegular)
                    .SetFontSize(11)
                    .SetTextAlignment(TextAlignment.CENTER)
                    .SetFontColor(new DeviceRgb(120, 120, 120));
                document.Add(footerParagraph);

                document.Close();
                return memoryStream.ToArray();
            }
        }

        private byte[] GenerateModernCertificatePDF(Certificate certificate)
        {
            // استخراج البيانات مع null checks
            var userName = certificate.User != null 
                ? $"{certificate.User.FirstName ?? ""} {certificate.User.LastName ?? ""}" 
                : "Unknown";
            var eventTitle = certificate.Event?.Title ?? "Unknown Event";
            var eventDate = certificate.Event?.EventDate ?? DateTime.Now;
            var volunteerHours = certificate.Event?.VolunteerHours ?? 0;
            var certNumber = certificate.CertificateNumber ?? "N/A";
            var issueDate = certificate.IssueDate;

            using (var memoryStream = new System.IO.MemoryStream())
            {
                var writer = new PdfWriter(memoryStream);
                var pdf = new PdfDocument(writer);
                var document = new Document(pdf, PageSize.A4.Rotate());

                document.SetMargins(50, 50, 50, 50);

                var font = PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD);
                var fontRegular = PdfFontFactory.CreateFont(StandardFonts.HELVETICA);

                var titleParagraph = new Paragraph("Certificate of Participation")
                    .SetFont(font)
                    .SetFontSize(36)
                    .SetTextAlignment(TextAlignment.CENTER)
                    .SetFontColor(new DeviceRgb(111, 78, 55))
                    .SetMarginTop(60);
                document.Add(titleParagraph);

                var pdfPage = pdf.GetFirstPage();
                var pageSize = pdfPage.GetPageSize();
                var canvas = new iText.Kernel.Pdf.Canvas.PdfCanvas(pdfPage);

                var gradient = new iText.Kernel.Pdf.Canvas.PdfCanvas(pdfPage);
                gradient.SetFillColor(new DeviceRgb(239, 230, 221))
                    .Rectangle(0, 0, pageSize.GetWidth(), pageSize.GetHeight())
                    .Fill();

                canvas.SetFillColor(new DeviceRgb(111, 78, 55))
                    .SetStrokeColor(new DeviceRgb(111, 78, 55))
                    .SetLineWidth(2);

                canvas.MoveTo(50, pageSize.GetHeight() - 50)
                    .LineTo(150, pageSize.GetHeight() - 50)
                    .LineTo(50, pageSize.GetHeight() - 150)
                    .ClosePathStroke();

                canvas.MoveTo(pageSize.GetWidth() - 50, 50)
                    .LineTo(pageSize.GetWidth() - 150, 50)
                    .LineTo(pageSize.GetWidth() - 50, 150)
                    .ClosePathStroke();

                document.Add(new Paragraph("\n\n"));

                var lineCanvas = new iText.Kernel.Pdf.Canvas.PdfCanvas(pdfPage);
                lineCanvas.SetStrokeColor(new DeviceRgb(111, 78, 55))
                    .SetLineWidth(3)
                    .MoveTo(100, pageSize.GetHeight() / 2 + 100)
                    .LineTo(pageSize.GetWidth() - 100, pageSize.GetHeight() / 2 + 100)
                    .Stroke();

                document.Add(new Paragraph("\n\n"));

                var nameParagraph = new Paragraph(userName)
                    .SetFont(font)
                    .SetFontSize(28)
                    .SetTextAlignment(TextAlignment.CENTER)
                    .SetFontColor(new DeviceRgb(51, 51, 51))
                    .SetMarginTop(20);
                document.Add(nameParagraph);

                document.Add(new Paragraph("\n"));

                // Body text
                var bodyParagraph = new Paragraph("This certificate is awarded in recognition of participation in")
                    .SetFont(fontRegular)
                    .SetFontSize(16)
                    .SetTextAlignment(TextAlignment.CENTER)
                    .SetFontColor(new DeviceRgb(100, 100, 100));
                document.Add(bodyParagraph);

                document.Add(new Paragraph("\n"));

                var eventParagraph = new Paragraph(eventTitle)
                    .SetFont(font)
                    .SetFontSize(22)
                    .SetTextAlignment(TextAlignment.CENTER)
                    .SetFontColor(new DeviceRgb(111, 78, 55))
                    .SetMarginTop(10);
                document.Add(eventParagraph);

                document.Add(new Paragraph("\n"));

                // Event date
                var dateParagraph = new Paragraph($"on {eventDate:MMMM dd, yyyy}")
                    .SetFont(fontRegular)
                    .SetFontSize(14)
                    .SetTextAlignment(TextAlignment.CENTER)
                    .SetFontColor(new DeviceRgb(120, 120, 120));
                document.Add(dateParagraph);

                document.Add(new Paragraph("\n\n"));

                var infoTable = new Table(2).SetWidth(500).SetHorizontalAlignment(HorizontalAlignment.CENTER);
                
                var certNumberCell = new Cell()
                    .SetBackgroundColor(new DeviceRgb(111, 78, 55))
                    .SetPadding(15)
                    .Add(new Paragraph("Certificate Number").SetFont(font).SetFontColor(ColorConstants.WHITE).SetFontSize(12))
                    .Add(new Paragraph(certNumber).SetFont(fontRegular).SetFontColor(ColorConstants.WHITE).SetFontSize(14));
                
                var hoursCell = new Cell()
                    .SetBackgroundColor(new DeviceRgb(25, 135, 84))
                    .SetPadding(15)
                    .Add(new Paragraph("Volunteer Hours").SetFont(font).SetFontColor(ColorConstants.WHITE).SetFontSize(12))
                    .Add(new Paragraph(volunteerHours.ToString()).SetFont(fontRegular).SetFontColor(ColorConstants.WHITE).SetFontSize(14));

                infoTable.AddCell(certNumberCell);
                infoTable.AddCell(hoursCell);
                document.Add(infoTable);

                document.Add(new Paragraph("\n\n"));

                var footerParagraph = new Paragraph("University of Hafr Al-Batin\nEvents Management System")
                    .SetFont(fontRegular)
                    .SetFontSize(11)
                    .SetTextAlignment(TextAlignment.CENTER)
                    .SetFontColor(new DeviceRgb(150, 150, 150))
                    .SetMarginTop(40);
                document.Add(footerParagraph);

                var issueDateParagraph = new Paragraph($"Issue Date: {issueDate:MMMM dd, yyyy}")
                    .SetFont(fontRegular)
                    .SetFontSize(10)
                    .SetTextAlignment(TextAlignment.CENTER)
                    .SetFontColor(new DeviceRgb(180, 180, 180))
                    .SetMarginTop(10);
                document.Add(issueDateParagraph);

                document.Close();
                return memoryStream.ToArray();
            }
        }

        private byte[] GenerateClassicCertificatePDFArabic(Certificate certificate)
        {
            // التحقق من البيانات المطلوبة
            var userName = certificate.User != null 
                ? $"{certificate.User.FirstName ?? ""} {certificate.User.LastName ?? ""}" 
                : "غير محدد";
            var eventTitle = certificate.Event?.Title ?? "غير محدد";
            var eventDate = certificate.Event?.EventDate ?? DateTime.Now;
            var volunteerHours = certificate.Event?.VolunteerHours ?? 0;
            var certNumber = certificate.CertificateNumber ?? "N/A";
            var issueDate = certificate.IssueDate;

            using (var memoryStream = new System.IO.MemoryStream())
            {
                var writer = new PdfWriter(memoryStream);
                var pdf = new PdfDocument(writer);
                var document = new Document(pdf, PageSize.A4.Rotate());
                document.SetMargins(35, 35, 35, 35);

                // استخدام خط Helvetica (لا يدعم العربية بشكل كامل لكنه آمن)
                var font = PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD);
                var fontRegular = PdfFontFactory.CreateFont(StandardFonts.HELVETICA);

                var titleParagraph = new Paragraph("Certificate of Participation")
                    .SetFont(font)
                    .SetFontSize(36)
                    .SetTextAlignment(TextAlignment.CENTER)
                    .SetFontColor(new DeviceRgb(111, 78, 55))
                    .SetMarginTop(20);
                document.Add(titleParagraph);

                // Get page for decorations
                var pdfPage = pdf.GetFirstPage();
                var pageSize = pdfPage.GetPageSize();
                var canvas = new iText.Kernel.Pdf.Canvas.PdfCanvas(pdfPage);

                canvas.SetStrokeColor(new DeviceRgb(111, 78, 55))
                    .SetLineWidth(4)
                    .Rectangle(15, 15, pageSize.GetWidth() - 30, pageSize.GetHeight() - 30)
                    .Stroke();

                // Inner decorative border
                canvas.SetStrokeColor(new DeviceRgb(200, 180, 120))
                    .SetLineWidth(2)
                    .Rectangle(25, 25, pageSize.GetWidth() - 50, pageSize.GetHeight() - 50)
                    .Stroke();

                canvas.SetFillColor(new DeviceRgb(111, 78, 55));
                var corners = new[] {
                    new { x = 25f, y = pageSize.GetHeight() - 25 },
                    new { x = pageSize.GetWidth() - 25, y = pageSize.GetHeight() - 25 },
                    new { x = 25f, y = 25f },
                    new { x = pageSize.GetWidth() - 25, y = 25f }
                };
                foreach (var corner in corners)
                {
                    canvas.Circle(corner.x, corner.y, 3).Fill();
                }

                document.Add(new Paragraph("\n"));

                var subtitleParagraph = new Paragraph("This is to certify that")
                    .SetFont(fontRegular)
                    .SetFontSize(16)
                    .SetTextAlignment(TextAlignment.CENTER)
                    .SetFontColor(new DeviceRgb(60, 60, 60));
                document.Add(subtitleParagraph);

                document.Add(new Paragraph("\n"));

                var nameParagraph = new Paragraph(userName)
                    .SetFont(font)
                    .SetFontSize(28)
                    .SetTextAlignment(TextAlignment.CENTER)
                    .SetFontColor(new DeviceRgb(200, 140, 40));
                document.Add(nameParagraph);

                document.Add(new Paragraph("\n"));

                var bodyParagraph = new Paragraph("has successfully participated in")
                    .SetFont(fontRegular)
                    .SetFontSize(15)
                    .SetTextAlignment(TextAlignment.CENTER)
                    .SetFontColor(new DeviceRgb(60, 60, 60));
                document.Add(bodyParagraph);

                document.Add(new Paragraph("\n"));

                // Event title
                var eventParagraph = new Paragraph(eventTitle)
                    .SetFont(font)
                    .SetFontSize(20)
                    .SetTextAlignment(TextAlignment.CENTER)
                    .SetFontColor(new DeviceRgb(111, 78, 55));
                document.Add(eventParagraph);

                document.Add(new Paragraph("\n"));

                // Event date
                var dateParagraph = new Paragraph($"on {eventDate:MMMM dd, yyyy}")
                    .SetFont(fontRegular)
                    .SetFontSize(13)
                    .SetTextAlignment(TextAlignment.CENTER)
                    .SetFontColor(new DeviceRgb(80, 80, 80));
                document.Add(dateParagraph);

                document.Add(new Paragraph("\n\n"));

                var table = new Table(2)
                    .SetWidth(450)
                    .SetHorizontalAlignment(HorizontalAlignment.CENTER)
                    .SetBorderCollapse(BorderCollapsePropertyValue.COLLAPSE);

                var headerColor = new DeviceRgb(111, 78, 55);
                var cellPadding = 12;

                var certCell1 = new Cell()
                    .SetBackgroundColor(headerColor)
                    .SetPadding(cellPadding)
                    .Add(new Paragraph("Certificate Number").SetFont(font).SetFontColor(ColorConstants.WHITE).SetFontSize(11));
                var certCell2 = new Cell()
                    .SetPadding(cellPadding)
                    .SetBackgroundColor(new DeviceRgb(245, 245, 245))
                    .Add(new Paragraph(certNumber).SetFont(fontRegular).SetFontSize(11));
                table.AddCell(certCell1);
                table.AddCell(certCell2);

                // Issued Date
                var dateCell1 = new Cell()
                    .SetBackgroundColor(headerColor)
                    .SetPadding(cellPadding)
                    .Add(new Paragraph("Issue Date").SetFont(font).SetFontColor(ColorConstants.WHITE).SetFontSize(11));
                var dateCell2 = new Cell()
                    .SetPadding(cellPadding)
                    .SetBackgroundColor(new DeviceRgb(245, 245, 245))
                    .Add(new Paragraph(issueDate.ToString("MMMM dd, yyyy")).SetFont(fontRegular).SetFontSize(11));
                table.AddCell(dateCell1);
                table.AddCell(dateCell2);

                // Volunteer Hours
                var hoursCell1 = new Cell()
                    .SetBackgroundColor(headerColor)
                    .SetPadding(cellPadding)
                    .Add(new Paragraph("Volunteer Hours").SetFont(font).SetFontColor(ColorConstants.WHITE).SetFontSize(11));
                var hoursCell2 = new Cell()
                    .SetPadding(cellPadding)
                    .SetBackgroundColor(new DeviceRgb(245, 245, 245))
                    .Add(new Paragraph(volunteerHours.ToString()).SetFont(fontRegular).SetFontSize(11));
                table.AddCell(hoursCell1);
                table.AddCell(hoursCell2);

                document.Add(table);

                document.Add(new Paragraph("\n\n"));

                // Signature line
                var signatureCanvas = new iText.Kernel.Pdf.Canvas.PdfCanvas(pdfPage);
                signatureCanvas.SetStrokeColor(new DeviceRgb(100, 100, 100))
                    .SetLineWidth(1)
                    .MoveTo(100, 80)
                    .LineTo(200, 80)
                    .Stroke();

                var signatureParagraph = new Paragraph("Authorized Signature")
                    .SetFont(fontRegular)
                    .SetFontSize(10)
                    .SetTextAlignment(TextAlignment.CENTER)
                    .SetFontColor(new DeviceRgb(100, 100, 100));
                document.Add(signatureParagraph);

                document.Add(new Paragraph("\n"));

                var footerParagraph = new Paragraph("University Events Management System")
                    .SetFont(fontRegular)
                    .SetFontSize(11)
                    .SetTextAlignment(TextAlignment.CENTER)
                    .SetFontColor(new DeviceRgb(120, 120, 120));
                document.Add(footerParagraph);

                document.Close();
                return memoryStream.ToArray();
            }
        }

        private byte[] GenerateModernCertificatePDFArabic(Certificate certificate)
        {
            // التحقق من البيانات المطلوبة
            var userName = certificate.User != null 
                ? $"{certificate.User.FirstName ?? ""} {certificate.User.LastName ?? ""}" 
                : "Unknown";
            var eventTitle = certificate.Event?.Title ?? "Unknown Event";
            var eventDate = certificate.Event?.EventDate ?? DateTime.Now;
            var volunteerHours = certificate.Event?.VolunteerHours ?? 0;
            var certNumber = certificate.CertificateNumber ?? "N/A";
            var issueDate = certificate.IssueDate;

            using (var memoryStream = new System.IO.MemoryStream())
            {
                var writer = new PdfWriter(memoryStream);
                var pdf = new PdfDocument(writer);
                var document = new Document(pdf, PageSize.A4.Rotate());
                document.SetMargins(50, 50, 50, 50);

                var font = PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD);
                var fontRegular = PdfFontFactory.CreateFont(StandardFonts.HELVETICA);

                var titleParagraph = new Paragraph("Certificate of Participation")
                    .SetFont(font)
                    .SetFontSize(40)
                    .SetTextAlignment(TextAlignment.CENTER)
                    .SetFontColor(new DeviceRgb(111, 78, 55))
                    .SetMarginTop(60);
                document.Add(titleParagraph);

                var pdfPage = pdf.GetFirstPage();
                var pageSize = pdfPage.GetPageSize();
                var canvas = new iText.Kernel.Pdf.Canvas.PdfCanvas(pdfPage);

                var gradient = new iText.Kernel.Pdf.Canvas.PdfCanvas(pdfPage);
                gradient.SetFillColor(new DeviceRgb(239, 230, 221))
                    .Rectangle(0, 0, pageSize.GetWidth(), pageSize.GetHeight())
                    .Fill();

                canvas.SetFillColor(new DeviceRgb(111, 78, 55))
                    .SetStrokeColor(new DeviceRgb(111, 78, 55))
                    .SetLineWidth(2);

                canvas.MoveTo(50, pageSize.GetHeight() - 50)
                    .LineTo(150, pageSize.GetHeight() - 50)
                    .LineTo(50, pageSize.GetHeight() - 150)
                    .ClosePathStroke();

                canvas.MoveTo(pageSize.GetWidth() - 50, 50)
                    .LineTo(pageSize.GetWidth() - 150, 50)
                    .LineTo(pageSize.GetWidth() - 50, 150)
                    .ClosePathStroke();

                document.Add(new Paragraph("\n\n"));

                var lineCanvas = new iText.Kernel.Pdf.Canvas.PdfCanvas(pdfPage);
                lineCanvas.SetStrokeColor(new DeviceRgb(200, 140, 40))
                    .SetLineWidth(3)
                    .MoveTo(100, pageSize.GetHeight() / 2 + 100)
                    .LineTo(pageSize.GetWidth() - 100, pageSize.GetHeight() / 2 + 100)
                    .Stroke();

                document.Add(new Paragraph("\n\n"));

                var nameParagraph = new Paragraph(userName)
                    .SetFont(font)
                    .SetFontSize(32)
                    .SetTextAlignment(TextAlignment.CENTER)
                    .SetFontColor(new DeviceRgb(200, 140, 40))
                    .SetMarginTop(20);
                document.Add(nameParagraph);

                document.Add(new Paragraph("\n"));

                // Body text
                var bodyParagraph = new Paragraph("This certificate is awarded in recognition of participation in")
                    .SetFont(fontRegular)
                    .SetFontSize(16)
                    .SetTextAlignment(TextAlignment.CENTER)
                    .SetFontColor(new DeviceRgb(60, 60, 60));
                document.Add(bodyParagraph);

                document.Add(new Paragraph("\n"));

                // Event title
                var eventParagraph = new Paragraph(eventTitle)
                    .SetFont(font)
                    .SetFontSize(24)
                    .SetTextAlignment(TextAlignment.CENTER)
                    .SetFontColor(new DeviceRgb(111, 78, 55))
                    .SetMarginTop(10);
                document.Add(eventParagraph);

                document.Add(new Paragraph("\n"));

                // Event date
                var dateParagraph = new Paragraph($"on {eventDate:MMMM dd, yyyy}")
                    .SetFont(fontRegular)
                    .SetFontSize(14)
                    .SetTextAlignment(TextAlignment.CENTER)
                    .SetFontColor(new DeviceRgb(120, 120, 120));
                document.Add(dateParagraph);

                document.Add(new Paragraph("\n\n"));

                var infoTable = new Table(2)
                    .SetWidth(500)
                    .SetHorizontalAlignment(HorizontalAlignment.CENTER);

                var certNumberCell = new Cell()
                    .SetBackgroundColor(new DeviceRgb(111, 78, 55))
                    .SetPadding(15)
                    .Add(new Paragraph("Certificate Number").SetFont(font).SetFontColor(ColorConstants.WHITE).SetFontSize(12))
                    .Add(new Paragraph(certNumber).SetFont(fontRegular).SetFontColor(ColorConstants.WHITE).SetFontSize(14));

                var hoursCell = new Cell()
                    .SetBackgroundColor(new DeviceRgb(200, 140, 40))
                    .SetPadding(15)
                    .Add(new Paragraph("Volunteer Hours").SetFont(font).SetFontColor(ColorConstants.WHITE).SetFontSize(12))
                    .Add(new Paragraph(volunteerHours.ToString()).SetFont(fontRegular).SetFontColor(ColorConstants.WHITE).SetFontSize(14));

                infoTable.AddCell(certNumberCell);
                infoTable.AddCell(hoursCell);
                document.Add(infoTable);

                document.Add(new Paragraph("\n\n"));

                var footerParagraph = new Paragraph("University Events Management System")
                    .SetFont(fontRegular)
                    .SetFontSize(11)
                    .SetTextAlignment(TextAlignment.CENTER)
                    .SetFontColor(new DeviceRgb(150, 150, 150))
                    .SetMarginTop(40);
                document.Add(footerParagraph);

                var issueDateParagraph = new Paragraph($"Issue Date: {issueDate:MMMM dd, yyyy}")
                    .SetFont(fontRegular)
                    .SetFontSize(10)
                    .SetTextAlignment(TextAlignment.CENTER)
                    .SetFontColor(new DeviceRgb(180, 180, 180))
                    .SetMarginTop(10);
                document.Add(issueDateParagraph);

                document.Close();
                return memoryStream.ToArray();
            }
        }
    }
}
