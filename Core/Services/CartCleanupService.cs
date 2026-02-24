using Infrastructure.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Core.Services
{
    public class CartCleanupService : BackgroundService
    {
        private readonly ILogger<CartCleanupService> _logger;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly TimeSpan _cleanupInterval = TimeSpan.FromHours(6); // Run every 6 hours
        private readonly int _abandonedCartDays = 7;

        public CartCleanupService(
            ILogger<CartCleanupService> logger,
            IServiceScopeFactory scopeFactory)
        {
            _logger = logger;
            _scopeFactory = scopeFactory;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Cart Cleanup Service started.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await CleanupAbandonedCarts(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during cart cleanup");
                }

                await Task.Delay(_cleanupInterval, stoppingToken);
            }
        }

        private async Task CleanupAbandonedCarts(CancellationToken cancellationToken)
        {
            using var scope = _scopeFactory.CreateScope();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            var cutoffDate = DateTime.UtcNow.AddDays(-_abandonedCartDays);

            // Find carts older than cutoff date
            var oldCarts = await unitOfWork.Carts
                .FindAsync(c => c.CreatedDate < cutoffDate &&
                               (!c.LastModified.HasValue || c.LastModified.Value < cutoffDate));

            int deletedCount = 0;
            foreach (var cart in oldCarts)
            {
                try
                {
                    // Delete cart items first (cascade delete should handle this)
                    unitOfWork.Carts.Delete(cart);
                    deletedCount++;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to delete cart {CartId}", cart.Id);
                }
            }

            if (deletedCount > 0)
            {
                await unitOfWork.SaveAsync(cancellationToken);
                _logger.LogInformation("Deleted {Count} abandoned carts", deletedCount);
            }
        }
    }
}