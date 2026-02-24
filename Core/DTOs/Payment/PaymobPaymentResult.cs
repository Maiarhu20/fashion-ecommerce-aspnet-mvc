using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Core.Services;

namespace Core.DTOs.Payment
{
    public class PaymobPaymentResult
    {
        public bool Success { get; set; }
        public string? TransactionId { get; set; }
        public string? PaymentKey { get; set; }
        public string? ErrorMessage { get; set; }
        public PaymentMethodType PaymentMethodType { get; set; }
        // For card payments - iframe ID
        public string? IframeId { get; set; }

        // For wallet payments - redirect URL
        public string? WalletRedirectUrl { get; set; }
    }

}
