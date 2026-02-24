using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Core.DTOs.Cart
{
    public class CartDto
    {
        public int Id { get; set; }
        public string SessionId { get; set; } = string.Empty;

        // Cart-level Discount (Coupon Code)
        public decimal DiscountAmount { get; set; }
        public string DiscountCode { get; set; } = string.Empty;

        // Totals
        public decimal TotalAmount { get; set; }        // Final total after all discounts
        public decimal Subtotal { get; set; }           // After product discounts, before cart discount
        public decimal TotalOriginalPrice { get; set; } // Before any discounts
        public decimal TotalProductDiscount { get; set; } // Total discount from products

        public int TotalItems { get; set; }
        public DateTime CreatedDate { get; set; }
        public List<CartItemDto> Items { get; set; } = new();

        // Computed Properties - FIXED
        public bool HasDiscount => DiscountAmount > 0 || TotalProductDiscount > 0;
        public bool HasProductDiscount => TotalProductDiscount > 0;
        public bool HasCouponDiscount => !string.IsNullOrEmpty(DiscountCode) && DiscountAmount > 0;

        // FIXED: This calculates total percentage off from original price
        public decimal DiscountPercentage => TotalOriginalPrice > 0
            ? Math.Round(((TotalOriginalPrice - TotalAmount) / TotalOriginalPrice) * 100, 2)
            : 0;

        // FIXED: This is total savings from both product discounts AND cart discounts
        public decimal TotalSavings => TotalProductDiscount + DiscountAmount;

        // Add this for clarity - shows just the cart-level discount percentage
        public decimal CouponDiscountPercentage => Subtotal > 0 && DiscountAmount > 0
            ? Math.Round((DiscountAmount / Subtotal) * 100, 2)
            : 0;

        // Order Summary Properties
        public decimal ItemsCount => Items.Sum(i => i.Quantity);
        public decimal AverageItemPrice => ItemsCount > 0 ? Subtotal / ItemsCount : 0;
    }
}