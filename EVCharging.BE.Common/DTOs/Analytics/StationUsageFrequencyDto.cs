namespace EVCharging.BE.Common.DTOs.Analytics
{
    /// <summary>
    /// DTO cho báo cáo tần suất sử dụng theo từng trạm
    /// </summary>
    public class StationUsageFrequencyDto
    {
        public int StationId { get; set; }
        public string StationName { get; set; } = string.Empty;
        public string Period { get; set; } = string.Empty;
        public int TotalSessions { get; set; }
        public double AverageSessionsPerDay { get; set; }
        public double UtilizationRate { get; set; }
        public List<UsageByHourDto> UsageByHour { get; set; } = new();
        public List<UsageByDayDto> UsageByDay { get; set; } = new();
        public List<int> PeakHours { get; set; } = new();
    }

    /// <summary>
    /// Tần suất sử dụng theo giờ
    /// </summary>
    public class UsageByHourDto
    {
        public int Hour { get; set; }
        public int SessionCount { get; set; }
        public double Percentage { get; set; }
        public decimal? AverageEnergyUsed { get; set; }
        public decimal? AverageRevenue { get; set; }
    }

    /// <summary>
    /// Tần suất sử dụng theo ngày
    /// </summary>
    public class UsageByDayDto
    {
        public DateTime Date { get; set; }
        public int SessionCount { get; set; }
        public decimal? TotalRevenue { get; set; }
        public decimal? TotalEnergyUsed { get; set; }
    }
}

