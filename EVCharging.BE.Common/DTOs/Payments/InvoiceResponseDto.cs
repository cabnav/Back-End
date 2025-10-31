namespace EVCharging.BE.Common.DTOs.Payments
{
    /// <summary>
    /// DTO trả về thông tin hóa đơn
    /// </summary>
    public class InvoiceResponseDto
    {
        public int InvoiceId { get; set; }
        public string InvoiceNumber { get; set; } = string.Empty;
        public int? UserId { get; set; }
        public decimal TotalAmount { get; set; }
        public string? Status { get; set; }
        public DateTime? CreatedAt { get; set; }
        public DateTime? PaidAt { get; set; }
        public List<InvoiceItemDto> Items { get; set; } = new();
        public SessionInfoDto? SessionInfo { get; set; }
    }

    public class InvoiceItemDto
    {
        public int ItemId { get; set; }
        public int? SessionId { get; set; }
        public string? Description { get; set; }
        public decimal? Quantity { get; set; }
        public decimal? UnitPrice { get; set; }
        public decimal? Amount { get; set; }
    }

    public class SessionInfoDto
    {
        public int SessionId { get; set; }
        public string? StationName { get; set; }
        public string? StationAddress { get; set; }
        public decimal? EnergyUsed { get; set; }
        public int? DurationMinutes { get; set; }
        public decimal? CostBeforeDiscount { get; set; }
        public decimal? AppliedDiscount { get; set; }
        public decimal? FinalCost { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime? EndTime { get; set; }
    }
}

