using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Core.Services;
using Core.DTOs.HomeMedia;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Web.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class HomeMediaController : Controller
    {
        private readonly HomeMediaService _homeMediaService;

        public HomeMediaController(HomeMediaService homeMediaService)
        {
            _homeMediaService = homeMediaService;
        }

        public async Task<IActionResult> Index()
        {
            var result = await _homeMediaService.GetAllAsync();
            if (!result.Succeeded)
            {
                TempData["Error"] = result.ErrorMessage;
                return View(new List<HomeMediaListDto>());
            }

            return View(result.Data);
        }

        public IActionResult Create()
        {
            ViewBag.MediaTypes = new SelectList(new[]
            {
                new { Value = "Image", Text = "Image" },
                new { Value = "Video", Text = "Video" }
            }, "Value", "Text");

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CreateHomeMediaDto dto)
        {
            if (ModelState.IsValid)
            {
                var result = await _homeMediaService.CreateAsync(dto);
                if (result.Succeeded)
                {
                    TempData["Success"] = "Home media created successfully";
                    return RedirectToAction(nameof(Index));
                }
                ModelState.AddModelError(string.Empty, result.ErrorMessage);
            }

            ViewBag.MediaTypes = new SelectList(new[]
            {
                new { Value = "Image", Text = "Image" },
                new { Value = "Video", Text = "Video" }
            }, "Value", "Text", dto.MediaType);

            return View(dto);
        }

        public async Task<IActionResult> Edit(int id)
        {
            var result = await _homeMediaService.GetByIdAsync(id);
            if (!result.Succeeded)
            {
                TempData["Error"] = result.ErrorMessage;
                return RedirectToAction(nameof(Index));
            }

            ViewBag.MediaTypes = new SelectList(new[]
            {
                new { Value = "Image", Text = "Image" },
                new { Value = "Video", Text = "Video" }
            }, "Value", "Text", result.Data?.MediaType);

            var updateDto = new UpdateHomeMediaDto
            {
                Title = result.Data!.Title,
                Description = result.Data.Description,
                CurrentMediaUrl = result.Data.MediaUrl,
                MediaType = result.Data.MediaType,
                ButtonText = result.Data.ButtonText,
                ButtonLink = result.Data.ButtonLink,
                DisplayOrder = result.Data.DisplayOrder,
                IsActive = result.Data.IsActive
            };

            ViewBag.CurrentMediaUrl = result.Data.MediaUrl;
            return View(updateDto);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, UpdateHomeMediaDto dto)
        {
            if (ModelState.IsValid)
            {
                var result = await _homeMediaService.UpdateAsync(id, dto);
                if (result.Succeeded)
                {
                    TempData["Success"] = "Home media updated successfully";
                    return RedirectToAction(nameof(Index));
                }
                ModelState.AddModelError(string.Empty, result.ErrorMessage);
            }

            ViewBag.MediaTypes = new SelectList(new[]
            {
                new { Value = "Image", Text = "Image" },
                new { Value = "Video", Text = "Video" }
            }, "Value", "Text", dto.MediaType);

            return View(dto);
        }

        [HttpPost]
        public async Task<IActionResult> ToggleStatus(int id)
        {
            var result = await _homeMediaService.ToggleStatusAsync(id);
            if (result.Succeeded)
            {
                var media = await _homeMediaService.GetByIdAsync(id);
                var status = media.Succeeded && media.Data?.IsActive == true ? "activated" : "deactivated";
                return Json(new { success = true, message = $"Home media {status} successfully" });
            }

            return Json(new { success = false, message = result.ErrorMessage });
        }

        [HttpPost]
        public async Task<IActionResult> Delete(int id)
        {
            var result = await _homeMediaService.DeleteAsync(id);
            return Json(new { success = result.Succeeded, message = result.Succeeded ? "Home media deleted successfully" : result.ErrorMessage });
        }
    }
}