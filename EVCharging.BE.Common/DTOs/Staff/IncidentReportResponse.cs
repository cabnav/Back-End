namespace EVCharging.BE.Common.DTOs.Staff
{
    /// <summary>
    /// Response cho thông tin báo cáo sự cố
    /// </summary>
    public class IncidentReportResponse
    {
        public int ReportId { get; set; }
        public int ReporterId { get; set; }
        public string ReporterName { get; set; } = string.Empty;
        public int PointId { get; set; }
        public int StationId { get; set; }
        public string StationName { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? Priority { get; set; }
        public string? Status { get; set; }
        public DateTime? ReportedAt { get; set; }
        public DateTime? ResolvedAt { get; set; }
        public int? ResolvedBy { get; set; }
        public string? ResolvedByName { get; set; }
    }
}

