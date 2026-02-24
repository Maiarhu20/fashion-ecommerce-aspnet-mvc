using Core.DTOs.HomeMedia;
using Domain.Models;
using Infrastructure.Interfaces;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using System;
using System.IO;
using System.Linq;

namespace Core.Services
{
    public class HomeMediaService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IWebHostEnvironment _webHostEnvironment;
        private const string UploadsFolder = "uploads/home-media";
        private readonly string[] AllowedImageExtensions = { ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp" };
        private readonly string[] AllowedVideoExtensions = { ".mp4", ".mov", ".avi", ".wmv", ".webm" };
        private readonly long MaxFileSize = 50 * 1024 * 1024; // 50MB

        public HomeMediaService(IUnitOfWork unitOfWork, IWebHostEnvironment webHostEnvironment)
        {
            _unitOfWork = unitOfWork;
            _webHostEnvironment = webHostEnvironment;
        }

        private string SaveMediaFile(IFormFile file, MediaType mediaType)
        {
            if (file == null || file.Length == 0)
                return string.Empty;

            // Validate file size
            if (file.Length > MaxFileSize)
                throw new InvalidOperationException($"File size exceeds {MaxFileSize / (1024 * 1024)}MB limit.");

            // Validate extension
            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();

            if (mediaType == MediaType.Image && !AllowedImageExtensions.Contains(extension))
                throw new InvalidOperationException($"Invalid image format. Allowed: {string.Join(", ", AllowedImageExtensions)}");

            if (mediaType == MediaType.Video && !AllowedVideoExtensions.Contains(extension))
                throw new InvalidOperationException($"Invalid video format. Allowed: {string.Join(", ", AllowedVideoExtensions)}");

            // Create unique filename
            var fileName = $"{Guid.NewGuid()}{extension}";
            var uploadsPath = Path.Combine(_webHostEnvironment.WebRootPath, UploadsFolder);

            // Create directory if it doesn't exist
            if (!Directory.Exists(uploadsPath))
                Directory.CreateDirectory(uploadsPath);

            var filePath = Path.Combine(uploadsPath, fileName);

            // Save file
            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                file.CopyTo(stream);
            }

            return $"/{UploadsFolder}/{fileName}";
        }

        private void DeleteMediaFile(string mediaUrl)
        {
            if (string.IsNullOrEmpty(mediaUrl) || !mediaUrl.StartsWith($"/{UploadsFolder}/"))
                return;

            var filePath = Path.Combine(_webHostEnvironment.WebRootPath, mediaUrl.TrimStart('/'));

            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }

        public async Task<ServiceResult<IEnumerable<HomeMediaListDto>>> GetAllAsync()
        {
            try
            {
                var media = await _unitOfWork.HomeMedia.GetAllAsync();
                var orderedMedia = media
                    .OfType<HomeMedia>()
                    .OrderBy(m => m.DisplayOrder)
                    .ThenByDescending(m => m.CreatedAt)
                    .ToList();

                var result = orderedMedia.Select(m => new HomeMediaListDto
                {
                    Id = m.Id,
                    Title = m.Title,
                    Description = m.Description,
                    MediaUrl = m.MediaUrl,
                    MediaType = m.MediaType.ToString(),
                    ButtonText = m.ButtonText,
                    ButtonLink = m.ButtonLink,
                    DisplayOrder = m.DisplayOrder,
                    IsActive = m.IsActive,
                    CreatedAt = m.CreatedAt
                });

                return ServiceResult<IEnumerable<HomeMediaListDto>>.Success(result);
            }
            catch (Exception ex)
            {
                return ServiceResult<IEnumerable<HomeMediaListDto>>.Failure($"Error retrieving home media: {ex.Message}", ex);
            }
        }

        public async Task<ServiceResult<IEnumerable<HomeMediaListDto>>> GetActiveCarouselItemsAsync()
        {
            try
            {
                var media = await _unitOfWork.HomeMedia.FindAsync(
                    m => m.IsActive && (m.MediaType == MediaType.Image || m.MediaType == MediaType.Video),
                    null
                );

                var orderedMedia = media
                    .OfType<HomeMedia>()
                    .OrderBy(m => m.DisplayOrder)
                    .ThenByDescending(m => m.CreatedAt)
                    .ToList();

                var result = orderedMedia.Select(m => new HomeMediaListDto
                {
                    Id = m.Id,
                    Title = m.Title,
                    Description = m.Description,
                    MediaUrl = m.MediaUrl,
                    MediaType = m.MediaType.ToString(),
                    ButtonText = m.ButtonText,
                    ButtonLink = m.ButtonLink,
                    DisplayOrder = m.DisplayOrder,
                    IsActive = m.IsActive,
                    CreatedAt = m.CreatedAt
                });

                return ServiceResult<IEnumerable<HomeMediaListDto>>.Success(result);
            }
            catch (Exception ex)
            {
                return ServiceResult<IEnumerable<HomeMediaListDto>>.Failure($"Error retrieving carousel items: {ex.Message}", ex);
            }
        }

        public async Task<ServiceResult<HomeMediaDto>> GetByIdAsync(int id)
        {
            try
            {
                var media = await _unitOfWork.HomeMedia.GetByIdAsync(id) as HomeMedia;
                if (media == null)
                {
                    return ServiceResult<HomeMediaDto>.Failure("Home media not found");
                }

                var result = new HomeMediaDto
                {
                    Id = media.Id,
                    Title = media.Title,
                    Description = media.Description,
                    MediaUrl = media.MediaUrl,
                    MediaType = media.MediaType.ToString(),
                    ButtonText = media.ButtonText,
                    ButtonLink = media.ButtonLink,
                    DisplayOrder = media.DisplayOrder,
                    IsActive = media.IsActive
                };

                return ServiceResult<HomeMediaDto>.Success(result);
            }
            catch (Exception ex)
            {
                return ServiceResult<HomeMediaDto>.Failure($"Error retrieving home media: {ex.Message}", ex);
            }
        }

        public async Task<ServiceResult<HomeMediaDto>> CreateAsync(CreateHomeMediaDto dto)
        {
            try
            {
                if (!Enum.TryParse<MediaType>(dto.MediaType, out var mediaType))
                {
                    return ServiceResult<HomeMediaDto>.Failure("Invalid media type");
                }

                if (dto.MediaFile == null || dto.MediaFile.Length == 0)
                {
                    return ServiceResult<HomeMediaDto>.Failure("Please select a media file to upload");
                }

                var mediaUrl = SaveMediaFile(dto.MediaFile, mediaType);

                var media = new HomeMedia
                {
                    Title = dto.Title,
                    Description = dto.Description,
                    MediaUrl = mediaUrl,
                    MediaType = mediaType,
                    ButtonText = dto.ButtonText,
                    ButtonLink = dto.ButtonLink,
                    DisplayOrder = dto.DisplayOrder,
                    IsActive = dto.IsActive,
                    CreatedAt = DateTime.UtcNow
                };

                await _unitOfWork.HomeMedia.AddAsync(media);
                await _unitOfWork.SaveAsync();

                var result = new HomeMediaDto
                {
                    Id = media.Id,
                    Title = media.Title,
                    Description = media.Description,
                    MediaUrl = media.MediaUrl,
                    MediaType = media.MediaType.ToString(),
                    ButtonText = media.ButtonText,
                    ButtonLink = media.ButtonLink,
                    DisplayOrder = media.DisplayOrder,
                    IsActive = media.IsActive
                };

                return ServiceResult<HomeMediaDto>.Success(result);
            }
            catch (Exception ex)
            {
                return ServiceResult<HomeMediaDto>.Failure($"Error creating home media: {ex.Message}", ex);
            }
        }

        public async Task<ServiceResult<HomeMediaDto>> UpdateAsync(int id, UpdateHomeMediaDto dto)
        {
            try
            {
                var media = await _unitOfWork.HomeMedia.GetByIdAsync(id) as HomeMedia;
                if (media == null)
                {
                    return ServiceResult<HomeMediaDto>.Failure("Home media not found");
                }

                if (!Enum.TryParse<MediaType>(dto.MediaType, out var mediaType))
                {
                    return ServiceResult<HomeMediaDto>.Failure("Invalid media type");
                }

                // Handle file upload
                string mediaUrl = media.MediaUrl;
                if (dto.MediaFile != null && dto.MediaFile.Length > 0)
                {
                    // Delete old file if it exists
                    if (!string.IsNullOrEmpty(media.MediaUrl))
                    {
                        DeleteMediaFile(media.MediaUrl);
                    }

                    // Save new file
                    mediaUrl = SaveMediaFile(dto.MediaFile, mediaType);
                }

                media.Title = dto.Title;
                media.Description = dto.Description;
                media.MediaUrl = mediaUrl;
                media.MediaType = mediaType;
                media.ButtonText = dto.ButtonText;
                media.ButtonLink = dto.ButtonLink;
                media.DisplayOrder = dto.DisplayOrder;
                media.IsActive = dto.IsActive;
                media.UpdatedAt = DateTime.UtcNow;

                _unitOfWork.HomeMedia.Update(media);
                await _unitOfWork.SaveAsync();

                var result = new HomeMediaDto
                {
                    Id = media.Id,
                    Title = media.Title,
                    Description = media.Description,
                    MediaUrl = media.MediaUrl,
                    MediaType = media.MediaType.ToString(),
                    ButtonText = media.ButtonText,
                    ButtonLink = media.ButtonLink,
                    DisplayOrder = media.DisplayOrder,
                    IsActive = media.IsActive
                };

                return ServiceResult<HomeMediaDto>.Success(result);
            }
            catch (Exception ex)
            {
                return ServiceResult<HomeMediaDto>.Failure($"Error updating home media: {ex.Message}", ex);
            }
        }

        public async Task<ServiceResult> DeleteAsync(int id)
        {
            try
            {
                var media = await _unitOfWork.HomeMedia.GetByIdAsync(id) as HomeMedia;
                if (media == null)
                {
                    return ServiceResult.Failure("Home media not found");
                }

                // Delete the associated file
                if (!string.IsNullOrEmpty(media.MediaUrl))
                {
                    DeleteMediaFile(media.MediaUrl);
                }

                _unitOfWork.HomeMedia.Delete(media);
                await _unitOfWork.SaveAsync();

                return ServiceResult.Success;
            }
            catch (Exception ex)
            {
                return ServiceResult.Failure($"Error deleting home media: {ex.Message}", ex);
            }
        }

        public async Task<ServiceResult> ToggleStatusAsync(int id)
        {
            try
            {
                var media = await _unitOfWork.HomeMedia.GetByIdAsync(id) as HomeMedia;
                if (media == null)
                {
                    return ServiceResult.Failure("Home media not found");
                }

                media.IsActive = !media.IsActive;
                media.UpdatedAt = DateTime.UtcNow;

                _unitOfWork.HomeMedia.Update(media);
                await _unitOfWork.SaveAsync();

                return ServiceResult.Success;
            }
            catch (Exception ex)
            {
                return ServiceResult.Failure($"Error toggling home media status: {ex.Message}", ex);
            }
        }
    }
}