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
            return CreatedAtAction(nameof(GetReservationByCode), new { reservationCode = result.ReservationCode }, result);
        }


        // -------------------------------
        // 🔍 GET reservation by code (tra cứu đặt chỗ bằng mã)
        // -------------------------------
        [HttpGet("lookup/{reservationCode}")]
        public async Task<IActionResult> GetReservationByCode(string reservationCode)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var reservation = await _reservationService.GetReservationByCodeAsync(userId, reservationCode);
            
            if (reservation == null)
                return NotFound(new { message = "Reservation not found or you don't have permission to view it." });
                
            return Ok(reservation);
        }

        // -------------------------------
        // 📋 GET station reservations (xem đặt chỗ của trạm)
        // -------------------------------
        [HttpGet("stations/{stationId}/reservations")]
        public async Task<IActionResult> GetStationReservations(int stationId, [FromQuery] int? pointId = null, [FromQuery] string? status = null, [FromQuery] DateTime? fromDate = null, [FromQuery] DateTime? toDate = null, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        {
            var filter = new ReservationFilter
            {
                StationId = stationId,
                PointId = pointId,
                Status = status,
                FromDate = fromDate,
                ToDate = toDate,
                Page = page,
                PageSize = pageSize
            };

            var reservations = await _reservationService.GetReservationsAsync(filter);
            return Ok(reservations);
        }

        // -------------------------------
        // 6️⃣ GET upcoming (các đặt chỗ sắp tới)
        // -------------------------------
        [HttpGet("upcoming")]
        public async Task<IActionResult> GetUpcoming([FromQuery] int hours = 48)
        {
            // Validation: hours phải > 0 và <= 8760 (1 năm)
            if (hours <= 0)
                return BadRequest(new { message = "Hours must be greater than 0." });
            
            if (hours > 8760) // 365 * 24 = 8760 hours = 1 year
                return BadRequest(new { message = "Hours cannot exceed 8760 (1 year)." });

            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var upcoming = await _reservationService.GetUpcomingReservationsAsync(userId, TimeSpan.FromHours(hours));
            return Ok(upcoming);
        }

        // -------------------------------
        // 7️⃣ CANCEL reservation (huỷ đặt chỗ bằng mã)
        // -------------------------------
        [HttpDelete("{reservationCode}")]
        public async Task<IActionResult> Cancel(string reservationCode, [FromQuery] string? reason = null)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var ok = await _reservationService.CancelReservationByCodeAsync(userId, reservationCode, reason);
            return ok ? NoContent() : NotFound();
        }

        // -------------------------------
        // 8️⃣ GENERATE QR Code (tạo mã QR bằng reservation code)
        // -------------------------------
        [HttpGet("{reservationCode}/qrcode")]
        public async Task<IActionResult> GetQRCode(string reservationCode)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var reservation = await _reservationService.GetReservationByCodeAsync(userId, reservationCode);
            
            if (reservation == null)
                return NotFound(new { message = "Reservation not found or you don't have permission to view it." });

            // Payload: nội dung mã QR (thông tin đặt chỗ + điểm sạc)
            var payload = $"EVCHG|RES={reservation.ReservationCode}|P={reservation.PointId}|D={reservation.DriverId}";
            var png = _qrCodeService.GenerateQRCode(payload);

            return File(png, "image/png");
        }
    }
}
