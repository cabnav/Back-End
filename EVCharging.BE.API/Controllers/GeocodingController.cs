using EVCharging.BE.Common.DTOs.Shared;
using EVCharging.BE.Common.DTOs.Stations;
using EVCharging.BE.Common.DTOs.Stations;
using EVCharging.BE.Services.Services.Charging;
using EVCharging.BE.Services.Services.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EVCharging.BE.API.Controllers
{
    /// <summary>
    /// Controller cho chức năng Geocoding - Chuyển đổi địa chỉ sang tọa độ
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class GeocodingController : ControllerBase
    {
        private readonly ILocationService _locationService;
        private readonly IChargingStationService _stationService;

        public GeocodingController(
            ILocationService locationService,
            IChargingStationService stationService)
        {
            _locationService = locationService;
            _stationService = stationService;
        }

        /// <summary>
        /// 🌍 Chuyển địa chỉ sang tọa độ (Geocoding)
        /// </summary>
        /// <remarks>
        /// Ví dụ request body:
        /// ```json
        /// {
        ///   "address": "123 Nguyễn Huệ, Quận 1, TP.HCM",
        ///   "countryCode": "VN",
        ///   "language": "vi"
        /// }
        /// ```
        /// **Lưu ý**: 
        /// - language phải dùng mã ISO 639-1: "vi" (không phải "vietnamese")
        /// - countryCode: "VN" cho Việt Nam
        /// </remarks>
        /// <param name="request">Thông tin địa chỉ</param>
        /// <returns>Tọa độ (latitude, longitude) và thông tin chi tiết</returns>
        [HttpPost("convert")]
        public async Task<ActionResult<GeocodingResponseDTO>> GeocodeAddress([FromBody] GeocodingRequestDTO request)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.Address))
                {
                    return BadRequest(new { message = "Vui lòng nhập địa chỉ" });
                }

                var result = await _locationService.GeocodeAddressAsync(request);

                if (!result.Success)
                {
                    return BadRequest(new 
                    { 
                        message = "Không tìm thấy địa chỉ", 
                        error = result.ErrorMessage,
                        hint = "Hãy thử nhập địa chỉ đầy đủ hơn hoặc kiểm tra lại chính tả",
                        data = result 
                    });
                }

                return Ok(new { message = "Chuyển đổi thành công", data = result });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Lỗi khi chuyển đổi địa chỉ", error = ex.Message });
            }
        }

        /// <summary>
        /// 🔍 Tìm trạm sạc gần địa chỉ của bạn - Đơn giản nhất!
        /// </summary>
        /// <remarks>
        /// **Cách dùng đơn giản:**
        /// 
        /// GET /api/Geocoding/nearby?address=Bitexco Tower, Quận 1, TP.HCM
        /// 
        /// **Hoặc với tham số đầy đủ:**
        /// 
        /// GET /api/Geocoding/nearby?address=Đại học FPT&amp;radiusKm=15&amp;countryCode=VN
        /// 
        /// Hệ thống sẽ tự động:
        /// 1. Chuyển địa chỉ → tọa độ
        /// 2. Tìm trạm sạc gần nhất
        /// </remarks>
        /// <param name="address">Địa chỉ của bạn</param>
        /// <param name="radiusKm">Bán kính tìm kiếm (km) - Mặc định: 10km</param>
        /// <param name="countryCode">Mã quốc gia - Mặc định: VN</param>
        /// <returns>Danh sách trạm sạc gần địa chỉ</returns>
        [HttpGet("nearby")]
        public async Task<ActionResult> FindNearbyStations(
            [FromQuery] string address,
            [FromQuery] double radiusKm = 10,
            [FromQuery] string countryCode = "VN")
        {
            try
            {
                if (string.IsNullOrWhiteSpace(address))
                {
                    return BadRequest(new { message = "Vui lòng nhập địa chỉ (address)" });
                }

                // Bước 1: Chuyển địa chỉ → tọa độ
                var coordinates = await _locationService.GetCoordinatesFromAddressAsync(address, countryCode);

                if (coordinates == null)
                {
                    return NotFound(new 
                    { 
                        message = "Không tìm thấy địa chỉ",
                        hint = "Hãy thử nhập địa chỉ rõ ràng hơn, ví dụ: 'Bitexco Tower, District 1, Ho Chi Minh City'"
                    });
                }

                // Bước 2: Tìm trạm sạc gần tọa độ
                var filter = new StationFilterDTO
                {
                    Latitude = coordinates.Value.latitude,
                    Longitude = coordinates.Value.longitude,
                    MaxDistanceKm = radiusKm
                };

                var stations = await _stationService.SearchStationsAsync(filter);

                return Ok(new
                {
                    message = "Tìm kiếm thành công! 🎉",
                    yourLocation = new
                    {
                        address,
                        latitude = coordinates.Value.latitude,
                        longitude = coordinates.Value.longitude,
                        googleMapsUrl = $"https://www.google.com/maps?q={coordinates.Value.latitude},{coordinates.Value.longitude}"
                    },
                    searchRadius = $"{radiusKm} km",
                    totalFound = stations.Count(),
                    stations
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Lỗi khi tìm kiếm", error = ex.Message });
            }
        }

        /// <summary>
        /// 📍 Tính khoảng cách từ địa chỉ đến trạm sạc
        /// </summary>
        /// <param name="fromAddress">Địa chỉ xuất phát</param>
        /// <param name="stationId">ID trạm sạc đích</param>
        /// <param name="countryCode">Mã quốc gia - Mặc định: VN</param>
        /// <returns>Khoảng cách và link chỉ đường Google Maps</returns>
        [HttpGet("distance")]
        public async Task<ActionResult> CalculateDistance(
            [FromQuery] string fromAddress,
            [FromQuery] int stationId,
            [FromQuery] string countryCode = "VN")
        {
            try
            {
                if (string.IsNullOrWhiteSpace(fromAddress))
                {
                    return BadRequest(new { message = "Vui lòng nhập địa chỉ xuất phát (fromAddress)" });
                }

                // Lấy tọa độ từ địa chỉ
                var userCoords = await _locationService.GetCoordinatesFromAddressAsync(fromAddress, countryCode);
                if (userCoords == null)
                {
                    return NotFound(new { message = "Không tìm thấy địa chỉ xuất phát" });
                }

                // Lấy thông tin trạm
                var station = await _stationService.GetByIdAsync(stationId);
                if (station == null)
                {
                    return NotFound(new { message = $"Không tìm thấy trạm sạc ID {stationId}" });
                }

                if (!station.Latitude.HasValue || !station.Longitude.HasValue)
                {
                    return BadRequest(new { message = "Trạm này chưa có thông tin vị trí" });
                }

                // Tính khoảng cách
                var distanceKm = _locationService.CalculateDistance(
                    userCoords.Value.latitude,
                    userCoords.Value.longitude,
                    station.Latitude.Value,
                    station.Longitude.Value
                );

                return Ok(new
                {
                    message = "Tính toán thành công! 📏",
                    from = new
                    {
                        address = fromAddress,
                        latitude = userCoords.Value.latitude,
                        longitude = userCoords.Value.longitude
                    },
                    to = new
                    {
                        stationId = station.StationId,
                        name = station.Name,
                        address = station.Address,
                        latitude = station.Latitude.Value,
                        longitude = station.Longitude.Value
                    },
                    distance = new
                    {
                        km = Math.Round(distanceKm, 2),
                        meters = Math.Round(distanceKm * 1000, 0),
                        readableDistance = distanceKm < 1 
                            ? $"{Math.Round(distanceKm * 1000, 0)} mét" 
                            : $"{Math.Round(distanceKm, 1)} km"
                    },
                    directions = new
                    {
                        googleMapsUrl = $"https://www.google.com/maps/dir/?api=1&origin={userCoords.Value.latitude},{userCoords.Value.longitude}&destination={station.Latitude},{station.Longitude}&travelmode=driving",
                        description = $"Khoảng {Math.Round(distanceKm, 1)}km từ vị trí của bạn đến {station.Name}"
                    }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Lỗi khi tính khoảng cách", error = ex.Message });
            }
        }
    }
}

