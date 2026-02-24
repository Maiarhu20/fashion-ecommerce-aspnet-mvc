using Core.DTOs.Cart;
using Domain.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Core.DTOs.Checkout
{
    public class CheckoutDto
    {
        public CartDto Cart { get; set; } = new();

        [Required(ErrorMessage = "Name is required")]
        [MaxLength(100)]
        public string GuestName { get; set; } = default!;

        [Required(ErrorMessage = "Email is required")]
        [EmailAddress(ErrorMessage = "Invalid email address")]
        public string GuestEmail { get; set; } = default!;

        [Required(ErrorMessage = "Phone number is required")]
        [Phone(ErrorMessage = "Invalid phone number")]
        public string GuestPhone { get; set; } = default!;

        [Required(ErrorMessage = "Address is required")]
        [MaxLength(200)]
        public string ShippingAddress { get; set; } = default!;

        [Required(ErrorMessage = "City is required")]
        public int ShippingCityId { get; set; }

        [MaxLength(20)]
        public string? ShippingPostalCode { get; set; }

        [Required(ErrorMessage = "Payment method is required")]
        public PaymentMethod PaymentMethod { get; set; }

        [MaxLength(500)]
        public string? Notes { get; set; }

        public decimal ShippingCost { get; set; }
        public decimal CartTotal { get; set; }
        public decimal GrandTotal { get; set; }

        public List<ShippingCityDto> AvailableCities { get; set; } = new();
    }
}