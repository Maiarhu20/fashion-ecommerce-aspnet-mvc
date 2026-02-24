using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Core.DTOs.Cart
{
    public class CartItemDto
    {
        public int Id { get; set; }
        public int ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string? ProductImageUrl { get; set; }
        public int Quantity { get; set; }
        public string? SelectedColor { get; set; }
        public string? SelectedColorHex { get; set; }

        // Price Information
        public decimal UnitPrice { get; set; }        // Final price after product discount
        public decimal OriginalPrice { get; set; }    // Price before any discount
        public decimal LineTotal { get; set; }        // Quantity × UnitPrice

        // Discount Information
        public decimal? DiscountPercent { get; set; } // Product discount percentage
        public decimal DiscountAmount { get; set; }   // Total savings per line (OriginalLineTotal - LineTotal)
        public decimal FinalPrice { get; set; }       // Same as UnitPrice (for backward compatibility)

        // Stock Information
        public int MaxStock { get; set; }
        public bool IsAvailable { get; set; }

        // Computed Properties (Not mapped to database)
        public decimal OriginalLineTotal => Quantity * OriginalPrice;
        public bool HasProductDiscount => OriginalPrice > UnitPrice;
        public string DiscountText => HasProductDiscount && DiscountPercent.HasValue
            ? $"-{DiscountPercent.Value}%"
            : "";

        // Calculated Properties for Display
        public decimal PerItemDiscount => OriginalPrice - UnitPrice;
        public decimal TotalDiscount => OriginalLineTotal - LineTotal;
    }
}