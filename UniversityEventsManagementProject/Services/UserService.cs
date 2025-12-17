#nullable disable
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UniversityEventsManagement.Data;
using UniversityEventsManagement.Models;

namespace UniversityEventsManagement.Services
{
    public interface IUserService
    {
        Task<ApplicationUser> FindByEmailAsync(string email);
        Task<ApplicationUser> FindByIdAsync(string id);
        Task<ApplicationUser> FindByUniversityIdAsync(string universityId);
        Task<bool> CheckPasswordAsync(ApplicationUser user, string password);
        Task<bool> CreateUserAsync(ApplicationUser user, string password);
        Task<bool> UpdateUserAsync(ApplicationUser user);
        Task<bool> DeleteUserAsync(ApplicationUser user);
        Task<IList<ApplicationUser>> GetAllUsersAsync();
        Task<IList<ApplicationUser>> GetUsersByRoleAsync(string role);
        Task<string> GetUserRoleAsync(ApplicationUser user);
        Task<bool> ChangePasswordAsync(ApplicationUser user, string oldPassword, string newPassword);
        Task<bool> ResetPasswordAsync(ApplicationUser user, string newPassword);
    }

    public class UserService : IUserService
    {
        private readonly ApplicationDbContext _context;
        private readonly IPasswordHasher<ApplicationUser> _passwordHasher;

        public UserService(ApplicationDbContext context, IPasswordHasher<ApplicationUser> passwordHasher)
        {
            _context = context;
            _passwordHasher = passwordHasher;
        }

        public async Task<ApplicationUser> FindByEmailAsync(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
                return null;

            return await _context.Users
                .FirstOrDefaultAsync(u => u.Email.ToLower() == email.ToLower());
        }

        public async Task<ApplicationUser> FindByIdAsync(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
                return null;

            return await _context.Users.FindAsync(id);
        }

        public async Task<ApplicationUser> FindByUniversityIdAsync(string universityId)
        {
            if (string.IsNullOrWhiteSpace(universityId))
                return null;

            return await _context.Users
                .FirstOrDefaultAsync(u => u.UniversityID == universityId);
        }

        public Task<bool> CheckPasswordAsync(ApplicationUser user, string password)
        {
            if (user == null || string.IsNullOrWhiteSpace(password))
                return Task.FromResult(false);

            try
            {
                if (string.IsNullOrEmpty(user.PasswordHash))
                {
                    return Task.FromResult(false);
                }

                var result = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, password);
                return Task.FromResult(result == PasswordVerificationResult.Success);
            }
            catch (FormatException)
            {
                return Task.FromResult(user.PasswordHash == password);
            }
        }

        public async Task<bool> CreateUserAsync(ApplicationUser user, string password)
        {
            if (user == null || string.IsNullOrWhiteSpace(password))
                return false;

            var existingUser = await FindByEmailAsync(user.Email);
            if (existingUser != null)
                return false;

            if (string.IsNullOrWhiteSpace(user.Id))
                user.Id = Guid.NewGuid().ToString();

            user.PasswordHash = _passwordHasher.HashPassword(user, password);

            try
            {
                _context.Users.Add(user);
                await _context.SaveChangesAsync();
                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> UpdateUserAsync(ApplicationUser user)
        {
            if (user == null)
                return false;

            try
            {
                _context.Users.Update(user);
                await _context.SaveChangesAsync();
                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> DeleteUserAsync(ApplicationUser user)
        {
            if (user == null)
                return false;

            try
            {
                _context.Users.Remove(user);
                await _context.SaveChangesAsync();
                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task<IList<ApplicationUser>> GetAllUsersAsync()
        {
            return await _context.Users.ToListAsync();
        }

        public async Task<IList<ApplicationUser>> GetUsersByRoleAsync(string role)
        {
            if (string.IsNullOrWhiteSpace(role))
                return new List<ApplicationUser>();

            return await _context.Users
                .Where(u => u.UserType == role)
                .ToListAsync();
        }

        public async Task<string> GetUserRoleAsync(ApplicationUser user)
        {
            if (user == null)
                return null;

            var dbUser = await FindByIdAsync(user.Id);
            return dbUser?.UserType;
        }

        public async Task<bool> ChangePasswordAsync(ApplicationUser user, string oldPassword, string newPassword)
        {
            if (user == null || string.IsNullOrWhiteSpace(oldPassword) || string.IsNullOrWhiteSpace(newPassword))
                return false;

            var isValid = await CheckPasswordAsync(user, oldPassword);
            if (!isValid)
                return false;

            user.PasswordHash = _passwordHasher.HashPassword(user, newPassword);

            return await UpdateUserAsync(user);
        }

        public async Task<bool> ResetPasswordAsync(ApplicationUser user, string newPassword)
        {
            if (user == null || string.IsNullOrWhiteSpace(newPassword))
                return false;

            user.PasswordHash = _passwordHasher.HashPassword(user, newPassword);

            return await UpdateUserAsync(user);
        }
    }
}
