namespace EVCharging.BE.Services.Services.Charging
{
    /// <summary>
    /// Service kiểm tra tính tương thích giữa connector type của xe và điểm sạc
    /// </summary>
    public interface IConnectorCompatibilityService
    {
        /// <summary>
        /// Kiểm tra connector type của xe có tương thích với connector type của điểm sạc không
        /// </summary>
        /// <param name="vehicleConnectorType">Connector type của xe (ví dụ: "CCS2", "CHAdeMO", "Type2")</param>
        /// <param name="pointConnectorType">Connector type của điểm sạc</param>
        /// <returns>true nếu tương thích, false nếu không</returns>
        bool IsCompatible(string? vehicleConnectorType, string? pointConnectorType);

        /// <summary>
        /// Lấy danh sách connector types tương thích với connector type của xe
        /// </summary>
        /// <param name="vehicleConnectorType">Connector type của xe</param>
        /// <returns>Danh sách connector types tương thích</returns>
        List<string> GetCompatibleConnectorTypes(string? vehicleConnectorType);
    }
}

