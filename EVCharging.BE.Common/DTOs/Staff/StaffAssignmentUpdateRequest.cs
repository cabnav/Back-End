using System.ComponentModel.DataAnnotations;

namespace EVCharging.BE.Common.DTOs.Staff
{
    /// <summary>
    /// Request để Admin update staff assignment
    /// </summary>
    public class StaffAssignmentUpdateRequest
    {
        [Required(ErrorMessage = "Shift start time is required")]
        public DateTime ShiftStart { get; set; }

        [Required(ErrorMessage = "Shift end time is required")]
        public DateTime ShiftEnd { get; set; }

        /// <summary>
        /// Status: "active", "inactive"
        /// </summary>
        [RegularExpression("^(active|inactive)$", ErrorMessage = "Status must be 'active' or 'inactive'")]
        public string Status { get; set; } = "active";

        /// <summary>
        /// Ghi chú về assignment (optional)
        /// </summary>
        [StringLength(500, ErrorMessage = "Notes cannot exceed 500 characters")]
        public string? Notes { get; set; }
    }
}





