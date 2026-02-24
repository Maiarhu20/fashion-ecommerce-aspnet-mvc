using Core.DTOs;
using Core.DTOs.Products;
using Domain.Models;
using Infrastructure.Interfaces;
using Microsoft.Extensions.Logging;

namespace Core.Services
{
    public class ProductService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<ProductService> _logger;

        public ProductService(IUnitOfWork unitOfWork, ILogger<ProductService> logger)
        {
            _unitOfWork = unitOfWork;
            _logger = logger;
        }

        public async Task<ServiceResult<ProductResponseDto?>> GetByIdAsync(int id)
        {
            try
            {
                _logger.LogInformation("Getting product by ID: {ProductId}", id);

                if (id <= 0)
                    return ServiceResult<ProductResponseDto?>.Failure("Invalid product ID.");

                // Get the basic product first
                var product = await _unitOfWork.Products.GetByIdAsync(id);
                if (product == null || product.IsDeleted)
                    return ServiceResult<ProductResponseDto?>.Failure("Product not found.");

                // Now manually load all related data
                await LoadProductRelationsAsync(product);

                var result = await MapToResponseDtoAsync(product);
                return ServiceResult<ProductResponseDto?>.Success(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving product with ID: {ProductId}", id);
                return ServiceResult<ProductResponseDto?>.Failure("An error occurred while retrieving the product.", ex);
            }
        }

        public async Task<ServiceResult<IEnumerable<ProductListDto>>> GetAllAsync()
        {
            try
            {
                _logger.LogInformation("Retrieving all products");

                var products = await _unitOfWork.Products.GetAllAsync();
                var activeProducts = products.Where(p => !p.IsDeleted).ToList();

                // Load relations for all products
                foreach (var product in activeProducts)
                {
                    await LoadProductRelationsAsync(product);
                }

                var result = new List<ProductListDto>();
                foreach (var product in activeProducts)
                {
                    var dto = await MapToListDtoAsync(product);
                    result.Add(dto);
                }

                _logger.LogInformation("Successfully retrieved {Count} products", result.Count);
                return ServiceResult<IEnumerable<ProductListDto>>.Success(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving all products");
                return ServiceResult<IEnumerable<ProductListDto>>.Failure("An error occurred while retrieving products.", ex);
            }
        }

        public async Task<ServiceResult<IEnumerable<ProductListDto>>> GetDeletedProductsAsync()
        {
            try
            {
                _logger.LogInformation("Retrieving deleted products");

                var products = await _unitOfWork.Products.FindAsync(p => p.IsDeleted);

                // Load relations for all deleted products
                foreach (var product in products)
                {
                    await LoadProductRelationsAsync(product);
                }

                var result = new List<ProductListDto>();
                foreach (var product in products)
                {
                    var dto = await MapToListDtoAsync(product);
                    result.Add(dto);
                }

                return ServiceResult<IEnumerable<ProductListDto>>.Success(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving deleted products");
                return ServiceResult<IEnumerable<ProductListDto>>.Failure("An error occurred while retrieving deleted products.", ex);
            }
        }

        // Helper method to load all related data for a product
        private async Task LoadProductRelationsAsync(Product product)
        {
            try
            {
                // Load category
                if (product.Category == null && product.CategoryId > 0)
                {
                    product.Category = await _unitOfWork.Categories.GetByIdAsync(product.CategoryId);
                }

                // Load images
                var images = await _unitOfWork.ProductImages.FindAsync(img => img.ProductId == product.Id);
                product.Images = images.OrderBy(img => img.DisplayOrder).ToList();

                // Load colors
                var colors = await _unitOfWork.ProductColors.FindAsync(c => c.ProductId == product.Id);
                product.Colors = colors.ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading relations for product ID: {ProductId}", product.Id);
            }
        }

        public async Task<ServiceResult<ProductResponseDto>> CreateAsync(ProductCreateDto productDto)
        {
            try
            {
                _logger.LogInformation("Creating new product: {ProductName}", productDto.Name);

                if (productDto == null)
                    return ServiceResult<ProductResponseDto>.Failure("Product data is required.");

                // Validate category exists
                var category = await _unitOfWork.Categories.GetByIdAsync(productDto.CategoryId);
                if (category == null || category.IsDeleted)
                    return ServiceResult<ProductResponseDto>.Failure("Selected category does not exist.");

                // Validate colors
                if (productDto.ColorHexCodes == null || !productDto.ColorHexCodes.Any())
                    return ServiceResult<ProductResponseDto>.Failure("At least one color is required.");

                // Validate color names
                if (productDto.ColorNames == null)
                    productDto.ColorNames = new List<string>();

                // Ensure color names array matches hex codes array
                while (productDto.ColorNames.Count < productDto.ColorHexCodes.Count)
                {
                    var hex = productDto.ColorHexCodes[productDto.ColorNames.Count];
                    productDto.ColorNames.Add(GetColorNameFromHex(hex));
                }

                var product = new Product
                {
                    Name = productDto.Name.Trim(),
                    Description = productDto.Description?.Trim() ?? string.Empty,
                    Price = productDto.Price,
                    StockQuantity = productDto.StockQuantity,
                    CategoryId = productDto.CategoryId,
                    DiscountPercent = productDto.DiscountPercent,
                    CreatedDate = DateTime.UtcNow,
                    IsDeleted = false
                };

                // Add colors with hex codes and custom names
                for (int i = 0; i < productDto.ColorHexCodes.Count; i++)
                {
                    var colorHex = productDto.ColorHexCodes[i];
                    if (!string.IsNullOrWhiteSpace(colorHex))
                    {
                        // Get the corresponding color name
                        var colorName = productDto.ColorNames[i].Trim();
                        if (string.IsNullOrWhiteSpace(colorName))
                            colorName = GetColorNameFromHex(colorHex);

                        product.Colors.Add(new ProductColor
                        {
                            ColorName = colorName,
                            ColorHexCode = colorHex.Trim().ToUpper()
                        });
                    }
                }
                _logger.LogInformation("Added {Count} colors to product", productDto.ColorHexCodes.Count);

                // Add images
                if (productDto.ImageUrls != null && productDto.ImageUrls.Any())
                {
                    for (int i = 0; i < productDto.ImageUrls.Count; i++)
                    {
                        if (!string.IsNullOrEmpty(productDto.ImageUrls[i]))
                        {
                            product.Images.Add(new ProductImage
                            {
                                ImageUrl = productDto.ImageUrls[i],
                                IsPrimary = i == 0, // First image is primary
                                DisplayOrder = i,
                                CreatedDate = DateTime.UtcNow
                            });
                        }
                    }
                    _logger.LogInformation("Added {Count} images to product", productDto.ImageUrls.Count);
                }
                else
                {
                    _logger.LogWarning("No images provided for product");
                }

                await _unitOfWork.Products.AddAsync(product);
                await _unitOfWork.SaveAsync();

                _logger.LogInformation("Successfully created product: {ProductName} with ID: {ProductId}",
                    product.Name, product.Id);

                var result = await MapToResponseDtoAsync(product);
                return ServiceResult<ProductResponseDto>.Success(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating product: {ProductName}", productDto?.Name);
                return ServiceResult<ProductResponseDto>.Failure("An error occurred while creating the product.", ex);
            }
        }

        public async Task<ServiceResult<ProductResponseDto?>> UpdateAsync(ProductUpdateDto productDto)
        {
            try
            {
                _logger.LogInformation("Updating product with ID: {ProductId}", productDto.Id);

                if (productDto == null)
                    return ServiceResult<ProductResponseDto?>.Failure("Product data is required.");

                var product = await _unitOfWork.Products.GetByIdAsync(productDto.Id);
                if (product == null || product.IsDeleted)
                    return ServiceResult<ProductResponseDto?>.Failure("Product not found.");

                var category = await _unitOfWork.Categories.GetByIdAsync(productDto.CategoryId);
                if (category == null || category.IsDeleted)
                    return ServiceResult<ProductResponseDto?>.Failure("Selected category does not exist.");

                if (productDto.ColorHexCodes == null || !productDto.ColorHexCodes.Any())
                    return ServiceResult<ProductResponseDto?>.Failure("At least one color is required.");

                if (productDto.ColorNames == null)
                    productDto.ColorNames = new List<string>();

                while (productDto.ColorNames.Count < productDto.ColorHexCodes.Count)
                {
                    var hex = productDto.ColorHexCodes[productDto.ColorNames.Count];
                    productDto.ColorNames.Add(GetColorNameFromHex(hex));
                }

                product.Name = productDto.Name.Trim();
                product.Description = productDto.Description?.Trim() ?? string.Empty;
                product.Price = productDto.Price;
                product.StockQuantity = productDto.StockQuantity;
                product.CategoryId = productDto.CategoryId;
                product.DiscountPercent = productDto.DiscountPercent;

                // Update colors
                var existingColors = (await _unitOfWork.ProductColors
                    .FindAsync(c => c.ProductId == product.Id)).ToList();

                var newColorHexes = productDto.ColorHexCodes?
                    .Where(c => !string.IsNullOrWhiteSpace(c))
                    .Select(c => c.Trim().ToUpper())
                    .Distinct()
                    .ToList() ?? new List<string>();

                foreach (var existingColor in existingColors)
                {
                    if (!newColorHexes.Contains(existingColor.ColorHexCode))
                    {
                        var colorToDelete = await _unitOfWork.ProductColors.GetByIdAsync(existingColor.Id);
                        if (colorToDelete != null)
                        {
                            _unitOfWork.ProductColors.Delete(colorToDelete);
                            _logger.LogInformation("Removed color: {ColorName} ({ColorHex})",
                                colorToDelete.ColorName, colorToDelete.ColorHexCode);
                        }
                    }
                    else
                    {
                        newColorHexes.Remove(existingColor.ColorHexCode);

                        var colorIndex = productDto.ColorHexCodes.IndexOf(existingColor.ColorHexCode);
                        if (colorIndex >= 0 && colorIndex < productDto.ColorNames.Count)
                        {
                            var newColorName = productDto.ColorNames[colorIndex].Trim();
                            if (!string.IsNullOrEmpty(newColorName) && existingColor.ColorName != newColorName)
                            {
                                existingColor.ColorName = newColorName;
                                _unitOfWork.ProductColors.Update(existingColor);
                                _logger.LogInformation("Updated color name: {OldName} -> {NewName} ({ColorHex})",
                                    existingColor.ColorName, newColorName, existingColor.ColorHexCode);
                            }
                        }
                    }
                }

                for (int i = 0; i < productDto.ColorHexCodes.Count; i++)
                {
                    var colorHex = productDto.ColorHexCodes[i];
                    if (newColorHexes.Contains(colorHex))
                    {
                        var colorName = productDto.ColorNames[i].Trim();
                        if (string.IsNullOrWhiteSpace(colorName))
                            colorName = GetColorNameFromHex(colorHex);

                        product.Colors.Add(new ProductColor
                        {
                            ColorName = colorName,
                            ColorHexCode = colorHex.Trim().ToUpper()
                        });

                        _logger.LogInformation("Added new color: {ColorName} ({ColorHex})", colorName, colorHex);
                        newColorHexes.Remove(colorHex);
                    }
                }

                // Update images
                var existingImages = (await _unitOfWork.ProductImages
                    .FindAsync(img => img.ProductId == product.Id))
                    .OrderBy(img => img.DisplayOrder)
                    .ToList();

                var existingImageUrls = productDto.ImageUrls?
                    .Where(url => !string.IsNullOrEmpty(url))
                    .ToList() ?? new List<string>();

                // Remove images that are no longer in the list
                foreach (var existingImage in existingImages)
                {
                    if (!existingImageUrls.Contains(existingImage.ImageUrl))
                    {
                        var imageToDelete = await _unitOfWork.ProductImages.GetByIdAsync(existingImage.Id);
                        if (imageToDelete != null)
                        {
                            _unitOfWork.ProductImages.Delete(imageToDelete);
                            _logger.LogInformation("Removed image: {ImageUrl}", imageToDelete.ImageUrl);
                        }
                    }
                    else
                    {
                        existingImageUrls.Remove(existingImage.ImageUrl);
                    }
                }

                // Add new images
                int displayOrder = existingImages.Count(existing => existingImageUrls.Contains(existing.ImageUrl));
                foreach (var imageUrl in existingImageUrls)
                {
                    product.Images.Add(new ProductImage
                    {
                        ImageUrl = imageUrl,
                        IsPrimary = product.Images.Count == 0,
                        DisplayOrder = displayOrder++,
                        CreatedDate = DateTime.UtcNow
                    });
                    _logger.LogInformation("Added new image: {ImageUrl}", imageUrl);
                }

                // Reorder images to maintain proper display order
                var allImages = product.Images.ToList();
                for (int i = 0; i < allImages.Count; i++)
                {
                    allImages[i].DisplayOrder = i;
                }

                _unitOfWork.Products.Update(product);
                await _unitOfWork.SaveAsync();

                // FIX: Ensure at least one image is primary AFTER save
                await EnsurePrimaryImageAsync(product.Id);

                _logger.LogInformation("Successfully updated product: {ProductName}", product.Name);

                var result = await MapToResponseDtoAsync(product);
                return ServiceResult<ProductResponseDto?>.Success(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating product with ID: {ProductId}", productDto?.Id);
                return ServiceResult<ProductResponseDto?>.Failure("An error occurred while updating the product.", ex);
            }
        }

        /// <summary>
        /// Ensures that at least one image is marked as primary.
        /// Called after image deletion to reassign primary status if needed.
        /// </summary>
        private async Task EnsurePrimaryImageAsync(int productId)
        {
            try
            {
                var images = (await _unitOfWork.ProductImages
                    .FindAsync(img => img.ProductId == productId))
                    .OrderBy(img => img.DisplayOrder)
                    .ToList();

                if (!images.Any())
                {
                    _logger.LogWarning("Product ID {ProductId} has no images", productId);
                    return;
                }

                // Check if any image is already marked as primary
                var primaryImage = images.FirstOrDefault(img => img.IsPrimary);

                if (primaryImage == null)
                {
                    // No primary image found, set the first one as primary
                    var firstImage = images.First();
                    firstImage.IsPrimary = true;
                    _unitOfWork.ProductImages.Update(firstImage);
                    await _unitOfWork.SaveAsync();
                    _logger.LogInformation("Auto-set image {ImageId} as primary for product {ProductId}",
                        firstImage.Id, productId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error ensuring primary image for product ID: {ProductId}", productId);
            }
        }

        public async Task<ServiceResult> DeleteAsync(int id)
        {
            try
            {
                _logger.LogInformation("Attempting to delete product ID: {ProductId}", id);

                // Check if product can be deleted
                var canDeleteResult = await CanDeleteProductAsync(id);
                if (!canDeleteResult.Succeeded || !canDeleteResult.Data)
                {
                    var eligibility = await CheckDeletionEligibilityAsync(id);
                    return ServiceResult.Failure(
                        eligibility.Succeeded ? eligibility.Data?.Message : "Product cannot be deleted.");
                }

                var product = await _unitOfWork.Products.GetByIdAsync(id);
                if (product == null || product.IsDeleted)
                    return ServiceResult.Failure("Product not found.");

                // Hard delete - only if no orders
                _unitOfWork.Products.Delete(product);
                await _unitOfWork.SaveAsync();

                _logger.LogInformation("Successfully hard deleted product ID: {ProductId}", id);
                return ServiceResult.Success;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting product ID: {ProductId}", id);
                return ServiceResult.Failure("An error occurred while deleting the product.", ex);
            }
        }

        public async Task<ServiceResult> SoftDeleteAsync(int id)
        {
            try
            {
                _logger.LogInformation("Attempting to soft delete product ID: {ProductId}", id);

                var product = await _unitOfWork.Products.GetByIdAsync(id);
                if (product == null || product.IsDeleted)
                    return ServiceResult.Failure("Product not found.");

                // Soft delete - set IsDeleted to true
                product.IsDeleted = true;
                _unitOfWork.Products.Update(product);
                await _unitOfWork.SaveAsync();

                _logger.LogInformation("Successfully soft deleted product ID: {ProductId}", id);
                return ServiceResult.Success;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error soft deleting product ID: {ProductId}", id);
                return ServiceResult.Failure("An error occurred while deleting the product.", ex);
            }
        }

        public async Task<ServiceResult> RestoreAsync(int id)
        {
            try
            {
                _logger.LogInformation("Restoring product with ID: {ProductId}", id);

                if (id <= 0)
                    return ServiceResult.Failure("Invalid product ID.");

                var product = await _unitOfWork.Products.GetByIdAsync(id);
                if (product == null)
                    return ServiceResult.Failure("Product not found.");

                if (!product.IsDeleted)
                    return ServiceResult.Failure("Product is not deleted.");

                // Restore the product
                product.IsDeleted = false;
                _unitOfWork.Products.Update(product);
                await _unitOfWork.SaveAsync();

                _logger.LogInformation("Successfully restored product: {ProductName} with ID: {ProductId}",
                    product.Name, product.Id);

                return ServiceResult.Success;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error restoring product with ID: {ProductId}", id);
                return ServiceResult.Failure("An error occurred while restoring the product.", ex);
            }
        }

        public async Task<ServiceResult<ProductDeletionCheckDto>> CheckDeletionEligibilityAsync(int productId)
        {
            try
            {
                _logger.LogInformation("Checking deletion eligibility for product ID: {ProductId}", productId);

                if (productId <= 0)
                    return ServiceResult<ProductDeletionCheckDto>.Failure("Invalid product ID.");

                var product = await _unitOfWork.Products.GetByIdAsync(productId);
                if (product == null || product.IsDeleted)
                    return ServiceResult<ProductDeletionCheckDto>.Failure("Product not found.");

                var result = new ProductDeletionCheckDto();
                var blockingReasons = new List<string>();

                // Check if product has orders
                var orderItems = await _unitOfWork.OrderItems.FindAsync(oi => oi.ProductId == productId);
                result.OrderCount = orderItems.Count();

                if (result.OrderCount > 0)
                {
                    blockingReasons.Add($"Product is referenced in {result.OrderCount} order(s)");
                    result.CanDelete = false;
                    result.Message = "Cannot delete product because it has order history. Use Archive instead.";
                    result.BlockingReasons = blockingReasons;
                    return ServiceResult<ProductDeletionCheckDto>.Success(result);
                }

                result.CanDelete = true;
                result.BlockingReasons = blockingReasons;
                result.Message = "Product can be safely deleted as it has no order history.";

                _logger.LogInformation("Deletion check for product {ProductId}: {Result}", productId, result.Message);
                return ServiceResult<ProductDeletionCheckDto>.Success(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking deletion eligibility for product ID: {ProductId}", productId);
                return ServiceResult<ProductDeletionCheckDto>.Failure("Error checking deletion eligibility.", ex);
            }
        }

        public async Task<ServiceResult<bool>> CanDeleteProductAsync(int productId)
        {
            var result = await CheckDeletionEligibilityAsync(productId);
            if (!result.Succeeded)
                return ServiceResult<bool>.Success(false);

            return ServiceResult<bool>.Success(result.Data?.CanDelete ?? false);
        }

        // Add to ProductService.cs
        public async Task<ServiceResult<IEnumerable<ProductListDto>>> GetProductsByCategoryAsync(int categoryId)
        {
            try
            {
                _logger.LogInformation("Retrieving products for category ID: {CategoryId}", categoryId);

                if (categoryId <= 0)
                    return ServiceResult<IEnumerable<ProductListDto>>.Failure("Invalid category ID.");

                // Verify category exists and is not deleted
                var category = await _unitOfWork.Categories.GetByIdAsync(categoryId);
                if (category == null || category.IsDeleted)
                    return ServiceResult<IEnumerable<ProductListDto>>.Failure("Category not found.");

                // Get non-deleted products for this category
                var products = await _unitOfWork.Products
                    .FindAsync(p => p.CategoryId == categoryId && !p.IsDeleted);

                // Load relations for all products
                foreach (var product in products)
                {
                    await LoadProductRelationsAsync(product);
                }

                var result = new List<ProductListDto>();
                foreach (var product in products)
                {
                    var dto = await MapToListDtoAsync(product);
                    result.Add(dto);
                }

                _logger.LogInformation("Retrieved {Count} products for category ID: {CategoryId}",
                    result.Count, categoryId);

                return ServiceResult<IEnumerable<ProductListDto>>.Success(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving products for category ID: {CategoryId}", categoryId);
                return ServiceResult<IEnumerable<ProductListDto>>.Failure("An error occurred while retrieving products.", ex);
            }
        }

        public async Task<ServiceResult<IEnumerable<ProductListDto>>> GetActiveProductsAsync()
        {
            try
            {
                _logger.LogInformation("Retrieving all active products");

                var products = await _unitOfWork.Products
                    .FindAsync(p => !p.IsDeleted);

                // Load relations for all products
                foreach (var product in products)
                {
                    await LoadProductRelationsAsync(product);
                }

                var result = new List<ProductListDto>();
                foreach (var product in products)
                {
                    var dto = await MapToListDtoAsync(product);
                    result.Add(dto);
                }

                _logger.LogInformation("Retrieved {Count} active products", result.Count);
                return ServiceResult<IEnumerable<ProductListDto>>.Success(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving active products");
                return ServiceResult<IEnumerable<ProductListDto>>.Failure("An error occurred while retrieving products.", ex);
            }
        }

        private async Task<ProductResponseDto> MapToResponseDtoAsync(Product product)
        {
            // Ensure relations are loaded (double safety)
            if (product.Images == null || !product.Images.Any())
            {
                var images = await _unitOfWork.ProductImages.FindAsync(img => img.ProductId == product.Id);
                product.Images = images.OrderBy(img => img.DisplayOrder).ToList();
            }

            if (product.Colors == null || !product.Colors.Any())
            {
                var colors = await _unitOfWork.ProductColors.FindAsync(c => c.ProductId == product.Id);
                product.Colors = colors.ToList();
            }

            if (product.Category == null && product.CategoryId > 0)
            {
                product.Category = await _unitOfWork.Categories.GetByIdAsync(product.CategoryId);
            }

            var orderCount = await _unitOfWork.OrderItems.CountAsync(oi => oi.ProductId == product.Id);
            var reviewCount = await _unitOfWork.Reviews.CountAsync(r => r.ProductId == product.Id && !r.IsDeleted);

            return new ProductResponseDto
            {
                Id = product.Id,
                Name = product.Name,
                Description = product.Description,
                Price = product.Price,
                FinalPrice = product.FinalPrice,
                StockQuantity = product.StockQuantity,
                CategoryId = product.CategoryId,
                CategoryName = product.Category?.Name ?? "Unknown",
                DiscountPercent = product.DiscountPercent,
                CreatedDate = product.CreatedDate,
                IsDeleted = product.IsDeleted,
                OrderCount = orderCount,
                ReviewCount = reviewCount,
                Images = product.Images.Select(img => new ProductImageDto
                {
                    Id = img.Id,
                    ImageUrl = img.ImageUrl,
                    IsPrimary = img.IsPrimary,
                    DisplayOrder = img.DisplayOrder
                }).OrderBy(img => img.DisplayOrder).ToList(),
                Colors = product.Colors.Select(c => new ProductColorDto
                {
                    Id = c.Id,
                    ColorName = c.ColorName,
                    ColorHexCode = c.ColorHexCode
                }).ToList()
            };
        }

        private async Task<ProductListDto> MapToListDtoAsync(Product product)
        {
            // Ensure relations are loaded (double safety)
            if (product.Images == null || !product.Images.Any())
            {
                var images = await _unitOfWork.ProductImages.FindAsync(img => img.ProductId == product.Id);
                product.Images = images.OrderBy(img => img.DisplayOrder).ToList();
            }

            if (product.Colors == null || !product.Colors.Any())
            {
                var colors = await _unitOfWork.ProductColors.FindAsync(c => c.ProductId == product.Id);
                product.Colors = colors.ToList();
            }

            if (product.Category == null && product.CategoryId > 0)
            {
                product.Category = await _unitOfWork.Categories.GetByIdAsync(product.CategoryId);
            }

            var orderCount = await _unitOfWork.OrderItems.CountAsync(oi => oi.ProductId == product.Id);
            var reviewCount = await _unitOfWork.Reviews.CountAsync(r => r.ProductId == product.Id && !r.IsDeleted);

            var primaryImage = product.Images?.FirstOrDefault(img => img.IsPrimary) ?? product.Images?.FirstOrDefault();

            return new ProductListDto
            {
                Id = product.Id,
                Name = product.Name,
                Description = product.Description,
                Price = product.Price,
                FinalPrice = product.FinalPrice,
                StockQuantity = product.StockQuantity,
                CategoryName = product.Category?.Name ?? "Unknown",
                DiscountPercent = product.DiscountPercent,
                CreatedDate = product.CreatedDate,
                IsDeleted = product.IsDeleted,
                OrderCount = orderCount,
                ReviewCount = reviewCount,
                PrimaryImageUrl = primaryImage?.ImageUrl ?? string.Empty,
                // FIX: Add Images property population
                Images = product.Images?.Select(img => new ProductImageDto
                {
                    Id = img.Id,
                    ImageUrl = img.ImageUrl,
                    IsPrimary = img.IsPrimary,
                    DisplayOrder = img.DisplayOrder
                }).OrderBy(img => img.DisplayOrder).ToList() ?? new List<ProductImageDto>(),
                Colors = product.Colors?.Select(c => new ProductColorListDto
                {
                    ColorName = c.ColorName,
                    ColorHexCode = c.ColorHexCode
                }).ToList() ?? new List<ProductColorListDto>()
            };
        }

        private string GetColorNameFromHex(string hexCode)
        {
            var colorMap = new Dictionary<string, string>
            {
                {"#FF0000", "Red"}, {"#00FF00", "Green"}, {"#0000FF", "Blue"},
                {"#FFFF00", "Yellow"}, {"#FF00FF", "Magenta"}, {"#00FFFF", "Cyan"},
                {"#000000", "Black"}, {"#FFFFFF", "White"}, {"#808080", "Gray"},
                {"#FFA500", "Orange"}, {"#800080", "Purple"}, {"#FFC0CB", "Pink"},
                {"#A52A2A", "Brown"}, {"#008000", "Dark Green"}, {"#000080", "Navy"},
                {"#FF4500", "Orange Red"}, {"#DA70D6", "Orchid"}, {"#EEE8AA", "Pale Goldenrod"},
                {"#98FB98", "Pale Green"}, {"#AFEEEE", "Pale Turquoise"}, {"#DB7093", "Pale Violet Red"},
                {"#FFEFD5", "Papaya Whip"}, {"#FFDAB9", "Peach Puff"}, {"#CD853F", "Peru"}
            };

            var normalizedHex = hexCode.Trim().ToUpper();
            return colorMap.ContainsKey(normalizedHex) ? colorMap[normalizedHex] : "Custom Color";
        }
    }
}