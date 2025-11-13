using EVCharging.BE.Common.DTOs.Stations;
using EVCharging.BE.Common.DTOs.Users;

namespace EVCharging.BE.Common.DTOs.Charging
{
    /// <summary>
    /// Response cho thông tin phiên sạc
    /// </summary>
    public class ChargingSessionResponse
    {
        public int SessionId { get; set; }
        public int DriverId { get; set; }
        public int ChargingPointId { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public int InitialSOC { get; set; }
        public int? FinalSOC { get; set; }
        public decimal EnergyUsed { get; set; }
        public int DurationMinutes { get; set; }
        public decimal CostBeforeDiscount { get; set; }
        public decimal AppliedDiscount { get; set; }
        public decimal FinalCost { get; set; }
        public string Status { get; set; } = string.Empty;
        public string? Notes { get; set; }
        public string? Reason { get; set; }
        public int? ReservationId { get; set; }
        
        // Navigation properties
        public ChargingPointDTO ChargingPoint { get; set; } = new();
        public DriverProfileDTO Driver { get; set; } = new();
        public List<SessionLogDTO> SessionLogs { get; set; } = new();
        
        // Real-time data
        public int? CurrentSOC { get; set; }
        public decimal? CurrentPower { get; set; }
        public decimal? Voltage { get; set; }
        public decimal? Temperature { get; set; }
        public DateTime? LastUpdated { get; set; }
    }
}
