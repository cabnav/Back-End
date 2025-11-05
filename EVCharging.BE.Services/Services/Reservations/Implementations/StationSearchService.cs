using EVCharging.BE.Common.DTOs.Reservations;
using EVCharging.BE.DAL;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using EVCharging.BE.Services.Services.Background;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace EVCharging.BE.Services.Services.Reservations.Implementations
{
    /// <summary>
    /// Service để tìm kiếm trạm sạc phù hợp với xe và khung giờ
    /// </summary>
    public class StationSearchService : IStationSearchService
    {
        private readonly EvchargingManagementContext _db;
        private readonly ReservationBackgroundOptions _opt;

        public StationSearchService(EvchargingManagementContext db, IOptions<ReservationBackgroundOptions> opt)
        {
            _db = db;
            _opt = opt.Value;
        }

        /// <summary>
        /// Tìm kiếm trạm sạc phù hợp với xe và ngày đặt chỗ
        /// </summary>
        public async Task<List<StationSearchResponse>> SearchCompatibleStationsAsync(StationSearchRequest request)
        {
            // Lấy tất cả trạm sạc có điểm sạc phù hợp với loại cổng
            var stationsQuery = _db.ChargingStations
                .Where(s => s.Status == "active")
                .Include(s => s.ChargingPoints)
                .Where(s => s.ChargingPoints.Any(p => p.ConnectorType == request.ConnectorType && p.Status == "available"));

            // Nếu có tọa độ, sắp xếp theo khoảng cách
            if (request.Latitude.HasValue && request.Longitude.HasValue)
            {
                var stations = await stationsQuery.ToListAsync();
                
                var stationsWithDistance = stations.Select(s => new
                {
                    Station = s,
                    Distance = CalculateDistance(
                        request.Latitude.Value, 
                        request.Longitude.Value, 
                        s.Latitude ?? 0, 
                        s.Longitude ?? 0)
                })
                .Where(x => x.Distance <= request.RadiusKm)
                .OrderBy(x => x.Distance)
                .Take(20) // Giới hạn 20 trạm gần nhất
                .ToList();

                var result = new List<StationSearchResponse>();
                
                foreach (var item in stationsWithDistance)
                {
                    var compatiblePoints = await GetCompatiblePointsAsync(item.Station.StationId, request.ConnectorType);
                    
                    result.Add(new StationSearchResponse
                    {
                        Station = new EVCharging.BE.Common.DTOs.Stations.StationDTO
                        {
                            StationId = item.Station.StationId,
                            Name = item.Station.Name,
                            Address = item.Station.Address,
                            Latitude = (decimal)(item.Station.Latitude ?? 0),
                            Longitude = (decimal)(item.Station.Longitude ?? 0),
                            Operator = item.Station.Operator,
                            Status = item.Station.Status,
                            TotalPoints = item.Station.TotalPoints ?? 0,
                            AvailablePoints = item.Station.AvailablePoints ?? 0
                        },
                        CompatiblePointsCount = compatiblePoints.Count,
                        DistanceKm = item.Distance,
                        CompatiblePoints = compatiblePoints
                    });
                }

                return result;
            }
            else
            {
                // Không có tọa độ, trả về tất cả trạm phù hợp
                var stations = await stationsQuery.Take(20).ToListAsync();
                var result = new List<StationSearchResponse>();

                foreach (var station in stations)
                {
                    var compatiblePoints = await GetCompatiblePointsAsync(station.StationId, request.ConnectorType);
                    
                    result.Add(new StationSearchResponse
                    {
                        Station = new EVCharging.BE.Common.DTOs.Stations.StationDTO
                        {
                            StationId = station.StationId,
                            Name = station.Name,
                            Address = station.Address,
                            Latitude = (decimal)(station.Latitude ?? 0),
                            Longitude = (decimal)(station.Longitude ?? 0),
                            Operator = station.Operator,
                            Status = station.Status,
                            TotalPoints = station.TotalPoints ?? 0,
                            AvailablePoints = station.AvailablePoints ?? 0
                        },
                        CompatiblePointsCount = compatiblePoints.Count,
                        DistanceKm = 0, // Không có thông tin khoảng cách
                        CompatiblePoints = compatiblePoints
                    });
                }

                return result;
            }
        }

        /// <summary>
        /// Lấy danh sách điểm sạc phù hợp tại một trạm cụ thể
        /// </summary>
        public async Task<List<CompatibleChargingPointDTO>> GetCompatiblePointsAsync(int stationId, string connectorType)
        {
            var points = await _db.ChargingPoints
                .Where(p => p.StationId == stationId 
                    && p.ConnectorType == connectorType 
                    && p.Status == "available")
                .Include(p => p.Station)
                .Select(p => new CompatibleChargingPointDTO
                {
                    PointId = p.PointId,
                    StationId = p.StationId,
                    ConnectorType = p.ConnectorType ?? "",
                    PowerOutput = p.PowerOutput ?? 0,
                    PricePerKwh = p.PricePerKwh,
                    Status = p.Status ?? "",
                    StationName = p.Station.Name,
                    StationAddress = p.Station.Address
                })
                .ToListAsync();

            return points;
        }

        /// <summary>
        /// Lấy danh sách khung giờ có sẵn cho một điểm sạc trong ngày cụ thể
        /// </summary>
        public async Task<List<TimeSlotDTO>> GetAvailableTimeSlotsAsync(int pointId, DateTime date)
        {
            var timeSlots = new List<TimeSlotDTO>();
            
            // Tạo 24 khung giờ trong ngày
            for (int hour = 0; hour < 24; hour++)
            {
                // Chuẩn hóa về UTC để so sánh nhất quán
                var dateUtc = date.Kind == DateTimeKind.Utc
                    ? date
                    : DateTime.SpecifyKind(date, DateTimeKind.Utc);

                var startTime = dateUtc.Date.AddHours(hour);
                var endTime = startTime.AddHours(1);

                // Kiểm tra xem khung giờ này có bị đặt chỗ không
                // ✅ CHỈ check các status đang active: booked, checked_in, in_progress
                // ✅ KHÔNG check completed, cancelled, no_show vì những slot đó đã trống
                var hasReservation = await _db.Reservations
                    .Where(r => r.PointId == pointId 
                        && (r.Status == "booked" || r.Status == "checked_in" || r.Status == "in_progress")
                        && !(endTime <= r.StartTime || startTime >= r.EndTime))
                    .AnyAsync();

                // ✅ Kiểm tra xem có walk-in session đang active trong slot này không
                var now = DateTime.UtcNow;
                // Chỉ lấy các session có thể overlap với slot này
                // Session overlap nếu: session.StartTime < slot.endTime VÀ (session.EndTime ?? maxEndTime ?? now) > slot.startTime
                var walkInSessions = await _db.ChargingSessions
                    .Where(s => 
                        s.PointId == pointId 
                        && s.Status == "in_progress"
                        && !s.ReservationId.HasValue // Walk-in session (không có ReservationId)
                        && s.StartTime < endTime) // Session bắt đầu trước khi slot kết thúc (điều kiện tối thiểu)
                    .ToListAsync();
                
                var hasWalkInSession = false;
                foreach (var session in walkInSessions)
                {
                    DateTime? effectiveEndTime = null;
                    
                    // Nếu session có EndTime, dùng EndTime
                    if (session.EndTime.HasValue)
                    {
                        effectiveEndTime = session.EndTime.Value;
                    }
                    // Nếu session chưa có EndTime nhưng có maxEndTime trong Notes, dùng maxEndTime
                    else if (!string.IsNullOrEmpty(session.Notes))
                    {
                        try
                        {
                            var notesJson = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(session.Notes);
                            if (notesJson.TryGetProperty("maxEndTime", out var maxEndTimeElement))
                            {
                                if (maxEndTimeElement.TryGetDateTime(out var maxEndTime))
                                {
                                    effectiveEndTime = maxEndTime;
                                }
                            }
                        }
                        catch
                        {
                            // Ignore parse errors
                        }
                    }
                    
                    // Nếu không có EndTime và không có maxEndTime, chỉ block slot hiện tại (nếu now trong slot)
                    if (!effectiveEndTime.HasValue)
                    {
                        // Chỉ block slot mà session đang active (now trong slot) hoặc slot mà session bắt đầu
                        // Điều kiện: session bắt đầu trong slot này HOẶC (session đã bắt đầu VÀ now đang trong slot này)
                        // QUAN TRỌNG: Không block các slot tương lai nếu session không có EndTime
                        var sessionStartInSlot = session.StartTime >= startTime && session.StartTime < endTime;
                        var nowInSlot = session.StartTime <= now && now >= startTime && now < endTime;
                        
                        // Chỉ block nếu session bắt đầu trong slot này HOẶC session đang active (now trong slot)
                        // KHÔNG block các slot tương lai nếu session không có EndTime
                        if (sessionStartInSlot || nowInSlot)
                        {
                            hasWalkInSession = true;
                            break;
                        }
                        // Nếu không có điều kiện nào thỏa mãn, tiếp tục check session tiếp theo
                    }
                    else
                    {
                        // Có effectiveEndTime: check overlap bình thường
                        if (effectiveEndTime.Value > startTime && session.StartTime < endTime)
                        {
                            hasWalkInSession = true;
                            break;
                        }
                    }
                }

                // Kiểm tra xem có trong quá khứ không (chỉ block slot đã kết thúc + buffer 5 phút)
                var bufferMinutes = 5;
                var cutoffTime = now.AddMinutes(bufferMinutes);
                var isInPast = endTime <= cutoffTime;

                // Không cho đặt nếu đã quá BookingCutoff kể từ start (ví dụ 15 phút)
                var tooLateToBook = now > startTime.AddMinutes(_opt.BookingCutoffMinutes);

                timeSlots.Add(new TimeSlotDTO
                {
                    Hour = hour,
                    StartTime = startTime,
                    EndTime = endTime,
                    IsAvailable = !hasReservation && !hasWalkInSession && !isInPast && !tooLateToBook,
                    AvailablePointsCount = (hasReservation || hasWalkInSession) ? 0 : 1 // Đơn giản hóa, mỗi điểm sạc chỉ có 1 cổng
                });
            }

            return timeSlots;
        }

        /// <summary>
        /// Tính khoảng cách giữa hai điểm (Haversine formula)
        /// </summary>
        private double CalculateDistance(double lat1, double lon1, double lat2, double lon2)
        {
            const double R = 6371; // Bán kính Trái Đất (km)
            
            var dLat = ToRadians(lat2 - lat1);
            var dLon = ToRadians(lon2 - lon1);
            
            var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                    Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2)) *
                    Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
            
            var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
            
            return R * c;
        }

        private double ToRadians(double degrees)
        {
            return degrees * (Math.PI / 180);
        }
    }
}
