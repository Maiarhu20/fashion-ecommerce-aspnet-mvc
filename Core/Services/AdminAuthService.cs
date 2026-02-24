using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Core.DTOs.Admin;
using Core.Services.Security;
using Domain.Models;
using Infrastructure.Interfaces;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

namespace Core.Services
{
    public class AuthResult //server side authentication result
    {
        public bool Succeeded { get; set; }
        public bool RequiresTwoFactor { get; set; }
        public bool IsLockedOut { get; set; }
        public string ErrorMessage { get; set; }

        public static AuthResult Success => new AuthResult { Succeeded = true };
        public static AuthResult Failure(string error) => new AuthResult { Succeeded = false, ErrorMessage = error };
    }

    public class AdminAuthService
    {
        private readonly UserManager<User> _userManager;
        private readonly SignInManager<User> _signInManager;
        private readonly ILogger<AdminAuthService> _logger;

        public AdminAuthService(
            UserManager<User> userManager,
            SignInManager<User> signInManager,
            ILogger<AdminAuthService> logger)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _logger = logger;
        }

        public async Task<AuthResult> AuthenticateAsync(string username, string password, bool rememberMe = false)
        {
            try
            {
                _logger.LogInformation("Authentication attempt for user: {Username}", username);

                // Find user by username OR email
                var user = await _userManager.FindByNameAsync(username)
                          ?? await _userManager.FindByEmailAsync(username);

                if (user == null)
                {
                    _logger.LogWarning("User not found: {Username}", username);
                    return AuthResult.Failure("Invalid login attempt.");
                }

                // Check if user is in Admin role
                var isAdmin = await _userManager.IsInRoleAsync(user, "Admin");
                if (!isAdmin)
                {
                    _logger.LogWarning("Non-admin user attempted login: {Username}", username);
                    return AuthResult.Failure("Access denied. Admin privileges required.");
                }

                // Check if account is locked out
                if (await _userManager.IsLockedOutAsync(user))
                {
                    var lockoutEnd = await _userManager.GetLockoutEndDateAsync(user);
                    _logger.LogWarning("Locked out user attempted login: {Username}. Lockout ends: {LockoutEnd}",
                        username, lockoutEnd);
                    return AuthResult.Failure("Account is temporarily locked. Please try again later.");
                }

                // Perform sign in
                var result = await _signInManager.PasswordSignInAsync(
                    user.UserName, password, rememberMe, lockoutOnFailure: true);

                if (result.Succeeded)
                {
                    _logger.LogInformation("Successful login for user: {Username}", username);

                    // Reset access failed count on successful login
                    await _userManager.ResetAccessFailedCountAsync(user);

                    return AuthResult.Success;
                }
                else if (result.RequiresTwoFactor)
                {
                    _logger.LogInformation("Two-factor required for user: {Username}", username);
                    return new AuthResult { RequiresTwoFactor = true };
                }
                else if (result.IsLockedOut)
                {
                    _logger.LogWarning("Account locked out: {Username}", username);
                    return new AuthResult { IsLockedOut = true };
                }
                else
                {
                    _logger.LogWarning("Failed login attempt for user: {Username}", username);
                    return AuthResult.Failure("Invalid username or password.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during authentication for user: {Username}", username);
                return AuthResult.Failure("An unexpected error occurred. Please try again.");
            }
        }

        public async Task LogoutAsync()
        {
            try
            {
                var userName = _signInManager.Context.User.Identity?.Name;
                await _signInManager.SignOutAsync();
                _logger.LogInformation("User logged out: {UserName}", userName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during logout");
                throw;
            }
        }

        public async Task<User> GetCurrentAdminUserAsync()
        {
            try
            {
                var user = await _userManager.GetUserAsync(_signInManager.Context.User);
                if (user == null) return null;

                var isAdmin = await _userManager.IsInRoleAsync(user, "Admin");
                return isAdmin ? user : null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting current admin user");
                return null;
            }
        }

        public async Task<IdentityResult> ChangePasswordAsync(User user, string currentPassword, string newPassword)
        {
            try
            {
                _logger.LogInformation("Changing password for user: {UserName}", user.UserName);
                var result = await _userManager.ChangePasswordAsync(user, currentPassword, newPassword);

                if (result.Succeeded)
                {
                    _logger.LogInformation("Password changed successfully for user: {UserName}", user.UserName);
                }
                else
                {
                    _logger.LogWarning("Password change failed for user: {UserName}. Errors: {Errors}",
                        user.UserName, string.Join(", ", result.Errors));
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error changing password for user: {UserName}", user.UserName);
                throw;
            }
        }

        public async Task<IdentityResult> ResetPasswordAsync(User user, string token, string newPassword)
        {
            try
            {
                _logger.LogInformation("Resetting password for user: {UserName}", user.UserName);
                var result = await _userManager.ResetPasswordAsync(user, token, newPassword);

                if (result.Succeeded)
                {
                    _logger.LogInformation("Password reset successfully for user: {UserName}", user.UserName);

                    // Unlock the account if it was locked due to failed attempts
                    if (await _userManager.IsLockedOutAsync(user))
                    {
                        await _userManager.SetLockoutEndDateAsync(user, DateTimeOffset.Now);
                        _logger.LogInformation("Account unlocked for user: {UserName}", user.UserName);
                    }
                }
                else
                {
                    _logger.LogWarning("Password reset failed for user: {UserName}. Errors: {Errors}",
                        user.UserName, string.Join(", ", result.Errors));
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resetting password for user: {UserName}", user.UserName);
                throw;
            }
        }

        public async Task<string> GeneratePasswordResetTokenAsync(User user)
        {
            try
            {
                _logger.LogInformation("Generating password reset token for user: {UserName}", user.UserName);
                return await _userManager.GeneratePasswordResetTokenAsync(user);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating password reset token for user: {UserName}", user.UserName);
                throw;
            }
        }

        public async Task<User> GetAdminByEmailAsync(string email)
        {
            try
            {
                var user = await _userManager.FindByEmailAsync(email);
                if (user == null) return null;

                var isAdmin = await _userManager.IsInRoleAsync(user, "Admin");
                return isAdmin ? user : null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting admin by email: {Email}", email);
                return null;
            }
        }

        public async Task<User> GetAdminByIdAsync(string userId)
        {
            try
            {
                var user = await _userManager.FindByIdAsync(userId);
                if (user == null) return null;

                var isAdmin = await _userManager.IsInRoleAsync(user, "Admin");
                return isAdmin ? user : null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting admin by ID: {UserId}", userId);
                return null;
            }
        }

        public async Task<bool> IsAdminExistsAsync(string email)
        {
            try
            {
                var user = await _userManager.FindByEmailAsync(email);
                if (user == null) return false;

                return await _userManager.IsInRoleAsync(user, "Admin");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking if admin exists for email: {Email}", email);
                return false;
            }
        }


        public async Task<bool> ValidatePasswordResetTokenAsync(User user, string token)
        {
            try
            {
                _logger.LogInformation("Validating password reset token for user: {UserName}", user.UserName);

                // This method validates if the token is correct for the user
                return await _userManager.VerifyUserTokenAsync(
                    user,
                    _userManager.Options.Tokens.PasswordResetTokenProvider,
                    "ResetPassword",
                    token);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating password reset token for user: {UserName}", user.UserName);
                return false;
            }
        }
    }
}
