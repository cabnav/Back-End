namespace EVCharging.BE.Common.DTOs.Staff
{
    /// <summary>
    /// Response về staff assignment
    /// </summary>
    public class StaffAssignmentResponse
    {
        public int AssignmentId { get; set; }
        public int StaffId { get; set; }
        public string StaffName { get; set; } = string.Empty;
        public string StaffEmail { get; set; } = string.Empty;
        public string? StaffPhone { get; set; }
        public int StationId { get; set; }
        public string StationName { get; set; } = string.Empty;
        public string? StationAddress { get; set; }
        public DateTime ShiftStart { get; set; }
        public DateTime ShiftEnd { get; set; }
        public string Status { get; set; } = string.Empty;
        public bool IsActive => Status == "active" && 
                                DateTime.UtcNow >= ShiftStart && 
                                DateTime.UtcNow <= ShiftEnd;
        public string? Notes { get; set; }
    }
}





