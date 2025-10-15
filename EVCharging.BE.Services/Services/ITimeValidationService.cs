using System;
using System.Threading.Tasks;

namespace EVCharging.BE.Services.Services
{
    public interface ITimeValidationService
    {
        /// <summary>
        /// Kiểm tra khung giờ có hợp lệ không (tránh trùng, tránh quá khứ, tránh ngoài giờ hoạt động)
        /// </summary>
        Task ValidateTimeSlotAsync(int pointId, DateTime startUtc, DateTime endUtc);
    }
}
