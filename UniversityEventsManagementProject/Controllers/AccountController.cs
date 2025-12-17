#nullable disable
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Threading.Tasks;
using UniversityEventsManagement.Data;
using UniversityEventsManagement.Models;
using UniversityEventsManagement.Services;

namespace UniversityEventsManagement.Controllers
{
    public class AccountController : Controller
    {
        private readonly IUserService _userService;
        private readonly ApplicationDbContext _context;
        private readonly IStringLocalizer _localizer;
        private readonly ITwoFactorAuthService _twoFactorAuthService;
        private readonly IEmailService _emailService;

        public AccountController(
            IUserService userService,
            ApplicationDbContext context,
            IStringLocalizer localizer,
            ITwoFactorAuthService twoFactorAuthService,
            IEmailService emailService)
        {
            _userService = userService;
            _context = context;
            _localizer = localizer;
            _twoFactorAuthService = twoFactorAuthService;
            _emailService = emailService;
        }

        [HttpGet]
        [AllowAnonymous]
        public IActionResult Register()
        {
            return View();
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            if (ModelState.IsValid)
            {
                var normalizedEmail = model.Email?.ToLowerInvariant().Trim() ?? "";

                // إنشاء مستخدم جديد: يتم تعيين نوع المستخدم كطالب افتراضياً
                var user = new ApplicationUser
                {
                    Id = Guid.NewGuid().ToString(),
                    Email = normalizedEmail,
                    FirstName = model.FirstName,
                    LastName = model.LastName,
                    UniversityID = model.UniversityID?.Trim(),
                    UserType = "Student", // نوع المستخدم الافتراضي هو طالب
                    JoinDate = DateTime.Now,
                    IsActive = true,
                    EmailConfirmed = false, // سيتم تأكيد البريد لاحقاً
                    Department = ""
                };

                var result = await _userService.CreateUserAsync(user, model.Password);
                if (result)
                {
                    var verificationCode = await _twoFactorAuthService.GenerateEmailVerificationCodeAsync(user.Id);
                    var userName = !string.IsNullOrEmpty(user?.FirstName) ? $"{user.FirstName} {user.LastName}".Trim() : user?.Email ?? _localizer["User"].Value;
                    await _emailService.SendEmailVerificationCodeAsync(user.Email, userName, verificationCode);
                    HttpContext.Session.SetString("EmailVerify_UserId", user.Id);
                    TempData["Info"] = "تم إرسال رمز التحقق إلى بريدك الإلكتروني. الرجاء إدخال الرمز لإتمام التسجيل.";
                    return RedirectToAction("VerifyEmail");
                }
                else
                {
                    ModelState.AddModelError(string.Empty, "حدث خطأ أثناء إنشاء الحساب. يرجى المحاولة مرة أخرى أو التواصل مع الدعم.");
                }
            }

            return View(model);
        }

        [HttpGet]
        [AllowAnonymous]
        public IActionResult Login(string returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            return View();
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model, string returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;

            if (ModelState.IsValid)
            {
                ApplicationUser user = null;
                var loginInput = model.Email?.Trim() ?? "";
                
                if (string.IsNullOrEmpty(loginInput))
                {
                    ModelState.AddModelError(string.Empty, "يرجى إدخال البريد الإلكتروني أو الرقم الجامعي");
                    return View(model);
                }

                bool isEmail = loginInput.Contains("@");

                if (isEmail)
                {
                    user = await _userService.FindByEmailAsync(loginInput);
                }
                else
                {
                    user = await _userService.FindByUniversityIdAsync(loginInput);
                }

                if (user == null)
                {
                    ModelState.AddModelError(string.Empty, "البريد الإلكتروني أو الرقم الجامعي غير صحيح");
                    return View(model);
                }

                if (!user.IsActive)
                {
                    ModelState.AddModelError(string.Empty, "الحساب موقوف حالياً. يرجى التواصل مع الإدارة.");
                    return View(model);
                }

                // منع تسجيل الدخول قبل تأكيد البريد: إعادة إرسال رمز التحقق والتحويل لصفحة VerifyEmail
                // تم تعليق التحقق البريدي عند الدخول
                // التحقق من تأكيد البريد الإلكتروني قبل السماح بتسجيل الدخول
                if (!user.EmailConfirmed)
                {
                    var verificationCode = await _twoFactorAuthService.GenerateEmailVerificationCodeAsync(user.Id);
                    var userNameVerify = !string.IsNullOrEmpty(user?.FirstName) ? $"{user.FirstName} {user.LastName}".Trim() : user?.Email ?? _localizer["User"].Value;
                    await _emailService.SendEmailVerificationCodeAsync(user.Email, userNameVerify, verificationCode);

                    HttpContext.Session.SetString("EmailVerify_UserId", user.Id);
                    TempData["Info"] = "لم يتم تأكيد بريدك الإلكتروني بعد. تم إرسال رمز التحقق إلى بريدك. يرجى إدخال الرمز للمتابعة.";
                    return RedirectToAction("VerifyEmail");
                }
                var passwordValid = await _userService.CheckPasswordAsync(user, model.Password);
                if (!passwordValid)
                {
                    ModelState.AddModelError(string.Empty, "كلمة المرور غير صحيحة");
                    return View(model);
                }

                var is2FAEnabled = await _twoFactorAuthService.Is2FAEnabledAsync(user.Id);
                
                if (is2FAEnabled)
                {
                    var otpCode = await _twoFactorAuthService.GenerateOTPAsync(user.Id);
                    
                    var userName = !string.IsNullOrEmpty(user?.FirstName) ? $"{user.FirstName} {user.LastName}".Trim() : user?.Email ?? _localizer["User"].Value;
                    await _emailService.Send2FAOTPAsync(user.Email, userName, otpCode);
                    
                    HttpContext.Session.SetString("2FA_UserId", user.Id);
                    HttpContext.Session.SetString("2FA_ReturnUrl", returnUrl ?? "");
                    
                    TempData["Info"] = "تم إرسال رمز التحقق إلى بريدك الإلكتروني. يرجى إدخال الرمز للمتابعة.";
                    return RedirectToAction("Verify2FA");
                }
                
                await SignInUserAsync(user);

                var userName2 = !string.IsNullOrEmpty(user?.FirstName) ? $"{user.FirstName} {user.LastName}".Trim() : user?.Email ?? _localizer["User"].Value;
                TempData["Success"] = string.Format(_localizer["LoginSuccess"].Value, userName2);
                
                if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                {
                    return Redirect(returnUrl);
                }
                
                if (user.UserType == "Admin")
                {
                    return RedirectToAction("Dashboard", "Admin");
                }
                else
                {
                    return RedirectToAction("Dashboard", "Home");
                }
            }

            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            TempData["Success"] = _localizer["LogoutSuccess"].Value;
            return RedirectToAction("Index", "Home");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> LogoutPost()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            TempData["Success"] = _localizer["LogoutSuccess"].Value;
            return RedirectToAction("Index", "Home");
        }

        [Authorize]
        public async Task<IActionResult> Manage()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
                return NotFound();

            var user = await _userService.FindByIdAsync(userId);
            if (user == null)
                return NotFound();

            var model = new ManageViewModel
            {
                FirstName = user.FirstName,
                LastName = user.LastName,
                Email = user.Email,
                PhoneNumber = user.PhoneNumber,
                UniversityID = user.UniversityID,
                Department = user.Department
            };

            return View(model);
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Manage(ManageViewModel model)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
                return NotFound();

            var user = await _userService.FindByIdAsync(userId);
            if (user == null)
                return NotFound();

            if (ModelState.IsValid)
            {
                user.FirstName = model.FirstName;
                user.LastName = model.LastName;
                user.PhoneNumber = model.PhoneNumber;
                user.Department = model.Department;

                var result = await _userService.UpdateUserAsync(user);
                if (result)
                {
                    TempData["Success"] = _localizer["AccountUpdatedSuccessfully"].Value;
                    return RedirectToAction(nameof(Manage));
                }
                else
                {
                    ModelState.AddModelError(string.Empty, "حدث خطأ أثناء تحديث البيانات");
                }
            }

            return View(model);
        }

        [Authorize]
        public IActionResult ChangePassword()
        {
            return View();
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
                return NotFound();

            var user = await _userService.FindByIdAsync(userId);
            if (user == null)
                return NotFound();

            var result = await _userService.ChangePasswordAsync(user, model.OldPassword, model.NewPassword);
            if (result)
            {
                TempData["Success"] = _localizer["PasswordChangedSuccessfully"].Value;
                return RedirectToAction(nameof(Manage));
            }
            else
            {
                ModelState.AddModelError(string.Empty, "كلمة المرور القديمة غير صحيحة أو حدث خطأ");
            }

            return View(model);
        }

        [HttpGet]
        [AllowAnonymous]
        public IActionResult Verify2FA()
        {
            var userId = HttpContext.Session.GetString("2FA_UserId");
            if (string.IsNullOrEmpty(userId))
            {
                TempData["Error"] = "انتهت صلاحية الجلسة. يرجى تسجيل الدخول مرة أخرى.";
                return RedirectToAction("Login");
            }

            return View();
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Verify2FA(Verify2FAViewModel model)
        {
            var userId = HttpContext.Session.GetString("2FA_UserId");
            var returnUrl = HttpContext.Session.GetString("2FA_ReturnUrl");

            if (string.IsNullOrEmpty(userId))
            {
                TempData["Error"] = "انتهت صلاحية الجلسة. يرجى تسجيل الدخول مرة أخرى.";
                return RedirectToAction("Login");
            }

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var isValid = await _twoFactorAuthService.VerifyOTPAsync(userId, model.Code);
            
            if (!isValid)
            {
                ModelState.AddModelError(string.Empty, "رمز التحقق غير صحيح. يرجى المحاولة مرة أخرى.");
                return View(model);
            }

            var user = await _userService.FindByIdAsync(userId);
            if (user == null)
            {
                TempData["Error"] = "حدث خطأ. يرجى المحاولة مرة أخرى.";
                return RedirectToAction("Login");
            }

            HttpContext.Session.Remove("2FA_UserId");
            HttpContext.Session.Remove("2FA_ReturnUrl");

            await SignInUserAsync(user);

            var userName = !string.IsNullOrEmpty(user?.FirstName) ? $"{user.FirstName} {user.LastName}".Trim() : user?.Email ?? _localizer["User"].Value;
            TempData["Success"] = string.Format(_localizer["LoginSuccess"].Value, userName);

            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }

            if (user.UserType == "Admin")
            {
                return RedirectToAction("Dashboard", "Admin");
            }
            else
            {
                return RedirectToAction("Dashboard", "Home");
            }
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Resend2FAOTP()
        {
            var userId = HttpContext.Session.GetString("2FA_UserId");
            if (string.IsNullOrEmpty(userId))
            {
                TempData["Error"] = "انتهت صلاحية الجلسة.";
                return RedirectToAction("Login");
            }

            var user = await _userService.FindByIdAsync(userId);
            if (user == null)
            {
                TempData["Error"] = "حدث خطأ. يرجى المحاولة مرة أخرى.";
                return RedirectToAction("Login");
            }

            var otpCode = await _twoFactorAuthService.GenerateOTPAsync(user.Id);
            var userName = !string.IsNullOrEmpty(user?.FirstName) ? $"{user.FirstName} {user.LastName}".Trim() : user?.Email ?? _localizer["User"].Value;
            await _emailService.Send2FAOTPAsync(user.Email, userName, otpCode);

            TempData["Info"] = "تم إرسال رمز تحقق جديد إلى بريدك الإلكتروني.";
            return RedirectToAction("Verify2FA");
        }

        [Authorize]
        [HttpGet]
        public async Task<IActionResult> Enable2FA()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
                return NotFound();

            var user = await _userService.FindByIdAsync(userId);
            if (user == null)
                return NotFound();

            var isEnabled = await _twoFactorAuthService.Is2FAEnabledAsync(userId);
            
            return View(new Enable2FAViewModel { IsEnabled = isEnabled });
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Enable2FA(Enable2FAViewModel model)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
                return NotFound();

            if (model.Enable)
            {
                var result = await _twoFactorAuthService.Enable2FAAsync(userId);
                if (result)
                {
                    TempData["Success"] = "تم تفعيل المصادقة الثنائية بنجاح.";
                }
                else
                {
                    TempData["Error"] = "حدث خطأ أثناء تفعيل المصادقة الثنائية.";
                }
            }
            else
            {
                var result = await _twoFactorAuthService.Disable2FAAsync(userId);
                if (result)
                {
                    TempData["Success"] = "تم تعطيل المصادقة الثنائية بنجاح.";
                }
                else
                {
                    TempData["Error"] = "حدث خطأ أثناء تعطيل المصادقة الثنائية.";
                }
            }

            return RedirectToAction(nameof(Enable2FA));
        }

        [HttpGet]
        [AllowAnonymous]
        public IActionResult VerifyEmail()
        {
            var userId = HttpContext.Session.GetString("EmailVerify_UserId");
            if (string.IsNullOrEmpty(userId))
            {
                TempData["Error"] = "انتهت صلاحية الجلسة. يرجى التسجيل مرة أخرى.";
                return RedirectToAction("Register");
            }

            return View();
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> VerifyEmail(VerifyEmailViewModel model)
        {
            var userId = HttpContext.Session.GetString("EmailVerify_UserId");

            if (string.IsNullOrEmpty(userId))
            {
                TempData["Error"] = "انتهت صلاحية الجلسة. يرجى التسجيل مرة أخرى.";
                return RedirectToAction("Register");
            }

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var isValid = await _twoFactorAuthService.VerifyEmailCodeAsync(userId, model.Code);
            
            if (!isValid)
            {
                ModelState.AddModelError(string.Empty, "رمز التحقق غير صحيح. يرجى المحاولة مرة أخرى.");
                return View(model);
            }

            var user = await _userService.FindByIdAsync(userId);
            if (user == null)
            {
                TempData["Error"] = "حدث خطأ. يرجى المحاولة مرة أخرى.";
                return RedirectToAction("Register");
            }

            user.EmailConfirmed = true;
            await _userService.UpdateUserAsync(user);

            HttpContext.Session.Remove("EmailVerify_UserId");

            await SignInUserAsync(user);

            var userName = !string.IsNullOrEmpty(user?.FirstName) ? $"{user.FirstName} {user.LastName}".Trim() : user?.Email ?? _localizer["User"].Value;
            TempData["Success"] = string.Format(_localizer["AccountCreatedSuccessfully"].Value, userName);
            
            if (user.UserType == "Admin")
            {
                return RedirectToAction("Dashboard", "Admin");
            }
            else
            {
                return RedirectToAction("Dashboard", "Home");
            }
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResendEmailVerificationCode()
        {
            var userId = HttpContext.Session.GetString("EmailVerify_UserId");
            if (string.IsNullOrEmpty(userId))
            {
                TempData["Error"] = "انتهت صلاحية الجلسة.";
                return RedirectToAction("Register");
            }

            var user = await _userService.FindByIdAsync(userId);
            if (user == null)
            {
                TempData["Error"] = "حدث خطأ. يرجى المحاولة مرة أخرى.";
                return RedirectToAction("Register");
            }

            var verificationCode = await _twoFactorAuthService.GenerateEmailVerificationCodeAsync(user.Id);
            var userName = !string.IsNullOrEmpty(user?.FirstName) ? $"{user.FirstName} {user.LastName}".Trim() : user?.Email ?? "المستخدم";
            await _emailService.SendEmailVerificationCodeAsync(user.Email, userName, verificationCode);

            TempData["Info"] = "تم إرسال رمز تحقق جديد إلى بريدك الإلكتروني.";
            return RedirectToAction("VerifyEmail");
        }

        [HttpGet]
        [Authorize]
        public IActionResult VerifyPasswordChange()
        {
            var userId = HttpContext.Session.GetString("PasswordChange_UserId");
            if (string.IsNullOrEmpty(userId))
            {
                TempData["Error"] = "انتهت صلاحية الجلسة. يرجى المحاولة مرة أخرى.";
                return RedirectToAction("ChangePassword");
            }

            return View();
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> VerifyPasswordChange(VerifyPasswordChangeViewModel model)
        {
            var userId = HttpContext.Session.GetString("PasswordChange_UserId");
            var newPassword = HttpContext.Session.GetString("PasswordChange_NewPassword");
            var oldPassword = HttpContext.Session.GetString("PasswordChange_OldPassword");

            if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(newPassword))
            {
                TempData["Error"] = "انتهت صلاحية الجلسة. يرجى المحاولة مرة أخرى.";
                return RedirectToAction("ChangePassword");
            }

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var isValid = await _twoFactorAuthService.VerifyPasswordChangeCodeAsync(userId, model.Code);
            
            if (!isValid)
            {
                ModelState.AddModelError(string.Empty, "رمز التحقق غير صحيح. يرجى المحاولة مرة أخرى.");
                return View(model);
            }

            var user = await _userService.FindByIdAsync(userId);
            if (user == null)
            {
                TempData["Error"] = "حدث خطأ. يرجى المحاولة مرة أخرى.";
                return RedirectToAction("ChangePassword");
            }

            var result = await _userService.ChangePasswordAsync(user, oldPassword, newPassword);
            
            HttpContext.Session.Remove("PasswordChange_UserId");
            HttpContext.Session.Remove("PasswordChange_NewPassword");
            HttpContext.Session.Remove("PasswordChange_OldPassword");

            if (result)
            {
                TempData["Success"] = "تم تغيير كلمة المرور بنجاح.";
                return RedirectToAction("Manage");
            }
            else
            {
                TempData["Error"] = "حدث خطأ أثناء تغيير كلمة المرور.";
                return RedirectToAction("ChangePassword");
            }
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResendPasswordChangeCode()
        {
            var userId = HttpContext.Session.GetString("PasswordChange_UserId");
            if (string.IsNullOrEmpty(userId))
            {
                TempData["Error"] = "انتهت صلاحية الجلسة.";
                return RedirectToAction("ChangePassword");
            }

            var user = await _userService.FindByIdAsync(userId);
            if (user == null)
            {
                TempData["Error"] = "حدث خطأ. يرجى المحاولة مرة أخرى.";
                return RedirectToAction("ChangePassword");
            }

            var verificationCode = await _twoFactorAuthService.GeneratePasswordChangeVerificationCodeAsync(user.Id);
            var userName = !string.IsNullOrEmpty(user?.FirstName) ? $"{user.FirstName} {user.LastName}".Trim() : user?.Email ?? "المستخدم";
            await _emailService.SendPasswordChangeVerificationCodeAsync(user.Email, userName, verificationCode);

            TempData["Info"] = "تم إرسال رمز تحقق جديد إلى بريدك الإلكتروني.";
            return RedirectToAction("VerifyPasswordChange");
        }

        [HttpGet]
        [AllowAnonymous]
        public IActionResult ForgotPassword()
        {
            return View();
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var user = await _userService.FindByEmailAsync(model.Email);
            
            if (user == null)
            {
                TempData["Info"] = "إذا كان البريد الإلكتروني موجوداً في نظامنا، سيتم إرسال رمز التحقق إليه.";
                return RedirectToAction("Login");
            }

            var resetCode = await _twoFactorAuthService.GeneratePasswordResetCodeAsync(user.Id);
            var userName = !string.IsNullOrEmpty(user?.FirstName) ? $"{user.FirstName} {user.LastName}".Trim() : user?.Email ?? "المستخدم";
            await _emailService.SendPasswordResetCodeAsync(user.Email, userName, resetCode);
            
            HttpContext.Session.SetString("PasswordReset_UserId", user.Id);
            
            TempData["Info"] = "تم إرسال رمز التحقق إلى بريدك الإلكتروني. يرجى إدخال الرمز لإعادة تعيين كلمة المرور.";
            return RedirectToAction("ResetPassword");
        }

        [HttpGet]
        [AllowAnonymous]
        public IActionResult ResetPassword()
        {
            var userId = HttpContext.Session.GetString("PasswordReset_UserId");
            if (string.IsNullOrEmpty(userId))
            {
                TempData["Error"] = "انتهت صلاحية الجلسة. يرجى المحاولة مرة أخرى.";
                return RedirectToAction("ForgotPassword");
            }

            return View();
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
        {
            var userId = HttpContext.Session.GetString("PasswordReset_UserId");

            if (string.IsNullOrEmpty(userId))
            {
                TempData["Error"] = "انتهت صلاحية الجلسة. يرجى المحاولة مرة أخرى.";
                return RedirectToAction("ForgotPassword");
            }

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var isValid = await _twoFactorAuthService.VerifyPasswordResetCodeAsync(userId, model.Code);
            
            if (!isValid)
            {
                ModelState.AddModelError(string.Empty, "رمز التحقق غير صحيح. يرجى المحاولة مرة أخرى.");
                return View(model);
            }

            var user = await _userService.FindByIdAsync(userId);
            if (user == null)
            {
                TempData["Error"] = "حدث خطأ. يرجى المحاولة مرة أخرى.";
                return RedirectToAction("ForgotPassword");
            }

            var result = await _userService.ResetPasswordAsync(user, model.NewPassword);
            
            HttpContext.Session.Remove("PasswordReset_UserId");

            if (result)
            {
                TempData["Success"] = "تم إعادة تعيين كلمة المرور بنجاح. يمكنك الآن تسجيل الدخول.";
                return RedirectToAction("Login");
            }
            else
            {
                TempData["Error"] = "حدث خطأ أثناء إعادة تعيين كلمة المرور.";
                return RedirectToAction("ForgotPassword");
            }
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResendPasswordResetCode()
        {
            var userId = HttpContext.Session.GetString("PasswordReset_UserId");
            if (string.IsNullOrEmpty(userId))
            {
                TempData["Error"] = "انتهت صلاحية الجلسة.";
                return RedirectToAction("ForgotPassword");
            }

            var user = await _userService.FindByIdAsync(userId);
            if (user == null)
            {
                TempData["Error"] = "حدث خطأ. يرجى المحاولة مرة أخرى.";
                return RedirectToAction("ForgotPassword");
            }

            var resetCode = await _twoFactorAuthService.GeneratePasswordResetCodeAsync(user.Id);
            var userName = !string.IsNullOrEmpty(user?.FirstName) ? $"{user.FirstName} {user.LastName}".Trim() : user?.Email ?? "المستخدم";
            await _emailService.SendPasswordResetCodeAsync(user.Email, userName, resetCode);

            TempData["Info"] = "تم إرسال رمز تحقق جديد إلى بريدك الإلكتروني.";
            return RedirectToAction("ResetPassword");
        }

        private async Task SignInUserAsync(ApplicationUser user)
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.Name, $"{user.FirstName} {user.LastName}".Trim()),
                new Claim(ClaimTypes.Role, user.UserType)
            };

            var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var authProperties = new AuthenticationProperties
            {
                IsPersistent = true,
                ExpiresUtc = DateTimeOffset.UtcNow.AddDays(30)
            };

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(claimsIdentity),
                authProperties);
        }

        [HttpGet]
        [AllowAnonymous]
        public IActionResult AccessDenied(string returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            return View();
        }
    }

    public class RegisterViewModel
    {
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string Email { get; set; }
        public string UniversityID { get; set; }
        public string Password { get; set; }
        public string ConfirmPassword { get; set; }
        public string UserType { get; set; } = "Student";
    }

    public class LoginViewModel
    {
        public string Email { get; set; }
        public string UniversityID { get; set; }
        public string Password { get; set; }
        public bool RememberMe { get; set; }
        public string UserType { get; set; } = "Student";
    }

    public class ManageViewModel
    {
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string Email { get; set; }
        public string PhoneNumber { get; set; }
        public string UniversityID { get; set; }
        public string Department { get; set; }
    }

    public class ChangePasswordViewModel
    {
        public string OldPassword { get; set; }
        public string NewPassword { get; set; }
        public string ConfirmPassword { get; set; }
    }

    public class Verify2FAViewModel
    {
        [Required(ErrorMessage = "يرجى إدخال رمز التحقق")]
        [StringLength(6, MinimumLength = 6, ErrorMessage = "رمز التحقق يجب أن يكون 6 أرقام")]
        public string Code { get; set; }
    }

    public class Enable2FAViewModel
    {
        public bool IsEnabled { get; set; }
        public bool Enable { get; set; }
    }

    public class VerifyEmailViewModel
    {
        [Required(ErrorMessage = "يرجى إدخال رمز التحقق")]
        [StringLength(6, MinimumLength = 6, ErrorMessage = "رمز التحقق يجب أن يكون 6 أرقام")]
        public string Code { get; set; }
    }

    public class ForgotPasswordViewModel
    {
        [Required(ErrorMessage = "يرجى إدخال البريد الإلكتروني")]
        [EmailAddress(ErrorMessage = "البريد الإلكتروني غير صحيح")]
        public string Email { get; set; }
    }

    public class ResetPasswordViewModel
    {
        [Required(ErrorMessage = "يرجى إدخال رمز التحقق")]
        [StringLength(6, MinimumLength = 6, ErrorMessage = "رمز التحقق يجب أن يكون 6 أرقام")]
        public string Code { get; set; }
        
        [Required(ErrorMessage = "يرجى إدخال كلمة المرور الجديدة")]
        [StringLength(100, MinimumLength = 6, ErrorMessage = "كلمة المرور يجب أن تكون بين 6 و 100 حرف")]
        public string NewPassword { get; set; }
        
        [Required(ErrorMessage = "يرجى تأكيد كلمة المرور")]
        [Compare("NewPassword", ErrorMessage = "كلمات المرور غير متطابقة")]
        public string ConfirmPassword { get; set; }
    }

    public class VerifyPasswordChangeViewModel
    {
        [Required(ErrorMessage = "يرجى إدخال رمز التحقق")]
        [StringLength(6, MinimumLength = 6, ErrorMessage = "رمز التحقق يجب أن يكون 6 أرقام")]
        public string Code { get; set; }
    }
}
