// File: Core.DTOs.Discount.CreateDiscountDto.cs
using System;
using System.ComponentModel.DataAnnotations;

namespace Core.DTOs.Discount
{
    public class CreateDiscountDto
    {
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
        [Display(Name = "Usage Limit Per Guest")]//public int? UsageLimit { get; set; }
        public int? UsageLimitPerGuest { get; set; }

        // ⭐ FORCE WHOLE MINUTES (NO SECONDS)
        [Required(ErrorMessage = "Start date is required")]
        [DataType(DataType.DateTime)]
        [DisplayFormat(
            DataFormatString = "{0:yyyy-MM-ddTHH:mm}",
            ApplyFormatInEditMode = true // ⭐ This removes seconds from the editor
        )]
        public DateTime StartDate { get; set; } = DateTime.UtcNow.Date.AddHours(DateTime.UtcNow.Hour).AddMinutes(DateTime.UtcNow.Minute);

        [DataType(DataType.DateTime)]
        [DisplayFormat(
            DataFormatString = "{0:yyyy-MM-ddTHH:mm}",
            ApplyFormatInEditMode = true
        )]
        public DateTime? ExpiryDate { get; set; }

        public bool IsActive { get; set; } = true;
    }
}