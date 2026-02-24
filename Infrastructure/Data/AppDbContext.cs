using Domain.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Data
{
    public class AppDbContext : IdentityDbContext<User>
    {
        public AppDbContext(DbContextOptions<AppDbContext> options)
            : base(options)
        {
        }

        public DbSet<Product> Products { get; set; } = null!;
        public DbSet<Category> Categories { get; set; } = null!;
        public DbSet<ProductColor> ProductColors { get; set; } = null!;
        public DbSet<ProductImage> ProductImages { get; set; } = null!;
        public DbSet<Cart> Carts { get; set; } = null!;
        public DbSet<CartItem> CartItems { get; set; } = null!;
        public DbSet<Order> Orders { get; set; } = null!;
        public DbSet<OrderItem> OrderItems { get; set; } = null!;
        public DbSet<Payment> Payments { get; set; } = null!;
        public DbSet<Review> Reviews { get; set; } = null!;
        //public DbSet<Image> Images { get; set; } = null!;
        public DbSet<HomeMedia> HomeMedias { get; set; } = null!;
        public DbSet<Admin> Admins { get; set; } = null!;
        public DbSet<Discount> Discounts { get; set; } = null!;
        public DbSet<DiscountUsage> DiscountUsages { get; set; } = null!;
        public DbSet<ShippingCity> ShippingCities { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // ============================================================
            // CATEGORY → PRODUCTS (1-to-Many)
            // ============================================================
            modelBuilder.Entity<Category>()
                .HasMany(c => c.Products)
                .WithOne(p => p.Category)
                .HasForeignKey(p => p.CategoryId)
                .OnDelete(DeleteBehavior.Restrict);

            // ============================================================
            // PRODUCT → ORDER ITEMS (1-to-Many)
            // ============================================================
            modelBuilder.Entity<Product>()
                .HasMany(p => p.OrderItems)
                .WithOne(oi => oi.Product)
                .HasForeignKey(oi => oi.ProductId)
                .OnDelete(DeleteBehavior.Restrict);

            // ============================================================
            // PRODUCT → REVIEWS (1-to-Many)
            // ============================================================
            modelBuilder.Entity<Product>()
                .HasMany(p => p.Reviews)
                .WithOne(r => r.Product)
                .HasForeignKey(r => r.ProductId)
                .OnDelete(DeleteBehavior.Cascade);

            // ============================================================
            // PRODUCT → PRODUCT COLORS (1-to-Many)
            // ============================================================
            modelBuilder.Entity<Product>()
                .HasMany(p => p.Colors)
                .WithOne(c => c.Product)
                .HasForeignKey(c => c.ProductId)
                .OnDelete(DeleteBehavior.Cascade);

            // ============================================================
            // PRODUCT → PRODUCT IMAGES (1-to-Many)
            // ============================================================
            modelBuilder.Entity<Product>()
                .HasMany(p => p.Images)
                .WithOne(pi => pi.Product)
                .HasForeignKey(pi => pi.ProductId)
                .OnDelete(DeleteBehavior.Cascade);

            // ============================================================
            // CART → CART ITEMS (1-to-Many)
            // ============================================================
            modelBuilder.Entity<Cart>()
                .HasMany(c => c.Items)
                .WithOne(i => i.Cart)
                .HasForeignKey(i => i.CartId)
                .OnDelete(DeleteBehavior.Cascade);

            // ============================================================
            // ORDER → ORDER ITEMS (1-to-Many)
            // ============================================================
            modelBuilder.Entity<Order>()
                .HasMany(o => o.OrderItems)
                .WithOne(oi => oi.Order)
                .HasForeignKey(oi => oi.OrderId)
                .OnDelete(DeleteBehavior.Cascade);

            // ============================================================
            // ORDER → PAYMENT (1-to-1)
            // ============================================================
            modelBuilder.Entity<Order>()
                .HasOne(o => o.Payment)
                .WithOne(p => p.Order)
                .HasForeignKey<Payment>(p => p.OrderId)
                .OnDelete(DeleteBehavior.Cascade);

            // ============================================================
            // CART ITEM → PRODUCT (Many-to-1)
            // ============================================================
            modelBuilder.Entity<CartItem>()
                .HasOne(ci => ci.Product)
                .WithMany()
                .HasForeignKey(ci => ci.ProductId)
                .OnDelete(DeleteBehavior.Restrict);

            // ============================================================
            // ADMIN → USER (1-to-1)
            // ============================================================
            modelBuilder.Entity<Admin>()
                .HasOne(a => a.User)
                .WithOne(u => u.Admin)
                .HasForeignKey<Admin>(a => a.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // ============================================================
            // ORDER → SHIPPING CITY (Many-to-1)
            // ============================================================
            modelBuilder.Entity<Order>()
                .HasOne(o => o.ShippingCity)
                .WithMany()
                .HasForeignKey(o => o.ShippingCityId)
                .OnDelete(DeleteBehavior.SetNull);

            // ============================================================
            // DISCOUNT → ORDERS (1-to-Many)
            // ============================================================
            modelBuilder.Entity<Discount>()
                .HasMany(d => d.Orders)
                .WithOne(o => o.AppliedDiscount)
                .HasForeignKey(o => o.AppliedDiscountId)
                .OnDelete(DeleteBehavior.SetNull);

            // ============================================================
            // DISCOUNT → DISCOUNT USAGES (1-to-Many) - NEW
            // ============================================================
            modelBuilder.Entity<Discount>()
                .HasMany(d => d.Usages)
                .WithOne(du => du.Discount)
                .HasForeignKey(du => du.DiscountId)
                .OnDelete(DeleteBehavior.Cascade);

            // ============================================================
            // UNIQUE CONSTRAINTS
            // ============================================================

            modelBuilder.Entity<Cart>()
                .HasIndex(c => c.SessionId)
                .IsUnique();

            modelBuilder.Entity<Order>()
                .HasIndex(o => o.OrderNumber)
                .IsUnique();

            modelBuilder.Entity<Discount>()
                .HasIndex(d => d.Code)
                .IsUnique();

            modelBuilder.Entity<ShippingCity>()
                .HasIndex(sc => sc.CityName)
                .IsUnique();

            // ============================================================
            // DISCOUNT USAGE INDEXES - NEW
            // ============================================================
            modelBuilder.Entity<DiscountUsage>(entity =>
            {
                // Unique constraint: one record per discount + session combination
                entity.HasIndex(e => new { e.DiscountId, e.SessionId })
                    .IsUnique();

                // Speed up lookups by session
                entity.HasIndex(e => e.SessionId);

                // Speed up lookups by email (for future: track by email too)
                entity.HasIndex(e => e.GuestEmail);

                // Speed up lookups by discount
                entity.HasIndex(e => e.DiscountId);
            });


            // Add configuration for HomeMedia
            modelBuilder.Entity<HomeMedia>(entity =>
            {
                // Indexes for performance
                entity.HasIndex(e => e.IsActive);
                entity.HasIndex(e => e.DisplayOrder);
                entity.HasIndex(e => e.MediaType);
                entity.HasIndex(e => new { e.IsActive, e.DisplayOrder });
                entity.HasIndex(e => e.CreatedAt);

                // Data validation configuration
                entity.Property(e => e.Title)
                    .IsRequired()
                    .HasMaxLength(255);

                entity.Property(e => e.MediaUrl)
                    .IsRequired()
                    .HasMaxLength(500);

                entity.Property(e => e.ButtonText)
                    .HasMaxLength(100);

                entity.Property(e => e.ButtonLink)
                    .HasMaxLength(500);

                entity.Property(e => e.Description)
                    .HasMaxLength(500);

                entity.Property(e => e.MediaType)
                    .HasConversion<string>()
                    .HasMaxLength(50);
            });

            // ============================================================
            // PERFORMANCE INDEXES
            // ============================================================

            modelBuilder.Entity<Product>()
                .HasIndex(p => p.CategoryId);

            modelBuilder.Entity<Product>()
                .HasIndex(p => p.IsDeleted);

            modelBuilder.Entity<Review>()
                .HasIndex(r => r.ProductId);

            modelBuilder.Entity<Review>()
                .HasIndex(r => r.Status);

            modelBuilder.Entity<Order>()
                .HasIndex(o => o.Status);

            modelBuilder.Entity<Order>()
                .HasIndex(o => o.ShippingCityId);

            modelBuilder.Entity<Category>()
                .HasIndex(c => c.IsDeleted);

            modelBuilder.Entity<ProductImage>()
                .HasIndex(pi => pi.ProductId);

            modelBuilder.Entity<ProductImage>()
                .HasIndex(pi => new { pi.ProductId, pi.IsPrimary });

            modelBuilder.Entity<ProductImage>()
                .HasIndex(pi => new { pi.ProductId, pi.DisplayOrder });

            modelBuilder.Entity<ProductColor>()
                .HasIndex(pc => pc.ProductId);

            modelBuilder.Entity<ShippingCity>()
                .HasIndex(sc => sc.IsActive);

            modelBuilder.Entity<ShippingCity>()
                .HasIndex(sc => sc.CityName);
        }
    }
}