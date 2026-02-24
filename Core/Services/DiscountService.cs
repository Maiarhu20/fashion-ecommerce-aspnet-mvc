using Core.DTOs.Discount;
using Domain.Models;
using Infrastructure.Interfaces;
using Microsoft.Extensions.Logging;

namespace Core.Services
{
    public class DiscountService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<DiscountService> _logger;

        public DiscountService(IUnitOfWork unitOfWork, ILogger<DiscountService> logger)
        {
            _unitOfWork = unitOfWork;
            _logger = logger;
        }

        public async Task<IEnumerable<DiscountListDto>> GetAllDiscountsAsync()
        {
            try
            {
                var discounts = await _unitOfWork.Discounts.GetAllAsync();

                return discounts.Select(d => new DiscountListDto
                {
                    Id = d.Id,
                    Code = d.Code,
                    Description = d.Description,
                    DiscountType = d.DiscountType.ToString(),
                    DiscountValue = d.DiscountValue,
                    UsageLimitPerGuest = d.UsageLimitPerGuest,
                    TotalUsageCount = d.TotalUsageCount,
                    StartDate = d.StartDate,
                    ExpiryDate = d.ExpiryDate,
                    IsActive = d.IsActive,
                    IsCurrentlyActive = d.IsActive && d.StartDate <= DateTime.UtcNow &&
                                       (!d.ExpiryDate.HasValue || d.ExpiryDate.Value > DateTime.UtcNow)
                }).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all discounts");
                throw;
            }
        }

        public async Task<DiscountDto> GetDiscountByIdAsync(int id)
        {
            try
            {
                var discount = await _unitOfWork.Discounts.GetByIdAsync(id);
                if (discount == null)
                    throw new KeyNotFoundException($"Discount with ID {id} not found");

                return MapToDto(discount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting discount by ID {Id}", id);
                throw;
            }
        }

        public async Task<DiscountDto> GetDiscountByCodeAsync(string code)
        {
            try
            {
                var discount = await _unitOfWork.Discounts
                    .FindOneAsync(d => d.Code == code);

                if (discount == null)
                    throw new KeyNotFoundException($"Discount with code {code} not found");

                return MapToDto(discount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting discount by code {Code}", code);
                throw;
            }
        }

        public async Task<DiscountDto> CreateDiscountAsync(CreateDiscountDto dto)
        {
            try
            {
                var existing = await _unitOfWork.Discounts
                    .FindOneAsync(d => d.Code == dto.Code);

                if (existing != null)
                    throw new InvalidOperationException($"Discount code '{dto.Code}' already exists");

                if (dto.ExpiryDate.HasValue && dto.ExpiryDate <= dto.StartDate)
                    throw new InvalidOperationException("Expiry date must be after start date");

                var discount = new Discount
                {
                    Code = dto.Code.ToUpper(),
                    Description = dto.Description,
                    DiscountType = Enum.Parse<DiscountType>(dto.DiscountType),
                    DiscountValue = dto.DiscountValue,
                    MinimumOrderAmount = dto.MinimumOrderAmount,
                    UsageLimitPerGuest = dto.UsageLimitPerGuest,
                    StartDate = dto.StartDate,
                    ExpiryDate = dto.ExpiryDate,
                    IsActive = dto.IsActive,
                    CreatedDate = DateTime.UtcNow
                };

                await _unitOfWork.Discounts.AddAsync(discount);
                await _unitOfWork.SaveAsync();

                return MapToDto(discount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating discount");
                throw;
            }
        }

        public async Task<DiscountDto> UpdateDiscountAsync(UpdateDiscountDto dto)
        {
            try
            {
                var discount = await _unitOfWork.Discounts.GetByIdAsync(dto.Id);
                if (discount == null)
                    throw new KeyNotFoundException($"Discount with ID {dto.Id} not found");

                if (!string.IsNullOrEmpty(dto.Description))
                    discount.Description = dto.Description;

                discount.DiscountValue = dto.DiscountValue;
                discount.DiscountType = Enum.Parse<DiscountType>(dto.DiscountType);
                discount.MinimumOrderAmount = dto.MinimumOrderAmount;
                discount.UsageLimitPerGuest = dto.UsageLimitPerGuest;
                discount.StartDate = dto.StartDate;
                discount.ExpiryDate = dto.ExpiryDate;
                discount.IsActive = dto.IsActive;
                discount.LastModified = DateTime.UtcNow;

                _unitOfWork.Discounts.Update(discount);
                await _unitOfWork.SaveAsync();

                return MapToDto(discount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating discount with ID {DiscountId}", dto.Id);
                throw;
            }
        }

        public async Task<bool> DeleteDiscountAsync(int id)
        {
            try
            {
                var discount = await _unitOfWork.Discounts.GetByIdAsync(id);
                if (discount == null)
                    throw new KeyNotFoundException($"Discount with ID {id} not found");

                // Check if discount is being used in orders - FIXED: Use AppliedDiscountId
                var isUsed = await _unitOfWork.Orders
                    .ExistsAsync(o => o.AppliedDiscountId == id);

                if (isUsed)
                {
                    // Instead of deleting, deactivate it
                    discount.IsActive = false;
                    discount.LastModified = DateTime.UtcNow;
                    _unitOfWork.Discounts.Update(discount);
                }
                else
                {
                    // If not used, delete it
                    _unitOfWork.Discounts.Delete(discount);
                }

                await _unitOfWork.SaveAsync();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting discount");
                throw;
            }
        }

        public async Task<bool> ToggleDiscountStatusAsync(int id)
        {
            try
            {
                var discount = await _unitOfWork.Discounts.GetByIdAsync(id);
                if (discount == null)
                    throw new KeyNotFoundException($"Discount with ID {id} not found");

                discount.IsActive = !discount.IsActive;
                discount.LastModified = DateTime.UtcNow;

                _unitOfWork.Discounts.Update(discount);
                await _unitOfWork.SaveAsync();

                return discount.IsActive;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error toggling discount status");
                throw;
            }
        }

        public async Task<bool> ValidateDiscountCodeAsync(string code)
        {
            try
            {
                var discount = await _unitOfWork.Discounts
                    .FindOneAsync(d => d.Code == code && d.IsActive);

                if (discount == null)
                    return false;

                // Check if active
                if (!discount.IsActive)
                    return false;

                // Check if started
                if (discount.StartDate > DateTime.UtcNow)
                    return false;

                // Check if expired
                if (discount.ExpiryDate.HasValue && discount.ExpiryDate < DateTime.UtcNow)
                    return false;

                // Check usage limit
                if (discount.UsageLimitPerGuest.HasValue && discount.TotalUsageCount >= discount.UsageLimitPerGuest.Value)
                    return false;

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating discount code");
                return false;
            }
        }

        public async Task<IEnumerable<DiscountListDto>> GetActiveDiscountsAsync()
        {
            try
            {
                var discounts = await _unitOfWork.Discounts
                    .FindAsync(d => d.IsActive &&
                                   d.StartDate <= DateTime.UtcNow &&
                                   (!d.ExpiryDate.HasValue || d.ExpiryDate > DateTime.UtcNow));

                return discounts.Select(d => new DiscountListDto
                {
                    Id = d.Id,
                    Code = d.Code,
                    Description = d.Description,
                    DiscountType = d.DiscountType.ToString(),
                    DiscountValue = d.DiscountValue,
                    TotalUsageCount = d.TotalUsageCount,
                    UsageLimitPerGuest = d.UsageLimitPerGuest,
                    StartDate = d.StartDate,
                    ExpiryDate = d.ExpiryDate,
                    IsActive = d.IsActive,
                    IsCurrentlyActive = true,
                    //IsUsageLimitReached = d.UsageLimit.HasValue && d.UsageCount >= d.UsageLimit.Value
                }).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting active discounts");
                throw;
            }
        }

        public async Task<DiscountStatsDto> GetDiscountStatsAsync()
        {
            try
            {
                var allDiscounts = await _unitOfWork.Discounts.GetAllAsync();
                var activeDiscounts = allDiscounts.Where(d => d.IsActive).ToList();
                var expiredDiscounts = allDiscounts.Where(d => d.ExpiryDate.HasValue && d.ExpiryDate < DateTime.UtcNow).ToList();
                var unusedDiscounts = allDiscounts.Where(d => d.TotalUsageCount == 0).ToList();

                return new DiscountStatsDto
                {
                    TotalDiscounts = allDiscounts.Count(),
                    ActiveDiscounts = activeDiscounts.Count(),
                    ExpiredDiscounts = expiredDiscounts.Count(),
                    UnusedDiscounts = unusedDiscounts.Count(),
                    TotalUsageCount = allDiscounts.Sum(d => d.TotalUsageCount),
                    MostUsedDiscount = allDiscounts.OrderByDescending(d => d.TotalUsageCount).FirstOrDefault()?.Code ?? "None"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting discount stats");
                throw;
            }
        }

        public async Task<int> GetDiscountUsageCountAsync(int discountId)
        {
            try
            {
                // FIXED: Count orders that used this discount
                var orders = await _unitOfWork.Orders
                    .FindAsync(o => o.AppliedDiscountId == discountId);

                return orders.Count(); // FIXED: Use Count() method
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting discount usage count");
                return 0;
            }
        }

        public async Task<IEnumerable<Order>> GetOrdersUsingDiscountAsync(int discountId)
        {
            try
            {
                var orders = await _unitOfWork.Orders
                    .FindAsync(o => o.AppliedDiscountId == discountId);

                return orders.ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting orders using discount");
                throw;
            }
        }

        public async Task IncrementDiscountUsageAsync(int discountId)
        {
            try
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
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error incrementing discount usage");
                throw;
            }
        }

        private DiscountDto MapToDto(Discount discount)
        {
            return new DiscountDto
            {
                Id = discount.Id,
                Code = discount.Code,
                Description = discount.Description,
                DiscountType = discount.DiscountType.ToString(),
                DiscountValue = discount.DiscountValue,
                MinimumOrderAmount = discount.MinimumOrderAmount,
                UsageLimitPerGuest = discount.UsageLimitPerGuest,
                TotalUsageCount = discount.TotalUsageCount,
                StartDate = discount.StartDate,
                ExpiryDate = discount.ExpiryDate,
                IsActive = discount.IsActive,
                CreatedDate = discount.CreatedDate,
                LastModified = discount.LastModified
            };
        }
    }

   
}