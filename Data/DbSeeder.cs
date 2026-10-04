using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ShoeStore.Models;

namespace ShoeStore.Data
{
    // Thêm dữ liệu mẫu vào database khi ứng dụng khởi động.
    // Chạy nhiều lần cũng không bị trùng: bản ghi nào đã có (theo tên) thì bỏ qua.
    public static class DbSeeder
    {
        public static async Task SeedAsync(ApplicationDbContext db)
        {
            await SeedCategoriesAsync(db);
            await SeedBrandsAsync(db);
            await SeedSizesAsync(db);
            await SeedColorsAsync(db);
            await SeedProductsAsync(db);
            await SeedUsersAsync(db);
            await SeedOrdersAsync(db);
            await SeedCartItemsAsync(db);
        }

        // ===== Danh mục =====
        private static async Task SeedCategoriesAsync(ApplicationDbContext db)
        {
            var items = new List<Category>
            {
                new() { Name = "Sneaker", Description = "Giày thể thao phong cách, đi chơi hằng ngày" },
                new() { Name = "Chạy bộ", Description = "Giày chạy bộ êm, nhẹ, thoáng khí" },
                new() { Name = "Bóng đá", Description = "Giày đá bóng sân cỏ tự nhiên và nhân tạo" },
                new() { Name = "Bóng rổ", Description = "Giày bóng rổ cổ cao, bám sân tốt" },
                new() { Name = "Sandal & Dép", Description = "Sandal, dép quai ngang thoải mái mùa hè" }
            };

            var existing = (await db.Categories.Select(x => x.Name).ToListAsync()).ToHashSet(StringComparer.OrdinalIgnoreCase);
            db.Categories.AddRange(items.Where(x => !existing.Contains(x.Name)));
            await db.SaveChangesAsync();
        }

        // ===== Thương hiệu =====
        private static async Task SeedBrandsAsync(ApplicationDbContext db)
        {
            var items = new List<Brand>
            {
                new() { Name = "Nike", Description = "Thương hiệu thể thao hàng đầu đến từ Mỹ" },
                new() { Name = "Adidas", Description = "Thương hiệu thể thao nổi tiếng đến từ Đức" },
                new() { Name = "Puma", Description = "Thương hiệu thể thao đến từ Đức" },
                new() { Name = "New Balance", Description = "Thương hiệu giày chạy bộ và lifestyle đến từ Mỹ" },
                new() { Name = "Converse", Description = "Giày canvas cổ điển đến từ Mỹ" }
            };

            var existing = (await db.Brands.Select(x => x.Name).ToListAsync()).ToHashSet(StringComparer.OrdinalIgnoreCase);
            db.Brands.AddRange(items.Where(x => !existing.Contains(x.Name)));
            await db.SaveChangesAsync();
        }

        // ===== Size =====
        private static async Task SeedSizesAsync(ApplicationDbContext db)
        {
            var names = new[] { "39", "40", "41", "42", "43" };

            var existing = (await db.Sizes.Select(x => x.Name).ToListAsync()).ToHashSet(StringComparer.OrdinalIgnoreCase);
            db.Sizes.AddRange(names.Where(n => !existing.Contains(n)).Select(n => new Size { Name = n }));
            await db.SaveChangesAsync();
        }

        // ===== Màu =====
        private static async Task SeedColorsAsync(ApplicationDbContext db)
        {
            var names = new[] { "Trắng", "Đen", "Xám", "Đỏ", "Xanh navy" };

            var existing = (await db.Colors.Select(x => x.Name).ToListAsync()).ToHashSet(StringComparer.OrdinalIgnoreCase);
            db.Colors.AddRange(names.Where(n => !existing.Contains(n)).Select(n => new Color { Name = n }));
            await db.SaveChangesAsync();
        }

        // ===== Sản phẩm + biến thể (size, màu, số lượng) =====
        private static async Task SeedProductsAsync(ApplicationDbContext db)
        {
            // Tra Id theo tên (không phân biệt hoa thường, nếu trùng tên thì lấy bản ghi đầu tiên)
            var categories = ToIdLookup(await db.Categories.Select(x => new { x.Name, x.Id }).ToListAsync(), x => x.Name, x => x.Id);
            var brands = ToIdLookup(await db.Brands.Select(x => new { x.Name, x.Id }).ToListAsync(), x => x.Name, x => x.Id);
            var sizes = ToIdLookup(await db.Sizes.Select(x => new { x.Name, x.Id }).ToListAsync(), x => x.Name, x => x.Id);
            var colors = ToIdLookup(await db.Colors.Select(x => new { x.Name, x.Id }).ToListAsync(), x => x.Name, x => x.Id);

            // Mỗi sản phẩm có 5 biến thể: (size, màu, số lượng tồn)
            var data = new[]
            {
                new
                {
                    Name = "Nike Air Force 1 '07", Brand = "Nike", Category = "Sneaker", Price = 2_929_000m, DaysAgo = 2,
                    Description = "Mẫu sneaker kinh điển với đế Air êm ái, da trơn dễ phối đồ.",
                    Variants = new[] { ("39", "Trắng", 12), ("40", "Trắng", 15), ("41", "Trắng", 10), ("42", "Đen", 8), ("43", "Đen", 5) }
                },
                new
                {
                    Name = "Adidas Ultraboost Light", Brand = "Adidas", Category = "Chạy bộ", Price = 4_200_000m, DaysAgo = 5,
                    Description = "Giày chạy bộ với đệm Boost nhẹ hơn, hoàn trả năng lượng tốt.",
                    Variants = new[] { ("39", "Đen", 6), ("40", "Đen", 4), ("41", "Xám", 7), ("42", "Xám", 3), ("43", "Trắng", 2) }
                },
                new
                {
                    Name = "Puma Future 7 Match FG", Brand = "Puma", Category = "Bóng đá", Price = 2_390_000m, DaysAgo = 9,
                    Description = "Giày đá bóng sân cỏ tự nhiên, upper ôm chân, kiểm soát bóng tốt.",
                    Variants = new[] { ("39", "Đỏ", 5), ("40", "Đỏ", 6), ("41", "Đỏ", 4), ("42", "Xanh navy", 3), ("43", "Xanh navy", 2) }
                },
                new
                {
                    Name = "New Balance 530", Brand = "New Balance", Category = "Sneaker", Price = 2_790_000m, DaysAgo = 20,
                    Description = "Phong cách chạy bộ retro thập niên 2000, lưới thoáng phối da lộn.",
                    Variants = new[] { ("39", "Trắng", 9), ("40", "Trắng", 11), ("41", "Xám", 8), ("42", "Xám", 6), ("43", "Xanh navy", 4) }
                },
                new
                {
                    Name = "Converse Chuck Taylor All Star High", Brand = "Converse", Category = "Sneaker", Price = 1_650_000m, DaysAgo = 30,
                    Description = "Giày canvas cổ cao huyền thoại, bền bỉ và dễ phối đồ.",
                    Variants = new[] { ("39", "Đen", 14), ("40", "Đen", 12), ("41", "Trắng", 10), ("42", "Trắng", 9), ("43", "Đỏ", 5) }
                }
            };

            var existing = (await db.Products.Select(x => x.Name).ToListAsync()).ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (var d in data.Where(d => !existing.Contains(d.Name)))
            {
                var product = new Product
                {
                    Name = d.Name,
                    Description = d.Description,
                    Price = d.Price,
                    Status = true,
                    CreatedAt = DateTime.Now.AddDays(-d.DaysAgo),
                    BrandId = brands[d.Brand],
                    CategoryId = categories[d.Category]
                };

                foreach (var (size, color, quantity) in d.Variants)
                {
                    product.ProductVariants.Add(new ProductVariant
                    {
                        SizeId = sizes[size],
                        ColorId = colors[color],
                        Quantity = quantity
                    });
                }

                db.Products.Add(product);
            }

            await db.SaveChangesAsync();
        }

        // ===== Người dùng (mật khẩu chung: 123456) =====
        private static async Task SeedUsersAsync(ApplicationDbContext db)
        {
            var users = new List<User>
            {
                new() { Username = "admin", Email = "admin@shoestore.vn", FullName = "Quản trị viên", Phone = "0900000001", Address = "1 Lê Lợi, Quận 1, TP.HCM", Role = "Admin" },
                new() { Username = "nguyenvana", Email = "nguyenvana@gmail.com", FullName = "Nguyễn Văn A", Phone = "0912345678", Address = "25 Trần Hưng Đạo, Hoàn Kiếm, Hà Nội" },
                new() { Username = "tranthib", Email = "tranthib@gmail.com", FullName = "Trần Thị B", Phone = "0987654321", Address = "102 Nguyễn Văn Linh, Hải Châu, Đà Nẵng" },
                new() { Username = "levanc", Email = "levanc@gmail.com", FullName = "Lê Văn C", Phone = "0934567890", Address = "45 Võ Văn Tần, Quận 3, TP.HCM" },
                new() { Username = "phamthid", Email = "phamthid@gmail.com", FullName = "Phạm Thị D", Phone = "0978123456", Address = "8 Lạch Tray, Ngô Quyền, Hải Phòng" }
            };

            var hasher = new PasswordHasher<User>();
            var existing = (await db.Users.Select(x => x.Username).ToListAsync()).ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (var user in users.Where(u => !existing.Contains(u.Username)))
            {
                user.PasswordHash = hasher.HashPassword(user, "123456");
                db.Users.Add(user);
            }

            await db.SaveChangesAsync();
        }

        // ===== Đơn hàng + chi tiết đơn (chỉ thêm khi bảng Orders đang trống) =====
        private static async Task SeedOrdersAsync(ApplicationDbContext db)
        {
            if (await db.Orders.AnyAsync()) return;

            var customers = await db.Users
                .Where(u => u.Role == "Customer")
                .OrderBy(u => u.Id)
                .Take(4)
                .ToListAsync();

            var variants = await db.ProductVariants
                .Include(v => v.Product)
                .OrderBy(v => v.Id)
                .ToListAsync();

            if (customers.Count == 0 || variants.Count < 5) return;

            // (khách thứ mấy, trạng thái, số ngày trước, các dòng: (biến thể thứ mấy, số lượng))
            var data = new[]
            {
                (0, "Pending",   1,  new[] { (0, 1) }),
                (1, "Confirmed", 3,  new[] { (5, 1), (10, 1) }),
                (2, "Shipping",  6,  new[] { (15, 2) }),
                (3, "Completed", 12, new[] { (20, 1), (1, 1) }),
                (0, "Cancelled", 15, new[] { (6, 1) })
            };

            foreach (var (customerIndex, status, daysAgo, lines) in data)
            {
                var customer = customers[customerIndex % customers.Count];

                var order = new Order
                {
                    UserId = customer.Id,
                    OrderDate = DateTime.Now.AddDays(-daysAgo),
                    Status = status,
                    ShippingAddress = customer.Address ?? "Chưa có địa chỉ",
                    Phone = customer.Phone
                };

                foreach (var (variantIndex, quantity) in lines)
                {
                    var variant = variants[variantIndex % variants.Count];
                    order.OrderDetails.Add(new OrderDetail
                    {
                        ProductVariantId = variant.Id,
                        Quantity = quantity,
                        Price = variant.Product!.Price
                    });
                }

                order.TotalAmount = order.OrderDetails.Sum(x => x.Price * x.Quantity);
                db.Orders.Add(order);
            }

            await db.SaveChangesAsync();
        }

        // ===== Giỏ hàng (chỉ thêm khi bảng CartItems đang trống) =====
        private static async Task SeedCartItemsAsync(ApplicationDbContext db)
        {
            if (await db.CartItems.AnyAsync()) return;

            var customers = await db.Users
                .Where(u => u.Role == "Customer")
                .OrderBy(u => u.Id)
                .Take(4)
                .ToListAsync();

            var variants = await db.ProductVariants.OrderBy(v => v.Id).ToListAsync();

            if (customers.Count == 0 || variants.Count < 5) return;

            // (khách thứ mấy, biến thể thứ mấy, số lượng)
            var data = new[] { (0, 2, 1), (0, 7, 1), (1, 12, 2), (2, 17, 1), (3, 22, 1) };

            foreach (var (customerIndex, variantIndex, quantity) in data)
            {
                db.CartItems.Add(new CartItem
                {
                    UserId = customers[customerIndex % customers.Count].Id,
                    ProductVariantId = variants[variantIndex % variants.Count].Id,
                    Quantity = quantity
                });
            }

            await db.SaveChangesAsync();
        }

        private static Dictionary<string, int> ToIdLookup<T>(List<T> rows, Func<T, string> name, Func<T, int> id)
        {
            return rows
                .GroupBy(name, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => id(g.First()), StringComparer.OrdinalIgnoreCase);
        }
    }
}
