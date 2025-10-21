using EVCharging.BE.Common.DTOs.Charging;

namespace EVCharging.BE.Services.Services.Charging
{
    /// <summary>
    /// Interface cho Cost Calculation Service - tính toán chi phí real-time
    /// </summary>
    public interface ICostCalculationService
    {
        // Cost Calculation
        Task<CostCalculationResponse> CalculateCostAsync(CostCalculationRequest request);
        Task<decimal> GetCurrentPricePerKwhAsync(int chargingPointId);
        Task<bool> IsPeakHoursAsync(DateTime dateTime);
        
        // Pricing Rules
        Task<decimal> GetMembershipDiscountRateAsync(string membershipTier);
        Task<decimal> GetPeakHourSurchargeRateAsync();
        Task<decimal> GetCustomDiscountRateAsync(int userId);
        
        // Real-time Updates
        Task UpdatePricingAsync(int chargingPointId, decimal newPrice);
        Task NotifyPriceChangeAsync(int chargingPointId, decimal oldPrice, decimal newPrice);
    }
}
