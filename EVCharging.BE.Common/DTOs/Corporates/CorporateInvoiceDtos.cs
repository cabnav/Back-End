namespace EVCharging.BE.Common.DTOs.Corporates
{
    /// <summary>
    /// DTO trả về thông tin Invoice của Corporate
    /// </summary>
    public class CorporateInvoiceResponseDto
    {
        public int InvoiceId { get; set; }
        public string InvoiceNumber { get; set; } = string.Empty;
        public int CorporateId { get; set; }
        public string CompanyName { get; set; } = string.Empty;
        public DateOnly BillingPeriodStart { get; set; }
        public DateOnly BillingPeriodEnd { get; set; }
        public decimal TotalAmount { get; set; }
        public string? Status { get; set; } // "pending", "paid", "overdue"
        public DateOnly DueDate { get; set; }
        public DateTime? CreatedAt { get; set; }
        public DateTime? PaidAt { get; set; }
        public int SessionCount { get; set; } // Số lượng sessions trong invoice
        public List<CorporateInvoiceItemDto> Items { get; set; } = new();
    }

    /// <summary>
    /// DTO cho InvoiceItem của Corporate
    /// </summary>
    public class CorporateInvoiceItemDto
    {
        public int ItemId { get; set; }
        public int? SessionId { get; set; }
        public int? DriverId { get; set; }
        public string? DriverName { get; set; }
        public string? StationName { get; set; }
        public string? Description { get; set; }
        public decimal? Quantity { get; set; }
        public decimal? UnitPrice { get; set; }
        public decimal? Amount { get; set; }
        public DateTime? SessionStartTime { get; set; }
        public DateTime? SessionEndTime { get; set; }
    }

    /// <summary>
    /// DTO request để tạo Invoice định kỳ
    /// </summary>
    public class GenerateCorporateInvoiceRequest
    {
        public DateOnly? BillingPeriodStart { get; set; } // Nếu null, lấy từ đầu tháng hiện tại
        public DateOnly? BillingPeriodEnd { get; set; } // Nếu null, lấy đến cuối tháng hiện tại
    }

    /// <summary>
    /// DTO request để thanh toán Invoice
    /// </summary>
    public class PayCorporateInvoiceRequest
    {
        public string PaymentMethod { get; set; } = "bank_transfer"; // "bank_transfer", "cash", "wallet"
        public string? TransactionReference { get; set; } // Số tham chiếu giao dịch (nếu có)
        public string? Notes { get; set; }
    }

    /// <summary>
    /// DTO cho danh sách Sessions pending (chưa có invoice)
    /// </summary>
    public class PendingSessionDto
    {
        public int SessionId { get; set; }
        public int DriverId { get; set; }
        public string? DriverName { get; set; }
        public int PointId { get; set; }
        public string? StationName { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public decimal? EnergyUsed { get; set; }
        public int? DurationMinutes { get; set; }
        public decimal? FinalCost { get; set; }
        public string? Status { get; set; }
    }
}

