using EVCharging.BE.DAL;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace EVCharging.BE.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class NotificationsController : ControllerBase
    {
        private readonly EvchargingManagementContext _db;

        public NotificationsController(EvchargingManagementContext db)
        {
            _db = db;
        }

        [HttpGet("my")]
        public async Task<IActionResult> GetMyNotifications([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        {
            try
            {
                var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

                // Validate pagination
                if (page < 1) page = 1;
                if (pageSize < 1 || pageSize > 100) pageSize = 20;

                // Tối ưu: Query data trước, count sau để tránh timeout
                var data = await _db.Notifications
                    .Where(n => n.UserId == userId)
                    .OrderByDescending(n => n.CreatedAt)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .Select(n => new
                    {
                        n.NotificationId,
                        n.Title,
                        n.Message,
                        n.Type,
                        n.IsRead,
                        n.CreatedAt
                    })
                    .ToListAsync();

                // Chỉ count nếu cần (estimate nếu page đầu và ít data)
                int total;
                if (page == 1 && data.Count < pageSize)
                {
                    total = data.Count;
                }
                else
                {
                    total = await _db.Notifications
                        .Where(n => n.UserId == userId)
                        .CountAsync();
                }

                return Ok(new { total, data });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error retrieving notifications", error = ex.Message });
            }
        }

        [HttpGet("unread-count")]
        public async Task<IActionResult> GetUnreadCount()
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var count = await _db.Notifications.CountAsync(n => n.UserId == userId && !(n.IsRead ?? false));
            return Ok(new { unread = count });
        }

        [HttpPut("{id}/read")]
        public async Task<IActionResult> MarkRead(int id)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var n = await _db.Notifications.FirstOrDefaultAsync(x => x.NotificationId == id && x.UserId == userId);
            if (n == null) return NotFound();
            n.IsRead = true;
            await _db.SaveChangesAsync();
            return NoContent();
        }

        [HttpPut("read-all")]
        public async Task<IActionResult> MarkAllRead()
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var list = await _db.Notifications.Where(n => n.UserId == userId && !(n.IsRead ?? false)).ToListAsync();
            foreach (var it in list) it.IsRead = true;
            await _db.SaveChangesAsync();
            return NoContent();
        }
    }
}
