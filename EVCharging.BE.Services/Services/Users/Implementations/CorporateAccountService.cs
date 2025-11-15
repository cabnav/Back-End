using EVCharging.BE.Common.DTOs.Corporates;
using EVCharging.BE.DAL;
using EVCharging.BE.DAL.Entities;
using EVCharging.BE.Services.Services.Notification;
using Microsoft.EntityFrameworkCore;
using PaymentEntity = EVCharging.BE.DAL.Entities.Payment;

namespace EVCharging.BE.Services.Services.Users.Implementations
{
    public class CorporateAccountService : ICorporateAccountService
    {
        private readonly EvchargingManagementContext _db;
        private readonly INotificationService _notificationService;

        public CorporateAccountService(EvchargingManagementContext db, INotificationService notificationService)
        {
            _db = db;
            _notificationService = notificationService;
        }

        public async Task<CorporateAccountDTO> CreateAsync(CorporateAccountCreateRequest req)
        {
            if (string.IsNullOrWhiteSpace(req.CompanyName))
                throw new ArgumentException("CompanyName is required");

            // công ty trùng tên?
            var dup = await _db.CorporateAccounts.AnyAsync(c => c.CompanyName == req.CompanyName);
            if (dup) throw new InvalidOperationException("CompanyName already exists");

            // admin user phải tồn tại
            var admin = await _db.Users.FindAsync(req.AdminUserId)
                        ?? throw new KeyNotFoundException("Admin user not found");

            var entity = new CorporateAccount
            {
                CompanyName = req.CompanyName,
                TaxCode = req.TaxCode,
                ContactPerson = req.ContactPerson,
                ContactEmail = req.ContactEmail,
                BillingType = req.BillingType,
                CreditLimit = req.CreditLimit ?? 0m,
                AdminUserId = req.AdminUserId,
                Status = string.IsNullOrWhiteSpace(req.Status) ? "active" : req.Status,
                CreatedAt = DateTime.UtcNow
            };

            _db.CorporateAccounts.Add(entity);
            await _db.SaveChangesAsync();

            // driverCount = 0 ngay sau khi tạo
            return new CorporateAccountDTO
            {
                CorporateId = entity.CorporateId,
                CompanyName = entity.CompanyName,
                ContactPerson = entity.ContactPerson,
                ContactEmail = entity.ContactEmail,
                BillingType = entity.BillingType ?? "",
                CreditLimit = entity.CreditLimit ?? 0m,
                Status = entity.Status ?? "",
                AdminUserId = entity.AdminUserId,
                CreatedAt = entity.CreatedAt,
                DriverCount = 0
            };
        }

        public async Task<IEnumerable<CorporateAccountDTO>> GetAllAsync(int page = 1, int pageSize = 50, string? q = null)
        {
            if (page < 1) page = 1;
            if (pageSize <= 0 || pageSize > 200) pageSize = 50;

            var baseQuery = _db.CorporateAccounts.AsNoTracking().AsQueryable();

            if (!string.IsNullOrWhiteSpace(q))
            {
                var k = q.Trim();
                baseQuery = baseQuery.Where(c =>
                    c.CompanyName!.Contains(k) ||
                    (c.ContactPerson ?? "").Contains(k) ||
                    (c.ContactEmail ?? "").Contains(k));
            }

            var list = await baseQuery
                .OrderBy(c => c.CorporateId)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(c => new CorporateAccountDTO
                {
                    CorporateId = c.CorporateId,
                    CompanyName = c.CompanyName!,
                    ContactPerson = c.ContactPerson,
                    ContactEmail = c.ContactEmail,
                    BillingType = c.BillingType ?? "",
                    CreditLimit = c.CreditLimit ?? 0m,
                    Status = c.Status ?? "",
                    AdminUserId = c.AdminUserId,
                    CreatedAt = c.CreatedAt,
                    // đếm số driver thuộc DN
                    DriverCount = _db.DriverProfiles.Count(p => p.CorporateId == c.CorporateId)
                })
                .ToListAsync();

            return list;
        }

        public async Task<CorporateAccountDTO?> GetByIdAsync(int corporateId)
        {
            var corporate = await _db.CorporateAccounts
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.CorporateId == corporateId);

            if (corporate == null) return null;

            return new CorporateAccountDTO
            {
                CorporateId = corporate.CorporateId,
                CompanyName = corporate.CompanyName!,
                ContactPerson = corporate.ContactPerson,
                ContactEmail = corporate.ContactEmail,
                BillingType = corporate.BillingType ?? "",
                CreditLimit = corporate.CreditLimit ?? 0m,
                Status = corporate.Status ?? "",
                AdminUserId = corporate.AdminUserId,
                CreatedAt = corporate.CreatedAt,
                DriverCount = await _db.DriverProfiles.CountAsync(p => p.CorporateId == corporateId && p.Status == "active")
            };
        }

        public async Task<IEnumerable<PendingDriverDTO>> GetPendingDriversAsync(int corporateId, int adminUserId)
        {
            // ✅ Kiểm tra quyền: User phải là AdminUserId
            var corporate = await _db.CorporateAccounts
                .FirstOrDefaultAsync(c => c.CorporateId == corporateId);
            
            if (corporate == null)
                throw new KeyNotFoundException("Corporate không tồn tại");
            
            if (corporate.AdminUserId != adminUserId)
                throw new UnauthorizedAccessException("Bạn không có quyền xem drivers của Corporate này");

            // ✅ Lấy danh sách drivers pending
            var pendingDrivers = await _db.DriverProfiles
                .Include(d => d.User)
                .Where(d => d.CorporateId == corporateId && d.Status == "pending")
                .OrderByDescending(d => d.CreatedAt)
                .Select(d => new PendingDriverDTO
                {
                    DriverId = d.DriverId,
                    UserId = d.UserId,
                    UserName = d.User.Name,
                    UserEmail = d.User.Email,
                    UserPhone = d.User.Phone,
                    LicenseNumber = d.LicenseNumber,
                    VehicleModel = d.VehicleModel,
                    VehiclePlate = d.VehiclePlate,
                    BatteryCapacity = d.BatteryCapacity,
                    CreatedAt = d.CreatedAt
                })
                .ToListAsync();

            return pendingDrivers;
        }

        public async Task<IEnumerable<PendingDriverDTO>> GetDriversAsync(int corporateId, int adminUserId, string? status = "active")
        {
            // ✅ Kiểm tra quyền: User phải là AdminUserId
            var corporate = await _db.CorporateAccounts
                .FirstOrDefaultAsync(c => c.CorporateId == corporateId);
            
            if (corporate == null)
                throw new KeyNotFoundException("Corporate không tồn tại");
            
            if (corporate.AdminUserId != adminUserId)
                throw new UnauthorizedAccessException("Bạn không có quyền xem drivers của Corporate này");

            // ✅ Lấy danh sách drivers theo status
            var query = _db.DriverProfiles
                .Include(d => d.User)
                .Where(d => d.CorporateId == corporateId);

            if (!string.IsNullOrWhiteSpace(status))
            {
                query = query.Where(d => d.Status == status);
            }

            var drivers = await query
                .OrderByDescending(d => d.CreatedAt)
                .Select(d => new PendingDriverDTO
                {
                    DriverId = d.DriverId,
                    UserId = d.UserId,
                    UserName = d.User.Name,
                    UserEmail = d.User.Email,
                    UserPhone = d.User.Phone,
                    LicenseNumber = d.LicenseNumber,
                    VehicleModel = d.VehicleModel,
                    VehiclePlate = d.VehiclePlate,
                    BatteryCapacity = d.BatteryCapacity,
                    CreatedAt = d.CreatedAt
                })
                .ToListAsync();

            return drivers;
        }

        public async Task<bool> ApproveDriverAsync(int corporateId, int driverId, int adminUserId)
        {
            // ✅ Kiểm tra quyền
            var corporate = await _db.CorporateAccounts
                .FirstOrDefaultAsync(c => c.CorporateId == corporateId);
            
            if (corporate == null)
                throw new KeyNotFoundException("Corporate không tồn tại");
            
            if (corporate.AdminUserId != adminUserId)
                throw new UnauthorizedAccessException("Bạn không có quyền approve driver này");

            // ✅ Lấy driver profile
            var driver = await _db.DriverProfiles
                .Include(d => d.User)
                .Include(d => d.Corporate)
                .FirstOrDefaultAsync(d => d.DriverId == driverId && d.CorporateId == corporateId);
            
            if (driver == null)
                throw new KeyNotFoundException("Driver không tồn tại hoặc không thuộc Corporate này");
            
            if (driver.Status != "pending")
                throw new InvalidOperationException($"Driver đã được xử lý (Status: {driver.Status})");

            // ✅ Update driver status
            driver.Status = "active";
            driver.ApprovedByUserId = adminUserId;
            driver.ApprovedAt = DateTime.UtcNow;
            driver.UpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();

            // ✅ Gửi notification cho Driver
            await _notificationService.SendNotificationAsync(
                driver.UserId,
                "Chúc mừng! Bạn đã được chấp nhận",
                $"Bạn đã được chấp nhận tham gia công ty {corporate.CompanyName}. Bạn có thể sử dụng dịch vụ ngay bây giờ.",
                "driver_approved",
                driver.DriverId
            );

            return true;
        }

        public async Task<bool> RejectDriverAsync(int corporateId, int driverId, int adminUserId, string? reason = null)
        {
            // ✅ Kiểm tra quyền
            var corporate = await _db.CorporateAccounts
                .FirstOrDefaultAsync(c => c.CorporateId == corporateId);
            
            if (corporate == null)
                throw new KeyNotFoundException("Corporate không tồn tại");
            
            if (corporate.AdminUserId != adminUserId)
                throw new UnauthorizedAccessException("Bạn không có quyền reject driver này");

            // ✅ Lấy driver profile
            var driver = await _db.DriverProfiles
                .Include(d => d.User)
                .Include(d => d.Corporate)
                .FirstOrDefaultAsync(d => d.DriverId == driverId && d.CorporateId == corporateId);
            
            if (driver == null)
                throw new KeyNotFoundException("Driver không tồn tại hoặc không thuộc Corporate này");
            
            if (driver.Status != "pending")
                throw new InvalidOperationException($"Driver đã được xử lý (Status: {driver.Status})");

            // ✅ Update driver status
            driver.Status = "rejected";
            driver.ApprovedByUserId = adminUserId;
            driver.ApprovedAt = DateTime.UtcNow;
            driver.UpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();

            // ✅ Gửi notification cho Driver
            var message = $"Bạn đã bị từ chối tham gia công ty {corporate.CompanyName}.";
            if (!string.IsNullOrWhiteSpace(reason))
                message += $" Lý do: {reason}";
            
            await _notificationService.SendNotificationAsync(
                driver.UserId,
                "Yêu cầu tham gia công ty bị từ chối",
                message,
                "driver_rejected",
                driver.DriverId
            );

            return true;
        }

        // ====== INVOICE MANAGEMENT (POSTPAID) ======

        public async Task<IEnumerable<PendingSessionDto>> GetPendingSessionsAsync(int corporateId, int adminUserId)
        {
            // ✅ Kiểm tra quyền
            var corporate = await _db.CorporateAccounts
                .FirstOrDefaultAsync(c => c.CorporateId == corporateId);
            
            if (corporate == null)
                throw new KeyNotFoundException("Corporate không tồn tại");
            
            if (corporate.AdminUserId != adminUserId)
                throw new UnauthorizedAccessException("Bạn không có quyền xem sessions của Corporate này");

            // ✅ Lấy các sessions của drivers có CorporateId và Status = "active", chưa có InvoiceItem
            var pendingSessions = await _db.ChargingSessions
                .Include(s => s.Driver)
                    .ThenInclude(d => d.User)
                .Include(s => s.Point)
                    .ThenInclude(p => p.Station)
                .Where(s => s.Driver.CorporateId == corporateId 
                    && s.Driver.Status == "active"
                    && s.Status == "completed" // Chỉ lấy sessions đã hoàn thành
                    && s.FinalCost.HasValue
                    && !s.InvoiceItems.Any()) // Chưa có InvoiceItem (chưa được gom vào invoice)
                .OrderBy(s => s.StartTime)
                .Select(s => new PendingSessionDto
                {
                    SessionId = s.SessionId,
                    DriverId = s.DriverId,
                    DriverName = s.Driver.User.Name,
                    PointId = s.PointId,
                    StationName = s.Point.Station.Name,
                    StartTime = s.StartTime,
                    EndTime = s.EndTime,
                    EnergyUsed = s.EnergyUsed,
                    DurationMinutes = s.DurationMinutes,
                    FinalCost = s.FinalCost,
                    Status = s.Status
                })
                .ToListAsync();

            return pendingSessions;
        }

        public async Task<CorporateInvoiceResponseDto> GenerateInvoiceAsync(int corporateId, int adminUserId, GenerateCorporateInvoiceRequest? request = null)
        {
            // ✅ Kiểm tra quyền
            var corporate = await _db.CorporateAccounts
                .FirstOrDefaultAsync(c => c.CorporateId == corporateId);
            
            if (corporate == null)
                throw new KeyNotFoundException("Corporate không tồn tại");
            
            if (corporate.AdminUserId != adminUserId)
                throw new UnauthorizedAccessException("Bạn không có quyền tạo invoice cho Corporate này");

            // ✅ Xác định billing period
            var now = DateTime.UtcNow;
            var billingPeriodStart = request?.BillingPeriodStart 
                ?? DateOnly.FromDateTime(new DateTime(now.Year, now.Month, 1)); // Đầu tháng
            var billingPeriodEnd = request?.BillingPeriodEnd 
                ?? DateOnly.FromDateTime(new DateTime(now.Year, now.Month, DateTime.DaysInMonth(now.Year, now.Month))); // Cuối tháng

            // ✅ Lấy các sessions pending trong billing period
            var pendingSessions = await _db.ChargingSessions
                .Include(s => s.Driver)
                    .ThenInclude(d => d.User)
                .Include(s => s.Point)
                    .ThenInclude(p => p.Station)
                .Where(s => s.Driver.CorporateId == corporateId 
                    && s.Driver.Status == "active"
                    && s.Status == "completed"
                    && s.FinalCost.HasValue
                    && !s.InvoiceItems.Any()
                    && DateOnly.FromDateTime(s.StartTime) >= billingPeriodStart
                    && DateOnly.FromDateTime(s.StartTime) <= billingPeriodEnd)
                .ToListAsync();

            if (!pendingSessions.Any())
                throw new InvalidOperationException("Không có sessions nào để tạo invoice trong khoảng thời gian này");

            // ✅ Tính tổng tiền
            var totalAmount = pendingSessions.Sum(s => s.FinalCost ?? 0m);

            // ✅ Tạo số hóa đơn
            var invoiceNumber = $"CORP-{corporateId}-{DateTime.UtcNow:yyyyMMddHHmmss}";

            // ✅ Tạo Invoice
            var invoice = new Invoice
            {
                InvoiceNumber = invoiceNumber,
                CorporateId = corporateId,
                UserId = null, // Corporate invoice không có UserId
                BillingPeriodStart = billingPeriodStart,
                BillingPeriodEnd = billingPeriodEnd,
                TotalAmount = totalAmount,
                Status = "pending",
                DueDate = billingPeriodEnd.AddDays(30), // Hạn thanh toán: 30 ngày sau billing period end
                CreatedAt = DateTime.UtcNow,
                PaidAt = null
            };

            _db.Invoices.Add(invoice);
            await _db.SaveChangesAsync();

            // ✅ Tạo InvoiceItems cho từng session
            foreach (var session in pendingSessions)
            {
                var invoiceItem = new InvoiceItem
                {
                    InvoiceId = invoice.InvoiceId,
                    SessionId = session.SessionId,
                    Description = $"Phiên sạc #{session.SessionId} - {session.Point?.Station?.Name} - Driver: {session.Driver.User.Name}",
                    Quantity = 1,
                    UnitPrice = session.FinalCost ?? 0m,
                    Amount = session.FinalCost ?? 0m
                };
                _db.InvoiceItems.Add(invoiceItem);
            }

            await _db.SaveChangesAsync();

            // ✅ Load lại invoice với items để trả về
            var invoiceWithItems = await _db.Invoices
                .Include(i => i.Corporate)
                .Include(i => i.InvoiceItems)
                    .ThenInclude(item => item.Session!)
                        .ThenInclude(s => s.Driver)
                            .ThenInclude(d => d.User)
                .Include(i => i.InvoiceItems)
                    .ThenInclude(item => item.Session!)
                        .ThenInclude(s => s.Point)
                            .ThenInclude(p => p.Station)
                .FirstOrDefaultAsync(i => i.InvoiceId == invoice.InvoiceId);

            return MapToCorporateInvoiceResponseDto(invoiceWithItems!);
        }

        public async Task<(IEnumerable<CorporateInvoiceResponseDto> Items, int Total)> GetCorporateInvoicesAsync(
            int corporateId, int adminUserId, int skip = 0, int take = 20)
        {
            // ✅ Kiểm tra quyền
            var corporate = await _db.CorporateAccounts
                .FirstOrDefaultAsync(c => c.CorporateId == corporateId);
            
            if (corporate == null)
                throw new KeyNotFoundException("Corporate không tồn tại");
            
            if (corporate.AdminUserId != adminUserId)
                throw new UnauthorizedAccessException("Bạn không có quyền xem invoices của Corporate này");

            // ✅ Lấy danh sách invoices
            var invoices = await _db.Invoices
                .Include(i => i.Corporate)
                .Include(i => i.InvoiceItems)
                    .ThenInclude(item => item.Session!)
                        .ThenInclude(s => s.Driver)
                            .ThenInclude(d => d.User)
                .Include(i => i.InvoiceItems)
                    .ThenInclude(item => item.Session!)
                        .ThenInclude(s => s.Point)
                            .ThenInclude(p => p.Station)
                .Where(i => i.CorporateId == corporateId)
                .OrderByDescending(i => i.CreatedAt)
                .Skip(skip)
                .Take(take)
                .ToListAsync();

            var total = await _db.Invoices
                .Where(i => i.CorporateId == corporateId)
                .CountAsync();

            var invoiceList = invoices.Select(i => MapToCorporateInvoiceResponseDto(i)).ToList();

            return (invoiceList, total);
        }

        public async Task<CorporateInvoiceResponseDto?> GetCorporateInvoiceByIdAsync(int corporateId, int invoiceId, int adminUserId)
        {
            // ✅ Kiểm tra quyền
            var corporate = await _db.CorporateAccounts
                .FirstOrDefaultAsync(c => c.CorporateId == corporateId);
            
            if (corporate == null)
                throw new KeyNotFoundException("Corporate không tồn tại");
            
            if (corporate.AdminUserId != adminUserId)
                throw new UnauthorizedAccessException("Bạn không có quyền xem invoice này");

            // ✅ Lấy invoice
            var invoice = await _db.Invoices
                .Include(i => i.Corporate)
                .Include(i => i.InvoiceItems)
                    .ThenInclude(item => item.Session!)
                        .ThenInclude(s => s.Driver)
                            .ThenInclude(d => d.User)
                .Include(i => i.InvoiceItems)
                    .ThenInclude(item => item.Session!)
                        .ThenInclude(s => s.Point)
                            .ThenInclude(p => p.Station)
                .FirstOrDefaultAsync(i => i.InvoiceId == invoiceId && i.CorporateId == corporateId);

            if (invoice == null)
                return null;

            return MapToCorporateInvoiceResponseDto(invoice);
        }

        public async Task<bool> PayCorporateInvoiceAsync(int corporateId, int invoiceId, int adminUserId, PayCorporateInvoiceRequest request)
        {
            // ✅ Kiểm tra quyền
            var corporate = await _db.CorporateAccounts
                .FirstOrDefaultAsync(c => c.CorporateId == corporateId);
            
            if (corporate == null)
                throw new KeyNotFoundException("Corporate không tồn tại");
            
            if (corporate.AdminUserId != adminUserId)
                throw new UnauthorizedAccessException("Bạn không có quyền thanh toán invoice này");

            // ✅ Lấy invoice
            var invoice = await _db.Invoices
                .FirstOrDefaultAsync(i => i.InvoiceId == invoiceId && i.CorporateId == corporateId);

            if (invoice == null)
                throw new KeyNotFoundException("Invoice không tồn tại");

            if (invoice.Status == "paid")
                throw new InvalidOperationException("Invoice đã được thanh toán rồi");

            // ✅ Update invoice status
            invoice.Status = "paid";
            invoice.PaidAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            // ✅ Tạo Payment record (nếu cần tracking)
            // Lưu ý: Payment.UserId là NOT NULL, nên dùng AdminUserId
            var payment = new PaymentEntity
            {
                UserId = adminUserId, // Dùng AdminUserId vì Payment.UserId là NOT NULL
                SessionId = null, // Corporate invoice có nhiều sessions
                Amount = invoice.TotalAmount,
                PaymentMethod = request.PaymentMethod,
                PaymentStatus = "success",
                PaymentType = "corporate_invoice",
                InvoiceNumber = invoice.InvoiceNumber,
                CreatedAt = DateTime.UtcNow
            };
            _db.Payments.Add(payment);
            await _db.SaveChangesAsync();

            return true;
        }

        /// <summary>
        /// Map Invoice entity sang CorporateInvoiceResponseDto
        /// </summary>
        private CorporateInvoiceResponseDto MapToCorporateInvoiceResponseDto(Invoice invoice)
        {
            return new CorporateInvoiceResponseDto
            {
                InvoiceId = invoice.InvoiceId,
                InvoiceNumber = invoice.InvoiceNumber,
                CorporateId = invoice.CorporateId ?? 0,
                CompanyName = invoice.Corporate?.CompanyName ?? "",
                BillingPeriodStart = invoice.BillingPeriodStart,
                BillingPeriodEnd = invoice.BillingPeriodEnd,
                TotalAmount = invoice.TotalAmount,
                Status = invoice.Status,
                DueDate = invoice.DueDate,
                CreatedAt = invoice.CreatedAt,
                PaidAt = invoice.PaidAt,
                SessionCount = invoice.InvoiceItems.Count,
                Items = invoice.InvoiceItems.Select(item => new CorporateInvoiceItemDto
                {
                    ItemId = item.ItemId,
                    SessionId = item.SessionId,
                    DriverId = item.Session?.DriverId,
                    DriverName = item.Session?.Driver?.User?.Name,
                    StationName = item.Session?.Point?.Station?.Name,
                    Description = item.Description,
                    Quantity = item.Quantity,
                    UnitPrice = item.UnitPrice,
                    Amount = item.Amount,
                    SessionStartTime = item.Session?.StartTime,
                    SessionEndTime = item.Session?.EndTime
                }).ToList()
            };
        }
    }
}
