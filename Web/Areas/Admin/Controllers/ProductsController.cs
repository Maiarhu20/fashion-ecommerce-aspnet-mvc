using Core.DTOs.Products;
using Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;

namespace Web.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class ProductsController : Controller
    {
        private readonly ProductService _productService;
        private readonly CategoryService _categoryService;
        private readonly ILogger<ProductsController> _logger;
        private readonly IWebHostEnvironment _webHostEnvironment;

        public ProductsController(
            ProductService productService,
            CategoryService categoryService,
            ILogger<ProductsController> logger,
            IWebHostEnvironment webHostEnvironment)
        {
            _productService = productService;
            _categoryService = categoryService;
            _logger = logger;
            _webHostEnvironment = webHostEnvironment;
        }

        // GET: Admin/Products
        //public async Task<IActionResult> Index()
        //{
        //    try
        //    {
        //        _logger.LogInformation("Loading products index page");
        //        var result = await _productService.GetAllAsync();

        //        if (!result.Succeeded)
        //        {
        //            TempData["ErrorMessage"] = result.ErrorMessage;
        //            return View(new List<ProductListDto>());
        //        }

        //        return View(result.Data);
        //    }
        //    catch (Exception ex)
        //    {
        //        _logger.LogError(ex, "Error loading products index page");
        //        TempData["ErrorMessage"] = "An unexpected error occurred while loading products.";
        //        return View(new List<ProductListDto>());
        //    }
        //}

        // Change it to:
        public async Task<IActionResult> Index(int? categoryId)
        {
            try
            {
                _logger.LogInformation("Loading products index page");

                ServiceResult<IEnumerable<ProductListDto>> result;

                if (categoryId.HasValue && categoryId > 0)
                {
                    // Filter by category
                    _logger.LogInformation("Filtering products by category ID: {CategoryId}", categoryId.Value);
                    result = await _productService.GetProductsByCategoryAsync(categoryId.Value);

                    // Store category info in ViewBag for display
                    var categoryService = _categoryService;
                    var categoryResult = await categoryService.GetByIdAsync(categoryId.Value);
                    if (categoryResult.Succeeded && categoryResult.Data != null)
                    {
                        ViewBag.CategoryName = categoryResult.Data.Name;
                        ViewBag.CategoryId = categoryId.Value;
                    }
                }
                else
                {
                    // Get all products
                    //result = await _productService.GetActiveProductsAsync(); // Changed from GetAllAsync()
                    result = await _productService.GetAllAsync();
                }

                if (!result.Succeeded)
                {
                    TempData["ErrorMessage"] = result.ErrorMessage;
                    return View(new List<ProductListDto>());
                }

                return View(result.Data);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading products index page");
                TempData["ErrorMessage"] = "An unexpected error occurred while loading products.";
                return View(new List<ProductListDto>());
            }
        }


        // GET: Admin/Products/Deleted
        public async Task<IActionResult> Deleted()
        {
            try
            {
                _logger.LogInformation("Loading deleted products page");
                var result = await _productService.GetDeletedProductsAsync();

                if (!result.Succeeded)
                {
                    TempData["ErrorMessage"] = result.ErrorMessage;
                    return View(new List<ProductListDto>());
                }

                return View(result.Data);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading deleted products page");
                TempData["ErrorMessage"] = "An unexpected error occurred while loading deleted products.";
                return View(new List<ProductListDto>());
            }
        }

        // GET: Admin/Products/Details/5
        public async Task<IActionResult> Details(int id)
        {
            try
            {
                _logger.LogInformation("Loading product details for ID: {ProductId}", id);

                if (id <= 0)
                {
                    TempData["ErrorMessage"] = "Invalid product ID.";
                    return RedirectToAction(nameof(Index));
                }

                var result = await _productService.GetByIdAsync(id);

                // Temporary logging to see what's returned
                if (result.Succeeded && result.Data != null)
                {
                    _logger.LogInformation("Product loaded successfully:");
                    _logger.LogInformation("  - Name: {Name}", result.Data.Name);
                    _logger.LogInformation("  - Image Count: {Count}", result.Data.Images?.Count ?? 0);
                    _logger.LogInformation("  - Color Count: {Count}", result.Data.Colors?.Count ?? 0);
                    _logger.LogInformation("  - Category: {Category}", result.Data.CategoryName);

                    if (result.Data.Images != null)
                    {
                        foreach (var img in result.Data.Images)
                        {
                            _logger.LogInformation("  - Image URL: {Url}", img.ImageUrl);
                        }
                    }
                }

                if (!result.Succeeded || result.Data == null)
                {
                    TempData["ErrorMessage"] = result.ErrorMessage ?? "Product not found.";
                    return RedirectToAction(nameof(Index));
                }

                return View(result.Data);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading product details for ID: {ProductId}", id);
                TempData["ErrorMessage"] = "An unexpected error occurred while loading product details.";
                return RedirectToAction(nameof(Index));
            }
        }

        // GET: Admin/Products/Create
        public async Task<IActionResult> Create()
        {
            await PopulateCategoriesViewBag();
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ProductCreateDto productDto)
        {
            try
            {
                _logger.LogInformation("Creating new product: {ProductName}", productDto.Name);

                if (!ModelState.IsValid)
                {
                    await PopulateCategoriesViewBag();
                    return View(productDto);
                }

                // DEBUG: Log what we're receiving
                _logger.LogInformation("Received - Images: {ImageCount}, Colors: {ColorCount}, ColorNames: {ColorNameCount}",
                    productDto.ImageFiles?.Count ?? 0,
                    productDto.ColorHexCodes?.Count ?? 0,
                    productDto.ColorNames?.Count ?? 0);

                // Handle multiple image upload
                List<string> imageUrls = new List<string>();
                if (productDto.ImageFiles != null && productDto.ImageFiles.Any(f => f != null && f.Length > 0))
                {
                    imageUrls = await SaveImagesAsync(productDto.ImageFiles.Where(f => f != null && f.Length > 0).ToList());
                    _logger.LogInformation("Successfully saved {Count} images", imageUrls.Count);

                    if (imageUrls.Any())
                    {
                        productDto.ImageUrls = imageUrls;
                    }
                    else
                    {
                        _logger.LogWarning("No images were saved successfully");
                        ModelState.AddModelError("ImageFiles", "Error uploading one or more images.");
                        await PopulateCategoriesViewBag();
                        return View(productDto);
                    }
                }
                else
                {
                    _logger.LogWarning("No valid image files provided");
                    ModelState.AddModelError("ImageFiles", "Please upload at least one product image.");
                    await PopulateCategoriesViewBag();
                    return View(productDto);
                }

                // Validate colors and color names
                if (productDto.ColorHexCodes == null || !productDto.ColorHexCodes.Any())
                {
                    _logger.LogWarning("No colors provided");
                    ModelState.AddModelError("", "Please select at least one color.");
                    await PopulateCategoriesViewBag();
                    return View(productDto);
                }

                // Ensure color names array exists and matches color hex codes count
                if (productDto.ColorNames == null)
                {
                    productDto.ColorNames = new List<string>();
                }

                // Fill missing color names with default names
                while (productDto.ColorNames.Count < productDto.ColorHexCodes.Count)
                {
                    var hex = productDto.ColorHexCodes[productDto.ColorNames.Count];
                    productDto.ColorNames.Add(GetDefaultColorName(hex));
                }

                // Log the final data being sent to service
                _logger.LogInformation("Sending to service - Colors: {ColorCount}, ColorNames: {ColorNameCount}",
                    productDto.ColorHexCodes.Count, productDto.ColorNames.Count);

                var result = await _productService.CreateAsync(productDto);
                if (!result.Succeeded)
                {
                    TempData["ErrorMessage"] = result.ErrorMessage;
                    await PopulateCategoriesViewBag();
                    return View(productDto);
                }

                TempData["SuccessMessage"] = "Product created successfully!";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating product");
                TempData["ErrorMessage"] = $"Error: {ex.Message}";
                await PopulateCategoriesViewBag();
                return View(productDto);
            }
        }

        // GET: Admin/Products/Edit/5
        public async Task<IActionResult> Edit(int id)
        {
            try
            {
                _logger.LogInformation("Loading product for edit with ID: {ProductId}", id);

                if (id <= 0)
                {
                    TempData["ErrorMessage"] = "Invalid product ID.";
                    return RedirectToAction(nameof(Index));
                }

                var result = await _productService.GetByIdAsync(id);

                if (!result.Succeeded || result.Data == null)
                {
                    TempData["ErrorMessage"] = result.ErrorMessage ?? "Product not found.";
                    return RedirectToAction(nameof(Index));
                }

                if (result.Data.IsDeleted)
                {
                    TempData["ErrorMessage"] = "Cannot edit a deleted product. Please restore it first.";
                    return RedirectToAction(nameof(Details), new { id });
                }

                var updateDto = new ProductUpdateDto
                {
                    Id = result.Data.Id,
                    Name = result.Data.Name,
                    Description = result.Data.Description,
                    Price = result.Data.Price,
                    StockQuantity = result.Data.StockQuantity,
                    CategoryId = result.Data.CategoryId,
                    DiscountPercent = result.Data.DiscountPercent,
                    ImageUrls = result.Data.Images.Select(img => img.ImageUrl).ToList(),
                    ColorHexCodes = result.Data.Colors.Select(c => c.ColorHexCode).ToList(),
                    ColorNames = result.Data.Colors.Select(c => c.ColorName).ToList()
                };

                await PopulateCategoriesViewBag();
                return View(updateDto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading product for edit with ID: {ProductId}", id);
                TempData["ErrorMessage"] = "An unexpected error occurred while loading the product for edit.";
                return RedirectToAction(nameof(Index));
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, ProductUpdateDto productDto)
        {
            try
            {
                _logger.LogInformation("Updating product with ID: {ProductId}", id);

                if (id != productDto.Id)
                {
                    TempData["ErrorMessage"] = "Product ID mismatch.";
                    return RedirectToAction(nameof(Index));
                }

                if (!ModelState.IsValid)
                {
                    await PopulateCategoriesViewBag();
                    return View(productDto);
                }

                // Get the current product to preserve existing data
                var currentProductResult = await _productService.GetByIdAsync(id);
                if (!currentProductResult.Succeeded || currentProductResult.Data == null)
                {
                    TempData["ErrorMessage"] = "Product not found.";
                    await PopulateCategoriesViewBag();
                    return View(productDto);
                }

                // Preserve existing images if ImageUrls is null or empty
                if (productDto.ImageUrls == null || !productDto.ImageUrls.Any())
                {
                    productDto.ImageUrls = currentProductResult.Data.Images.Select(img => img.ImageUrl).ToList();
                    _logger.LogInformation("Preserved {Count} existing images", productDto.ImageUrls.Count);
                }

                // Handle multiple image upload if new images provided
                if (productDto.ImageFiles != null && productDto.ImageFiles.Any(f => f != null && f.Length > 0))
                {
                    var newImageUrls = await SaveImagesAsync(productDto.ImageFiles.Where(f => f != null && f.Length > 0).ToList());
                    if (newImageUrls.Any())
                    {
                        // Combine existing URLs with new ones
                        var existingUrls = productDto.ImageUrls ?? new List<string>();
                        productDto.ImageUrls = existingUrls.Concat(newImageUrls).ToList();
                        _logger.LogInformation("Added {Count} new images, total: {Total}", newImageUrls.Count, productDto.ImageUrls.Count);
                    }
                }

                // Ensure we have colors and color names
                if (productDto.ColorHexCodes == null || !productDto.ColorHexCodes.Any())
                {
                    // Preserve existing colors if none selected
                    productDto.ColorHexCodes = currentProductResult.Data.Colors.Select(c => c.ColorHexCode).ToList();
                    productDto.ColorNames = currentProductResult.Data.Colors.Select(c => c.ColorName).ToList();
                    _logger.LogInformation("Preserved {Count} existing colors", productDto.ColorHexCodes.Count);
                }
                else
                {
                    // Ensure color names array exists and matches color hex codes count
                    if (productDto.ColorNames == null)
                    {
                        productDto.ColorNames = new List<string>();
                    }

                    // Fill missing color names with default names or existing names
                    while (productDto.ColorNames.Count < productDto.ColorHexCodes.Count)
                    {
                        var hex = productDto.ColorHexCodes[productDto.ColorNames.Count];

                        // Try to find existing color name from current product
                        var existingColor = currentProductResult.Data.Colors.FirstOrDefault(c => c.ColorHexCode == hex);
                        if (existingColor != null)
                        {
                            productDto.ColorNames.Add(existingColor.ColorName);
                        }
                        else
                        {
                            productDto.ColorNames.Add(GetDefaultColorName(hex));
                        }
                    }

                    // Log the data being sent to service
                    _logger.LogInformation("Updating - Colors: {ColorCount}, ColorNames: {ColorNameCount}",
                        productDto.ColorHexCodes.Count, productDto.ColorNames.Count);
                }

                var result = await _productService.UpdateAsync(productDto);
                if (!result.Succeeded)
                {
                    TempData["ErrorMessage"] = result.ErrorMessage;
                    await PopulateCategoriesViewBag();
                    return View(productDto);
                }

                // FIX: Verify primary image was set
                var updatedProduct = await _productService.GetByIdAsync(id);
                if (updatedProduct.Succeeded && updatedProduct.Data != null)
                {
                    var hasPrimary = updatedProduct.Data.Images.Any(img => img.IsPrimary);
                    if (!hasPrimary && updatedProduct.Data.Images.Any())
                    {
                        _logger.LogWarning("Product {ProductId} has no primary image after update. This should not happen.", id);
                        TempData["WarningMessage"] = "Note: Primary image was auto-selected.";
                    }
                }

                TempData["SuccessMessage"] = "Product updated successfully!";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating product");
                TempData["ErrorMessage"] = $"Error: {ex.Message}";
                await PopulateCategoriesViewBag();
                return View(productDto);
            }
        }

        // POST: Admin/Products/Delete/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                _logger.LogInformation("Admin attempting to delete product ID: {ProductId}", id);

                // First check if we can hard delete
                var checkResult = await _productService.CheckDeletionEligibilityAsync(id);

                if (!checkResult.Succeeded)
                {
                    TempData["ErrorMessage"] = checkResult.ErrorMessage;
                    return RedirectToAction(nameof(Index));
                }

                if (checkResult.Data.CanDelete)
                {
                    var deleteResult = await _productService.DeleteAsync(id);
                    if (deleteResult.Succeeded)
                    {
                        TempData["SuccessMessage"] = "Product permanently deleted successfully!";
                    }
                    else
                    {
                        TempData["ErrorMessage"] = deleteResult.ErrorMessage;
                    }
                }
                else
                {
                    var softDeleteResult = await _productService.SoftDeleteAsync(id);
                    if (softDeleteResult.Succeeded)
                    {
                        TempData["SuccessMessage"] = "Product archived successfully! It has been hidden from the store but order history is preserved.";
                    }
                    else
                    {
                        TempData["ErrorMessage"] = softDeleteResult.ErrorMessage;
                    }
                }

                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting product ID: {ProductId}", id);
                TempData["ErrorMessage"] = "An unexpected error occurred while deleting the product.";
                return RedirectToAction(nameof(Index));
            }
        }

        // POST: Admin/Products/Restore/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Restore(int id)
        {
            try
            {
                _logger.LogInformation("Restoring product with ID: {ProductId}", id);

                if (id <= 0)
                {
                    TempData["ErrorMessage"] = "Invalid product ID.";
                    return RedirectToAction(nameof(Index));
                }

                var result = await _productService.RestoreAsync(id);

                if (!result.Succeeded)
                {
                    TempData["ErrorMessage"] = result.ErrorMessage;
                }
                else
                {
                    TempData["SuccessMessage"] = "Product restored successfully!";
                }

                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error restoring product with ID: {ProductId}", id);
                TempData["ErrorMessage"] = "An unexpected error occurred while restoring the product.";
                return RedirectToAction(nameof(Index));
            }
        }

        // AJAX: Admin/Products/CheckDelete/5
        [HttpGet]
        public async Task<IActionResult> CheckDelete(int id)
        {
            try
            {
                _logger.LogInformation("Checking delete eligibility for product ID: {ProductId}", id);

                var result = await _productService.CheckDeletionEligibilityAsync(id);

                if (!result.Succeeded)
                {
                    return Json(new { success = false, message = result.ErrorMessage });
                }

                return Json(new
                {
                    success = true,
                    canDelete = result.Data.CanDelete,
                    message = result.Data.Message,
                    orderCount = result.Data.OrderCount,
                    blockingReasons = result.Data.BlockingReasons
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking delete eligibility for product ID: {ProductId}", id);
                return Json(new { success = false, message = "Error checking deletion eligibility." });
            }
        }

        private async Task PopulateCategoriesViewBag()
        {
            var categoriesResult = await _categoryService.GetAllAsync();
            if (categoriesResult.Succeeded)
            {
                ViewBag.Categories = new SelectList(categoriesResult.Data, "Id", "Name");
            }
            else
            {
                ViewBag.Categories = new SelectList(new List<SelectListItem>());
            }
        }

        private async Task<List<string>> SaveImagesAsync(List<IFormFile> imageFiles)
        {
            var savedUrls = new List<string>();

            foreach (var imageFile in imageFiles)
            {
                var imageUrl = await SaveSingleImageAsync(imageFile);
                if (!string.IsNullOrEmpty(imageUrl))
                {
                    savedUrls.Add(imageUrl);
                }
            }

            return savedUrls;
        }

        private async Task<string?> SaveSingleImageAsync(IFormFile imageFile)
        {
            try
            {
                if (imageFile == null || imageFile.Length == 0)
                    return null;

                var extension = Path.GetExtension(imageFile.FileName).ToLowerInvariant();
                var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };

                if (!allowedExtensions.Contains(extension))
                    return null;

                if (imageFile.Length > 5 * 1024 * 1024) // 5MB
                    return null;

                var webRoot = _webHostEnvironment.WebRootPath;
                if (string.IsNullOrEmpty(webRoot))
                {
                    webRoot = Path.Combine(_webHostEnvironment.ContentRootPath, "wwwroot");
                }

                var uploadsFolder = Path.Combine(webRoot, "images", "products");

                // Ensure directory exists
                if (!Directory.Exists(uploadsFolder))
                {
                    Directory.CreateDirectory(uploadsFolder);
                    _logger.LogInformation("Created directory: {Directory}", uploadsFolder);
                }

                var uniqueFileName = Guid.NewGuid().ToString() + extension;
                var filePath = Path.Combine(uploadsFolder, uniqueFileName);

                await using (var fs = new FileStream(filePath, FileMode.Create))
                {
                    await imageFile.CopyToAsync(fs);
                }

                _logger.LogInformation("Successfully saved image: {FilePath}", filePath);
                return $"/images/products/{uniqueFileName}";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving image");
                return null;
            }
        }

        // Helper method to get default color name
        private string GetDefaultColorName(string hexCode)
        {
            var colorMap = new Dictionary<string, string>
            {
                {"#FF0000", "Red"}, {"#00FF00", "Green"}, {"#0000FF", "Blue"},
                {"#FFFF00", "Yellow"}, {"#FF00FF", "Magenta"}, {"#00FFFF", "Cyan"},
                {"#000000", "Black"}, {"#FFFFFF", "White"}, {"#808080", "Gray"},
                {"#FFA500", "Orange"}, {"#800080", "Purple"}, {"#FFC0CB", "Pink"},
                {"#A52A2A", "Brown"}, {"#008000", "Dark Green"}, {"#000080", "Navy"}
            };

            var normalizedHex = hexCode.Trim().ToUpper();
            return colorMap.ContainsKey(normalizedHex) ? colorMap[normalizedHex] : "Custom Color";
        }
    }
}