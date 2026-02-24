using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Core.DTOs.Admin
{
    public class AdminLoginDto   // api or view model result
    {
        /// <summary>Username or email address</summary>
        [Required(ErrorMessage = "Username or Email is required")]
        [StringLength(100)]
        [Display(Name = "Username or Email")]
        public string Username { get; set; }

        /// <summary>Account password</summary>
        [Required(ErrorMessage = "Password is required")]
        [StringLength(100, MinimumLength = 6)]
        [DataType(DataType.Password)]
        [Display(Name = "Password")]
        public string Password { get; set; }

        /// <summary>Remember login for 7 days</summary>
        [Display(Name = "Remember Me")]
        public bool RememberMe { get; set; }

        /// <summary>URL to redirect after successful login</summary>
        public string? ReturnUrl { get; set; }
    }

}
