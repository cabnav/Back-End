using EVCharging.BE.DAL;
using EVCharging.BE.DAL.Entities;
using Microsoft.AspNetCore.Mvc;

// ✅ thêm DTO cần dùng
using EVCharging.BE.Common.DTOs.Payments;
using EVCharging.BE.Common.DTOs.Users;
using EVCharging.BE.Services.Services.Users;

namespace EVCharging.BE.API.Controllers
{
    [ApiController]
    [Route("api/users")]
    public class UsersController : ControllerBase
    {
        private readonly IUserService _service;

        public UsersController(IUserService service)
        {
            _service = service;
        }

        // ===== CRUD cơ bản =====
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var users = await _service.GetAllAsync();
            return Ok(users);
        }

        [HttpGet("{id:int}")]
        public async Task<IActionResult> Get(int id)
        {
            var user = await _service.GetByIdAsync(id);
            if (user == null) return NotFound(new { message = "User not found" });
            return Ok(user);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] User user)
        {
            var created = await _service.CreateAsync(user);
            return CreatedAtAction(nameof(Get), new { id = created.UserId }, created);
        }

        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, [FromBody] User user)
        {
            var success = await _service.UpdateAsync(id, user);
            if (!success) return BadRequest(new { message = "Update failed" });
            return NoContent();
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            var success = await _service.DeleteAsync(id);
            if (!success) return NotFound(new { message = "User not found" });
            return NoContent();
        }

        // ====== ✅ MỞ RỘNG: Ví & Hồ sơ ======

        // 1) Nạp tiền ví
        // POST /api/users/{id}/wallet/topup
        [HttpPost("{id:int}/wallet/topup")]
        public async Task<IActionResult> WalletTopUp(int id, [FromBody] TopUpRequest body)
        {
            if (body == null || body.Amount <= 0)
                return BadRequest(new { message = "Amount phải > 0" });

            var dto = await _service.WalletTopUpAsync(id, body.Amount, body.Description);
            return Ok(dto);
        }

        public class TopUpRequest
        {
            public decimal Amount { get; set; }
            public string? Description { get; set; }
        }

        // 2) Lịch sử giao dịch ví
        // GET /api/users/{id}/wallet/transactions?page=1&pageSize=50
        [HttpGet("{id:int}/wallet/transactions")]
        public async Task<IActionResult> GetWalletTransactions(
            int id,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 50)
        {
            var list = await _service.GetWalletTransactionsAsync(id, page, pageSize);
            return Ok(list);
        }

        // 3) Cập nhật hồ sơ (User + DriverProfile nếu có)
        // PUT /api/users/{id}/profile
        [HttpPut("{id:int}/profile")]
        public async Task<IActionResult> UpdateUserProfile(int id, [FromBody] UserUpdateRequest req)
        {
            if (req == null) return BadRequest(new { message = "Body rỗng" });

            var ok = await _service.UpdateUserProfileAsync(id, req);
            if (!ok) return NotFound(new { message = "User not found" });
            return NoContent();
        }
    }
}
