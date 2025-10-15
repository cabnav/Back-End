using System;
using System.Linq;
using EVCharging.BE.DAL.Entities;

namespace EVCharging.BE.DAL
{
    public static class DataSeeder
    {
        public static void Seed(EvchargingManagementContext context)
        {
            // Nếu database chưa có user nào -> seed mẫu
            if (!context.Users.Any())
            {
                var users = new[]
                {
                    new User
                    {
                        Name = "Nguyen Van A",
                        Email = "a@example.com",
                        Password = "123456", // ⚠️ Khi có Auth thì hash
                        Phone = "0901234567",
                        Role = "driver",
                        WalletBalance = 500000,
                        BillingType = "prepaid",
                        MembershipTier = "standard",
                        CreatedAt = DateTime.Now
                    },
                    new User
                    {
                        Name = "Tran Thi B",
                        Email = "b@example.com",
                        Password = "123456",
                        Phone = "0987654321",
                        Role = "admin",
                        WalletBalance = 1000000,
                        BillingType = "postpaid",
                        MembershipTier = "vip",
                        CreatedAt = DateTime.Now
                    }
                };

                context.Users.AddRange(users);
            }

            // Nếu chưa có trạm sạc nào -> seed mẫu
            if (!context.ChargingStations.Any())
            {
                var stations = new[]
                {
                    new ChargingStation
                    {
                        Name = "EV Station District 1",
                        Address = "12 Nguyen Hue, District 1, HCMC",
                        Latitude = 10.7769,
                        Longitude = 106.7009,
                        Operator = "EVPower Co.",
                        Status = "active",
                        TotalPoints = 8,
                        AvailablePoints = 5
                    },
                    new ChargingStation
                    {
                        Name = "EV Station District 7",
                        Address = "25 Nguyen Van Linh, District 7, HCMC",
                        Latitude = 10.7275,
                        Longitude = 106.7215,
                        Operator = "GreenCharge",
                        Status = "maintenance",
                        TotalPoints = 6,
                        AvailablePoints = 0
                    },
                };

                context.ChargingStations.AddRange(stations);
            }

            // ✅ Nếu chưa có DriverProfile nào -> seed 1 profile cho user driver
            if (!context.DriverProfiles.Any())
            {
                // Lấy user driver "a@example.com" từ Local trước (vừa Add) rồi mới tới DB
                var driverUser =
                    context.Users.Local.FirstOrDefault(u => u.Email == "a@example.com")
                    ?? context.Users.FirstOrDefault(u => u.Email == "a@example.com")
                    ?? context.Users.Local.FirstOrDefault(u => u.Role == "driver")
                    ?? context.Users.FirstOrDefault(u => u.Role == "driver");

                if (driverUser != null)
                {
                    // Gán qua navigation để EF tự set FK kể cả khi user chưa SaveChanges
                    context.DriverProfiles.Add(new DriverProfile
                    {
                        User = driverUser,
                        LicenseNumber = "B123456",
                        VehicleModel = "VinFast VF e34",
                        VehiclePlate = "51H-123.45",
                        BatteryCapacity = 42
                    });
                }
            }

            context.SaveChanges();
        }
    }
}
