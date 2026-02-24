// Web/Areas/Admin/Controllers/CategoriesController.cs
using Core.DTOs;
using Core.DTOs.Categories;
using Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.IO;

namespace Web.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class CategoriesController : Controller
    {
        private readonly CategoryService _categoryService;
        private readonly ILogger<CategoriesController> _logger;
        private readonly IWebHostEnvironment _webHostEnvironment;

        public CategoriesController(
            CategoryService categoryService,
            ILogger<CategoriesController> logger,
            IWebHostEnvironment webHostEnvironment)
        {
            _categoryService = categoryService ?? throw new ArgumentNullException(nameof(categoryService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _webHostEnvironment = webHostEnvironment ?? throw new ArgumentNullException(nameof(webHostEnvironment));

            _logger.LogInformation("✅ CategoriesController instantiated successfully");
        }


        // GET: Admin/Categories
        public async Task<IActionResult> Index()
        {
            try
            {
                _logger.LogInformation("📋 Loading categories index page");

                var result = await _categoryService.GetAllAsync();

                if (!result.Succeeded)
                {
                    _logger.LogWarning("⚠️ Failed to load categories: {ErrorMessage}", result.ErrorMessage);
                    TempData["ErrorMessage"] = result.ErrorMessage;
                    return View(new List<CategoryListDto>());
                }

                _logger.LogInformation("✅ Loaded {Count} categories", result.Data?.Count() ?? 0);
                return View(result.Data);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error loading categories index page");
                TempData["ErrorMessage"] = "An unexpected error occurred while loading categories.";
                return View(new List<CategoryListDto>());
            }
        }


        // GET: Admin/Categories/Deleted
        public async Task<IActionResult> Deleted()
        {
            try
            {
                _logger.LogInformation("Loading deleted categories page");

                var result = await _categoryService.GetDeletedCategoriesAsync();

                if (!result.Succeeded)
                {
                    _logger.LogWarning("Failed to load deleted categories: {ErrorMessage}", result.ErrorMessage);
                    TempData["ErrorMessage"] = result.ErrorMessage;
                    return View(new List<CategoryListDto>());
                }

                return View(result.Data);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error loading deleted categories page");
                TempData["ErrorMessage"] = "An unexpected error occurred while loading deleted categories.";
                return View(new List<CategoryListDto>());
            }
        }

        // GET: Admin/Categories/Details/5
        public async Task<IActionResult> Details(int id)
        {
            try
            {
                _logger.LogInformation("Loading category details for ID: {CategoryId}", id);

                if (id <= 0)
                {
                    _logger.LogWarning("Invalid category ID for details: {CategoryId}", id);
                    TempData["ErrorMessage"] = "Invalid category ID.";
                    return RedirectToAction(nameof(Index));
                }

                var result = await _categoryService.GetByIdAsync(id);

                if (!result.Succeeded)
                {
                    _logger.LogWarning("Failed to load category details for ID {CategoryId}: {ErrorMessage}",
                        id, result.ErrorMessage);
                    TempData["ErrorMessage"] = result.ErrorMessage;
                    return RedirectToAction(nameof(Index));
                }

                if (result.Data == null)
                {
                    _logger.LogWarning("Category not found for details with ID: {CategoryId}", id);
                    TempData["ErrorMessage"] = "Category not found.";
                    return RedirectToAction(nameof(Index));
                }

                return View(result.Data);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error loading category details for ID: {CategoryId}", id);
                TempData["ErrorMessage"] = "An unexpected error occurred while loading category details.";
                return RedirectToAction(nameof(Index));
            }
        }

        // GET: Admin/Categories/Create
        [HttpGet]
        public IActionResult Create()
        {
            try
            {
                _logger.LogInformation("📝 Loading Create category page");
                return View();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error loading Create page");
                TempData["ErrorMessage"] = $"Error: {ex.Message}";
                return RedirectToAction(nameof(Index));
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(string Name, string Description, IFormFile ImageFile)
        {
            try
            {
                _logger.LogInformation("=== CREATE CATEGORY START ===");

                // === Step 1: Basic Validation ===
                if (string.IsNullOrWhiteSpace(Name))
                {
                    TempData["ErrorMessage"] = "Category name is required.";
                    return View();
                }

                if (ImageFile == null || ImageFile.Length == 0)
                {
                    TempData["ErrorMessage"] = "Please select an image file.";
                    return View();
                }

                var extension = Path.GetExtension(ImageFile.FileName).ToLowerInvariant();
                var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
                if (!allowedExtensions.Contains(extension))
                {
                    TempData["ErrorMessage"] = "Only image files are allowed (JPG, PNG, GIF, WebP).";
                    return View();
                }

                if (ImageFile.Length > 5 * 1024 * 1024)
                {
                    TempData["ErrorMessage"] = "File size must be less than 5MB.";
                    return View();
                }

                // === Step 2: Ensure Upload Folder Exists ===
                var webRoot = _webHostEnvironment.WebRootPath;
                if (string.IsNullOrEmpty(webRoot))
                {
                    webRoot = Path.Combine(_webHostEnvironment.ContentRootPath, "wwwroot");
                }

                var uploadsFolder = Path.Combine(webRoot, "images", "categories");
                Directory.CreateDirectory(uploadsFolder); // Safe to call even if exists

                // === Step 3: Save File Safely ===
                var uniqueFileName = Guid.NewGuid().ToString() + extension;
                var filePath = Path.Combine(uploadsFolder, uniqueFileName);

                await using (var fs = new FileStream(filePath, FileMode.Create))
                {
                    await ImageFile.CopyToAsync(fs);
                }

                // === Step 4: Create DTO and Save ===
                var dto = new CategoryCreateDto
                {
                    Name = Name.Trim(),
                    Description = Description?.Trim() ?? string.Empty,
                    ImageUrl = $"/images/categories/{uniqueFileName}"
                };

                var result = await _categoryService.CreateAsync(dto);
                if (!result.Succeeded)
                {
                    if (System.IO.File.Exists(filePath))
                        System.IO.File.Delete(filePath);

                    TempData["ErrorMessage"] = result.ErrorMessage ?? "Failed to create category.";
                    return View();
                }

                TempData["SuccessMessage"] = "Category created successfully!";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "💥 Fatal error in Create");
                TempData["ErrorMessage"] = $"Error: {ex.Message}";
                return View();
            }
        }

        // GET: Admin/Categories/Edit/5
        public async Task<IActionResult> Edit(int id)
        {
            try
            {
                _logger.LogInformation("Loading category for edit with ID: {CategoryId}", id);

                if (id <= 0)
                {
                    _logger.LogWarning("Invalid category ID for edit: {CategoryId}", id);
                    TempData["ErrorMessage"] = "Invalid category ID.";
                    return RedirectToAction(nameof(Index));
                }

                var result = await _categoryService.GetByIdAsync(id);

                if (!result.Succeeded || result.Data == null)
                {
                    _logger.LogWarning("Category not found for edit with ID: {CategoryId}", id);
                    TempData["ErrorMessage"] = result.ErrorMessage ?? "Category not found.";
                    return RedirectToAction(nameof(Index));
                }

                if (result.Data.IsDeleted)
                {
                    TempData["ErrorMessage"] = "Cannot edit a deleted category. Please restore it first.";
                    return RedirectToAction(nameof(Details), new { id });
                }

                var updateDto = new CategoryUpdateDto
                {
                    Id = result.Data.Id,
                    Name = result.Data.Name,
                    Description = result.Data.Description,
                    ImageUrl = result.Data.ImageUrl
                };

                return View(updateDto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error loading category for edit with ID: {CategoryId}", id);
                TempData["ErrorMessage"] = "An unexpected error occurred while loading the category for edit.";
                return RedirectToAction(nameof(Index));
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, CategoryUpdateDto categoryDto)
        {
            try
            {
                _logger.LogInformation("Updating category with ID: {CategoryId}", id);

                if (id != categoryDto.Id)
                {
                    TempData["ErrorMessage"] = "Category ID mismatch.";
                    return RedirectToAction(nameof(Index));
                }

                // Validate and process file if new image uploaded
                if (categoryDto.ImageFile != null && categoryDto.ImageFile.Length > 0)
                {
                    var ext = Path.GetExtension(categoryDto.ImageFile.FileName).ToLowerInvariant();
                    var allowed = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };

                    if (!allowed.Contains(ext))
                    {
                        TempData["ErrorMessage"] = "Invalid image format.";
                        return View(categoryDto);
                    }

                    if (categoryDto.ImageFile.Length > 5 * 1024 * 1024)
                    {
                        TempData["ErrorMessage"] = "File too large (max 5MB).";
                        return View(categoryDto);
                    }

                    var webRoot = _webHostEnvironment.WebRootPath ??
                        Path.Combine(_webHostEnvironment.ContentRootPath, "wwwroot");

                    var folder = Path.Combine(webRoot, "images", "categories");
                    Directory.CreateDirectory(folder);

                    var fileName = Guid.NewGuid().ToString() + ext;
                    var filePath = Path.Combine(folder, fileName);

                    await using (var fs = new FileStream(filePath, FileMode.Create))
                    {
                        await categoryDto.ImageFile.CopyToAsync(fs);
                    }

                    // Delete old image if exists
                    if (!string.IsNullOrEmpty(categoryDto.ImageUrl))
                    {
                        var oldPath = Path.Combine(webRoot, categoryDto.ImageUrl.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
                        if (System.IO.File.Exists(oldPath))
                            System.IO.File.Delete(oldPath);
                    }

                    // Update to new path
                    categoryDto.ImageUrl = $"/images/categories/{fileName}";
                }

                // If no new image uploaded, keep existing one
                var result = await _categoryService.UpdateAsync(categoryDto);
                if (!result.Succeeded)
                {
                    TempData["ErrorMessage"] = result.ErrorMessage;
                    return View(categoryDto);
                }

                TempData["SuccessMessage"] = "Category updated successfully!";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating category");
                TempData["ErrorMessage"] = $"Error: {ex.Message}";
                return View(categoryDto);
            }
        }

        // POST: Admin/Categories/Delete/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                _logger.LogInformation("Admin attempting to delete category ID: {CategoryId}", id);

                // First check if we can delete
                var checkResult = await _categoryService.CheckDeletionEligibilityAsync(id);

                if (!checkResult.Succeeded)
                {
                    TempData["ErrorMessage"] = checkResult.ErrorMessage;
                    return RedirectToAction(nameof(Index));
                }

                if (!checkResult.Data.CanDelete)
                {
                    // Show detailed error message
                    var errorMsg = $"Cannot delete category. {string.Join(" ", checkResult.Data.BlockingReasons)}";
                    TempData["ErrorMessage"] = errorMsg;

                    // Optionally redirect to a details page with more options
                    return RedirectToAction(nameof(Details), new { id });
                }

                // Proceed with deletion
                var deleteResult = await _categoryService.DeleteAsync(id);

                if (!deleteResult.Succeeded)
                {
                    TempData["ErrorMessage"] = deleteResult.ErrorMessage;
                }
                else
                {
                    TempData["SuccessMessage"] = "Category deleted successfully!";
                }

                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error deleting category ID: {CategoryId}", id);
                TempData["ErrorMessage"] = "An unexpected error occurred while deleting the category.";
                return RedirectToAction(nameof(Index));
            }
        }

        // POST: Admin/Categories/Restore/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Restore(int id)
        {
            try
            {
                _logger.LogInformation("Restoring category with ID: {CategoryId}", id);

                if (id <= 0)
                {
                    _logger.LogWarning("Invalid category ID for restoration: {CategoryId}", id);
                    TempData["ErrorMessage"] = "Invalid category ID.";
                    return RedirectToAction(nameof(Index));
                }

                var result = await _categoryService.RestoreAsync(id);

                if (!result.Succeeded)
                {
                    _logger.LogWarning("Failed to restore category with ID {CategoryId}: {ErrorMessage}",
                        id, result.ErrorMessage);

                    TempData["ErrorMessage"] = result.ErrorMessage;
                }
                else
                {
                    _logger.LogInformation("Successfully restored category with ID: {CategoryId}", id);
                    TempData["SuccessMessage"] = "Category restored successfully!";
                }

                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error restoring category with ID: {CategoryId}", id);
                TempData["ErrorMessage"] = "An unexpected error occurred while restoring the category.";
                return RedirectToAction(nameof(Index));
            }
        }

        // POST: Admin/Categories/Archive/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Archive(int id)
        {
            try
            {
                _logger.LogInformation("Archiving category ID: {CategoryId}", id);

                var result = await _categoryService.ArchiveCategoryAsync(id);

                if (!result.Succeeded)
                {
                    TempData["ErrorMessage"] = result.ErrorMessage;
                }
                else
                {
                    TempData["SuccessMessage"] = "Category archived successfully. Products have been hidden from catalog.";
                }

                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error archiving category ID: {CategoryId}", id);
                TempData["ErrorMessage"] = "An error occurred while archiving the category.";
                return RedirectToAction(nameof(Index));
            }
        }

        // AJAX: Admin/Categories/CheckDelete/5
        [HttpGet]
        public async Task<IActionResult> CheckDelete(int id)
        {
            try
            {
                _logger.LogInformation("Checking delete eligibility for category ID: {CategoryId}", id);

                var result = await _categoryService.CheckDeletionEligibilityAsync(id);

                if (!result.Succeeded)
                {
                    return Json(new { success = false, message = result.ErrorMessage });
                }

                return Json(new
                {
                    success = true,
                    canDelete = result.Data.CanDelete,
                    message = result.Data.Message,
                    productCount = result.Data.ProductCount,
                    orderCount = result.Data.ActiveOrderCount,
                    blockingReasons = result.Data.BlockingReasons
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking delete eligibility for category ID: {CategoryId}", id);
                return Json(new { success = false, message = "Error checking deletion eligibility." });
            }
        }
    }
}