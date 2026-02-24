using System.Text.Json;
using Core.DTOs;
using Core.DTOs.Cart;
using Core.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;

namespace Web.Controllers
{
    [Route("cart")]
    public class CartController : Controller
    {
        private readonly CartService _cartService;
        private readonly ILogger<CartController> _logger;
        private readonly IMemoryCache _cache;
        private const string SESSION_COOKIE_NAME = "CartSessionId";
        private const int SESSION_EXPIRY_DAYS = 7;

        public CartController(
            CartService cartService,
            ILogger<CartController> logger,
            IMemoryCache cache)
        {
            _cartService = cartService;
            _logger = logger;
            _cache = cache;
        }


        // GET: /cart
        [HttpGet("")]
        public async Task<IActionResult> Index()
        {
            try
            {
                var sessionId = GetOrCreateSessionId();
                var cart = await _cartService.GetOrCreateCartAsync(sessionId);

                // Validate cart items
                await _cartService.ValidateCartAsync(sessionId);

                ViewBag.CartSessionId = sessionId;
                return View(cart);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading cart page");
                return View(new CartDto());
            }
        }

        // Returns the mini cart for AJAX dropdown or modal

        [HttpGet("mini-partial")]
        public async Task<IActionResult> MiniCartPartial()
        {
            try 
            { 
                var sessionId = GetOrCreateSessionId();
                var cart = await _cartService.GetOrCreateCartAsync(sessionId);
                return PartialView("_MiniCartPartial", cart);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading mini cart partial");
                return PartialView("_MiniCartPartial", new CartDto());
            }
        }

        // Returns just the cart item count
        [HttpGet("cart-count-number")]
        public async Task<IActionResult> CartCountNumber()
        {
            try 
            { 
                var sessionId = GetOrCreateSessionId();
                var count = await _cartService.GetCartItemCountAsync(sessionId);
                return Content(count.ToString());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading cart count number");
                return Content("0");
            }
        }


        // POST: /cart/add-to-cart
        [HttpPost("add-to-cart")]
        // We remove ValidateAntiForgeryToken here if you are having issues with AJAX headers, 
        // otherwise ensure your JS sends the token.
        public async Task<IActionResult> AddToCart(AddToCartRequest request)
        {
            // If request comes from Index page, SelectedColor might be null. 
            // The service handles null color correctly (treats it as "no color selected").

            if (!ModelState.IsValid)
            {
                return Json(new { success = false, message = "Invalid input" });
            }

            try
            {
                var sessionId = GetOrCreateSessionId();
                await _cartService.AddItemToCartAsync(sessionId, request);

                _cache.Remove($"cart_summary_{sessionId}");

                // Get updated count
                var count = await _cartService.GetCartItemCountAsync(sessionId);

                return Json(new
                {
                    success = true,
                    message = "Item added to cart successfully!",
                    cartCount = count
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding to cart");
                return Json(new { success = false, message = ex.Message });
            }
        }

        // POST: /cart/update-quantity (From Cart Page - Form Submission)
        [HttpPost("update-quantity")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateQuantity([FromForm] UpdateCartItemRequest request)
        {
            if (!ModelState.IsValid)
            {
                TempData["Error"] = "Invalid quantity";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                var sessionId = GetOrCreateSessionId();
                await _cartService.UpdateItemQuantityAsync(sessionId, request);

                _cache.Remove($"cart_summary_{sessionId}");

                TempData["Success"] = "Cart updated successfully!";
                return RedirectToAction(nameof(Index));
            }
            catch (KeyNotFoundException ex)
            {
                TempData["Error"] = ex.Message;
                return RedirectToAction(nameof(Index));
            }
            catch (InvalidOperationException ex)
            {
                TempData["Error"] = ex.Message;
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating cart quantity");
                TempData["Error"] = "An error occurred while updating cart";
                return RedirectToAction(nameof(Index));
            }
        }


        // POST: /cart/remove-item (From Cart Page - Form Submission)
        [HttpPost("remove-item")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoveItem([FromForm] int cartItemId)
        {
            try
            {
                var sessionId = GetOrCreateSessionId();
                await _cartService.RemoveItemFromCartAsync(sessionId, cartItemId);

                _cache.Remove($"cart_summary_{sessionId}");

                TempData["Success"] = "Item removed from cart!";
                return RedirectToAction(nameof(Index));
            }
            catch (KeyNotFoundException ex)
            {
                TempData["Error"] = ex.Message;
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing item from cart");
                TempData["Error"] = "An error occurred while removing item";
                return RedirectToAction(nameof(Index));
            }
        }


        // POST: /cart/clear-cart (From Cart Page - Form Submission)
        [HttpPost("clear-cart")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ClearCart()
        {
            try
            {
                var sessionId = GetOrCreateSessionId();
                await _cartService.ClearCartAsync(sessionId);

                _cache.Remove($"cart_summary_{sessionId}");

                TempData["Success"] = "Cart cleared successfully!";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error clearing cart");
                TempData["Error"] = "An error occurred while clearing cart";
                return RedirectToAction(nameof(Index));
            }
        }


        // GET: /cart/cart-summary (For AJAX updates)

        [HttpGet("cart-summary")]
        public async Task<IActionResult> GetCartSummary()
        {
            try
            {
                var sessionId = GetOrCreateSessionId();
                var summary = await _cartService.GetCartSummaryAsync(sessionId);
                return Json(summary);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting cart summary");
                return Json(new CartSummaryDTO { IsEmpty = true });
            }
        }

        [HttpPost("apply-discount")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApplyDiscount([FromForm] string discountCode)
        {
            if (string.IsNullOrWhiteSpace(discountCode))
            {
                TempData["DiscountError"] = "Please enter a discount code";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                var sessionId = GetOrCreateSessionId();
                var cart = await _cartService.ApplyDiscountAsync(sessionId, discountCode);

                _cache.Remove($"cart_summary_{sessionId}");

                // Only show success message if discount was actually applied (amount > 0)
                if (cart.DiscountAmount > 0)
                {
                    TempData["Success"] = $"Discount '{cart.DiscountCode}' applied successfully! You saved EGP{cart.DiscountAmount:F2}";
                }
                else if (!string.IsNullOrEmpty(cart.DiscountCode))
                {
                    TempData["Warning"] = $"Discount code '{discountCode}' applied but no discount amount was calculated.";
                }

                return RedirectToAction(nameof(Index));
            }
            catch (InvalidOperationException ex)
            {
                // Show specific error message from validation
                // Don't set any success TempData here
                TempData["DiscountError"] = ex.Message;
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error applying discount");
                TempData["DiscountError"] = "An error occurred while applying discount";
                return RedirectToAction(nameof(Index));
            }
        }

        [HttpGet("remove-discount")] // Change from HttpPost to HttpGet
        public async Task<IActionResult> RemoveDiscount()
        {
            try
            {
                var sessionId = GetOrCreateSessionId();
                await _cartService.RemoveDiscountAsync(sessionId);

                _cache.Remove($"cart_summary_{sessionId}");
                TempData["Success"] = "Discount removed successfully!";

                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing discount");
                TempData["Error"] = "An error occurred while removing discount";
                return RedirectToAction(nameof(Index));
            }
        }


        #region Private Methods

        private string GetOrCreateSessionId()
        {
            var sessionId = Request.Cookies[SESSION_COOKIE_NAME];

            if (string.IsNullOrEmpty(sessionId))
            {
                sessionId = Guid.NewGuid().ToString();

                var cookieOptions = new CookieOptions
                {
                    HttpOnly = true,
                    Secure = Request.IsHttps,
                    SameSite = SameSiteMode.Lax,
                    Expires = DateTime.UtcNow.AddDays(SESSION_EXPIRY_DAYS),
                    IsEssential = true
                };

                Response.Cookies.Append(SESSION_COOKIE_NAME, sessionId, cookieOptions);
            }

            return sessionId;
        }

        #endregion
    }
}