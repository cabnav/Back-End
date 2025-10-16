using EVCharging.BE.Common.DTOs.Corporates;

namespace EVCharging.BE.Services.Services
{
    public interface ICorporateAccountService
    {
        Task<CorporateAccountDTO> CreateAsync(CorporateAccountCreateRequest req);
        Task<IEnumerable<CorporateAccountDTO>> GetAllAsync(int page = 1, int pageSize = 50, string? q = null);
    }
}
