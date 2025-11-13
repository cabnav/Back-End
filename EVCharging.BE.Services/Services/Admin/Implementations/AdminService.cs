using EVCharging.BE.Common.DTOs.Shared;
using EVCharging.BE.Common.DTOs.Staff;
using EVCharging.BE.DAL;
using EVCharging.BE.DAL.Entities;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;

namespace EVCharging.BE.Services.Services.Admin.Implementations
{
    public class AdminService : IAdminService
    {
        private readonly EvchargingManagementContext _db;

        public AdminService(EvchargingManagementContext db)
        {
            _db = db;
        }

        // ✅ 1. System Stats tổng quan
        public async Task<object> GetSystemStatsAsync()
        {
            var totalUsers = await _db.Users.CountAsync();
            var totalStations = await _db.ChargingStations.CountAsync();
            var totalPoints = await _db.ChargingPoints.CountAsync();
            var activeSessions = await _db.ChargingSessions.CountAsync(s => s.Status == "active");
            var totalRevenue = await _db.Payments.SumAsync(p => (decimal?)p.Amount) ?? 0;

            return new
            {
                TotalUsers = totalUsers,
                TotalStations = totalStations,
                TotalPoints = totalPoints,
                ActiveSessions = activeSessions,
                TotalRevenue = Math.Round(totalRevenue, 2)
            };
        }

        // ✅ 2. Hiệu suất từng trạm sạc
        public async Task<object> GetStationPerformanceAsync()
        {
            var data = await _db.ChargingStations
                .Include(s => s.ChargingPoints)
                .Select(s => new
                {
                    s.StationId,
                    s.Name,
                    TotalPoints = s.ChargingPoints.Count,
                    Active = s.ChargingPoints.Count(p => p.Status == "active"),
                    Available = s.ChargingPoints.Count(p => p.Status == "Available"),
                    UtilizationRate = s.ChargingPoints.Count == 0
                        ? 0
                        : Math.Round(
                            (double)s.ChargingPoints.Count(p => p.Status == "Busy") /
                            s.ChargingPoints.Count * 100, 1)
                })
                .ToListAsync();

            return data;
        }

        // ✅ 3. Doanh thu & báo cáo tài chính
        public async Task<object> GetRevenueAnalyticsAsync(DateTime? from = null, DateTime? to = null)
        {
            var startDate = from ?? DateTime.Now.AddDays(-30);
            var endDate = to ?? DateTime.Now;

            var dailyRevenue = await _db.Payments
                .Where(p => p.CreatedAt >= startDate && p.CreatedAt <= endDate)
                .GroupBy(p => p.CreatedAt.Value.Date)
                .Select(g => new
                {
                    Date = g.Key,
                    Total = g.Sum(p => p.Amount)
                })
                .OrderBy(g => g.Date)
                .ToListAsync();

            var total = dailyRevenue.Sum(x => x.Total);

            return new
            {
                Period = $"{startDate:dd/MM/yyyy} - {endDate:dd/MM/yyyy}",
                TotalRevenue = total,
                Daily = dailyRevenue
            };
        }

        // ✅ 4. Thống kê xu hướng sử dụng (usage pattern)
        public async Task<object> GetUsagePatternAsync()
        {
            var usageByHour = await _db.ChargingSessions
                .GroupBy(s => s.StartTime.Hour)
                .Select(g => new { Hour = g.Key, Count = g.Count() })
                .OrderBy(g => g.Hour)
                .ToListAsync();

            var usageByDay = await _db.ChargingSessions
                .GroupBy(s => s.StartTime.Date)
                .Select(g => new { Date = g.Key, Count = g.Count() })
                .OrderBy(g => g.Date)
                .ToListAsync();

            return new
            {
                ByHour = usageByHour,
                ByDay = usageByDay
            };
        }

        // ✅ 5. Đánh giá hiệu suất nhân viên
        public async Task<object> GetStaffPerformanceAsync()
        {
            var staffData = await _db.Users
                .Where(u => u.Role == "staff")
                .Select(u => new
                {
                    u.UserId,
                    u.Name,
                    SessionsHandled = _db.ChargingSessions
                        .Where(s => _db.StationStaffs
                            .Any(st => st.StaffId == u.UserId && 
                                      st.StationId == s.Point.StationId))
                        .Count(),
                    RevenueHandled = _db.Payments
                        .Where(p => p.SessionId.HasValue && 
                                   _db.ChargingSessions
                                       .Any(s => s.SessionId == p.SessionId &&
                                               _db.StationStaffs
                                                   .Any(st => st.StaffId == u.UserId && 
                                                           st.StationId == s.Point.StationId)))
                        .Sum(p => (decimal?)p.Amount) ?? 0
                })
                .ToListAsync();

            return staffData;
        }

        // ✅ 6. Tổng doanh thu theo trạm & phương thức thanh toán
        public async Task<object> GetRevenueByStationAndMethodAsync()
        {
            var data = await _db.Payments
                .Where(p => p.PaymentStatus == "completed") // chỉ tính thanh toán thành công
                .Include(p => p.Session)
                    .ThenInclude(s => s.Point)
                    .ThenInclude(pt => pt.Station)
                .GroupBy(p => new
                {
                    p.Session.Point.Station.StationId,
                    StationName = p.Session.Point.Station.Name,
                    p.PaymentMethod
                })
                .Select(g => new
                {
                    g.Key.StationId,
                    g.Key.StationName,
                    g.Key.PaymentMethod,
                    TotalAmount = Math.Round(g.Sum(x => x.Amount), 2)
                })
                .OrderBy(x => x.StationId)
                .ToListAsync();

            var totalRevenue = data.Sum(x => x.TotalAmount);

            return new
            {
                TotalRevenue = totalRevenue,
                Breakdown = data
            };
        }

        // ========== INCIDENT REPORT MANAGEMENT ==========

        /// <summary>
        /// Lấy danh sách báo cáo sự cố (cho admin)
        /// </summary>
        public async Task<object> GetIncidentReportsAsync(IncidentReportFilter filter)
        {
            try
            {
                // 1. Build query
                var query = _db.IncidentReports
                    .Include(ir => ir.Point)
                    .ThenInclude(p => p.Station)
                    .Include(ir => ir.Reporter)
                    .Include(ir => ir.ResolvedByNavigation)
                    .AsQueryable();

                // 2. Apply filters
                if (filter.Status != "all" && !string.IsNullOrEmpty(filter.Status))
                {
                    query = query.Where(ir => ir.Status != null && ir.Status.ToLower() == filter.Status.ToLower());
                }

                if (filter.Priority != "all" && !string.IsNullOrEmpty(filter.Priority))
                {
                    query = query.Where(ir => ir.Priority != null && ir.Priority.ToLower() == filter.Priority.ToLower());
                }

                if (filter.StationId.HasValue && filter.StationId.Value > 0)
                {
                    query = query.Where(ir => ir.Point != null && ir.Point.StationId == filter.StationId.Value);
                }

                if (filter.ReporterId.HasValue && filter.ReporterId.Value > 0)
                {
                    query = query.Where(ir => ir.ReporterId == filter.ReporterId.Value);
                }

                if (filter.FromDate.HasValue)
                {
                    query = query.Where(ir => ir.ReportedAt >= filter.FromDate.Value);
                }

                if (filter.ToDate.HasValue)
                {
                    query = query.Where(ir => ir.ReportedAt <= filter.ToDate.Value);
                }

                // 3. Get total count
                var totalCount = await query.CountAsync();

                // 4. Apply pagination
                var incidents = await query
                    .OrderByDescending(ir => ir.ReportedAt ?? DateTime.MinValue)
                    .Skip((filter.Page - 1) * filter.PageSize)
                    .Take(filter.PageSize)
                    .ToListAsync();

                // 5. Map to DTOs
                var reports = incidents.Select(ir => new IncidentReportResponse
                {
                    ReportId = ir.ReportId,
                    ReporterId = ir.ReporterId,
                    ReporterName = ir.Reporter?.Name ?? "Unknown",
                    PointId = ir.PointId,
                    StationId = ir.Point?.StationId ?? 0,
                    StationName = ir.Point?.Station?.Name ?? "Unknown",
                    Title = ir.Title,
                    Description = ir.Description,
                    Priority = ir.Priority,
                    Status = ir.Status,
                    ReportedAt = ir.ReportedAt,
                    ResolvedAt = ir.ResolvedAt,
                    ResolvedBy = ir.ResolvedBy,
                    ResolvedByName = ir.ResolvedByNavigation?.Name
                }).ToList();

                return new
                {
                    Reports = reports,
                    TotalCount = totalCount,
                    Page = filter.Page,
                    PageSize = filter.PageSize,
                    TotalPages = (int)Math.Ceiling(totalCount / (double)filter.PageSize)
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting incident reports: {ex.Message}");
                return new
                {
                    Reports = new List<IncidentReportResponse>(),
                    TotalCount = 0,
                    Page = filter.Page,
                    PageSize = filter.PageSize,
                    TotalPages = 0
                };
            }
        }

        /// <summary>
        /// Lấy chi tiết báo cáo sự cố
        /// </summary>
        public async Task<IncidentReportResponse?> GetIncidentReportByIdAsync(int reportId)
        {
            try
            {
                var incident = await _db.IncidentReports
                    .Include(ir => ir.Point)
                    .ThenInclude(p => p.Station)
                    .Include(ir => ir.Reporter)
                    .Include(ir => ir.ResolvedByNavigation)
                    .FirstOrDefaultAsync(ir => ir.ReportId == reportId);

                if (incident == null)
                    return null;

                return new IncidentReportResponse
                {
                    ReportId = incident.ReportId,
                    ReporterId = incident.ReporterId,
                    ReporterName = incident.Reporter?.Name ?? "Unknown",
                    PointId = incident.PointId,
                    StationId = incident.Point?.StationId ?? 0,
                    StationName = incident.Point?.Station?.Name ?? "Unknown",
                    Title = incident.Title,
                    Description = incident.Description,
                    Priority = incident.Priority,
                    Status = incident.Status,
                    ReportedAt = incident.ReportedAt,
                    ResolvedAt = incident.ResolvedAt,
                    ResolvedBy = incident.ResolvedBy,
                    ResolvedByName = incident.ResolvedByNavigation?.Name
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting incident report by id: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Cập nhật trạng thái báo cáo sự cố
        /// </summary>
        public async Task<IncidentReportResponse?> UpdateIncidentReportStatusAsync(int reportId, int adminId, UpdateIncidentStatusRequest request)
        {
            try
            {
                // 1. Get incident report
                var incident = await _db.IncidentReports
                    .Include(ir => ir.Point)
                    .ThenInclude(p => p.Station)
                    .Include(ir => ir.Reporter)
                    .Include(ir => ir.ResolvedByNavigation)
                    .FirstOrDefaultAsync(ir => ir.ReportId == reportId);

                if (incident == null)
                {
                    Console.WriteLine($"Incident report {reportId} not found");
                    return null;
                }

                // 2. Verify admin user
                var adminUser = await _db.Users.FindAsync(adminId);
                if (adminUser == null || !adminUser.Role.Equals("Admin", StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine($"User {adminId} is not an admin");
                    return null;
                }

                // 3. Update status
                var newStatus = request.Status.ToLower();
                incident.Status = newStatus;

                // 4. Handle status transitions
                if (newStatus == "resolved")
                {
                    // Mark as resolved - set who and when
                    incident.ResolvedBy = adminId;
                    incident.ResolvedAt = DateTime.UtcNow;
                }
                else if (newStatus == "open")
                {
                    // If reopening, clear resolved info
                    incident.ResolvedBy = null;
                    incident.ResolvedAt = null;
                }
                else if (newStatus == "in_progress")
                {
                    // If moving to in_progress from resolved, keep resolved info for history
                    // But if moving from open, no change needed
                    // This preserves the resolution history even if reopened
                }

                // 5. Save changes
                await _db.SaveChangesAsync();

                // 6. Reload to get updated ResolvedByNavigation
                await _db.Entry(incident).Reference(ir => ir.ResolvedByNavigation).LoadAsync();

                // 7. Return updated report
                return new IncidentReportResponse
                {
                    ReportId = incident.ReportId,
                    ReporterId = incident.ReporterId,
                    ReporterName = incident.Reporter?.Name ?? "Unknown",
                    PointId = incident.PointId,
                    StationId = incident.Point?.StationId ?? 0,
                    StationName = incident.Point?.Station?.Name ?? "Unknown",
                    Title = incident.Title,
                    Description = incident.Description,
                    Priority = incident.Priority,
                    Status = incident.Status,
                    ReportedAt = incident.ReportedAt,
                    ResolvedAt = incident.ResolvedAt,
                    ResolvedBy = incident.ResolvedBy,
                    ResolvedByName = incident.ResolvedByNavigation?.Name
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error updating incident report status: {ex.Message}");
                return null;
            }
        }

    }
}
