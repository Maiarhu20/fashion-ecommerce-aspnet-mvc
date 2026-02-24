using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Core.DTOs.Admin
{
    // <summary>
    /// ViewModel for changing admin password
    /// Used when logged-in admin wants to change their password
    /// </summary>
    public class ChangePasswordDto
    {
        /// <summary>Current password (for verification)</summary>
        [Required(ErrorMessage = "Current password is required")]
        [DataType(DataType.Password)]
        [Display(Name = "Current Password")]
        public string CurrentPassword { get; set; }

        /// <summary>New password</summary>
        [Required(ErrorMessage = "New password is required")]
        [StringLength(100, MinimumLength = 8,
            ErrorMessage = "Password must be at least 8 characters")]
        [DataType(DataType.Password)]
        [RegularExpression(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d).+$",
            ErrorMessage = "Password must contain uppercase, lowercase, and numbers")]
        [Display(Name = "New Password")]
        public string NewPassword { get; set; }

        /// <summary>Confirm new password</summary>
        [Required(ErrorMessage = "Confirm password is required")]
        [DataType(DataType.Password)]
        [Compare(nameof(NewPassword), ErrorMessage = "Passwords do not match")]
        [Display(Name = "Confirm New Password")]
        public string ConfirmNewPassword { get; set; }
    }
}
