namespace EVCharging.BE.Common.DTOs.Charging
{
    /// <summary>
    /// Response cho kết quả tính toán chi phí
    /// </summary>
    public class CostCalculationResponse
    {
        public decimal BasePricePerKwh { get; set; }
        public decimal EnergyUsed { get; set; }
        public decimal DurationMinutes { get; set; }
        public decimal BaseCost { get; set; }
        public decimal PeakHourSurcharge { get; set; }
        public decimal MembershipDiscount { get; set; }
        public decimal CustomDiscount { get; set; }
        public decimal TotalDiscount { get; set; }
        public decimal FinalCost { get; set; }
        public string Currency { get; set; } = "VND";
        public DateTime CalculatedAt { get; set; } = DateTime.UtcNow;
        public string? Notes { get; set; }
    }
}
