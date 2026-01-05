using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Ecomm.Models;
using Microsoft.AspNetCore.Identity;

namespace Ecomm.Data
{
    public class ApplicationDbContext : IdentityDbContext<IdentityUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<Product> Products { get; set; }
        public DbSet<Category> Categories { get; set; }
        public DbSet<CartItem> CartItems { get; set; }
        public DbSet<Order> Orders { get; set; }
        public DbSet<OrderItem> OrderItems { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder); //important for Identity

            // Configure Order entity
            modelBuilder.Entity<Order>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.TotalAmount)
                      .HasColumnType("decimal(18,2)");
                entity.Property(e => e.Status)
                      .HasDefaultValue("Processing")
                      .HasMaxLength(50);
                entity.Property(e => e.PaymentMethod)
                      .HasMaxLength(100);
                entity.Property(e => e.TransactionId)
                      .HasMaxLength(255);
                entity.Property(e => e.FullName)
                      .HasMaxLength(255);
                entity.Property(e => e.Email)
                      .HasMaxLength(255);
                entity.Property(e => e.Phone)
                      .HasMaxLength(50);
                entity.Property(e => e.City)
                      .HasMaxLength(100);
                entity.Property(e => e.ZipCode)
                      .HasMaxLength(20);
            });

            // Configure OrderItem entity
            modelBuilder.Entity<OrderItem>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Price)
                      .HasColumnType("decimal(18,2)");
            });

            // Existing relationships configurations
            modelBuilder.Entity<Product>()
                .HasOne(p => p.Category)
                .WithMany(c => c.Products)
                .HasForeignKey(p => p.CategoryId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<CartItem>()
                .HasOne(ci => ci.Product)
                .WithMany()
                .HasForeignKey(ci => ci.ProductId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<OrderItem>()
                .HasOne(oi => oi.Order)
                .WithMany(o => o.OrderItems)
                .HasForeignKey(oi => oi.OrderId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<OrderItem>()
                .HasOne(oi => oi.Product)
                .WithMany()
                .HasForeignKey(oi => oi.ProductId)
                .OnDelete(DeleteBehavior.Restrict);

            // Existing seed data
            modelBuilder.Entity<Category>().HasData(
                new Category { Id = 1, Name = "Electronics" },
                new Category { Id = 2, Name = "Clothing" },
                new Category { Id = 3, Name = "Books" },
                new Category { Id = 4, Name = "Home & Garden" }
            );

            modelBuilder.Entity<Product>().HasData(
                new Product
                {
                    Id = 1,
                    Name = "Wireless Headphones",
                    Description = "High-quality wireless headphones with noise cancellation",
                    Price = 99.99m,
                    StockCount = 25,
                    CategoryId = 1,
                    ImageUrl = "/images/products/headphones.jpg",
                    IsOnSale = true
                },
                new Product
                {
                    Id = 2,
                    Name = "Smartphone",
                    Description = "Latest smartphone with advanced features",
                    Price = 699.99m,
                    StockCount = 15,
                    CategoryId = 1,
                    ImageUrl = "/images/products/smartphone.jpg"
                },
                new Product
                {
                    Id = 3,
                    Name = "Cotton T-Shirt",
                    Description = "Comfortable cotton t-shirt in various colors",
                    Price = 19.99m,
                    StockCount = 3,
                    CategoryId = 2,
                    ImageUrl = "/images/products/tshirt.jpg",
                    IsOnSale = true
                },
                new Product
                {
                    Id = 4,
                    Name = "Programming Book",
                    Description = "Comprehensive guide to modern programming",
                    Price = 39.99m,
                    StockCount = 50,
                    CategoryId = 3,
                    ImageUrl = "/images/products/book.jpg"
                },
                new Product
                {
                    Id = 5,
                    Name = "Desk Lamp",
                    Description = "Modern LED desk lamp with adjustable brightness",
                    Price = 29.99m,
                    StockCount = 8,
                    CategoryId = 4,
                    ImageUrl = "/images/products/lamp.jpg"
                }
            );
        }

        internal async Task<object> SaveChangesAsync()
        {
            throw new NotImplementedException();
        }
    }
}