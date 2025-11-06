using System;
using System.Collections.Generic;

namespace EVCharging.BE.DAL.Entities;

public partial class Payment
{
    public int PaymentId { get; set; }

    public int UserId { get; set; }

    public int? SessionId { get; set; }

    public int? ReservationId { get; set; }

    public decimal Amount { get; set; }

    public string? PaymentMethod { get; set; }

    public string? PaymentStatus { get; set; }

    public string? InvoiceNumber { get; set; }

    public DateTime? CreatedAt { get; set; }

    public string? PaymentType { get; set; }

    public virtual Reservation? Reservation { get; set; }

    public virtual ChargingSession? Session { get; set; }

    public virtual User User { get; set; } = null!;
}
