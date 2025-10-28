using EVCharging.BE.DAL;
using EVCharging.BE.DAL.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;

namespace EVCharging.BE.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class DatabaseFixController : ControllerBase
    {
        private readonly EvchargingManagementContext _db;

        public DatabaseFixController(EvchargingManagementContext db)
        {
            _db = db;
        }

        [HttpPost("fix-passwords")]
        public async Task<IActionResult> FixPasswords()
        {
            try
            {
                var results = new List<string>();

                // Hash function
                string HashPassword(string password)
                {
                    using var sha256 = SHA256.Create();
                    var hashedBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
                    return Convert.ToBase64String(hashedBytes);
                }

                // Tìm user với email chinh22@gmail.com
                var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == "chinh22@gmail.com");
                
                if (user != null)
                {
                    // Cập nhật password đã hash
                    user.Password = HashPassword("12345");
                    results.Add("✅ Đã cập nhật password cho user chinh22@gmail.com");
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

                    _db.Users.Add(newUser);
                    await _db.SaveChangesAsync();

                    // Tạo DriverProfile cho user này
                    var driverProfile = new DriverProfile
                    {
                        UserId = newUser.UserId,
                        LicenseNumber = "B123456",
                        VehicleModel = "VinFast VF e34",
                        VehiclePlate = "51H-123.45",
                        BatteryCapacity = 42
                    };

                    _db.DriverProfiles.Add(driverProfile);
                    await _db.SaveChangesAsync();
                    
                    results.Add("✅ Đã tạo user mới chinh22@gmail.com với password 12345");
                }

                // Cập nhật tất cả user có password plain text
                var usersWithPlainPassword = await _db.Users
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
                    await _db.SaveChangesAsync();
                    results.Add($"✅ Đã cập nhật {usersWithPlainPassword.Count} user có password plain text");
                }

                return Ok(new { 
                    message = "🎉 Hoàn thành fix database!",
                    results = results,
                    loginInfo = new {
                        email = "chinh22@gmail.com",
                        password = "12345"
                    }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"❌ Lỗi: {ex.Message}" });
            }
        }
    }
}
