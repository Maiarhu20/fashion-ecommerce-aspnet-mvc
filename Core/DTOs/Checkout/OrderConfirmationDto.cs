// Core/DTOs/Checkout/CheckoutDto.cs
using Domain.Models;

namespace Core.DTOs.Checkout
{
    public class OrderConfirmationDto
    {
        public string OrderNumber { get; set; } = default!;
        public DateTime OrderDate { get; set; }
        public string CustomerName { get; set; } = default!;
        public string CustomerEmail { get; set; } = default!;
        public string CustomerPhone { get; set; } = default!;
        public string ShippingAddress { get; set; } = default!;
        public string ShippingCity { get; set; } = default!;
        public decimal ShippingCost { get; set; }
        public decimal CartTotal { get; set; }
        public decimal DiscountAmount { get; set; }
        public decimal GrandTotal { get; set; }
        public PaymentMethod PaymentMethod { get; set; }
        public ShippingCityDto? ShippingCityDetails { get; set; }
        public List<OrderItemDto> Items { get; set; } = new();

        // ✅ ADD THESE for credit card payments
        public string? PaymentKey { get; set; }
        public string? PaymentTransactionId { get; set; }
    }
}