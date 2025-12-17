#nullable disable
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using UniversityEventsManagement.Models;
using UniversityEventsManagement.Services;

namespace UniversityEventsManagement.Data
{
    public class DatabaseSeeder
    {
        private readonly ApplicationDbContext _context;
        private readonly IUserService _userService;
        private readonly ILogger<DatabaseSeeder> _logger;

        public DatabaseSeeder(
            ApplicationDbContext context,
            IUserService userService,
            ILogger<DatabaseSeeder> logger)
        {
            _context = context;
            _userService = userService;
            _logger = logger;
        }

        public async Task SeedAsync()
        {
            try
            {
                await SeedUsersAsync();
                await SeedEventsAsync();
                _logger.LogInformation("Database seeding completed successfully.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while seeding the database.");
                throw;
            }
        }

        private async Task SeedEventsAsync()
        {
            if (await _context.Events.AnyAsync())
            {
                _logger.LogInformation("Events already exist. Skipping event seeding...");
                return;
            }

            var adminUser = await _context.Users.FirstOrDefaultAsync(u => u.UserType == "Admin");
            if (adminUser == null)
            {
                _logger.LogWarning("No admin user found. Skipping event seeding...");
                return;
            }

            var events = new List<Event>
            {
                new Event
                {
                    Title = "ورشة عمل البرمجة بلغة Python",
                    Description = "ورشة عمل تفاعلية لتعلم أساسيات البرمجة بلغة Python. ستتعلم كيفية كتابة البرامج الأولى وفهم المفاهيم الأساسية للبرمجة.",
                    EventDate = DateTime.Now.AddDays(7),
                    Venue = "قاعة التدريب - مبنى كلية الحاسب",
                    MaxCapacity = 50,
                    EventType = "ورشة عمل",
                    IsApproved = true,
                    VolunteerHours = 3,
                    CreatedByUserID = adminUser.Id,
                    CreatedDate = DateTime.Now,
                    Secret = Guid.NewGuid().ToString("N").Substring(0, 8).ToUpper()
                },
                new Event
                {
                    Title = "ملتقى ريادة الأعمال",
                    Description = "ملتقى يجمع رواد الأعمال والمستثمرين لمناقشة أحدث الفرص والتحديات في عالم ريادة الأعمال.",
                    EventDate = DateTime.Now.AddDays(14),
                    Venue = "قاعة المؤتمرات الكبرى",
                    MaxCapacity = 200,
                    EventType = "ملتقى",
                    IsApproved = true,
                    VolunteerHours = 6,
                    CreatedByUserID = adminUser.Id,
                    CreatedDate = DateTime.Now,
                    Secret = Guid.NewGuid().ToString("N").Substring(0, 8).ToUpper()
                },
                new Event
                {
                    Title = "محاضرة الذكاء الاصطناعي",
                    Description = "محاضرة متخصصة حول تطبيقات الذكاء الاصطناعي في الحياة اليومية ومستقبل التقنية.",
                    EventDate = DateTime.Now.AddDays(5),
                    Venue = "المدرج الرئيسي - كلية العلوم",
                    MaxCapacity = 150,
                    EventType = "محاضرة",
                    IsApproved = true,
                    VolunteerHours = 2,
                    CreatedByUserID = adminUser.Id,
                    CreatedDate = DateTime.Now,
                    Secret = Guid.NewGuid().ToString("N").Substring(0, 8).ToUpper()
                },
                new Event
                {
                    Title = "مسابقة البرمجة التنافسية",
                    Description = "مسابقة برمجية للطلاب المتميزين. تتضمن تحديات برمجية متنوعة مع جوائز قيمة للفائزين.",
                    EventDate = DateTime.Now.AddDays(21),
                    Venue = "مختبرات الحاسب - المبنى 5",
                    MaxCapacity = 100,
                    EventType = "مسابقة",
                    IsApproved = true,
                    VolunteerHours = 8,
                    CreatedByUserID = adminUser.Id,
                    CreatedDate = DateTime.Now,
                    Secret = Guid.NewGuid().ToString("N").Substring(0, 8).ToUpper()
                },
                new Event
                {
                    Title = "يوم التطوع المجتمعي",
                    Description = "فعالية تطوعية لخدمة المجتمع تشمل زيارة دار الأيتام وتقديم المساعدات.",
                    EventDate = DateTime.Now.AddDays(10),
                    Venue = "ساحة الجامعة الرئيسية",
                    MaxCapacity = 80,
                    EventType = "تطوع",
                    IsApproved = true,
                    VolunteerHours = 7,
                    CreatedByUserID = adminUser.Id,
                    CreatedDate = DateTime.Now,
                    Secret = Guid.NewGuid().ToString("N").Substring(0, 8).ToUpper()
                },
                new Event
                {
                    Title = "معرض المشاريع الطلابية",
                    Description = "معرض لعرض مشاريع التخرج والمشاريع الإبداعية للطلاب من مختلف الكليات.",
                    EventDate = DateTime.Now.AddDays(30),
                    Venue = "صالة المعارض",
                    MaxCapacity = 500,
                    EventType = "معرض",
                    IsApproved = true,
                    VolunteerHours = 4,
                    CreatedByUserID = adminUser.Id,
                    CreatedDate = DateTime.Now,
                    Secret = Guid.NewGuid().ToString("N").Substring(0, 8).ToUpper()
                }
            };

            await _context.Events.AddRangeAsync(events);
            await _context.SaveChangesAsync();
            _logger.LogInformation($"Successfully seeded {events.Count} events.");
        }

        private async Task SeedUsersAsync()
        {
            // Seed Admin User
            await SeedUserAsync(
                email: "admin@university.edu",
                password: "Admin@123",
                firstName: "مدير",
                lastName: "النظام",
                universityID: "ADMIN001",
                userType: "Admin",
                department: "إدارة النظام",
                phoneNumber: "+966501234567"
            );

            // Seed Student User
            await SeedUserAsync(
                email: "student@university.edu",
                password: "Student@123",
                firstName: "أحمد",
                lastName: "محمد",
                universityID: "443012345",
                userType: "Student",
                department: "علوم الحاسب",
                phoneNumber: "+966503456789"
            );
        }

        private async Task SeedUserAsync(
            string email,
            string password,
            string firstName,
            string lastName,
            string universityID,
            string userType,
            string department,
            string phoneNumber)
        {
            var existingUser = await _userService.FindByEmailAsync(email);
            if (existingUser != null)
            {
                _logger.LogInformation($"User with email {email} already exists. Skipping...");
                return;
            }

            var user = new ApplicationUser
            {
                Email = email,
                FirstName = firstName,
                LastName = lastName,
                UniversityID = universityID,
                UserType = userType,
                Department = department,
                PhoneNumber = phoneNumber,
                JoinDate = DateTime.Now,
                IsActive = true,
                EmailConfirmed = true,
                TwoFactorEnabled = false,
                TotalVolunteerHours = 0
            };

            var result = await _userService.CreateUserAsync(user, password);
            if (result)
            {
                _logger.LogInformation($"Successfully created {userType} user: {email}");
            }
            else
            {
                _logger.LogWarning($"Failed to create {userType} user: {email}");
            }
        }
    }
}
