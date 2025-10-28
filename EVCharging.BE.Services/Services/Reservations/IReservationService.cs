using EVCharging.BE.Common.DTOs.Reservations;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace EVCharging.BE.Services.Services.Reservations
{
    public interface IReservationService
    {
        // 🟢 Tạo đặt chỗ mới
        Task<ReservationDTO> CreateReservationAsync(int userId, ReservationRequest request);

        // 🟢 Lấy danh sách đặt chỗ (có thể lọc)
        Task<IEnumerable<ReservationDTO>> GetReservationsAsync(ReservationFilter filter);

        // 🟢 Lấy các đặt chỗ sắp tới của tài xế
        Task<IEnumerable<ReservationDTO>> GetUpcomingReservationsAsync(int userId, TimeSpan horizon);

        // 🟠 Huỷ đặt chỗ
        Task<bool> CancelReservationAsync(int userId, int reservationId, string? reason = null);

        // 🟠 Huỷ đặt chỗ bằng mã
        Task<bool> CancelReservationByCodeAsync(int userId, string reservationCode, string? reason = null);

        // 🔍 Tra cứu đặt chỗ bằng mã
        Task<ReservationDTO?> GetReservationByCodeAsync(int userId, string reservationCode);
    }
}
