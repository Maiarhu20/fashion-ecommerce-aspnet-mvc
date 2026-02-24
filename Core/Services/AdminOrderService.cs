using Core.DTOs.Orders;
using Core.Services.Email;
using Domain.Models;
using Infrastructure.Interfaces;
using Microsoft.Extensions.Logging;

namespace Core.Services
{
    public class AdminOrderService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<AdminOrderService> _logger;
        private readonly IEmailService _emailService;

        public AdminOrderService(
            IUnitOfWork unitOfWork,
            ILogger<AdminOrderService> logger,
            IEmailService emailService)
        {
            _unitOfWork = unitOfWork;
            _logger = logger;
            _emailService = emailService;
        }

        public async Task<ServiceResult<AdminOrderListResponse>> GetOrdersAsync(OrderFilterDto filter)
        {
            try
            {
                var allOrders = (await _unitOfWork.Orders
                    .FindAsync(o => true, includes: new[] { "ShippingCity", "Payment", "OrderItems" }))
                    .ToList();

                foreach (var order in allOrders)
                {
                    if (order.OrderItems != null)
                    {
                        foreach (var item in order.OrderItems)
                        {
                            item.Product = await _unitOfWork.Products.GetByIdAsync(item.ProductId);
                        }
                    }
                }

                var query = allOrders.AsEnumerable();

                if (!string.IsNullOrEmpty(filter.OrderNumber))
                {
                    query = query.Where(o => o.OrderNumber != null &&
                        o.OrderNumber.Contains(filter.OrderNumber, StringComparison.OrdinalIgnoreCase));
                }

                if (!string.IsNullOrEmpty(filter.CustomerEmail))
                {
                    query = query.Where(o => o.GuestEmail != null &&
                        o.GuestEmail.Contains(filter.CustomerEmail, StringComparison.OrdinalIgnoreCase));
                }

                if (!string.IsNullOrEmpty(filter.CustomerName))
                {
                    query = query.Where(o => o.GuestName != null &&
                        o.GuestName.Contains(filter.CustomerName, StringComparison.OrdinalIgnoreCase));
                }

                if (!string.IsNullOrEmpty(filter.Status))
                {
                    if (Enum.TryParse<OrderStatus>(filter.Status, out var status))
                    {
                        query = query.Where(o => o.Status == status);
                    }
                }

                if (!string.IsNullOrEmpty(filter.PaymentMethod))
                {
                    if (Enum.TryParse<PaymentMethod>(filter.PaymentMethod, out var paymentMethod))
                    {
                        query = query.Where(o => o.PaymentMethod == paymentMethod);
                    }
                }

                if (!string.IsNullOrEmpty(filter.PaymentStatus))
                {
                    if (Enum.TryParse<PaymentStatus>(filter.PaymentStatus, out var paymentStatus))
                    {
                        query = query.Where(o => o.Payment != null && o.Payment.Status == paymentStatus);
                    }
                }

                if (filter.StartDate.HasValue)
                {
                    query = query.Where(o => o.OrderDate >= filter.StartDate.Value);
                }

                if (filter.EndDate.HasValue)
                {
                    query = query.Where(o => o.OrderDate <= filter.EndDate.Value.AddDays(1));
                }

                if (filter.ShippingCityId.HasValue)
                {
                    query = query.Where(o => o.ShippingCityId == filter.ShippingCityId.Value);
                }

                var filteredList = query.ToList();
                var totalCount = filteredList.Count;

                filteredList = ApplySorting(filteredList, filter.SortBy, filter.SortDescending);

                var pagedOrders = filteredList
                    .Skip((filter.Page - 1) * filter.PageSize)
                    .Take(filter.PageSize)
                    .ToList();

                var orderDtos = pagedOrders.Select(MapToAdminOrderDto).ToList();

                var response = new AdminOrderListResponse
                {
                    Orders = orderDtos,
                    TotalCount = totalCount,
                    Page = filter.Page,
                    PageSize = filter.PageSize
                };

                return ServiceResult<AdminOrderListResponse>.Success(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting orders with filter");
                return ServiceResult<AdminOrderListResponse>.Failure($"Error retrieving orders: {ex.Message}", ex);
            }
        }

        private List<Order> ApplySorting(List<Order> orders, string sortBy, bool descending)
        {
            return sortBy?.ToLower() switch
            {
                "orderdate" => descending
                    ? orders.OrderByDescending(o => o.OrderDate).ToList()
                    : orders.OrderBy(o => o.OrderDate).ToList(),
                "totalamount" => descending
                    ? orders.OrderByDescending(o => o.TotalAmount).ToList()
                    : orders.OrderBy(o => o.TotalAmount).ToList(),
                "guestname" => descending
                    ? orders.OrderByDescending(o => o.GuestName).ToList()
                    : orders.OrderBy(o => o.GuestName).ToList(),
                "status" => descending
                    ? orders.OrderByDescending(o => o.Status).ToList()
                    : orders.OrderBy(o => o.Status).ToList(),
                _ => descending
                    ? orders.OrderByDescending(o => o.OrderDate).ToList()
                    : orders.OrderBy(o => o.OrderDate).ToList()
            };
        }

        public async Task<ServiceResult<AdminOrderDto>> GetOrderAsync(int orderId)
        {
            try
            {
                var order = await LoadOrderWithRelationsAsync(orderId);
                if (order == null)
                {
                    return ServiceResult<AdminOrderDto>.Failure("Order not found");
                }

                var dto = MapToAdminOrderDto(order);
                return ServiceResult<AdminOrderDto>.Success(dto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting order {OrderId}", orderId);
                return ServiceResult<AdminOrderDto>.Failure($"Error retrieving order: {ex.Message}", ex);
            }
        }

        public async Task<ServiceResult<AdminOrderDto>> UpdateOrderStatusAsync(int orderId, UpdateOrderStatusDto dto)
        {
            try
            {
                var order = await LoadOrderWithRelationsAsync(orderId);
                if (order == null)
                {
                    return ServiceResult<AdminOrderDto>.Failure("Order not found");
                }

                if (!Enum.TryParse<OrderStatus>(dto.Status, out var newStatus))
                {
                    return ServiceResult<AdminOrderDto>.Failure("Invalid status value");
                }

                var oldStatus = order.Status;
                order.Status = newStatus;

                if (newStatus == OrderStatus.Shipped)
                {
                    order.ShippedDate = dto.ShippedDate ?? DateTime.UtcNow;
                }

                if (newStatus == OrderStatus.Delivered)
                {
                    order.DeliveredDate = dto.DeliveredDate ?? DateTime.UtcNow;

                    if (!order.ShippedDate.HasValue)
                    {
                        order.ShippedDate = order.DeliveredDate.Value.AddDays(-1);
                    }

                    // === FIXED: For COD orders, auto-update payment to Succeeded when delivered ===
                    if (order.PaymentMethod == PaymentMethod.CashOnDelivery)
                    {
                        // First, try to find payment if it doesn't exist in navigation property
                        if (order.Payment == null)
                        {
                            var payment = (await _unitOfWork.Payments
                                .FindAsync(p => p.OrderId == orderId))
                                .FirstOrDefault();
                            order.Payment = payment;
                        }

                        if (order.Payment != null)
                        {
                            // Only update if payment is still pending
                            if (order.Payment.Status == PaymentStatus.Pending)
                            {
                                order.Payment.Status = PaymentStatus.Succeeded;
                                order.Payment.CompletedDate = DateTime.UtcNow;
                                _unitOfWork.Payments.Update(order.Payment);
                                _logger.LogInformation("COD Payment marked as Succeeded for order {OrderNumber}", order.OrderNumber);
                            }
                            else
                            {
                                _logger.LogInformation("COD Payment for order {OrderNumber} is already {Status}, skipping update",
                                    order.OrderNumber, order.Payment.Status);
                            }
                        }
                        else
                        {
                            _logger.LogWarning("No payment record found for COD order {OrderNumber}", order.OrderNumber);
                        }
                    }
                }

                if (newStatus == OrderStatus.Refunded && order.Payment != null)
                {
                    order.Payment.Status = PaymentStatus.Refunded;
                    order.Payment.CompletedDate = DateTime.UtcNow;
                    _unitOfWork.Payments.Update(order.Payment);
                }

                if (!string.IsNullOrEmpty(dto.Notes))
                {
                    order.Notes = string.IsNullOrEmpty(order.Notes)
                        ? dto.Notes
                        : $"{order.Notes}\n{DateTime.UtcNow:yyyy-MM-dd HH:mm}: {dto.Notes}";
                }

                _unitOfWork.Orders.Update(order);
                await _unitOfWork.SaveAsync();

                // Send email for Shipped status
                if (newStatus == OrderStatus.Shipped && oldStatus != OrderStatus.Shipped)
                {
                    await SendShippedEmailAsync(order);
                }

                order = await LoadOrderWithRelationsAsync(order.Id);
                var orderDto = MapToAdminOrderDto(order!);

                return ServiceResult<AdminOrderDto>.Success(orderDto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating order {OrderId} status", orderId);
                return ServiceResult<AdminOrderDto>.Failure($"Error updating order status: {ex.Message}", ex);
            }
        }

        public async Task<ServiceResult> CancelOrderAsync(int orderId, string reason)
        {
            try
            {
                var order = await LoadOrderWithRelationsAsync(orderId);
                if (order == null)
                {
                    return ServiceResult.Failure("Order not found");
                }

                if (order.Status == OrderStatus.Shipped ||
                    order.Status == OrderStatus.Delivered ||
                    order.Status == OrderStatus.Refunded ||
                    order.Status == OrderStatus.Cancelled)
                {
                    return ServiceResult.Failure($"Cannot cancel order with status: {order.Status}");
                }

                order.Status = OrderStatus.Cancelled;
                order.Notes = string.IsNullOrEmpty(order.Notes)
                    ? $"Cancelled on {DateTime.UtcNow:yyyy-MM-dd HH:mm}: {reason}"
                    : $"{order.Notes}\nCancelled on {DateTime.UtcNow:yyyy-MM-dd HH:mm}: {reason}";

                if (order.OrderItems != null)
                {
                    foreach (var item in order.OrderItems)
                    {
                        var product = await _unitOfWork.Products.GetByIdAsync(item.ProductId);
                        if (product != null)
                        {
                            product.StockQuantity += item.Quantity;
                            _unitOfWork.Products.Update(product);
                        }
                    }
                }

                // === FIXED: Update payment status to Cancelled ===
                if (order.Payment == null)
                {
                    var payment = (await _unitOfWork.Payments
                        .FindAsync(p => p.OrderId == orderId))
                        .FirstOrDefault();
                    order.Payment = payment;
                }

                if (order.Payment != null)
                {
                    // Only update if payment is still pending (don't overwrite succeeded payments)
                    if (order.Payment.Status == PaymentStatus.Pending)
                    {
                        order.Payment.Status = PaymentStatus.Cancelled;
                        order.Payment.CompletedDate = DateTime.UtcNow;
                        _unitOfWork.Payments.Update(order.Payment);
                        _logger.LogInformation("Payment marked as Cancelled for order {OrderNumber}", order.OrderNumber);
                    }
                    else if (order.Payment.Status == PaymentStatus.Succeeded)
                    {
                        // If payment was already succeeded, mark as Refunded instead
                        order.Payment.Status = PaymentStatus.Refunded;
                        order.Payment.CompletedDate = DateTime.UtcNow;
                        _unitOfWork.Payments.Update(order.Payment);
                        _logger.LogInformation("Payment marked as Refunded (was Succeeded) for order {OrderNumber}", order.OrderNumber);
                    }
                }
                else
                {
                    _logger.LogWarning("No payment record found for order {OrderNumber}", order.OrderNumber);
                }

                _unitOfWork.Orders.Update(order);
                await _unitOfWork.SaveAsync();

                await SendCancellationEmailAsync(order, reason);

                return ServiceResult.Success;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cancelling order {OrderId}", orderId);
                return ServiceResult.Failure($"Error cancelling order: {ex.Message}", ex);
            }
        }

        public async Task<ServiceResult<AdminOrderStatsDto>> GetOrderStatsAsync(DateTime? startDate = null, DateTime? endDate = null)
        {
            try
            {
                startDate ??= DateTime.UtcNow.AddMonths(-12);
                endDate ??= DateTime.UtcNow;

                var orders = (await _unitOfWork.Orders
                    .FindAsync(o => o.OrderDate >= startDate && o.OrderDate <= endDate,
                        includes: new[] { "Payment" }))
                    .ToList();

                // FIXED: Only count DELIVERED orders as confirmed revenue
                var deliveredOrders = orders.Where(o => o.Status == OrderStatus.Delivered).ToList();

                var stats = new AdminOrderStatsDto
                {
                    TotalOrders = orders.Count,
                    TotalRevenue = deliveredOrders.Sum(o => o.TotalAmount),
                    PendingOrders = orders.Count(o => o.Status == OrderStatus.Pending),
                    ProcessingOrders = orders.Count(o => o.Status == OrderStatus.Processing),
                    ShippedOrders = orders.Count(o => o.Status == OrderStatus.Shipped),
                    DeliveredOrders = orders.Count(o => o.Status == OrderStatus.Delivered),
                    CancelledOrders = orders.Count(o => o.Status == OrderStatus.Cancelled),
                    AverageOrderValue = deliveredOrders.Any() ? deliveredOrders.Average(o => o.TotalAmount) : 0
                };

                foreach (var status in Enum.GetValues<OrderStatus>())
                {
                    stats.StatusDistribution[status.ToString()] = orders.Count(o => o.Status == status);
                }

                var monthGroups = deliveredOrders
                    .GroupBy(o => new { o.OrderDate.Year, o.OrderDate.Month })
                    .OrderBy(g => g.Key.Year)
                    .ThenBy(g => g.Key.Month);

                foreach (var group in monthGroups)
                {
                    var monthKey = $"{group.Key.Year}-{group.Key.Month:D2}";
                    stats.RevenueByMonth[monthKey] = group.Sum(o => o.TotalAmount);
                }

                foreach (var method in Enum.GetValues<PaymentMethod>())
                {
                    stats.OrdersByPaymentMethod[method.ToString()] = orders.Count(o => o.PaymentMethod == method);
                }

                return ServiceResult<AdminOrderStatsDto>.Success(stats);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting order stats");
                return ServiceResult<AdminOrderStatsDto>.Failure($"Error retrieving statistics: {ex.Message}", ex);
            }
        }

        public async Task<ServiceResult<List<ShippingCity>>> GetShippingCitiesAsync()
        {
            try
            {
                var cities = (await _unitOfWork.ShippingCities.GetAllAsync()).ToList();
                return ServiceResult<List<ShippingCity>>.Success(cities);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting shipping cities");
                return ServiceResult<List<ShippingCity>>.Failure($"Error retrieving shipping cities: {ex.Message}", ex);
            }
        }

        // ===== NEW: REFUND ORDER METHOD =====
        public async Task<ServiceResult> RefundOrderAsync(int orderId, string reason)
        {
            try
            {
                var order = await LoadOrderWithRelationsAsync(orderId);
                if (order == null)
                {
                    return ServiceResult.Failure("Order not found");
                }

                // Only allow refund if order is Delivered
                if (order.Status != OrderStatus.Delivered)
                {
                    return ServiceResult.Failure($"Can only refund Delivered orders. Current status: {order.Status}");
                }

                // Update order status to Refunded
                order.Status = OrderStatus.Refunded;
                order.Notes = string.IsNullOrEmpty(order.Notes)
                    ? $"Refunded on {DateTime.UtcNow:yyyy-MM-dd HH:mm}: {reason}"
                    : $"{order.Notes}\nRefunded on {DateTime.UtcNow:yyyy-MM-dd HH:mm}: {reason}";

                // Return all items to stock
                if (order.OrderItems != null)
                {
                    foreach (var item in order.OrderItems)
                    {
                        var product = await _unitOfWork.Products.GetByIdAsync(item.ProductId);
                        if (product != null)
                        {
                            product.StockQuantity += item.Quantity;
                            _unitOfWork.Products.Update(product);
                            _logger.LogInformation("Returned {Quantity} units of product {ProductId} to stock for refund",
                                item.Quantity, item.ProductId);
                        }
                    }
                }

                // Update payment status to Refunded
                if (order.Payment == null)
                {
                    var payment = (await _unitOfWork.Payments
                        .FindAsync(p => p.OrderId == orderId))
                        .FirstOrDefault();
                    order.Payment = payment;
                }

                if (order.Payment != null)
                {
                    order.Payment.Status = PaymentStatus.Refunded;
                    order.Payment.CompletedDate = DateTime.UtcNow;
                    _unitOfWork.Payments.Update(order.Payment);
                    _logger.LogInformation("Payment marked as Refunded for order {OrderNumber}", order.OrderNumber);
                }
                else
                {
                    _logger.LogWarning("No payment record found for refund order {OrderNumber}", order.OrderNumber);
                }

                _unitOfWork.Orders.Update(order);
                await _unitOfWork.SaveAsync();

                //await SendRefundEmailAsync(order, reason);

                return ServiceResult.Success;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error refunding order {OrderId}", orderId);
                return ServiceResult.Failure($"Error refunding order: {ex.Message}", ex);
            }
        }

        

        private async Task<Order?> LoadOrderWithRelationsAsync(int orderId)
        {
            var order = (await _unitOfWork.Orders
                .FindAsync(o => o.Id == orderId,
                    includes: new[] { "ShippingCity", "Payment", "OrderItems", "AppliedDiscount" }))
                .FirstOrDefault();

            if (order?.OrderItems != null)
            {
                foreach (var item in order.OrderItems)
                {
                    item.Product = await _unitOfWork.Products.GetByIdAsync(item.ProductId);
                }
            }

            return order;
        }

        private AdminOrderDto MapToAdminOrderDto(Order order)
        {
            var dto = new AdminOrderDto
            {
                Id = order.Id,
                OrderNumber = order.OrderNumber ?? "",
                GuestName = order.GuestName ?? "",
                GuestEmail = order.GuestEmail ?? "",
                GuestPhone = order.GuestPhone ?? "",
                Status = order.Status.ToString(),
                PaymentMethod = order.PaymentMethod.ToString(),
                PaymentStatus = order.Payment?.Status.ToString() ?? "Unknown",
                OrderDate = order.OrderDate,
                ShippedDate = order.ShippedDate,
                DeliveredDate = order.DeliveredDate,
                TotalAmount = order.TotalAmount,
                Subtotal = order.Subtotal,
                ShippingCost = order.ShippingCost,
                DiscountAmount = order.DiscountAmount,
                DiscountCode = order.DiscountCode,
                ShippingAddress = order.ShippingAddress ?? "",
                ShippingCity = order.ShippingCityName ?? "",
                Notes = order.Notes
            };

            if (order.Payment != null)
            {
                dto.PaymentInfo = new PaymentInfoDto
                {
                    ProviderName = order.Payment.ProviderName,
                    TransactionId = order.Payment.ProviderTransactionId,
                    CreatedDate = order.Payment.CreatedDate,
                    CompletedDate = order.Payment.CompletedDate,
                    Status = order.Payment.Status.ToString()
                };
            }

            if (order.OrderItems != null)
            {
                foreach (var item in order.OrderItems)
                {
                    dto.Items.Add(new AdminOrderItemDto
                    {
                        ProductId = item.ProductId,
                        ProductName = item.Product?.Name ?? "Unknown Product",
                        SelectedColor = item.SelectedColor,
                        Quantity = item.Quantity,
                        UnitPrice = item.UnitPrice,
                        LineTotal = item.LineTotal,
                        DiscountPercent = item.DiscountPercent,
                        DiscountedPrice = item.DiscountedPrice,
                        StockQuantity = item.Product?.StockQuantity ?? 0
                    });
                }
            }

            return dto;
        }

        private string BuildOrderItemsTable(Order order)
        {
            var itemsHtml = new System.Text.StringBuilder();

            itemsHtml.Append(@"
                <table style='width: 100%; border-collapse: collapse; margin: 20px 0;'>
                    <thead>
                        <tr style='background-color: #f8f9fa;'>
                            <th style='padding: 12px; text-align: left; border-bottom: 2px solid #dee2e6;'>Product</th>
                            <th style='padding: 12px; text-align: center; border-bottom: 2px solid #dee2e6;'>Color</th>
                            <th style='padding: 12px; text-align: center; border-bottom: 2px solid #dee2e6;'>Qty</th>
                            <th style='padding: 12px; text-align: right; border-bottom: 2px solid #dee2e6;'>Price</th>
                            <th style='padding: 12px; text-align: right; border-bottom: 2px solid #dee2e6;'>Subtotal</th>
                        </tr>
                    </thead>
                    <tbody>");

            if (order.OrderItems != null)
            {
                foreach (var item in order.OrderItems)
                {
                    var productName = item.Product?.Name ?? "Unknown Product";
                    var color = !string.IsNullOrEmpty(item.SelectedColor) ? item.SelectedColor : "-";

                    itemsHtml.Append($@"
                        <tr>
                            <td style='padding: 12px; border-bottom: 1px solid #dee2e6;'>{productName}</td>
                            <td style='padding: 12px; text-align: center; border-bottom: 1px solid #dee2e6;'>{color}</td>
                            <td style='padding: 12px; text-align: center; border-bottom: 1px solid #dee2e6;'>{item.Quantity}</td>
                            <td style='padding: 12px; text-align: right; border-bottom: 1px solid #dee2e6;'>EGP {item.UnitPrice:N2}</td>
                            <td style='padding: 12px; text-align: right; border-bottom: 1px solid #dee2e6;'>EGP {item.LineTotal:N2}</td>
                        </tr>");
                }
            }

            itemsHtml.Append(@"
                    </tbody>
                </table>");

            return itemsHtml.ToString();
        }

        private async Task SendShippedEmailAsync(Order order)
        {
            try
            {
                var subject = $"Your Order #{order.OrderNumber} Has Been Shipped! 🚚";
                var orderItemsTable = BuildOrderItemsTable(order);

                var htmlContent = $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
</head>
<body style='font-family: Arial, Helvetica, sans-serif; line-height: 1.6; color: #333; margin: 0; padding: 0; background-color: #f4f4f4;'>
    <div style='max-width: 600px; margin: 0 auto; padding: 20px;'>
        <div style='background: linear-gradient(135deg, #912356 0%, #b32b5e 100%); color: white; padding: 30px; text-align: center; border-radius: 10px 10px 0 0;'>
            <h1 style='margin: 0 0 10px 0; font-size: 24px;'>🚚 Your Order is On Its Way!</h1>
            <p style='margin: 0; font-size: 16px;'>Order #{order.OrderNumber}</p>
        </div>
        
        <div style='background: #ffffff; padding: 30px; border: 1px solid #eaeaea;'>
            <p style='font-size: 16px;'>Dear <strong>{order.GuestName}</strong>,</p>
            
            <p>Great news! Your order has been shipped and is on its way to you.</p>
            
            <div style='background: #d4edda; color: #155724; padding: 15px; border-radius: 8px; margin: 20px 0; text-align: center;'>
                <strong style='font-size: 18px;'>Status: SHIPPED</strong>
                <p style='margin: 5px 0 0 0;'>Shipped on: {order.ShippedDate?.ToString("MMMM dd, yyyy 'at' hh:mm tt") ?? "Today"}</p>
            </div>

            <h3 style='color: #912356; border-bottom: 2px solid #912356; padding-bottom: 10px;'>📦 Order Items</h3>
            {orderItemsTable}

            <div style='background: #f8f9fa; padding: 20px; border-radius: 8px; margin: 20px 0;'>
                <h3 style='margin-top: 0; color: #333;'>Order Summary</h3>
                <table style='width: 100%;'>
                    <tr>
                        <td style='padding: 5px 0;'>Subtotal:</td>
                        <td style='text-align: right;'>EGP {order.Subtotal:N2}</td>
                    </tr>
                    {(order.DiscountAmount > 0 ? $@"
                    <tr style='color: #28a745;'>
                        <td style='padding: 5px 0;'>Discount:</td>
                        <td style='text-align: right;'>-EGP {order.DiscountAmount:N2}</td>
                    </tr>" : "")}
                    <tr>
                        <td style='padding: 5px 0;'>Shipping:</td>
                        <td style='text-align: right;'>EGP {order.ShippingCost:N2}</td>
                    </tr>
                    <tr style='font-weight: bold; font-size: 18px; border-top: 2px solid #dee2e6;'>
                        <td style='padding-top: 10px;'>Total:</td>
                        <td style='text-align: right; padding-top: 10px; color: #912356;'>EGP {order.TotalAmount:N2}</td>
                    </tr>
                </table>
            </div>

            <div style='background: #e7f3ff; padding: 20px; border-radius: 8px; margin: 20px 0;'>
                <h3 style='margin-top: 0; color: #0056b3;'>📍 Delivery Address</h3>
                <p style='margin: 0;'><strong>{order.GuestName}</strong></p>
                <p style='margin: 5px 0;'>{order.ShippingAddress}</p>
                <p style='margin: 5px 0;'>{order.ShippingCityName}</p>
                <p style='margin: 5px 0;'>Phone: {order.GuestPhone}</p>
            </div>

            <p>If you have any questions about your delivery, please don't hesitate to contact us.</p>
            
            <p>Thank you for shopping with us!</p>
        </div>
        
        <div style='text-align: center; padding: 20px; font-size: 12px; color: #666; border-top: 1px solid #eaeaea; background: #fff; border-radius: 0 0 10px 10px;'>
            <p style='margin: 0;'>&copy; {DateTime.Now.Year} . All rights reserved.</p>
        </div>
    </div>
</body>
</html>";

                await _emailService.SendEmailAsync(order.GuestEmail, subject, htmlContent);
                _logger.LogInformation("Shipped email sent for order {OrderNumber}", order.OrderNumber);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send shipped email for order {OrderNumber}", order.OrderNumber);
            }
        }

        private async Task SendCancellationEmailAsync(Order order, string reason)
        {
            try
            {
                var subject = $"Order #{order.OrderNumber} Has Been Cancelled";
                var orderItemsTable = BuildOrderItemsTable(order);

                var htmlContent = $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
</head>
<body style='font-family: Arial, Helvetica, sans-serif; line-height: 1.6; color: #333; margin: 0; padding: 0; background-color: #f4f4f4;'>
    <div style='max-width: 600px; margin: 0 auto; padding: 20px;'>
        <div style='background: linear-gradient(135deg, #dc3545 0%, #c82333 100%); color: white; padding: 30px; text-align: center; border-radius: 10px 10px 0 0;'>
            <h1 style='margin: 0 0 10px 0; font-size: 24px;'>Order Cancelled</h1>
            <p style='margin: 0; font-size: 16px;'>Order #{order.OrderNumber}</p>
        </div>
        
        <div style='background: #ffffff; padding: 30px; border: 1px solid #eaeaea;'>
            <p style='font-size: 16px;'>Dear <strong>{order.GuestName}</strong>,</p>
            
            <p>We regret to inform you that your order has been cancelled.</p>
            
            <div style='background: #f8d7da; color: #721c24; padding: 20px; border-radius: 8px; margin: 20px 0;'>
                <h4 style='margin: 0 0 10px 0;'>❌ Cancellation Reason:</h4>
                <p style='margin: 0;'>{reason}</p>
            </div>

            <h3 style='color: #dc3545; border-bottom: 2px solid #dc3545; padding-bottom: 10px;'>📦 Cancelled Items</h3>
            {orderItemsTable}

            <div style='background: #f8f9fa; padding: 20px; border-radius: 8px; margin: 20px 0;'>
                <h3 style='margin-top: 0; color: #333;'>Order Summary</h3>
                <table style='width: 100%;'>
                    <tr>
                        <td style='padding: 5px 0;'>Subtotal:</td>
                        <td style='text-align: right;'>EGP {order.Subtotal:N2}</td>
                    </tr>
                    {(order.DiscountAmount > 0 ? $@"
                    <tr style='color: #28a745;'>
                        <td style='padding: 5px 0;'>Discount:</td>
                        <td style='text-align: right;'>-EGP {order.DiscountAmount:N2}</td>
                    </tr>" : "")}
                    <tr>
                        <td style='padding: 5px 0;'>Shipping:</td>
                        <td style='text-align: right;'>EGP {order.ShippingCost:N2}</td>
                    </tr>
                    <tr style='font-weight: bold; font-size: 18px; border-top: 2px solid #dee2e6;'>
                        <td style='padding-top: 10px;'>Total (Cancelled):</td>
                        <td style='text-align: right; padding-top: 10px; color: #dc3545; text-decoration: line-through;'>EGP {order.TotalAmount:N2}</td>
                    </tr>
                </table>
            </div>

            <div style='background: #fff3cd; color: #856404; padding: 15px; border-radius: 8px; margin: 20px 0;'>
                <p style='margin: 0;'><strong>💡 Note:</strong> If you paid online, your refund will be processed within 5-7 business days.</p>
            </div>

            <p>If you believe this cancellation was made in error or if you have any questions, please contact our customer support immediately.</p>
            
            <p>We apologize for any inconvenience caused and hope to serve you again soon.</p>
            
            <p>Best regards,<br><strong>Customer Support Team</strong></p>
        </div>
        
        <div style='text-align: center; padding: 20px; font-size: 12px; color: #666; border-top: 1px solid #eaeaea; background: #fff; border-radius: 0 0 10px 10px;'>
            <p style='margin: 0;'>&copy; {DateTime.Now.Year} . All rights reserved.</p>
        </div>
    </div>
</body>
</html>";

                await _emailService.SendEmailAsync(order.GuestEmail, subject, htmlContent);
                _logger.LogInformation("Cancellation email sent for order {OrderNumber}", order.OrderNumber);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send cancellation email for order {OrderNumber}", order.OrderNumber);
            }
        }
    }
}