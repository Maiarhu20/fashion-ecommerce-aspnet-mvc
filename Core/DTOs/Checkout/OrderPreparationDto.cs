using Core.DTOs.Cart;
using Core.Services;
using Domain.Models;

namespace Core.DTOs.Checkout
{
    public class OrderPreparationDto
    {
        public string OrderNumber { get; set; } = default!;
        public string GuestName { get; set; } = default!;
        public string GuestEmail { get; set; } = default!;
        public string GuestPhone { get; set; } = default!;
        public string ShippingAddress { get; set; } = default!;
        public int ShippingCityId { get; set; }
        public string ShippingCityName { get; set; } = default!;
        public decimal ShippingCost { get; set; }
        public PaymentMethod PaymentMethod { get; set; }
        public decimal Subtotal { get; set; }
        public decimal DiscountAmount { get; set; }
        public decimal TotalAmount { get; set; }
        public string? DiscountCode { get; set; }
        public string? PaymentKey { get; set; }
        public string? TransactionId { get; set; }
        public string? IframeId { get; set; }
        public string? WalletRedirectUrl { get; set; }
        public List<CartItemDto> CartItems { get; set; } = new();
    }

    public class OrderPreparationResult
    {
        public bool Success { get; set; }
        public string? OrderNumber { get; set; }
        public PaymentMethod PaymentMethod { get; set; }
        public string? PaymentKey { get; set; }
        public decimal TotalAmount { get; set; }
        public string? RedirectUrl { get; set; }
        public string? ErrorMessage { get; set; }

        // For wallet payments
        public string? WalletRedirectUrl { get; set; }
        public string? IframeId { get; set; }
    }

    public class OrderSessionData
    {
        public string SessionId { get; set; } = default!;
        public OrderPreparationDto OrderPreparation { get; set; } = default!;
        public CartDto CartData { get; set; } = default!;
        public PlaceOrderRequest RequestData { get; set; } = default!;
        public DateTime CreatedAt { get; set; }
    }

    public class PaymentVerificationRequest
    {
        public string TransactionId { get; set; } = default!;
        public string OrderNumber { get; set; } = default!;
    }

    public class CartItemDto
    {
        public int ProductId { get; set; }
        public string ProductName { get; set; } = default!;
        public int Quantity { get; set; }
        public string? SelectedColor { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal LineTotal { get; set; }
    }

    public class PaymentProcessingViewModel
    {
        public string OrderNumber { get; set; } = default!;
        public decimal GrandTotal { get; set; }
        public string CustomerName { get; set; } = default!;
        public string CustomerEmail { get; set; } = default!;
        public string CustomerPhone { get; set; } = default!;
        public string? PaymentKey { get; set; }
        public string? IframeId { get; set; }
        public bool IsWalletPayment { get; set; }
        public string? WalletRedirectUrl { get; set; }
    }
    // Add this class to your OrderPreparationDto.cs file

    // Add this class to your OrderPreparationDto.cs file

    public class WalletPaymentViewModel
    {
        public string OrderNumber { get; set; } = default!;
        public decimal GrandTotal { get; set; }
        public string CustomerName { get; set; } = default!;
        public string CustomerPhone { get; set; } = default!;
        public string? PaymentKey { get; set; }
    }

}