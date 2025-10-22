namespace EVCharging.BE.Services.Services.Background;

public class ReservationBackgroundOptions
{
    public const string SectionName = "ReservationBackground";

    public int CheckIntervalSeconds { get; set; } = 60; // Check every minute by default
    public int ReminderMinutes { get; set; } = 30; // Send reminder 30 minutes before
    public int ExpireGraceMinutes { get; set; } = 15; // Grace period before expiring reservations
}