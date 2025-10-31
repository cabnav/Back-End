using EVCharging.BE.Common.DTOs.Shared;
using System.Text.Json;
using System.Web;

namespace EVCharging.BE.Services.Services.Common.Implementations
{
    public class LocationService : ILocationService
    {
        private readonly HttpClient _httpClient;
        private const string NOMINATIM_API = "https://nominatim.openstreetmap.org/search";

        public LocationService(HttpClient httpClient)
        {
            _httpClient = httpClient;
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "EVCharging.BE.API/1.0");
        }

        /// <summary>
        /// Calculate distance between two coordinates using Haversine formula
        /// </summary>
        public double CalculateDistance(double lat1, double lon1, double lat2, double lon2)
        {
            const double R = 6371; // km
            var dLat = (lat2 - lat1) * Math.PI / 180;
            var dLon = (lon2 - lon1) * Math.PI / 180;

            var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                    Math.Cos(lat1 * Math.PI / 180) * Math.Cos(lat2 * Math.PI / 180) *
                    Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
            var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
            return R * c;
        }

        /// <summary>
        /// Convert address to coordinates using Nominatim (OpenStreetMap) API
        /// </summary>
        public async Task<GeocodingResponseDTO> GeocodeAddressAsync(GeocodingRequestDTO request)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.Address))
                {
                    return new GeocodingResponseDTO
                    {
                        Success = false,
                        ErrorMessage = "Address is required"
                    };
                }

                // Build query with country code for better accuracy
                var query = $"{request.Address}, {request.CountryCode}";
                var encodedQuery = HttpUtility.UrlEncode(query);
                var url = $"{NOMINATIM_API}?q={encodedQuery}&format=json&limit=1&accept-language={request.Language}";

                var response = await _httpClient.GetAsync(url);
                
                if (!response.IsSuccessStatusCode)
                {
                    return new GeocodingResponseDTO
                    {
                        OriginalAddress = request.Address,
                        Success = false,
                        ErrorMessage = $"Geocoding API error: {response.StatusCode}"
                    };
                }

                var content = await response.Content.ReadAsStringAsync();
                var results = JsonSerializer.Deserialize<List<NominatimResult>>(content);

                if (results == null || results.Count == 0)
                {
                    return new GeocodingResponseDTO
                    {
                        OriginalAddress = request.Address,
                        Success = false,
                        ErrorMessage = "No results found for the provided address"
                    };
                }

                var result = results[0];

                return new GeocodingResponseDTO
                {
                    OriginalAddress = request.Address,
                    FormattedAddress = result.display_name ?? request.Address,
                    Latitude = double.Parse(result.lat ?? "0"),
                    Longitude = double.Parse(result.lon ?? "0"),
                    Success = true,
                    Details = ExtractLocationDetails(result)
                };
            }
            catch (Exception ex)
            {
                return new GeocodingResponseDTO
                {
                    OriginalAddress = request.Address,
                    Success = false,
                    ErrorMessage = $"Geocoding failed: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// Convert address string to coordinates (simplified version)
        /// </summary>
        public async Task<(double latitude, double longitude)?> GetCoordinatesFromAddressAsync(string address, string countryCode = "VN")
        {
            var request = new GeocodingRequestDTO
            {
                Address = address,
                CountryCode = countryCode
            };

            var result = await GeocodeAddressAsync(request);

            if (result.Success)
            {
                return (result.Latitude, result.Longitude);
            }

            return null;
        }

        /// <summary>
        /// Extract location details from Nominatim result
        /// </summary>
        private LocationDetailsDTO ExtractLocationDetails(NominatimResult result)
        {
            return new LocationDetailsDTO
            {
                City = result.address?.city ?? result.address?.town ?? result.address?.village,
                District = result.address?.suburb ?? result.address?.district,
                Ward = result.address?.neighbourhood ?? result.address?.quarter,
                Street = result.address?.road,
                PostalCode = result.address?.postcode,
                Country = result.address?.country
            };
        }

        // Nominatim API Response Models
        private class NominatimResult
        {
            public string? lat { get; set; }
            public string? lon { get; set; }
            public string? display_name { get; set; }
            public NominatimAddress? address { get; set; }
        }

        private class NominatimAddress
        {
            public string? road { get; set; }
            public string? suburb { get; set; }
            public string? district { get; set; }
            public string? city { get; set; }
            public string? town { get; set; }
            public string? village { get; set; }
            public string? neighbourhood { get; set; }
            public string? quarter { get; set; }
            public string? postcode { get; set; }
            public string? country { get; set; }
        }
    }
}
