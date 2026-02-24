using Core.DTOs.Shipping;
using Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Web.Controllers.Admin
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    [Route("admin/shipping")]
    public class ShippingController : Controller
    {
        private readonly ShippingService _shippingService;
        private readonly ILogger<ShippingController> _logger;

        public ShippingController(
            ShippingService shippingService,
            ILogger<ShippingController> logger)
        {
            _shippingService = shippingService;
            _logger = logger;
        }

        [HttpGet("cities")]
        public async Task<IActionResult> ManageCities()
        {
            var result = await _shippingService.GetAllShippingCitiesAsync();

            if (!result.Succeeded)
            {
                TempData["Error"] = result.ErrorMessage;
                return View(new List<ShippingCityDto>());
            }

            return View(result.Data);
        }

        [HttpGet("cities/create")]
        public IActionResult CreateCity()
        {
            return View(new CreateShippingCityDto());
        }

        [HttpPost("cities/create")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateCity(CreateShippingCityDto createDto)
        {
            if (!ModelState.IsValid)
            {
                return View(createDto);
            }

            var result = await _shippingService.CreateShippingCityAsync(createDto);

            if (result.Succeeded)
            {
                TempData["Success"] = "Shipping city added successfully!";
                return RedirectToAction(nameof(ManageCities));
            }

            ModelState.AddModelError("", result.ErrorMessage);
            return View(createDto);
        }

        [HttpGet("cities/edit/{id}")]
        public async Task<IActionResult> EditCity(int id)
        {
            var result = await _shippingService.GetShippingCityByIdAsync(id);

            if (!result.Succeeded)
            {
                TempData["Error"] = result.ErrorMessage;
                return RedirectToAction(nameof(ManageCities));
            }

            var updateDto = new UpdateShippingCityDto
            {
                Id = result.Data.Id,
                CityName = result.Data.CityName,
                ShippingCost = result.Data.ShippingCost,
                IsActive = result.Data.IsActive
            };

            return View(updateDto);
        }

        [HttpPost("cities/edit/{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditCity(int id, UpdateShippingCityDto updateDto)
        {
            if (id != updateDto.Id)
            {
                return NotFound();
            }

            if (!ModelState.IsValid)
            {
                return View(updateDto);
            }

            var result = await _shippingService.UpdateShippingCityAsync(updateDto);

            if (result.Succeeded)
            {
                TempData["Success"] = "Shipping city updated successfully!";
                return RedirectToAction(nameof(ManageCities));
            }

            ModelState.AddModelError("", result.ErrorMessage);
            return View(updateDto);
        }

        [HttpPost("cities/toggle-status/{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleCityStatus(int id)
        {
            var result = await _shippingService.ToggleShippingCityStatusAsync(id);

            if (result.Succeeded)
            {
                TempData["Success"] = "City status updated successfully!";
            }
            else
            {
                TempData["Error"] = result.ErrorMessage;
            }

            return RedirectToAction(nameof(ManageCities));
        }

        [HttpPost("cities/delete/{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteCity(int id)
        {
            var result = await _shippingService.DeleteShippingCityAsync(id);

            if (result.Succeeded)
            {
                TempData["Success"] = "Shipping city deleted successfully!";
            }
            else
            {
                TempData["Error"] = result.ErrorMessage;
            }

            return RedirectToAction(nameof(ManageCities));
        }
    }
}