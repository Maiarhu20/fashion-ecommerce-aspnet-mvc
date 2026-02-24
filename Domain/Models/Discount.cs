// Domain/Models/Discount.cs
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Domain.Models
{
    public class Discount
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(50)]
        public string Code { get; set; } = default!;

        [MaxLength(200)]
        public string Description { get; set; } = string.Empty;

        public DiscountType DiscountType { get; set; } = DiscountType.Percentage;

        [Column(TypeName = "decimal(10,2)")]
        [Range(0.01, 10000)]
        public decimal DiscountValue { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        public decimal? MinimumOrderAmount { get; set; }

        // Per-guest usage limit (e.g., 1 = each guest can use once)
        [Display(Name = "Usage Limit Per Guest")]
        public int? UsageLimitPerGuest { get; set; }

        // Keep track of total usage for stats (optional, just for reporting)
        public int TotalUsageCount { get; set; } = 0;

        public DateTime StartDate { get; set; } = DateTime.UtcNow;
        public DateTime? ExpiryDate { get; set; }

        public bool IsActive { get; set; } = true;

        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
        public DateTime? LastModified { get; set; }

        public virtual ICollection<Order> Orders { get; set; } = new List<Order>();
        public virtual ICollection<DiscountUsage> Usages { get; set; } = new List<DiscountUsage>();
    }

    public enum DiscountType
    {
        Percentage,
        FixedAmount
    }
}