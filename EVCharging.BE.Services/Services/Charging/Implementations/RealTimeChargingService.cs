using EVCharging.BE.Common.DTOs.Charging;
using EVCharging.BE.DAL;
using EVCharging.BE.DAL.Entities;
using EVCharging.BE.Services.Services.Charging;
using EVCharging.BE.Services.Services.Notification;
using Microsoft.EntityFrameworkCore;

namespace EVCharging.BE.Services.Services.Charging.Implementations
{
    /// <summary>
    /// Service for real-time charging session monitoring
    /// </summary>
    public class RealTimeChargingService : IRealTimeChargingService
    {
        private readonly EvchargingManagementContext _db;
        private readonly IEmailService _emailService;
        private readonly ISignalRNotificationService _signalRService;

        public RealTimeChargingService(
            EvchargingManagementContext db,
            IEmailService emailService,
            ISignalRNotificationService signalRService)
        {
            _db = db;
            _emailService = emailService;
            _signalRService = signalRService;
        }

        /// <summary>
        /// Get real-time session data including SOC and remaining time
        /// </summary>
        public async Task<RealTimeSessionDTO?> GetRealTimeSessionAsync(int sessionId)
        {
            var session = await _db.ChargingSessions
                .Include(s => s.Driver)
                .ThenInclude(d => d.User)
                .Include(s => s.Point)
                .ThenInclude(p => p.Station)
                .FirstOrDefaultAsync(s => s.SessionId == sessionId);

            if (session == null) return null;

            // Calculate real-time data
            var duration = DateTime.UtcNow - session.StartTime;
            var energyUsed = CalculateEnergyUsed(session);
            var currentPower = await GetCurrentPowerAsync(session.PointId);
            var averagePower = energyUsed / (duration.TotalHours > 0 ? duration.TotalHours : 1);

            // Estimate remaining time (simplified calculation)
            var estimatedRemaining = await CalculateRemainingTimeAsync(sessionId, session.InitialSoc, session.FinalSoc);

            return new RealTimeSessionDTO
            {
                SessionId = session.SessionId,
                DriverId = session.DriverId,
                PointId = session.PointId,
                StartTime = session.StartTime,
                EndTime = session.EndTime,
                Status = session.Status ?? "Unknown",
                CurrentSOC = session.InitialSoc, // This would be updated from real charging data
                InitialSOC = session.InitialSoc,
                TargetSOC = session.FinalSoc,
                EnergyUsed = (double)energyUsed,
                CurrentPower = currentPower,
                AveragePower = averagePower,
                DurationMinutes = (int)duration.TotalMinutes,
                EstimatedRemainingMinutes = estimatedRemaining,
                EstimatedCompletionTime = estimatedRemaining.HasValue ? DateTime.UtcNow.AddMinutes(estimatedRemaining.Value) : null,
                CurrentCost = CalculateCurrentCost(session),
                EstimatedTotalCost = CalculateEstimatedTotalCost(session),
                PricePerKwh = session.Point.PricePerKwh,
                ChargingPoint = new ChargingPointInfoDTO
                {
                    PointId = session.Point.PointId,
                    StationId = session.Point.StationId,
                    StationName = session.Point.Station.Name,
                    StationAddress = session.Point.Station.Address,
                    ConnectorType = Enum.TryParse<EVCharging.BE.Common.Enums.ConnectorType>(session.Point.ConnectorType, out var connectorType) ? connectorType : EVCharging.BE.Common.Enums.ConnectorType.AC,
                    PowerOutput = session.Point.PowerOutput ?? 0,
                    Status = session.Point.Status ?? "Unknown"
                }
            };
        }

        /// <summary>
        /// Update session with current SOC and power data
        /// </summary>
        public async Task<bool> UpdateSessionDataAsync(int sessionId, int currentSOC, double currentPower)
        {
            try
            {
                var session = await _db.ChargingSessions
                    .Include(s => s.Point)
                    .FirstOrDefaultAsync(s => s.SessionId == sessionId);

                if (session == null) return false;

                // Update charging point current power
                session.Point.CurrentPower = currentPower;
                
                // Update session data
                var energyUsed = CalculateEnergyUsed(session);
                session.EnergyUsed = (decimal)energyUsed;
                session.DurationMinutes = (int)(DateTime.UtcNow - session.StartTime).TotalMinutes;

                // Calculate costs
                var currentCost = CalculateCurrentCost(session);
                session.CostBeforeDiscount = currentCost;
                session.FinalCost = currentCost; // Simplified - no discount applied

                await _db.SaveChangesAsync();

                // Send real-time update via SignalR
                var realTimeData = await GetRealTimeSessionAsync(sessionId);
                if (realTimeData != null)
                {
                    await _signalRService.NotifySessionUpdateAsync(sessionId, new ChargingSessionResponse
                    {
                        SessionId = realTimeData.SessionId,
                        DriverId = realTimeData.DriverId,
                        ChargingPointId = realTimeData.PointId,
                        StartTime = realTimeData.StartTime,
                        Status = realTimeData.Status,
                        EnergyUsed = (decimal)realTimeData.EnergyUsed,
                        DurationMinutes = realTimeData.DurationMinutes,
                        FinalCost = realTimeData.CurrentCost
                    });
                }

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error updating session data: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Calculate estimated remaining time based on current SOC and target
        /// </summary>
        public async Task<int?> CalculateRemainingTimeAsync(int sessionId, int currentSOC, int? targetSOC)
        {
            if (!targetSOC.HasValue) return null;

            var session = await _db.ChargingSessions
                .Include(s => s.Point)
                .FirstOrDefaultAsync(s => s.SessionId == sessionId);

            if (session == null) return null;

            var socNeeded = targetSOC.Value - currentSOC;
            if (socNeeded <= 0) return 0;

            // Simplified calculation: assume average charging rate
            var averagePower = session.Point.PowerOutput ?? 50; // kW
            var batteryCapacity = 80; // kWh (typical EV battery)
            var socPerHour = (averagePower / batteryCapacity) * 100; // SOC percentage per hour

            if (socPerHour <= 0) return null;

            var hoursNeeded = socNeeded / socPerHour;
            return (int)(hoursNeeded * 60); // Convert to minutes
        }

        /// <summary>
        /// Get all active sessions for a driver
        /// </summary>
        public async Task<IEnumerable<RealTimeSessionDTO>> GetActiveSessionsAsync(int driverId)
        {
            var sessions = await _db.ChargingSessions
                .Include(s => s.Driver)
                .ThenInclude(d => d.User)
                .Include(s => s.Point)
                .ThenInclude(p => p.Station)
                .Where(s => s.DriverId == driverId && s.Status == "in_progress")
                .ToListAsync();

            var result = new List<RealTimeSessionDTO>();
            foreach (var session in sessions)
            {
                var realTimeData = await GetRealTimeSessionAsync(session.SessionId);
                if (realTimeData != null)
                    result.Add(realTimeData);
            }

            return result;
        }

        /// <summary>
        /// Check if charging is complete and send notifications
        /// </summary>
        public async Task<bool> CheckChargingCompletionAsync(int sessionId)
        {
            var session = await _db.ChargingSessions
                .Include(s => s.Driver)
                .ThenInclude(d => d.User)
                .Include(s => s.Point)
                .ThenInclude(p => p.Station)
                .FirstOrDefaultAsync(s => s.SessionId == sessionId);

            if (session == null || session.Status != "in_progress") return false;

            // Check if target SOC is reached (simplified logic)
            var currentSOC = session.InitialSoc; // This would be actual current SOC from charging data
            if (session.FinalSoc.HasValue && currentSOC >= session.FinalSoc.Value)
            {
                // Charging complete
                session.Status = "completed";
                session.EndTime = DateTime.UtcNow;
                session.FinalSoc = currentSOC;
                session.EnergyUsed = (decimal?)CalculateEnergyUsed(session);
                session.FinalCost = CalculateCurrentCost(session);

                await _db.SaveChangesAsync();

                // Send completion notification
                await SendChargingCompletionNotificationAsync(session);

                return true;
            }

            return false;
        }

        /// <summary>
        /// Send charging completion notification via email and SMS
        /// </summary>
        private async Task SendChargingCompletionNotificationAsync(ChargingSession session)
        {
            try
            {
                var completionData = new ChargingCompletionDTO
                {
                    SessionId = session.SessionId,
                    DriverId = session.DriverId,
                    DriverName = session.Driver?.User?.Name ?? "Driver",
                    DriverEmail = session.Driver?.User?.Email ?? "",
                    DriverPhone = session.Driver?.User?.Phone ?? "",
                    StartTime = session.StartTime,
                    EndTime = session.EndTime ?? DateTime.UtcNow,
                    InitialSOC = session.InitialSoc,
                    FinalSOC = session.FinalSoc ?? 0,
                    EnergyUsed = (double)(session.EnergyUsed ?? 0),
                    TotalCost = session.FinalCost ?? 0,
                    DurationMinutes = session.DurationMinutes ?? 0,
                    StationName = session.Point.Station.Name,
                    StationAddress = session.Point.Station.Address
                };

                // Send email notification
                if (!string.IsNullOrEmpty(completionData.DriverEmail))
                {
                    var subject = "Charging Session Completed";
                    var body = $@"
                        <h2>Charging Session Completed</h2>
                        <p>Dear {completionData.DriverName},</p>
                        <p>Your charging session has been completed successfully.</p>
                        <ul>
                            <li><strong>Station:</strong> {completionData.StationName}</li>
                            <li><strong>Address:</strong> {completionData.StationAddress}</li>
                            <li><strong>Duration:</strong> {completionData.DurationMinutes} minutes</li>
                            <li><strong>Energy Used:</strong> {completionData.EnergyUsed:F2} kWh</li>
                            <li><strong>Total Cost:</strong> ${completionData.TotalCost:F2}</li>
                            <li><strong>Initial SOC:</strong> {completionData.InitialSOC}%</li>
                            <li><strong>Final SOC:</strong> {completionData.FinalSOC}%</li>
                        </ul>
                        <p>Thank you for using our charging service!</p>";

                    await _emailService.SendEmailAsync(completionData.DriverEmail, subject, body);
                }

                // TODO: Implement SMS notification service
                // await _smsService.SendSMSAsync(completionData.DriverPhone, "Charging completed successfully!");

                Console.WriteLine($"Charging completion notification sent for session {session.SessionId}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error sending completion notification: {ex.Message}");
            }
        }

        /// <summary>
        /// Calculate energy used in the session
        /// </summary>
        private double CalculateEnergyUsed(ChargingSession session)
        {
            if (session.EnergyUsed.HasValue)
                return (double)session.EnergyUsed.Value;

            // Simplified calculation based on duration and power
            var duration = DateTime.UtcNow - session.StartTime;
            var averagePower = session.Point.PowerOutput ?? 50; // kW
            return duration.TotalHours * averagePower;
        }

        /// <summary>
        /// Get current power output from charging point
        /// </summary>
        private async Task<double> GetCurrentPowerAsync(int pointId)
        {
            var point = await _db.ChargingPoints
                .FirstOrDefaultAsync(p => p.PointId == pointId);

            return point?.CurrentPower ?? 0;
        }

        /// <summary>
        /// Calculate current cost of the session
        /// </summary>
        private decimal CalculateCurrentCost(ChargingSession session)
        {
            var energyUsed = CalculateEnergyUsed(session);
            return (decimal)energyUsed * session.Point.PricePerKwh;
        }

        /// <summary>
        /// Calculate estimated total cost
        /// </summary>
        private decimal CalculateEstimatedTotalCost(ChargingSession session)
        {
            // Simplified estimation
            return CalculateCurrentCost(session);
        }
    }
}
