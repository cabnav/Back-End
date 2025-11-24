using EVCharging.BE.Common.DTOs.Analytics;
using EVCharging.BE.Common.DTOs.Shared;
using EVCharging.BE.Common.DTOs.Staff;
using EVCharging.BE.DAL;
using EVCharging.BE.DAL.Entities;
using EVCharging.BE.Services.Services.Notification;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;

namespace EVCharging.BE.Services.Services.Admin.Implementations
{
    public class AdminService : IAdminService
    {
        private readonly EvchargingManagementContext _db;
        private readonly INotificationService _notificationService;

        public AdminService(EvchargingManagementContext db, INotificationService notificationService)
        {
            _db = db;
            _notificationService = notificationService;
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
                .Where(p => p.CreatedAt.HasValue 
                    && p.CreatedAt.Value >= startDate 
                    && p.CreatedAt.Value <= endDate)
                .GroupBy(p => p.CreatedAt!.Value.Date)
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
            // Lấy tất cả trạm
            var allStations = await _db.ChargingStations
                .Select(s => new
                {
                    s.StationId,
                    s.Name
                })
                .ToListAsync();

            // Lấy doanh thu từ payments thành công
            var revenueData = await _db.Payments
                .Where(p => p.PaymentStatus == "success" // chỉ tính thanh toán thành công
                    && p.SessionId.HasValue) // đảm bảo có Session
                .Include(p => p.Session!)
                    .ThenInclude(s => s.Point)
                    .ThenInclude(pt => pt.Station)
                .Where(p => p.Session != null
                    && p.Session.Point != null
                    && p.Session.Point.Station != null) // đảm bảo có Station
                .GroupBy(p => new
                {
                    StationId = p.Session!.Point!.Station!.StationId,
                    StationName = p.Session.Point.Station.Name,
                    PaymentMethod = p.PaymentMethod ?? "Unknown"
                })
                .Select(g => new
                {
                    g.Key.StationId,
                    g.Key.StationName,
                    g.Key.PaymentMethod,
                    TotalAmount = Math.Round(g.Sum(x => x.Amount), 2)
                })
                .ToListAsync();

            // Kết hợp: hiển thị tất cả trạm, kể cả trạm chưa có payment
            var breakdownList = new List<(int StationId, string StationName, string PaymentMethod, double TotalAmount)>();
            
            foreach (var station in allStations)
            {
                var stationPayments = revenueData
                    .Where(r => r.StationId == station.StationId)
                    .ToList();

                // Nếu trạm chưa có payment nào, thêm 1 record với TotalAmount = 0
                if (!stationPayments.Any())
                {
                    breakdownList.Add((station.StationId, station.Name, "N/A", 0.0));
                }
                else
                {
                    // Thêm tất cả payment methods của trạm
                    foreach (var payment in stationPayments)
                    {
                        breakdownList.Add((
                            station.StationId, 
                            station.Name, 
                            payment.PaymentMethod ?? "Unknown", 
                            (double)payment.TotalAmount
                        ));
                    }
                }
            }

            // Sắp xếp breakdown
            var breakdown = breakdownList
                .OrderBy(x => x.StationId)
                .ThenBy(x => x.PaymentMethod)
                .Select(x => new
                {
                    StationId = x.StationId,
                    StationName = x.StationName,
                    PaymentMethod = x.PaymentMethod,
                    TotalAmount = x.TotalAmount
                })
                .ToList();

            var totalRevenue = breakdown.Sum(x => x.TotalAmount);
            var walletTotal = breakdown
                .Where(x => x.PaymentMethod != null && x.PaymentMethod.ToLower() == "wallet")
                .Sum(x => x.TotalAmount);

            return new
            {
                TotalRevenue = totalRevenue,
                Breakdown = breakdown
            };
        }

        // ========== STATION ANALYTICS ==========

        /// <summary>
        /// Lấy tần suất sử dụng theo từng trạm
        /// </summary>
        public async Task<object> GetStationUsageFrequencyAsync(int stationId, DateTime? from = null, DateTime? to = null)
        {
            var startDate = from ?? DateTime.Now.AddDays(-30);
            var endDate = to ?? DateTime.Now;

            // 1. Kiểm tra trạm có tồn tại không
            var station = await _db.ChargingStations
                .FirstOrDefaultAsync(s => s.StationId == stationId);

            if (station == null)
            {
                throw new KeyNotFoundException($"Station with ID {stationId} not found");
            }

            // 2. Lấy tất cả sessions của trạm trong khoảng thời gian
            var sessions = await _db.ChargingSessions
                .Where(s => s.Point != null && 
                           s.Point.StationId == stationId &&
                           s.StartTime >= startDate && 
                           s.StartTime <= endDate &&
                           s.Status.ToLower() == "completed")
                .Include(s => s.Point)
                .ToListAsync();

            var totalSessions = sessions.Count;
            var daysInPeriod = (endDate - startDate).TotalDays;
            var averageSessionsPerDay = daysInPeriod > 0 ? totalSessions / daysInPeriod : 0;

            // 3. Tính utilization rate
            var totalPoints = await _db.ChargingPoints
                .CountAsync(p => p.StationId == stationId);
            
            var utilizationRate = 0.0;
            if (totalPoints > 0 && daysInPeriod > 0)
            {
                var totalPossibleSessions = totalPoints * daysInPeriod * 24; // Giả sử mỗi giờ có thể có 1 session
                utilizationRate = totalPossibleSessions > 0 
                    ? (totalSessions / totalPossibleSessions) * 100 
                    : 0;
            }

            // 4. Thống kê theo giờ
            var usageByHour = sessions
                .GroupBy(s => s.StartTime.Hour)
                .Select(g => new
                {
                    Hour = g.Key,
                    SessionCount = g.Count(),
                    AverageEnergyUsed = g.Where(s => s.EnergyUsed.HasValue).Any() 
                        ? (decimal?)g.Where(s => s.EnergyUsed.HasValue).Average(s => s.EnergyUsed!.Value) 
                        : null,
                    AverageRevenue = g.Where(s => s.FinalCost.HasValue).Any()
                        ? (decimal?)g.Where(s => s.FinalCost.HasValue).Average(s => s.FinalCost!.Value)
                        : null
                })
                .OrderBy(x => x.Hour)
                .ToList();

            // Tính phần trăm cho mỗi giờ
            var usageByHourWithPercentage = usageByHour.Select(x => new
            {
                x.Hour,
                x.SessionCount,
                Percentage = totalSessions > 0 ? Math.Round((x.SessionCount / (double)totalSessions) * 100, 2) : 0,
                x.AverageEnergyUsed,
                x.AverageRevenue
            }).ToList();

            // 5. Thống kê theo ngày
            var usageByDay = sessions
                .GroupBy(s => s.StartTime.Date)
                .Select(g => new
                {
                    Date = g.Key,
                    SessionCount = g.Count(),
                    TotalRevenue = g.Where(s => s.FinalCost.HasValue).Sum(s => s.FinalCost!.Value),
                    TotalEnergyUsed = g.Where(s => s.EnergyUsed.HasValue).Sum(s => s.EnergyUsed!.Value)
                })
                .OrderBy(x => x.Date)
                .ToList();

            // 6. Xác định giờ cao điểm (top 3 giờ có nhiều session nhất)
            var peakHours = usageByHour
                .OrderByDescending(x => x.SessionCount)
                .Take(3)
                .Select(x => x.Hour)
                .ToList();

            return new StationUsageFrequencyDto
            {
                StationId = stationId,
                StationName = station.Name,
                Period = $"{startDate:dd/MM/yyyy} - {endDate:dd/MM/yyyy}",
                TotalSessions = totalSessions,
                AverageSessionsPerDay = Math.Round(averageSessionsPerDay, 2),
                UtilizationRate = Math.Round(utilizationRate, 2),
                UsageByHour = usageByHourWithPercentage.Select(x => new UsageByHourDto
                {
                    Hour = x.Hour,
                    SessionCount = x.SessionCount,
                    Percentage = x.Percentage,
                    AverageEnergyUsed = x.AverageEnergyUsed,
                    AverageRevenue = x.AverageRevenue
                }).ToList(),
                UsageByDay = usageByDay.Select(x => new UsageByDayDto
                {
                    Date = x.Date,
                    SessionCount = x.SessionCount,
                    TotalRevenue = x.TotalRevenue > 0 ? x.TotalRevenue : null,
                    TotalEnergyUsed = x.TotalEnergyUsed > 0 ? x.TotalEnergyUsed : null
                }).ToList(),
                PeakHours = peakHours
            };
        }

        /// <summary>
        /// Lấy giờ cao điểm theo từng trạm
        /// </summary>
        public async Task<object> GetStationPeakHoursAsync(int stationId, DateTime? from = null, DateTime? to = null)
        {
            var startDate = from ?? DateTime.Now.AddDays(-30);
            var endDate = to ?? DateTime.Now;

            // 1. Kiểm tra trạm có tồn tại không
            var station = await _db.ChargingStations
                .FirstOrDefaultAsync(s => s.StationId == stationId);

            if (station == null)
            {
                throw new KeyNotFoundException($"Station with ID {stationId} not found");
            }

            // 2. Lấy tất cả sessions của trạm trong khoảng thời gian
            var sessions = await _db.ChargingSessions
                .Where(s => s.Point != null && 
                           s.Point.StationId == stationId &&
                           s.StartTime >= startDate && 
                           s.StartTime <= endDate &&
                           s.Status.ToLower() == "completed")
                .Include(s => s.Point)
                .ToListAsync();

            if (!sessions.Any())
            {
                return new StationPeakHoursDto
                {
                    StationId = stationId,
                    StationName = station.Name,
                    Period = $"{startDate:dd/MM/yyyy} - {endDate:dd/MM/yyyy}",
                    PeakHours = new List<PeakHourDetailDto>(),
                    PeakHourRange = "N/A",
                    Recommendations = new List<string> { "Chưa có dữ liệu sử dụng trong khoảng thời gian này" }
                };
            }

            // 3. Tính toán thống kê theo giờ
            var hourlyStats = sessions
                .GroupBy(s => s.StartTime.Hour)
                .Select(g => new
                {
                    Hour = g.Key,
                    SessionCount = g.Count(),
                    AverageDurationMinutes = g.Where(s => s.DurationMinutes.HasValue).Any()
                        ? g.Where(s => s.DurationMinutes.HasValue).Average(s => s.DurationMinutes!.Value)
                        : 0,
                    Revenue = g.Where(s => s.FinalCost.HasValue).Sum(s => s.FinalCost!.Value),
                    AverageEnergyUsed = g.Where(s => s.EnergyUsed.HasValue).Any()
                        ? (decimal?)g.Where(s => s.EnergyUsed.HasValue).Average(s => s.EnergyUsed!.Value)
                        : null
                })
                .OrderByDescending(x => x.SessionCount)
                .ToList();

            // 4. Tính utilization rate cho mỗi giờ
            var totalPoints = await _db.ChargingPoints
                .CountAsync(p => p.StationId == stationId);

            var peakHourDetails = hourlyStats.Select(stat =>
            {
                // Tính utilization rate: số session / (số điểm sạc * số ngày trong period)
                var daysInPeriod = (endDate - startDate).TotalDays;
                var utilizationRate = 0.0;
                if (totalPoints > 0 && daysInPeriod > 0)
                {
                    // Giả sử mỗi giờ có thể có tối đa số điểm sạc session
                    var maxPossibleSessions = totalPoints * daysInPeriod;
                    utilizationRate = maxPossibleSessions > 0
                        ? (stat.SessionCount / maxPossibleSessions) * 100
                        : 0;
                }

                // Tính concurrent sessions (ước tính dựa trên duration)
                var concurrentSessions = (int?)Math.Ceiling(stat.AverageDurationMinutes / 60.0 * stat.SessionCount);

                return new PeakHourDetailDto
                {
                    Hour = stat.Hour,
                    SessionCount = stat.SessionCount,
                    AverageDurationMinutes = Math.Round(stat.AverageDurationMinutes, 2),
                    UtilizationRate = Math.Round(utilizationRate, 2),
                    Revenue = stat.Revenue,
                    AverageEnergyUsed = stat.AverageEnergyUsed,
                    ConcurrentSessions = concurrentSessions
                };
            }).ToList();

            // 5. Lấy top 3 giờ cao điểm
            var topPeakHours = peakHourDetails
                .OrderByDescending(x => x.SessionCount)
                .Take(3)
                .ToList();

            // 6. Tạo peak hour range string
            var peakHourRange = topPeakHours.Any()
                ? string.Join(", ", topPeakHours.Select(h => $"{h.Hour}:00 - {h.Hour + 1}:00"))
                : "N/A";

            // 7. Tạo recommendations
            var recommendations = new List<string>();
            
            if (topPeakHours.Any())
            {
                var maxPeakHour = topPeakHours.First();
                if (maxPeakHour.UtilizationRate > 80)
                {
                    recommendations.Add($"Giờ cao điểm {maxPeakHour.Hour}:00 có tỷ lệ sử dụng {maxPeakHour.UtilizationRate:F1}%. Nên xem xét tăng số điểm sạc vào giờ này.");
                }
                
                if (maxPeakHour.ConcurrentSessions.HasValue && maxPeakHour.ConcurrentSessions > totalPoints)
                {
                    recommendations.Add($"Giờ {maxPeakHour.Hour}:00 có nhiều session đồng thời ({maxPeakHour.ConcurrentSessions}) hơn số điểm sạc ({totalPoints}). Cần mở rộng hạ tầng.");
                }

                var lowUtilizationHours = peakHourDetails
                    .Where(h => h.UtilizationRate < 30 && h.SessionCount > 0)
                    .OrderBy(h => h.UtilizationRate)
                    .Take(3)
                    .ToList();

                if (lowUtilizationHours.Any())
                {
                    recommendations.Add($"Các giờ có tỷ lệ sử dụng thấp: {string.Join(", ", lowUtilizationHours.Select(h => $"{h.Hour}:00 ({h.UtilizationRate:F1}%)"))}. Có thể giảm giá vào các giờ này để tăng nhu cầu.");
                }
            }

            if (!recommendations.Any())
            {
                recommendations.Add("Tỷ lệ sử dụng trạm đang ở mức ổn định.");
            }

            return new StationPeakHoursDto
            {
                StationId = stationId,
                StationName = station.Name,
                Period = $"{startDate:dd/MM/yyyy} - {endDate:dd/MM/yyyy}",
                PeakHours = topPeakHours,
                PeakHourRange = peakHourRange,
                Recommendations = recommendations
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

                // 5. Map to DTOs (bao gồm thông tin role của reporter)
                var reports = incidents.Select(ir => new IncidentReportResponse
                {
                    ReportId = ir.ReportId,
                    ReporterId = ir.ReporterId,
                    ReporterName = ir.Reporter?.Name ?? "Unknown",
                    ReporterRole = ir.Reporter?.Role ?? "Unknown", // Thêm role của người báo cáo
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
                    ResolvedByName = ir.ResolvedByNavigation?.Name,
                    AdminNotes = ir.AdminNotes
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
                    ReporterRole = incident.Reporter?.Role ?? "Unknown",
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
                    ResolvedByName = incident.ResolvedByNavigation?.Name,
                    AdminNotes = incident.AdminNotes
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

                // 3. Save old status before updating
                var oldStatus = incident.Status;

                // 4. Update status
                var newStatus = request.Status.ToLower();
                incident.Status = newStatus;

                // 5. Update admin notes if provided
                if (!string.IsNullOrWhiteSpace(request.Notes))
                {
                    incident.AdminNotes = request.Notes;
                }

                // 6. Handle status transitions
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

                // 7. Save changes
                await _db.SaveChangesAsync();

                // 8. Notify reporter (staff/driver) about the update
                try
                {
                    var statusChanged = oldStatus != newStatus;
                    
                    string notificationTitle;
                    string notificationMessage;
                    
                    if (newStatus == "resolved")
                    {
                        notificationTitle = $"Báo cáo sự cố đã được giải quyết - #{reportId}";
                        notificationMessage = $"Báo cáo sự cố của bạn \"{incident.Title}\" đã được admin giải quyết.";
                        if (!string.IsNullOrWhiteSpace(request.Notes))
                        {
                            notificationMessage += $" Phản hồi: {request.Notes}";
                        }
                    }
                    else if (newStatus == "in_progress")
                    {
                        notificationTitle = $"Báo cáo sự cố đang được xử lý - #{reportId}";
                        notificationMessage = $"Báo cáo sự cố của bạn \"{incident.Title}\" đang được admin xử lý.";
                        if (!string.IsNullOrWhiteSpace(request.Notes))
                        {
                            notificationMessage += $" Ghi chú: {request.Notes}";
                        }
                    }
                    else if (!string.IsNullOrWhiteSpace(request.Notes))
                    {
                        // Nếu có notes nhưng không phải resolved/in_progress
                        notificationTitle = $"Cập nhật báo cáo sự cố - #{reportId}";
                        notificationMessage = $"Admin đã cập nhật báo cáo sự cố \"{incident.Title}\" của bạn. Phản hồi: {request.Notes}";
                    }
                    else
                    {
                        // Chỉ update status, không có notes
                        notificationTitle = $"Cập nhật trạng thái báo cáo sự cố - #{reportId}";
                        notificationMessage = $"Trạng thái báo cáo sự cố \"{incident.Title}\" của bạn đã được cập nhật thành: {newStatus}.";
                    }
                    
                    Console.WriteLine($"[ADMIN UPDATE] Notifying reporter {incident.ReporterId} about incident report {reportId} update");
                    await _notificationService.SendNotificationAsync(
                        userId: incident.ReporterId,
                        title: notificationTitle,
                        message: notificationMessage,
                        type: "incident_update",
                        relatedId: reportId
                    );
                    Console.WriteLine($"[ADMIN UPDATE] Notification sent successfully to reporter {incident.ReporterId}");
                }
                catch (Exception notifEx)
                {
                    // Log lỗi nhưng không fail toàn bộ request
                    Console.WriteLine($"[ADMIN UPDATE ERROR] Failed to send notification to reporter: {notifEx.Message}");
                    Console.WriteLine($"[ADMIN UPDATE ERROR] StackTrace: {notifEx.StackTrace}");
                }

                // 7. Reload to get updated ResolvedByNavigation
                await _db.Entry(incident).Reference(ir => ir.ResolvedByNavigation).LoadAsync();

                // 8. Return updated report
                return new IncidentReportResponse
                {
                    ReportId = incident.ReportId,
                    ReporterId = incident.ReporterId,
                    ReporterName = incident.Reporter?.Name ?? "Unknown",
                    ReporterRole = incident.Reporter?.Role ?? "Unknown",
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
                    ResolvedByName = incident.ResolvedByNavigation?.Name,
                    AdminNotes = incident.AdminNotes
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
