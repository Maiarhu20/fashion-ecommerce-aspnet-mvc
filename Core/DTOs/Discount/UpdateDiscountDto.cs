using System;
using System.ComponentModel.DataAnnotations;

namespace Core.DTOs.Discount
{
    public class UpdateDiscountDto
    {
        [Required]
        public int Id { get; set; }

        [Required(ErrorMessage = "Code is required")]
        [StringLength(50, ErrorMessage = "Code cannot exceed 50 characters")]
        [RegularExpression(@"^[A-Z0-9]+$", ErrorMessage = "Code must contain only uppercase letters and numbers")]
        public string Code { get; set; } = string.Empty;

        [StringLength(200, ErrorMessage = "Description cannot exceed 200 characters")]
        public string Description { get; set; } = string.Empty;

        [Required(ErrorMessage = "Discount type is required")]
        public string DiscountType { get; set; } = "Percentage";

        [Range(0.01, 10000, ErrorMessage = "Discount value must be between 0.01 and 10000")]
        public decimal DiscountValue { get; set; }

        [Range(0, 10000, ErrorMessage = "Minimum order amount must be positive")]
        public decimal? MinimumOrderAmount { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "Usage limit must be at least 1")]
        [Display(Name = "Usage Limit Per Guest")]
        public int? UsageLimitPerGuest { get; set; }
        //public int? UsageLimit { get; set; }

        [Required(ErrorMessage = "Start date is required")]
        public DateTime StartDate { get; set; } = DateTime.UtcNow;

        public DateTime? ExpiryDate { get; set; }

        public bool IsActive { get; set; } = true;
    }
}