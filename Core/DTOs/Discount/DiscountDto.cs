using System;
using System.ComponentModel.DataAnnotations;

namespace Core.DTOs.Discount
{
    public class DiscountDto
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Code is required")]
        [StringLength(50, ErrorMessage = "Code cannot exceed 50 characters")]
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
        public int? UsageLimitPerGuest { get; set; }
        //public int? UsageLimit { get; set; }

        public int TotalUsageCount { get; set; }

        [Required(ErrorMessage = "Start date is required")]
        public DateTime StartDate { get; set; } = DateTime.UtcNow;

        public DateTime? ExpiryDate { get; set; }

        public bool IsActive { get; set; } = true;

        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
        public DateTime? LastModified { get; set; }

        // Computed properties
        public bool IsExpired => ExpiryDate.HasValue && ExpiryDate < DateTime.UtcNow;
        public bool IsCurrentlyActive => IsActive && StartDate <= DateTime.UtcNow && !IsExpired;
        public bool HasUsageLimit => UsageLimitPerGuest.HasValue;
        public bool IsUsageLimitReached => HasUsageLimit && TotalUsageCount >= UsageLimitPerGuest.Value;
    }
}