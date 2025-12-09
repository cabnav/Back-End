using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using EVCharging.BE.DAL;
using EVCharging.BE.DAL.Entities;

namespace EVCharging.BE.DAL
{
    public static class DataSeeder
    {
        private static string HashPassword(string password)
        {
            using var sha256 = SHA256.Create();
            var hashedBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
            return Convert.ToBase64String(hashedBytes);
        }

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
                        Password = HashPassword("123456"), // ✅ Hash password đúng cách
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
                        Password = HashPassword("123456"), // ✅ Hash password đúng cách
                        Phone = "0987654321",
                        Role = "admin",
                        WalletBalance = 1000000,
                        BillingType = "postpaid",
                        MembershipTier = "vip",
                        CreatedAt = DateTime.Now
                    },
                    new User
                    {
                        Name = "Chinh User",
                        Email = "chinh22@gmail.com",
                        Password = HashPassword("12345"), // ✅ Hash password đúng cách
                        Phone = "0901234567",
                        Role = "driver",
                        WalletBalance = 500000,
                        BillingType = "prepaid",
                        MembershipTier = "standard",
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
                // Chú ý: Chỉ lưu trạm sạc tại đây để lấy StationId trước khi thêm ChargingPoint
                context.SaveChanges();
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

            // ✅ Nếu chưa có ChargingPoint nào -> seed các điểm sạc cho các trạm
            if (!context.ChargingPoints.Any())
            {
                // Sau khi chắc chắn rằng các trạm đã có trong DB (StationId đã được insert), truy vấn lại các trạm
                var station1 = context.ChargingStations.FirstOrDefault(s => s.Name == "EV Station District 1");
                var station2 = context.ChargingStations.FirstOrDefault(s => s.Name == "EV Station District 7");

                var chargingPoints = new System.Collections.Generic.List<ChargingPoint>();

                // Tạo 8 điểm sạc cho Station 1 (District 1)
                if (station1 != null)
                {
                    chargingPoints.AddRange(new[]
                    {
                        new ChargingPoint
                        {
                            StationId = station1.StationId,
                            ConnectorType = "CCS2",
                            PowerOutput = 50,
                            PricePerKwh = 3500,
                            Status = "available",
                            QrCode = "QR_D1_001",
                            CurrentPower = 0,
                            LastMaintenance = DateOnly.FromDateTime(DateTime.Now.AddDays(-30))
                        },
                        new ChargingPoint
                        {
                            StationId = station1.StationId,
                            ConnectorType = "CCS2",
                            PowerOutput = 50,
                            PricePerKwh = 3500,
                            Status = "available",
                            QrCode = "QR_D1_002",
                            CurrentPower = 0,
                            LastMaintenance = DateOnly.FromDateTime(DateTime.Now.AddDays(-25))
                        },
                        new ChargingPoint
                        {
                            StationId = station1.StationId,
                            ConnectorType = "CHAdeMO",
                            PowerOutput = 50,
                            PricePerKwh = 3500,
                            Status = "available",
                            QrCode = "QR_D1_003",
                            CurrentPower = 0,
                            LastMaintenance = DateOnly.FromDateTime(DateTime.Now.AddDays(-20))
                        },
                        new ChargingPoint
                        {
                            StationId = station1.StationId,
                            ConnectorType = "Type2",
                            PowerOutput = 22,
                            PricePerKwh = 3000,
                            Status = "available",
                            QrCode = "QR_D1_004",
                            CurrentPower = 0,
                            LastMaintenance = DateOnly.FromDateTime(DateTime.Now.AddDays(-15))
                        },
                        new ChargingPoint
                        {
                            StationId = station1.StationId,
                            ConnectorType = "CCS2",
                            PowerOutput = 150,
                            PricePerKwh = 4500,
                            Status = "available",
                            QrCode = "QR_D1_005",
                            CurrentPower = 0,
                            LastMaintenance = DateOnly.FromDateTime(DateTime.Now.AddDays(-10))
                        },
                        new ChargingPoint
                        {
                            StationId = station1.StationId,
                            ConnectorType = "CCS2",
                            PowerOutput = 50,
                            PricePerKwh = 3500,
                            Status = "occupied",
                            QrCode = "QR_D1_006",
                            CurrentPower = 45.5,
                            LastMaintenance = DateOnly.FromDateTime(DateTime.Now.AddDays(-5))
                        },
                        new ChargingPoint
                        {
                            StationId = station1.StationId,
                            ConnectorType = "Type2",
                            PowerOutput = 22,
                            PricePerKwh = 3000,
                            Status = "maintenance",
                            QrCode = "QR_D1_007",
                            CurrentPower = 0,
                            LastMaintenance = DateOnly.FromDateTime(DateTime.Now.AddDays(-1))
                        },
                        new ChargingPoint
                        {
                            StationId = station1.StationId,
                            ConnectorType = "CHAdeMO",
                            PowerOutput = 50,
                            PricePerKwh = 3500,
                            Status = "available",
                            QrCode = "QR_D1_008",
                            CurrentPower = 0,
                            LastMaintenance = DateOnly.FromDateTime(DateTime.Now.AddDays(-3))
                        }
                    });
                }

                // Tạo 6 điểm sạc cho Station 2 (District 7)
                if (station2 != null)
                {
                    chargingPoints.AddRange(new[]
                    {
                        new ChargingPoint
                        {
                            StationId = station2.StationId,
                            ConnectorType = "CCS2",
                            PowerOutput = 50,
                            PricePerKwh = 3200,
                            Status = "maintenance",
                            QrCode = "QR_D7_001",
                            CurrentPower = 0,
                            LastMaintenance = DateOnly.FromDateTime(DateTime.Now.AddDays(-2))
                        },
                        new ChargingPoint
                        {
                            StationId = station2.StationId,
                            ConnectorType = "CCS2",
                            PowerOutput = 50,
                            PricePerKwh = 3200,
                            Status = "maintenance",
                            QrCode = "QR_D7_002",
                            CurrentPower = 0,
                            LastMaintenance = DateOnly.FromDateTime(DateTime.Now.AddDays(-1))
                        },
                        new ChargingPoint
                        {
                            StationId = station2.StationId,
                            ConnectorType = "Type2",
                            PowerOutput = 22,
                            PricePerKwh = 2800,
                            Status = "maintenance",
                            QrCode = "QR_D7_003",
                            CurrentPower = 0,
                            LastMaintenance = DateOnly.FromDateTime(DateTime.Now.AddDays(-1))
                        },
                        new ChargingPoint
                        {
                            StationId = station2.StationId,
                            ConnectorType = "CHAdeMO",
                            PowerOutput = 50,
                            PricePerKwh = 3200,
                            Status = "maintenance",
                            QrCode = "QR_D7_004",
                            CurrentPower = 0,
                            LastMaintenance = DateOnly.FromDateTime(DateTime.Now.AddDays(-2))
                        },
                        new ChargingPoint
                        {
                            StationId = station2.StationId,
                            ConnectorType = "CCS2",
                            PowerOutput = 150,
                            PricePerKwh = 4200,
                            Status = "maintenance",
                            QrCode = "QR_D7_005",
                            CurrentPower = 0,
                            LastMaintenance = DateOnly.FromDateTime(DateTime.Now.AddDays(-1))
                        },
                        new ChargingPoint
                        {
                            StationId = station2.StationId,
                            ConnectorType = "Type2",
                            PowerOutput = 22,
                            PricePerKwh = 2800,
                            Status = "maintenance",
                            QrCode = "QR_D7_006",
                            CurrentPower = 0,
                            LastMaintenance = DateOnly.FromDateTime(DateTime.Now.AddDays(-2))
                        }
                    });
                }

                // Chỉ add nếu các trạm đã tồn tại (đã có StationId)
                context.ChargingPoints.AddRange(chargingPoints);
            }

            context.SaveChanges();
        }
    }
}
