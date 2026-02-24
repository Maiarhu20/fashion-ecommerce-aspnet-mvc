using Core.DTOs.Admin;
using Core.Services;
using Core.Services.Email;
using Domain.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

namespace Web.Areas.Admin.Controllers
{
    [Area("Admin")]
    public class AuthController : Controller
    {
        private readonly AdminAuthService _authService;
        private readonly IEmailService _emailService;
        private readonly ILogger<AuthController> _logger;

        public AuthController(
            AdminAuthService authService,
            IEmailService emailService,
            ILogger<AuthController> logger)
        {
            _authService = authService;
            _emailService = emailService;
            _logger = logger;
        }

        [HttpGet]
        [AllowAnonymous]
        public IActionResult Login()
        {
            try
            {
                if (User.Identity?.IsAuthenticated == true)
                {
                    _logger.LogInformation("User already authenticated, redirecting to dashboard");
                    return RedirectToAction("Index", "Dashboard");
                }

                return View();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in Login GET action");
                TempData["ErrorMessage"] = "An error occurred while loading the login page.";
                return View();
            }
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(AdminLoginDto model)
        {
            if (!ModelState.IsValid)
            {
                TempData["ErrorMessage"] = "Please fix the validation errors.";
                return View(model);
            }

            try
            {
                _logger.LogInformation("Login attempt for user: {Username}", model.Username);

                var result = await _authService.AuthenticateAsync(model.Username, model.Password, model.RememberMe);

                if (result.Succeeded)
                {
                    _logger.LogInformation("Successful login for user: {Username}", model.Username);

                    if (!string.IsNullOrEmpty(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
                    {
                        return Redirect(model.ReturnUrl);
                    }

                    return RedirectToAction("Index", "Dashboard", new { area = "Admin" });
                }

                // Handle different failure scenarios
                if (result.RequiresTwoFactor)
                {
                    TempData["ErrorMessage"] = "Two-factor authentication is required.";
                }
                else if (result.IsLockedOut)
                {
                    TempData["ErrorMessage"] = "Your account has been locked due to multiple failed attempts. Please try again later.";
                }
                else
                {
                    TempData["ErrorMessage"] = result.ErrorMessage ?? "Invalid login attempt.";
                }

                _logger.LogWarning("Failed login attempt for user: {Username}. Reason: {Reason}",
                    model.Username, TempData["ErrorMessage"]);

                return View(model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during login for user: {Username}", model.Username);
                TempData["ErrorMessage"] = "An unexpected error occurred during login. Please try again.";
                return View(model);
            }
        }

        [HttpPost]
        [Authorize(Policy = "AdminOnly")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            try
            {
                await _authService.LogoutAsync();
                _logger.LogInformation("User logged out successfully");

                return RedirectToAction("Login", "Auth");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during logout");
                TempData["ErrorMessage"] = "An error occurred during logout.";
                return RedirectToAction("Index", "Dashboard");
            }
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
        public async Task<IActionResult> ForgotPassword(ForgotPassword model)
        {
            if (!ModelState.IsValid)
            {
                TempData["ErrorMessage"] = "Please enter a valid email address.";
                return View(model);
            }

            try
            {
                _logger.LogInformation("Processing forgot password request for email: {Email}", model.Email);

                var user = await _authService.GetAdminByEmailAsync(model.Email);

                if (user == null)
                {
                    _logger.LogWarning("Forgot password request for non-existent email: {Email}", model.Email);
                    return View("ForgotPasswordConfirmation");
                }

                // Generate reset token
                var token = await _authService.GeneratePasswordResetTokenAsync(user);

                // **********************************************
                // *** CRITICAL FIX: SPECIFY DOMAIN HOST HERE ***
                // **********************************************
                const string domain = "";

                var callbackUrl = Url.Action(
                    action: "ResetPassword",
                    controller: "Auth",
                    values: new { area = "Admin", userId = user.Id, token = token },
                    protocol: Request.Scheme,
                    host: domain); // <--- FIX IS HERE

                _logger.LogInformation("Generated password reset link for user: {UserName}", user.UserName);
                _logger.LogInformation("Reset URL: {ResetUrl}", callbackUrl);

                // Send email
                var emailResult = await _emailService.SendPasswordResetEmailAsync(
                    user.Email, user.Name, callbackUrl);

                if (emailResult.Success)
                {
                    _logger.LogInformation("Password reset email sent successfully to: {Email}", user.Email);
                }
                else
                {
                    _logger.LogError("Failed to send password reset email to {Email}. Error: {Error}",
                        user.Email, emailResult.Error);
                }

                return View("ForgotPasswordConfirmation");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error processing forgot password for email: {Email}", model.Email);
                TempData["ErrorMessage"] = "An unexpected error occurred. Please try again.";
                return View(model);
            }
        }


        [HttpGet]
        [AllowAnonymous]
        public IActionResult ResetPassword(string userId, string token)
        {
            try
            {
                if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(token))
                {
                    _logger.LogWarning("Reset password accessed with invalid parameters");
                    TempData["ErrorMessage"] = "Invalid password reset link.";
                    return RedirectToAction("Login");
                }

                var model = new ResetPasswordDto
                {
                    UserId = userId,
                    Token = token
                };

                _logger.LogInformation("Reset password page loaded for user ID: {UserId}", userId);
                _logger.LogInformation("Token received (first 50 chars): {TokenPreview}",
                    token.Length > 50 ? token.Substring(0, 50) + "..." : token);

                return View(model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in ResetPassword GET action");
                TempData["ErrorMessage"] = "An error occurred while loading the reset password page.";
                return RedirectToAction("Login");
            }
        }


        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(ResetPasswordDto model)
        {
            if (!ModelState.IsValid)
            {
                TempData["ErrorMessage"] = "Please fix the validation errors.";
                return View(model);
            }

            try
            {
                _logger.LogInformation("Processing password reset for user ID: {UserId}", model.UserId);

                var user = await _authService.GetAdminByIdAsync(model.UserId);

                if (user == null)
                {
                    _logger.LogWarning("Password reset attempt for non-existent admin user ID: {UserId}", model.UserId);
                    TempData["ErrorMessage"] = "Invalid admin account.";
                    return View(model);
                }

                _logger.LogInformation("User found: {UserName}, Email: {UserEmail}", user.UserName, user.Email);

                if (user.Email != model.Email)
                {
                    _logger.LogWarning("Email mismatch for password reset. User email: {UserEmail}, Provided email: {ProvidedEmail}",
                        user.Email, model.Email);
                    TempData["ErrorMessage"] = "Email does not match the account.";
                    return View(model);
                }

                var isValidToken = await _authService.ValidatePasswordResetTokenAsync(user, model.Token);
                if (!isValidToken)
                {
                    _logger.LogError("Token validation failed for user: {UserName}", user.UserName);
                    TempData["ErrorMessage"] = "The reset link has expired or is invalid. Please request a new password reset.";
                    return View(model);
                }

                _logger.LogInformation("Token validated successfully for user: {UserName}", user.UserName);

                var result = await _authService.ResetPasswordAsync(user, model.Token, model.NewPassword);

                if (result.Succeeded)
                {
                    _logger.LogInformation("✅ Password reset successful for user: {UserName}", user.UserName);

                    // Send confirmation email (simplified for this example)
                    await _emailService.SendEmailAsync(
                        user.Email,
                        "Password Reset Confirmation ",
                        $@"
                <div style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto;'>
                    <h2 style='color: #912356;'>Password Reset Successful</h2>
                    <p>Hello {user.Name},</p>
                    <p>Your password has been successfully reset for your  admin account.</p>
                </div>");

                    return View("ResetPasswordConfirmation");
                }

                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                    _logger.LogError("Password reset error for user {UserName}: {ErrorCode} - {ErrorDescription}",
                        user.UserName, error.Code, error.Description);
                }

                TempData["ErrorMessage"] = "Failed to reset password. Please check the errors above.";
                return View(model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during password reset for user ID: {UserId}", model.UserId);
                TempData["ErrorMessage"] = "An unexpected error occurred. Please try again.";
                return View(model);
            }
        }


        [HttpGet]
        [Authorize(Roles = "Admin")]
        public IActionResult ChangePassword()
        {
            return View();
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePassword(ChangePasswordDto model)
        {
            if (!ModelState.IsValid)
            {
                TempData["ErrorMessage"] = "Please fix the validation errors.";
                return View(model);
            }

            try
            {
                var user = await _authService.GetCurrentAdminUserAsync();
                if (user == null)
                {
                    _logger.LogWarning("Change password attempted with null current user");
                    return Unauthorized();
                }

                var result = await _authService.ChangePasswordAsync(user, model.CurrentPassword, model.NewPassword);

                if (result.Succeeded)
                {
                    _logger.LogInformation("Password changed successfully for user: {UserName}", user.UserName);
                    TempData["SuccessMessage"] = "Your password has been changed successfully.";
                    return RedirectToAction("Index", "Dashboard");
                }

                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }

                TempData["ErrorMessage"] = "Failed to change password. Please check the errors above.";
                return View(model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during password change");
                TempData["ErrorMessage"] = "An unexpected error occurred. Please try again.";
                return View(model);
            }
        }

        [HttpGet]
        [AllowAnonymous]
        public IActionResult AccessDenied()
        {
            _logger.LogWarning("Access denied for user: {UserName}", User.Identity?.Name);
            return View();
        }

        [HttpGet]
        [AllowAnonymous]
        public IActionResult ForgotPasswordConfirmation()
        {
            return View();
        }

        [HttpGet]
        [AllowAnonymous]
        public IActionResult ResetPasswordConfirmation()
        {
            return View();
        }
    }
}