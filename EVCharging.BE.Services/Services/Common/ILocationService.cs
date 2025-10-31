using EVCharging.BE.Common.DTOs.Shared;

namespace EVCharging.BE.Services.Services.Common
{
    public interface ILocationService
    {
        /// <summary>
        /// Calculate distance between two coordinates using Haversine formula
        /// </summary>
        double CalculateDistance(double lat1, double lon1, double lat2, double lon2);

        /// <summary>
        /// Convert address to coordinates (Geocoding)
        /// </summary>
        Task<GeocodingResponseDTO> GeocodeAddressAsync(GeocodingRequestDTO request);

        /// <summary>
        /// Convert address string to coordinates (simplified)
        /// </summary>
        Task<(double latitude, double longitude)?> GetCoordinatesFromAddressAsync(string address, string countryCode = "VN");
    }
}
