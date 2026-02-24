using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Core.DTOs.Payment
{
    public class PaymobTransactionResponse
    {
        [System.Text.Json.Serialization.JsonPropertyName("id")]
        public int? Id { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("success")]
        public bool? Success { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("pending")]
        public bool? Pending { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("amount_cents")]
        public int? AmountCents { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("currency")]
        public string? Currency { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("order")]
        public PaymobOrderInfo? Order { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("data")]
        public PaymobTransactionData? Data { get; set; }
    }

    public class PaymobOrderInfo
    {
        [System.Text.Json.Serialization.JsonPropertyName("id")]
        public int? Id { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("merchant_order_id")]
        public string? MerchantOrderId { get; set; }
    }

    public class PaymobTransactionData
    {
        [System.Text.Json.Serialization.JsonPropertyName("message")]
        public string? Message { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("txn_response_code")]
        public string? TxnResponseCode { get; set; }
    }

    public class PaymobOrderDetailsResponse
    {
        [System.Text.Json.Serialization.JsonPropertyName("id")]
        public int? Id { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("merchant_order_id")]
        public string? MerchantOrderId { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("amount_cents")]
        public int? AmountCents { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("currency")]
        public string? Currency { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("paid_amount_cents")]
        public int? PaidAmountCents { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("is_payment_locked")]
        public bool? IsPaymentLocked { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("payment_transactions")]
        public List<PaymobTransactionInfo>? PaymentTransactions { get; set; }
    }

    public class PaymobTransactionInfo
    {
        [System.Text.Json.Serialization.JsonPropertyName("id")]
        public int? Id { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("success")]
        public bool? Success { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("pending")]
        public bool? Pending { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("amount_cents")]
        public int? AmountCents { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("currency")]
        public string? Currency { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("created_at")]
        public string? CreatedAt { get; set; }
    }
}
