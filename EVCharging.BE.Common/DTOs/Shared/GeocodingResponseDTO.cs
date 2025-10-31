namespace EVCharging.BE.Common.DTOs.Shared
{
    /// <summary>
    /// DTO for geocoding response - coordinates from address
    /// </summary>
    public class GeocodingResponseDTO
    {
        /// <summary>
        /// Original address from request
        /// </summary>
        public string OriginalAddress { get; set; } = string.Empty;

        /// <summary>
        /// Formatted address from geocoding service
        /// </summary>
        public string FormattedAddress { get; set; } = string.Empty;

        /// <summary>
        /// Latitude coordinate
        /// </summary>
        public double Latitude { get; set; }

        /// <summary>
        /// Longitude coordinate
        /// </summary>
        public double Longitude { get; set; }

        /// <summary>
        /// Indicates if geocoding was successful
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Error message if geocoding failed
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// Additional location details
        /// </summary>
        public LocationDetailsDTO? Details { get; set; }
    }

    /// <summary>
    /// Additional location details from geocoding
    /// </summary>
    public class LocationDetailsDTO
    {
        public string? City { get; set; }
        public string? District { get; set; }
        public string? Ward { get; set; }
        public string? Street { get; set; }
        public string? PostalCode { get; set; }
        public string? Country { get; set; }
    }
}

