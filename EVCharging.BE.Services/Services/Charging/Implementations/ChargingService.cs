using EVCharging.BE.Common.DTOs.Charging;
using EVCharging.BE.Common.DTOs.Stations;
using EVCharging.BE.Common.DTOs.Users;
using EVCharging.BE.DAL;
using EVCharging.BE.DAL.Entities;
using Microsoft.EntityFrameworkCore;

namespace EVCharging.BE.Services.Services.Charging.Implementations
{
    /// <summary>
    /// Service quản lý phiên sạc - core logic
    /// </summary>
    public class ChargingService : IChargingService
    {
        private readonly EvchargingManagementContext _db;
        private readonly ICostCalculationService _costCalculationService;
        private readonly ISessionMonitorService _sessionMonitorService;

        public ChargingService(
            EvchargingManagementContext db, 
            ICostCalculationService costCalculationService,
            ISessionMonitorService sessionMonitorService)
        {
            _db = db;
            _costCalculationService = costCalculationService;
            _sessionMonitorService = sessionMonitorService;
        }

        /// <summary>
        /// Bắt đầu phiên sạc
        /// </summary>
        public async Task<ChargingSessionResponse?> StartSessionAsync(ChargingSessionStartRequest request)
        {
            try
            {
                // Validate inputs
                if (!await ValidateChargingPointAsync(request.ChargingPointId))
                    return null;

                if (!await ValidateDriverAsync(request.DriverId))
                    return null;

                if (!await CanStartSessionAsync(request.ChargingPointId, request.DriverId))
                    return null;

                // Get charging point and driver info
                var chargingPoint = await _db.ChargingPoints
                    .Include(cp => cp.Station)
                    .FirstOrDefaultAsync(cp => cp.PointId == request.ChargingPointId);

                var driver = await _db.DriverProfiles
                    .Include(d => d.User)
                    .FirstOrDefaultAsync(d => d.DriverId == request.DriverId);

                if (chargingPoint == null || driver == null)
                    return null;

                // Create new charging session
                var session = new ChargingSession
                {
                    DriverId = request.DriverId,
                    PointId = request.ChargingPointId,
                    StartTime = DateTime.UtcNow,
                    InitialSoc = request.InitialSOC,
                    Status = "in_progress",
                    EnergyUsed = 0,
                    DurationMinutes = 0,
                    CostBeforeDiscount = 0,
                    AppliedDiscount = 0,
                    FinalCost = 0
                };

                _db.ChargingSessions.Add(session);
                await _db.SaveChangesAsync();

                // Update charging point status
                chargingPoint.Status = "in_use";
                await _db.SaveChangesAsync();

                // Start monitoring
                await _sessionMonitorService.StartMonitoringAsync(session.SessionId);

                // Return response
                return await GetSessionByIdAsync(session.SessionId);
            }
            catch (Exception ex)
            {
                // Log error
                Console.WriteLine($"Error starting charging session: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Dừng phiên sạc
        /// </summary>
        public async Task<ChargingSessionResponse?> StopSessionAsync(ChargingSessionStopRequest request)
        {
            try
            {
                var session = await _db.ChargingSessions
                    .Include(s => s.Point)
                    .Include(s => s.Driver)
                    .FirstOrDefaultAsync(s => s.SessionId == request.SessionId);

                if (session == null || session.Status != "in_progress")
                    return null;

                // Calculate final values
                session.EndTime = DateTime.UtcNow;
                session.FinalSoc = request.FinalSOC;
                session.DurationMinutes = (int)(session.EndTime.Value - session.StartTime).TotalMinutes;
                session.Status = "completed";

                // Calculate cost
                var costRequest = new CostCalculationRequest
                {
                    ChargingPointId = session.PointId,
                    EnergyUsed = session.EnergyUsed ?? 0,
                    DurationMinutes = session.DurationMinutes ?? 0,
                    UserId = session.Driver?.UserId
                };

                var costResponse = await _costCalculationService.CalculateCostAsync(costRequest);
                session.CostBeforeDiscount = costResponse.BaseCost;
                session.AppliedDiscount = costResponse.TotalDiscount;
                session.FinalCost = costResponse.FinalCost;

                await _db.SaveChangesAsync();

                // Update charging point status
                var chargingPoint = await _db.ChargingPoints.FindAsync(session.PointId);
                if (chargingPoint != null)
                {
                    chargingPoint.Status = "available";
                    await _db.SaveChangesAsync();
                }

                // Stop monitoring
                await _sessionMonitorService.StopMonitoringAsync(session.SessionId);

                // Send completion notification
                await _sessionMonitorService.SendSessionCompleteNotificationAsync(session.SessionId);

                return await GetSessionByIdAsync(session.SessionId);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error stopping charging session: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Cập nhật trạng thái phiên sạc
        /// </summary>
        public async Task<ChargingSessionResponse?> UpdateSessionStatusAsync(ChargingSessionStatusRequest request)
        {
            try
            {
                var session = await _db.ChargingSessions.FindAsync(request.SessionId);
                if (session == null)
                    return null;

                // Update status
                session.Status = request.Status;

                // Update real-time data if provided
                if (request.CurrentSOC.HasValue || request.CurrentPower.HasValue || 
                    request.Voltage.HasValue || request.Temperature.HasValue)
                {
                    await _sessionMonitorService.UpdateSessionDataAsync(
                        request.SessionId,
                        request.CurrentSOC ?? session.FinalSoc ?? session.InitialSoc,
                        request.CurrentPower ?? 0,
                        request.Voltage ?? 0,
                        request.Temperature ?? 0
                    );
                }

                await _db.SaveChangesAsync();
                return await GetSessionByIdAsync(session.SessionId);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error updating session status: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Lấy thông tin phiên sạc theo ID
        /// </summary>
        public async Task<ChargingSessionResponse?> GetSessionByIdAsync(int sessionId)
        {
            try
            {
                var session = await _db.ChargingSessions
                    .Include(s => s.Point)
                        .ThenInclude(p => p.Station)
                    .Include(s => s.Driver)
                        .ThenInclude(d => d.User)
                    .Include(s => s.SessionLogs)
                    .FirstOrDefaultAsync(s => s.SessionId == sessionId);

                if (session == null)
                    return null;

                return MapToResponse(session);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting session by ID: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Lấy danh sách phiên sạc đang hoạt động
        /// </summary>
        public async Task<IEnumerable<ChargingSessionResponse>> GetActiveSessionsAsync()
        {
            try
            {
                var sessions = await _db.ChargingSessions
                    .Include(s => s.Point)
                        .ThenInclude(p => p.Station)
                    .Include(s => s.Driver)
                        .ThenInclude(d => d.User)
                    .Where(s => s.Status == "in_progress")
                    .ToListAsync();

                return sessions.Select(MapToResponse);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting active sessions: {ex.Message}");
                return new List<ChargingSessionResponse>();
            }
        }

        /// <summary>
        /// Lấy danh sách phiên sạc theo driver
        /// </summary>
        public async Task<IEnumerable<ChargingSessionResponse>> GetSessionsByDriverAsync(int driverId)
        {
            try
            {
                var sessions = await _db.ChargingSessions
                    .Include(s => s.Point)
                        .ThenInclude(p => p.Station)
                    .Include(s => s.Driver)
                        .ThenInclude(d => d.User)
                    .Where(s => s.DriverId == driverId)
                    .OrderByDescending(s => s.StartTime)
                    .ToListAsync();

                return sessions.Select(MapToResponse);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting sessions by driver: {ex.Message}");
                return new List<ChargingSessionResponse>();
            }
        }

        /// <summary>
        /// Lấy danh sách phiên sạc theo trạm
        /// </summary>
        public async Task<IEnumerable<ChargingSessionResponse>> GetSessionsByStationAsync(int stationId)
        {
            try
            {
                var sessions = await _db.ChargingSessions
                    .Include(s => s.Point)
                        .ThenInclude(p => p.Station)
                    .Include(s => s.Driver)
                        .ThenInclude(d => d.User)
                    .Where(s => s.Point.StationId == stationId)
                    .OrderByDescending(s => s.StartTime)
                    .ToListAsync();

                return sessions.Select(MapToResponse);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting sessions by station: {ex.Message}");
                return new List<ChargingSessionResponse>();
            }
        }

        /// <summary>
        /// Tạo log cho phiên sạc
        /// </summary>
        public async Task<bool> CreateSessionLogAsync(SessionLogCreateRequest request)
        {
            try
            {
                var sessionLog = new SessionLog
                {
                    SessionId = request.SessionId,
                    SocPercentage = request.SOCPercentage,
                    CurrentPower = request.CurrentPower,
                    Voltage = request.Voltage,
                    Temperature = request.Temperature,
                    LogTime = request.LogTime ?? DateTime.UtcNow
                };

                _db.SessionLogs.Add(sessionLog);
                await _db.SaveChangesAsync();

                // Update session progress
                if (request.SOCPercentage.HasValue || request.CurrentPower.HasValue)
                {
                    await _sessionMonitorService.UpdateSessionDataAsync(
                        request.SessionId,
                        request.SOCPercentage ?? 0,
                        request.CurrentPower ?? 0,
                        request.Voltage ?? 0,
                        request.Temperature ?? 0
                    );
                }

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error creating session log: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Lấy logs của phiên sạc
        /// </summary>
        public async Task<IEnumerable<SessionLogDTO>> GetSessionLogsAsync(int sessionId)
        {
            try
            {
                var logs = await _db.SessionLogs
                    .Where(sl => sl.SessionId == sessionId)
                    .OrderBy(sl => sl.LogTime)
                    .ToListAsync();

                return logs.Select(log => new SessionLogDTO
                {
                    LogId = log.LogId,
                    SessionId = log.SessionId,
                    SOCPercentage = log.SocPercentage,
                    CurrentPower = log.CurrentPower,
                    Voltage = log.Voltage,
                    Temperature = log.Temperature,
                    LogTime = log.LogTime
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting session logs: {ex.Message}");
                return new List<SessionLogDTO>();
            }
        }

        /// <summary>
        /// Cập nhật tiến trình phiên sạc
        /// </summary>
        public async Task<bool> UpdateSessionProgressAsync(int sessionId, int soc, decimal power, decimal voltage, decimal temperature)
        {
            try
            {
                var session = await _db.ChargingSessions.FindAsync(sessionId);
                if (session == null || session.Status != "in_progress")
                    return false;

                // Create session log
                var logRequest = new SessionLogCreateRequest
                {
                    SessionId = sessionId,
                    SOCPercentage = soc,
                    CurrentPower = power,
                    Voltage = voltage,
                    Temperature = temperature
                };

                await CreateSessionLogAsync(logRequest);

                // Update session energy usage (simplified calculation)
                var timeElapsed = DateTime.UtcNow - session.StartTime;
                var energyUsed = (decimal)((double)power * timeElapsed.TotalHours);
                session.EnergyUsed = energyUsed;

                await _db.SaveChangesAsync();

                // Notify real-time updates
                var sessionData = await GetSessionByIdAsync(sessionId);
                if (sessionData != null)
                {
                    await _sessionMonitorService.NotifySessionUpdateAsync(sessionId, sessionData);
                }

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error updating session progress: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Validate charging point
        /// </summary>
        public async Task<bool> ValidateChargingPointAsync(int chargingPointId)
        {
            try
            {
                var chargingPoint = await _db.ChargingPoints
                    .Include(cp => cp.Station)
                    .FirstOrDefaultAsync(cp => cp.PointId == chargingPointId);

                return chargingPoint != null && 
                       chargingPoint.Status == "available" && 
                       chargingPoint.Station.Status == "active";
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Validate driver
        /// </summary>
        public async Task<bool> ValidateDriverAsync(int driverId)
        {
            try
            {
                var driver = await _db.DriverProfiles
                    .Include(d => d.User)
                    .FirstOrDefaultAsync(d => d.DriverId == driverId);

                return driver != null && driver.User != null;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Kiểm tra có thể bắt đầu phiên sạc không
        /// </summary>
        public async Task<bool> CanStartSessionAsync(int chargingPointId, int driverId)
        {
            try
            {
                // Check if driver has any active sessions
                var activeSession = await _db.ChargingSessions
                    .FirstOrDefaultAsync(s => s.DriverId == driverId && s.Status == "in_progress");

                if (activeSession != null)
                    return false;

                // Check if charging point is available
                var chargingPoint = await _db.ChargingPoints.FindAsync(chargingPointId);
                if (chargingPoint?.Status != "available")
                    return false;

                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Map entity to response DTO
        /// </summary>
        public ChargingSessionResponse MapToResponse(ChargingSession session)
        {
                return new ChargingSessionResponse
                {
                    SessionId = session.SessionId,
                    DriverId = session.DriverId,
                    ChargingPointId = session.PointId,
                    StartTime = session.StartTime,
                    EndTime = session.EndTime,
                    InitialSOC = session.InitialSoc,
                    FinalSOC = session.FinalSoc,
                    EnergyUsed = session.EnergyUsed ?? 0,
                    DurationMinutes = session.DurationMinutes ?? 0,
                    CostBeforeDiscount = session.CostBeforeDiscount ?? 0,
                    AppliedDiscount = session.AppliedDiscount ?? 0,
                    FinalCost = session.FinalCost ?? 0,
                    Status = session.Status ?? "unknown",
                ChargingPoint = new ChargingPointDTO
                {
                    PointId = session.Point?.PointId ?? 0,
                    StationId = session.Point?.StationId ?? 0,
                    ConnectorType = session.Point?.ConnectorType ?? "",
                    PowerOutput = session.Point?.PowerOutput ?? 0,
                    PricePerKwh = session.Point?.PricePerKwh ?? 0,
                    Status = session.Point?.Status ?? "",
                    QrCode = session.Point?.QrCode ?? ""
                },
                Driver = new DriverProfileDTO
                {
                    DriverId = session.Driver?.DriverId ?? 0,
                    LicenseNumber = session.Driver?.LicenseNumber ?? "",
                    VehicleModel = session.Driver?.VehicleModel ?? "",
                    VehiclePlate = session.Driver?.VehiclePlate ?? "",
                    BatteryCapacity = session.Driver?.BatteryCapacity ?? 0
                },
                SessionLogs = session.SessionLogs?.Select(log => new SessionLogDTO
                {
                    LogId = log.LogId,
                    SessionId = log.SessionId,
                    SOCPercentage = log.SocPercentage,
                    CurrentPower = log.CurrentPower,
                    Voltage = log.Voltage,
                    Temperature = log.Temperature,
                    LogTime = log.LogTime
                }).ToList() ?? new List<SessionLogDTO>(),
                LastUpdated = DateTime.UtcNow
            };
        }
    }
}