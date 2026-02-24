// Domain/Models/DiscountUsage.cs
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Domain.Models
{
    public class DiscountUsage
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int DiscountId { get; set; }

        [Required]
        [MaxLength(100)]
        public string SessionId { get; set; } = default!;

        [MaxLength(255)]
        public string? GuestEmail { get; set; }

        public int UsageCount { get; set; } = 0;

        public DateTime FirstUsedDate { get; set; } = DateTime.UtcNow;
        public DateTime LastUsedDate { get; set; } = DateTime.UtcNow;

        [ForeignKey("DiscountId")]
        public virtual Discount Discount { get; set; } = default!;
    }
}