using System;
using System.Collections.Generic;

namespace EVCharging.BE.DAL.Entities;

public partial class Notification
{
    public int NotificationId { get; set; }

    public int UserId { get; set; }

    public string Title { get; set; } = null!;

    public string? Message { get; set; }

    public string? Type { get; set; }

    public bool? IsRead { get; set; }

    public int? RelatedId { get; set; }

    public DateTime? CreatedAt { get; set; }

    public virtual User User { get; set; } = null!;
}
