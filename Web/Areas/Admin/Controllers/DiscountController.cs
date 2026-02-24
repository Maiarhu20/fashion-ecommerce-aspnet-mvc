using Core.DTOs.Discount;
using Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Web.Controllers.Admin
{
    [Area("Admin")] 
    [Authorize(Roles = "Admin")]
    //[Route("admin/discounts")]
    public class DiscountController : Controller
    {
        private readonly DiscountService _discountService;
        private readonly ILogger<DiscountController> _logger;

        public DiscountController(
            DiscountService discountService,
            ILogger<DiscountController> logger)
        {
            _discountService = discountService;
            _logger = logger;
        }

        // GET: /admin/discounts
        //[HttpGet]
        public async Task<IActionResult> Index()
        {
            try
            {
                var discounts = await _discountService.GetAllDiscountsAsync();
                return View(discounts);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading discounts index");
                TempData["Error"] = "Error loading discounts";
                return View(new List<DiscountListDto>());
            }
        }

        // GET: /Admin/Discount/Create
        public IActionResult Create()
        {
            ViewBag.DiscountTypes = new List<SelectListItem>
            {
                new SelectListItem { Value = "Percentage", Text = "Percentage (%)" },
                new SelectListItem { Value = "FixedAmount", Text = "Fixed Amount (EGP)" }
            };

            // Initialize with current time but NO seconds
            var now = DateTime.UtcNow;
            var startDate = new DateTime(now.Year, now.Month, now.Day, now.Hour, now.Minute, 0);

            return View(new CreateDiscountDto
            {
                StartDate = startDate,
                IsActive = true
            });
        }

        // POST: /Admin/Discount/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CreateDiscountDto dto)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    ViewBag.DiscountTypes = new List<SelectListItem>
                    {
                        new SelectListItem { Value = "Percentage", Text = "Percentage (%)" },
                        new SelectListItem { Value = "FixedAmount", Text = "Fixed Amount (EGP)" }
                    };
                    return View(dto);
                }

                // ⭐ STRIP SECONDS/MILLISECONDS (if any sneaked in)
                dto.StartDate = dto.StartDate.AddSeconds(-dto.StartDate.Second);
                if (dto.ExpiryDate.HasValue)
                {
                    var expiry = dto.ExpiryDate.Value;
                    dto.ExpiryDate = expiry.AddSeconds(-expiry.Second);
                }

                var discount = await _discountService.CreateDiscountAsync(dto);

                TempData["Success"] = $"Discount code '{discount.Code}' created successfully!";
                return RedirectToAction(nameof(Index));
            }
            catch (InvalidOperationException ex)
            {
                TempData["Error"] = ex.Message;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating discount");
                TempData["Error"] = "Error creating discount";
            }

            // If we got this far, redisplay form with errors
            ViewBag.DiscountTypes = new List<SelectListItem>
            {
                new SelectListItem { Value = "Percentage", Text = "Percentage (%)" },
                new SelectListItem { Value = "FixedAmount", Text = "Fixed Amount (EGP)" }
            };
            return View(dto);
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            try
            {
                var discount = await _discountService.GetDiscountByIdAsync(id);

                ViewBag.DiscountTypes = new List<SelectListItem>
                {
                    new SelectListItem { Value = "Percentage", Text = "Percentage (%)", Selected = discount.DiscountType == "Percentage" },
                    new SelectListItem { Value = "FixedAmount", Text = "Fixed Amount (EGP)", Selected = discount.DiscountType == "FixedAmount" }
                };

                var updateDto = new UpdateDiscountDto
                {
                    Id = discount.Id,
                    Code = discount.Code,
                    Description = discount.Description,
                    DiscountValue = discount.DiscountValue,
                    MinimumOrderAmount = discount.MinimumOrderAmount,
                    UsageLimitPerGuest = discount.UsageLimitPerGuest,
                    StartDate = discount.StartDate,
                    ExpiryDate = discount.ExpiryDate,
                    IsActive = discount.IsActive
                };

                return View(updateDto);
            }
            catch (KeyNotFoundException)
            {
                TempData["Error"] = "Discount not found";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading discount for edit");
                TempData["Error"] = "Error loading discount";
                return RedirectToAction(nameof(Index));
            }
        }

        // POST: /admin/discounts/edit/{id}
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, UpdateDiscountDto dto)
        {
            try
            {
                if (id != dto.Id)
                {
                    TempData["Error"] = "Invalid discount ID";
                    return RedirectToAction(nameof(Index));
                }

                if (!ModelState.IsValid)
                {
                    ViewBag.DiscountTypes = new List<SelectListItem>
                    {
                        new SelectListItem { Value = "Percentage", Text = "Percentage (%)" },
                        new SelectListItem { Value = "FixedAmount", Text = "Fixed Amount (EGP)" }
                    };
                    return View(dto);
                }

                var discount = await _discountService.UpdateDiscountAsync(dto);

                TempData["Success"] = $"Discount code '{discount.Code}' updated successfully!";
                return RedirectToAction(nameof(Index));
            }
            catch (KeyNotFoundException)
            {
                TempData["Error"] = "Discount not found";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating discount");
                TempData["Error"] = "Error updating discount";
                ViewBag.DiscountTypes = new List<SelectListItem>
                {
                    new SelectListItem { Value = "Percentage", Text = "Percentage (%)" },
                    new SelectListItem { Value = "FixedAmount", Text = "Fixed Amount (EGP)" }
                };
                return View(dto);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                var success = await _discountService.DeleteDiscountAsync(id);

                if (success)
                    TempData["Success"] = "Discount deleted successfully!";
                else
                    TempData["Error"] = "Failed to delete discount";

                return RedirectToAction(nameof(Index));
            }
            catch (KeyNotFoundException)
            {
                TempData["Error"] = "Discount not found";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting discount");
                TempData["Error"] = "Error deleting discount";
                return RedirectToAction(nameof(Index));
            }
        }

        // POST: /admin/discounts/toggle/{id}
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleStatus(int id)
        {
            try
            {
                var isActive = await _discountService.ToggleDiscountStatusAsync(id);

                TempData["Success"] = $"Discount {(isActive ? "activated" : "deactivated")} successfully!";
                return RedirectToAction(nameof(Index));
            }
            catch (KeyNotFoundException)
            {
                TempData["Error"] = "Discount not found";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error toggling discount status");
                TempData["Error"] = "Error updating discount status";
                return RedirectToAction(nameof(Index));
            }
        }

        // GET: /admin/discounts/stats
        [HttpGet]
        public async Task<IActionResult> Stats()
        {
            try
            {
                var stats = await _discountService.GetDiscountStatsAsync();
                return View(stats);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading discount stats");
                TempData["Error"] = "Error loading statistics";
                return View(new DiscountStatsDto());
            }
        }

        // GET: /admin/discounts/validate/{code}
        [HttpGet]
        public async Task<IActionResult> Validate(string code)
        {
            try
            {
                var isValid = await _discountService.ValidateDiscountCodeAsync(code);

                return Json(new { isValid });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating discount code");
                return Json(new { isValid = false });
            }
        }
    }
}