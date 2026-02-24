using Core.DTOs;
using Core.DTOs.Cart;
using Domain.Models;
using Infrastructure.Data;
using Infrastructure.Interfaces;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Core.Services
{
    public class CartService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<CartService> _logger;
        private const int MAX_QUANTITY_PER_ITEM = 50;

        public CartService(IUnitOfWork unitOfWork, ILogger<CartService> logger)
        {
            _unitOfWork = unitOfWork;
            _logger = logger;
        }

        public async Task<CartDto> GetOrCreateCartAsync(string sessionId)
        {
            try
            {
                var cart = await _unitOfWork.Carts
                    .FindAsync(c => c.SessionId == sessionId);

                var existingCart = cart.FirstOrDefault();

                if (existingCart == null)
                {
                    // Create new cart
                    var newCart = new Cart
                    {
                        SessionId = sessionId,
                        TotalAmount = 0,
                        CreatedDate = DateTime.UtcNow
                    };

                    await _unitOfWork.Carts.AddAsync(newCart);
                    await _unitOfWork.SaveAsync();

                    return MapToCartDTO(newCart);
                }

                await LoadCartItems(existingCart);
                await UpdateCartTotals(existingCart);

                return MapToCartDTO(existingCart);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting/creating cart for session {SessionId}", sessionId);
                throw;
            }
        }

        public async Task<CartDto> AddItemToCartAsync(string sessionId, AddToCartRequest request)
        {
            try
            {
                // 1. Validate Product
                var product = await _unitOfWork.Products.GetByIdAsync(request.ProductId);
                if (product == null || product.IsDeleted)
                    throw new KeyNotFoundException($"Product not found");

                if (product.StockQuantity < request.Quantity)
                    throw new InvalidOperationException($"Insufficient stock");

                // 2. Validate Color
                if (!string.IsNullOrEmpty(request.SelectedColor))
                {
                    var colorExists = await _unitOfWork.ProductColors
                        .ExistsAsync(pc => pc.ProductId == request.ProductId &&
                                          pc.ColorName == request.SelectedColor);
                    if (!colorExists)
                        throw new InvalidOperationException($"Color not available");
                }

                // 3. Get Cart
                var cart = await GetOrCreateCartEntityAsync(sessionId);

                // 4. Check existing item using DB query
                var existingItem = await _unitOfWork.CartItems.FindOneAsync(i =>
                    i.CartId == cart.Id &&
                    i.ProductId == request.ProductId &&
                    i.SelectedColor == request.SelectedColor);

                if (existingItem != null)
                {
                    // Update quantity
                    var newQuantity = existingItem.Quantity + request.Quantity;
                    if (newQuantity > MAX_QUANTITY_PER_ITEM)
                        throw new InvalidOperationException($"Max quantity exceeded");
                    if (newQuantity > product.StockQuantity)
                        throw new InvalidOperationException($"Insufficient stock");

                    existingItem.Quantity = newQuantity;
                    existingItem.OriginalPrice = product.Price; // Update original price
                    existingItem.UnitPrice = product.FinalPrice; // Update unit price
                    existingItem.ProductDiscountPercent = product.DiscountPercent;

                    _unitOfWork.CartItems.Update(existingItem);
                }
                else
                {
                    // Create new item
                    var cartItem = new CartItem
                    {
                        CartId = cart.Id,
                        ProductId = request.ProductId,
                        Quantity = request.Quantity,
                        SelectedColor = request.SelectedColor,
                        UnitPrice = product.FinalPrice,
                        OriginalPrice = product.Price, // Store original price
                        ProductDiscountPercent = product.DiscountPercent
                    };

                    await _unitOfWork.CartItems.AddAsync(cartItem);
                }

                await _unitOfWork.SaveAsync();
                await UpdateCartTotals(cart);
                await _unitOfWork.SaveAsync();

                return MapToCartDTO(cart);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding item to cart");
                throw;
            }
        }

        public async Task<CartDto> UpdateItemQuantityAsync(string sessionId, UpdateCartItemRequest request)
        {
            try
            {
                var cart = await GetCartEntityAsync(sessionId);
                await LoadCartItems(cart);

                var cartItem = cart.Items.FirstOrDefault(i => i.Id == request.CartItemId);
                if (cartItem == null)
                    throw new KeyNotFoundException($"Cart item with ID {request.CartItemId} not found");

                var product = await _unitOfWork.Products.GetByIdAsync(cartItem.ProductId);
                if (product == null || product.IsDeleted)
                    throw new KeyNotFoundException($"Product not found");

                // Validate stock
                if (request.Quantity > product.StockQuantity)
                    throw new InvalidOperationException($"Insufficient stock. Available: {product.StockQuantity}");

                if (request.Quantity > MAX_QUANTITY_PER_ITEM)
                    throw new InvalidOperationException($"Maximum quantity per item is {MAX_QUANTITY_PER_ITEM}");

                if (request.Quantity < 1)
                {
                    // Remove item if quantity is 0 or less
                    cart.Items.Remove(cartItem);
                    _unitOfWork.CartItems.Delete(cartItem);
                }
                else
                {
                    cartItem.Quantity = request.Quantity;
                    _unitOfWork.CartItems.Update(cartItem);
                }

                await UpdateCartTotals(cart);
                await _unitOfWork.SaveAsync();

                return MapToCartDTO(cart);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating cart item quantity for session {SessionId}", sessionId);
                throw;
            }
        }

        public async Task<CartDto> RemoveItemFromCartAsync(string sessionId, int cartItemId)
        {
            try
            {
                var cart = await GetCartEntityAsync(sessionId);
                await LoadCartItems(cart);

                var cartItem = cart.Items.FirstOrDefault(i => i.Id == cartItemId);
                if (cartItem == null)
                    throw new KeyNotFoundException($"Cart item with ID {cartItemId} not found");

                cart.Items.Remove(cartItem);
                _unitOfWork.CartItems.Delete(cartItem);

                await UpdateCartTotals(cart);
                await _unitOfWork.SaveAsync();

                return MapToCartDTO(cart);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing item from cart for session {SessionId}", sessionId);
                throw;
            }
        }

        public async Task ClearCartAsync(string sessionId)
        {
            try
            {
                var cart = await GetCartEntityAsync(sessionId);
                await LoadCartItems(cart);

                foreach (var item in cart.Items)
                {
                    _unitOfWork.CartItems.Delete(item);
                }

                cart.Items.Clear();
                cart.TotalAmount = 0;
                cart.DiscountAmount = 0;
                cart.LastModified = DateTime.UtcNow;

                _unitOfWork.Carts.Update(cart);
                await _unitOfWork.SaveAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error clearing cart for session {SessionId}", sessionId);
                throw;
            }
        }

        public async Task<CartSummaryDTO> GetCartSummaryAsync(string sessionId)
        {
            try
            {
                var cart = await _unitOfWork.Carts
                    .FindAsync(c => c.SessionId == sessionId);

                var existingCart = cart.FirstOrDefault();

                if (existingCart == null)
                    return new CartSummaryDTO { IsEmpty = true };

                await LoadCartItems(existingCart);

                return new CartSummaryDTO
                {
                    TotalItems = existingCart.Items.Sum(i => i.Quantity),
                    SubTotal = existingCart.Items.Sum(i => i.Quantity * i.UnitPrice),
                    Discount = existingCart.DiscountAmount,
                    Total = existingCart.TotalAmount,
                    IsEmpty = !existingCart.Items.Any()
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting cart summary for session {SessionId}", sessionId);
                return new CartSummaryDTO { IsEmpty = true };
            }
        }

        public async Task<bool> ValidateCartAsync(string sessionId)
        {
            try
            {
                var cart = await GetCartEntityAsync(sessionId);
                await LoadCartItems(cart);

                bool isValid = true;
                foreach (var item in cart.Items)
                {
                    var product = await _unitOfWork.Products.GetByIdAsync(item.ProductId);

                    if (product == null || product.IsDeleted || product.StockQuantity < item.Quantity)
                    {
                        isValid = false;
                        break;
                    }

                    // Update unit price in case product price changed
                    if (item.UnitPrice != product.FinalPrice)
                    {
                        item.UnitPrice = product.FinalPrice;
                        _unitOfWork.CartItems.Update(item);
                    }
                }

                if (!isValid)
                {
                    // Remove invalid items
                    var invalidItems = new List<CartItem>();
                    foreach (var item in cart.Items.ToList())
                    {
                        var product = await _unitOfWork.Products.GetByIdAsync(item.ProductId);
                        if (product == null || product.IsDeleted || product.StockQuantity < item.Quantity)
                        {
                            invalidItems.Add(item);
                            cart.Items.Remove(item);
                            _unitOfWork.CartItems.Delete(item);
                        }
                    }

                    await UpdateCartTotals(cart);
                    await _unitOfWork.SaveAsync();
                }

                return isValid;
            }
            catch
            {
                return false;
            }
        }

        public async Task<CartDto> MergeCartsAsync(string sourceSessionId, string targetSessionId)
        {
            try
            {
                var sourceCart = await GetCartEntityAsync(sourceSessionId);
                var targetCart = await GetOrCreateCartEntityAsync(targetSessionId);

                await LoadCartItems(sourceCart);
                await LoadCartItems(targetCart);

                foreach (var sourceItem in sourceCart.Items)
                {
                    var existingItem = targetCart.Items.FirstOrDefault(i =>
                        i.ProductId == sourceItem.ProductId &&
                        i.SelectedColor == sourceItem.SelectedColor);

                    if (existingItem != null)
                    {
                        // Update quantity
                        var product = await _unitOfWork.Products.GetByIdAsync(sourceItem.ProductId);
                        var newQuantity = existingItem.Quantity + sourceItem.Quantity;

                        if (product != null && newQuantity <= product.StockQuantity && newQuantity <= MAX_QUANTITY_PER_ITEM)
                        {
                            existingItem.Quantity = newQuantity;
                            _unitOfWork.CartItems.Update(existingItem);
                        }
                    }
                    else
                    {
                        // Add as new item
                        var newItem = new CartItem
                        {
                            CartId = targetCart.Id,
                            ProductId = sourceItem.ProductId,
                            Quantity = sourceItem.Quantity,
                            SelectedColor = sourceItem.SelectedColor,
                            UnitPrice = sourceItem.UnitPrice
                        };

                        await _unitOfWork.CartItems.AddAsync(newItem);
                        targetCart.Items.Add(newItem);
                    }

                    // Remove from source cart
                    _unitOfWork.CartItems.Delete(sourceItem);
                }

                // Update totals and save
                await UpdateCartTotals(targetCart);
                sourceCart.Items.Clear();
                sourceCart.TotalAmount = 0;
                sourceCart.DiscountAmount = 0;
                sourceCart.LastModified = DateTime.UtcNow;

                _unitOfWork.Carts.Update(sourceCart);
                _unitOfWork.Carts.Update(targetCart);

                await _unitOfWork.SaveAsync();

                return MapToCartDTO(targetCart);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error merging carts from {SourceSession} to {TargetSession}",
                    sourceSessionId, targetSessionId);
                throw;
            }
        }

        public async Task<int> GetCartItemCountAsync(string sessionId)
        {
            try
            {
                var cart = await _unitOfWork.Carts
                    .FindAsync(c => c.SessionId == sessionId);

                var existingCart = cart.FirstOrDefault();
                if (existingCart == null) return 0;

                await LoadCartItems(existingCart);
                return existingCart.Items.Sum(i => i.Quantity);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting cart item count for session {SessionId}", sessionId);
                return 0;
            }
        }

        public async Task<CartDto> ApplyDiscountAsync(string sessionId, string discountCode)
        {
            try
            {
                var cart = await GetCartEntityAsync(sessionId);
                await LoadCartItems(cart);

                // Validate and calculate discount
                var discountResult = await ValidateDiscountCode(discountCode, cart);

                if (!discountResult.IsValid)
                {
                    throw new InvalidOperationException(discountResult.ErrorMessage);
                }

                // Apply discount to cart
                cart.DiscountCode = discountResult.DiscountCode;
                cart.DiscountAmount = discountResult.DiscountAmount;

                await UpdateCartTotals(cart);
                await _unitOfWork.SaveAsync();

                // Increment usage count in discount (optional - you can do this on checkout)
                // await IncrementDiscountUsage(discountResult.DiscountId.Value);

                return MapToCartDTO(cart);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error applying discount to cart");
                throw;
            }
        }

        //public async Task<CartDto> ApplyDiscountAsync(string sessionId, string discountCode)
        //{
        //    try
        //    {
        //        var cart = await GetCartEntityAsync(sessionId);
        //        await LoadCartItems(cart);

        //        // Validate and calculate discount
        //        var discountResult = await ValidateDiscountCode(discountCode, cart);

        //        if (!discountResult.IsValid)
        //        {
        //            throw new InvalidOperationException(discountResult.ErrorMessage);
        //        }

        //        // Apply discount to cart
        //        cart.DiscountCode = discountResult.DiscountCode;
        //        cart.DiscountAmount = discountResult.DiscountAmount;

        //        await UpdateCartTotals(cart);
        //        await _unitOfWork.SaveAsync();

        //        return MapToCartDTO(cart);
        //    }
        //    catch (Exception ex)
        //    {
        //        _logger.LogError(ex, "Error applying discount to cart");
        //        throw;
        //    }
        //}

        //private async Task<DiscountValidationResult> ValidateDiscountCode(string discountCode, Cart cart)
        //{
        //    var result = new DiscountValidationResult();

        //    if (string.IsNullOrWhiteSpace(discountCode))
        //    {
        //        result.ErrorMessage = "Discount code cannot be empty";
        //        return result;
        //    }

        //    // Get discount from database
        //    var discount = await _unitOfWork.Discounts
        //        .FindOneAsync(d => d.Code == discountCode && d.IsActive);

        //    if (discount == null)
        //    {
        //        result.ErrorMessage = "Invalid discount code";
        //        return result;
        //    }

        //    // Check if discount is active
        //    if (!discount.IsActive)
        //    {
        //        result.ErrorMessage = "Discount code is not active";
        //        return result;
        //    }

        //    // Check if discount has started
        //    if (discount.StartDate > DateTime.UtcNow)
        //    {
        //        result.ErrorMessage = "Discount code is not yet valid";
        //        return result;
        //    }

        //    // Check expiration
        //    if (discount.ExpiryDate.HasValue && discount.ExpiryDate < DateTime.UtcNow)
        //    {
        //        result.ErrorMessage = "Discount code has expired";
        //        return result;
        //    }

        //    // Check if discount has usage limit
        //    if (discount.UsageLimit.HasValue && discount.UsageCount >= discount.UsageLimit.Value)
        //    {
        //        result.ErrorMessage = "Discount code usage limit reached";
        //        return result;
        //    }

        //    // Calculate subtotal (sum of items after product discounts)
        //    decimal subtotal = cart.Items.Sum(i => i.Quantity * i.UnitPrice);

        //    // Check minimum order amount
        //    if (discount.MinimumOrderAmount.HasValue && subtotal < discount.MinimumOrderAmount.Value)
        //    {
        //        result.ErrorMessage = $"Minimum order amount of {discount.MinimumOrderAmount.Value:C} required";
        //        return result;
        //    }

        //    // Calculate discount amount based on type
        //    decimal discountAmount = 0;

        //    if (discount.DiscountType == DiscountType.Percentage)
        //    {
        //        // Percentage discount
        //        discountAmount = subtotal * (discount.DiscountValue / 100m);
        //    }
        //    else if (discount.DiscountType == DiscountType.FixedAmount)
        //    {
        //        // Fixed amount discount
        //        discountAmount = discount.DiscountValue;

        //        // Ensure discount doesn't exceed subtotal
        //        discountAmount = Math.Min(discountAmount, subtotal);
        //    }

        //    result.IsValid = true;
        //    result.DiscountAmount = Math.Round(discountAmount, 2);
        //    result.DiscountPercentage = discount.DiscountType == DiscountType.Percentage ? discount.DiscountValue : 0;
        //    result.DiscountId = discount.Id; // Store discount ID for later reference
        //    result.DiscountCode = discount.Code;

        //    return result;
        //}

        public async Task<CartDto> RemoveDiscountAsync(string sessionId)
        {
            try
            {
                var cart = await GetCartEntityAsync(sessionId);

                cart.DiscountCode = string.Empty;
                cart.DiscountAmount = 0;

                await UpdateCartTotals(cart);
                await _unitOfWork.SaveAsync();

                return MapToCartDTO(cart);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing discount from cart");
                throw;
            }
        }

        // Core/Services/CartService.cs - Replace the ValidateDiscountCode method

        private async Task<DiscountValidationResult> ValidateDiscountCode(string discountCode, Cart cart)
        {
            var result = new DiscountValidationResult();
            result.DiscountCode = discountCode;

            if (string.IsNullOrWhiteSpace(discountCode))
            {
                result.ErrorMessage = "Please enter a discount code";
                return result;
            }

            // Get discount from database (case-insensitive search)
            var discount = await _unitOfWork.Discounts
                .FindOneAsync(d => d.Code.ToUpper() == discountCode.ToUpper() && d.IsActive);

            if (discount == null)
            {
                result.ErrorMessage = "Invalid discount code. Please check and try again.";
                return result;
            }

            // Check if discount is active
            if (!discount.IsActive)
            {
                result.ErrorMessage = "This discount code is currently inactive.";
                return result;
            }

            // Check if discount has started
            if (discount.StartDate > DateTime.UtcNow)
            {
                result.ErrorMessage = $"This discount code is not valid until {discount.StartDate:MMM dd, yyyy}.";
                return result;
            }

            // Check expiration
            if (discount.ExpiryDate.HasValue && discount.ExpiryDate < DateTime.UtcNow)
            {
                result.ErrorMessage = $"This discount code expired on {discount.ExpiryDate.Value:MMM dd, yyyy}.";
                return result;
            }

            // ⭐ CHECK PER-GUEST USAGE LIMIT
            if (discount.UsageLimitPerGuest.HasValue)
            {
                var guestUsage = await _unitOfWork.DiscountUsages
                    .FindOneAsync(du => du.DiscountId == discount.Id && du.SessionId == cart.SessionId);

                if (guestUsage != null && guestUsage.UsageCount >= discount.UsageLimitPerGuest.Value)
                {
                    if (discount.UsageLimitPerGuest.Value == 1)
                    {
                        result.ErrorMessage = "You have already used this discount code.";
                    }
                    else
                    {
                        result.ErrorMessage = $"You have already used this discount code {discount.UsageLimitPerGuest.Value} times.";
                    }
                    return result;
                }
            }

            // Calculate subtotal
            decimal subtotal = cart.Items.Sum(i => i.Quantity * i.UnitPrice);

            // Check minimum order amount
            if (discount.MinimumOrderAmount.HasValue && subtotal < discount.MinimumOrderAmount.Value)
            {
                //result.ErrorMessage = $"Minimum order amount of {discount.MinimumOrderAmount.Value:C} required. Your subtotal is {subtotal:C}.";
                result.ErrorMessage =$"Minimum order amount of {FormatEgp(discount.MinimumOrderAmount.Value)} required. " + $"Your subtotal is {FormatEgp(subtotal)}.";
                return result;
            }

            // Calculate discount amount
            decimal discountAmount = 0;

            if (discount.DiscountType == DiscountType.Percentage)
            {
                discountAmount = subtotal * (discount.DiscountValue / 100m);
                _logger.LogInformation("Applying {Percentage}% discount: {Amount} on subtotal {Subtotal}",
                    discount.DiscountValue, discountAmount, subtotal);
            }
            else if (discount.DiscountType == DiscountType.FixedAmount)
            {
                discountAmount = Math.Min(discount.DiscountValue, subtotal);
                _logger.LogInformation("Applying fixed discount: {Amount} on subtotal {Subtotal}",
                    discountAmount, subtotal);
            }

            result.IsValid = true;
            result.DiscountAmount = Math.Round(discountAmount, 2);
            result.DiscountPercentage = discount.DiscountType == DiscountType.Percentage ? discount.DiscountValue : 0;
            result.DiscountId = discount.Id;
            result.DiscountCode = discount.Code;

            return result;
        }



        #region Private Helper Methods

        private string FormatEgp(decimal amount)
        {
            return $"EGP {amount:N2}";
        }


        private async Task<Cart> GetCartEntityAsync(string sessionId)
        {
            var cart = await _unitOfWork.Carts
                .FindAsync(c => c.SessionId == sessionId);

            var existingCart = cart.FirstOrDefault();
            if (existingCart == null)
                throw new KeyNotFoundException($"Cart not found for session {sessionId}");

            return existingCart;
        }

        private async Task<Cart> GetOrCreateCartEntityAsync(string sessionId)
        {
            var cart = await _unitOfWork.Carts
                .FindAsync(c => c.SessionId == sessionId);

            var existingCart = cart.FirstOrDefault();

            if (existingCart == null)
            {
                existingCart = new Cart
                {
                    SessionId = sessionId,
                    TotalAmount = 0,
                    CreatedDate = DateTime.UtcNow
                };

                await _unitOfWork.Carts.AddAsync(existingCart);
                await _unitOfWork.SaveAsync();
            }

            return existingCart;
        }

        private async Task LoadCartItems(Cart cart)
        {
            _logger.LogInformation("Loading cart items for cart ID: {CartId}", cart.Id);

            var items = await _unitOfWork.CartItems
                .FindAsync(ci => ci.CartId == cart.Id);

            cart.Items = items.ToList();

            _logger.LogInformation("Loaded {ItemCount} items for cart ID: {CartId}", cart.Items.Count, cart.Id);
        }

        private async Task UpdateCartTotals(Cart cart)
        {
            await LoadCartItems(cart);

            decimal subtotal = 0;
            decimal originalSubtotal = 0;
            decimal totalProductDiscount = 0;

            foreach (var item in cart.Items)
            {
                // Ensure we have latest product price
                var product = await _unitOfWork.Products.GetByIdAsync(item.ProductId);
                if (product != null)
                {
                    // Update prices
                    item.OriginalPrice = product.Price;
                    item.UnitPrice = product.FinalPrice;
                    item.ProductDiscountPercent = product.DiscountPercent;
                }

                // Calculate item totals
                decimal itemSubtotal = item.Quantity * item.UnitPrice;
                decimal itemOriginalTotal = item.Quantity * item.OriginalPrice;

                subtotal += itemSubtotal;
                originalSubtotal += itemOriginalTotal;
                totalProductDiscount += (itemOriginalTotal - itemSubtotal);
            }

            // Set cart properties
            cart.Subtotal = subtotal;

            // IMPORTANT: Store the original subtotal in a different property
            // Since we can't add OriginalSubtotal to Cart model, we can:
            // Option A: Use an existing unused property
            // Option B: Calculate in DTO only
            // Let's calculate in DTO only for now

            // Re-validate cart-level discount if exists
            if (!string.IsNullOrEmpty(cart.DiscountCode))
            {
                var discountResult = await ValidateDiscountCode(cart.DiscountCode, cart);
                if (!discountResult.IsValid)
                {
                    // Remove invalid discount
                    cart.DiscountCode = string.Empty;
                    cart.DiscountAmount = 0;
                }
                else
                {
                    // Update discount amount (in case cart changed)
                    // Cart discount applies to SUBTOTAL (after product discounts)
                    cart.DiscountAmount = Math.Min(discountResult.DiscountAmount, cart.Subtotal);
                }
            }

            // Calculate final total: Subtotal - Cart Discount
            cart.TotalAmount = Math.Max(0, cart.Subtotal - cart.DiscountAmount);
            cart.LastModified = DateTime.UtcNow;

            _unitOfWork.Carts.Update(cart);
        }


        private CartDto MapToCartDTO(Cart cart)
        {
            decimal subtotal = 0;
            decimal originalSubtotal = 0;
            decimal totalProductDiscount = 0;

            var dto = new CartDto
            {
                Id = cart.Id,
                SessionId = cart.SessionId,
                DiscountAmount = cart.DiscountAmount,
                TotalAmount = cart.TotalAmount, // This comes from cart entity
                Subtotal = cart.Subtotal, // This comes from cart entity
                CreatedDate = cart.CreatedDate,
                TotalItems = cart.Items.Sum(i => i.Quantity),
                DiscountCode = cart.DiscountCode ?? string.Empty
            };

            // Map items with product details
            foreach (var item in cart.Items)
            {
                var product = _unitOfWork.Products.GetByIdAsync(item.ProductId).Result;
                var productImage = _unitOfWork.ProductImages
                    .FindAsync(pi => pi.ProductId == item.ProductId && pi.IsPrimary)
                    .Result.FirstOrDefault();

                // Calculate per-item totals
                decimal lineTotal = item.Quantity * item.UnitPrice;
                decimal originalLineTotal = item.Quantity * item.OriginalPrice;
                decimal lineDiscount = originalLineTotal - lineTotal;

                var itemDto = new CartItemDto
                {
                    Id = item.Id,
                    ProductId = item.ProductId,
                    ProductName = product?.Name ?? "Unknown Product",
                    ProductImageUrl = productImage?.ImageUrl ?? "/images/default-product.png",
                    Quantity = item.Quantity,
                    SelectedColor = item.SelectedColor,

                    // Price information
                    UnitPrice = item.UnitPrice,
                    OriginalPrice = item.OriginalPrice,
                    LineTotal = lineTotal,

                    // Discount information
                    DiscountPercent = item.ProductDiscountPercent,
                    DiscountAmount = lineDiscount,
                    FinalPrice = item.UnitPrice,

                    // Stock info
                    MaxStock = product?.StockQuantity ?? 0,
                    IsAvailable = product != null && !product.IsDeleted && product.StockQuantity >= item.Quantity
                };

                // Get color hex code if color is selected
                if (!string.IsNullOrEmpty(item.SelectedColor) && product != null)
                {
                    var color = _unitOfWork.ProductColors
                        .FindAsync(pc => pc.ProductId == product.Id && pc.ColorName == item.SelectedColor)
                        .Result.FirstOrDefault();

                    itemDto.SelectedColorHex = color?.ColorHexCode;
                }

                dto.Items.Add(itemDto);

                // Accumulate totals for verification
                subtotal += lineTotal;
                originalSubtotal += originalLineTotal;
                totalProductDiscount += lineDiscount;
            }

            // CRITICAL: Don't overwrite these values - they should come from the cart entity
            // The cart entity already has the correct calculations from UpdateCartTotals()

            // Use the calculated values only for these DTO-only properties
            dto.TotalOriginalPrice = originalSubtotal;
            dto.TotalProductDiscount = totalProductDiscount;

            // Ensure consistency (optional validation)
            if (Math.Abs(dto.Subtotal - subtotal) > 0.01m)
            {
                _logger.LogWarning("Subtotal mismatch: Cart.Subtotal={CartSubtotal}, Calculated={Calculated}",
                    dto.Subtotal, subtotal);
            }

            if (Math.Abs(dto.TotalAmount - Math.Max(0, dto.Subtotal - dto.DiscountAmount)) > 0.01m)
            {
                _logger.LogWarning("TotalAmount mismatch");
                // Optionally recalculate for safety
                dto.TotalAmount = Math.Max(0, dto.Subtotal - dto.DiscountAmount);
            }

            return dto;
        }

        // Optional: Method to increment discount usage
        private async Task IncrementDiscountUsage(int discountId)
        {
            var discount = await _unitOfWork.Discounts.GetByIdAsync(discountId);
            if (discount != null)
            {
                discount.TotalUsageCount++;
                discount.LastModified = DateTime.UtcNow;
                _unitOfWork.Discounts.Update(discount);
                await _unitOfWork.SaveAsync();
            }
        }

        #endregion
    }

   
}