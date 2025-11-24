using EVCharging.BE.Common.DTOs.Corporates;

namespace EVCharging.BE.Services.Services.Users
{
    public interface ICorporateAccountService
    {
        Task<CorporateAccountDTO> CreateAsync(CorporateAccountCreateRequest req);
        Task<CorporateAccountDTO?> GetByIdAsync(int corporateId);
        Task<CorporateAccountDTO?> GetByAdminUserIdAsync(int adminUserId);
        Task<IEnumerable<CorporateAccountDTO>> GetAllAsync(int page = 1, int pageSize = 50, string? q = null);
        Task<IEnumerable<PendingDriverDTO>> GetPendingDriversAsync(int corporateId, int adminUserId);
        Task<IEnumerable<PendingDriverDTO>> GetDriversAsync(int corporateId, int adminUserId, string? status = "active");
        Task<bool> ApproveDriverAsync(int corporateId, int driverId, int adminUserId);
        Task<bool> RejectDriverAsync(int corporateId, int driverId, int adminUserId, string? reason = null);
        
        // Invoice Management (Postpaid)
        Task<IEnumerable<PendingSessionDto>> GetPendingSessionsAsync(int corporateId, int adminUserId);
        Task<CorporateInvoiceResponseDto> GenerateInvoiceAsync(int corporateId, int adminUserId, GenerateCorporateInvoiceRequest? request = null);
        Task<(IEnumerable<CorporateInvoiceResponseDto> Items, int Total)> GetCorporateInvoicesAsync(int corporateId, int adminUserId, int skip = 0, int take = 20);
        Task<CorporateInvoiceResponseDto?> GetCorporateInvoiceByIdAsync(int corporateId, int invoiceId, int adminUserId);
        Task<bool> PayCorporateInvoiceAsync(int corporateId, int invoiceId, int adminUserId, PayCorporateInvoiceRequest request);
        Task<CorporateInvoiceMomoPaymentResponseDto> PayCorporateInvoiceWithMomoAsync(int corporateId, int invoiceId, int adminUserId);
    }
}
