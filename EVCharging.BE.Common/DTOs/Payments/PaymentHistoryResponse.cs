namespace EVCharging.BE.Common.DTOs.Payments
{
    /// <summary>
    /// Response cho payment history (chưa thanh toán và đã thanh toán)
    /// </summary>
    public class PaymentHistoryResponse
    {
        public UnpaidSessionsResponse UnpaidSessions { get; set; } = new();
        public PaidInvoicesResponse PaidInvoices { get; set; } = new();
    }

    /// <summary>
    /// Danh sách sessions chưa thanh toán
    /// </summary>
    public class UnpaidSessionsResponse
    {
        public int Total { get; set; }
        public int Skip { get; set; }
        public int Take { get; set; }
        public List<UnpaidSessionDto> Items { get; set; } = new();
    }

    /// <summary>
    /// DTO cho session chưa thanh toán
    /// </summary>
    public class UnpaidSessionDto
    {
        public int SessionId { get; set; }
        public int DriverId { get; set; }
        public int PointId { get; set; }
        public int? ReservationId { get; set; }
        public string? Status { get; set; }
        public int? StationId { get; set; }
        public string? StationName { get; set; }
        public string? StationAddress { get; set; }
        public string? ConnectorType { get; set; }
        public int? PowerOutput { get; set; }
        public decimal? PricePerKwh { get; set; }
        public int InitialSoc { get; set; }
        public int? FinalSoc { get; set; }
        public decimal? EnergyUsed { get; set; }
        public int? DurationMinutes { get; set; }
        public decimal? CostBeforeDiscount { get; set; }
        public decimal? AppliedDiscount { get; set; }
        public decimal? FinalCost { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public string? Notes { get; set; }
        // Payment info nếu có pending payment
        public int? PaymentId { get; set; }
        public string? PaymentMethod { get; set; }
        public string PaymentStatus { get; set; } = "none"; // "none", "pending"
        public bool HasPendingPayment { get; set; }
    }

    /// <summary>
    /// Danh sách invoices đã thanh toán
    /// </summary>
    public class PaidInvoicesResponse
    {
        public int Total { get; set; }
        public int Skip { get; set; }
        public int Take { get; set; }
        public List<PaidInvoiceDto> Items { get; set; } = new();
    }

    /// <summary>
    /// DTO cho invoice đã thanh toán
    /// </summary>
    public class PaidInvoiceDto
    {
        public int InvoiceId { get; set; }
        public string InvoiceNumber { get; set; } = string.Empty;
        public decimal TotalAmount { get; set; }
        public string? Status { get; set; }
        public string? PaymentMethod { get; set; }
        public string? PaymentStatus { get; set; }
        public int? PaymentId { get; set; }
        public DateTime? CreatedAt { get; set; }
        public DateTime? PaidAt { get; set; }
        public SessionInfoDto? SessionInfo { get; set; }
    }
}

