using Microsoft.EntityFrameworkCore;
using ShoeStore.Models;

namespace ShoeStore.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(
            DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<User> Users { get; set; }

        public DbSet<Category> Categories { get; set; }

        public DbSet<Brand> Brands { get; set; }

        public DbSet<Product> Products { get; set; }

        public DbSet<Size> Sizes { get; set; }

        public DbSet<Color> Colors { get; set; }

        public DbSet<ProductVariant> ProductVariants { get; set; }

        public DbSet<ProductImage> ProductImages { get; set; }

        public DbSet<Order> Orders { get; set; }

        public DbSet<OrderDetail> OrderDetails { get; set; }

        public DbSet<CartItem> CartItems { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    base.OnModelCreating(modelBuilder);

    // Cấu hình kiểu dữ liệu cho tiền
    modelBuilder.Entity<Product>()
        .Property(x => x.Price)
        .HasPrecision(18, 2);

    modelBuilder.Entity<Order>()
        .Property(x => x.TotalAmount)
        .HasPrecision(18, 2);

    modelBuilder.Entity<OrderDetail>()
        .Property(x => x.Price)
        .HasPrecision(18, 2);

    // Không cho phép trùng Product + Size + Color
    modelBuilder.Entity<ProductVariant>()
        .HasIndex(x => new
        {
            x.ProductId,
            x.SizeId,
            x.ColorId
        })
        .IsUnique();

    // Username không được trùng
    modelBuilder.Entity<User>()
        .HasIndex(x => x.Username)
        .IsUnique();

    // Email không được trùng
    modelBuilder.Entity<User>()
        .HasIndex(x => x.Email)
        .IsUnique();
}
    }
}