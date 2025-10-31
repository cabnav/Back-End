namespace EVCharging.BE.Common.DTOs.Shared
{
    /// <summary>
    /// DTO for geocoding request - convert address to coordinates
    /// </summary>
    public class GeocodingRequestDTO
    {
        /// <summary>
        /// Address to convert to coordinates
        /// Example: "123 Nguyen Hue, District 1, Ho Chi Minh City"
        /// </summary>
        public string Address { get; set; } = string.Empty;

        /// <summary>
        /// Optional: Country code for better accuracy (default: VN)
        /// </summary>
        public string CountryCode { get; set; } = "VN";

        /// <summary>
        /// Optional: Language for results (default: vi)
        /// </summary>
        public string Language { get; set; } = "vi";
    }
}

