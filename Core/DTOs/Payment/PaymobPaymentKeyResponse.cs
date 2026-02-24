using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Core.DTOs.Payment
{
    public class PaymobPaymentKeyResponse
    {
        [System.Text.Json.Serialization.JsonPropertyName("token")]
        public string? Token { get; set; }
    }

}
