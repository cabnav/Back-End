namespace EVCharging.BE.Common.DTOs.Analytics
{
    public class DriverMonthlyReportDto
    {
        public int TotalSessions { get; set; }
        public decimal TotalEnergyUsed { get; set; }
        public string FavoriteStation { get; set; }
        public string PeakHour { get; set; }
        public decimal AverageEnergyPerSession { get; set; }
        public decimal TotalCost { get; set; }
    }
}
