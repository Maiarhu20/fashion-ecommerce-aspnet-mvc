using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain.Models
{
    public class Order
    {
        [Key]
        public int Id { get; set; }

        [Required, MaxLength(50)]
        public string OrderNumber { get; set; } = default!;

        [EmailAddress]
        public string GuestEmail { get; set; }

        [Required, Phone]
        public string GuestPhone { get; set; }

        [Required, MaxLength(100)]
        public string GuestName { get; set; }

        public OrderStatus Status { get; set; } = OrderStatus.Pending;

        [Required]
        public PaymentMethod PaymentMethod { get; set; }

        public DateTime OrderDate { get; set; } = DateTime.UtcNow;
        public DateTime? ShippedDate { get; set; }
        public DateTime? DeliveredDate { get; set; }

        // Shipping Address
        [Required, MaxLength(200)]
        public string ShippingAddress { get; set; } = default!;

        // Link to ShippingCity (Foreign Key)
        [ForeignKey(nameof(ShippingCity))]
        public int? ShippingCityId { get; set; }

        [Required, MaxLength(100)]
        public string ShippingCityName { get; set; } = default!; // Snapshot of city name

        [MaxLength(20)]
        public string? ShippingPostalCode { get; set; } = default!;

        [Required, MaxLength(100)]
        public string ShippingCountry { get; set; } = default!;

        // Financials
        [Column(TypeName = "decimal(10,2)")]
        public decimal TotalAmount { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        public decimal Subtotal { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        public decimal OriginalTotal { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        public decimal ShippingCost { get; set; } = 0m;

        [Column(TypeName = "decimal(10,2)")]
        public decimal DiscountAmount { get; set; } = 0m;

        [MaxLength(50)]
        public string? DiscountCode { get; set; }

        [ForeignKey(nameof(AppliedDiscount))]
        public int? AppliedDiscountId { get; set; }
        public virtual Discount? AppliedDiscount { get; set; }

        [MaxLength(500)]
        public string? Notes { get; set; }

        // Navigation
        public virtual ShippingCity? ShippingCity { get; set; } = default!;
        public virtual Payment Payment { get; set; }
        public virtual ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
    }

    public enum OrderStatus
    {
        Pending,
        Processing,
        Shipped,
        Delivered,
        Cancelled,
        Refunded
    }
}