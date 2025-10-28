using System;
using System.Security.Cryptography;
using System.Text;
using EVCharging.BE.DAL;
using EVCharging.BE.DAL.Entities;
using Microsoft.EntityFrameworkCore;

namespace EVCharging.BE.DAL
{
    public static class DatabaseFixer
    {
        private static string HashPassword(string password)
        {
            using var sha256 = SHA256.Create();
            var hashedBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
            return Convert.ToBase64String(hashedBytes);
        }

        public static async Task FixUserPasswordsAsync()
        {
            var connectionString = "Server=.\\SQLEXPRESS;Database=EVChargingManagement;User ID=sa;Password=sa12345;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True";
            
            var options = new DbContextOptionsBuilder<EvchargingManagementContext>()
                .UseSqlServer(connectionString)
                .Options;

            using var context = new EvchargingManagementContext(options);

            // Tìm user với email chinh22@gmail.com
            var user = await context.Users.FirstOrDefaultAsync(u => u.Email == "chinh22@gmail.com");
            
            if (user != null)
            {
                // Cập nhật password đã hash
                user.Password = HashPassword("12345");
                await context.SaveChangesAsync();
                Console.WriteLine("✅ Đã cập nhật password cho user chinh22@gmail.com");
            }
            else
            {
                // Tạo user mới nếu chưa có
                var newUser = new User
                {
                    Name = "Chinh User",
                    Email = "chinh22@gmail.com",
                    Password = HashPassword("12345"),
                    Phone = "0901234567",
                    Role = "driver",
                    WalletBalance = 500000,
                    BillingType = "prepaid",
                    MembershipTier = "standard",
                    CreatedAt = DateTime.Now
                };

                context.Users.Add(newUser);
                await context.SaveChangesAsync();

                // Tạo DriverProfile cho user này
                var driverProfile = new DriverProfile
                {
                    UserId = newUser.UserId,
                    LicenseNumber = "B123456",
                    VehicleModel = "VinFast VF e34",
                    VehiclePlate = "51H-123.45",
                    BatteryCapacity = 42
                };

                context.DriverProfiles.Add(driverProfile);
                await context.SaveChangesAsync();
                
                Console.WriteLine("✅ Đã tạo user mới chinh22@gmail.com với password 12345");
            }

            // Cập nhật tất cả user có password plain text
            var usersWithPlainPassword = await context.Users
                .Where(u => u.Password.Length < 50) // Password đã hash sẽ dài hơn 50 ký tự
                .ToListAsync();

            foreach (var u in usersWithPlainPassword)
            {
                if (u.Password == "123456")
                {
                    u.Password = HashPassword("123456");
                }
            }

            if (usersWithPlainPassword.Any())
            {
                await context.SaveChangesAsync();
                Console.WriteLine($"✅ Đã cập nhật {usersWithPlainPassword.Count} user có password plain text");
            }

            Console.WriteLine("🎉 Hoàn thành fix database!");
        }
    }
}
