    using EVCharging.BE.Common.DTOs.Charging;
using EVCharging.BE.Common.DTOs.Stations;
using EVCharging.BE.Common.DTOs.Users;
using EVCharging.BE.DAL;
using EVCharging.BE.DAL.Entities;
using Microsoft.EntityFrameworkCore;
using IncidentReport = EVCharging.BE.DAL.Entities.IncidentReport;

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
            // ✅ Validate ChargingPointId không null
            if (!request.ChargingPointId.HasValue)
            {
                Console.WriteLine($"⚠️ [StartSessionAsync] ChargingPointId is required but not provided.");
                throw new ArgumentException("ChargingPointId is required. Please provide either ChargingPointId or PointQrCode.");
            }

            var chargingPointId = request.ChargingPointId.Value;
            Console.WriteLine($"[StartSessionAsync] Starting session for PointId={chargingPointId}, DriverId={request.DriverId}, StartAtUtc={request.StartAtUtc}");
            
            // Sử dụng EF Core execution strategy để tương thích với retry-on-failure
            var strategy = _db.Database.CreateExecutionStrategy();
            return await strategy.ExecuteAsync(async () =>
            {
                using var transaction = await _db.Database.BeginTransactionAsync();
                ChargingSession? session = null;
                var transactionCommitted = false;
                var transactionRolledBack = false; // ✅ Track xem transaction đã được rollback chưa
                
                try
                {
                    // Re-validate trong transaction context

                    // Re-validate trong transaction context (có thể khác với validation ở controller)

                    // Truyền request.StartAtUtc để cho phép check-in sớm nếu session đang active sẽ kết thúc trước start time
                    if (!await ValidateChargingPointAsync(chargingPointId, request.StartAtUtc))
                    {
                        Console.WriteLine($"⚠️ [StartSessionAsync] Charging point validation failed for PointId={chargingPointId}, StartAtUtc={request.StartAtUtc}");
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
                    if (!await CanStartSessionAsync(chargingPointId, request.DriverId, request.StartAtUtc))
                    {
                        Console.WriteLine($"⚠️ [StartSessionAsync] Cannot start session - PointId={chargingPointId}, DriverId={request.DriverId}, StartAtUtc={request.StartAtUtc}");
                        await transaction.RollbackAsync();
                        return null;
                    }
                    
                    Console.WriteLine($"[StartSessionAsync] All validations passed. Proceeding with session creation...");

                    // Get charging point and driver info
                    var chargingPoint = await _db.ChargingPoints
                        .Include(cp => cp.Station)
                        .FirstOrDefaultAsync(cp => cp.PointId == chargingPointId);

                    var driver = await _db.DriverProfiles
                        .Include(d => d.User)   
                        .FirstOrDefaultAsync(d => d.DriverId == request.DriverId);

                    if (chargingPoint == null || driver == null)
                    {
                        Console.WriteLine($"⚠️ [StartSessionAsync] Point or Driver not found - PointId={chargingPointId}, DriverId={request.DriverId}");
                        await transaction.RollbackAsync();
                        return null;
                    }

                    Console.WriteLine($"[StartSessionAsync] Creating session - Point status: {chargingPoint.Status}, Station status: {chargingPoint.Station?.Status}");

                    // ✅ QUAN TRỌNG: Check upcoming reservation TRƯỚC KHI tạo session object
                    // Đảm bảo không có reservation sắp đến quá gần trước khi tạo session
                    int? reservationId = null;
                    Reservation? reservation = null;
                    DateTime sessionStartTime;
                    DateTime? maxEndTime = null;
                    
                    // Nếu có ReservationCode, tìm reservation và set ReservationId
                    if (!string.IsNullOrEmpty(request.ReservationCode))
                    {
                        reservation = await _db.Reservations
                            .FirstOrDefaultAsync(r => r.ReservationCode == request.ReservationCode && r.DriverId == request.DriverId);
                        if (reservation != null)
                        {
                            // ✅ Kiểm tra status của reservation - không cho tạo session nếu đã bị hủy, hoàn thành, hoặc no-show
                            if (reservation.Status is "cancelled" or "completed" or "no_show")
                            {
                                throw new InvalidOperationException(
                                    $"Cannot start session. This reservation has been {reservation.Status}. " +
                                    "Please create a new reservation or contact support."
                                );
                            }
                            
                            reservationId = reservation.ReservationId;
                            Console.WriteLine($"[StartSessionAsync] Found reservation - ReservationId={reservationId}, ReservationCode={request.ReservationCode}, StartTime={reservation.StartTime}, Status={reservation.Status}");
                        }
                    }
                    
                    // ✅ Xác định StartTime cho session
                    // - Nếu có request.StartAtUtc (từ check-in), dùng nó (đã được tính = reservation.StartTime khi check-in sớm)
                    // - Nếu không có request.StartAtUtc nhưng có reservation, dùng reservation.StartTime
                    // - Nếu không có cả hai, dùng DateTime.UtcNow
                    if (request.StartAtUtc.HasValue)
                    {
                        sessionStartTime = request.StartAtUtc.Value;
                        Console.WriteLine($"[StartSessionAsync] Using request.StartAtUtc = {sessionStartTime}");
                    }
                    else if (reservation != null)
                    {
                        // ✅ Đảm bảo: Nếu có reservation nhưng không có StartAtUtc, dùng reservation.StartTime
                        sessionStartTime = reservation.StartTime;
                        Console.WriteLine($"[StartSessionAsync] Using reservation.StartTime = {sessionStartTime} (request.StartAtUtc is null)");
                    }
                    else
                    {
                        sessionStartTime = DateTime.UtcNow;
                        Console.WriteLine($"[StartSessionAsync] Using DateTime.UtcNow = {sessionStartTime} (no reservation)");
                    }
                    
                    Console.WriteLine($"[StartSessionAsync] Final session StartTime = {sessionStartTime}, ReservationId = {reservationId}");
                    
                    // ✅ Check upcoming reservation TRƯỚC KHI tạo session object (để tránh tạo session rồi mới throw exception)
                    if (request.MaxEndTimeUtc.HasValue)
                    {
                        maxEndTime = request.MaxEndTimeUtc.Value;
                        Console.WriteLine($"[StartSessionAsync] MaxEndTime set from request: {maxEndTime}");
                    }
                    else if (reservationId == null)
                    {
                        // Walk-in session: Đơn giản hóa logic - CHỈ block nếu thực sự cần thiết
                        // Note: CanStartSessionAsync đã kiểm tra point availability (có session đang chạy không)
                        var now = DateTime.UtcNow;
                        const int NO_SHOW_GRACE_MINUTES = 30; // Grace period 30 phút sau start_time
                        var gracePeriodStart = now.AddMinutes(-NO_SHOW_GRACE_MINUTES);
                        
                        // ✅ CHỈ block walk-in nếu có reservation status="booked" (chưa check-in) đang trong grace period
                        // Logic: Nếu reservation đã check-in (status="checked_in" hoặc "in_progress"), point đã được sử dụng
                        // và CanStartSessionAsync đã kiểm tra point availability rồi, không cần block ở đây nữa
                        var activeReservation = await _db.Reservations
                            .Where(r => r.PointId == chargingPointId 
                                && r.Status == "booked" // CHỈ check reservation chưa check-in
                                && r.StartTime <= now // Đã quá start_time
                                && r.StartTime >= gracePeriodStart // Vẫn trong grace period (30 phút)
                                && r.EndTime > now) // Reservation chưa kết thúc
                            .OrderBy(r => r.StartTime)
                            .FirstOrDefaultAsync();
                        
                        // ✅ BLOCK walk-in nếu có reservation đang trong grace period (chưa check-in)
                        if (activeReservation != null)
                        {
                            var minutesSinceStart = (now - activeReservation.StartTime).TotalMinutes;
                            Console.WriteLine($"⚠️ [StartSessionAsync] BLOCKING: Reservation (ReservationId={activeReservation.ReservationId}, StartTime={activeReservation.StartTime:HH:mm}, {minutesSinceStart:F0} minutes ago) is still within grace period. Rolling back transaction.");
                            try
                            {
                                await transaction.RollbackAsync();
                                transactionRolledBack = true;
                                Console.WriteLine("⚠️ [StartSessionAsync] Transaction rolled back successfully.");
                            }
                            catch (Exception rollbackEx)
                            {
                                Console.WriteLine($"⚠️ [StartSessionAsync] Error rolling back transaction: {rollbackEx.Message}");
                            }
                            throw new InvalidOperationException($"Cannot start walk-in session. There is a reservation (started at {activeReservation.StartTime:HH:mm}, {minutesSinceStart:F0} minutes ago) that is still within the grace period. The reservation holder may check in at any time.");
                        }
                        
                        // ✅ Check reservation sắp đến - CHỈ block nếu reservation sắp đến trong 15 phút tới
                        // CHỈ block reservations status="booked" (chưa check-in) vì reservations đã check-in
                        // sẽ được xử lý bởi CanStartSessionAsync (kiểm tra point availability)
                        const int MINIMUM_TIME_BEFORE_RESERVATION_MINUTES = 15; // Tối thiểu 15 phút trước reservation
                        var timeWindowEnd = now.AddMinutes(MINIMUM_TIME_BEFORE_RESERVATION_MINUTES);
                        
                        var upcomingReservation = await _db.Reservations
                            .Where(r => r.PointId == chargingPointId 
                                && r.Status == "booked" // CHỈ check reservations chưa check-in (relax logic)
                                && r.StartTime > now // Trong tương lai
                                && r.StartTime <= timeWindowEnd) // CHỈ check reservations trong 15 phút tới
                            .OrderBy(r => r.StartTime)
                            .FirstOrDefaultAsync();
                        
                        if (upcomingReservation != null)
                        {
                            var timeUntilReservation = (upcomingReservation.StartTime - now).TotalMinutes;
                            
                            // ✅ BLOCK walk-in nếu reservation sắp đến quá gần (dưới 15 phút)
                            Console.WriteLine($"⚠️ [StartSessionAsync] BLOCKING: Reservation (Status=booked) starting at {upcomingReservation.StartTime:HH:mm} (in {timeUntilReservation:F0} minutes) is too close. Rolling back transaction.");
                            try
                            {
                                await transaction.RollbackAsync();
                                transactionRolledBack = true;
                                Console.WriteLine("⚠️ [StartSessionAsync] Transaction rolled back successfully.");
                            }
                            catch (Exception rollbackEx)
                            {
                                Console.WriteLine($"⚠️ [StartSessionAsync] Error rolling back transaction: {rollbackEx.Message}");
                            }
                            throw new InvalidOperationException($"Cannot start walk-in session. There is a reservation starting at {upcomingReservation.StartTime:HH:mm} (in {timeUntilReservation:F0} minutes). Please wait or use a different charging point.");
                        }
                        
                        Console.WriteLine($"[StartSessionAsync] No upcoming reservations found in the next 15 minutes. Walk-in session can proceed.");
                        
                        // ✅ Nếu có reservation xa hơn (> 15 phút), set maxEndTime để tự động dừng trước khi reservation bắt đầu
                        // CHỈ check reservations status="booked" vì reservations đã check-in sẽ được xử lý bởi CanStartSessionAsync
                        var futureReservation = await _db.Reservations
                            .Where(r => r.PointId == chargingPointId 
                                && r.Status == "booked" // CHỈ check reservations chưa check-in
                                && r.StartTime > timeWindowEnd) // Xa hơn 15 phút
                            .OrderBy(r => r.StartTime)
                            .FirstOrDefaultAsync();
                        
                        if (futureReservation != null)
                        {
                            var bufferMinutes = 5;
                            maxEndTime = futureReservation.StartTime.AddMinutes(-bufferMinutes);
                            var timeUntilReservation = (futureReservation.StartTime - now).TotalMinutes;
                            Console.WriteLine($"[StartSessionAsync] Future reservation found (Status=booked) - ReservationId={futureReservation.ReservationId}, StartTime={futureReservation.StartTime}, MaxEndTime={maxEndTime}, TimeUntilReservation={timeUntilReservation:F0} minutes");
                        }
                        else
                        {
                            Console.WriteLine($"[StartSessionAsync] No blocking reservations found for walk-in session. Point is available.");
                        }
                    }

                    // ✅ Create new charging session
                    // ✅ LOGIC FINALSOC VÀ INITIALSOC:
                    // - InitialSOC: Pin hiện tại khi bắt đầu sạc (từ request.InitialSOC), sẽ cập nhật theo pin thực tế khi sạc lên 1-2%
                    // - FinalSOC: Pin cuối cùng, mặc định 100%, nếu có targetSOC từ reservation thì FinalSOC = targetSOC
                    //   + Nếu reservation.TargetSoc = 100 → FinalSOC = 100 → Session sẽ tự động dừng khi đạt 100%
                    //   + Nếu reservation.TargetSoc = 80 → FinalSOC = 80 → Session sẽ tự động dừng khi đạt 80%
                    //   + Nếu không có reservation (walk-in) → FinalSOC = 100 → Session sẽ tự động dừng khi đạt 100%
                    // - Khi currentSOC >= FinalSOC (targetSOC), session sẽ tự động dừng
                    // - Khi dừng (auto-stop hoặc thủ công), FinalSOC sẽ được set = targetSOC từ reservation hoặc 100%
                    int? targetSocFromReservation = reservation?.TargetSoc;
                    
                    // ✅ Xác định FinalSOC: Nếu có targetSOC từ reservation thì dùng nó, nếu không thì dùng 100%
                    int finalSOC = targetSocFromReservation ?? 100; // Mặc định 100% nếu không có reservation
                    
                    // ✅ VALIDATION: Kiểm tra targetSOC không được nhỏ hơn initialSOC
                    // Logic: SOC không thể giảm, nên targetSOC phải >= initialSOC
                    if (finalSOC < request.InitialSOC)
                    {
                        Console.WriteLine($"⚠️ [StartSessionAsync] FinalSOC ({finalSOC}%) < InitialSOC ({request.InitialSOC}%). Rolling back transaction.");
                        await transaction.RollbackAsync();
                        throw new ArgumentException($"Target SOC ({finalSOC}%) cannot be less than Initial SOC ({request.InitialSOC}%). Please check your reservation settings or initial SOC value.");
                    }
                    
                    session = new ChargingSession
                    {
                        DriverId = request.DriverId,
                        PointId = chargingPointId, // ✅ Đã validate không null ở đầu method
                        ReservationId = reservationId,
                        StartTime = sessionStartTime, // ✅ Đảm bảo StartTime = reservation.StartTime khi check-in sớm
                        InitialSoc = request.InitialSOC, // Pin hiện tại khi bắt đầu sạc (sẽ cập nhật theo pin thực tế khi sạc lên 1-2%)
                        FinalSoc = finalSOC, // Pin cuối cùng: targetSOC từ reservation hoặc 100% mặc định
                        Status = "in_progress",
                        EnergyUsed = 0,
                        DurationMinutes = 0,
                        CostBeforeDiscount = 0,
                        AppliedDiscount = 0,
                        FinalCost = 0
                    };
                    
                    if (targetSocFromReservation.HasValue)
                    {
                        Console.WriteLine($"[StartSessionAsync] Session {session.SessionId} - InitialSOC: {request.InitialSOC}%, FinalSOC (Target): {finalSOC}% (từ reservation). Session sẽ tự động dừng khi currentSOC >= {finalSOC}%.");
                    }
                    else
                    {
                        Console.WriteLine($"[StartSessionAsync] Session {session.SessionId} - InitialSOC: {request.InitialSOC}%, FinalSOC (Target): {finalSOC}% (walk-in, mặc định 100%). Session sẽ tự động dừng khi currentSOC >= {finalSOC}%.");
                    }

                    // ✅ Lưu maxEndTime vào Notes nếu có (vì entity không có field MaxEndTime)
                    if (maxEndTime.HasValue)
                    {
                        var maxEndTimeInfo = new
                        {
                            maxEndTime = maxEndTime.Value,
                            reason = "upcoming_reservation",
                            autoStop = true
                        };
                        session.Notes = System.Text.Json.JsonSerializer.Serialize(maxEndTimeInfo);
                        Console.WriteLine($"[StartSessionAsync] MaxEndTime saved in Notes: {maxEndTime}");
                    }

                    _db.ChargingSessions.Add(session);
                    
                    // Update charging point status
                    chargingPoint.Status = "in_use";
                    
                    // ✅ Update reservation status: "checked_in" → "in_progress" khi session StartTime đến
                    if (reservation != null)
                    {
                        var now = DateTime.UtcNow;
                        // Nếu session StartTime đã đến hoặc đã qua, chuyển reservation status sang "in_progress"
                        if (sessionStartTime <= now)
                        {
                            if (reservation.Status == "checked_in")
                            {
                                reservation.Status = "in_progress";
                                reservation.UpdatedAt = now;
                                Console.WriteLine($"[StartSessionAsync] Updated reservation status from 'checked_in' to 'in_progress' - ReservationId={reservationId}, StartTime={sessionStartTime}, Now={now}");
                            }
                        }
                        // Nếu session StartTime trong tương lai (check-in sớm), giữ status = "checked_in"
                        else
                        {
                            Console.WriteLine($"[StartSessionAsync] Reservation status kept as 'checked_in' (session starts in future) - ReservationId={reservationId}, StartTime={sessionStartTime}, Now={now}");
                        }
                    }
                    
                    Console.WriteLine($"[StartSessionAsync] Saving session to database...");
                    await _db.SaveChangesAsync();
                    Console.WriteLine($"[StartSessionAsync] Session saved with SessionId={session.SessionId}");

                    // Tạo initial log khi session bắt đầu
                    var initialLog = new SessionLog
                    {
                        SessionId = session.SessionId,
                        SocPercentage = request.InitialSOC,
                        CurrentPower = chargingPoint.PowerOutput ?? 0, // Dùng PowerOutput ban đầu
                        Voltage = 400, // Mặc định 400V
                        Temperature = 25, // Mặc định 25°C
                        LogTime = session.StartTime
                    };
                    _db.SessionLogs.Add(initialLog);
                    await _db.SaveChangesAsync();
                    Console.WriteLine($"[StartSessionAsync] Initial log created for SessionId={session.SessionId}");

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
                    // ✅ Sử dụng GetSessionByIdAsync thay vì reload trực tiếp từ _db để tránh conflict với monitoring service
                    try
                    {
                        Console.WriteLine($"[StartSessionAsync] Retrieving session data for response... SessionId={session.SessionId}");
                        
                        // Detach session entity hiện tại để tránh tracking issue
                        _db.Entry(session).State = Microsoft.EntityFrameworkCore.EntityState.Detached;
                        
                        // Sử dụng GetSessionByIdAsync để reload session từ DB (sẽ tạo query mới)
                        var response = await GetSessionByIdAsync(session.SessionId);
                        if (response != null)
                        {
                            Console.WriteLine($"[StartSessionAsync] Session created successfully! SessionId={session.SessionId}");
                            return response;
                        }
                        
                        Console.WriteLine($"⚠️ [StartSessionAsync] GetSessionByIdAsync returned null. SessionId={session.SessionId}");
                        return null;
                    }
                    catch (Exception getEx)
                    {
                        Console.WriteLine($"⚠️ [StartSessionAsync] Error getting session after creation: {getEx.Message}. Session was created successfully but failed to retrieve.");
                        Console.WriteLine($"⚠️ [StartSessionAsync] Stack trace: {getEx.StackTrace}");
                        
                        // Thử lại một lần nữa với GetSessionByIdAsync sau khi đợi một chút
                        try
                        {
                            await Task.Delay(100); // Đợi 100ms để đảm bảo monitoring đã chạy xong
                            var response = await GetSessionByIdAsync(session.SessionId);
                            if (response != null)
                            {
                                Console.WriteLine($"[StartSessionAsync] GetSessionByIdAsync succeeded on retry. SessionId={session.SessionId}");
                                return response;
                            }
                        }
                        catch (Exception retryEx)
                        {
                            Console.WriteLine($"⚠️ [StartSessionAsync] Error retrying GetSessionByIdAsync: {retryEx.Message}");
                        }
                        
                        return null;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"⚠️ [StartSessionAsync] Exception caught: {ex.Message}");
                    Console.WriteLine($"⚠️ [StartSessionAsync] Exception type: {ex.GetType().Name}");
                    Console.WriteLine($"⚠️ [StartSessionAsync] Stack trace: {ex.StackTrace}");
                    
                    // Rollback nếu transaction chưa commit VÀ chưa được rollback
                    if (!transactionCommitted && !transactionRolledBack)
                    {
                        try
                        {
                            await transaction.RollbackAsync();
                            transactionRolledBack = true; // ✅ Đánh dấu transaction đã được rollback
                            Console.WriteLine("⚠️ [StartSessionAsync] Transaction rolled back due to error.");
                        }
                        catch (Exception rollbackEx)
                        {
                            Console.WriteLine($"⚠️ [StartSessionAsync] Error rolling back transaction: {rollbackEx.Message}");
                        }
                    }
                    else if (transactionRolledBack)
                    {
                        Console.WriteLine("⚠️ [StartSessionAsync] Transaction was already rolled back. Skipping duplicate rollback.");
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
                    .Include(s => s.Reservation) // ✅ Include reservation để update status
                    .FirstOrDefaultAsync(s => s.SessionId == request.SessionId);

                if (session == null || session.Status != "in_progress")
                    return null;

                // Calculate final values
                var now = DateTime.UtcNow;
                
                // ✅ Nếu session có maxEndTime trong Notes (walk-in session được auto-stop), dùng maxEndTime làm EndTime
                DateTime? endTimeToUse = null;
                if (!string.IsNullOrEmpty(session.Notes))
                {
                    try
                    {
                        if (System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(session.Notes) is { } notesJson
                            && notesJson.TryGetProperty("maxEndTime", out var maxEndTimeElement))
                        {
                            DateTime? maxEndTime = null;
                            
                            // Try parse as DateTime (if serialized as DateTime)
                            if (maxEndTimeElement.TryGetDateTime(out var parsedDateTime))
                            {
                                maxEndTime = parsedDateTime;
                            }
                            // Try parse as string (if serialized as string)
                            else if (maxEndTimeElement.ValueKind == System.Text.Json.JsonValueKind.String)
                            {
                                var dateTimeString = maxEndTimeElement.GetString();
                                if (!string.IsNullOrEmpty(dateTimeString) && DateTime.TryParse(dateTimeString, null, System.Globalization.DateTimeStyles.RoundtripKind, out var parsedFromString))
                                {
                                    maxEndTime = parsedFromString;
                                }
                            }
                            
                            if (maxEndTime.HasValue && maxEndTime.Value <= now)
                            {
                                // ✅ Dùng maxEndTime làm EndTime nếu đã đến (session được auto-stop)
                                endTimeToUse = maxEndTime.Value;
                                Console.WriteLine($"[StopSessionAsync] Using maxEndTime {maxEndTime.Value:O} as EndTime for auto-stopped walk-in session {session.SessionId}");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"⚠️ [StopSessionAsync] Error parsing Notes for maxEndTime: {ex.Message}");
                    }
                }
                
                // Nếu không có maxEndTime hoặc parse failed, dùng logic thông thường
                if (!endTimeToUse.HasValue)
                {
                    // Đảm bảo EndTime >= StartTime
                    // Nếu StartTime trong tương lai (check-in sớm cho reservation), dùng StartTime làm EndTime
                    // Nếu StartTime đã qua, dùng thời gian hiện tại
                    endTimeToUse = now < session.StartTime ? session.StartTime : now;
                }
                
                // Đảm bảo EndTime >= StartTime (safety check)
                session.EndTime = endTimeToUse.Value < session.StartTime ? session.StartTime : endTimeToUse.Value;
                
                // ✅ QUAN TRỌNG: Set FinalSOC từ request
                // - Nếu auto-stop: FinalSOC = targetSOC từ reservation (đã được set trong CheckAndAutoStopSessionAsync)
                // - Nếu stop thủ công: FinalSOC = request.FinalSOC từ client
                // - Đảm bảo FinalSOC >= InitialSOC (SOC không thể giảm)
                int finalSOC = request.FinalSOC;
                if (finalSOC < session.InitialSoc)
                {
                    Console.WriteLine($"⚠️ [StopSessionAsync] FinalSOC ({finalSOC}%) < InitialSOC ({session.InitialSoc}%). Điều chỉnh thành InitialSOC.");
                    finalSOC = session.InitialSoc;
                }
                
                // Đảm bảo FinalSOC không vượt quá 100%
                finalSOC = Math.Min(finalSOC, 100);
                
                session.FinalSoc = finalSOC;
                
                Console.WriteLine($"[StopSessionAsync] Session {session.SessionId} - InitialSOC: {session.InitialSoc}%, FinalSOC: {finalSOC}%, EnergyUsed: {session.EnergyUsed ?? 0}kWh");
                
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

                // ✅ Update reservation status to "completed" if session has reservation
                if (session.ReservationId.HasValue && session.Reservation != null)
                {
                    if (session.Reservation.Status == "checked_in" || session.Reservation.Status == "in_progress")
                    {
                        session.Reservation.Status = "completed";
                        session.Reservation.UpdatedAt = now;
                        Console.WriteLine($"[StopSessionAsync] Updated reservation status to 'completed' - ReservationId={session.ReservationId}, SessionId={session.SessionId}");
                    }
                }

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
                
                // Lấy deposit amount từ reservation (nếu có)
                decimal depositAmount = 0m;
                if (session.ReservationId.HasValue)
                {
                    var depositPayment = await _db.Payments
                        .Where(p => p.ReservationId == session.ReservationId.Value
                            && p.PaymentType == "deposit"
                            && p.PaymentStatus == "success")
                        .OrderByDescending(p => p.CreatedAt)
                        .FirstOrDefaultAsync();
                    
                    if (depositPayment != null)
                    {
                        depositAmount = depositPayment.Amount;
                    }
                }
                
                // Trừ deposit amount vào final_cost (final_cost = cost_after_discount - deposit)
                var finalCostAfterDiscount = costResponse.FinalCost;
                session.DepositAmount = depositAmount;
                session.FinalCost = Math.Max(0, finalCostAfterDiscount - depositAmount);
                
                Console.WriteLine($"[StopSessionAsync] Session {session.SessionId} - CostAfterDiscount: {finalCostAfterDiscount}, DepositAmount: {depositAmount}, FinalCost: {session.FinalCost}");

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

                // ✅ Update charging point status to "available" để slot tiếp theo có thể check-in
                var chargingPoint = await _db.ChargingPoints.FindAsync(session.PointId);
                if (chargingPoint != null)
                {
                    chargingPoint.Status = "available";
                    Console.WriteLine($"[StopSessionAsync] Updated ChargingPoint {chargingPoint.PointId} status to 'available' after stopping session {session.SessionId}");
                }
                else
                {
                    Console.WriteLine($"⚠️ [StopSessionAsync] ChargingPoint {session.PointId} not found for session {session.SessionId}");
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
        /// Lấy lịch sử phiên sạc của tài xế (tự động lấy từ userId, có filter và pagination)
        /// </summary>
        public async Task<IEnumerable<ChargingSessionResponse>> GetSessionHistoryAsync(int userId, string? status = null, DateTime? fromDate = null, DateTime? toDate = null, int? stationId = null, string? stationName = null, string? stationAddress = null, int? pointId = null, int page = 1, int pageSize = 20)
        {
            // Lấy driverId từ userId
            var driverId = await _db.DriverProfiles
                .Where(d => d.UserId == userId)
                .Select(d => d.DriverId)
                .FirstOrDefaultAsync();

            if (driverId == 0)
                return Enumerable.Empty<ChargingSessionResponse>();

            // Bắt đầu query với driverId
            var q = _db.ChargingSessions
                .Include(s => s.Point)
                    .ThenInclude(p => p.Station)
                .Include(s => s.Driver)
                    .ThenInclude(d => d.User)
                .Include(s => s.Reservation)
                .Where(s => s.DriverId == driverId)
                .AsQueryable();

            // Filter theo trạng thái (status)
            if (!string.IsNullOrWhiteSpace(status))
            {
                q = q.Where(s => s.Status == status);
            }

            // Filter theo khoảng thời gian (fromDate)
            if (fromDate.HasValue)
            {
                var fromUtc = DateTime.SpecifyKind(fromDate.Value, DateTimeKind.Utc);
                q = q.Where(s => s.StartTime >= fromUtc);
            }

            // Filter theo khoảng thời gian (toDate)
            if (toDate.HasValue)
            {
                var toUtc = DateTime.SpecifyKind(toDate.Value, DateTimeKind.Utc);
                q = q.Where(s => s.StartTime <= toUtc);
            }

            // Filter theo trạm sạc (stationId) - ưu tiên stationId nếu có
            if (stationId.HasValue)
            {
                q = q.Where(s => s.Point.StationId == stationId.Value);
            }
            else
            {
                // Ưu tiên filter theo tên trạm (dễ nhớ, ngắn gọn)
                if (!string.IsNullOrWhiteSpace(stationName))
                {
                    q = q.Where(s => s.Point.Station != null && 
                        EF.Functions.Like(s.Point.Station.Name, $"%{stationName}%"));
                }
                // Địa chỉ chỉ là bổ sung (optional), không bắt buộc
                // Nếu có cả tên và địa chỉ, sẽ tìm trạm thỏa mãn cả hai
                if (!string.IsNullOrWhiteSpace(stationAddress))
                {
                    q = q.Where(s => s.Point.Station != null && 
                        EF.Functions.Like(s.Point.Station.Address, $"%{stationAddress}%"));
                }
            }

            // Filter theo điểm sạc (pointId)
            if (pointId.HasValue)
            {
                q = q.Where(s => s.PointId == pointId.Value);
            }

            // Phân trang (pagination)
            if (page < 1) page = 1;
            if (pageSize <= 0 || pageSize > 200) pageSize = 20;

            var skip = (page - 1) * pageSize;

            // Sắp xếp theo StartTime descending (mới nhất trước)
            q = q.OrderByDescending(s => s.StartTime)
                 .Skip(skip)
                 .Take(pageSize);

            var list = await q.ToListAsync();
            return list.Select(MapToResponse);
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
                    ReservationId = session.ReservationId,
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
        /// Tính currentSOC dựa trên năng lượng đã sạc hoặc giá trị lưu trữ
        /// </summary>
        private int? CalculateCurrentSOC(ChargingSession session)
        {
            if (session.CurrentSoc.HasValue)
            {
                return session.CurrentSoc.Value;
            }

            if (session.Driver?.BatteryCapacity.HasValue == true &&
                session.Driver.BatteryCapacity.Value > 0 &&
                session.EnergyUsed.HasValue)
            {
                var batteryCapacity = session.Driver.BatteryCapacity.Value;
                var energyUsed = Math.Max(session.EnergyUsed.Value, 0m);
                var socIncrease = (int)((energyUsed / batteryCapacity) * 100);
                var currentSOC = session.InitialSoc + socIncrease;
                currentSOC = Math.Min(currentSOC, 100);
                return Math.Max(currentSOC, session.InitialSoc);
            }

            return session.InitialSoc;
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

        // ========== INCIDENT REPORTS (DRIVER) ==========

        /// <summary>
        /// Tạo báo cáo sự cố cho driver
        /// </summary>
        public async Task<EVCharging.BE.Common.DTOs.Staff.IncidentReportResponse?> CreateIncidentReportAsync(int userId, EVCharging.BE.Common.DTOs.Staff.CreateIncidentReportRequest request)
        {
            try
            {
                // 1. Lấy driverId từ userId
                var driverProfile = await _db.DriverProfiles
                    .FirstOrDefaultAsync(d => d.UserId == userId);

                if (driverProfile == null)
                {
                    Console.WriteLine($"Driver profile not found for userId {userId}");
                    return null;
                }

                // 2. Verify charging point exists
                var chargingPoint = await _db.ChargingPoints
                    .Include(cp => cp.Station)
                    .FirstOrDefaultAsync(cp => cp.PointId == request.PointId);

                if (chargingPoint == null)
                {
                    Console.WriteLine($"Charging point {request.PointId} not found");
                    return null;
                }

                // 3. Get driver user info
                var driverUser = await _db.Users.FindAsync(userId);
                if (driverUser == null)
                {
                    Console.WriteLine($"Driver user {userId} not found");
                    return null;
                }

                // 4. Create incident report
                var incident = new IncidentReport
                {
                    ReporterId = userId, // Driver's userId
                    PointId = request.PointId,
                    Title = request.Title,
                    Description = request.Description,
                    Priority = request.Priority,
                    Status = "open",
                    ReportedAt = DateTime.UtcNow
                };

                _db.IncidentReports.Add(incident);
                await _db.SaveChangesAsync();

                // 5. Mark charging point as maintenance if required (driver can suggest, but staff/admin decides)
                // Note: Driver reports don't automatically put point in maintenance, that's for staff/admin to decide
                if (request.RequiresMaintenance)
                {
                    // Just log the suggestion, don't change status automatically
                    Console.WriteLine($"[DRIVER REPORT] Maintenance suggested for Point {request.PointId} by Driver {userId}");
                }

                // 6. TODO: Notify admin/staff if required (implement with SignalR or notification service)
                if (request.NotifyAdmin)
                {
                    // await _notificationService.NotifyAdminIncidentAsync(incident.ReportId, request.Title);
                    Console.WriteLine($"[ADMIN ALERT] New incident report from driver: {incident.ReportId} - {request.Title} by User {userId}");
                }

                // 7. Return response
                return new EVCharging.BE.Common.DTOs.Staff.IncidentReportResponse
                {
                    ReportId = incident.ReportId,
                    ReporterId = incident.ReporterId,
                    ReporterName = driverUser.Name ?? "Unknown",
                    ReporterRole = driverUser.Role ?? "Driver",
                    PointId = incident.PointId,
                    StationId = chargingPoint.StationId,
                    StationName = chargingPoint.Station?.Name ?? "Unknown",
                    Title = incident.Title,
                    Description = incident.Description,
                    Priority = incident.Priority,
                    Status = incident.Status,
                    ReportedAt = incident.ReportedAt,
                    AdminNotes = incident.AdminNotes
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error creating incident report for driver: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Lấy danh sách báo cáo sự cố của driver (tự động lấy từ userId)
        /// </summary>
        public async Task<IEnumerable<EVCharging.BE.Common.DTOs.Staff.IncidentReportResponse>> GetDriverIncidentReportsAsync(int userId, string status = "all", string priority = "all", int page = 1, int pageSize = 20)
        {
            try
            {
                // Validate pagination
                if (page < 1) page = 1;
                if (pageSize < 1 || pageSize > 100) pageSize = 20;

                // Build query
                var query = _db.IncidentReports
                    .Include(ir => ir.Point)
                        .ThenInclude(p => p.Station)
                    .Include(ir => ir.Reporter)
                    .Include(ir => ir.ResolvedByNavigation)
                    .Where(ir => ir.ReporterId == userId); // Only driver's own reports

                // Apply status filter
                if (!string.IsNullOrEmpty(status) && status.ToLower() != "all")
                {
                    query = query.Where(ir => ir.Status != null && ir.Status.ToLower() == status.ToLower());
                }

                // Apply priority filter
                if (!string.IsNullOrEmpty(priority) && priority.ToLower() != "all")
                {
                    query = query.Where(ir => ir.Priority != null && ir.Priority.ToLower() == priority.ToLower());
                }

                // Order by most recent first
                query = query.OrderByDescending(ir => ir.ReportedAt ?? DateTime.MinValue);

                // Apply pagination
                var reports = await query
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                // Map to response
                return reports.Select(ir => new EVCharging.BE.Common.DTOs.Staff.IncidentReportResponse
                {
                    ReportId = ir.ReportId,
                    ReporterId = ir.ReporterId,
                    ReporterName = ir.Reporter?.Name ?? "Unknown",
                    ReporterRole = ir.Reporter?.Role ?? "Unknown",
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
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting driver incident reports: {ex.Message}");
                return Enumerable.Empty<EVCharging.BE.Common.DTOs.Staff.IncidentReportResponse>();
            }
        }
    }
}