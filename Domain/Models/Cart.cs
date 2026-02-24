using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain.Models
{
    public class Cart
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(200)]
        public string SessionId { get; set; } = default!; // Guest session key

        [Column(TypeName = "decimal(10,2)")]
        public decimal DiscountAmount { get; set; } = 0m;

        // NEW: Discount Code
        [MaxLength(50)]
        public string? DiscountCode { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        public decimal TotalAmount { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        public decimal Subtotal { get; set; } // After product discounts, before cart discount

        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

        public DateTime? LastModified { get; set; }

        public virtual ICollection<CartItem> Items { get; set; } = new List<CartItem>();

        // NOTE: No virtual Discount property - just storing the code string
        // The cart is temporary, so we don't need a foreign key relationship
    }
}