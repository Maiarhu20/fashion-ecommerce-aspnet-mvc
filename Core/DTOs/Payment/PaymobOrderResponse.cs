using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Core.DTOs.Payment
{
    public class PaymobOrderResponse
    {
        [System.Text.Json.Serialization.JsonPropertyName("id")]
        public int? Id { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("merchant_order_id")]
        public string? MerchantOrderId { get; set; }
    }
}
