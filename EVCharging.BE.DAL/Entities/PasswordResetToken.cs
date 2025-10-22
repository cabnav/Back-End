using System;
using System.Collections.Generic;

namespace EVCharging.BE.DAL.Entities;

public partial class PasswordResetToken
{
    public int Id { get; set; }

    public int UserId { get; set; }

    public string Token { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public DateTime ExpiresAt { get; set; }

    public DateTime? UsedAt { get; set; }

    public bool IsRevoked { get; set; }

    public virtual User User { get; set; } = null!;
}
