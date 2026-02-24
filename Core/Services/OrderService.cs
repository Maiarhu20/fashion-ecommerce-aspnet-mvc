using Core.Services.Email;
using Core.DTOs.Checkout;
using Core.DTOs.Payment;
using Domain.Models;
using Infrastructure.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Core.Services
{
    public class OrderService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly CartService _cartService;
        private readonly ILogger<OrderService> _logger;
        private readonly PaymentService _paymentService;
        private readonly IEmailService _emailService;
        private readonly DistributedCacheService _cacheService; // Add this

        public OrderService(
            IUnitOfWork unitOfWork,
            CartService cartService,
            ILogger<OrderService> logger,
            PaymentService paymentService,
            DistributedCacheService cacheService,
            IEmailService emailService)
        {
            _unitOfWork = unitOfWork;
            _cartService = cartService;
            _logger = logger;
            _paymentService = paymentService;
            _cacheService = cacheService;
            _emailService = emailService;
        }

        public async Task<CheckoutDto> PrepareCheckoutAsync(string sessionId)
        {
            try
            {
                var cart = await _cartService.GetOrCreateCartAsync(sessionId);
                await _cartService.ValidateCartAsync(sessionId);

                if (!cart.Items.Any())
                    throw new InvalidOperationException("Cart is empty");

                var availableCities = await GetActiveShippingCitiesAsync();

                return new CheckoutDto
                {
                    Cart = cart,
                    AvailableCities = availableCities,
                    CartTotal = cart.TotalAmount,
                    ShippingCost = 0, // Will be calculated when city is selected
                    GrandTotal = cart.TotalAmount
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error preparing checkout for session {SessionId}", sessionId);
                throw;
            }
        }

        public async Task<decimal> CalculateShippingCostAsync(int cityId)
        {
            var city = await _unitOfWork.ShippingCities.GetByIdAsync(cityId);
            if (city == null || !city.IsActive)
                throw new KeyNotFoundException("Shipping city not found or inactive");

            return city.ShippingCost;
        }

        public async Task<OrderConfirmationDto> PlaceOrderAsync(string sessionId, PlaceOrderRequest request)
        {
            _logger.LogInformation("Starting PlaceOrderAsync for session {SessionId}", sessionId);
            _logger.LogInformation("Request details: {GuestName}, {Email}, CityId: {CityId}, Payment: {PaymentMethod}",
                request.GuestName, request.GuestEmail, request.ShippingCityId, request.PaymentMethod);

            using var transaction = await _unitOfWork.BeginTransactionAsync();
            try
            {
                // 1. Validate cart
                _logger.LogInformation("Validating cart...");
                var cart = await _cartService.GetOrCreateCartAsync(sessionId);
                await _cartService.ValidateCartAsync(sessionId);

                if (!cart.Items.Any())
                    throw new InvalidOperationException("Cannot place order with empty cart");

                _logger.LogInformation("Cart validated. Items: {ItemCount}", cart.Items.Count);

                // 2. Get shipping city
                _logger.LogInformation("Getting shipping city ID: {CityId}", request.ShippingCityId);
                var shippingCity = await _unitOfWork.ShippingCities.GetByIdAsync(request.ShippingCityId);
                if (shippingCity == null || !shippingCity.IsActive)
                    throw new InvalidOperationException($"Invalid shipping city. ID: {request.ShippingCityId}, Found: {shippingCity != null}, Active: {shippingCity?.IsActive}");

                _logger.LogInformation("Shipping city found: {CityName}, Cost: {ShippingCost}",
                    shippingCity.CityName, shippingCity.ShippingCost);

                // 3. Use calculated shipping cost or city cost
                var finalShippingCost = request.ShippingCost > 0 ? request.ShippingCost : shippingCity.ShippingCost;
                _logger.LogInformation("Final shipping cost: {FinalShippingCost}", finalShippingCost);

                // 4. Check stock availability
                _logger.LogInformation("Checking stock availability...");
                foreach (var item in cart.Items)
                {
                    var product = await _unitOfWork.Products.GetByIdAsync(item.ProductId);
                    if (product == null || product.IsDeleted || product.StockQuantity < item.Quantity)
                    {
                        throw new InvalidOperationException(
                            $"Product '{item.ProductName}' is not available in requested quantity. " +
                            $"Stock: {product?.StockQuantity ?? 0}, Requested: {item.Quantity}");
                    }
                }

                // 5. Generate order number
                var orderNumber = GenerateOrderNumber();
                _logger.LogInformation("Generated order number: {OrderNumber}", orderNumber);

                // 6. Create order
                _logger.LogInformation("Creating order entity...");
                var order = new Order
                {
                    OrderNumber = orderNumber,
                    GuestName = request.GuestName,
                    GuestEmail = request.GuestEmail,
                    GuestPhone = request.GuestPhone,
                    ShippingAddress = request.ShippingAddress,
                    ShippingCityId = shippingCity.Id,
                    ShippingCityName = shippingCity.CityName,
                    ShippingPostalCode = string.IsNullOrWhiteSpace(request.ShippingPostalCode) ? null : request.ShippingPostalCode,
                    ShippingCountry = "Egypt",
                    ShippingCost = finalShippingCost,
                    PaymentMethod = request.PaymentMethod,
                    Status = OrderStatus.Pending,
                    Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes,
                    Subtotal = cart.Subtotal,
                    DiscountAmount = cart.DiscountAmount,
                    OriginalTotal = cart.TotalOriginalPrice,
                    TotalAmount = cart.TotalAmount + finalShippingCost,
                    DiscountCode = cart.DiscountCode,
                    ShippingCity = shippingCity,
                    OrderDate = DateTime.UtcNow
                };

                _logger.LogInformation("Adding order to database...");
                try
                {
                    await _unitOfWork.Orders.AddAsync(order);
                    await _unitOfWork.SaveAsync();
                    _logger.LogInformation("Order saved with ID: {OrderId}", order.Id);
                }
                catch (DbUpdateException dbEx)
                {
                    // Log the specific database error
                    _logger.LogError(dbEx, "DATABASE SAVE FAILED for OrderNumber: {OrderNumber}", orderNumber);

                    // Log the inner exception with full details
                    if (dbEx.InnerException != null)
                    {
                        _logger.LogError("INNER EXCEPTION: {Message}", dbEx.InnerException.Message);
                        _logger.LogError("INNER STACK: {StackTrace}", dbEx.InnerException.StackTrace);
                    }

                    // Re-throw a more specific exception
                    throw new InvalidOperationException($"Failed to save order to database: {dbEx.InnerException?.Message}", dbEx);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "General error saving order");
                    throw;
                }

                _logger.LogInformation("Order saved with ID: {OrderId}", order.Id);

                // 7. Create order items and update stock
                _logger.LogInformation("Creating order items...");
                foreach (var item in cart.Items)
                {
                    var product = await _unitOfWork.Products.GetByIdAsync(item.ProductId);

                    var orderItem = new OrderItem
                    {
                        OrderId = order.Id,
                        ProductId = item.ProductId,
                        Quantity = item.Quantity,
                        SelectedColor = item.SelectedColor,
                        UnitPrice = item.UnitPrice,
                        LineTotal = item.LineTotal,
                        DiscountPercent = item.DiscountPercent
                    };

                    _logger.LogInformation("Adding order item: Product {ProductId}, Quantity {Quantity}",
                        item.ProductId, item.Quantity);
                    await _unitOfWork.OrderItems.AddAsync(orderItem);

                    // Update stock
                    product!.StockQuantity -= item.Quantity;
                    _logger.LogInformation("Updating product stock: {ProductId}, New stock: {NewStock}",
                        product.Id, product.StockQuantity);
                    _unitOfWork.Products.Update(product);
                }

                // 8. Create payment record - BUT DON'T ADD IT YET
                _logger.LogInformation("Creating payment record...");
                var payment = new Payment
                {
                    OrderId = order.Id,
                    Amount = order.TotalAmount,
                    OriginalAmount = order.OriginalTotal,
                    PaymentMethod = request.PaymentMethod,
                    Currency = "EGP",
                    Status = PaymentStatus.Pending,
                    AppliedDiscountCode = cart.DiscountCode,
                    DiscountAmount = cart.DiscountAmount,
                    CreatedDate = DateTime.UtcNow
                };

                if (request.PaymentMethod == PaymentMethod.Card)
                {
                    _logger.LogInformation("Processing credit card payment via Paymob...");
                    try
                    {
                        var paymobResult = await _paymentService.InitiatePaymobPaymentAsync(new PaymentRequestDto
                        {
                            Amount = order.TotalAmount,
                            Currency = "EGP",
                            //OrderId = order.Id,
                            OrderNumber = order.OrderNumber, // ⭐ ADD THIS
                            CustomerEmail = request.GuestEmail,
                            CustomerPhone = request.GuestPhone,
                            CustomerName = request.GuestName
                        });

                        if (paymobResult.Success)
                        {
                            payment.ProviderName = "Paymob";
                            payment.ProviderTransactionId = paymobResult.TransactionId;
                            payment.ProviderPaymentKey = paymobResult.PaymentKey; // ✅ Store it here

                            _logger.LogInformation("Paymob payment initialized. Transaction ID: {TransactionId}",
                                paymobResult.TransactionId);
                        }
                        else
                        {
                            // Paymob failed but order is saved
                            _logger.LogWarning("Paymob payment failed: {ErrorMessage}", paymobResult.ErrorMessage);
                            payment.ProviderName = "Paymob-Failed";
                            payment.Status = PaymentStatus.Failed;

                            // Don't add payment failure to notes - customer doesn't need to see system errors
                        }
                    }
                    catch (Exception paymobEx)
                    {
                        _logger.LogError(paymobEx, "Failed to initialize Paymob payment");
                        payment.ProviderName = "Paymob-Error";
                        payment.Status = PaymentStatus.Failed;

                        // Log the error but don't show to customer
                        _logger.LogWarning("Continuing order despite Paymob failure. Order will be created with failed payment.");
                    }
                }

                // ✅ NOW add the payment with all its properties set
                await _unitOfWork.Payments.AddAsync(payment);

                // 9. Increment discount usage if applied
                if (!string.IsNullOrEmpty(cart.DiscountCode))
                {
                    _logger.LogInformation("Incrementing discount usage for code: {DiscountCode}", cart.DiscountCode);
                    await IncrementDiscountUsage(cart.DiscountCode, sessionId, request.GuestEmail);
                }

                // 10. Clear cart
                _logger.LogInformation("Clearing cart...");
                await _cartService.ClearCartAsync(sessionId);

                // 11. Commit transaction
                _logger.LogInformation("Committing transaction...");
                await _unitOfWork.SaveAsync();
                await transaction.CommitAsync();

                // Send order confirmation email
                await SendOrderConfirmationEmailAsync(order);

                _logger.LogInformation("Order placed successfully. Order number: {OrderNumber}", orderNumber);

                // ✅ Reload the order with payment data before returning
                order = await LoadOrderWithCityAsync(orderNumber);
                return MapToOrderConfirmationDto(order);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error placing order for session {SessionId}", sessionId);
                _logger.LogError("Exception details: {Message}\n{StackTrace}", ex.Message, ex.StackTrace);

                // Log inner exception if exists
                if (ex.InnerException != null)
                {
                    _logger.LogError("Inner exception: {InnerMessage}\n{InnerStackTrace}",
                        ex.InnerException.Message, ex.InnerException.StackTrace);
                }

                try
                {
                    await transaction.RollbackAsync();
                    _logger.LogInformation("Transaction rolled back successfully");
                }
                catch (Exception rollbackEx)
                {
                    _logger.LogError(rollbackEx, "Failed to rollback transaction");
                }

                throw;
            }
        }

        public async Task<OrderPreparationResult> PrepareOrderForPaymentAsync(string sessionId, PlaceOrderRequest request)
        {
            _logger.LogInformation("Preparing order for payment for session {SessionId}", sessionId);

            try
            {
                var cart = await _cartService.GetOrCreateCartAsync(sessionId);
                await _cartService.ValidateCartAsync(sessionId);

                if (!cart.Items.Any())
                    throw new InvalidOperationException("Cannot prepare order with empty cart");

                var shippingCity = await _unitOfWork.ShippingCities.GetByIdAsync(request.ShippingCityId);
                if (shippingCity == null || !shippingCity.IsActive)
                    throw new InvalidOperationException("Invalid shipping city");

                var finalShippingCost = request.ShippingCost > 0 ? request.ShippingCost : shippingCity.ShippingCost;

                // Stock validation
                foreach (var item in cart.Items)
                {
                    var product = await _unitOfWork.Products.GetByIdAsync(item.ProductId);
                    if (product == null || product.IsDeleted || product.StockQuantity < item.Quantity)
                    {
                        throw new InvalidOperationException(
                            $"Product '{item.ProductName}' is not available in requested quantity.");
                    }
                }

                var orderNumber = GenerateOrderNumber();

                var tempOrder = new OrderPreparationDto
                {
                    OrderNumber = orderNumber,
                    GuestName = request.GuestName,
                    GuestEmail = request.GuestEmail,
                    GuestPhone = request.GuestPhone,
                    ShippingAddress = request.ShippingAddress,
                    ShippingCityId = shippingCity.Id,
                    ShippingCityName = shippingCity.CityName,
                    ShippingCost = finalShippingCost,
                    PaymentMethod = request.PaymentMethod,
                    Subtotal = cart.Subtotal,
                    DiscountAmount = cart.DiscountAmount,
                    TotalAmount = cart.TotalAmount + finalShippingCost,
                    DiscountCode = cart.DiscountCode,
                    CartItems = cart.Items.Select(i => new CartItemDto
                    {
                        ProductId = i.ProductId,
                        ProductName = i.ProductName,
                        Quantity = i.Quantity,
                        SelectedColor = i.SelectedColor,
                        UnitPrice = i.UnitPrice,
                        LineTotal = i.LineTotal
                    }).ToList()
                };

                var result = new OrderPreparationResult
                {
                    Success = true,
                    OrderNumber = orderNumber,
                    PaymentMethod = request.PaymentMethod,
                    TotalAmount = tempOrder.TotalAmount
                };

                // Handle Card and Wallet payments
                // In PrepareOrderForPaymentAsync, change this section:

                if (request.PaymentMethod == PaymentMethod.Card ||
                    request.PaymentMethod == PaymentMethod.Wallet)
                {
                    var paymentMethodType = request.PaymentMethod == PaymentMethod.Wallet
                        ? PaymentMethodType.Wallet
                        : PaymentMethodType.Card;

                    var paymobResult = await _paymentService.InitiatePaymobPaymentAsync(
                        new PaymentRequestDto
                        {
                            Amount = tempOrder.TotalAmount,
                            Currency = "EGP",
                            OrderNumber = orderNumber,
                            CustomerEmail = request.GuestEmail,
                            CustomerPhone = request.GuestPhone,
                            CustomerName = request.GuestName
                        },
                        paymentMethodType);

                    if (!paymobResult.Success)
                    {
                        return new OrderPreparationResult
                        {
                            Success = false,
                            ErrorMessage = paymobResult.ErrorMessage ?? "Payment initialization failed"
                        };
                    }

                    tempOrder.PaymentKey = paymobResult.PaymentKey;
                    tempOrder.TransactionId = paymobResult.TransactionId;
                    tempOrder.IframeId = paymobResult.IframeId;
                    // DON'T set WalletRedirectUrl here - it will be set when user submits phone

                    result.PaymentKey = paymobResult.PaymentKey;
                    result.IframeId = paymobResult.IframeId;

                    // Both card and wallet go to their respective pages
                    // The controller will handle the redirect
                    result.RedirectUrl = request.PaymentMethod == PaymentMethod.Wallet
                        ? $"/checkout/wallet-payment/{orderNumber}"
                        : $"/checkout/payment-processing/{orderNumber}";
                }
                else
                {
                    // COD
                    result.RedirectUrl = $"/checkout/complete-order/{orderNumber}";
                }

                // Save to cache
                var orderData = new OrderSessionData
                {
                    SessionId = sessionId,
                    OrderPreparation = tempOrder,
                    CartData = cart,
                    RequestData = request,
                    CreatedAt = DateTime.UtcNow
                };

                await _cacheService.SetAsync($"order_{sessionId}", orderData, TimeSpan.FromMinutes(30));

                // Also save by order number for callback processing
                await _cacheService.SetAsync($"order_by_number_{orderNumber}", orderData, TimeSpan.FromMinutes(30));

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error preparing order for payment");
                return new OrderPreparationResult
                {
                    Success = false,
                    ErrorMessage = ex.Message
                };
            }
        }

        // In OrderService.cs, find the CompleteOrderAsync method and fix this:

        public async Task<OrderConfirmationDto> CompleteOrderAsync(string sessionId,string orderNumber,bool paymentSuccess = false)
        {
            _logger.LogInformation("Completing order {OrderNumber} for session {SessionId}",
                orderNumber, sessionId);

            using var transaction = await _unitOfWork.BeginTransactionAsync();
            try
            {
                // Try to get order data from cache - first by session, then by order number
                var orderData = await _cacheService.GetAsync<OrderSessionData>($"order_{sessionId}");

                if (orderData == null || orderData.OrderPreparation.OrderNumber != orderNumber)
                {
                    _logger.LogInformation("Order data not found by session {SessionId}, trying by order number {OrderNumber}",
                        sessionId, orderNumber);

                    // Fallback: try to get by order number
                    orderData = await _cacheService.GetAsync<OrderSessionData>($"order_by_number_{orderNumber}");
                }

                if (orderData == null)
                {
                    _logger.LogError("Order data not found in cache for order {OrderNumber}", orderNumber);
                    throw new InvalidOperationException("Order data not found or expired. Please contact support.");
                }

                if (orderData.OrderPreparation.OrderNumber != orderNumber)
                {
                    _logger.LogError("Order number mismatch: Expected {Expected}, Got {Actual}",
                        orderNumber, orderData.OrderPreparation.OrderNumber);
                    throw new InvalidOperationException("Order data mismatch");
                }

                var tempOrder = orderData.OrderPreparation;
                var cart = orderData.CartData;
                var request = orderData.RequestData;

                // Check if payment is required
                if ((tempOrder.PaymentMethod == PaymentMethod.Card ||
                     tempOrder.PaymentMethod == PaymentMethod.Wallet) && !paymentSuccess)
                {
                    throw new InvalidOperationException("Payment not completed");
                }

                // Check if order already exists (to prevent duplicates)
                var existingOrder = (await _unitOfWork.Orders
                    .FindAsync(o => o.OrderNumber == orderNumber))
                    .FirstOrDefault();

                if (existingOrder != null)
                {
                    _logger.LogInformation("Order {OrderNumber} already exists, returning existing order", orderNumber);
                    // Clean up cache
                    await _cacheService.RemoveAsync($"order_{orderData.SessionId}");
                    await _cacheService.RemoveAsync($"order_by_number_{orderNumber}");
                    await transaction.CommitAsync();
                    return await GetOrderConfirmationAsync(orderNumber, existingOrder.GuestEmail);
                }

                var shippingCity = await _unitOfWork.ShippingCities.GetByIdAsync(tempOrder.ShippingCityId);
                if (shippingCity == null)
                {
                    throw new InvalidOperationException("Shipping city not found");
                }

                var originalTotal = cart.TotalOriginalPrice + tempOrder.ShippingCost;

                var order = new Order
                {
                    OrderNumber = tempOrder.OrderNumber,
                    GuestName = tempOrder.GuestName,
                    GuestEmail = tempOrder.GuestEmail,
                    GuestPhone = tempOrder.GuestPhone,
                    ShippingAddress = tempOrder.ShippingAddress,
                    ShippingCityId = shippingCity.Id,
                    ShippingCityName = shippingCity.CityName,

                    // ⭐ ADD THESE TO MATCH PlaceOrderAsync
                    ShippingPostalCode = string.IsNullOrWhiteSpace(request.ShippingPostalCode)
         ? null
         : request.ShippingPostalCode,
                    ShippingCountry = "Egypt",
                    ShippingCity = shippingCity,
                    Notes = string.IsNullOrWhiteSpace(request.Notes)
         ? null
         : request.Notes,

                    ShippingCost = tempOrder.ShippingCost,
                    PaymentMethod = tempOrder.PaymentMethod,
                    Status = OrderStatus.Pending,
                    Subtotal = tempOrder.Subtotal,
                    DiscountAmount = tempOrder.DiscountAmount,
                    OriginalTotal = originalTotal,
                    TotalAmount = tempOrder.TotalAmount,
                    DiscountCode = tempOrder.DiscountCode,
                    OrderDate = DateTime.UtcNow
                };

                await _unitOfWork.Orders.AddAsync(order);
                await _unitOfWork.SaveAsync();

                _logger.LogInformation("Order {OrderNumber} created with ID {OrderId}", orderNumber, order.Id);

                foreach (var item in cart.Items)
                {
                    var product = await _unitOfWork.Products.GetByIdAsync(item.ProductId);

                    if (product == null)
                    {
                        _logger.LogWarning("Product {ProductId} not found for order {OrderNumber}", item.ProductId, orderNumber);
                        continue;
                    }

                    var orderItem = new OrderItem
                    {
                        OrderId = order.Id,
                        ProductId = item.ProductId,
                        Quantity = item.Quantity,
                        SelectedColor = item.SelectedColor,
                        UnitPrice = item.UnitPrice,
                        LineTotal = item.LineTotal,
                        DiscountPercent = item.DiscountPercent
                    };

                    await _unitOfWork.OrderItems.AddAsync(orderItem);

                    product.StockQuantity -= item.Quantity;
                    _unitOfWork.Products.Update(product);
                }

                var paymentStatus = PaymentStatus.Pending;

                if (tempOrder.PaymentMethod == PaymentMethod.CashOnDelivery)
                {
                    paymentStatus = PaymentStatus.Pending;
                }
                else if ((tempOrder.PaymentMethod == PaymentMethod.Card ||
                          tempOrder.PaymentMethod == PaymentMethod.Wallet) && paymentSuccess)
                {
                    paymentStatus = PaymentStatus.Succeeded;
                }

                var payment = new Payment
                {
                    OrderId = order.Id,
                    Amount = order.TotalAmount,
                    OriginalAmount = originalTotal,
                    PaymentMethod = tempOrder.PaymentMethod,
                    Currency = "EGP",
                    Status = paymentStatus,
                    AppliedDiscountCode = tempOrder.DiscountCode,
                    DiscountAmount = tempOrder.DiscountAmount,
                    CreatedDate = DateTime.UtcNow,
                    ProviderPaymentKey = tempOrder.PaymentKey,
                    ProviderTransactionId = tempOrder.TransactionId,
                    ProviderName = (tempOrder.PaymentMethod == PaymentMethod.Card ||
                                   tempOrder.PaymentMethod == PaymentMethod.Wallet)
                        ? "Paymob"
                        : null,
                    CompletedDate = paymentSuccess ? DateTime.UtcNow : null
                };

                await _unitOfWork.Payments.AddAsync(payment);

                if (!string.IsNullOrEmpty(tempOrder.DiscountCode))
                {
                    await IncrementDiscountUsage(tempOrder.DiscountCode, orderData.SessionId, tempOrder.GuestEmail);
                }

                // Clear cart using the ORIGINAL session ID from the cached data
                await _cartService.ClearCartAsync(orderData.SessionId);

                // Remove both cache entries
                await _cacheService.RemoveAsync($"order_{orderData.SessionId}");
                await _cacheService.RemoveAsync($"order_by_number_{orderNumber}");

                await _unitOfWork.SaveAsync();
                await transaction.CommitAsync();

                // Send order confirmation email (don't await - fire and forget)
                _ = SendOrderConfirmationEmailAsync(order);

                _logger.LogInformation("Order completed successfully: {OrderNumber}", orderNumber);

                return await GetOrderConfirmationAsync(orderNumber, order.GuestEmail);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error completing order {OrderNumber}: {Message}", orderNumber, ex.Message);
                throw;
            }
        }

        public async Task<OrderConfirmationDto> GetOrderConfirmationAsync(string orderNumber, string? email = null)
        {
            try
            {
                var order = await LoadOrderWithCityAsync(orderNumber);

                // Verify email for guest orders
                if (!string.IsNullOrEmpty(email) && order.GuestEmail != email)
                    throw new UnauthorizedAccessException("Access denied");

                return MapToOrderConfirmationDto(order);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting order confirmation for order {OrderNumber}", orderNumber);
                throw;
            }
        }
        public async Task<List<Order>> GetOrdersByShippingCityAsync(int cityId)
        {
            return (await _unitOfWork.Orders.FindAsync(o => o.ShippingCityId == cityId))
                .ToList();
        }

        public async Task UpdateShippingCostForCityAsync(int cityId, decimal newCost, bool updateFutureOrdersOnly = true)
        {
            var city = await _unitOfWork.ShippingCities.GetByIdAsync(cityId);
            if (city == null)
                throw new KeyNotFoundException("Shipping city not found");

            // Update city cost
            var oldCost = city.ShippingCost;
            city.ShippingCost = newCost;
            city.LastModified = DateTime.UtcNow;
            _unitOfWork.ShippingCities.Update(city);

            // If needed, update pending orders for this city
            if (!updateFutureOrdersOnly)
            {
                var pendingOrders = (await _unitOfWork.Orders
                    .FindAsync(o => o.ShippingCityId == cityId && o.Status == OrderStatus.Pending))
                    .ToList();

                foreach (var order in pendingOrders)
                {
                    // Recalculate order total
                    order.ShippingCost = newCost;
                    order.TotalAmount = (order.Subtotal - order.DiscountAmount) + newCost;
                    _unitOfWork.Orders.Update(order);
                }
            }

            await _unitOfWork.SaveAsync();

            _logger.LogInformation(
                "Updated shipping cost for city {CityName} from {OldCost} to {NewCost}",
                city.CityName, oldCost, newCost);
        }

        public async Task<List<ShippingCityDto>> GetActiveShippingCitiesAsync()
        {
            var cities = await _unitOfWork.ShippingCities
                .FindAsync(c => c.IsActive);

            return cities.OrderBy(c => c.CityName)
                .Select(c => new ShippingCityDto
                {
                    Id = c.Id,
                    CityName = c.CityName,
                    ShippingCost = c.ShippingCost
                }).ToList();
        }

        // In OrderService.cs - Add this method
        public async Task<PaymentTokenResult> GetPaymentTokenAsync(string orderNumber)
        {
            try
            {
                var order = (await _unitOfWork.Orders
                    .FindAsync(o => o.OrderNumber == orderNumber,
                        includes: new[] { "Payment" }))
                    .FirstOrDefault();

                if (order == null)
                {
                    _logger.LogError("Order not found: {OrderNumber}", orderNumber);
                    return new PaymentTokenResult
                    {
                        Success = false,
                        Message = "Order not found"
                    };
                }

                if (order.Payment == null)
                {
                    _logger.LogError("Payment record not found for order: {OrderNumber}", orderNumber);
                    return new PaymentTokenResult
                    {
                        Success = false,
                        Message = "Payment record not found"
                    };
                }

                var paymentKey = order.Payment.ProviderPaymentKey;

                if (string.IsNullOrEmpty(paymentKey))
                {
                    if (order.PaymentMethod == PaymentMethod.Card)
                    {
                        _logger.LogInformation("Creating new Paymob payment for order {OrderNumber}", orderNumber);

                        var paymobResult = await _paymentService.InitiatePaymobPaymentAsync(
                            new PaymentRequestDto
                            {
                                Amount = order.TotalAmount,
                                Currency = "EGP",
                                OrderNumber = order.OrderNumber, // ✅ FIX: Add OrderNumber
                                CustomerEmail = order.GuestEmail,
                                CustomerPhone = order.GuestPhone,
                                CustomerName = order.GuestName
                            },
                            PaymentMethodType.Card); // ✅ FIX: Specify payment type

                        if (!paymobResult.Success)
                        {
                            return new PaymentTokenResult
                            {
                                Success = false,
                                Message = paymobResult.ErrorMessage ?? "Failed to create payment token"
                            };
                        }

                        paymentKey = paymobResult.PaymentKey;

                        order.Payment.ProviderPaymentKey = paymentKey;
                        order.Payment.ProviderTransactionId = paymobResult.TransactionId;
                        _unitOfWork.Payments.Update(order.Payment);
                        await _unitOfWork.SaveAsync();
                    }
                    else
                    {
                        return new PaymentTokenResult
                        {
                            Success = false,
                            Message = "No payment key available for this order"
                        };
                    }
                }

                return new PaymentTokenResult
                {
                    Success = true,
                    PaymentKey = paymentKey,
                    OrderNumber = orderNumber,
                    Amount = order.TotalAmount
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting payment token for order {OrderNumber}", orderNumber);
                return new PaymentTokenResult
                {
                    Success = false,
                    Message = "Error getting payment token"
                };
            }
        }

        // Add this complete method to send order confirmation email
        private async Task SendOrderConfirmationEmailAsync(Order order)
        {
            try
            {
                _logger.LogInformation("Sending order confirmation email for order {OrderNumber}", order.OrderNumber);

                // Load order items if not already loaded
                var orderItems = order.OrderItems?.ToList();
                if (orderItems == null || !orderItems.Any())
                {
                    orderItems = (await _unitOfWork.OrderItems
                        .FindAsync(oi => oi.OrderId == order.Id, includes: new[] { "Product" }))
                        .ToList();
                }
                else
                {
                    // Ensure products are loaded
                    foreach (var item in orderItems)
                    {
                        if (item.Product == null)
                        {
                            item.Product = await _unitOfWork.Products.GetByIdAsync(item.ProductId);
                        }
                    }
                }

                var isCOD = order.PaymentMethod == PaymentMethod.CashOnDelivery;
                var depositAmount = order.TotalAmount * 0.5m;
                var remainingAmount = order.TotalAmount - depositAmount;

                // Bank Account Details
                var depositAccountNumber = "**********";
                var depositAccountName = "#####";
                var depositAccountNameArabic = "######";

                // WhatsApp Details
                var whatsappNumber = "***********";
                var whatsappMessage = Uri.EscapeDataString($"Hello, I want to send my deposit screenshot for Order #{order.OrderNumber}. Deposit Amount: EGP {depositAmount:N2}");

                // Build order items HTML
                var itemsHtml = new System.Text.StringBuilder();
                foreach (var item in orderItems)
                {
                    var productName = item.Product?.Name ?? "Product";
                    var color = !string.IsNullOrEmpty(item.SelectedColor) ? item.SelectedColor : "-";
                    itemsHtml.Append($@"
                <tr>
                    <td style='padding: 12px 8px; border-bottom: 1px solid #eee; font-weight: 500; font-size: 14px;'>{productName}</td>
                    <td style='padding: 12px 8px; border-bottom: 1px solid #eee; text-align: center; font-size: 14px;'>
                        <span style='background: #f9e8e9; color: #912356; padding: 4px 8px; border-radius: 10px; font-size: 12px;'>{color}</span>
                    </td>
                    <td style='padding: 12px 8px; border-bottom: 1px solid #eee; text-align: center; font-size: 14px;'>{item.Quantity}</td>
                    <td style='padding: 12px 8px; border-bottom: 1px solid #eee; text-align: right; font-size: 14px;'>EGP {item.UnitPrice:N2}</td>
                    <td style='padding: 12px 8px; border-bottom: 1px solid #eee; text-align: right; font-weight: 600; color: #912356; font-size: 14px;'>EGP {item.LineTotal:N2}</td>
                </tr>
            ");
                }

                // COD Deposit Section (only for Cash on Delivery)
                var codDepositSection = isCOD ? $@"
            <!-- DEPOSIT REQUIRED SECTION -->
            <div style='background: linear-gradient(135deg, #fff8e1 0%, #ffecb3 100%); border: 3px solid #ffc107; border-radius: 12px; padding: 20px; margin: 25px 0;'>
                <div style='text-align: center; margin-bottom: 20px;'>
                    <span style='font-size: 35px;'>⚠️</span>
                    <h2 style='color: #856404; margin: 10px 0 0 0; font-size: 20px;'>Deposit Required / مطلوب عربون</h2>
                </div>
                
                <!-- Deposit Amount Box -->
                <div style='background: white; border-radius: 12px; padding: 20px; text-align: center; margin-bottom: 20px; border: 2px solid #c9a227;'>
                    <p style='margin: 0 0 8px 0; color: #666; font-size: 13px;'>Deposit Amount (50%) / قيمة العربون</p>
                    <p style='margin: 0; font-size: 32px; font-weight: bold; color: #912356;'>EGP {depositAmount:N2}</p>
                    <p style='margin: 5px 0 0 0; color: #666; font-size: 15px; direction: rtl;'>{depositAmount:N2} جنيه مصري</p>
                </div>

                <!-- Bank Account Details -->
                <div style='background: white; border-radius: 12px; overflow: hidden; margin-bottom: 20px; border: 2px solid #912356;'>
                    <div style='background: linear-gradient(135deg, #912356 0%, #7a1d47 100%); color: white; padding: 12px 15px; font-weight: 600; font-size: 14px;'>
                        🏦 Transfer Deposit To / حول العربون إلى
                    </div>
                    <div style='padding: 15px;'>
                        <!-- Account Name -->
                        <div style='padding: 12px; background: #f8f9fa; border-radius: 8px; margin-bottom: 10px;'>
                            <p style='margin: 0 0 5px 0; color: #666; font-size: 12px;'>👤 Account Name / اسم الحساب:</p>
                            <p style='margin: 0; font-size: 18px; font-weight: bold; color: #333;'>{depositAccountName}</p>
                            <p style='margin: 3px 0 0 0; font-size: 14px; color: #666; direction: rtl;'>{depositAccountNameArabic}</p>
                        </div>
                        <!-- Account Number -->
                        <div style='padding: 12px; background: #f8f9fa; border-radius: 8px; margin-bottom: 10px;'>
                            <p style='margin: 0 0 5px 0; color: #666; font-size: 12px;'>📱 Account Number / رقم الحساب:</p>
                            <p style='margin: 0; font-size: 24px; font-weight: bold; color: #912356; font-family: monospace; letter-spacing: 2px;'>{depositAccountNumber}</p>
                        </div>
                        <!-- Amount -->
                        <div style='padding: 12px; background: #f9e8e9; border-radius: 8px; border: 2px dashed #912356;'>
                            <p style='margin: 0 0 5px 0; color: #666; font-size: 12px;'>💰 Amount to Transfer / المبلغ المطلوب:</p>
                            <p style='margin: 0; font-size: 22px; font-weight: bold; color: #912356;'>EGP {depositAmount:N2}</p>
                        </div>
                    </div>
                    <div style='background: #e3f2fd; padding: 10px 15px; font-size: 12px; color: #1565c0; text-align: center;'>
                        ℹ️ You can transfer via Instapay, Vodafone Cash, or any bank transfer<br>
                        يمكنك التحويل عبر انستاباي أو فودافون كاش أو أي تحويل بنكي
                    </div>
                </div>
                
                <!-- English Instructions -->
                <div style='background: white; border-radius: 12px; padding: 15px; margin-bottom: 15px;'>
                    <h4 style='color: #912356; margin: 0 0 12px 0; font-size: 14px;'>
                        ℹ️ Important Instructions:
                    </h4>
                    <ol style='margin: 0; padding-left: 20px; line-height: 1.8; color: #333; font-size: 13px;'>
                        <li>Your order <strong>will NOT be processed</strong> until we receive the 50% deposit.</li>
                        <li>Transfer <strong>EGP {depositAmount:N2}</strong> to account: <strong>{depositAccountNumber}</strong> ({depositAccountName})</li>
                        <li>Take a screenshot of the payment confirmation.</li>
                        <li>Send the screenshot via WhatsApp to confirm your order.</li>
                        <li>Once we verify your deposit, we will start processing your order.</li>
                    </ol>
                </div>
                
                <!-- Arabic Instructions -->
                <div style='background: white; border-radius: 12px; padding: 15px; margin-bottom: 20px; direction: rtl; text-align: right;'>
                    <h4 style='color: #912356; margin: 0 0 12px 0; font-size: 14px;'>
                        ℹ️ تعليمات هامة:
                    </h4>
                    <ol style='margin: 0; padding-right: 20px; line-height: 1.8; color: #333; font-size: 13px;'>
                        <li>لن يتم معالجة طلبك <strong>حتى نستلم العربون (50%)</strong>.</li>
                        <li>حول <strong>{depositAmount:N2} جنيه</strong> إلى حساب: <strong>{depositAccountNumber}</strong> ({depositAccountNameArabic})</li>
                        <li>قم بأخذ لقطة شاشة لتأكيد الدفع.</li>
                        <li>أرسل لقطة الشاشة عبر الواتساب لتأكيد طلبك.</li>
                        <li>بمجرد التحقق من العربون، سنبدأ في معالجة طلبك.</li>
                    </ol>
                </div>
                
                <!-- WhatsApp Button -->
                <div style='text-align: center; margin-bottom: 15px;'>
                    <a href='https://wa.me/{whatsappNumber}?text={whatsappMessage}' 
                       style='display: inline-block; background: #25d366; color: white; padding: 15px 35px; border-radius: 50px; text-decoration: none; font-weight: bold; font-size: 14px;'>
                        📱 Send Deposit Screenshot via WhatsApp
                    </a>
                    <p style='margin: 8px 0 0 0; font-size: 13px; color: #666;'>أرسل صورة العربون عبر الواتساب</p>
                    <p style='margin: 5px 0 0 0; font-size: 12px; color: #888;'>WhatsApp: {whatsappNumber}</p>
                </div>
                
                <!-- Warning Note -->
                <div style='background: #fff3cd; border: 1px solid #c9a227; border-radius: 8px; padding: 12px; text-align: center;'>
                    <p style='margin: 0; color: #856404; font-size: 13px;'>
                        <strong>⏰ Note:</strong> Orders without deposit confirmation will be automatically cancelled after 24 hours.
                        <br>
                        <strong>ملاحظة:</strong> سيتم إلغاء الطلبات بدون تأكيد العربون تلقائياً بعد 24 ساعة.
                    </p>
                </div>
            </div>
        " : "";

                // Payment method display text
                var paymentMethodText = order.PaymentMethod switch
                {
                    PaymentMethod.CashOnDelivery => "💵 Cash on Delivery",
                    PaymentMethod.Card => "💳 Credit/Debit Card (Paid)",
                    PaymentMethod.Wallet => "📱 Mobile Wallet (Paid)",
                    _ => order.PaymentMethod.ToString()
                };

                var paymentStatusBadge = order.PaymentMethod switch
                {
                    PaymentMethod.CashOnDelivery => "<span style='background: #fff3cd; color: #856404; padding: 5px 12px; border-radius: 15px; font-size: 12px; font-weight: 600;'>⏳ Deposit Required</span>",
                    _ => "<span style='background: #d4edda; color: #155724; padding: 5px 12px; border-radius: 15px; font-size: 12px; font-weight: 600;'>✓ Paid</span>"
                };

                // COD Summary section
                var codSummarySection = isCOD ? $@"
            <tr style='background: #fff3cd;'>
                <td colspan='2' style='padding: 10px 15px; border-top: 2px solid #c9a227; font-size: 14px;'>
                    <strong style='color: #856404;'>💰 Deposit Required (50%):</strong>
                </td>
                <td style='padding: 10px 15px; text-align: right; border-top: 2px solid #c9a227; font-size: 14px;'>
                    <strong style='color: #856404;'>EGP {depositAmount:N2}</strong>
                </td>
            </tr>
            <tr style='background: #f9e8e9;'>
                <td colspan='2' style='padding: 10px 15px; font-size: 14px;'>
                    <strong style='color: #912356;'>🚚 Pay on Delivery:</strong>
                </td>
                <td style='padding: 10px 15px; text-align: right; font-size: 14px;'>
                    <strong style='color: #912356;'>EGP {remainingAmount:N2}</strong>
                </td>
            </tr>
        " : "";

                // Build the complete HTML email - Mobile Responsive
                var htmlContent = $@"
<!DOCTYPE html>
<html lang='en'>
<head>
    <meta charset='UTF-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <meta http-equiv='X-UA-Compatible' content='IE=edge'>
    <title>Order Confirmation - {order.OrderNumber}</title>
    <!--[if mso]>
    <style type='text/css'>
        table {{ border-collapse: collapse; }}
        .mobile-hide {{ display: table-cell !important; }}
    </style>
    <![endif]-->
</head>
<body style='font-family: -apple-system, BlinkMacSystemFont, ""Segoe UI"", Roboto, ""Helvetica Neue"", Arial, sans-serif; line-height: 1.6; color: #333; margin: 0; padding: 0; background-color: #f5f5f5; -webkit-font-smoothing: antialiased;'>
    
    <!-- Wrapper Table -->
    <table role='presentation' cellspacing='0' cellpadding='0' border='0' width='100%' style='background-color: #f5f5f5;'>
        <tr>
            <td style='padding: 20px 10px;'>
                
                <!-- Main Container -->
                <table role='presentation' cellspacing='0' cellpadding='0' border='0' width='100%' style='max-width: 600px; margin: 0 auto; background: white; border-radius: 12px; overflow: hidden; box-shadow: 0 4px 20px rgba(0,0,0,0.1);'>
                    
                    <!-- Header -->
                    <tr>
                        <td style='background: linear-gradient(135deg, #912356 0%, #7a1d47 100%); padding: 35px 20px; text-align: center;'>
                            <div style='width: 60px; height: 60px; background: rgba(255,255,255,0.2); border-radius: 50%; margin: 0 auto 15px; line-height: 60px;'>
                                <span style='font-size: 30px; color: white;'>✓</span>
                            </div>
                            <h1 style='color: white; margin: 0 0 8px 0; font-size: 24px; font-weight: 700;'>Order Confirmed!</h1>
                            <p style='color: rgba(255,255,255,0.9); margin: 0 0 12px 0; font-size: 14px;'>تم تأكيد طلبك بنجاح</p>
                            <div style='background: rgba(255,255,255,0.2); display: inline-block; padding: 8px 20px; border-radius: 20px;'>
                                <span style='color: white; font-size: 16px; font-weight: 600;'>Order #{order.OrderNumber}</span>
                            </div>
                        </td>
                    </tr>
                    
                    <!-- Main Content -->
                    <tr>
                        <td style='padding: 25px 20px;'>
                            
                            <!-- Greeting -->
                            <p style='font-size: 15px; margin: 0 0 15px 0;'>Dear <strong>{order.GuestName}</strong>,</p>
                            <p style='font-size: 14px; margin: 0 0 20px 0; color: #555;'>
                                Thank you for your order! We're excited to get your items ready for you.
                                {(isCOD ? " <strong style='color: #856404;'>Please note that a 50% deposit is required to process your order.</strong>" : "")}
                            </p>
                            
                            {codDepositSection}
                            
                            <!-- Order Information -->
                            <div style='background: #f8f9fa; border-radius: 12px; padding: 20px; margin: 20px 0; border: 1px solid #e9ecef;'>
                                <h3 style='color: #912356; margin: 0 0 15px 0; font-size: 16px; border-bottom: 2px solid #912356; padding-bottom: 8px;'>
                                    📋 Order Information
                                </h3>
                                <table role='presentation' cellspacing='0' cellpadding='0' border='0' width='100%' style='font-size: 14px;'>
                                    <tr>
                                        <td style='padding: 6px 0; color: #666; width: 40%;'>Order Number:</td>
                                        <td style='padding: 6px 0; font-weight: 600;'>{order.OrderNumber}</td>
                                    </tr>
                                    <tr>
                                        <td style='padding: 6px 0; color: #666;'>Order Date:</td>
                                        <td style='padding: 6px 0;'>{order.OrderDate:MMM dd, yyyy h:mm tt}</td>
                                    </tr>
                                    <tr>
                                        <td style='padding: 6px 0; color: #666;'>Payment:</td>
                                        <td style='padding: 6px 0;'>{paymentMethodText}</td>
                                    </tr>
                                    <tr>
                                        <td style='padding: 6px 0; color: #666;'>Status:</td>
                                        <td style='padding: 6px 0;'>{paymentStatusBadge}</td>
                                    </tr>
                                </table>
                            </div>
                            
                            <!-- Shipping Address -->
                            <div style='background: #f8f9fa; border-radius: 12px; padding: 20px; margin: 20px 0; border: 1px solid #e9ecef;'>
                                <h3 style='color: #912356; margin: 0 0 15px 0; font-size: 16px; border-bottom: 2px solid #912356; padding-bottom: 8px;'>
                                    🚚 Shipping Address
                                </h3>
                                <div style='background: white; padding: 12px; border-radius: 8px; border-left: 4px solid #912356; font-size: 14px;'>
                                    <p style='margin: 0 0 4px 0; font-weight: 600;'>{order.GuestName}</p>
                                    <p style='margin: 0 0 4px 0; color: #555;'>{order.ShippingAddress}</p>
                                    <p style='margin: 0 0 4px 0; color: #555;'>{order.ShippingCityName}</p>
                                    <p style='margin: 0 0 4px 0; color: #555;'>📞 {order.GuestPhone}</p>
                                    <p style='margin: 0; color: #555;'>✉️ {order.GuestEmail}</p>
                                </div>
                            </div>
                            
                            <!-- Order Items -->
                            <h3 style='color: #912356; margin: 25px 0 15px 0; font-size: 16px;'>🛍️ Order Items</h3>
                            <div style='border-radius: 12px; overflow: hidden; border: 1px solid #e9ecef;'>
                                <table role='presentation' cellspacing='0' cellpadding='0' border='0' width='100%' style='border-collapse: collapse;'>
                                    <thead>
                                        <tr style='background: linear-gradient(135deg, #912356 0%, #7a1d47 100%);'>
                                            <th style='padding: 12px 8px; text-align: left; color: white; font-weight: 600; font-size: 12px;'>Product</th>
                                            <th style='padding: 12px 8px; text-align: center; color: white; font-weight: 600; font-size: 12px;'>Color</th>
                                            <th style='padding: 12px 8px; text-align: center; color: white; font-weight: 600; font-size: 12px;'>Qty</th>
                                            <th style='padding: 12px 8px; text-align: right; color: white; font-weight: 600; font-size: 12px;'>Price</th>
                                            <th style='padding: 12px 8px; text-align: right; color: white; font-weight: 600; font-size: 12px;'>Total</th>
                                        </tr>
                                    </thead>
                                    <tbody>
                                        {itemsHtml}
                                    </tbody>
                                </table>
                            </div>
                            
                            <!-- Order Summary -->
                            <div style='background: #f8f9fa; border-radius: 12px; overflow: hidden; margin: 25px 0; border: 1px solid #e9ecef;'>
                                <div style='background: linear-gradient(135deg, #912356 0%, #7a1d47 100%); padding: 12px 15px;'>
                                    <h3 style='color: white; margin: 0; font-size: 16px;'>💰 Order Summary</h3>
                                </div>
                                <table role='presentation' cellspacing='0' cellpadding='0' border='0' width='100%' style='border-collapse: collapse;'>
                                    <tr>
                                        <td colspan='2' style='padding: 10px 15px; color: #666; font-size: 14px;'>Subtotal:</td>
                                        <td style='padding: 10px 15px; text-align: right; font-size: 14px;'>EGP {order.Subtotal:N2}</td>
                                    </tr>
                                    {(order.DiscountAmount > 0 ? $@"
                                    <tr style='color: #28a745;'>
                                        <td colspan='2' style='padding: 10px 15px; font-size: 14px;'>🏷️ Discount ({order.DiscountCode}):</td>
                                        <td style='padding: 10px 15px; text-align: right; font-size: 14px;'>-EGP {order.DiscountAmount:N2}</td>
                                    </tr>
                                    " : "")}
                                    <tr>
                                        <td colspan='2' style='padding: 10px 15px; color: #666; font-size: 14px;'>🚚 Shipping ({order.ShippingCityName}):</td>
                                        <td style='padding: 10px 15px; text-align: right; font-size: 14px;'>EGP {order.ShippingCost:N2}</td>
                                    </tr>
                                    <tr style='background: #912356; color: white;'>
                                        <td colspan='2' style='padding: 12px 15px; font-size: 16px; font-weight: bold;'>Grand Total:</td>
                                        <td style='padding: 12px 15px; text-align: right; font-size: 18px; font-weight: bold;'>EGP {order.TotalAmount:N2}</td>
                                    </tr>
                                    {codSummarySection}
                                </table>
                            </div>
                            
                            <!-- What's Next -->
                            
                            
                            <!-- Contact Support -->
                            <div style='text-align: center; padding: 20px; background: #f8f9fa; border-radius: 12px; margin-top: 25px;'>
                                <p style='margin: 0 0 8px 0; color: #666; font-size: 13px;'>Need help? Contact our support team:</p>
                                <p style='margin: 0;'>
                                    <a href='mailto:' style='color: #912356; text-decoration: none; font-weight: 600; font-size: 14px;'>📧 info@labellamodesty.store</a>
                                </p>
                                {(isCOD ? $@"
                                <p style='margin: 10px 0 0 0;'>
                                    <a href='https://wa.me/{whatsappNumber}' style='color: #25d366; text-decoration: none; font-weight: 600; font-size: 14px;'>📱 WhatsApp: </a>
                                </p>
                                " : "")}
                            </div>
                        </td>
                    </tr>
                    
                    <!-- Footer -->
                    <tr>
                        <td style='background: linear-gradient(135deg, #f9e8e9 0%, #fce4ec 100%); padding: 25px 20px; text-align: center;'>
                            <p style='margin: 0 0 8px 0; color: #912356; font-weight: 600; font-size: 15px;'>Thank you for shopping with us! 💕</p>
                            <p style='margin: 0 0 8px 0; color: #912356; font-size: 14px;'>شكراً لتسوقك معنا</p>
                            <p style='margin: 12px 0 0 0; font-size: 11px; color: #666;'>
                                © {DateTime.Now.Year} LaBella Scarves. All rights reserved.
                            </p>
                        </td>
                    </tr>
                    
                </table>
                <!-- End Main Container -->
                
            </td>
        </tr>
    </table>
    <!-- End Wrapper Table -->
    
</body>
</html>
";

                // Determine email subject
                var subject = isCOD
                    ? $"⚠️ Order Confirmed - Deposit Required - #{order.OrderNumber}"
                    : $"✓ Order Confirmed - #{order.OrderNumber}";

                // Send the email
                await _emailService.SendEmailAsync(order.GuestEmail, subject, htmlContent);

                _logger.LogInformation("Order confirmation email sent successfully for order {OrderNumber} to {Email}",
                    order.OrderNumber, order.GuestEmail);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send order confirmation email for order {OrderNumber}", order.OrderNumber);
                // Don't throw - email failure shouldn't break the order process
            }
        }

        private async Task<Order> LoadOrderWithCityAsync(string orderNumber)
        {
            var order = (await _unitOfWork.Orders
                .FindAsync(o => o.OrderNumber == orderNumber,
                    includes: new[] { "ShippingCity", "OrderItems", "Payment" })) // ✅ ADD "Payment"
                .FirstOrDefault();

            if (order == null)
                throw new KeyNotFoundException("Order not found");

            // Explicitly load OrderItems with Product if not already loaded
            if (order.OrderItems != null && order.OrderItems.Any())
            {
                foreach (var item in order.OrderItems)
                {
                    item.Product = await _unitOfWork.Products.GetByIdAsync(item.ProductId);
                }
            }

            return order;
        }

        private string GenerateOrderNumber()
        {
            var datePart = DateTime.UtcNow.ToString("yyyyMMdd");
            var suffix = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
            return $"ORD-{datePart}-{suffix}";
        }

        // Core/Services/OrderService.cs - Replace IncrementDiscountUsage method

        private async Task IncrementDiscountUsage(string discountCode, string sessionId, string? guestEmail = null)
        {
            var discount = await _unitOfWork.Discounts
                .FindOneAsync(d => d.Code.ToUpper() == discountCode.ToUpper());

            if (discount == null) return;

            // Update total usage count on discount (for stats)
            discount.TotalUsageCount++;
            discount.LastModified = DateTime.UtcNow;
            _unitOfWork.Discounts.Update(discount);

            // Track per-guest usage
            var existingUsage = await _unitOfWork.DiscountUsages
                .FindOneAsync(du => du.DiscountId == discount.Id && du.SessionId == sessionId);

            if (existingUsage != null)
            {
                existingUsage.UsageCount++;
                existingUsage.LastUsedDate = DateTime.UtcNow;
                if (!string.IsNullOrEmpty(guestEmail))
                    existingUsage.GuestEmail = guestEmail;

                _unitOfWork.DiscountUsages.Update(existingUsage);
            }
            else
            {
                var newUsage = new DiscountUsage
                {
                    DiscountId = discount.Id,
                    SessionId = sessionId,
                    GuestEmail = guestEmail,
                    UsageCount = 1,
                    FirstUsedDate = DateTime.UtcNow,
                    LastUsedDate = DateTime.UtcNow
                };

                await _unitOfWork.DiscountUsages.AddAsync(newUsage);
            }

            await _unitOfWork.SaveAsync();

            _logger.LogInformation("Recorded discount usage: Code={DiscountCode}, SessionId={SessionId}, GuestEmail={Email}",
                discountCode, sessionId, guestEmail);
        }

        private OrderConfirmationDto MapToOrderConfirmationDto(Order order)
        {
            var dto = new OrderConfirmationDto
            {
                OrderNumber = order.OrderNumber,
                OrderDate = order.OrderDate,
                CustomerName = order.GuestName,
                CustomerEmail = order.GuestEmail,
                CustomerPhone = order.GuestPhone,
                ShippingAddress = order.ShippingAddress,
                ShippingCity = order.ShippingCityName,
                ShippingCost = order.ShippingCost,
                CartTotal = order.Subtotal,
                DiscountAmount = order.DiscountAmount,
                GrandTotal = order.TotalAmount,
                PaymentMethod = order.PaymentMethod,
                ShippingCityDetails = order.ShippingCity != null ? new ShippingCityDto
                {
                    Id = order.ShippingCity.Id,
                    CityName = order.ShippingCity.CityName,
                    ShippingCost = order.ShippingCity.ShippingCost
                } : null,

                // ✅ ADD PAYMENT DATA
                PaymentKey = order.Payment?.ProviderPaymentKey,
                PaymentTransactionId = order.Payment?.ProviderTransactionId
            };

            if (order.OrderItems != null)
            {
                foreach (var item in order.OrderItems)
                {
                    var product = item.Product;

                    dto.Items.Add(new OrderItemDto
                    {
                        ProductName = product?.Name ?? "Unknown Product",
                        SelectedColor = item.SelectedColor,
                        Quantity = item.Quantity,
                        UnitPrice = item.UnitPrice,
                        LineTotal = item.LineTotal
                    });
                }
            }

            return dto;
        }
    }


    public class PaymentTokenResult
    {
        public bool Success { get; set; }
        public string? PaymentKey { get; set; }
        public string? OrderNumber { get; set; }
        public decimal Amount { get; set; }
        public string? Message { get; set; }
    }



}