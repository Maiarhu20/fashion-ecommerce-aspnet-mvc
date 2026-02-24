// Core/Services/CategoryService.cs
using Core.DTOs;
using Core.DTOs.Categories;
using Core.Services;
using Domain.Models;
using Infrastructure.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Core.Services
{
    public class CategoryService 
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<CategoryService> _logger;

        public CategoryService(IUnitOfWork unitOfWork, ILogger<CategoryService> logger)
        {
            _unitOfWork = unitOfWork;
            _logger = logger;
        }

        public async Task<ServiceResult<CategoryResponseDto?>> GetByIdAsync(int id)
        {
            try
            {
                _logger.LogInformation("Getting category by ID: {CategoryId}", id);

                if (id <= 0)
                {
                    _logger.LogWarning("Invalid category ID: {CategoryId}", id);
                    return ServiceResult<CategoryResponseDto?>.Failure("Invalid category ID.");
                }

                var category = await _unitOfWork.Categories
                    .FindAsync(c => c.Id == id)
                    .ContinueWith(t => t.Result.FirstOrDefault());

                if (category == null)
                {
                    _logger.LogWarning("Category not found with ID: {CategoryId}", id);
                    return ServiceResult<CategoryResponseDto?>.Failure("Category not found.");
                }

                var productCount = await _unitOfWork.Products
                    .FindAsync(p => p.CategoryId == id && !p.IsDeleted)
                    .ContinueWith(t => t.Result.Count());

                var result = MapToResponseDto(category, productCount);
                _logger.LogInformation("Successfully retrieved category: {CategoryName}", category.Name);

                return ServiceResult<CategoryResponseDto?>.Success(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving category with ID: {CategoryId}", id);
                return ServiceResult<CategoryResponseDto?>.Failure("An error occurred while retrieving the category.", ex);
            }
        }

        public async Task<ServiceResult<IEnumerable<CategoryListDto>>> GetAllAsync()
        {
            try
            {
                _logger.LogInformation("Retrieving all categories");

                var categories = await _unitOfWork.Categories.GetAllAsync();
                var activeCategories = categories.Where(c => !c.IsDeleted).ToList();

                var result = new List<CategoryListDto>();
                foreach (var category in activeCategories)
                {
                    var productCount = await _unitOfWork.Products
                        .FindAsync(p => p.CategoryId == category.Id && !p.IsDeleted)
                        .ContinueWith(t => t.Result.Count());

                    result.Add(new CategoryListDto
                    {
                        Id = category.Id,
                        Name = category.Name,
                        Description = category.Description,
                        ImageUrl = category.ImageUrl,
                        ProductCount = productCount
                    });
                }

                _logger.LogInformation("Successfully retrieved {Count} categories", result.Count);
                return ServiceResult<IEnumerable<CategoryListDto>>.Success(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving all categories");
                return ServiceResult<IEnumerable<CategoryListDto>>.Failure("An error occurred while retrieving categories.", ex);
            }
        }

        public async Task<ServiceResult<IEnumerable<CategoryListDto>>> GetActiveCategoriesAsync()
        {
            try
            {
                _logger.LogInformation("Retrieving active categories with products");

                var categories = await _unitOfWork.Categories
                    .FindAsync(c => !c.IsDeleted);

                var result = new List<CategoryListDto>();
                foreach (var category in categories)
                {
                    var productCount = await _unitOfWork.Products
                        .FindAsync(p => p.CategoryId == category.Id && !p.IsDeleted)
                        .ContinueWith(t => t.Result.Count());

                    // Only include categories that have products
                    if (productCount > 0)
                    {
                        result.Add(new CategoryListDto
                        {
                            Id = category.Id,
                            Name = category.Name,
                            Description = category.Description,
                            ImageUrl = category.ImageUrl,
                            ProductCount = productCount
                        });
                    }
                }

                _logger.LogInformation("Successfully retrieved {Count} active categories", result.Count);
                return ServiceResult<IEnumerable<CategoryListDto>>.Success(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving active categories");
                return ServiceResult<IEnumerable<CategoryListDto>>.Failure("An error occurred while retrieving active categories.", ex);
            }
        }

        public async Task<ServiceResult<IEnumerable<CategoryListDto>>> GetDeletedCategoriesAsync()
        {
            try
            {
                _logger.LogInformation("Retrieving deleted categories");

                var categories = await _unitOfWork.Categories
                    .FindAsync(c => c.IsDeleted);

                var result = new List<CategoryListDto>();
                foreach (var category in categories)
                {
                    var productCount = await _unitOfWork.Products
                        .FindAsync(p => p.CategoryId == category.Id)
                        .ContinueWith(t => t.Result.Count());

                    result.Add(new CategoryListDto
                    {
                        Id = category.Id,
                        Name = category.Name,
                        Description = category.Description,
                        ImageUrl = category.ImageUrl,
                        ProductCount = productCount
                    });
                }

                _logger.LogInformation("Successfully retrieved {Count} deleted categories", result.Count);
                return ServiceResult<IEnumerable<CategoryListDto>>.Success(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving deleted categories");
                return ServiceResult<IEnumerable<CategoryListDto>>.Failure("An error occurred while retrieving deleted categories.", ex);
            }
        }

        public async Task<ServiceResult<CategoryResponseDto>> CreateAsync(CategoryCreateDto categoryDto)
        {
            try
            {
                _logger.LogInformation("Creating new category: {CategoryName}", categoryDto.Name);

                if (categoryDto == null)
                {
                    _logger.LogWarning("Attempted to create category with null DTO");
                    return ServiceResult<CategoryResponseDto>.Failure("Category data is required.");
                }

                // Validate required fields
                if (string.IsNullOrWhiteSpace(categoryDto.Name))
                {
                    return ServiceResult<CategoryResponseDto>.Failure("Category name is required.");
                }

                // ✅ ADD THIS BACK: Check if category with same name already exists (including deleted ones)
                var existingCategory = await _unitOfWork.Categories
                    .FindAsync(c => c.Name.ToLower() == categoryDto.Name.Trim().ToLower())
                    .ContinueWith(t => t.Result.FirstOrDefault());

                if (existingCategory != null)
                {
                    if (existingCategory.IsDeleted)
                    {
                        // If a deleted category with same name exists, restore it instead
                        _logger.LogInformation("Restoring previously deleted category with name: {CategoryName}", categoryDto.Name);
                        existingCategory.IsDeleted = false;
                        existingCategory.Description = categoryDto.Description?.Trim() ?? string.Empty;
                        existingCategory.ImageUrl = categoryDto.ImageUrl?.Trim() ?? string.Empty;

                        _unitOfWork.Categories.Update(existingCategory);
                        await _unitOfWork.SaveAsync();

                        var productCount = await _unitOfWork.Products
                            .FindAsync(p => p.CategoryId == existingCategory.Id && !p.IsDeleted)
                            .ContinueWith(t => t.Result.Count());

                        var restoredResult = MapToResponseDto(existingCategory, productCount);
                        return ServiceResult<CategoryResponseDto>.Success(restoredResult);
                    }
                    else
                    {
                        _logger.LogWarning("Category with name '{CategoryName}' already exists", categoryDto.Name);
                        return ServiceResult<CategoryResponseDto>.Failure("A category with this name already exists.");
                    }
                }

                var category = new Category
                {
                    Name = categoryDto.Name.Trim(),
                    Description = categoryDto.Description?.Trim() ?? string.Empty,
                    ImageUrl = categoryDto.ImageUrl?.Trim() ?? string.Empty,
                    CreatedDate = DateTime.UtcNow,
                    IsDeleted = false
                };

                await _unitOfWork.Categories.AddAsync(category);
                await _unitOfWork.SaveAsync();

                _logger.LogInformation("Successfully created category: {CategoryName} with ID: {CategoryId}",
                    category.Name, category.Id);

                return ServiceResult<CategoryResponseDto>.Success(MapToResponseDto(category, 0));
            }
            catch (DbUpdateException dbEx)
            {
                _logger.LogError(dbEx, "Database error while creating category: {CategoryName}", categoryDto?.Name);
                return ServiceResult<CategoryResponseDto>.Failure("A database error occurred while creating the category.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error creating category: {CategoryName}", categoryDto?.Name);
                return ServiceResult<CategoryResponseDto>.Failure("An unexpected error occurred while creating the category.");
            }
        }

        public async Task<ServiceResult<CategoryResponseDto?>> UpdateAsync(CategoryUpdateDto categoryDto)
        {
            try
            {
                _logger.LogInformation("Updating category with ID: {CategoryId}", categoryDto.Id);

                if (categoryDto == null)
                {
                    _logger.LogWarning("Attempted to update category with null DTO");
                    return ServiceResult<CategoryResponseDto?>.Failure("Category data is required.");
                }

                var category = await _unitOfWork.Categories.GetByIdAsync(categoryDto.Id);
                if (category == null)
                {
                    _logger.LogWarning("Category not found for update with ID: {CategoryId}", categoryDto.Id);
                    return ServiceResult<CategoryResponseDto?>.Failure("Category not found.");
                }

                if (category.IsDeleted)
                {
                    _logger.LogWarning("Attempted to update deleted category with ID: {CategoryId}", categoryDto.Id);
                    return ServiceResult<CategoryResponseDto?>.Failure("Cannot update a deleted category. Please restore it first.");
                }

                // Check if another category with the same name exists
                var duplicateCategory = await _unitOfWork.Categories
                    .FindAsync(c => c.Name.ToLower() == categoryDto.Name.ToLower()
                                 && c.Id != categoryDto.Id
                                 && !c.IsDeleted)
                    .ContinueWith(t => t.Result.FirstOrDefault());

                if (duplicateCategory != null)
                {
                    _logger.LogWarning("Another category with name '{CategoryName}' already exists", categoryDto.Name);
                    return ServiceResult<CategoryResponseDto?>.Failure("Another category with this name already exists.");
                }

                category.Name = categoryDto.Name.Trim();
                category.Description = categoryDto.Description?.Trim() ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(categoryDto.ImageUrl)) { category.ImageUrl = categoryDto.ImageUrl.Trim(); }

                _unitOfWork.Categories.Update(category);
                await _unitOfWork.SaveAsync();

                var productCount = await _unitOfWork.Products
                    .FindAsync(p => p.CategoryId == category.Id && !p.IsDeleted)
                    .ContinueWith(t => t.Result.Count());

                _logger.LogInformation("Successfully updated category: {CategoryName}", category.Name);

                var result = MapToResponseDto(category, productCount);
                return ServiceResult<CategoryResponseDto?>.Success(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating category with ID: {CategoryId}", categoryDto?.Id);
                return ServiceResult<CategoryResponseDto?>.Failure("An error occurred while updating the category.", ex);
            }
        }

        public async Task<ServiceResult> DeleteAsync(int id)
        {
            try
            {
                _logger.LogInformation("Attempting to delete category ID: {CategoryId}", id);

                // Check if category can be deleted
                var canDeleteResult = await CanDeleteCategoryAsync(id);
                if (!canDeleteResult.Succeeded || !canDeleteResult.Data)
                {
                    var eligibility = await CheckDeletionEligibilityAsync(id);
                    return ServiceResult.Failure(
                        eligibility.Succeeded ? eligibility.Data?.Message : "Category cannot be deleted.");
                }

                var category = await _unitOfWork.Categories.GetByIdAsync(id);
                if (category == null || category.IsDeleted)
                {
                    return ServiceResult.Failure("Category not found.");
                }

                // Only soft delete products that have no orders
                var products = await _unitOfWork.Products
                    .FindAsync(p => p.CategoryId == id && !p.IsDeleted);

                foreach (var product in products)
                {
                    // Check if product has any order items
                    var hasOrders = await _unitOfWork.OrderItems
                        .ExistsAsync(oi => oi.ProductId == product.Id);

                    if (!hasOrders)
                    {
                        product.IsDeleted = true;
                        _unitOfWork.Products.Update(product);
                        _logger.LogInformation("Soft deleted product ID: {ProductId} as it has no orders", product.Id);
                    }
                    else
                    {
                        _logger.LogInformation("Preserved product ID: {ProductId} as it has order history", product.Id);
                    }
                }

                // Soft delete the category
                category.IsDeleted = true;
                _unitOfWork.Categories.Update(category);
                await _unitOfWork.SaveAsync();

                _logger.LogInformation("Successfully soft deleted category ID: {CategoryId}", id);
                return ServiceResult.Success;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting category ID: {CategoryId}", id);
                return ServiceResult.Failure("An error occurred while deleting the category.", ex);
            }
        }

        public async Task<ServiceResult> RestoreAsync(int id)
        {
            try
            {
                _logger.LogInformation("Restoring category with ID: {CategoryId}", id);

                if (id <= 0)
                {
                    _logger.LogWarning("Invalid category ID for restoration: {CategoryId}", id);
                    return ServiceResult.Failure("Invalid category ID.");
                }

                var category = await _unitOfWork.Categories.GetByIdAsync(id);
                if (category == null)
                {
                    _logger.LogWarning("Category not found for restoration with ID: {CategoryId}", id);
                    return ServiceResult.Failure("Category not found.");
                }

                if (!category.IsDeleted)
                {
                    _logger.LogWarning("Category is not deleted, cannot restore ID: {CategoryId}", id);
                    return ServiceResult.Failure("Category is not deleted.");
                }

                // Restore the category
                category.IsDeleted = false;
                _unitOfWork.Categories.Update(category);

                // Note: Products are NOT automatically restored
                // Admin must restore products separately if needed

                await _unitOfWork.SaveAsync();

                _logger.LogInformation("Successfully restored category: {CategoryName} with ID: {CategoryId}",
                    category.Name, category.Id);

                return ServiceResult.Success;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error restoring category with ID: {CategoryId}", id);
                return ServiceResult.Failure("An error occurred while restoring the category.", ex);
            }
        }

        public async Task<ServiceResult<CategoryDeletionCheckDto>> CheckDeletionEligibilityAsync(int categoryId)
        {
            try
            {
                _logger.LogInformation("Checking deletion eligibility for category ID: {CategoryId}", categoryId);

                if (categoryId <= 0)
                {
                    return ServiceResult<CategoryDeletionCheckDto>.Failure("Invalid category ID.");
                }

                var category = await _unitOfWork.Categories.GetByIdAsync(categoryId);
                if (category == null || category.IsDeleted)
                {
                    return ServiceResult<CategoryDeletionCheckDto>.Failure("Category not found.");
                }

                var result = new CategoryDeletionCheckDto();
                var blockingReasons = new List<string>();

                // Check if category has products
                var products = await _unitOfWork.Products
                    .FindAsync(p => p.CategoryId == categoryId && !p.IsDeleted);
                result.ProductCount = products.Count();

                if (result.ProductCount > 0)
                {
                    blockingReasons.Add($"Category contains {result.ProductCount} active product(s)");

                    // Check if any products have existing orders
                    var productIds = products.Select(p => p.Id).ToList();
                    var orderItems = await _unitOfWork.OrderItems
                        .FindAsync(oi => productIds.Contains(oi.ProductId));

                    if (orderItems.Any())
                    {
                        var uniqueOrderCount = orderItems.Select(oi => oi.OrderId).Distinct().Count();
                        result.ActiveOrderCount = uniqueOrderCount;
                        blockingReasons.Add($"Products in this category are referenced in {uniqueOrderCount} order(s)");

                        // This is a hard block - cannot delete if products have orders
                        result.CanDelete = false;
                        result.Message = "Cannot delete category because products have order history. Use Archive instead.";
                        result.BlockingReasons = blockingReasons;
                        return ServiceResult<CategoryDeletionCheckDto>.Success(result);
                    }
                }

                result.CanDelete = true;
                result.BlockingReasons = blockingReasons;
                result.Message = result.ProductCount > 0
                    ? "Category can be deleted. Products will be removed as they have no order history."
                    : "Category can be safely deleted as it contains no products.";

                _logger.LogInformation("Deletion check for category {CategoryId}: {Result}",
                    categoryId, result.Message);

                return ServiceResult<CategoryDeletionCheckDto>.Success(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking deletion eligibility for category ID: {CategoryId}", categoryId);
                return ServiceResult<CategoryDeletionCheckDto>.Failure("Error checking deletion eligibility.", ex);
            }
        }

        public async Task<ServiceResult<bool>> CanDeleteCategoryAsync(int categoryId)
        {
            var result = await CheckDeletionEligibilityAsync(categoryId);
            if (!result.Succeeded)
            {
                return ServiceResult<bool>.Success(false);
            }
            return ServiceResult<bool>.Success(result.Data?.CanDelete ?? false);
        }

        public async Task<ServiceResult> ArchiveCategoryAsync(int categoryId)
        {
            try
            {
                _logger.LogInformation("Archiving category ID: {CategoryId}", categoryId);

                var category = await _unitOfWork.Categories.GetByIdAsync(categoryId);
                if (category == null || category.IsDeleted)
                {
                    return ServiceResult.Failure("Category not found.");
                }

                // Hide all products in this category (soft delete)
                var products = await _unitOfWork.Products
                    .FindAsync(p => p.CategoryId == categoryId && !p.IsDeleted);

                foreach (var product in products)
                {
                    product.IsDeleted = true;
                    _unitOfWork.Products.Update(product);
                    _logger.LogInformation("Archived product ID: {ProductId}", product.Id);
                }

                // Category itself remains active but products are hidden
                // This allows the category structure to remain for future use

                await _unitOfWork.SaveAsync();

                _logger.LogInformation("Successfully archived category ID: {CategoryId}. {ProductCount} products hidden.",
                    categoryId, products.Count());

                return ServiceResult.Success;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error archiving category ID: {CategoryId}", categoryId);
                return ServiceResult.Failure("Error archiving category.", ex);
            }
        }

        public async Task<ServiceResult<bool>> ExistsAsync(int id)
        {
            try
            {
                if (id <= 0)
                {
                    return ServiceResult<bool>.Success(false);
                }

                var exists = await _unitOfWork.Categories
                    .ExistsAsync(c => c.Id == id && !c.IsDeleted);

                return ServiceResult<bool>.Success(exists);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking if category exists with ID: {CategoryId}", id);
                return ServiceResult<bool>.Failure("An error occurred while checking category existence.", ex);
            }
        }

        private static CategoryResponseDto MapToResponseDto(Category category, int productCount)
        {
            return new CategoryResponseDto
            {
                Id = category.Id,
                Name = category.Name,
                Description = category.Description,
                ImageUrl = category.ImageUrl,
                CreatedDate = category.CreatedDate,
                ProductCount = productCount,
                IsDeleted = category.IsDeleted
            };
        }
    }
}