namespace EVCharging.BE.Common.DTOs.Staff
{
    /// <summary>
    /// Response cho tiến trình sạc real-time của session đang hoạt động
    /// </summary>
    public class ActiveSessionProgressResponse
    {
        // Thông tin session
        public int SessionId { get; set; }
        public int PointId { get; set; }
        public string PointName { get; set; } = string.Empty; // Tên điểm sạc (nếu có)
        public int StationId { get; set; }
        public string StationName { get; set; } = string.Empty;
        
        // Thông tin khách hàng
        public int DriverId { get; set; }
        public string? DriverName { get; set; }
        public string? CustomerPhone { get; set; }
        public string? VehiclePlate { get; set; }
        public string? VehicleModel { get; set; }
        public bool IsWalkIn { get; set; } // true nếu là walk-in customer
        
        // Thông tin sạc
        public DateTime StartTime { get; set; }
        public int DurationMinutes { get; set; }
        public int InitialSOC { get; set; }
        public int? CurrentSOC { get; set; } // Real-time từ session log
        public int? TargetSOC { get; set; }
        public double EnergyUsed { get; set; } // kWh
        public decimal CurrentCost { get; set; }
        public string Status { get; set; } = string.Empty; // in_progress, paused
        
        // Thông số real-time từ session log mới nhất
        public decimal? CurrentPower { get; set; } // kW - real-time
        public decimal? Voltage { get; set; } // V - real-time
        public decimal? Temperature { get; set; } // °C - real-time
        public DateTime? LastLogTime { get; set; } // Thời gian log cuối cùng
        
        // Ước tính
        public int? EstimatedRemainingMinutes { get; set; } // Ước tính thời gian còn lại
        public decimal? EstimatedRemainingCost { get; set; } // Ước tính chi phí còn lại
        public double ProgressPercentage { get; set; } // % tiến trình (CurrentSOC - InitialSOC) / (TargetSOC - InitialSOC) * 100
    }
}

