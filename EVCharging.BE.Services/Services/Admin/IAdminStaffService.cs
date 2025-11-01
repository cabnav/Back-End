using EVCharging.BE.Common.DTOs.Shared;
using EVCharging.BE.Common.DTOs.Staff;

namespace EVCharging.BE.Services.Services.Admin
{
    /// <summary>
    /// Service quản lý Staff Assignments cho Admin
    /// </summary>
    public interface IAdminStaffService
    {
        // ========== STAFF ASSIGNMENT CRUD ==========

        /// <summary>
        /// Assign staff vào station
        /// </summary>
        /// <param name="request">Thông tin assignment</param>
        /// <returns>Assignment đã tạo</returns>
        Task<StaffAssignmentResponse?> CreateStaffAssignmentAsync(StaffAssignmentCreateRequest request);

        /// <summary>
        /// Update staff assignment
        /// </summary>
        /// <param name="assignmentId">ID của assignment</param>
        /// <param name="request">Thông tin update</param>
        /// <returns>Assignment đã update</returns>
        Task<StaffAssignmentResponse?> UpdateStaffAssignmentAsync(int assignmentId, StaffAssignmentUpdateRequest request);

        /// <summary>
        /// Xóa (remove) staff assignment
        /// </summary>
        /// <param name="assignmentId">ID của assignment</param>
        /// <returns>True nếu xóa thành công</returns>
        Task<bool> DeleteStaffAssignmentAsync(int assignmentId);

        /// <summary>
        /// Lấy chi tiết assignment
        /// </summary>
        /// <param name="assignmentId">ID của assignment</param>
        /// <returns>Assignment details</returns>
        Task<StaffAssignmentResponse?> GetStaffAssignmentByIdAsync(int assignmentId);

        /// <summary>
        /// Lấy danh sách assignments với filter
        /// </summary>
        /// <param name="filter">Bộ lọc</param>
        /// <returns>Paginated list of assignments</returns>
        Task<PagedResult<StaffAssignmentResponse>> GetStaffAssignmentsAsync(StaffAssignmentFilterRequest filter);

        // ========== QUERY METHODS ==========

        /// <summary>
        /// Lấy danh sách staff tại một station
        /// </summary>
        /// <param name="stationId">ID của station</param>
        /// <param name="onlyActive">Chỉ lấy staff đang active shift</param>
        /// <returns>Danh sách assignments</returns>
        Task<List<StaffAssignmentResponse>> GetStaffByStationAsync(int stationId, bool onlyActive = false);

        /// <summary>
        /// Lấy danh sách stations mà một staff được assign
        /// </summary>
        /// <param name="staffId">ID của staff</param>
        /// <param name="onlyActive">Chỉ lấy assignments đang active</param>
        /// <returns>Danh sách assignments</returns>
        Task<List<StaffAssignmentResponse>> GetStationsByStaffAsync(int staffId, bool onlyActive = false);

        // ========== VALIDATION ==========

        /// <summary>
        /// Kiểm tra staff có thể được assign vào station không (không conflict)
        /// </summary>
        /// <param name="staffId">ID của staff</param>
        /// <param name="stationId">ID của station</param>
        /// <param name="shiftStart">Thời gian bắt đầu shift</param>
        /// <param name="shiftEnd">Thời gian kết thúc shift</param>
        /// <param name="excludeAssignmentId">ID assignment để exclude (khi update)</param>
        /// <returns>True nếu có thể assign, False nếu conflict</returns>
        Task<bool> CanAssignStaffAsync(int staffId, int stationId, DateTime shiftStart, DateTime shiftEnd, int? excludeAssignmentId = null);

        /// <summary>
        /// Kiểm tra staff có role "Staff" không
        /// </summary>
        /// <param name="userId">ID của user</param>
        /// <returns>True nếu là staff</returns>
        Task<bool> IsStaffAsync(int userId);

        /// <summary>
        /// Set user thành staff (update role = "Staff")
        /// </summary>
        /// <param name="userId">ID của user cần set thành staff</param>
        /// <returns>True nếu thành công</returns>
        Task<bool> SetUserAsStaffAsync(int userId);
    }
}





