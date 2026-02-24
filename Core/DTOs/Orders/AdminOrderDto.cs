using System;
using System.Collections.Generic;

namespace Core.DTOs.Orders
{
    public class AdminOrderDto
    {
        public int Id { get; set; }
        public string OrderNumber { get; set; } = default!;
        public string GuestName { get; set; } = default!;
        public string GuestEmail { get; set; } = default!;
        public string GuestPhone { get; set; } = default!;
        public string Status { get; set; } = default!;
        public string PaymentMethod { get; set; } = default!;
        public string PaymentStatus { get; set; } = default!;
        public DateTime OrderDate { get; set; }
        public DateTime? ShippedDate { get; set; }
        public DateTime? DeliveredDate { get; set; }
        public decimal TotalAmount { get; set; }
        public decimal Subtotal { get; set; }
        public decimal ShippingCost { get; set; }
        public decimal DiscountAmount { get; set; }
        public string? DiscountCode { get; set; }
        public string ShippingAddress { get; set; } = default!;
        public string ShippingCity { get; set; } = default!;
        public string? Notes { get; set; }
        public List<AdminOrderItemDto> Items { get; set; } = new();
        public PaymentInfoDto? PaymentInfo { get; set; }
    }

    public class AdminOrderItemDto
    {
        public int ProductId { get; set; }
        public string ProductName { get; set; } = default!;
        public string? SelectedColor { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal LineTotal { get; set; }
        public decimal? DiscountPercent { get; set; }
        public decimal DiscountedPrice { get; set; }
        public int StockQuantity { get; set; }
    }

    public class PaymentInfoDto
    {
        public string? ProviderName { get; set; }
        public string? TransactionId { get; set; }
        public DateTime CreatedDate { get; set; }
        public DateTime? CompletedDate { get; set; }
        public string Status { get; set; } = default!;
    }

    public class OrderFilterDto
    {
        public string? OrderNumber { get; set; }
        public string? CustomerEmail { get; set; }
        public string? CustomerName { get; set; }
        public string? Status { get; set; }
        public string? PaymentMethod { get; set; }
        public string? PaymentStatus { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public int? ShippingCityId { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 20;
        public string SortBy { get; set; } = "OrderDate";
        public bool SortDescending { get; set; } = true;
    }

    public class UpdateOrderStatusDto
    {
        public string Status { get; set; } = default!;
        public string? Notes { get; set; }
        public DateTime? ShippedDate { get; set; }
        public DateTime? DeliveredDate { get; set; }
        public string? TrackingNumber { get; set; }
    }

    public class AdminOrderStatsDto
    {
        public int TotalOrders { get; set; }
        public decimal TotalRevenue { get; set; }
        public decimal AverageOrderValue { get; set; }
        public int PendingOrders { get; set; }
        public int ProcessingOrders { get; set; }
        public int ShippedOrders { get; set; }
        public int DeliveredOrders { get; set; }
        public int CancelledOrders { get; set; }
        public Dictionary<string, int> StatusDistribution { get; set; } = new();
        public Dictionary<string, decimal> RevenueByMonth { get; set; } = new();
        public Dictionary<string, int> OrdersByPaymentMethod { get; set; } = new();
    }

    public class AdminOrderListResponse
    {
        public List<AdminOrderDto> Orders { get; set; } = new();
        public int TotalCount { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
    }
}