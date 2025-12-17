using Microsoft.Extensions.Caching.Memory;
using System.Security.Cryptography;
using System.Text;

namespace UniversityEventsManagement.Services
{
    public interface ITwoFactorAuthService
    {
        Task<string> GenerateOTPAsync(string userId);
        Task<bool> VerifyOTPAsync(string userId, string code);
        Task<bool> Is2FAEnabledAsync(string userId);
        Task<bool> Enable2FAAsync(string userId);
        Task<bool> Disable2FAAsync(string userId);
        Task<string> GenerateEmailVerificationCodeAsync(string userId);
        Task<bool> VerifyEmailCodeAsync(string userId, string code);
        Task<string> GeneratePasswordResetCodeAsync(string userId);
        Task<bool> VerifyPasswordResetCodeAsync(string userId, string code);
        Task<string> GeneratePasswordChangeVerificationCodeAsync(string userId);
        Task<bool> VerifyPasswordChangeCodeAsync(string userId, string code);
    }

    public class TwoFactorAuthService : ITwoFactorAuthService
    {
        private readonly IMemoryCache _cache;
        private readonly IEmailService _emailService;
        private readonly IUserService _userService;
        private readonly ILogger<TwoFactorAuthService> _logger;
        private const int OTP_EXPIRY_MINUTES = 10;
        private const int OTP_LENGTH = 6;

        public TwoFactorAuthService(
            IMemoryCache cache,
            IEmailService emailService,
            IUserService userService,
            ILogger<TwoFactorAuthService> logger)
        {
            _cache = cache;
            _emailService = emailService;
            _userService = userService;
            _logger = logger;
        }

        public Task<string> GenerateOTPAsync(string userId)
        {
            var code = GenerateRandomCode(OTP_LENGTH);
            
            var cacheKey = $"2FA_OTP_{userId}";
            _cache.Set(cacheKey, code, TimeSpan.FromMinutes(OTP_EXPIRY_MINUTES));
            
            _cache.Set($"{cacheKey}_timestamp", DateTime.UtcNow, TimeSpan.FromMinutes(OTP_EXPIRY_MINUTES));
            
            _logger.LogInformation($"Generated OTP for user {userId}");
            
            return Task.FromResult(code);
        }

        public Task<bool> VerifyOTPAsync(string userId, string code)
        {
            if (string.IsNullOrWhiteSpace(code) || code.Length != OTP_LENGTH)
                return Task.FromResult(false);

            var cacheKey = $"2FA_OTP_{userId}";
            
            if (!_cache.TryGetValue(cacheKey, out string? storedCode) || string.IsNullOrEmpty(storedCode))
            {
                _logger.LogWarning($"OTP verification failed: No OTP found for user {userId}");
                return Task.FromResult(false);
            }

            var isValid = ConstantTimeEquals(storedCode, code);
            
            if (isValid)
            {
                _cache.Remove(cacheKey);
                _cache.Remove($"{cacheKey}_timestamp");
                _logger.LogInformation($"OTP verified successfully for user {userId}");
                return Task.FromResult(true);
            }

            _logger.LogWarning($"OTP verification failed: Invalid code for user {userId}");
            return Task.FromResult(false);
        }

        public async Task<bool> Is2FAEnabledAsync(string userId)
        {
            var user = await _userService.FindByIdAsync(userId);
            return user?.TwoFactorEnabled ?? false;
        }

        public async Task<bool> Enable2FAAsync(string userId)
        {
            var user = await _userService.FindByIdAsync(userId);
            if (user == null)
                return false;

            user.TwoFactorEnabled = true;
            return await _userService.UpdateUserAsync(user);
        }

        public async Task<bool> Disable2FAAsync(string userId)
        {
            var user = await _userService.FindByIdAsync(userId);
            if (user == null)
                return false;

            user.TwoFactorEnabled = false;
            user.TwoFactorSecret = null;
            user.TwoFactorBackupCodes = null;
            return await _userService.UpdateUserAsync(user);
        }

        private string GenerateRandomCode(int length)
        {
            var random = RandomNumberGenerator.GetInt32(0, (int)Math.Pow(10, length));
            return random.ToString($"D{length}");
        }

        private bool ConstantTimeEquals(string a, string b)
        {
            if (a == null || b == null || a.Length != b.Length)
                return false;

            var result = 0;
            for (int i = 0; i < a.Length; i++)
            {
                result |= a[i] ^ b[i];
            }
            return result == 0;
        }

        public Task<string> GenerateEmailVerificationCodeAsync(string userId)
        {
            var code = GenerateRandomCode(OTP_LENGTH);
            
            var cacheKey = $"EMAIL_VERIFY_{userId}";
            _cache.Set(cacheKey, code, TimeSpan.FromMinutes(OTP_EXPIRY_MINUTES));
            
            _cache.Set($"{cacheKey}_timestamp", DateTime.UtcNow, TimeSpan.FromMinutes(OTP_EXPIRY_MINUTES));
            
            _logger.LogInformation($"Generated email verification code for user {userId}");
            
            return Task.FromResult(code);
        }

        public Task<bool> VerifyEmailCodeAsync(string userId, string code)
        {
            if (string.IsNullOrWhiteSpace(code) || code.Length != OTP_LENGTH)
                return Task.FromResult(false);

            var cacheKey = $"EMAIL_VERIFY_{userId}";
            
            if (!_cache.TryGetValue(cacheKey, out string? storedCode) || string.IsNullOrEmpty(storedCode))
            {
                _logger.LogWarning($"Email verification failed: No code found for user {userId}");
                return Task.FromResult(false);
            }

            var isValid = ConstantTimeEquals(storedCode, code);
            
            if (isValid)
            {
                _cache.Remove(cacheKey);
                _cache.Remove($"{cacheKey}_timestamp");
                _logger.LogInformation($"Email verified successfully for user {userId}");
                return Task.FromResult(true);
            }

            _logger.LogWarning($"Email verification failed: Invalid code for user {userId}");
            return Task.FromResult(false);
        }

        public Task<string> GeneratePasswordResetCodeAsync(string userId)
        {
            var code = GenerateRandomCode(OTP_LENGTH);
            
            var cacheKey = $"PASSWORD_RESET_{userId}";
            _cache.Set(cacheKey, code, TimeSpan.FromMinutes(OTP_EXPIRY_MINUTES));
            
            _cache.Set($"{cacheKey}_timestamp", DateTime.UtcNow, TimeSpan.FromMinutes(OTP_EXPIRY_MINUTES));
            
            _logger.LogInformation($"Generated password reset code for user {userId}");
            
            return Task.FromResult(code);
        }

        public Task<bool> VerifyPasswordResetCodeAsync(string userId, string code)
        {
            if (string.IsNullOrWhiteSpace(code) || code.Length != OTP_LENGTH)
                return Task.FromResult(false);

            var cacheKey = $"PASSWORD_RESET_{userId}";
            
            if (!_cache.TryGetValue(cacheKey, out string? storedCode) || string.IsNullOrEmpty(storedCode))
            {
                _logger.LogWarning($"Password reset verification failed: No code found for user {userId}");
                return Task.FromResult(false);
            }

            var isValid = ConstantTimeEquals(storedCode, code);
            
            if (isValid)
            {
                _cache.Remove(cacheKey);
                _cache.Remove($"{cacheKey}_timestamp");
                _logger.LogInformation($"Password reset code verified successfully for user {userId}");
                return Task.FromResult(true);
            }

            _logger.LogWarning($"Password reset verification failed: Invalid code for user {userId}");
            return Task.FromResult(false);
        }

        public Task<string> GeneratePasswordChangeVerificationCodeAsync(string userId)
        {
            var code = GenerateRandomCode(OTP_LENGTH);
            
            var cacheKey = $"PASSWORD_CHANGE_VERIFY_{userId}";
            _cache.Set(cacheKey, code, TimeSpan.FromMinutes(OTP_EXPIRY_MINUTES));
            
            _cache.Set($"{cacheKey}_timestamp", DateTime.UtcNow, TimeSpan.FromMinutes(OTP_EXPIRY_MINUTES));
            
            _logger.LogInformation($"Generated password change verification code for user {userId}");
            
            return Task.FromResult(code);
        }

        public Task<bool> VerifyPasswordChangeCodeAsync(string userId, string code)
        {
            if (string.IsNullOrWhiteSpace(code) || code.Length != OTP_LENGTH)
                return Task.FromResult(false);

            var cacheKey = $"PASSWORD_CHANGE_VERIFY_{userId}";
            
            if (!_cache.TryGetValue(cacheKey, out string? storedCode) || string.IsNullOrEmpty(storedCode))
            {
                _logger.LogWarning($"Password change verification failed: No code found for user {userId}");
                return Task.FromResult(false);
            }

            var isValid = ConstantTimeEquals(storedCode, code);
            
            if (isValid)
            {
                _cache.Remove(cacheKey);
                _cache.Remove($"{cacheKey}_timestamp");
                _logger.LogInformation($"Password change code verified successfully for user {userId}");
                return Task.FromResult(true);
            }

            _logger.LogWarning($"Password change verification failed: Invalid code for user {userId}");
            return Task.FromResult(false);
        }
    }
}

