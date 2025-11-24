using EVCharging.BE.Common.DTOs.Payments;
using EVCharging.BE.DAL;
using EVCharging.BE.DAL.Entities;
using Microsoft.EntityFrameworkCore;

namespace EVCharging.BE.Services.Services.Payment.Implementations
{
    /// <summary>
    /// Service quản lý hóa đơn
    /// </summary>
    public class InvoiceService : IInvoiceService
    {
        private readonly EvchargingManagementContext _db;

        public InvoiceService(EvchargingManagementContext db)
        {
            _db = db;
        }

        public async Task<InvoiceResponseDto?> GetInvoiceByIdAsync(int invoiceId, int currentUserId)
        {
            var invoice = await _db.Invoices
                .Include(i => i.InvoiceItems)
                    .ThenInclude(item => item.Session!)
                        .ThenInclude(s => s.Point)
                            .ThenInclude(p => p.Station)
                .Include(i => i.User)
                .FirstOrDefaultAsync(i => i.InvoiceId == invoiceId);

            if (invoice == null)
                return null;

            // Kiểm tra quyền - chỉ chủ sở hữu mới được xem
            if (invoice.UserId != currentUserId)
                throw new UnauthorizedAccessException("Bạn không có quyền xem hóa đơn này.");

            return await MapToInvoiceResponseDtoAsync(invoice);
        }

        public async Task<InvoiceResponseDto?> GetInvoiceBySessionIdAsync(int sessionId, int currentUserId)
        {
            // Kiểm tra session có tồn tại và thuộc về user không
            var session = await _db.ChargingSessions
                .Include(s => s.Driver)
                .FirstOrDefaultAsync(s => s.SessionId == sessionId);

            if (session == null)
                return null;

            if (session.Driver?.UserId != currentUserId)
                throw new UnauthorizedAccessException("Bạn không có quyền xem hóa đơn của phiên sạc này.");

            // Kiểm tra session đã được thanh toán chưa
            var payment = await _db.Payments
                .Where(p => p.SessionId == sessionId && p.PaymentStatus == "success")
                .OrderByDescending(p => p.CreatedAt)
                .FirstOrDefaultAsync();

            if (payment == null)
                return null;

            // Tìm invoice thông qua InvoiceItem với SessionId
            var invoiceItem = await _db.InvoiceItems
                .Include(item => item.Invoice)
                    .ThenInclude(inv => inv!.InvoiceItems)
                        .ThenInclude(item => item.Session!)
                            .ThenInclude(s => s.Point)
                                .ThenInclude(p => p.Station)
                .Include(item => item.Invoice!)
                    .ThenInclude(inv => inv!.User)
                .Where(item => item.SessionId == sessionId)
                .OrderByDescending(item => item.Invoice!.CreatedAt)
                .FirstOrDefaultAsync();

            if (invoiceItem?.Invoice == null)
                return null;

            return await MapToInvoiceResponseDtoAsync(invoiceItem.Invoice);
        }

        public async Task<(IEnumerable<InvoiceResponseDto> Items, int Total)> GetUserInvoicesAsync(
            int userId, 
            int skip = 0, 
            int take = 20)
        {
            var invoices = await _db.Invoices
                .Include(i => i.InvoiceItems)
                    .ThenInclude(item => item.Session!)
                        .ThenInclude(s => s.Point)
                            .ThenInclude(p => p.Station)
                .Where(i => i.UserId == userId)
                .OrderByDescending(i => i.CreatedAt)
                .Skip(skip)
                .Take(take)
                .ToListAsync();

            var total = await _db.Invoices
                .Where(i => i.UserId == userId)
                .CountAsync();

            var invoiceList = new List<InvoiceResponseDto>();
            foreach (var invoice in invoices)
            {
                invoiceList.Add(await MapToInvoiceResponseDtoAsync(invoice));
            }

            return (invoiceList, total);
        }

        public async Task<InvoiceResponseDto> CreateInvoiceForSessionAsync(
            int sessionId,
            int userId,
            decimal amount,
            string paymentMethod,
            string invoiceNumberPrefix = "INV")
        {
            var session = await _db.ChargingSessions
                .Include(s => s.Point)
                    .ThenInclude(p => p.Station)
                .FirstOrDefaultAsync(s => s.SessionId == sessionId);

            if (session == null)
                throw new InvalidOperationException($"Không tìm thấy phiên sạc với ID: {sessionId}");

            // Tạo số hóa đơn
            var prefix = paymentMethod == "wallet" ? "WALLET" : "CASH";
            var invoiceNumber = $"{invoiceNumberPrefix}-{prefix}-{sessionId}-{DateTime.UtcNow:yyyyMMddHHmmss}";
            var currentDate = DateOnly.FromDateTime(DateTime.UtcNow);

            // Tạo Invoice
            var invoice = new Invoice
            {
                InvoiceNumber = invoiceNumber,
                UserId = userId,
                BillingPeriodStart = currentDate,
                BillingPeriodEnd = currentDate,
                TotalAmount = amount,
                Status = "paid",
                DueDate = currentDate,
                CreatedAt = DateTime.UtcNow,
                PaidAt = DateTime.UtcNow
            };

            _db.Invoices.Add(invoice);
            await _db.SaveChangesAsync();

            // Tạo InvoiceItem
            var invoiceItem = new InvoiceItem
            {
                InvoiceId = invoice.InvoiceId,
                SessionId = sessionId,
                Description = $"Phiên sạc #{sessionId} - Trạm: {session.Point.Station.Name}",
                Quantity = 1,
                UnitPrice = amount,
                Amount = amount
            };

            _db.InvoiceItems.Add(invoiceItem);
            await _db.SaveChangesAsync();

            // Load lại invoice với items
            var invoiceWithItems = await _db.Invoices
                .Include(i => i.InvoiceItems)
                    .ThenInclude(item => item.Session!)
                        .ThenInclude(s => s.Point)
                            .ThenInclude(p => p.Station)
                .FirstOrDefaultAsync(i => i.InvoiceId == invoice.InvoiceId);

            return await MapToInvoiceResponseDtoAsync(invoiceWithItems!);
        }

        /// <summary>
        /// Map Invoice entity sang InvoiceResponseDto
        /// </summary>
        private async Task<InvoiceResponseDto> MapToInvoiceResponseDtoAsync(Invoice invoice)
        {
            var firstItem = invoice.InvoiceItems.FirstOrDefault();
            var session = firstItem?.Session;

            // Lấy phương thức thanh toán từ bảng Payments
            var payment = await _db.Payments
                .Where(p => p.InvoiceNumber == invoice.InvoiceNumber && p.PaymentStatus == "success")
                .OrderByDescending(p => p.CreatedAt)
                .FirstOrDefaultAsync();

            return new InvoiceResponseDto
            {
                InvoiceId = invoice.InvoiceId,
                InvoiceNumber = invoice.InvoiceNumber,
                UserId = invoice.UserId,
                TotalAmount = invoice.TotalAmount,
                DepositAmount = session?.DepositAmount, // Thêm tiền cọc vào invoice
                Status = invoice.Status,
                PaymentMethod = payment?.PaymentMethod,
                CreatedAt = invoice.CreatedAt,
                PaidAt = invoice.PaidAt,
                Items = invoice.InvoiceItems.Select(item => new InvoiceItemDto
                {
                    ItemId = item.ItemId,
                    SessionId = item.SessionId,
                    Description = item.Description,
                    Quantity = item.Quantity,
                    UnitPrice = item.UnitPrice,
                    Amount = item.Amount
                }).ToList(),
                SessionInfo = session != null ? new SessionInfoDto
                {
                    SessionId = session.SessionId,
                    DriverId = session.DriverId,
                    PointId = session.PointId,
                    ReservationId = session.ReservationId,
                    Status = session.Status,
                    StationId = session.Point?.Station?.StationId,
                    StationName = session.Point?.Station?.Name,
                    StationAddress = session.Point?.Station?.Address,
                    ConnectorType = session.Point?.ConnectorType,
                    PowerOutput = session.Point?.PowerOutput,
                    PricePerKwh = session.Point?.PricePerKwh,
                    InitialSoc = session.InitialSoc,
                    FinalSoc = session.FinalSoc,
                    EnergyUsed = session.EnergyUsed,
                    DurationMinutes = session.DurationMinutes,
                    CostBeforeDiscount = session.CostBeforeDiscount,
                    AppliedDiscount = session.AppliedDiscount,
                    DepositAmount = session.DepositAmount, // Thêm tiền cọc vào session info
                    FinalCost = session.FinalCost,
                    StartTime = session.StartTime,
                    EndTime = session.EndTime,
                    Notes = session.Notes
                } : null
            };
        }
    }
}

