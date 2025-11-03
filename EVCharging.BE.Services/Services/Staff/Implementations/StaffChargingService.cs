using EVCharging.BE.Common.DTOs.Charging;
using EVCharging.BE.Common.DTOs.Payments;
using EVCharging.BE.Common.DTOs.Staff;
using EVCharging.BE.DAL;
using EVCharging.BE.DAL.Entities;
using EVCharging.BE.Services.Services.Charging;
using EVCharging.BE.Services.Services.Payment;
using Microsoft.EntityFrameworkCore;

namespace EVCharging.BE.Services.Services.Staff.Implementations
{
    /// <summary>
    /// Service quản lý phiên sạc từ góc độ Staff (Nhân viên trạm sạc)
    /// </summary>
    public class StaffChargingService : IStaffChargingService
    {
        private readonly EvchargingManagementContext _db;
        private readonly IChargingService _chargingService;
        private readonly ISessionMonitorService _sessionMonitorService;
        private readonly ICostCalculationService _costCalculationService;
        private readonly IPaymentService _paymentService;

        public StaffChargingService(
            EvchargingManagementContext db,
            IChargingService chargingService,
            ISessionMonitorService sessionMonitorService,
            ICostCalculationService costCalculationService,
            IPaymentService paymentService)
        {
            _db = db;
            _chargingService = chargingService;
            _sessionMonitorService = sessionMonitorService;
            _costCalculationService = costCalculationService;
            _paymentService = paymentService;
        }

        // ========== STATION ASSIGNMENT & VERIFICATION ==========

        /// <summary>
        /// Verify staff assignment
        /// Note: staffId here is actually userId (User.UserId) of a user with role="Staff"
        /// StationStaff.StaffId is a foreign key to User.UserId
        /// </summary>
        public async Task<bool> VerifyStaffAssignmentAsync(int staffId, int stationId)
        {
            try
            {
                // First verify user is actually a staff
                var user = await _db.Users.FindAsync(staffId);
                if (user == null || !user.Role.Equals("Staff", StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                var now = DateTime.UtcNow;
                var assignment = await _db.StationStaffs
                    .FirstOrDefaultAsync(ss =>
                        ss.StaffId == staffId &&  // StaffId = UserId (foreign key)
                        ss.StationId == stationId &&
                        ss.Status == "active" &&
                        ss.ShiftStart <= now &&
                        ss.ShiftEnd >= now);

                return assignment != null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error verifying staff assignment: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Get assigned stations for a staff
        /// Note: staffId is actually userId (User.UserId) of a user with role="Staff"
        /// </summary>
        public async Task<List<int>> GetAssignedStationsAsync(int staffId)
        {
            try
            {
                // Verify user is actually a staff
                var user = await _db.Users.FindAsync(staffId);
                if (user == null || !user.Role.Equals("Staff", StringComparison.OrdinalIgnoreCase))
                {
                    return new List<int>();
                }

                var now = DateTime.UtcNow;
                var assignments = await _db.StationStaffs
                    .Where(ss =>
                        ss.StaffId == staffId &&  // StaffId = UserId (foreign key)
                        ss.Status == "active" &&
                        ss.ShiftStart <= now &&
                        ss.ShiftEnd >= now)
                    .Select(ss => ss.StationId)
                    .ToListAsync();

                return assignments;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting assigned stations: {ex.Message}");
                return new List<int>();
            }
        }

        /// <summary>
        /// Get station info for staff
        /// Note: staffId is actually userId (User.UserId) of a user with role="Staff"
        /// </summary>
        public async Task<StationInfo?> GetMyStationInfoAsync(int staffId)
        {
            try
            {
                var stationIds = await GetAssignedStationsAsync(staffId);
                if (!stationIds.Any())
                    return null;

                // Lấy trạm đầu tiên (trong thực tế staff thường chỉ assigned 1 trạm)
                var stationId = stationIds.First();
                var station = await _db.ChargingStations
                    .Include(s => s.ChargingPoints)
                    .FirstOrDefaultAsync(s => s.StationId == stationId);

                if (station == null)
                    return null;

                var totalPoints = station.ChargingPoints.Count;
                var availablePoints = station.ChargingPoints.Count(cp => cp.Status == "available");
                var inUsePoints = station.ChargingPoints.Count(cp => cp.Status == "in_use");
                var maintenancePoints = station.ChargingPoints.Count(cp => cp.Status == "maintenance");
                var offlinePoints = station.ChargingPoints.Count(cp => cp.Status == "offline");

                return new StationInfo
                {
                    StationId = station.StationId,
                    StationName = station.Name,
                    Address = station.Address ?? "",
                    TotalPoints = totalPoints,
                    AvailablePoints = availablePoints,
                    InUsePoints = inUsePoints,
                    MaintenancePoints = maintenancePoints,
                    OfflinePoints = offlinePoints
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting station info: {ex.Message}");
                return null;
            }
        }

        // ========== WALK-IN CUSTOMER SESSION MANAGEMENT ==========

        public async Task<WalkInSessionResponse?> StartWalkInSessionAsync(int staffId, WalkInSessionRequest request)
        {
            try
            {
                // 1. Verify charging point
                var chargingPoint = await _db.ChargingPoints
                    .Include(cp => cp.Station)
                    .FirstOrDefaultAsync(cp => cp.PointId == request.ChargingPointId);

                if (chargingPoint == null)
                    return null;

                // 2. Verify staff assignment to this station
                var hasAccess = await VerifyStaffAssignmentAsync(staffId, chargingPoint.StationId);
                if (!hasAccess)
                    return null;

                // 3. Check point availability
                if (chargingPoint.Status != "available")
                    return null;

                // 4. Create or get guest driver profile
                var guestDriver = await GetOrCreateGuestDriverAsync();
                if (guestDriver == null)
                    return null;

                // 5. Prepare customer info JSON
                var customerInfo = new
                {
                    customerName = request.CustomerName,
                    customerPhone = request.CustomerPhone,
                    vehiclePlate = request.VehiclePlate,
                    vehicleModel = request.VehicleModel,
                    batteryCapacity = request.BatteryCapacity,
                    paymentMethod = request.PaymentMethod,
                    notes = request.Notes,
                    isWalkIn = true,
                    initiatedByStaffId = staffId,
                    initiatedAt = DateTime.UtcNow
                };

                // 6. Create charging session
                var session = new ChargingSession
                {
                    DriverId = guestDriver.DriverId,
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

                // 7. Update charging point status
                chargingPoint.Status = "in_use";
                await _db.SaveChangesAsync();

                // 8. Start monitoring
                await _sessionMonitorService.StartMonitoringAsync(session.SessionId);

                // 9. Log staff action
                await LogStaffActionAsync(staffId, "start_walk_in_session", session.SessionId, 
                    $"Walk-in customer: {request.CustomerName}, Plate: {request.VehiclePlate}");

                // 10. Calculate estimated cost and time
                var pricePerKwh = await _costCalculationService.GetCurrentPricePerKwhAsync(request.ChargingPointId);
                var batteryCapacity = request.BatteryCapacity ?? 50; // Default 50 kWh
                var energyNeeded = batteryCapacity * (request.TargetSOC - request.InitialSOC) / 100;
                var estimatedCost = energyNeeded * pricePerKwh;
                
                // Estimate time based on charging power (assume average 50kW)
                var chargingPower = chargingPoint.PowerOutput ?? 50;
                var estimatedHours = energyNeeded / chargingPower;
                var estimatedCompletionTime = DateTime.UtcNow.AddHours((double)estimatedHours);

                // 11. Create payment immediately if payment method is cash/card/pos (pending status)
                // Payment will be updated with actual cost when session completes
                if (request.PaymentMethod.ToLower() == "cash" || 
                    request.PaymentMethod.ToLower() == "card" || 
                    request.PaymentMethod.ToLower() == "pos")
                {
                    var userId = guestDriver.UserId;
                    var paymentRequest = new PaymentCreateRequest
                    {
                        UserId = userId,
                        SessionId = session.SessionId,
                        Amount = estimatedCost, // Estimated cost, will be updated when session completes
                        PaymentMethod = request.PaymentMethod.ToLower(),
                        Description = $"Walk-in payment for session {session.SessionId} - {request.CustomerName}"
                    };

                    var paymentResponse = await _paymentService.CreatePaymentAsync(paymentRequest);
                    
                    if (paymentResponse != null && string.IsNullOrEmpty(paymentResponse.ErrorMessage))
                    {
                        // Update PaymentType for walk-in payments
                        var payment = await _db.Payments.FindAsync(paymentResponse.PaymentId);
                        if (payment != null)
                        {
                            payment.PaymentType = "session_payment";
                            await _db.SaveChangesAsync();
                        }

                        await LogStaffActionAsync(staffId, "create_pending_payment", session.SessionId,
                            $"Payment method: {request.PaymentMethod}, Estimated amount: {estimatedCost}");
                    }
                }

                // 12. Get session details
                var sessionDetails = await _chargingService.GetSessionByIdAsync(session.SessionId);

                // 12. Return response
                return new WalkInSessionResponse
                {
                    Session = sessionDetails ?? new ChargingSessionResponse { SessionId = session.SessionId },
                    EstimatedCost = estimatedCost,
                    EstimatedCompletionTime = estimatedCompletionTime,
                    PaymentMethod = request.PaymentMethod,
                    PaymentInstructions = GetPaymentInstructions(request.PaymentMethod),
                    CustomerInfo = new WalkInCustomerInfo
                    {
                        CustomerName = request.CustomerName,
                        CustomerPhone = request.CustomerPhone,
                        VehiclePlate = request.VehiclePlate,
                        VehicleModel = request.VehicleModel
                    }
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error starting walk-in session: {ex.Message}");
                return null;
            }
        }

        // ========== EMERGENCY OPERATIONS ==========

        public async Task<ChargingSessionResponse?> EmergencyStopSessionAsync(int staffId, int sessionId, EmergencyStopRequest request)
        {
            try
            {
                // 1. Get session with related data
                var session = await _db.ChargingSessions
                    .Include(s => s.Point)
                    .ThenInclude(p => p.Station)
                    .FirstOrDefaultAsync(s => s.SessionId == sessionId);

                if (session == null)
                    return null;

                // 2. Verify staff has access to this station
                var hasAccess = await VerifyStaffAssignmentAsync(staffId, session.Point.StationId);
                if (!hasAccess)
                    return null;

                // 3. Stop the session using existing service
                var stopRequest = new ChargingSessionStopRequest
                {
                    SessionId = sessionId,
                    FinalSOC = session.InitialSoc + 10 // Estimate, should be from real-time data
                };

                var stoppedSession = await _chargingService.StopSessionAsync(stopRequest);
                if (stoppedSession == null)
                    return null;

                // 4. Mark charging point as maintenance (not available)
                if (request.RequiresMaintenance)
                {
                    var point = await _db.ChargingPoints.FindAsync(session.PointId);
                    if (point != null)
                    {
                        point.Status = "maintenance";
                        await _db.SaveChangesAsync();
                    }
                }

                // 5. Create incident report automatically
                var incident = new IncidentReport
                {
                    ReporterId = staffId,
                    PointId = session.PointId,
                    Title = $"Emergency Stop - Session #{sessionId}",
                    Description = request.Reason,
                    Priority = request.Severity,
                    Status = "open",
                    ReportedAt = DateTime.UtcNow
                };

                _db.IncidentReports.Add(incident);
                await _db.SaveChangesAsync();

                // 6. Log emergency event
                await LogStaffActionAsync(staffId, "emergency_stop", sessionId,
                    $"Reason: {request.Reason}, Severity: {request.Severity}");

                // 7. Create detailed session log
                var logRequest = new SessionLogCreateRequest
                {
                    SessionId = sessionId,
                    SOCPercentage = session.FinalSoc ?? session.InitialSoc,
                    CurrentPower = 0,
                    Voltage = 0,
                    Temperature = 0,
                    LogTime = DateTime.UtcNow
                };
                await _chargingService.CreateSessionLogAsync(logRequest);

                // 8. TODO: Notify admin if required (implement with SignalR or notification service)
                if (request.NotifyAdmin)
                {
                    // await _notificationService.NotifyAdminEmergencyAsync(sessionId, request.Reason);
                    Console.WriteLine($"[ADMIN ALERT] Emergency stop at session {sessionId}: {request.Reason}");
                }

                return stoppedSession;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in emergency stop: {ex.Message}");
                return null;
            }
        }

        // ========== PAUSE/RESUME OPERATIONS ==========

        public async Task<ChargingSessionResponse?> PauseSessionAsync(int staffId, int sessionId, PauseSessionRequest request)
        {
            try
            {
                // 1. Get session
                var session = await _db.ChargingSessions
                    .Include(s => s.Point)
                    .FirstOrDefaultAsync(s => s.SessionId == sessionId);

                if (session == null || session.Status != "in_progress")
                    return null;

                // 2. Verify staff access
                var hasAccess = await VerifyStaffAssignmentAsync(staffId, session.Point.StationId);
                if (!hasAccess)
                    return null;

                // 3. Update session status
                var statusRequest = new ChargingSessionStatusRequest
                {
                    SessionId = sessionId,
                    Status = "paused"
                };

                var updatedSession = await _chargingService.UpdateSessionStatusAsync(statusRequest);
                if (updatedSession == null)
                    return null;

                // 4. Update charging point status to "paused" (reserved for this session)
                var point = await _db.ChargingPoints.FindAsync(session.PointId);
                if (point != null)
                {
                    point.Status = "paused";
                    await _db.SaveChangesAsync();
                }

                // 5. Log pause event
                await LogStaffActionAsync(staffId, "pause_session", sessionId,
                    $"Reason: {request.Reason}, Max duration: {request.MaxPauseDuration} minutes");

                var logRequest = new SessionLogCreateRequest
                {
                    SessionId = sessionId,
                    SOCPercentage = session.FinalSoc ?? session.InitialSoc,
                    CurrentPower = 0,
                    Voltage = 0,
                    Temperature = 0,
                    LogTime = DateTime.UtcNow
                };
                await _chargingService.CreateSessionLogAsync(logRequest);

                // 6. TODO: Schedule auto-cancel timer (implement with background service)
                // await _backgroundJobService.ScheduleAutoCancelAsync(sessionId, request.MaxPauseDuration);

                return updatedSession;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error pausing session: {ex.Message}");
                return null;
            }
        }

        public async Task<ChargingSessionResponse?> ResumeSessionAsync(int staffId, int sessionId, ResumeSessionRequest request)
        {
            try
            {
                // 1. Get session
                var session = await _db.ChargingSessions
                    .Include(s => s.Point)
                    .FirstOrDefaultAsync(s => s.SessionId == sessionId);

                if (session == null || session.Status != "paused")
                    return null;

                // 2. Verify staff access
                var hasAccess = await VerifyStaffAssignmentAsync(staffId, session.Point.StationId);
                if (!hasAccess)
                    return null;

                // 3. Update session status back to in_progress
                var statusRequest = new ChargingSessionStatusRequest
                {
                    SessionId = sessionId,
                    Status = "in_progress"
                };

                var updatedSession = await _chargingService.UpdateSessionStatusAsync(statusRequest);
                if (updatedSession == null)
                    return null;

                // 4. Update charging point status back to in_use
                var point = await _db.ChargingPoints.FindAsync(session.PointId);
                if (point != null)
                {
                    point.Status = "in_use";
                    await _db.SaveChangesAsync();
                }

                // 5. Resume monitoring
                await _sessionMonitorService.StartMonitoringAsync(sessionId);

                // 6. Log resume event
                await LogStaffActionAsync(staffId, "resume_session", sessionId,
                    $"Notes: {request.Notes ?? "No notes"}");

                var logRequest = new SessionLogCreateRequest
                {
                    SessionId = sessionId,
                    SOCPercentage = session.FinalSoc ?? session.InitialSoc,
                    CurrentPower = 0,
                    Voltage = 0,
                    Temperature = 0,
                    LogTime = DateTime.UtcNow
                };
                await _chargingService.CreateSessionLogAsync(logRequest);

                return updatedSession;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error resuming session: {ex.Message}");
                return null;
            }
        }

        // ========== SESSION MONITORING ==========

        public async Task<StaffSessionsDashboard> GetMyStationSessionsAsync(int staffId, StaffSessionsFilterRequest filter)
        {
            try
            {
                // 1. Get assigned stations
                var stationIds = await GetAssignedStationsAsync(staffId);
                if (!stationIds.Any())
                {
                    return new StaffSessionsDashboard
                    {
                        Sessions = new List<ChargingSessionResponse>(),
                        TotalCount = 0
                    };
                }

                // 2. Get station info
                var stationInfo = await GetMyStationInfoAsync(staffId);

                // 3. Build query
                var query = _db.ChargingSessions
                    .Include(s => s.Driver)
                    .ThenInclude(d => d.User)
                    .Include(s => s.Point)
                    .ThenInclude(p => p.Station)
                    .Where(s => stationIds.Contains(s.Point.StationId));

                // Apply filters
                if (filter.Status != "all")
                {
                    query = query.Where(s => s.Status == filter.Status);
                }

                if (filter.Date.HasValue)
                {
                    var date = filter.Date.Value.Date;
                    query = query.Where(s => s.StartTime.Date == date);
                }

                if (filter.FromDate.HasValue && filter.ToDate.HasValue)
                {
                    query = query.Where(s => s.StartTime >= filter.FromDate.Value && s.StartTime <= filter.ToDate.Value);
                }

                // 4. Get total count
                var totalCount = await query.CountAsync();

                // 5. Apply pagination
                var sessions = await query
                    .OrderByDescending(s => s.StartTime)
                    .Skip((filter.Page - 1) * filter.PageSize)
                    .Take(filter.PageSize)
                    .ToListAsync();

                // 6. Map to DTOs
                var sessionDtos = sessions.Select(MapToSessionResponse).ToList();

                // 7. Calculate stats
                var today = DateTime.UtcNow.Date;
                var stats = new QuickStats
                {
                    ActiveSessions = await _db.ChargingSessions
                        .CountAsync(s => stationIds.Contains(s.Point.StationId) && s.Status == "in_progress"),
                    CompletedToday = await _db.ChargingSessions
                        .CountAsync(s => stationIds.Contains(s.Point.StationId) && 
                                        s.Status == "completed" && 
                                        s.StartTime.Date == today),
                    WalkInToday = 0, // TODO: Track walk-in sessions
                    EstimatedRevenueToday = await _db.ChargingSessions
                        .Where(s => stationIds.Contains(s.Point.StationId) && s.StartTime.Date == today)
                        .SumAsync(s => s.FinalCost ?? 0),
                    IncidentsToday = await _db.IncidentReports
                        .CountAsync(ir => stationIds.Contains(ir.Point.Station.StationId) && 
                                         ir.ReportedAt.HasValue &&
                                         ir.ReportedAt.Value.Date == today)
                };

                // 8. Return dashboard
                return new StaffSessionsDashboard
                {
                    Station = stationInfo ?? new StationInfo(),
                    Sessions = sessionDtos,
                    TotalCount = totalCount,
                    CurrentPage = filter.Page,
                    TotalPages = (int)Math.Ceiling(totalCount / (double)filter.PageSize),
                    Stats = stats
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting staff sessions: {ex.Message}");
                return new StaffSessionsDashboard();
            }
        }

        public async Task<ChargingSessionResponse?> GetSessionDetailAsync(int staffId, int sessionId)
        {
            try
            {
                // Get session
                var session = await _db.ChargingSessions
                    .Include(s => s.Point)
                    .FirstOrDefaultAsync(s => s.SessionId == sessionId);

                if (session == null)
                    return null;

                // Verify staff access
                var hasAccess = await VerifyStaffAssignmentAsync(staffId, session.Point.StationId);
                if (!hasAccess)
                    return null;

                // Return session details
                return await _chargingService.GetSessionByIdAsync(sessionId);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting session detail: {ex.Message}");
                return null;
            }
        }

        // ========== PAYMENT OPERATIONS ==========

        /// <summary>
        /// Lấy danh sách payments pending tại trạm của staff (để xác nhận thanh toán)
        /// </summary>
        public async Task<List<PaymentResponse>> GetPendingPaymentsAsync(int staffId)
        {
            try
            {
                // 1. Get assigned stations
                var stationIds = await GetAssignedStationsAsync(staffId);
                if (!stationIds.Any())
                    return new List<PaymentResponse>();

                // 2. Get pending payments for sessions at assigned stations
                var pendingPayments = await _db.Payments
                    .Include(p => p.Session)
                    .ThenInclude(s => s != null ? s.Point : null!)
                    .Where(p => p.PaymentStatus == "pending" &&
                               (p.PaymentMethod == "cash" || p.PaymentMethod == "card" || p.PaymentMethod == "pos") &&
                               p.SessionId.HasValue &&
                               p.Session != null &&
                               p.Session.Point != null &&
                               stationIds.Contains(p.Session.Point.StationId))
                    .OrderByDescending(p => p.CreatedAt)
                    .ToListAsync();

                return pendingPayments.Select(p => new PaymentResponse
                {
                    PaymentId = p.PaymentId,
                    UserId = p.UserId,
                    SessionId = p.SessionId,
                    Amount = p.Amount,
                    PaymentMethod = p.PaymentMethod ?? "",
                    PaymentStatus = p.PaymentStatus ?? "",
                    InvoiceNumber = p.InvoiceNumber,
                    CreatedAt = p.CreatedAt ?? DateTime.UtcNow
                }).ToList();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting pending payments: {ex.Message}");
                return new List<PaymentResponse>();
            }
        }

        // ========== LOGGING & AUDIT ==========

        /// <summary>
        /// Get staff ID by user ID
        /// Note: Since there's no separate Staff table, staffId = userId for users with role="Staff"
        /// This method verifies the user is a staff and returns the userId (which is used as staffId)
        /// </summary>
        public async Task<int> GetStaffIdByUserIdAsync(int userId)
        {
            try
            {
                var user = await _db.Users.FindAsync(userId);
                if (user == null || !user.Role.Equals("Staff", StringComparison.OrdinalIgnoreCase))
                {
                    return 0; // Not a staff
                }

                // In this system, staffId = userId (no separate Staff table)
                return userId;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting staff ID by user ID: {ex.Message}");
                return 0;
            }
        }

        public async Task LogStaffActionAsync(int staffId, string action, int sessionId, string? details = null)
        {
            try
            {
                // Create audit log entry
                var logEntry = new SessionLog
                {
                    SessionId = sessionId,
                    SocPercentage = 0,
                    CurrentPower = 0,
                    Voltage = 0,
                    Temperature = 0,
                    LogTime = DateTime.UtcNow
                };

                _db.SessionLogs.Add(logEntry);
                await _db.SaveChangesAsync();

                Console.WriteLine($"[AUDIT] Staff {staffId} - {action} - Session {sessionId}: {details ?? ""}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error logging staff action: {ex.Message}");
            }
        }

        // ========== HELPER METHODS ==========

        private async Task<DriverProfile?> GetOrCreateGuestDriverAsync()
        {
            try
            {
                // Try to find existing guest user
                var guestUser = await _db.Users
                    .Include(u => u.DriverProfile)
                    .FirstOrDefaultAsync(u => u.Email == "guest@evcharging.system");

                if (guestUser?.DriverProfile != null)
                    return guestUser.DriverProfile;

                // Create guest user if not exists
                if (guestUser == null)
                {
                    guestUser = new User
                    {
                        Name = "Guest Customer",
                        Email = "guest@evcharging.system",
                        Password = "N/A",
                        Phone = "N/A",
                        Role = "guest",
                        CreatedAt = DateTime.UtcNow
                    };
                    _db.Users.Add(guestUser);
                    await _db.SaveChangesAsync();
                }

                // Create driver profile for guest
                var guestDriver = new DriverProfile
                {
                    UserId = guestUser.UserId,
                    LicenseNumber = "GUEST",
                    VehicleModel = "Walk-in Customer",
                    VehiclePlate = "N/A"
                };

                _db.DriverProfiles.Add(guestDriver);
                await _db.SaveChangesAsync();

                return guestDriver;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error creating guest driver: {ex.Message}");
                return null;
            }
        }

        private string GetPaymentInstructions(string paymentMethod)
        {
            return paymentMethod switch
            {
                "cash" => "Customer will pay in cash at the end of charging session. Please collect payment before customer leaves.",
                "card" => "Customer will pay by card using POS terminal. Please process card payment after charging is complete.",
                "pos" => "Customer will pay using POS terminal. Please process payment after charging is complete.",
                _ => "Please collect payment from customer after charging is complete."
            };
        }

        private ChargingSessionResponse MapToSessionResponse(ChargingSession session)
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
                Status = session.Status ?? "unknown",
                CostBeforeDiscount = session.CostBeforeDiscount ?? 0,
                AppliedDiscount = session.AppliedDiscount ?? 0,
                FinalCost = session.FinalCost ?? 0,
                Driver = session.Driver != null ? new EVCharging.BE.Common.DTOs.Users.DriverProfileDTO
                {
                    DriverId = session.Driver.DriverId,
                    LicenseNumber = session.Driver.LicenseNumber ?? "",
                    VehicleModel = session.Driver.VehicleModel ?? "",
                    VehiclePlate = session.Driver.VehiclePlate ?? "",
                    BatteryCapacity = session.Driver.BatteryCapacity
                } : new EVCharging.BE.Common.DTOs.Users.DriverProfileDTO(),
                ChargingPoint = session.Point != null ? new EVCharging.BE.Common.DTOs.Stations.ChargingPointDTO
                {
                    PointId = session.Point.PointId,
                    StationId = session.Point.StationId,
                    ConnectorType = session.Point.ConnectorType ?? "",
                    PowerOutput = session.Point.PowerOutput ?? 0,
                    PricePerKwh = session.Point.PricePerKwh,
                    Status = session.Point.Status ?? "unknown"
                } : new EVCharging.BE.Common.DTOs.Stations.ChargingPointDTO()
            };
        }
    }
}

