using EVCharging.BE.Common.Enums;

namespace EVCharging.BE.Common.DTOs.Charging
{
    /// <summary>
    /// DTO for real-time charging session monitoring
    /// </summary>
    public class RealTimeSessionDTO
    {
        public int SessionId { get; set; }
        public int DriverId { get; set; }
        public int PointId { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public string Status { get; set; } = string.Empty;
        
        // Real-time charging data
        public int CurrentSOC { get; set; } // State of Charge percentage
        public int InitialSOC { get; set; }
        public int? TargetSOC { get; set; }
        public double EnergyUsed { get; set; } // kWh
        public double CurrentPower { get; set; } // kW
        public double AveragePower { get; set; } // kW
        
        // Time estimates
        public int DurationMinutes { get; set; }
        public int? EstimatedRemainingMinutes { get; set; }
        public DateTime? EstimatedCompletionTime { get; set; }
        
        // Cost information
        public decimal CurrentCost { get; set; }
        public decimal EstimatedTotalCost { get; set; }
        public decimal PricePerKwh { get; set; }
        
        // Charging point information
        public ChargingPointInfoDTO ChargingPoint { get; set; } = new();
    }

    /// <summary>
    /// DTO for charging point information in session
    /// </summary>
    public class ChargingPointInfoDTO
    {
        public int PointId { get; set; }
        public int StationId { get; set; }
        public string StationName { get; set; } = string.Empty;
        public string StationAddress { get; set; } = string.Empty;
        public ConnectorType ConnectorType { get; set; }
        public int PowerOutput { get; set; }
        public string Status { get; set; } = string.Empty;
    }

    /// <summary>
    /// DTO for charging completion notification
    /// </summary>
    public class ChargingCompletionDTO
    {
        public int SessionId { get; set; }
        public int DriverId { get; set; }
        public string DriverName { get; set; } = string.Empty;
        public string DriverEmail { get; set; } = string.Empty;
        public string DriverPhone { get; set; } = string.Empty;
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public int InitialSOC { get; set; }
        public int FinalSOC { get; set; }
        public double EnergyUsed { get; set; }
        public decimal TotalCost { get; set; }
        public int DurationMinutes { get; set; }
        public string StationName { get; set; } = string.Empty;
        public string StationAddress { get; set; } = string.Empty;
    }
}
