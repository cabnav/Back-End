using EVCharging.BE.Common.DTOs.Charging;
using EVCharging.BE.DAL;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace EVCharging.BE.Services.Services.Charging.Implementations
{
    /// <summary>
    /// Service tính toán chi phí sạc real-time
    /// </summary>
    public class CostCalculationService : ICostCalculationService
    {
        private readonly EvchargingManagementContext _db;
        private readonly IConfiguration _configuration;

        // Pricing rules - có thể lưu trong database hoặc config
        private readonly Dictionary<string, decimal> _membershipDiscounts = new()
        {
            { "basic", 0.00m },
            { "silver", 0.05m },
            { "gold", 0.10m },
            { "platinum", 0.15m }
        };

        private readonly decimal _peakHourSurchargeRate = 0.20m; // 20% surcharge during peak hours
        private readonly TimeSpan _peakHourStart = new(7, 0, 0); // 7:00 AM
        private readonly TimeSpan _peakHourEnd = new(9, 0, 0);   // 9:00 AM
        private readonly TimeSpan _eveningPeakStart = new(17, 0, 0); // 5:00 PM
        private readonly TimeSpan _eveningPeakEnd = new(19, 0, 0);   // 7:00 PM

        public CostCalculationService(EvchargingManagementContext db, IConfiguration configuration)
        {
            _db = db;
            _configuration = configuration;
        }

        /// <summary>
        /// Tính toán chi phí sạc
        /// </summary>
        public async Task<CostCalculationResponse> CalculateCostAsync(CostCalculationRequest request)
        {
            try
            {
                // Get base price per kWh
                var basePricePerKwh = await GetCurrentPricePerKwhAsync(request.ChargingPointId);
                
                // Calculate base cost
                var baseCost = request.EnergyUsed * basePricePerKwh;

                // Check if it's peak hours
                var isPeakHours = await IsPeakHoursAsync(DateTime.UtcNow);
                var peakHourSurcharge = 0m;
                
                if (isPeakHours)
                {
                    peakHourSurcharge = baseCost * _peakHourSurchargeRate;
                }

                // Calculate membership discount
                var membershipDiscount = 0m;
                if (!string.IsNullOrEmpty(request.MembershipTier))
                {
                    var discountRate = await GetMembershipDiscountRateAsync(request.MembershipTier);
                    membershipDiscount = baseCost * discountRate;
                }

                // Calculate custom discount
                var customDiscount = 0m;
                if (request.CustomDiscountRate.HasValue)
                {
                    customDiscount = baseCost * request.CustomDiscountRate.Value;
                }

                // Calculate total discount
                var totalDiscount = membershipDiscount + customDiscount;

                // Calculate final cost
                var finalCost = baseCost + peakHourSurcharge - totalDiscount;

                return new CostCalculationResponse
                {
                    BasePricePerKwh = basePricePerKwh,
                    EnergyUsed = request.EnergyUsed,
                    DurationMinutes = request.DurationMinutes,
                    BaseCost = baseCost,
                    PeakHourSurcharge = peakHourSurcharge,
                    MembershipDiscount = membershipDiscount,
                    CustomDiscount = customDiscount,
                    TotalDiscount = totalDiscount,
                    FinalCost = Math.Max(0, finalCost), // Ensure non-negative cost
                    Currency = "VND",
                    CalculatedAt = DateTime.UtcNow,
                    Notes = GenerateCostNotes(isPeakHours, request.MembershipTier, request.CustomDiscountRate)
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error calculating cost: {ex.Message}");
                return new CostCalculationResponse
                {
                    BasePricePerKwh = 0,
                    EnergyUsed = request.EnergyUsed,
                    DurationMinutes = request.DurationMinutes,
                    BaseCost = 0,
                    PeakHourSurcharge = 0,
                    MembershipDiscount = 0,
                    CustomDiscount = 0,
                    TotalDiscount = 0,
                    FinalCost = 0,
                    Currency = "VND",
                    CalculatedAt = DateTime.UtcNow,
                    Notes = "Error calculating cost"
                };
            }
        }

        /// <summary>
        /// Lấy giá hiện tại per kWh
        /// </summary>
        public async Task<decimal> GetCurrentPricePerKwhAsync(int chargingPointId)
        {
            try
            {
                var chargingPoint = await _db.ChargingPoints.FindAsync(chargingPointId);
                return chargingPoint?.PricePerKwh ?? 0;
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>
        /// Kiểm tra có phải giờ cao điểm không
        /// </summary>
        public Task<bool> IsPeakHoursAsync(DateTime dateTime)
        {
            var time = dateTime.TimeOfDay;
            
            // Morning peak: 7:00 AM - 9:00 AM
            if (time >= _peakHourStart && time <= _peakHourEnd)
                return Task.FromResult(true);
            
            // Evening peak: 5:00 PM - 7:00 PM
            if (time >= _eveningPeakStart && time <= _eveningPeakEnd)
                return Task.FromResult(true);
            
            // Weekend check (optional)
            if (dateTime.DayOfWeek == DayOfWeek.Saturday || dateTime.DayOfWeek == DayOfWeek.Sunday)
            {
                // Weekend peak: 10:00 AM - 2:00 PM
                var weekendPeakStart = new TimeSpan(10, 0, 0);
                var weekendPeakEnd = new TimeSpan(14, 0, 0);
                if (time >= weekendPeakStart && time <= weekendPeakEnd)
                    return Task.FromResult(true);
            }
            
            return Task.FromResult(false);
        }

        /// <summary>
        /// Lấy tỷ lệ giảm giá theo membership
        /// </summary>
        public Task<decimal> GetMembershipDiscountRateAsync(string membershipTier)
        {
            var discount = _membershipDiscounts.TryGetValue(membershipTier?.ToLower() ?? "basic", out var rate) 
                ? rate 
                : 0m;
            return Task.FromResult(discount);
        }

        /// <summary>
        /// Lấy tỷ lệ phụ thu giờ cao điểm
        /// </summary>
        public Task<decimal> GetPeakHourSurchargeRateAsync()
        {
            return Task.FromResult(_peakHourSurchargeRate);
        }

        /// <summary>
        /// Lấy tỷ lệ giảm giá tùy chỉnh
        /// </summary>
        public async Task<decimal> GetCustomDiscountRateAsync(int userId)
        {
            try
            {
                // Có thể lấy từ database hoặc config
                // Ví dụ: user có subscription đặc biệt
                var user = await _db.Users.FindAsync(userId);
                if (user?.MembershipTier == "vip")
                    return 0.25m; // 25% discount for VIP users
                
                return 0m;
            }
            catch
            {
                return 0m;
            }
        }

        /// <summary>
        /// Cập nhật giá
        /// </summary>
        public async Task UpdatePricingAsync(int chargingPointId, decimal newPrice)
        {
            try
            {
                var chargingPoint = await _db.ChargingPoints.FindAsync(chargingPointId);
                if (chargingPoint != null)
                {
                    var oldPrice = chargingPoint.PricePerKwh;
                    chargingPoint.PricePerKwh = newPrice;
                    await _db.SaveChangesAsync();
                    
                    // Notify about price change
                    await NotifyPriceChangeAsync(chargingPointId, oldPrice, newPrice);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error updating pricing: {ex.Message}");
            }
        }

        /// <summary>
        /// Cập nhật giá với validation và thông báo
        /// </summary>
        public async Task<PriceUpdateResponse> UpdatePricingWithValidationAsync(int chargingPointId, PriceUpdateRequest request, string updatedBy)
        {
            try
            {
                // Validate price
                if (!await ValidatePriceAsync(request.NewPrice))
                {
                    throw new ArgumentException("Invalid price: must be between 0.01 and 100,000 VND per kWh");
                }

                var chargingPoint = await _db.ChargingPoints.FindAsync(chargingPointId);
                if (chargingPoint == null)
                {
                    throw new ArgumentException($"Charging point {chargingPointId} not found");
                }

                var oldPrice = chargingPoint.PricePerKwh;
                var priceChange = request.NewPrice - oldPrice;
                var priceChangePercentage = oldPrice > 0 ? (priceChange / oldPrice) * 100 : 0;

                // Update price
                chargingPoint.PricePerKwh = request.NewPrice;
                await _db.SaveChangesAsync();

                // Notify users if requested
                if (request.NotifyUsers)
                {
                    await NotifyPriceChangeAsync(chargingPointId, oldPrice, request.NewPrice);
                }

                return new PriceUpdateResponse
                {
                    ChargingPointId = chargingPointId,
                    OldPrice = oldPrice,
                    NewPrice = request.NewPrice,
                    PriceChange = priceChange,
                    PriceChangePercentage = priceChangePercentage,
                    Reason = request.Reason,
                    UpdatedAt = DateTime.UtcNow,
                    UpdatedBy = updatedBy,
                    NotifyUsers = request.NotifyUsers,
                    Message = $"Giá đã được cập nhật thành công từ {oldPrice:N0} lên {request.NewPrice:N0} VNĐ/kWh"
                };
            }
            catch (Exception ex)
            {
                throw new Exception($"Error updating pricing: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Validate price range
        /// </summary>
        public Task<bool> ValidatePriceAsync(decimal price)
        {
            // Price must be between 0.01 and 100,000 VND per kWh
            var isValid = price >= 0.01m && price <= 100000m;
            return Task.FromResult(isValid);
        }

        /// <summary>
        /// Thông báo thay đổi giá
        /// </summary>
        public Task NotifyPriceChangeAsync(int chargingPointId, decimal oldPrice, decimal newPrice)
        {
            try
            {
                // Log price change
                Console.WriteLine($"Price changed for charging point {chargingPointId}: {oldPrice:N0} -> {newPrice:N0} VND/kWh");
                
                // TODO: Implement real-time notification via SignalR
                // await _hubContext.Clients.All.SendAsync("PriceChanged", new { 
                //     chargingPointId, 
                //     oldPrice, 
                //     newPrice,
                //     changePercentage = oldPrice > 0 ? ((newPrice - oldPrice) / oldPrice) * 100 : 0,
                //     timestamp = DateTime.UtcNow
                // });
                
                // TODO: Send push notifications to users
                // await _notificationService.SendPriceChangeNotificationAsync(chargingPointId, oldPrice, newPrice);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error notifying price change: {ex.Message}");
            }
            
            return Task.CompletedTask;
        }

        /// <summary>
        /// Tạo ghi chú cho chi phí
        /// </summary>
        private string GenerateCostNotes(bool isPeakHours, string? membershipTier, decimal? customDiscount)
        {
            var notes = new List<string>();
            
            if (isPeakHours)
                notes.Add("Peak hours surcharge applied");
            
            if (!string.IsNullOrEmpty(membershipTier) && membershipTier != "basic")
                notes.Add($"{membershipTier.ToUpper()} membership discount applied");
            
            if (customDiscount.HasValue && customDiscount > 0)
                notes.Add("Custom discount applied");
            
            return notes.Count > 0 ? string.Join(", ", notes) : "Standard pricing applied";
        }
    }
}
