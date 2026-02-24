using Core.DTOs.Products;
using Core.Services;
using Microsoft.AspNetCore.Mvc;

namespace Web.Controllers
{
    [Route("products")]
    public class ProductsController : Controller
    {
        private readonly ProductService _productService;
        private readonly CategoryService _categoryService;
        private readonly ILogger<ProductsController> _logger;

        public ProductsController(
            ProductService productService,
            CategoryService categoryService,
            ILogger<ProductsController> logger)
        {
            _productService = productService;
            _categoryService = categoryService;
            _logger = logger;
        }

        // GET: /products/all
        [HttpGet("all")]
        public async Task<IActionResult> AllProducts()
        {
            try
            {
                var result = await _productService.GetActiveProductsAsync();

                if (!result.Succeeded)
                {
                    ViewBag.ErrorMessage = result.ErrorMessage;
                    return View("Index", new List<ProductListDto>());
                }

                ViewBag.Title = "All Products";
                ViewBag.ViewType = "all";
                return View("Index", result.Data);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading all products");
                ViewBag.ErrorMessage = "Unable to load products. Please try again later.";
                return View("Index", new List<ProductListDto>());
            }
        }

        // GET: /products/category/{id}
        [HttpGet("category/{id}")]
        public async Task<IActionResult> ProductsByCategory(int id)
        {
            try
            {
                // Get category info
                var categoryResult = await _categoryService.GetByIdAsync(id);
                if (!categoryResult.Succeeded || categoryResult.Data == null || categoryResult.Data.IsDeleted)
                {
                    ViewBag.ErrorMessage = "Category not found.";
                    return View("Index", new List<ProductListDto>());
                }

                // Get products for this category
                var productsResult = await _productService.GetProductsByCategoryAsync(id);

                if (!productsResult.Succeeded)
                {
                    ViewBag.ErrorMessage = productsResult.ErrorMessage;
                    return View("Index", new List<ProductListDto>());
                }

                ViewBag.Title = categoryResult.Data.Name;
                ViewBag.Category = categoryResult.Data;
                ViewBag.ViewType = "category";
                ViewBag.CategoryId = id;

                return View("Index", productsResult.Data);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading products for category {CategoryId}", id);
                ViewBag.ErrorMessage = "Unable to load products. Please try again later.";
                return View("Index", new List<ProductListDto>());
            }
        }

        // GET: /products/details/{id}
        [HttpGet("details/{id}")]
        public async Task<IActionResult> Details(int id)
        {
            try
            {
                var result = await _productService.GetByIdAsync(id);

                if (!result.Succeeded || result.Data == null || result.Data.IsDeleted)
                {
                    TempData["ErrorMessage"] = "Product not found.";
                    return RedirectToAction("AllProducts");
                }

                ViewBag.Title = result.Data.Name;
                return View("Details", result.Data);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading product details {ProductId}", id);
                TempData["ErrorMessage"] = "Unable to load product details.";
                return RedirectToAction("AllProducts");
            }
        }

        /// <summary>
        /// Search for products and categories
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> Search(string query)
        {
            if (string.IsNullOrWhiteSpace(query) || query.Trim().Length < 2)
            {
                return Json(new { products = new List<object>(), categories = new List<object>() });
            }

            var searchTerm = query.Trim().ToLower();

            // Search products
            var productsResult = await _productService.GetActiveProductsAsync();
            var matchingProducts = new List<object>();

            if (productsResult.Succeeded)
            {
                matchingProducts = productsResult.Data
                    .Where(p => p.Name.ToLower().Contains(searchTerm) ||
                               (p.Description != null && p.Description.ToLower().Contains(searchTerm)) ||
                               (p.CategoryName != null && p.CategoryName.ToLower().Contains(searchTerm)))
                    .Take(5) // Limit to 5 products
                    .Select(p => new
                    {
                        id = p.Id,
                        name = p.Name,
                        price = p.Price,
                        finalPrice = p.FinalPrice,
                        discountPercent = p.DiscountPercent ?? 0,
                        primaryImageUrl = p.PrimaryImageUrl,
                        categoryName = p.CategoryName
                    })
                    .ToList<object>();
            }

            // Search categories
            var categoriesResult = await _categoryService.GetAllAsync();
            var matchingCategories = new List<object>();

            if (categoriesResult.Succeeded)
            {
                matchingCategories = categoriesResult.Data
                    .Where(c => c.Name.ToLower().Contains(searchTerm) ||
                               (c.Description != null && c.Description.ToLower().Contains(searchTerm)))
                    .Take(3) // Limit to 3 categories
                    .Select(c => new
                    {
                        id = c.Id,
                        name = c.Name,
                        productCount = c.ProductCount
                    })
                    .ToList<object>();
            }

            return Json(new
            {
                products = matchingProducts,
                categories = matchingCategories
            });
        }
    }
}
