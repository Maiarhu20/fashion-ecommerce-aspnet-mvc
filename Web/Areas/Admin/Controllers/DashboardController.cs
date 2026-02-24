using Core.DTOs.Orders;
using Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Web.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class DashboardController : Controller
    {
        private readonly AdminOrderService _adminOrderService;
        private readonly ProductService _productService;

        public DashboardController(
            AdminOrderService adminOrderService,
            ProductService productService)
        {
            _adminOrderService = adminOrderService;
            _productService = productService;
        }

        public async Task<IActionResult> Index()
        {
            try
            {
                // Get order statistics for the last 12 months
                var orderStatsResult = await _adminOrderService.GetOrderStatsAsync(
                    startDate: DateTime.UtcNow.AddMonths(-12),
                    endDate: DateTime.UtcNow
                );

                // Get product statistics
                var productsResult = await _productService.GetAllAsync();

                var viewModel = new DashboardViewModel
                {
                    OrderStats = orderStatsResult.Succeeded ? orderStatsResult.Data : null,
                    TotalProducts = productsResult.Succeeded ? productsResult.Data.Count() : 0
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                // Log the error and return the view with default values
                return View(new DashboardViewModel());
            }
        }
    }


    public class DashboardViewModel
    {
        public AdminOrderStatsDto? OrderStats { get; set; }
        public int TotalProducts { get; set; }
    }
}