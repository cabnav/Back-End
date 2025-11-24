namespace EVCharging.BE.Common.DTOs.Analytics
{
    /// <summary>
    /// DTO cho báo cáo giờ cao điểm theo từng trạm
    /// </summary>
    public class StationPeakHoursDto
    {
        public int StationId { get; set; }
        public string StationName { get; set; } = string.Empty;
        public string Period { get; set; } = string.Empty;
        public List<PeakHourDetailDto> PeakHours { get; set; } = new();
        public string PeakHourRange { get; set; } = string.Empty;
        public List<string> Recommendations { get; set; } = new();
    }

    /// <summary>
    /// Chi tiết giờ cao điểm
    /// </summary>
    public class PeakHourDetailDto
    {
        public int Hour { get; set; }
        public int SessionCount { get; set; }
        public double AverageDurationMinutes { get; set; }
        public double UtilizationRate { get; set; }
        public decimal Revenue { get; set; }
        public decimal? AverageEnergyUsed { get; set; }
        public int? ConcurrentSessions { get; set; }
    }
}

