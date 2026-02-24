using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain.Models
{
    public class CartItem
    {
        [Key]
        public int Id { get; set; }

        [ForeignKey(nameof(Product))]
        public int ProductId { get; set; }

        [ForeignKey(nameof(Cart))]
        public int CartId { get; set; }

        [Range(1, 100)]
        public int Quantity { get; set; }

        [MaxLength(50)]
        public string? SelectedColor { get; set; }

        // Price Information
        [Column(TypeName = "decimal(10,2)")]
        public decimal UnitPrice { get; set; } // Final price after product discount

        [Column(TypeName = "decimal(10,2)")]
        public decimal OriginalPrice { get; set; } // Price before any discount

        // Optional: Store discount info at cart item level
        [Column(TypeName = "decimal(5,2)")]
        public decimal? ProductDiscountPercent { get; set; }

        [NotMapped]
        public decimal LineTotal => Quantity * UnitPrice;

        [NotMapped]
        public decimal OriginalLineTotal => Quantity * OriginalPrice;

        [NotMapped]
        public decimal DiscountAmount => OriginalLineTotal - LineTotal;

        [NotMapped]
        public bool HasProductDiscount => OriginalPrice > UnitPrice;

        public virtual Cart Cart { get; set; } = default!;
        public virtual Product Product { get; set; } = default!;
    }
}