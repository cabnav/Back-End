using EVCharging.BE.Common.DTOs.Corporates;
using EVCharging.BE.DAL;
using EVCharging.BE.DAL.Entities;
using Microsoft.EntityFrameworkCore;

namespace EVCharging.BE.Services.Services.Implementations
{
    public class CorporateAccountService : ICorporateAccountService
    {
        private readonly EvchargingManagementContext _db;
        public CorporateAccountService(EvchargingManagementContext db) => _db = db;

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
    }
}
