using System;

namespace EVCharging.BE.DAL.Entities;

public partial class EmailOTP
{
    public int OtpId { get; set; }

    public string Email { get; set; } = null!;

    public string OtpCode { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public DateTime ExpiresAt { get; set; }

    public bool IsUsed { get; set; }

    public string? Purpose { get; set; }
}

