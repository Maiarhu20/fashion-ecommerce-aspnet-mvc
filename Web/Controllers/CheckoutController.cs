using Core.DTOs.Checkout;
using Core.Services;
using Domain.Models;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Mvc;

namespace Web.Controllers
{
    [Route("checkout")]
    public class CheckoutController : Controller
    {
        private readonly CartService _cartService;
        private readonly OrderService _orderService;
        private readonly PaymentService _paymentService;
        private readonly IConfiguration _configuration;
        private readonly ILogger<CheckoutController> _logger;
        private readonly DistributedCacheService _cacheService;
        private const string SESSION_COOKIE_NAME = "CartSessionId";

        public CheckoutController(
            CartService cartService,
            OrderService orderService,
            PaymentService paymentService,
            IConfiguration configuration,
            DistributedCacheService cacheService,
            ILogger<CheckoutController> logger)
        {
            _cartService = cartService;
            _orderService = orderService;
            _paymentService = paymentService;
            _configuration = configuration;
            _cacheService = cacheService;
            _logger = logger;
        }

        [HttpGet("")]
        public async Task<IActionResult> Index()
        {
            try
            {
                var sessionId = GetOrCreateSessionId();
                var isValid = await _cartService.ValidateCartAsync(sessionId);

                if (!isValid)
                {
                    TempData["Error"] = "Some items in your cart are no longer available.";
                    return RedirectToAction("Index", "Cart");
                }

                var checkoutData = await _orderService.PrepareCheckoutAsync(sessionId);
                return View(checkoutData);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading checkout page");
                TempData["Error"] = "Unable to load checkout. Please try again.";
                return RedirectToAction("Index", "Cart");
            }
        }

        [HttpPost("calculate-shipping")]
        public async Task<IActionResult> CalculateShipping([FromBody] ShippingCalculationRequest request)
        {
            if (request?.CityId <= 0)
                return Json(new { success = false, message = "Invalid city selection" });

            try
            {
                var shippingCost = await _orderService.CalculateShippingCostAsync(request.CityId);
                var sessionId = GetOrCreateSessionId();
                var cart = await _cartService.GetOrCreateCartAsync(sessionId);

                return Json(new
                {
                    success = true,
                    shippingCost,
                    cartTotal = cart.TotalAmount,
                    grandTotal = cart.TotalAmount + shippingCost
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating shipping cost");
                return Json(new { success = false, message = "Unable to calculate shipping cost" });
            }
        }

        [HttpPost("place-order")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> PlaceOrder(PlaceOrderRequest request)
        {
            try
            {
                var sessionId = GetOrCreateSessionId();

                ModelState.Remove("ShippingPostalCode");
                ModelState.Remove("Notes");

                if (!ModelState.IsValid)
                {
                    TempData["Error"] = "Please fill in all required fields correctly.";
                    return RedirectToAction(nameof(Index));
                }

                // COD: Create order immediately
                if (request.PaymentMethod == PaymentMethod.CashOnDelivery)
                {
                    var confirmation = await _orderService.PlaceOrderAsync(sessionId, request);
                    return RedirectToAction(nameof(Confirmation), new { orderNumber = confirmation.OrderNumber });
                }

                // Card or Wallet: Prepare order first
                var result = await _orderService.PrepareOrderForPaymentAsync(sessionId, request);

                if (!result.Success)
                {
                    TempData["Error"] = result.ErrorMessage;
                    return RedirectToAction(nameof(Index));
                }

                // WALLET: Go to wallet payment page (user needs to enter wallet phone)
                if (request.PaymentMethod == PaymentMethod.Wallet)
                {
                    return RedirectToAction(nameof(WalletPayment), new { orderNumber = result.OrderNumber });
                }

                // CARD: Go to card payment page (iframe)
                return RedirectToAction(nameof(PaymentProcessing), new { orderNumber = result.OrderNumber });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error placing order");
                TempData["Error"] = "An error occurred while placing your order.";
                return RedirectToAction(nameof(Index));
            }
        }

        // ============================================
        // CARD PAYMENT PAGE (Shows Paymob Iframe)
        // ============================================
        [HttpGet("payment-processing/{orderNumber}")]
        public async Task<IActionResult> PaymentProcessing(string orderNumber)
        {
            try
            {
                var sessionId = GetOrCreateSessionId();
                var orderData = await _cacheService.GetAsync<OrderSessionData>($"order_{sessionId}");

                if (orderData == null || orderData.OrderPreparation.OrderNumber != orderNumber)
                {
                    TempData["Error"] = "Order session expired. Please start over.";
                    return RedirectToAction(nameof(Index));
                }

                var tempOrder = orderData.OrderPreparation;

                // If this is a wallet order, redirect to wallet page
                if (tempOrder.PaymentMethod == PaymentMethod.Wallet)
                {
                    return RedirectToAction(nameof(WalletPayment), new { orderNumber });
                }

                var viewModel = new PaymentProcessingViewModel
                {
                    OrderNumber = tempOrder.OrderNumber,
                    GrandTotal = tempOrder.TotalAmount,
                    CustomerName = tempOrder.GuestName,
                    CustomerEmail = tempOrder.GuestEmail,
                    CustomerPhone = tempOrder.GuestPhone,
                    PaymentKey = tempOrder.PaymentKey,
                    IframeId = tempOrder.IframeId ?? _configuration["Paymob:IframeIdCard"],
                    IsWalletPayment = false
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading payment processing");
                TempData["Error"] = "Unable to process payment.";
                return RedirectToAction(nameof(Index));
            }
        }

        // ============================================
        // WALLET PAYMENT PAGE (User enters wallet phone)
        // ============================================
        [HttpGet("wallet-payment/{orderNumber}")]
        public async Task<IActionResult> WalletPayment(string orderNumber)
        {
            try
            {
                var sessionId = GetOrCreateSessionId();
                var orderData = await _cacheService.GetAsync<OrderSessionData>($"order_{sessionId}");

                if (orderData == null || orderData.OrderPreparation.OrderNumber != orderNumber)
                {
                    TempData["Error"] = "Order session expired. Please start over.";
                    return RedirectToAction(nameof(Index));
                }

                var tempOrder = orderData.OrderPreparation;

                var viewModel = new WalletPaymentViewModel
                {
                    OrderNumber = tempOrder.OrderNumber,
                    GrandTotal = tempOrder.TotalAmount,
                    CustomerName = tempOrder.GuestName,
                    CustomerPhone = tempOrder.GuestPhone, // Pre-fill with checkout phone
                    PaymentKey = tempOrder.PaymentKey
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading wallet payment page");
                TempData["Error"] = "Unable to load wallet payment.";
                return RedirectToAction(nameof(Index));
            }
        }

        // ============================================
        // EXECUTE WALLET PAYMENT (Called via AJAX)
        // ============================================
        [HttpPost("execute-wallet-payment")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ExecuteWalletPayment([FromBody] ExecuteWalletRequest request)
        {
            try
            {
                _logger.LogInformation("Executing wallet payment for order {OrderNumber}", request.OrderNumber);

                var sessionId = GetOrCreateSessionId();
                var orderData = await _cacheService.GetAsync<OrderSessionData>($"order_{sessionId}");

                if (orderData == null || orderData.OrderPreparation.OrderNumber != request.OrderNumber)
                {
                    _logger.LogWarning("Order data not found for wallet payment: {OrderNumber}", request.OrderNumber);
                    return Json(new { success = false, message = "Order session not found. Please restart checkout." });
                }

                var paymentKey = orderData.OrderPreparation.PaymentKey;
                if (string.IsNullOrEmpty(paymentKey))
                {
                    _logger.LogError("No payment key for wallet payment: {OrderNumber}", request.OrderNumber);
                    return Json(new { success = false, message = "Payment not initialized. Please try again." });
                }

                // ✅ Execute wallet payment
                var walletResult = await _paymentService.ExecuteWalletPaymentAsync(paymentKey, request.WalletPhone);

                if (!walletResult.Success)
                {
                    _logger.LogWarning("Wallet payment failed: {Message}", walletResult.ErrorMessage);
                    return Json(new { success = false, message = walletResult.ErrorMessage ?? "Wallet payment failed" });
                }

                // Store transaction ID
                orderData.OrderPreparation.TransactionId = walletResult.TransactionId;
                await _cacheService.SetAsync($"order_{sessionId}", orderData, TimeSpan.FromMinutes(30));

                _logger.LogInformation("Wallet payment executed. TxnId: {TxnId}, HasRedirect: {HasRedirect}",
                    walletResult.TransactionId, !string.IsNullOrEmpty(walletResult.RedirectUrl));

                // If redirect URL exists, send it
                if (!string.IsNullOrEmpty(walletResult.RedirectUrl))
                {
                    return Json(new
                    {
                        success = true,
                        redirectUrl = walletResult.RedirectUrl,
                        transactionId = walletResult.TransactionId
                    });
                }

                // No redirect - payment will be verified via callback or polling
                return Json(new
                {
                    success = true,
                    message = "Payment request sent to your wallet. Please approve it in your wallet app.",
                    transactionId = walletResult.TransactionId,
                    waitForApproval = true
                });
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogError(ex, "Validation error in wallet payment");
                return Json(new { success = false, message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing wallet payment");
                return Json(new { success = false, message = "An error occurred. Please try again." });
            }
        }

        // ============================================
        // CHECK PAYMENT STATUS (Polling endpoint)
        // ============================================
        [HttpPost("check-payment-status")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CheckPaymentStatus([FromBody] CheckPaymentRequest request)
        {
            try
            {
                var sessionId = GetOrCreateSessionId();
                var orderData = await _cacheService.GetAsync<OrderSessionData>($"order_{sessionId}");

                if (orderData == null)
                {
                    return Json(new { success = false, message = "Session expired" });
                }

                var transactionId = request.TransactionId ?? orderData.OrderPreparation.TransactionId;

                if (string.IsNullOrEmpty(transactionId))
                {
                    return Json(new { success = false, message = "No transaction to check" });
                }

                var verification = await _paymentService.VerifyPaymobPaymentAsync(transactionId);

                if (verification.IsPending)
                {
                    return Json(new { success = false, pending = true, message = "Payment still processing..." });
                }

                if (verification.Success)
                {
                    // Complete the order
                    var confirmation = await _orderService.CompleteOrderAsync(
                        sessionId,
                        orderData.OrderPreparation.OrderNumber,
                        paymentSuccess: true);

                    return Json(new
                    {
                        success = true,
                        redirectUrl = Url.Action(nameof(Confirmation), new { orderNumber = confirmation.OrderNumber })
                    });
                }

                return Json(new
                {
                    success = false,
                    pending = false,
                    message = verification.FailureReason?? "Wallet payment failed. Please try another payment method."});
                }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking payment status");
                return Json(new { success = false, message = "Error checking payment" });
            }
        }


        [HttpGet("payment-response")]
        public async Task<IActionResult> PaymentResponse()
        {
            var allParams = Request.Query.ToDictionary(
                x => x.Key,
                x => x.Value.ToString());

            _logger.LogInformation("=== PAYMENT RESPONSE ===");
            _logger.LogInformation("Full URL: {Url}", Request.GetDisplayUrl());
            foreach (var p in allParams)
            {
                _logger.LogInformation("  Param: {Key} = {Value}", p.Key, p.Value);
            }

            var queryLower = allParams.ToDictionary(x => x.Key.ToLower(), x => x.Value);

            var successStr = queryLower.GetValueOrDefault("success", "false");
            var success = successStr.Equals("true", StringComparison.OrdinalIgnoreCase);

            var pendingStr = queryLower.GetValueOrDefault("pending", "false");
            var pending = pendingStr.Equals("true", StringComparison.OrdinalIgnoreCase);

            var transactionId = queryLower.GetValueOrDefault("id");
            var merchantOrderId = queryLower.GetValueOrDefault("merchant_order_id");
            var paymobOrderId = queryLower.GetValueOrDefault("order");
            var txnResponseCode = queryLower.GetValueOrDefault("txn_response_code");

            _logger.LogInformation(
                "Parsed: Success={Success}, Pending={Pending}, TxnId={TxnId}, PaymobOrderId={PaymobOrderId}, MerchantOrderId={MerchantOrderId}, ResponseCode={Code}",
                success, pending, transactionId, paymobOrderId, merchantOrderId, txnResponseCode);

            try
            {
                if (pending)
                {
                    return View("PaymentPending", new PaymentPendingViewModel
                    {
                        OrderNumber = merchantOrderId,
                        TransactionId = transactionId
                    });
                }

                if (!success)
                {
                    var errorMsg = GetUserFriendlyError(txnResponseCode, null);
                    _logger.LogWarning("Payment failed with response code: {Code}", txnResponseCode);
                    TempData["Error"] = errorMsg;
                    return RedirectToAction(nameof(Index));
                }

                // SUCCESS PATH - Payment was successful
                _logger.LogInformation("Payment reported as successful, completing order...");

                var orderNumber = merchantOrderId;

                if (string.IsNullOrEmpty(orderNumber))
                {
                    _logger.LogError("No merchant_order_id in payment response");
                    TempData["Error"] = "Payment successful but order reference missing. Please contact support.";
                    return RedirectToAction(nameof(Index));
                }

                // Get the original session ID from cached order data
                var orderData = await _cacheService.GetAsync<OrderSessionData>($"order_by_number_{orderNumber}");

                if (orderData == null)
                {
                    _logger.LogWarning("Order data not found in cache for {OrderNumber}, trying current session", orderNumber);
                }

                // Use the original session ID if available, otherwise use current session
                var sessionIdToUse = orderData?.SessionId ?? GetOrCreateSessionId();

                try
                {
                    var confirmation = await _orderService.CompleteOrderAsync(
                        sessionIdToUse,
                        orderNumber,
                        paymentSuccess: true);

                    _logger.LogInformation("Order completed successfully: {OrderNumber}", confirmation.OrderNumber);
                    return RedirectToAction(nameof(Confirmation), new { orderNumber = confirmation.OrderNumber });
                }
                catch (InvalidOperationException ex) when (ex.Message.Contains("not found") || ex.Message.Contains("expired"))
                {
                    _logger.LogWarning(ex, "Order data not found or expired for {OrderNumber}", orderNumber);

                    // Check if order was already created (possibly via callback)
                    try
                    {
                        var existingConfirmation = await _orderService.GetOrderConfirmationAsync(orderNumber);
                        _logger.LogInformation("Order {OrderNumber} was already completed, redirecting to confirmation", orderNumber);
                        return RedirectToAction(nameof(Confirmation), new { orderNumber });
                    }
                    catch
                    {
                        // Order doesn't exist
                        TempData["Error"] = "Your payment was successful but we couldn't create your order. Please contact support with your order number: " + orderNumber;
                        return RedirectToAction(nameof(Index));
                    }
                }
                catch (InvalidOperationException ex) when (ex.Message.Contains("already exists"))
                {
                    // Order already created, just redirect to confirmation
                    _logger.LogInformation("Order {OrderNumber} already exists, redirecting to confirmation", orderNumber);
                    return RedirectToAction(nameof(Confirmation), new { orderNumber });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in PaymentResponse: {Message}", ex.Message);
                TempData["Error"] = "An error occurred processing your payment. If you were charged, please contact support.";
                return RedirectToAction(nameof(Index));
            }
        }

        private string GetUserFriendlyError(string? responseCode, string? message)
        {
            if (string.IsNullOrEmpty(responseCode))
                return message ?? "Payment was declined. Please try again.";

            return responseCode.ToUpper() switch
            {
                "DECLINED" => "Your card was declined by your bank. Please try a different card.",
                "INSUFFICIENT_FUNDS" => "Insufficient funds. Please try a different card.",
                "EXPIRED_CARD" => "Your card has expired.",
                "INVALID_CARD" => "Invalid card details.",
                "DO_NOT_HONOR" => "Your bank declined this transaction. Please contact your bank.",
                "AUTHENTICATION_FAILED" => "3D Secure verification failed.",
                _ => $"Payment declined ({responseCode}). Please try a different card."
            };
        }


        [HttpGet("complete-order/{orderNumber}")]
        public async Task<IActionResult> CompleteOrderCod(string orderNumber)
        {
            try
            {
                var sessionId = GetOrCreateSessionId();
                var confirmation = await _orderService.CompleteOrderAsync(sessionId, orderNumber, paymentSuccess: false);
                return RedirectToAction(nameof(Confirmation), new { orderNumber = confirmation.OrderNumber });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error completing COD order");
                TempData["Error"] = "Error completing order.";
                return RedirectToAction(nameof(Index));
            }
        }

        
        [HttpGet("confirmation/{orderNumber}")]
        public async Task<IActionResult> Confirmation(string orderNumber)
        {
            try
            {
                var confirmation = await _orderService.GetOrderConfirmationAsync(orderNumber);
                return View(confirmation);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading order confirmation");
                TempData["Error"] = "Order not found.";
                return RedirectToAction("Index", "Home");
            }
        }

        [HttpPost("paymob-callback")]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> PaymobCallback()
        {
            try
            {
                var callbackData = Request.Form.ToDictionary(x => x.Key, x => x.Value.ToString());

                _logger.LogInformation("=== PAYMOB CALLBACK ===");
                foreach (var kvp in callbackData)
                {
                    _logger.LogInformation("  {Key} = {Value}", kvp.Key, kvp.Value);
                }

                var result = _paymentService.ProcessCallback(callbackData);

                _logger.LogInformation("Callback processed: IsValid={IsValid}, Success={Success}, MerchantOrderId={OrderId}",
                    result.IsValid, result.Success, result.MerchantOrderId);

                if (result.IsValid && result.Success && !string.IsNullOrEmpty(result.MerchantOrderId))
                {
                    var orderData = await _cacheService.GetAsync<OrderSessionData>($"order_by_number_{result.MerchantOrderId}");

                    if (orderData != null)
                    {
                        try
                        {
                            // Update the transaction ID in the cached data
                            if (!string.IsNullOrEmpty(result.TransactionId))
                            {
                                orderData.OrderPreparation.TransactionId = result.TransactionId;
                                await _cacheService.SetAsync($"order_by_number_{result.MerchantOrderId}", orderData, TimeSpan.FromMinutes(30));
                                await _cacheService.SetAsync($"order_{orderData.SessionId}", orderData, TimeSpan.FromMinutes(30));
                            }

                            await _orderService.CompleteOrderAsync(
                                orderData.SessionId,
                                result.MerchantOrderId,
                                paymentSuccess: true);

                            _logger.LogInformation("Order {OrderNumber} completed via callback", result.MerchantOrderId);
                        }
                        catch (InvalidOperationException ex) when (ex.Message.Contains("already exists"))
                        {
                            _logger.LogInformation("Order {OrderNumber} already completed", result.MerchantOrderId);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error completing order {OrderNumber} via callback", result.MerchantOrderId);
                        }
                    }
                    else
                    {
                        _logger.LogWarning("Order data not found in cache for callback: {OrderNumber}", result.MerchantOrderId);
                    }
                }

                return Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing Paymob callback");
                return Ok(); // Always return OK to Paymob
            }
        }

        // Keep the old VerifyPayment for backwards compatibility
        [HttpPost("verify-payment")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> VerifyPayment([FromBody] VerifyPaymentRequest request)
        {
            return await CheckPaymentStatus(new CheckPaymentRequest { OrderNumber = request.OrderNumber });
        }

        private string GetOrCreateSessionId()
        {
            var sessionId = Request.Cookies[SESSION_COOKIE_NAME];
            if (string.IsNullOrEmpty(sessionId))
            {
                sessionId = HttpContext.Session.GetString("CartSessionId");
                if (string.IsNullOrEmpty(sessionId))
                {
                    sessionId = Guid.NewGuid().ToString();
                    SetSessionId(sessionId);
                }
            }
            return sessionId;
        }

        private void SetSessionId(string sessionId)
        {
            Response.Cookies.Append(SESSION_COOKIE_NAME, sessionId, new CookieOptions
            {
                HttpOnly = true,
                Secure = Request.IsHttps,
                SameSite = SameSiteMode.Strict,
                Expires = DateTime.UtcNow.AddDays(7)
            });
            HttpContext.Session.SetString("CartSessionId", sessionId);
        }
    }

    // Request Models
    public class ShippingCalculationRequest { public int CityId { get; set; } }
    public class VerifyPaymentRequest { public string OrderNumber { get; set; } = default!; }
    public class ExecuteWalletRequest
    {
        public string OrderNumber { get; set; } = default!;
        public string WalletPhone { get; set; } = default!;
    }
    public class CheckPaymentRequest
    {
        public string? OrderNumber { get; set; }
        public string? TransactionId { get; set; }
    }
    public class PaymentPendingViewModel
    {
        public string? OrderNumber { get; set; }
        public string? TransactionId { get; set; }
        public decimal Amount { get; set; }
    }
}