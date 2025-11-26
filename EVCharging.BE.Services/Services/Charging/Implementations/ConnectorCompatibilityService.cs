using EVCharging.BE.Services.Services.Charging;
using System;

namespace EVCharging.BE.Services.Services.Charging.Implementations
{
    /// <summary>
    /// Service kiểm tra tính tương thích giữa connector type của xe và điểm sạc
    /// Logic: Cổng sạc xe phải Y CHANG (exact match) với điểm sạc mới sạc được
    /// </summary>
    public class ConnectorCompatibilityService : IConnectorCompatibilityService
    {
        public bool IsCompatible(string? vehicleConnectorType, string? pointConnectorType)
        {
            // ✅ Strict validation: Không cho phép nếu connector type null
            // Yêu cầu user phải cấu hình connector type trước khi sạc để đảm bảo an toàn
            
            // Nếu điểm sạc không có connector type, không cho phép
            if (string.IsNullOrWhiteSpace(pointConnectorType))
            {
                return false; // Điểm sạc phải có connector type
            }
            
            // Nếu xe chưa cấu hình connector type, không cho phép
            if (string.IsNullOrWhiteSpace(vehicleConnectorType))
            {
                return false; // User phải cấu hình connector type cho xe
            }

            // ✅ EXACT MATCH ONLY: Cổng sạc xe phải Y CHANG với điểm sạc (case-insensitive)
            var vehicleType = vehicleConnectorType.Trim();
            var pointType = pointConnectorType.Trim();

            return vehicleType.Equals(pointType, StringComparison.OrdinalIgnoreCase);
        }

        public List<string> GetCompatibleConnectorTypes(string? vehicleConnectorType)
        {
            if (string.IsNullOrWhiteSpace(vehicleConnectorType))
                return new List<string>();

            // ✅ Chỉ trả về chính connector type của xe (exact match only)
            return new List<string> { vehicleConnectorType.Trim() };
        }
    }
}

