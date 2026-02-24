using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain.Models
{
    public class Payment
    {
        public int Id { get; set; }

        [ForeignKey(nameof(Order))]
        public int OrderId { get; set; }

        // External provider references
        [MaxLength(100)]
        public string? ProviderName { get; set; }   // example: Stripe, PayPal, Fawry

        public string? ProviderTransactionId { get; set; }

        // ✅ ADD THIS: Store payment provider keys/tokens here
        [MaxLength(2000)]
        public string? ProviderPaymentKey { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        public decimal Amount { get; set; }

        public PaymentStatus Status { get; set; } // e.g., Succeeded/Failed/Pending

        [Required]
        public PaymentMethod PaymentMethod { get; set; }

        [MaxLength(10)]
        public string? Currency { get; set; } = "EGP";

        // NEW: Discount Information
        [MaxLength(50)]
        public string? AppliedDiscountCode { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        public decimal DiscountAmount { get; set; } = 0m;

        [Column(TypeName = "decimal(10,2)")]
        public decimal OriginalAmount { get; set; } // Amount before discount

        // Time tracking
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
        public DateTime? CompletedDate { get; set; }

        public virtual Order Order { get; set; } = default!;
    }

    public enum PaymentMethod
    {
        CashOnDelivery ,
        Card ,
        Wallet 
    }

    public enum PaymentStatus
    {
        Pending,
        Succeeded,
        Failed,
        Cancelled,
        Refunded
    }
}