namespace EVCharging.BE.Services.Services.Background;

public class ReservationBackgroundOptions
{
    public const string SectionName = "ReservationBackground";

    public int CheckIntervalSeconds { get; set; } = 60; // Check every minute by default
    public int ReminderMinutes { get; set; } = 30; // Send reminder 30 minutes before
    public int ExpireGraceMinutes { get; set; } = 15; // Grace period before expiring reservations
    public int NoShowGraceMinutes { get; set; } = 30; // Auto-cancel if not checked in after 30 minutes from start
    public int BookingCutoffMinutes { get; set; } = 55; // Latest booking is within 55 minutes after slot start
    public int EarlyCheckInMinutes { get; set; } = 30; // Maximum check-in time before reservation start time (e.g., 30 minutes)
}