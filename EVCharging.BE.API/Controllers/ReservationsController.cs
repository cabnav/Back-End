using EVCharging.BE.Common.DTOs.Reservations;
using EVCharging.BE.Services.Services.Reservations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace EVCharging.BE.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize] // yêu cầu đăng nhập bằng JWT
    public class ReservationsController : ControllerBase
    {
        private readonly IReservationService _reservationService;
        private readonly IQRCodeService _qrCodeService;
        private readonly IStationSearchService _stationSearchService;

        public ReservationsController(
            IReservationService reservationService,
            IQRCodeService qrCodeService,
            IStationSearchService stationSearchService)
        {
            _reservationService = reservationService;
            _qrCodeService = qrCodeService;
            _stationSearchService = stationSearchService;
        }

        // -------------------------------
        // 1️⃣ SEARCH compatible stations (tìm trạm sạc phù hợp)
        // -------------------------------
        [HttpPost("search-stations")]
        public async Task<IActionResult> SearchStations([FromBody] StationSearchRequest request)
        {
            var stations = await _stationSearchService.SearchCompatibleStationsAsync(request);
            return Ok(stations);
        }

        // -------------------------------
        // 2️⃣ GET compatible points at station (lấy điểm sạc phù hợp tại trạm)
        // -------------------------------
        [HttpGet("stations/{stationId}/compatible-points")]
        public async Task<IActionResult> GetCompatiblePoints(int stationId, [FromQuery] string connectorType)
        {
            var points = await _stationSearchService.GetCompatiblePointsAsync(stationId, connectorType);
            return Ok(points);
        }

        // -------------------------------
        // 3️⃣ GET available time slots (lấy khung giờ có sẵn)
        // -------------------------------
        [HttpGet("points/{pointId}/time-slots")]
        public async Task<IActionResult> GetTimeSlots(int pointId, [FromQuery] DateTime date)
        {
            var timeSlots = await _stationSearchService.GetAvailableTimeSlotsAsync(pointId, date);
            return Ok(timeSlots);
        }

        // -------------------------------
        // 4️⃣ CREATE reservation (tạo đặt chỗ)
        // -------------------------------
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] ReservationRequest request)
        {
            // Lấy userId từ JWT claims
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

            var result = await _reservationService.CreateReservationAsync(userId, request);
            return CreatedAtAction(nameof(GetMyReservations), new { id = result.ReservationId }, result);
        }

        // -------------------------------
        // 5️⃣ GET my reservations (danh sách đặt chỗ của tôi)
        // -------------------------------
        [HttpGet("me")]
        public async Task<IActionResult> GetMyReservations([FromQuery] ReservationFilter filter)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            filter.DriverId = userId;

            var reservations = await _reservationService.GetReservationsAsync(filter);
            return Ok(reservations);
        }

        // -------------------------------
        // 6️⃣ GET upcoming (các đặt chỗ sắp tới)
        // -------------------------------
        [HttpGet("upcoming")]
        public async Task<IActionResult> GetUpcoming([FromQuery] int hours = 48)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var upcoming = await _reservationService.GetUpcomingReservationsAsync(userId, TimeSpan.FromHours(hours));
            return Ok(upcoming);
        }

        // -------------------------------
        // 7️⃣ CANCEL reservation (huỷ đặt chỗ)
        // -------------------------------
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Cancel(int id, [FromQuery] string? reason = null)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var ok = await _reservationService.CancelReservationAsync(userId, id, reason);
            return ok ? NoContent() : NotFound();
        }

        // -------------------------------
        // 8️⃣ GENERATE QR Code (tạo mã QR)
        // -------------------------------
        [HttpGet("{id:int}/qrcode")]
        public async Task<IActionResult> GetQRCode(int id)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var filter = new ReservationFilter { DriverId = userId };
            var reservations = await _reservationService.GetReservationsAsync(filter);

            var reservation = reservations.FirstOrDefault(r => r.ReservationId == id);
            if (reservation == null)
                return NotFound();

            // Payload: nội dung mã QR (thông tin đặt chỗ + điểm sạc)
            var payload = $"EVCHG|RES={reservation.ReservationCode}|P={reservation.PointId}|D={reservation.DriverId}";
            var png = _qrCodeService.GenerateQRCode(payload);

            return File(png, "image/png");
        }
    }
}
