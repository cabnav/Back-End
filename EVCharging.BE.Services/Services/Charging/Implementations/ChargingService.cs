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
                // Validate charging point với message cụ thể
                var chargingPoint = await _db.ChargingPoints
                    .Include(cp => cp.Station)
                    .FirstOrDefaultAsync(cp => cp.PointId == request.ChargingPointId);

                if (chargingPoint == null)
                    throw new KeyNotFoundException("Charging point not found (không tìm thấy điểm sạc).");

                if (chargingPoint.Status != "available")
                    throw new InvalidOperationException($"Charging point is not available (điểm sạc không khả dụng). Current status: {chargingPoint.Status ?? "null"}");

                if (chargingPoint.Station == null || chargingPoint.Station.Status != "active")
                    throw new InvalidOperationException("Charging station is not active (trạm sạc không hoạt động).");

                // Validate driver với message cụ thể
                var driver = await _db.DriverProfiles
                    .Include(d => d.User)
                    .FirstOrDefaultAsync(d => d.DriverId == request.DriverId);

                if (driver == null || driver.User == null)
                    throw new KeyNotFoundException("Driver profile not found or driver does not have a user account (không tìm thấy hồ sơ tài xế hoặc tài xế chưa có tài khoản).");

                // Kiểm tra driver có session đang chạy không
                var hasActiveSession = await _db.ChargingSessions
                    .AnyAsync(s => s.DriverId == request.DriverId && s.Status == "in_progress");

                if (hasActiveSession)
                    throw new InvalidOperationException("Driver already has an active charging session (tài xế đang có phiên sạc đang hoạt động).");

                // Kiểm tra lại point có đang bận không (có thể bị thay đổi sau khi validate)
                if (chargingPoint.Status != "available")
                    throw new InvalidOperationException("Charging point is no longer available (điểm sạc không còn khả dụng).");

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
                
                // Update charging point status
                chargingPoint.Status = "in_use";
                
                await _db.SaveChangesAsync();

                // Start monitoring
                await _sessionMonitorService.StartMonitoringAsync(session.SessionId);

                // Return response
                return await GetSessionByIdAsync(session.SessionId);
            }
            catch (KeyNotFoundException)
            {
                // Re-throw validation exceptions để controller xử lý
                throw;
            }
            catch (InvalidOperationException)
            {
                // Re-throw validation exceptions để controller xử lý
                throw;
            }
            catch (Exception ex)
            {
                // Log error nhưng không throw để tránh crash
                Console.WriteLine($"Unexpected error starting charging session: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
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

                // Update payment amount if there's a pending payment for this session (walk-in cash/card/pos)
                var pendingPayment = await _db.Payments
                    .FirstOrDefaultAsync(p => p.SessionId == session.SessionId && 
                                              p.PaymentStatus == "pending" &&
                                              (p.PaymentMethod == "cash" || p.PaymentMethod == "card" || p.PaymentMethod == "pos"));
                
                if (pendingPayment != null && session.FinalCost.HasValue)
                {
                    // Update payment amount to actual final cost
                    pendingPayment.Amount = session.FinalCost.Value;
                    Console.WriteLine($"Updated payment {pendingPayment.PaymentId} amount from {pendingPayment.Amount} to {session.FinalCost.Value}");
                }

                // Update charging point status
                var chargingPoint = await _db.ChargingPoints.FindAsync(session.PointId);
                if (chargingPoint != null)
                {
                    chargingPoint.Status = "available";
                }
                
                await _db.SaveChangesAsync();

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
        /// Cập nhật trạng thái phiên sạc (chỉ status, không update real-time data)
        /// Real-time data được tự động update qua SessionMonitorService
        /// </summary>
        public async Task<ChargingSessionResponse?> UpdateSessionStatusAsync(ChargingSessionStatusRequest request)
        {
            try
            {
                var session = await _db.ChargingSessions.FindAsync(request.SessionId);
                if (session == null)
                    return null;

                // Chỉ update status, real-time data sẽ được tự động update qua monitor
                session.Status = request.Status;
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
            var session = await _db.ChargingSessions
                .Include(s => s.Point)
                    .ThenInclude(p => p.Station)
                .Include(s => s.Driver)
                    .ThenInclude(d => d.User)
                .Include(s => s.SessionLogs)
                .FirstOrDefaultAsync(s => s.SessionId == sessionId);

            return session == null ? null : MapToResponse(session);
        }

        /// <summary>
        /// Lấy danh sách phiên sạc đang hoạt động
        /// </summary>
        public async Task<IEnumerable<ChargingSessionResponse>> GetActiveSessionsAsync()
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

        /// <summary>
        /// Lấy danh sách phiên sạc theo driver
        /// </summary>
        public async Task<IEnumerable<ChargingSessionResponse>> GetSessionsByDriverAsync(int driverId)
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

        /// <summary>
        /// Lấy danh sách phiên sạc theo trạm
        /// </summary>
        public async Task<IEnumerable<ChargingSessionResponse>> GetSessionsByStationAsync(int stationId)
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

        /// <summary>
        /// Tạo log cho phiên sạc
        /// </summary>
        public async Task<bool> CreateSessionLogAsync(SessionLogCreateRequest request)
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

            // Update session energy usage if provided
            if ((request.SOCPercentage.HasValue || request.CurrentPower.HasValue))
            {
                var session = await _db.ChargingSessions.FindAsync(request.SessionId);
                if (session?.Status == "in_progress")
                {
                    var timeElapsed = DateTime.UtcNow - session.StartTime;
                    var energyUsed = (decimal)((double)(request.CurrentPower ?? 0) * timeElapsed.TotalHours);
                    session.EnergyUsed = energyUsed;
                }
            }

            await _db.SaveChangesAsync();
            return true;
        }

        /// <summary>
        /// Lấy logs của phiên sạc
        /// </summary>
        public async Task<IEnumerable<SessionLogDTO>> GetSessionLogsAsync(int sessionId)
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

        /// <summary>
        /// Cập nhật tiến trình phiên sạc (tự động tạo log)
        /// </summary>
        public async Task<bool> UpdateSessionProgressAsync(int sessionId, int soc, decimal power, decimal voltage, decimal temperature)
        {
            var session = await _db.ChargingSessions.FindAsync(sessionId);
            if (session?.Status != "in_progress")
                return false;

            // Tạo session log trực tiếp
            var sessionLog = new SessionLog
            {
                SessionId = sessionId,
                SocPercentage = soc,
                CurrentPower = power,
                Voltage = voltage,
                Temperature = temperature,
                LogTime = DateTime.UtcNow
            };

            _db.SessionLogs.Add(sessionLog);

            // Update session energy usage
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

        /// <summary>
        /// Validate charging point availability
        /// </summary>
        public async Task<bool> ValidateChargingPointAsync(int chargingPointId)
        {
            var chargingPoint = await _db.ChargingPoints
                .Include(cp => cp.Station)
                .FirstOrDefaultAsync(cp => cp.PointId == chargingPointId);

            return chargingPoint != null && 
                   chargingPoint.Status == "available" && 
                   chargingPoint.Station?.Status == "active";
        }

        /// <summary>
        /// Validate driver exists and has user account
        /// </summary>
        public async Task<bool> ValidateDriverAsync(int driverId)
        {
            var driver = await _db.DriverProfiles
                .Include(d => d.User)
                .FirstOrDefaultAsync(d => d.DriverId == driverId);

            return driver?.User != null;
        }

        /// <summary>
        /// Kiểm tra có thể bắt đầu phiên sạc không
        /// </summary>
        public async Task<bool> CanStartSessionAsync(int chargingPointId, int driverId)
        {
            // Check if driver has any active sessions
            var hasActiveSession = await _db.ChargingSessions
                .AnyAsync(s => s.DriverId == driverId && s.Status == "in_progress");

            if (hasActiveSession)
                return false;

            // Check if charging point is available
            var chargingPoint = await _db.ChargingPoints.FindAsync(chargingPointId);
            return chargingPoint?.Status == "available";
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
                
                // Tính toán currentSOC, currentPower từ log mới nhất hoặc ước tính
                CurrentSOC = CalculateCurrentSOC(session),
                CurrentPower = CalculateCurrentPower(session),
                Voltage = session.SessionLogs?.OrderByDescending(l => l.LogTime).FirstOrDefault()?.Voltage,
                Temperature = session.SessionLogs?.OrderByDescending(l => l.LogTime).FirstOrDefault()?.Temperature,
                
                LastUpdated = DateTime.UtcNow
            };
        }

        /// <summary>
        /// Tính currentSOC từ log mới nhất hoặc ước tính từ thời gian
        /// </summary>
        private int? CalculateCurrentSOC(ChargingSession session)
        {
            // Nếu có log, lấy từ log mới nhất
            var latestLog = session.SessionLogs?.OrderByDescending(l => l.LogTime).FirstOrDefault();
            if (latestLog?.SocPercentage.HasValue == true)
            {
                return latestLog.SocPercentage.Value;
            }

            // Nếu chưa có log, ước tính từ thời gian và công suất
            if (session.Status == "in_progress" && session.Driver?.BatteryCapacity.HasValue == true && session.Point?.PowerOutput.HasValue == true)
            {
                var duration = DateTime.UtcNow - session.StartTime;
                var batteryCapacity = (decimal)session.Driver.BatteryCapacity.Value;
                var powerOutput = (decimal)session.Point.PowerOutput.Value;

                // Tính năng lượng có thể sạc được (không vượt quá dung lượng còn lại)
                var maxEnergyAvailable = batteryCapacity * (100 - session.InitialSoc) / 100;
                var estimatedEnergy = (decimal)duration.TotalHours * powerOutput;
                var actualEnergy = Math.Min(estimatedEnergy, maxEnergyAvailable);

                // Tính % SOC tăng thêm
                var socIncrease = (actualEnergy / batteryCapacity) * 100;
                var estimatedSOC = session.InitialSoc + (int)socIncrease;

                // Không vượt quá 100%
                return Math.Min(estimatedSOC, 100);
            }

            return null;
        }

        /// <summary>
        /// Tính currentPower từ log mới nhất hoặc dùng PowerOutput
        /// </summary>
        private decimal? CalculateCurrentPower(ChargingSession session)
        {
            // Nếu có log, lấy từ log mới nhất
            var latestLog = session.SessionLogs?.OrderByDescending(l => l.LogTime).FirstOrDefault();
            if (latestLog?.CurrentPower.HasValue == true)
            {
                return latestLog.CurrentPower.Value;
            }

            // Nếu chưa có log, dùng PowerOutput của điểm sạc
            return session.Point?.PowerOutput;
        }
    }
}