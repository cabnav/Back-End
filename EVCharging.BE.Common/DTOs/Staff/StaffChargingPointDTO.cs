namespace EVCharging.BE.Common.DTOs.Staff
{
    /// <summary>
    /// DTO cho điểm sạc với đầy đủ thông tin real-time cho Staff
    /// </summary>
    public class StaffChargingPointDTO
    {
        // Thông tin cơ bản điểm sạc
        public int PointId { get; set; }
        public int StationId { get; set; }
        public string StationName { get; set; } = string.Empty;
        public string StationAddress { get; set; } = string.Empty;

        // Thông tin kỹ thuật
        public string? ConnectorType { get; set; }
        public int? PowerOutput { get; set; } // Công suất tối đa (kW)
        public double? CurrentPower { get; set; } // Công suất hiện tại (kW) - real-time
        public decimal PricePerKwh { get; set; }
        public string? QrCode { get; set; }

        // Trạng thái
        public string? Status { get; set; } // available, in_use, maintenance, offline, paused
        public bool IsOnline { get; set; } // true nếu online, false nếu offline
        public bool IsAvailable { get; set; } // true nếu available, false nếu đang dùng/bảo trì/offline
        public DateOnly? LastMaintenance { get; set; }

        // Thông tin session đang chạy (nếu có)
        public ActiveSessionInfo? ActiveSession { get; set; }
    }

    /// <summary>
    /// Thông tin session đang chạy tại điểm sạc
    /// </summary>
    public class ActiveSessionInfo
    {
        public int SessionId { get; set; }
        public int DriverId { get; set; }
        public string? DriverName { get; set; }
        public string? VehiclePlate { get; set; }
        public DateTime StartTime { get; set; }
        public int DurationMinutes { get; set; }
        public double EnergyUsed { get; set; } // kWh
        public double CurrentPower { get; set; } // kW - real-time
        public int InitialSOC { get; set; }
        public int? CurrentSOC { get; set; }
        public int? TargetSOC { get; set; }
        public decimal CurrentCost { get; set; }
        public string Status { get; set; } = string.Empty; // in_progress, paused
    }
}

