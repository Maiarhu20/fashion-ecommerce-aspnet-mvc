using Core.DTOs.Orders;
using Core.Services;
using Domain.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.Text;

namespace Web.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    [Route("admin/orders")]
    public class OrdersController : Controller
    {
        private readonly AdminOrderService _orderService;
        private readonly ILogger<OrdersController> _logger;

        public OrdersController(
            AdminOrderService orderService,
            ILogger<OrdersController> logger)
        {
            _orderService = orderService;
            _logger = logger;
        }

        [HttpGet("")]
        public async Task<IActionResult> Index(OrderFilterDto filter)
        {
            try
            {
                filter ??= new OrderFilterDto();

                var result = await _orderService.GetOrdersAsync(filter);
                if (!result.Succeeded)
                {
                    TempData["ErrorMessage"] = result.ErrorMessage;
                    return View(new AdminOrderListResponse());
                }

                ViewBag.Filter = filter;
                ViewBag.Statuses = GetOrderStatusSelectList();
                ViewBag.PaymentMethods = GetPaymentMethodSelectList();
                ViewBag.PaymentStatuses = GetPaymentStatusSelectList();

                return View(result.Data);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading orders index");
                TempData["ErrorMessage"] = "Error loading orders";

                ViewBag.Filter = filter ?? new OrderFilterDto();
                ViewBag.Statuses = GetOrderStatusSelectList();
                ViewBag.PaymentMethods = GetPaymentMethodSelectList();
                ViewBag.PaymentStatuses = GetPaymentStatusSelectList();

                return View(new AdminOrderListResponse());
            }
        }

        [HttpGet("details/{id:int}")]
        public async Task<IActionResult> Details(int id)
        {
            try
            {
                var result = await _orderService.GetOrderAsync(id);
                if (!result.Succeeded)
                {
                    TempData["ErrorMessage"] = result.ErrorMessage;
                    return RedirectToAction(nameof(Index));
                }

                ViewBag.Statuses = GetOrderStatusSelectList();
                return View(result.Data);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading order details {OrderId}", id);
                TempData["ErrorMessage"] = "Error loading order details";
                return RedirectToAction(nameof(Index));
            }
        }

        [HttpPost("update-status")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateStatus(int id, UpdateOrderStatusDto dto)
        {
            try
            {
                _logger.LogInformation("UpdateStatus called for order ID: {OrderId}, Status: {Status}", id, dto?.Status);

                if (id <= 0)
                {
                    TempData["ErrorMessage"] = "Invalid order ID";
                    return RedirectToAction(nameof(Index));
                }

                if (dto == null || string.IsNullOrEmpty(dto.Status))
                {
                    TempData["ErrorMessage"] = "Please select a status";
                    return RedirectToAction(nameof(Details), new { id });
                }

                var result = await _orderService.UpdateOrderStatusAsync(id, dto);
                if (!result.Succeeded)
                {
                    TempData["ErrorMessage"] = result.ErrorMessage;
                }
                else
                {
                    TempData["SuccessMessage"] = $"Order status updated to {dto.Status}";
                }

                return RedirectToAction(nameof(Details), new { id });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating order status {OrderId}", id);
                TempData["ErrorMessage"] = "Error updating order status";
                return RedirectToAction(nameof(Details), new { id });
            }
        }

        [HttpPost("cancel")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CancelOrder(int id, string reason)
        {
            try
            {
                _logger.LogInformation("CancelOrder called for order ID: {OrderId}", id);

                if (id <= 0)
                {
                    TempData["ErrorMessage"] = "Invalid order ID";
                    return RedirectToAction(nameof(Index));
                }

                if (string.IsNullOrWhiteSpace(reason))
                {
                    TempData["ErrorMessage"] = "Cancellation reason is required";
                    return RedirectToAction(nameof(Details), new { id });
                }

                var result = await _orderService.CancelOrderAsync(id, reason);
                if (!result.Succeeded)
                {
                    TempData["ErrorMessage"] = result.ErrorMessage;
                }
                else
                {
                    TempData["SuccessMessage"] = "Order cancelled successfully";
                }

                return RedirectToAction(nameof(Details), new { id });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cancelling order {OrderId}", id);
                TempData["ErrorMessage"] = "Error cancelling order";
                return RedirectToAction(nameof(Details), new { id });
            }
        }

        [HttpGet("stats")]
        public async Task<IActionResult> Stats()
        {
            try
            {
                var result = await _orderService.GetOrderStatsAsync();
                if (!result.Succeeded)
                {
                    TempData["ErrorMessage"] = result.ErrorMessage;
                    return View(new AdminOrderStatsDto());
                }

                return View(result.Data);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading order stats");
                TempData["ErrorMessage"] = "Error loading statistics";
                return View(new AdminOrderStatsDto());
            }
        }

        [HttpGet("export")]
        public async Task<IActionResult> Export(OrderFilterDto filter)
        {
            try
            {
                filter ??= new OrderFilterDto();
                filter.PageSize = 1000000;
                filter.Page = 1;

                var result = await _orderService.GetOrdersAsync(filter);
                if (!result.Succeeded)
                {
                    TempData["ErrorMessage"] = result.ErrorMessage;
                    return RedirectToAction(nameof(Index));
                }

                var csv = GenerateOrdersCsv(result.Data.Orders);
                var fileName = $"orders-export-{DateTime.UtcNow:yyyyMMdd-HHmmss}.csv";

                return File(Encoding.UTF8.GetBytes(csv), "text/csv", fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting orders");
                TempData["ErrorMessage"] = "Error exporting orders";
                return RedirectToAction(nameof(Index));
            }
        }

        // ===== NEW: REFUND ORDER ENDPOINT =====
        [HttpPost("refund")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RefundOrder(int id, string reason)
        {
            try
            {
                _logger.LogInformation("RefundOrder called for order ID: {OrderId}", id);

                if (id <= 0)
                {
                    TempData["ErrorMessage"] = "Invalid order ID";
                    return RedirectToAction(nameof(Index));
                }

                if (string.IsNullOrWhiteSpace(reason))
                {
                    TempData["ErrorMessage"] = "Refund reason is required";
                    return RedirectToAction(nameof(Details), new { id });
                }

                var result = await _orderService.RefundOrderAsync(id, reason);
                if (!result.Succeeded)
                {
                    TempData["ErrorMessage"] = result.ErrorMessage;
                }
                else
                {
                    TempData["SuccessMessage"] = "Order refunded successfully and stock has been returned";
                }

                return RedirectToAction(nameof(Details), new { id });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error refunding order {OrderId}", id);
                TempData["ErrorMessage"] = "Error refunding order";
                return RedirectToAction(nameof(Details), new { id });
            }
        }

        private string GenerateOrdersCsv(List<AdminOrderDto> orders)
        {
            var csv = new StringBuilder();
            csv.AppendLine("OrderNumber,GuestName,GuestEmail,GuestPhone,Status,PaymentMethod,OrderDate,TotalAmount,ShippingCost,DiscountAmount,ShippingAddress,ShippingCity");

            foreach (var order in orders)
            {
                csv.AppendLine($"\"{order.OrderNumber}\"," +
                              $"\"{order.GuestName}\"," +
                              $"\"{order.GuestEmail}\"," +
                              $"\"{order.GuestPhone}\"," +
                              $"\"{order.Status}\"," +
                              $"\"{order.PaymentMethod}\"," +
                              $"\"{order.OrderDate:yyyy-MM-dd HH:mm}\"," +
                              $"{order.TotalAmount}," +
                              $"{order.ShippingCost}," +
                              $"{order.DiscountAmount}," +
                              $"\"{order.ShippingAddress?.Replace("\"", "\"\"")}\"," +
                              $"\"{order.ShippingCity}\"");
            }

            return csv.ToString();
        }

        private List<SelectListItem> GetOrderStatusSelectList()
        {
            return Enum.GetValues(typeof(OrderStatus))
                .Cast<OrderStatus>()
                .Select(s => new SelectListItem
                {
                    Value = s.ToString(),
                    Text = s.ToString()
                })
                .ToList();
        }

        private List<SelectListItem> GetPaymentMethodSelectList()
        {
            return Enum.GetValues(typeof(PaymentMethod))
                .Cast<PaymentMethod>()
                .Select(m => new SelectListItem
                {
                    Value = m.ToString(),
                    Text = m.ToString()
                })
                .ToList();
        }

        private List<SelectListItem> GetPaymentStatusSelectList()
        {
            return Enum.GetValues(typeof(PaymentStatus))
                .Cast<PaymentStatus>()
                .Select(s => new SelectListItem
                {
                    Value = s.ToString(),
                    Text = s.ToString()
                })
                .ToList();
        }
    }
}