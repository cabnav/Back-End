namespace EVCharging.BE.Common.DTOs.Charging
{
    /// <summary>
    /// Response cho việc cập nhật giá sạc
    /// </summary>
    public class PriceUpdateResponse
    {
        public int ChargingPointId { get; set; }
        public decimal OldPrice { get; set; }
        public decimal NewPrice { get; set; }
        public decimal PriceChange { get; set; }
        public decimal PriceChangePercentage { get; set; }
        public string? Reason { get; set; }
        public DateTime UpdatedAt { get; set; }
        public string UpdatedBy { get; set; } = string.Empty;
        public bool NotifyUsers { get; set; }
        public string Message { get; set; } = string.Empty;
    }
}
