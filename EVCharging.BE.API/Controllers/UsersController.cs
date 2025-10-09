namespace EVCharging.BE.API.Controllers
{
    using EVCharging.BE.DAL;
    using EVCharging.BE.DAL.Entities;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.EntityFrameworkCore;

    [ApiController]
    [Route("api/[controller]")]
    public class UsersController : ControllerBase
    {
        private readonly EvchargingManagementContext _db;

        public UsersController(EvchargingManagementContext db) => _db = db;

        [HttpGet]
        public async Task<IActionResult> GetAll() => Ok(await _db.Users.ToListAsync());

        [HttpGet("{id}")]
        public async Task<IActionResult> Get(int id)
        {
            var user = await _db.Users.FindAsync(id);
            if (user == null) return NotFound();
            return Ok(user);
        }

        [HttpPost]
        public async Task<IActionResult> Create(User user)
        {
            _db.Users.Add(user);
            await _db.SaveChangesAsync();
            return CreatedAtAction(nameof(Get), new { id = user.UserId }, user);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, User updated)
        {
            if (id != updated.UserId) return BadRequest();
            _db.Users.Update(updated);
            await _db.SaveChangesAsync();
            return NoContent();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var user = await _db.Users.FindAsync(id);
            if (user == null) return NotFound();
            _db.Users.Remove(user);
            await _db.SaveChangesAsync();
            return NoContent();
        }
    }

}
