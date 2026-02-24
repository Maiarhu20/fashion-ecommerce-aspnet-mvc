using Core.DTOs.Payment;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;

namespace Core.Services
{
    public class PaymentService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;
        private readonly ILogger<PaymentService> _logger;
        private readonly string _apiKey;
        private readonly string _integrationIdCard;
        private readonly string _integrationIdWallet;
        private readonly string _iframeIdCard;
        private readonly string _hmacSecret;

        public PaymentService(
            IHttpClientFactory httpClientFactory,
            IConfiguration configuration,
            ILogger<PaymentService> logger)
        {
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
            _logger = logger;

            _apiKey = _configuration["Paymob:ApiKey"]
                ?? throw new InvalidOperationException("Paymob:ApiKey not configured");
            _integrationIdCard = _configuration["Paymob:IntegrationIdCard"]
                ?? throw new InvalidOperationException("Paymob:IntegrationIdCard not configured");
            _integrationIdWallet = _configuration["Paymob:IntegrationIdWallet"]
                ?? throw new InvalidOperationException("Paymob:IntegrationIdWallet not configured");
            _iframeIdCard = _configuration["Paymob:IframeIdCard"]
                ?? throw new InvalidOperationException("Paymob:IframeIdCard not configured");
            _hmacSecret = _configuration["Paymob:HMACSecret"]
                ?? throw new InvalidOperationException("Paymob:HMACSecret not configured");
        }

        public async Task<PaymobPaymentResult> InitiatePaymobPaymentAsync(
     PaymentRequestDto request,
     PaymentMethodType methodType = PaymentMethodType.Card)
        {
            try
            {
                var client = _httpClientFactory.CreateClient("Paymob");

                var integrationId = methodType == PaymentMethodType.Wallet
                    ? _integrationIdWallet
                    : _integrationIdCard;

                // Step 1: Authentication
                var authToken = await AuthenticateWithRetryAsync(client);

                // Step 2: Create Order
                var paymobOrderId = await CreatePaymobOrderAsync(client, authToken, request);

                // Step 3: Generate Payment Key
                var paymentKey = await GeneratePaymentKeyAsync(
                    client,
                    authToken,
                    paymobOrderId,
                    request,
                    integrationId);

                // DON'T initiate wallet payment here - that's done separately when user enters phone

                return new PaymobPaymentResult
                {
                    Success = true,
                    TransactionId = paymobOrderId.ToString(),
                    PaymentKey = paymentKey,
                    PaymentMethodType = methodType,
                    IframeId = methodType == PaymentMethodType.Card ? _iframeIdCard : null,
                    WalletRedirectUrl = null // This will be set later for wallet
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initiate Paymob payment");
                return new PaymobPaymentResult
                {
                    Success = false,
                    ErrorMessage = $"Payment initialization failed: {ex.Message}"
                };
            }
        }

        private async Task<string?> InitiateWalletPaymentAsync(
            HttpClient client,
            string paymentKey,
            string phoneNumber)
        {
            try
            {
                // Clean phone number for wallet
                var cleanPhone = CleanPhoneNumber(phoneNumber);

                // Ensure it starts with +20 for Egypt
                if (!cleanPhone.StartsWith("+"))
                {
                    cleanPhone = "+2" + cleanPhone; // Egyptian format
                }

                var walletRequest = new
                {
                    source = new
                    {
                        identifier = cleanPhone,
                        subtype = "WALLET"
                    },
                    payment_token = paymentKey
                };

                _logger.LogInformation("Initiating wallet payment for phone: {Phone}", cleanPhone);

                var response = await client.PostAsJsonAsync(
                    "acceptance/payments/pay",
                    walletRequest);

                var content = await response.Content.ReadAsStringAsync();
                _logger.LogInformation("Wallet payment response: {Content}", content);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("Wallet payment initiation failed: {StatusCode} - {Content}",
                        response.StatusCode, content);
                    return null;
                }

                var walletResult = await response.Content.ReadFromJsonAsync<WalletPaymentResponse>();

                // The redirect_url is where user completes wallet payment
                return walletResult?.RedirectUrl;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initiating wallet payment");
                return null;
            }
        }

        private async Task<string> AuthenticateWithRetryAsync(HttpClient client, int maxRetries = 3)
        {
            Exception? lastException = null;

            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                try
                {
                    _logger.LogInformation("Paymob authentication attempt {Attempt}/{MaxRetries}",
                        attempt, maxRetries);

                    var response = await client.PostAsJsonAsync("auth/tokens", new
                    {
                        api_key = _apiKey
                    });

                    var content = await response.Content.ReadAsStringAsync();

                    if (!response.IsSuccessStatusCode)
                    {
                        _logger.LogWarning("Paymob auth failed on attempt {Attempt}: {StatusCode} - {Content}",
                            attempt, response.StatusCode, content);

                        if (attempt < maxRetries)
                        {
                            await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt)));
                            continue;
                        }

                        throw new HttpRequestException($"Authentication failed: {response.StatusCode}");
                    }

                    var authResult = await response.Content.ReadFromJsonAsync<PaymobAuthResponse>();

                    if (string.IsNullOrEmpty(authResult?.Token))
                        throw new InvalidOperationException("Invalid auth token received");

                    _logger.LogInformation("Paymob authentication successful");
                    return authResult.Token;
                }
                catch (Exception ex)
                {
                    lastException = ex;
                    _logger.LogError(ex, "Paymob auth attempt {Attempt} failed", attempt);

                    if (attempt < maxRetries)
                        await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt)));
                }
            }

            throw new Exception("Paymob authentication failed after all retries", lastException);
        }

        private async Task<int> CreatePaymobOrderAsync(
            HttpClient client,
            string authToken,
            PaymentRequestDto request)
        {
            var orderRequest = new
            {
                auth_token = authToken,
                delivery_needed = false,
                amount_cents = (int)(request.Amount * 100),
                currency = request.Currency,
                merchant_order_id = request.OrderNumber,
                items = new[]
                {
                    new
                    {
                        name = $"Order {request.OrderNumber}",
                        amount_cents = (int)(request.Amount * 100),
                        quantity = 1
                    }
                }
            };

            _logger.LogInformation("Creating Paymob order for {OrderNumber}, Amount: {Amount}",
                request.OrderNumber, request.Amount);

            var response = await client.PostAsJsonAsync("ecommerce/orders", orderRequest);
            var content = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Paymob order creation failed: {StatusCode} - {Content}",
                    response.StatusCode, content);
                throw new HttpRequestException($"Order creation failed: {response.StatusCode}");
            }

            var orderResult = await response.Content.ReadFromJsonAsync<PaymobOrderResponse>();

            if (orderResult?.Id == null)
                throw new InvalidOperationException("Invalid order response from Paymob");

            _logger.LogInformation("Paymob order created: {PaymobOrderId}", orderResult.Id);
            return orderResult.Id.Value;
        }

        private async Task<string> GeneratePaymentKeyAsync(
            HttpClient client,
            string authToken,
            int paymobOrderId,
            PaymentRequestDto request,
            string integrationId)
        {
            var nameParts = SplitName(request.CustomerName);
            var baseUrl = _configuration["App:BaseUrl"] ?? "https://yourdomain.com";

            var paymentKeyRequest = new
            {
                auth_token = authToken,
                amount_cents = (int)(request.Amount * 100),
                expiration = 3600,
                order_id = paymobOrderId,
                billing_data = new
                {
                    email = request.CustomerEmail,
                    phone_number = CleanPhoneNumber(request.CustomerPhone),
                    first_name = nameParts.firstName,
                    last_name = nameParts.lastName,
                    street = "NA",
                    city = "Cairo",
                    country = "EG",
                    apartment = "NA",
                    floor = "NA",
                    building = "NA",
                    shipping_method = "PKG",
                    postal_code = "NA",
                    state = "NA"
                },
                currency = request.Currency,
                integration_id = int.Parse(integrationId),
                lock_order_when_paid = true
            };

            _logger.LogInformation("Generating payment key for order {PaymobOrderId} with integration {IntegrationId}",
                paymobOrderId, integrationId);

            var response = await client.PostAsJsonAsync("acceptance/payment_keys", paymentKeyRequest);
            var content = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Payment key generation failed: {StatusCode} - {Content}",
                    response.StatusCode, content);
                throw new HttpRequestException($"Payment key generation failed: {response.StatusCode}");
            }

            var paymentKeyResult = await response.Content.ReadFromJsonAsync<PaymobPaymentKeyResponse>();

            if (string.IsNullOrEmpty(paymentKeyResult?.Token))
                throw new InvalidOperationException("Invalid payment key received");

            _logger.LogInformation("Payment key generated successfully");
            return paymentKeyResult.Token;
        }

        public async Task<PaymentVerificationResult> VerifyPaymobPaymentAsync(string idToVerify)
        {
            try
            {
                var client = _httpClientFactory.CreateClient("Paymob");
                var authToken = await AuthenticateWithRetryAsync(client);

                var response = await client.GetAsync($"acceptance/transactions/{idToVerify}");

                if (!response.IsSuccessStatusCode)
                {
                    return new PaymentVerificationResult
                    {
                        Success = false,
                        IsPending = false,
                        FailureReason = "Unable to verify wallet payment."
                    };
                }

                var transaction = await response.Content.ReadFromJsonAsync<PaymobTransactionResponse>();

                if (transaction == null)
                {
                    return new PaymentVerificationResult
                    {
                        Success = false,
                        IsPending = false,
                        FailureReason = "Invalid payment response."
                    };
                }

                var isSuccess = transaction.Success == true && transaction.Pending == false;
                var isPending = transaction.Pending == true;

                string? failureReason = null;

                // ✅ DECLINED CASE
                if (!isSuccess && !isPending)
                {
                    failureReason = GetWalletFailureMessage(transaction.Data);
                }

                return new PaymentVerificationResult
                {
                    Success = isSuccess,
                    IsPending = isPending,
                    TransactionId = idToVerify,
                    Amount = (decimal)(transaction.AmountCents ?? 0) / 100m,
                    Currency = transaction.Currency,
                    OrderNumber = transaction.Order?.MerchantOrderId,
                    FailureReason = failureReason
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error verifying payment {Id}", idToVerify);

                return new PaymentVerificationResult
                {
                    Success = false,
                    IsPending = false,
                    FailureReason = "An error occurred while checking wallet payment."
                };
            }
        }

        public PaymentCallbackResult ProcessCallback(Dictionary<string, string> callbackData)
        {
            try
            {
                // Log all received data for debugging
                _logger.LogInformation("Processing Paymob callback with {Count} fields", callbackData.Count);
                foreach (var kvp in callbackData)
                {
                    _logger.LogDebug("Callback field: {Key} = {Value}", kvp.Key, kvp.Value);
                }

                // Verify HMAC
                if (!VerifyHmacSignature(callbackData))
                {
                    _logger.LogWarning("HMAC verification failed");
                    return new PaymentCallbackResult
                    {
                        IsValid = false,
                        ErrorMessage = "Invalid signature"
                    };
                }

                var success = callbackData.GetValueOrDefault("success")?.ToLower() == "true";
                var pending = callbackData.GetValueOrDefault("pending")?.ToLower() == "true";
                var transactionId = callbackData.GetValueOrDefault("id");
                var orderId = callbackData.GetValueOrDefault("order");
                var merchantOrderId = callbackData.GetValueOrDefault("merchant_order_id");
                var amountCents = int.TryParse(callbackData.GetValueOrDefault("amount_cents"), out var amt) ? amt : 0;

                _logger.LogInformation(
                    "Callback processed: TransactionId={TransactionId}, Success={Success}, Pending={Pending}, MerchantOrderId={MerchantOrderId}",
                    transactionId, success, pending, merchantOrderId);

                return new PaymentCallbackResult
                {
                    IsValid = true,
                    Success = success && !pending,
                    IsPending = pending,
                    TransactionId = transactionId,
                    PaymobOrderId = orderId,
                    MerchantOrderId = merchantOrderId,
                    Amount = amountCents / 100m
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing callback");
                return new PaymentCallbackResult
                {
                    IsValid = false,
                    ErrorMessage = ex.Message
                };
            }
        }

        public bool VerifyHmacSignature(Dictionary<string, string> callbackData)
        {
            try
            {
                if (!callbackData.TryGetValue("hmac", out var receivedHmac))
                {
                    _logger.LogWarning("HMAC signature missing from callback");
                    return false;
                }

                // Build concatenated string according to Paymob documentation
                // Order matters! Must be alphabetical by key name
                var keysInOrder = new[]
                {
                    "amount_cents",
                    "created_at",
                    "currency",
                    "error_occured",
                    "has_parent_transaction",
                    "id",
                    "integration_id",
                    "is_3d_secure",
                    "is_auth",
                    "is_capture",
                    "is_refunded",
                    "is_standalone_payment",
                    "is_voided",
                    "order",
                    "owner",
                    "pending",
                    "source_data.pan",
                    "source_data.sub_type",
                    "source_data.type",
                    "success"
                };

                var concatenatedString = string.Join("",
                    keysInOrder.Select(key => callbackData.GetValueOrDefault(key, "")));

                using var hmac = new HMACSHA512(Encoding.UTF8.GetBytes(_hmacSecret));
                var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(concatenatedString));
                var calculatedHmac = BitConverter.ToString(hash).Replace("-", "").ToLower();

                var isValid = calculatedHmac == receivedHmac.ToLower();

                if (!isValid)
                {
                    _logger.LogWarning("HMAC mismatch. Calculated: {Calculated}, Received: {Received}",
                        calculatedHmac.Substring(0, 20) + "...",
                        receivedHmac.Substring(0, Math.Min(20, receivedHmac.Length)) + "...");
                }

                return isValid;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error verifying HMAC signature");
                return false;
            }
        }


        private string GetWalletFailureMessage(PaymobTransactionData? data)
        {
            if (data == null)
                return "Wallet payment failed. Please try again.";

            var code = data.TxnResponseCode?.ToUpperInvariant();
            var rawMessage = data.Message;

            return code switch
            {
                "NO_WALLET_FOUND" =>
                    "This phone number does not have an active mobile wallet.",

                "INSUFFICIENT_FUNDS" =>
                    "Your wallet balance is not enough to complete this payment.",

                "USER_REJECTED" =>
                    "Payment was rejected from your wallet app.",

                "TIMEOUT" =>
                    "You did not approve the payment in time.",

                "DECLINED" =>
                    "Wallet payment was declined.",

                _ => rawMessage ??
                     "Wallet payment failed. Please try another number or payment method."
            };
        }

        public async Task<WalletExecutionResult> ExecuteWalletPaymentAsync(string paymentKey, string walletPhone)
        {
            try
            {
                var client = _httpClientFactory.CreateClient("Paymob");

                // ✅ FIX 1: Get clean 11-digit phone number (NO +2)
                var formattedPhone = FormatEgyptianWalletPhone(walletPhone);

                _logger.LogInformation("Executing wallet payment for phone: {Phone}", formattedPhone);

                var walletRequest = new
                {
                    source = new
                    {
                        identifier = formattedPhone, // Sends "010xxxxxxxx"
                        subtype = "WALLET"
                    },
                    payment_token = paymentKey
                };

                var response = await client.PostAsJsonAsync("acceptance/payments/pay", walletRequest);
                var content = await response.Content.ReadAsStringAsync();

                _logger.LogInformation("Wallet payment response: {Content}", content);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("Wallet payment failed: {StatusCode} - {Content}", response.StatusCode, content);
                    return new WalletExecutionResult
                    {
                        Success = false,
                        ErrorMessage = "Payment request failed. Please check the number and try again."
                    };
                }

                var walletResult = await response.Content.ReadFromJsonAsync<WalletPaymentApiResponse>();

                if (walletResult == null)
                {
                    return new WalletExecutionResult { Success = false, ErrorMessage = "Invalid response" };
                }

                // ✅ FIX 2: Check if redirect URL exists (Test mode often returns this)
                // If redirect_url is present, we must use it.
                var redirectUrl = walletResult.RedirectUrl ?? walletResult.IframeRedirectionUrl;

                return new WalletExecutionResult
                {
                    Success = true,
                    TransactionId = walletResult.Id?.ToString(),
                    RedirectUrl = redirectUrl,
                    // If we have a redirect URL, it's not "pending" in the sense of waiting for app approval
                    // It's pending user action on the redirect page.
                    IsPending = string.IsNullOrEmpty(redirectUrl) && (walletResult.Pending ?? false)
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing wallet payment");
                return new WalletExecutionResult
                {
                    Success = false,
                    ErrorMessage = "An error occurred while processing wallet payment"
                };
            }
        }

        private string FormatEgyptianWalletPhone(string phone)
        {
            if (string.IsNullOrWhiteSpace(phone))
                throw new ArgumentException("Phone number is required");

            // Remove all non-digits
            var digits = new string(phone.Where(char.IsDigit).ToArray());

            // Remove country code variations (+20, 002, 20)
            if (digits.StartsWith("20") && digits.Length > 10)
                digits = digits.Substring(2);
            else if (digits.StartsWith("002") && digits.Length > 10)
                digits = digits.Substring(3);

            // Add leading zero if missing (e.g. 10xxxxxxx -> 010xxxxxxx)
            if (digits.Length == 10 && digits.StartsWith("1"))
                digits = "0" + digits;

            // Validate strictly 11 digits starting with 01
            if (digits.Length != 11 || !digits.StartsWith("01"))
            {
                _logger.LogWarning("Invalid wallet phone format: {Phone}", phone);
                throw new ArgumentException("Invalid phone number. Must be 11 digits starting with 01.");
            }

            // ✅ FIX 3: Return ONLY the digits. DO NOT ADD "+2"
            // Paymob Wallet API requires "01xxxxxxxxx"
            return digits;
        }



        private (string firstName, string lastName) SplitName(string fullName)
        {
            if (string.IsNullOrWhiteSpace(fullName))
                return ("Customer", "Unknown");

            var parts = fullName.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);

            return parts.Length switch
            {
                0 => ("Customer", "Unknown"),
                1 => (parts[0], "Unknown"),
                _ => (parts[0], string.Join(" ", parts.Skip(1)))
            };
        }

        private string CleanPhoneNumber(string phone)
        {
            if (string.IsNullOrWhiteSpace(phone))
                return "01000000000";

            var cleaned = new string(phone.Where(char.IsDigit).ToArray());

            if (cleaned.StartsWith("20") && cleaned.Length > 10)
                cleaned = cleaned[2..];

            if (cleaned.StartsWith("2") && cleaned.Length == 12)
                cleaned = cleaned[1..];

            if (!cleaned.StartsWith("01") || cleaned.Length != 11)
                return "01000000000";

            return cleaned;
        }
    }

    public enum PaymentMethodType
    {
        Card,
        Wallet
    }

    public class PaymentVerificationResult
    {
        public bool Success { get; set; }
        public bool IsPending { get; set; }
        public string? TransactionId { get; set; }
        public decimal Amount { get; set; }
        public string? Currency { get; set; }
        public string? OrderNumber { get; set; }

        // ✅ UX message for customer
        public string? FailureReason { get; set; }
    }

    public class PaymentCallbackResult
    {
        public bool IsValid { get; set; }
        public bool Success { get; set; }
        public bool IsPending { get; set; }
        public string? TransactionId { get; set; }
        public string? PaymobOrderId { get; set; }
        public string? MerchantOrderId { get; set; }
        public decimal Amount { get; set; }
        public string? ErrorMessage { get; set; }
    }

    public class WalletPaymentResponse
    {
        public int? Id { get; set; }
        public bool? Pending { get; set; }
        public string? RedirectUrl { get; set; }
        public string? IframeRedirectionUrl { get; set; }
    }

    public class WalletExecutionResult
    {
        public bool Success { get; set; }
        public string? TransactionId { get; set; }
        public string? RedirectUrl { get; set; }
        public bool IsPending { get; set; }
        public string? ErrorMessage { get; set; }
    }

    public class WalletPaymentApiResponse
    {
        [System.Text.Json.Serialization.JsonPropertyName("id")]
        public int? Id { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("pending")]
        public bool? Pending { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("redirect_url")]
        public string? RedirectUrl { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("iframe_redirection_url")]
        public string? IframeRedirectionUrl { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("message")]
        public string? Message { get; set; }
    }


}