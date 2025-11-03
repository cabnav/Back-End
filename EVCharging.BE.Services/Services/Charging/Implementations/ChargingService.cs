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
            Console.WriteLine($"[StartSessionAsync] Starting session for PointId={request.ChargingPointId}, DriverId={request.DriverId}, StartAtUtc={request.StartAtUtc}");
            
            // Sử dụng EF Core execution strategy để tương thích với retry-on-failure
            var strategy = _db.Database.CreateExecutionStrategy();
            return await strategy.ExecuteAsync(async () =>
            {
                using var transaction = await _db.Database.BeginTransactionAsync();
                ChargingSession? session = null;
                var transactionCommitted = false;
                
                try
                {
                    // Re-validate trong transaction context

                    // Re-validate trong transaction context (có thể khác với validation ở controller)

                    // Truyền request.StartAtUtc để cho phép check-in sớm nếu session đang active sẽ kết thúc trước start time
                    if (!await ValidateChargingPointAsync(request.ChargingPointId, request.StartAtUtc))
                    {
                        Console.WriteLine($"⚠️ [StartSessionAsync] Charging point validation failed for PointId={request.ChargingPointId}, StartAtUtc={request.StartAtUtc}");
                        await transaction.RollbackAsync();
                        return null;
                    }

                    if (!await ValidateDriverAsync(request.DriverId))
                    {
                        Console.WriteLine($"⚠️ [StartSessionAsync] Driver validation failed for DriverId={request.DriverId}");
                        await transaction.RollbackAsync();
                        return null;
                    }

                    // Truyền request.StartAtUtc để cho phép check-in sớm nếu session đang active sẽ kết thúc trước start time
                    if (!await CanStartSessionAsync(request.ChargingPointId, request.DriverId, request.StartAtUtc))
                    {
                        Console.WriteLine($"⚠️ [StartSessionAsync] Cannot start session - PointId={request.ChargingPointId}, DriverId={request.DriverId}, StartAtUtc={request.StartAtUtc}");
                        await transaction.RollbackAsync();
                        return null;
                    }
                    
                    Console.WriteLine($"[StartSessionAsync] All validations passed. Proceeding with session creation...");

                    // Get charging point and driver info
                    var chargingPoint = await _db.ChargingPoints
                        .Include(cp => cp.Station)
                        .FirstOrDefaultAsync(cp => cp.PointId == request.ChargingPointId);

                    var driver = await _db.DriverProfiles
                        .Include(d => d.User)   
                        .FirstOrDefaultAsync(d => d.DriverId == request.DriverId);

                    if (chargingPoint == null || driver == null)
                    {
                        Console.WriteLine($"⚠️ [StartSessionAsync] Point or Driver not found - PointId={request.ChargingPointId}, DriverId={request.DriverId}");
                        await transaction.RollbackAsync();
                        return null;
                    }

                    Console.WriteLine($"[StartSessionAsync] Creating session - Point status: {chargingPoint.Status}, Station status: {chargingPoint.Station?.Status}");


                    // Nếu có ReservationCode, tìm reservation và set ReservationId
                    int? reservationId = null;
                    if (!string.IsNullOrEmpty(request.ReservationCode))
                    {
                        var reservation = await _db.Reservations
                            .FirstOrDefaultAsync(r => r.ReservationCode == request.ReservationCode && r.DriverId == request.DriverId);
                        if (reservation != null)
                        {
                            reservationId = reservation.ReservationId;
                            Console.WriteLine($"[StartSessionAsync] Found reservation - ReservationId={reservationId}, ReservationCode={request.ReservationCode}");
                        }
                    }
                    // Create new charging session
                    session = new ChargingSession
                    {
                        DriverId = request.DriverId,
                        PointId = request.ChargingPointId,
                        ReservationId = reservationId,
                        StartTime = request.StartAtUtc ?? DateTime.UtcNow,
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
                    
                    Console.WriteLine($"[StartSessionAsync] Saving session to database...");
                    await _db.SaveChangesAsync();
                    Console.WriteLine($"[StartSessionAsync] Session saved with SessionId={session.SessionId}");

                    // Start monitoring (có thể throw exception)
                    try
                    {
                        Console.WriteLine($"[StartSessionAsync] Starting monitoring for SessionId={session.SessionId}");
                        await _sessionMonitorService.StartMonitoringAsync(session.SessionId);
                        Console.WriteLine($"[StartSessionAsync] Monitoring started successfully");
                    }
                    catch (Exception monitorEx)
                    {
                        Console.WriteLine($"⚠️ [StartSessionAsync] Error starting monitoring: {monitorEx.Message}. Rolling back session creation.");
                        Console.WriteLine($"⚠️ [StartSessionAsync] Stack trace: {monitorEx.StackTrace}");
                        await transaction.RollbackAsync();
                        return null;
                    }

                    // Commit transaction TRƯỚC khi return response
                    // Đảm bảo session và point được lưu vào DB trước khi return
                    Console.WriteLine($"[StartSessionAsync] Committing transaction...");
                    await transaction.CommitAsync();
                    transactionCommitted = true;
                    Console.WriteLine($"[StartSessionAsync] Transaction committed successfully");

                    // Return response (nếu GetSessionByIdAsync fail thì vẫn OK vì transaction đã commit)
                    try
                    {
                        Console.WriteLine($"[StartSessionAsync] Retrieving session data for response... SessionId={session.SessionId}");
                        
                        // Force reload từ DB sau khi commit để tránh tracking issue
                        // Detach session entity hiện tại và reload
                        _db.Entry(session).State = Microsoft.EntityFrameworkCore.EntityState.Detached;
                        
                        var reloadedSession = await _db.ChargingSessions
                            .AsNoTracking()
                            .Include(s => s.Point)
                                .ThenInclude(p => p.Station)
                            .Include(s => s.Driver)
                                .ThenInclude(d => d.User)
                            .Include(s => s.SessionLogs)
                            .FirstOrDefaultAsync(s => s.SessionId == session.SessionId);
                        
                        if (reloadedSession == null)
                        {
                            Console.WriteLine($"⚠️ [StartSessionAsync] Could not reload session from DB after commit. SessionId={session.SessionId}");
                            // Thử lại với GetSessionByIdAsync
                            var response = await GetSessionByIdAsync(session.SessionId);
                            if (response != null)
                            {
                                Console.WriteLine($"[StartSessionAsync] GetSessionByIdAsync succeeded after reload failed. SessionId={session.SessionId}");
                                return response;
                            }
                            Console.WriteLine($"⚠️ [StartSessionAsync] Both reload and GetSessionByIdAsync failed. SessionId={session.SessionId}");
                            return null;
                        }
                        
                        var response2 = MapToResponse(reloadedSession);
                        Console.WriteLine($"[StartSessionAsync] Session created successfully! SessionId={session.SessionId}");
                        return response2;
                    }
                    catch (Exception getEx)
                    {
                        Console.WriteLine($"⚠️ [StartSessionAsync] Error getting session after creation: {getEx.Message}. Session was created successfully but failed to retrieve.");
                        Console.WriteLine($"⚠️ [StartSessionAsync] Stack trace: {getEx.StackTrace}");
                        
                        // Try to reload session from DB
                        try
                        {
                            _db.Entry(session).State = Microsoft.EntityFrameworkCore.EntityState.Detached;
                            
                            var reloadedSession = await _db.ChargingSessions
                                .AsNoTracking()
                                .Include(s => s.Point)
                                    .ThenInclude(p => p.Station)
                                .Include(s => s.Driver)
                                    .ThenInclude(d => d.User)
                                .Include(s => s.SessionLogs)
                                .FirstOrDefaultAsync(s => s.SessionId == session.SessionId);
                            
                            if (reloadedSession != null)
                            {
                                var response = MapToResponse(reloadedSession);
                                Console.WriteLine($"[StartSessionAsync] Response created from reloaded session after exception. SessionId={session.SessionId}");
                                return response;
                            }
                            else
                            {
                                Console.WriteLine($"⚠️ [StartSessionAsync] Reloaded session is null. SessionId={session.SessionId}");
                            }
                        }
                        catch (Exception reloadEx)
                        {
                            Console.WriteLine($"⚠️ [StartSessionAsync] Error reloading session: {reloadEx.Message}");
                            Console.WriteLine($"⚠️ [StartSessionAsync] Stack trace: {reloadEx.StackTrace}");
                        }
                        
                        return null;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"⚠️ [StartSessionAsync] Exception caught: {ex.Message}");
                    Console.WriteLine($"⚠️ [StartSessionAsync] Exception type: {ex.GetType().Name}");
                    Console.WriteLine($"⚠️ [StartSessionAsync] Stack trace: {ex.StackTrace}");
                    
                    // Rollback nếu transaction chưa commit
                    if (!transactionCommitted)
                    {
                        try
                        {
                            await transaction.RollbackAsync();
                            Console.WriteLine("⚠️ [StartSessionAsync] Transaction rolled back due to error.");
                        }
                        catch (Exception rollbackEx)
                        {
                            Console.WriteLine($"⚠️ [StartSessionAsync] Error rolling back transaction: {rollbackEx.Message}");
                        }
                    }
                    else
                    {
                        Console.WriteLine("⚠️ [StartSessionAsync] Exception occurred after transaction commit. Session may have been created in DB.");
                    }
                    
                    // Log error
                    Console.WriteLine($"Error starting charging session: {ex.Message}");
                    return null;
                }
            });
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
                var now = DateTime.UtcNow;
                
                // Đảm bảo EndTime >= StartTime
                // Nếu StartTime trong tương lai (check-in sớm cho reservation), dùng StartTime làm EndTime
                // Nếu StartTime đã qua, dùng thời gian hiện tại
                session.EndTime = now < session.StartTime ? session.StartTime : now;
                
                session.FinalSoc = request.FinalSOC;
                
                // Tính DurationMinutes và đảm bảo >= 0
                var durationMinutes = (int)(session.EndTime.Value - session.StartTime).TotalMinutes;
                if (durationMinutes < 0)
                {
                    Console.WriteLine($"⚠️ [StopSessionAsync] Negative duration detected! SessionId={session.SessionId}, StartTime={session.StartTime}, EndTime={session.EndTime.Value}");
                    session.EndTime = session.StartTime;
                    durationMinutes = 0;
                }
                
                session.DurationMinutes = durationMinutes;
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
                var session = await _db.ChargingSessions
                    .Include(s => s.Point)
                    .FirstOrDefaultAsync(s => s.SessionId == request.SessionId);
                
                if (session == null)
                    return null;

                var oldStatus = session.Status;
                session.Status = request.Status;

                // Auto-release point khi session kết thúc (completed, cancelled, no_show)
                if ((request.Status == "completed" || request.Status == "cancelled" || request.Status == "no_show") 
                    && oldStatus == "in_progress")
                {
                    var chargingPoint = await _db.ChargingPoints.FindAsync(session.PointId);
                    if (chargingPoint != null && chargingPoint.Status == "in_use")
                    {
                        chargingPoint.Status = "available";
                    }

                    // Set EndTime nếu chưa có
                    // Đảm bảo EndTime >= StartTime (không cho phép EndTime < StartTime)
                    if (!session.EndTime.HasValue)
                    {
                        var now = DateTime.UtcNow;
                        // Nếu StartTime trong tương lai (check-in sớm cho reservation), đợi đến StartTime mới cho phép set EndTime
                        // Nếu StartTime đã qua, dùng thời gian hiện tại
                        session.EndTime = now < session.StartTime ? session.StartTime : now;
                    }
                    
                    // Đảm bảo EndTime >= StartTime và tính lại DurationMinutes
                    if (session.EndTime.HasValue && session.EndTime.Value < session.StartTime)
                    {
                        Console.WriteLine($"⚠️ [UpdateSessionStatusAsync] EndTime < StartTime detected! SessionId={session.SessionId}, StartTime={session.StartTime}, EndTime={session.EndTime.Value}");
                        session.EndTime = session.StartTime; // Set EndTime = StartTime nếu không hợp lệ
                    }
                    
                    // Tính lại DurationMinutes
                    if (session.EndTime.HasValue)
                    {
                        session.DurationMinutes = (int)(session.EndTime.Value - session.StartTime).TotalMinutes;
                        // Đảm bảo duration >= 0
                        if (session.DurationMinutes < 0)
                        {
                            Console.WriteLine($"⚠️ [UpdateSessionStatusAsync] Negative duration detected! SessionId={session.SessionId}, Duration={session.DurationMinutes}");
                            session.DurationMinutes = 0;
                            session.EndTime = session.StartTime;
                        }
                    }
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
                    var now = DateTime.UtcNow;
                    // Đảm bảo timeElapsed >= 0 (nếu StartTime trong tương lai, dùng 0)
                    var timeElapsed = now > session.StartTime ? (now - session.StartTime) : TimeSpan.Zero;
                    var energyUsed = (decimal)((double)(request.CurrentPower ?? 0) * timeElapsed.TotalHours);
                    session.EnergyUsed = energyUsed;
                    // Update DurationMinutes
                    session.DurationMinutes = (int)timeElapsed.TotalMinutes;
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
            var now = DateTime.UtcNow;
            // Đảm bảo timeElapsed >= 0 (nếu StartTime trong tương lai, dùng 0)
            var timeElapsed = now > session.StartTime ? (now - session.StartTime) : TimeSpan.Zero;
            var energyUsed = (decimal)((double)power * timeElapsed.TotalHours);
            session.EnergyUsed = energyUsed;
            
            // Update DurationMinutes
            session.DurationMinutes = (int)timeElapsed.TotalMinutes;

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
        public async Task<bool> ValidateChargingPointAsync(int chargingPointId, DateTime? scheduledStartTime = null)
        {
            var chargingPoint = await _db.ChargingPoints
                .Include(cp => cp.Station)
                .FirstOrDefaultAsync(cp => cp.PointId == chargingPointId);

            if (chargingPoint == null || chargingPoint.Station?.Status != "active")
                return false;

            // Auto-release point nếu bị stuck ở "in_use" nhưng không có session in_progress
            if (chargingPoint.Status == "in_use")
            {
                var hasActiveSession = await _db.ChargingSessions
                    .AnyAsync(s => s.PointId == chargingPointId && s.Status == "in_progress");
                
                if (!hasActiveSession)
                {
                    // Point bị stuck, auto-release
                    chargingPoint.Status = "available";
                    await _db.SaveChangesAsync();
                    Console.WriteLine($"[ValidateChargingPointAsync] Point {chargingPointId} was stuck in 'in_use' but no active session. Auto-released to 'available'.");
                }
                else if (scheduledStartTime.HasValue && scheduledStartTime.Value > DateTime.UtcNow)
                {
                    // Có scheduledStartTime trong tương lai, kiểm tra xem session đang active có kết thúc trước scheduledStartTime không
                    var activeSession = await _db.ChargingSessions
                        .Where(s => s.PointId == chargingPointId && s.Status == "in_progress")
                        .Select(s => s.EndTime)
                        .FirstOrDefaultAsync();
                    
                    if (activeSession.HasValue && activeSession.Value < scheduledStartTime.Value)
                    {
                        // Session sẽ kết thúc trước scheduledStartTime, cho phép check-in sớm
                        Console.WriteLine($"[ValidateChargingPointAsync] Point {chargingPointId} is in_use but active session ends at {activeSession.Value} before scheduled start time {scheduledStartTime.Value}. Allowing early check-in.");
                        return true; // Cho phép check-in sớm
                    }
                    else
                    {
                        // Session sẽ overlap với scheduledStartTime, không cho phép
                        Console.WriteLine($"[ValidateChargingPointAsync] Point {chargingPointId} is in_use and active session will overlap with scheduled start time {scheduledStartTime.Value}.");
                        return false;
                    }
                }
            }

            return chargingPoint.Status == "available";
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
        public async Task<bool> CanStartSessionAsync(int chargingPointId, int driverId, DateTime? scheduledStartTime = null)
        {
            // Check if driver has any active sessions
            var hasActiveSession = await _db.ChargingSessions
                .AnyAsync(s => s.DriverId == driverId && s.Status == "in_progress");

            if (hasActiveSession)
            {
                Console.WriteLine($"[CanStartSessionAsync] Driver {driverId} already has an active session");
                return false;
            }

            // Check if charging point has any active sessions
            // Nếu có scheduledStartTime (check-in sớm cho reservation), kiểm tra xem session đang active có kết thúc trước scheduledStartTime không
            if (scheduledStartTime.HasValue)
            {
                var now = DateTime.UtcNow;
                
                // Nếu scheduledStartTime trong tương lai, kiểm tra session đang active có kết thúc trước scheduledStartTime không
                if (scheduledStartTime.Value > now)
                {
                    // Kiểm tra có session nào đang active trên point này VÀ sẽ overlap với scheduledStartTime không
                    // Nếu session đang active sẽ kết thúc trước scheduledStartTime, cho phép check-in sớm
                    var activeSessionOnPoint = await _db.ChargingSessions
                        .Where(s => s.PointId == chargingPointId && s.Status == "in_progress")
                        .Select(s => new { s.StartTime, s.EndTime })
                        .FirstOrDefaultAsync();
                    
                    if (activeSessionOnPoint != null)
                    {
                        // Nếu session có EndTime (từ reservation hoặc đã được set), kiểm tra xem có kết thúc trước scheduledStartTime không
                        if (activeSessionOnPoint.EndTime.HasValue)
                        {
                            if (activeSessionOnPoint.EndTime.Value >= scheduledStartTime.Value)
                            {
                                Console.WriteLine($"[CanStartSessionAsync] Point {chargingPointId} has active session that ends at {activeSessionOnPoint.EndTime.Value}, which overlaps with scheduled start time {scheduledStartTime.Value}");
                                return false;
                            }
                            else
                            {
                                // Session sẽ kết thúc trước scheduledStartTime, cho phép check-in sớm
                                Console.WriteLine($"[CanStartSessionAsync] Point {chargingPointId} has active session that ends at {activeSessionOnPoint.EndTime.Value}, before scheduled start time {scheduledStartTime.Value}. Allowing early check-in.");
                                // Không return false, tiếp tục kiểm tra point status
                            }
                        }
                        else
                        {
                            // Session không có EndTime (không phải từ reservation), không thể đảm bảo sẽ kết thúc trước scheduledStartTime
                            Console.WriteLine($"[CanStartSessionAsync] Point {chargingPointId} has active session without EndTime. Cannot allow early check-in.");
                            return false;
                        }
                    }
                }
                else
                {
                    // scheduledStartTime đã qua, check bình thường
                    var hasActiveSessionOnPoint = await _db.ChargingSessions
                        .AnyAsync(s => s.PointId == chargingPointId && s.Status == "in_progress");
                    
                    if (hasActiveSessionOnPoint)
                    {
                        Console.WriteLine($"[CanStartSessionAsync] Point {chargingPointId} already has an active session");
                        return false;
                    }
                }
            }
            else
            {
                // Không có scheduledStartTime, check bình thường
                var hasActiveSessionOnPoint = await _db.ChargingSessions
                    .AnyAsync(s => s.PointId == chargingPointId && s.Status == "in_progress");
                
                if (hasActiveSessionOnPoint)
                {
                    Console.WriteLine($"[CanStartSessionAsync] Point {chargingPointId} already has an active session");
                    return false;
                }
            }

            // Check if charging point is available (hoặc có thể được release khi session đang active kết thúc)
            var chargingPoint = await _db.ChargingPoints.FindAsync(chargingPointId);
            
            if (chargingPoint == null)
            {
                Console.WriteLine($"[CanStartSessionAsync] Point {chargingPointId} not found");
                return false;
            }
            
            // Nếu point status = "in_use" nhưng scheduledStartTime trong tương lai, kiểm tra session đang active
            if (chargingPoint.Status == "in_use" && scheduledStartTime.HasValue && scheduledStartTime.Value > DateTime.UtcNow)
            {
                var activeSession = await _db.ChargingSessions
                    .Where(s => s.PointId == chargingPointId && s.Status == "in_progress")
                    .Select(s => s.EndTime)
                    .FirstOrDefaultAsync();
                
                if (activeSession.HasValue)
                {
                    // Có session active với EndTime
                    if (activeSession.Value < scheduledStartTime.Value)
                    {
                        Console.WriteLine($"[CanStartSessionAsync] Point {chargingPointId} is in_use but active session ends at {activeSession.Value} before scheduled start time {scheduledStartTime.Value}. Allowing early check-in.");
                        return true; // Cho phép check-in sớm
                    }
                    else
                    {
                        Console.WriteLine($"[CanStartSessionAsync] Point {chargingPointId} is in_use and active session ends at {activeSession.Value}, which overlaps with scheduled start time {scheduledStartTime.Value}");
                        return false; // Không cho phép vì overlap
                    }
                }
                else
                {
                    // Point status = "in_use" nhưng không có session active (point bị stuck)
                    // Nếu scheduledStartTime trong tương lai, có thể cho phép (point sẽ được auto-release trong ValidateChargingPointAsync)
                    Console.WriteLine($"[CanStartSessionAsync] Point {chargingPointId} is in_use but no active session found. Point may be stuck. Checking with ValidateChargingPointAsync...");
                    // Không return false ngay, tiếp tục check point status hoặc auto-release
                }
            }
            
            // Check point status (sau khi auto-release trong ValidateChargingPointAsync nếu cần)
            var isAvailable = chargingPoint.Status == "available";
            
            if (!isAvailable)
            {
                Console.WriteLine($"[CanStartSessionAsync] Point {chargingPointId} is not available. Status: {chargingPoint.Status}");
            }
            else
            {
                Console.WriteLine($"[CanStartSessionAsync] Point {chargingPointId} is available");
            }
            
            return isAvailable;
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

                // Kiểm tra batteryCapacity > 0 để tránh DivideByZeroException
                if (batteryCapacity <= 0)
                {
                    Console.WriteLine($"⚠️ [CalculateCurrentSOC] BatteryCapacity is 0 or negative for session {session.SessionId}. Cannot calculate SOC.");
                    return null;
                }

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