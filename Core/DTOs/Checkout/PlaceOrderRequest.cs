// Core/DTOs/Checkout/CheckoutDto.cs
using Domain.Models;
using System.ComponentModel.DataAnnotations;

namespace Core.DTOs.Checkout
{
    public class PlaceOrderRequest
    {
        [Required(ErrorMessage = "Full name is required")]
        public string GuestName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Email is required")]
        [EmailAddress(ErrorMessage = "Invalid email address")]
        public string GuestEmail { get; set; } = string.Empty;

        [Required(ErrorMessage = "Phone number is required")]
        public string GuestPhone { get; set; } = string.Empty;

        [Required(ErrorMessage = "Shipping address is required")]
        public string ShippingAddress { get; set; } = string.Empty;

        [Required(ErrorMessage = "Please select a city")]
        public int ShippingCityId { get; set; }

        // Make these optional
        public string? ShippingPostalCode { get; set; }

        [Required(ErrorMessage = "Please select a payment method")]
        public PaymentMethod PaymentMethod { get; set; }

        public decimal ShippingCost { get; set; }

        // Make this optional
        public string? Notes { get; set; }
    }
}