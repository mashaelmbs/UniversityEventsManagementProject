using Microsoft.AspNetCore.Identity;
using UniversityEventsManagement.Models;

var passwordHasher = new PasswordHasher<ApplicationUser>();
var adminUser = new ApplicationUser { Id = "admin-user-id-001", Email = "admin@ksu.edu.sa" };
var studentUser = new ApplicationUser { Id = "student-user-id-001", Email = "fatima@ksu.edu.sa" };

string adminHash = passwordHasher.HashPassword(adminUser, "Admin@123");
string studentHash = passwordHasher.HashPassword(studentUser, "Student@123");

Console.WriteLine("Admin Hash (Admin@123):");
Console.WriteLine(adminHash);
Console.WriteLine("\nStudent Hash (Student@123):");
Console.WriteLine(studentHash);
