using EVCharging.BE.Common.DTOs.Corporates;
using EVCharging.BE.Services.Services.Users;
using Microsoft.AspNetCore.Mvc;

namespace EVCharging.BE.API.Controllers
{
    [ApiController]
    [Route("api/corporate")] // giữ đúng path bạn đang dùng
    public class CorporateAccountsController : ControllerBase
    {
        private readonly ICorporateAccountService _svc;
        public CorporateAccountsController(ICorporateAccountService svc) { _svc = svc; }

        /// <summary>Create a corporate account</summary>
        [HttpPost(Name = "Corporate_Create")]
        public async Task<IActionResult> CreateCorporateAccount([FromBody] CorporateAccountCreateRequest req)
        {
            try
            {
                var dto = await _svc.CreateAsync(req);

                // ✅ Tránh lỗi "No route matches" – trả Location thủ công (đơn giản, an toàn)
                return Created($"/api/corporate/{dto.CorporateId}", dto);
                // Hoặc nếu sau này có action GetById chuẩn:
                // return CreatedAtAction(nameof(GetCorporateAccountById), new { corporateId = dto.CorporateId }, dto);
            }
            catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
            catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
            catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
        }

        /// <summary>List corporate accounts (paging + search)</summary>
        [HttpGet(Name = "Corporate_List")] // ✅ bỏ template thừa & khoảng trắng
        public async Task<IActionResult> GetCorporateAccounts(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 50,
            [FromQuery] string? q = null)
            => Ok(await _svc.GetAllAsync(page, pageSize, q));

        /// <summary>Get one corporate by id</summary>
        [HttpGet("{corporateId:int}", Name = "Corporate_GetById")]
        public async Task<IActionResult> GetCorporateAccountById([FromRoute] int corporateId)
        {
            // ⚠️ Tạm thời chưa có GetById trong service, cách này chỉ hoạt động nếu item nằm trong "first page".
            // Khuyên bạn thêm ICorporateAccountService.GetByIdAsync để chuẩn hơn.
            var list = await _svc.GetAllAsync(page: 1, pageSize: 200, q: null); // service sẽ clamp tối đa 200
            var item = list.FirstOrDefault(x => x.CorporateId == corporateId);
            return item is null ? NotFound(new { message = "Corporate not found" }) : Ok(item);
        }
    }
}
