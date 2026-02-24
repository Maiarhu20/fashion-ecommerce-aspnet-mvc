using Domain.Models;
using Microsoft.EntityFrameworkCore.Storage;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Infrastructure.Interfaces
{
    public interface IUnitOfWork : IDisposable
    {
        IGenericRepository<Product> Products { get; }
        IGenericRepository<ProductImage> ProductImages { get; }
        IGenericRepository<ProductColor> ProductColors { get; }
        IGenericRepository<Category> Categories { get; }
        //IGenericRepository<Image> Images { get; }

        IGenericRepository<HomeMedia> HomeMedia { get; }
        IGenericRepository<Cart> Carts { get; }
        IGenericRepository<CartItem> CartItems { get; }
        IGenericRepository<Order> Orders { get; }
        IGenericRepository<OrderItem> OrderItems { get; }
        IGenericRepository<Review> Reviews { get; }
        IGenericRepository<Payment> Payments { get; }
        IGenericRepository<Admin> AdminUsers { get; }
        IGenericRepository<Discount> Discounts { get; }
        IGenericRepository<DiscountUsage> DiscountUsages { get; }
        IGenericRepository<ShippingCity> ShippingCities { get; }

        Task<int> SaveAsync();
        Task<int> SaveAsync(CancellationToken cancellationToken = default);
        Task<IDbContextTransaction> BeginTransactionAsync();
    }
}
