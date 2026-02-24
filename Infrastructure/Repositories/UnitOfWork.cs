using Domain.Models;
using Infrastructure.Data;
using Infrastructure.Interfaces;
using Microsoft.EntityFrameworkCore.Storage;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Infrastructure.Repositories
{

    public class UnitOfWork : IUnitOfWork
    {
        private readonly AppDbContext _context;

        public IGenericRepository<Product> Products { get; }
        public IGenericRepository<ProductImage> ProductImages { get; }
        public IGenericRepository<ProductColor> ProductColors { get; }
        public IGenericRepository<Category> Categories { get; }
        //public IGenericRepository<Image> Images { get; }
        public IGenericRepository<HomeMedia> HomeMedia { get; }
        public IGenericRepository<Cart> Carts { get; }
        public IGenericRepository<CartItem> CartItems { get; }
        public IGenericRepository<Order> Orders { get; }
        public IGenericRepository<OrderItem> OrderItems { get; }
        public IGenericRepository<Review> Reviews { get; }
        public IGenericRepository<Payment> Payments { get; }
        public IGenericRepository<Admin> AdminUsers { get; }
        public IGenericRepository<Discount> Discounts { get; }
        public IGenericRepository<DiscountUsage> DiscountUsages { get; private set; }
        public IGenericRepository<ShippingCity> ShippingCities { get; }

        public UnitOfWork(AppDbContext context)
        {
            _context = context;

            Products = new GenericRepository<Product>(context);
            ProductImages = new GenericRepository<ProductImage>(context);
            ProductColors = new GenericRepository<ProductColor>(context);
            Categories = new GenericRepository<Category>(context);
            //Images = new GenericRepository<Image>(context);
            HomeMedia = new GenericRepository<HomeMedia>(context);
            Carts = new GenericRepository<Cart>(context);
            CartItems = new GenericRepository<CartItem>(context);
            Orders = new GenericRepository<Order>(context);
            OrderItems = new GenericRepository<OrderItem>(context);
            Reviews = new GenericRepository<Review>(context);
            Payments = new GenericRepository<Payment>(context);
            AdminUsers = new GenericRepository<Admin>(context);
            Discounts = new GenericRepository<Discount>(context);
            DiscountUsages = new GenericRepository<DiscountUsage>(context);
            ShippingCities = new GenericRepository<ShippingCity>(context);
        }

        public async Task<int> SaveAsync()
        {
            return await _context.SaveChangesAsync();
        }

        public async Task<int> SaveAsync(CancellationToken cancellationToken = default)
        {
            return await _context.SaveChangesAsync(cancellationToken);
        }

        public void Dispose()
        {
            _context.Dispose();
        }

        public async Task<IDbContextTransaction> BeginTransactionAsync()
        {
            return await _context.Database.BeginTransactionAsync();
        }
    }
}
