using EVCharging.BE.Common.DTOs.Shared;
using EVCharging.BE.Common.DTOs.Staff;
using EVCharging.BE.DAL;
using EVCharging.BE.DAL.Entities;
using Microsoft.EntityFrameworkCore;

namespace EVCharging.BE.Services.Services.Admin.Implementations
{
    /// <summary>
    /// Service quản lý Staff Assignments cho Admin
    /// </summary>
    public class AdminStaffService : IAdminStaffService
    {
        private readonly EvchargingManagementContext _db;

        public AdminStaffService(EvchargingManagementContext db)
        {
            _db = db;
        }

        // ========== STAFF ASSIGNMENT CRUD ==========

        public async Task<StaffAssignmentResponse?> CreateStaffAssignmentAsync(StaffAssignmentCreateRequest request)
        {
            try
            {
                // 1. Validate shift time
                if (request.ShiftStart >= request.ShiftEnd)
                {
                    throw new ArgumentException("Shift start time must be before shift end time");
                }

                // 2. Check if user is actually a staff
                var isStaff = await IsStaffAsync(request.StaffId);
                if (!isStaff)
                {
                    throw new ArgumentException($"User {request.StaffId} is not a staff member");
                }

                // 3. Check if staff and station exist
                var staff = await _db.Users.FindAsync(request.StaffId);
                var station = await _db.ChargingStations.FindAsync(request.StationId);

                if (staff == null)
                    throw new ArgumentException($"Staff with ID {request.StaffId} not found");

                if (station == null)
                    throw new ArgumentException($"Station with ID {request.StationId} not found");

                // 4. Check for conflicts (staff đã được assign vào trạm khác trong cùng thời gian)
                var canAssign = await CanAssignStaffAsync(
                    request.StaffId, 
                    request.StationId, 
                    request.ShiftStart, 
                    request.ShiftEnd);

                if (!canAssign)
                {
                    throw new InvalidOperationException(
                        "Cannot assign staff. Staff is already assigned to another station during this shift time, " +
                        "or there is a time conflict with existing assignments."
                    );
                }

                // 5. Create assignment
                var assignment = new StationStaff
                {
                    StaffId = request.StaffId,
                    StationId = request.StationId,
                    ShiftStart = request.ShiftStart,
                    ShiftEnd = request.ShiftEnd,
                    Status = request.Status
                };

                _db.StationStaffs.Add(assignment);
                await _db.SaveChangesAsync();

                // 6. Return response
                return await GetStaffAssignmentByIdAsync(assignment.AssignmentId);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error creating staff assignment: {ex.Message}");
                throw;
            }
        }

        public async Task<StaffAssignmentResponse?> UpdateStaffAssignmentAsync(int assignmentId, StaffAssignmentUpdateRequest request)
        {
            try
            {
                // 1. Validate shift time
                if (request.ShiftStart >= request.ShiftEnd)
                {
                    throw new ArgumentException("Shift start time must be before shift end time");
                }

                // 2. Get assignment
                var assignment = await _db.StationStaffs
                    .Include(ss => ss.Staff)
                    .Include(ss => ss.Station)
                    .FirstOrDefaultAsync(ss => ss.AssignmentId == assignmentId);

                if (assignment == null)
                    return null;

                // 3. Check for conflicts (exclude current assignment)
                var canAssign = await CanAssignStaffAsync(
                    assignment.StaffId,
                    assignment.StationId,
                    request.ShiftStart,
                    request.ShiftEnd,
                    assignmentId);

                if (!canAssign)
                {
                    throw new InvalidOperationException(
                        "Cannot update assignment. There is a time conflict with existing assignments."
                    );
                }

                // 4. Update assignment
                assignment.ShiftStart = request.ShiftStart;
                assignment.ShiftEnd = request.ShiftEnd;
                assignment.Status = request.Status;

                await _db.SaveChangesAsync();

                // 5. Return updated assignment
                return await GetStaffAssignmentByIdAsync(assignmentId);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error updating staff assignment: {ex.Message}");
                throw;
            }
        }

        public async Task<bool> DeleteStaffAssignmentAsync(int assignmentId)
        {
            try
            {
                var assignment = await _db.StationStaffs.FindAsync(assignmentId);
                if (assignment == null)
                    return false;

                _db.StationStaffs.Remove(assignment);
                await _db.SaveChangesAsync();

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error deleting staff assignment: {ex.Message}");
                return false;
            }
        }

        public async Task<StaffAssignmentResponse?> GetStaffAssignmentByIdAsync(int assignmentId)
        {
            try
            {
                var assignment = await _db.StationStaffs
                    .Include(ss => ss.Staff)
                    .Include(ss => ss.Station)
                    .FirstOrDefaultAsync(ss => ss.AssignmentId == assignmentId);

                if (assignment == null)
                    return null;

                return MapToResponse(assignment);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting staff assignment: {ex.Message}");
                return null;
            }
        }

        public async Task<PagedResult<StaffAssignmentResponse>> GetStaffAssignmentsAsync(StaffAssignmentFilterRequest filter)
        {
            try
            {
                // Build query
                var query = _db.StationStaffs
                    .Include(ss => ss.Staff)
                    .Include(ss => ss.Station)
                    .AsQueryable();

                // Apply filters
                if (filter.StaffId.HasValue)
                {
                    query = query.Where(ss => ss.StaffId == filter.StaffId.Value);
                }

                if (filter.StationId.HasValue)
                {
                    query = query.Where(ss => ss.StationId == filter.StationId.Value);
                }

                if (filter.Status != "all")
                {
                    query = query.Where(ss => ss.Status == filter.Status);
                }

                if (filter.OnlyActiveShifts == true)
                {
                    var now = DateTime.UtcNow;
                    query = query.Where(ss => 
                        ss.Status == "active" &&
                        ss.ShiftStart <= now &&
                        ss.ShiftEnd >= now);
                }

                if (filter.Date.HasValue)
                {
                    var date = filter.Date.Value.Date;
                    query = query.Where(ss =>
                        date >= ss.ShiftStart.Date &&
                        date <= ss.ShiftEnd.Date);
                }

                // Get total count
                var totalCount = await query.CountAsync();

                // Apply pagination
                var assignments = await query
                    .OrderByDescending(ss => ss.ShiftStart)
                    .Skip((filter.Page - 1) * filter.PageSize)
                    .Take(filter.PageSize)
                    .ToListAsync();

                // Map to DTOs
                var items = assignments.Select(MapToResponse).ToList();

                var pagedResult = new PagedResult<StaffAssignmentResponse>
                {
                    Items = items,
                    TotalCount = totalCount,
                    Page = filter.Page,
                    PageSize = filter.PageSize
                };
                // If TotalPages is read-only and automatically calculated, no need to set it.
                return pagedResult;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting staff assignments: {ex.Message}");
                var emptyResult = new PagedResult<StaffAssignmentResponse>
                {
                    Items = new List<StaffAssignmentResponse>(),
                    TotalCount = 0,
                    Page = filter.Page,
                    PageSize = filter.PageSize
                };
                return emptyResult;
            }
        }

        // ========== QUERY METHODS ==========

        public async Task<List<StaffAssignmentResponse>> GetStaffByStationAsync(int stationId, bool onlyActive = false)
        {
            try
            {
                var query = _db.StationStaffs
                    .Include(ss => ss.Staff)
                    .Include(ss => ss.Station)
                    .Where(ss => ss.StationId == stationId);

                if (onlyActive)
                {
                    var now = DateTime.UtcNow;
                    query = query.Where(ss =>
                        ss.Status == "active" &&
                        ss.ShiftStart <= now &&
                        ss.ShiftEnd >= now);
                }

                var assignments = await query
                    .OrderByDescending(ss => ss.ShiftStart)
                    .ToListAsync();

                return assignments.Select(MapToResponse).ToList();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting staff by station: {ex.Message}");
                return new List<StaffAssignmentResponse>();
            }
        }

        public async Task<List<StaffAssignmentResponse>> GetStationsByStaffAsync(int staffId, bool onlyActive = false)
        {
            try
            {
                var query = _db.StationStaffs
                    .Include(ss => ss.Staff)
                    .Include(ss => ss.Station)
                    .Where(ss => ss.StaffId == staffId);

                if (onlyActive)
                {
                    var now = DateTime.UtcNow;
                    query = query.Where(ss =>
                        ss.Status == "active" &&
                        ss.ShiftStart <= now &&
                        ss.ShiftEnd >= now);
                }

                var assignments = await query
                    .OrderByDescending(ss => ss.ShiftStart)
                    .ToListAsync();

                return assignments.Select(MapToResponse).ToList();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting stations by staff: {ex.Message}");
                return new List<StaffAssignmentResponse>();
            }
        }

        // ========== VALIDATION ==========

        public async Task<bool> CanAssignStaffAsync(int staffId, int stationId, DateTime shiftStart, DateTime shiftEnd, int? excludeAssignmentId = null)
        {
            try
            {
                // Check if staff has any overlapping assignments
                var conflictingAssignments = await _db.StationStaffs
                    .Where(ss =>
                        ss.StaffId == staffId &&
                        ss.Status == "active" &&
                        (excludeAssignmentId == null || ss.AssignmentId != excludeAssignmentId) &&
                        // Check for time overlap
                        ((ss.ShiftStart <= shiftStart && ss.ShiftEnd > shiftStart) ||
                         (ss.ShiftStart < shiftEnd && ss.ShiftEnd >= shiftEnd) ||
                         (ss.ShiftStart >= shiftStart && ss.ShiftEnd <= shiftEnd)))
                    .AnyAsync();

                // If no conflicts, can assign
                return !conflictingAssignments;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error checking if can assign staff: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> IsStaffAsync(int userId)
        {
            try
            {
                var user = await _db.Users.FindAsync(userId);
                return user != null && user.Role.Equals("Staff", StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error checking if user is staff: {ex.Message}");
                return false;
            }
        }

        // ========== HELPER METHODS ==========

        private StaffAssignmentResponse MapToResponse(StationStaff assignment)
        {
            return new StaffAssignmentResponse
            {
                AssignmentId = assignment.AssignmentId,
                StaffId = assignment.StaffId,
                StaffName = assignment.Staff?.Name ?? "N/A",
                StaffEmail = assignment.Staff?.Email ?? "N/A",
                StaffPhone = assignment.Staff?.Phone,
                StationId = assignment.StationId,
                StationName = assignment.Station?.Name ?? "N/A",
                StationAddress = assignment.Station?.Address,
                ShiftStart = assignment.ShiftStart,
                ShiftEnd = assignment.ShiftEnd,
                Status = assignment.Status ?? "inactive"
            };
        }
    }
}


