using Core.DTOs.Shipping;
using Core.Services;
using Domain.Models;
using Infrastructure.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Core.Services
{
    public class ShippingService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<ShippingService> _logger;

        public ShippingService(
            IUnitOfWork unitOfWork,
            ILogger<ShippingService> logger)
        {
            _unitOfWork = unitOfWork;
            _logger = logger;
        }

        public async Task<ServiceResult<List<ShippingCityDto>>> GetAllShippingCitiesAsync()
        {
            try
            {
                var cities = await _unitOfWork.ShippingCities
                    .GetAllAsync();

                var cityDtos = cities
                    .OrderBy(c => c.CityName)
                    .Select(MapToDto)
                    .ToList();

                return ServiceResult<List<ShippingCityDto>>.Success(cityDtos);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all shipping cities");
                return ServiceResult<List<ShippingCityDto>>.Failure("An error occurred while retrieving shipping cities", ex);
            }
        }

        public async Task<ServiceResult<ShippingCityDto>> GetShippingCityByIdAsync(int id)
        {
            try
            {
                var city = await _unitOfWork.ShippingCities.GetByIdAsync(id);
                if (city == null)
                {
                    return ServiceResult<ShippingCityDto>.Failure($"Shipping city with ID {id} not found");
                }

                var cityDto = MapToDto(city);
                return ServiceResult<ShippingCityDto>.Success(cityDto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting shipping city by ID: {Id}", id);
                return ServiceResult<ShippingCityDto>.Failure($"An error occurred while retrieving shipping city with ID {id}", ex);
            }
        }

        public async Task<ServiceResult<ShippingCityDto>> CreateShippingCityAsync(CreateShippingCityDto createDto)
        {
            try
            {
                // Check if city already exists
                var existingCity = await _unitOfWork.ShippingCities
                    .FindAsync(c => c.CityName.ToLower() == createDto.CityName.ToLower());

                if (existingCity.Any())
                {
                    return ServiceResult<ShippingCityDto>.Failure($"A shipping city with name '{createDto.CityName}' already exists");
                }

                var city = new ShippingCity
                {
                    CityName = createDto.CityName,
                    ShippingCost = createDto.ShippingCost,
                    IsActive = createDto.IsActive,
                    CreatedDate = DateTime.UtcNow
                };

                await _unitOfWork.ShippingCities.AddAsync(city);
                await _unitOfWork.SaveAsync();

                var cityDto = MapToDto(city);
                return ServiceResult<ShippingCityDto>.Success(cityDto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating shipping city");
                return ServiceResult<ShippingCityDto>.Failure("An error occurred while creating the shipping city", ex);
            }
        }

        public async Task<ServiceResult<ShippingCityDto>> UpdateShippingCityAsync(UpdateShippingCityDto updateDto)
        {
            try
            {
                var city = await _unitOfWork.ShippingCities.GetByIdAsync(updateDto.Id);
                if (city == null)
                {
                    return ServiceResult<ShippingCityDto>.Failure($"Shipping city with ID {updateDto.Id} not found");
                }

                // Check if another city already has this name
                var duplicateCity = await _unitOfWork.ShippingCities
                    .FindAsync(c => c.CityName.ToLower() == updateDto.CityName.ToLower() && c.Id != updateDto.Id);

                if (duplicateCity.Any())
                {
                    return ServiceResult<ShippingCityDto>.Failure($"Another shipping city with name '{updateDto.CityName}' already exists");
                }

                city.CityName = updateDto.CityName;
                city.ShippingCost = updateDto.ShippingCost;
                city.IsActive = updateDto.IsActive;
                city.LastModified = DateTime.UtcNow;

                _unitOfWork.ShippingCities.Update(city);
                await _unitOfWork.SaveAsync();

                var cityDto = MapToDto(city);
                return ServiceResult<ShippingCityDto>.Success(cityDto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating shipping city with ID: {Id}", updateDto.Id);
                return ServiceResult<ShippingCityDto>.Failure($"An error occurred while updating the shipping city", ex);
            }
        }

        public async Task<ServiceResult> ToggleShippingCityStatusAsync(int id)
        {
            try
            {
                var city = await _unitOfWork.ShippingCities.GetByIdAsync(id);
                if (city == null)
                {
                    return ServiceResult.Failure($"Shipping city with ID {id} not found");
                }

                city.IsActive = !city.IsActive;
                city.LastModified = DateTime.UtcNow;

                _unitOfWork.ShippingCities.Update(city);
                await _unitOfWork.SaveAsync();

                return ServiceResult.Success;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error toggling status for shipping city with ID: {Id}", id);
                return ServiceResult.Failure($"An error occurred while toggling shipping city status", ex);
            }
        }

        public async Task<ServiceResult> DeleteShippingCityAsync(int id)
        {
            try
            {
                var city = await _unitOfWork.ShippingCities.GetByIdAsync(id);
                if (city == null)
                {
                    return ServiceResult.Failure($"Shipping city with ID {id} not found");
                }

                // Check if city has orders
                var hasOrders = await HasOrdersAsync(id);
                if (hasOrders)
                {
                    return ServiceResult.Failure($"Cannot delete '{city.CityName}' because it has associated orders. You can deactivate it instead.");
                }

                _unitOfWork.ShippingCities.Delete(city);
                await _unitOfWork.SaveAsync();

                return ServiceResult.Success;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting shipping city with ID: {Id}", id);
                return ServiceResult.Failure($"An error occurred while deleting the shipping city", ex);
            }
        }

        public async Task<bool> HasOrdersAsync(int shippingCityId)
        {
            try
            {
                var orders = await _unitOfWork.Orders
                    .FindAsync(o => o.ShippingCityId == shippingCityId);

                return orders.Any();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking if shipping city has orders: {Id}", shippingCityId);
                throw;
            }
        }

        private static ShippingCityDto MapToDto(ShippingCity city)
        {
            return new ShippingCityDto
            {
                Id = city.Id,
                CityName = city.CityName,
                ShippingCost = city.ShippingCost,
                IsActive = city.IsActive,
                CreatedDate = city.CreatedDate,
                LastModified = city.LastModified
            };
        }
    }
}