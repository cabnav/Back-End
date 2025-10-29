using System;

namespace EVCharging.BE.Common.DTOs.Reservations
{
    /// <summary>
    /// Thông tin khung giờ có sẵn để đặt chỗ
    /// </summary>
    public class TimeSlotDTO
    {
        /// <summary>
        /// Giờ bắt đầu (0-23)
        /// </summary>
        public int Hour { get; set; }

        /// <summary>
        /// Thời gian bắt đầu (DateTime với giờ cụ thể)
        /// </summary>
        public DateTime StartTime { get; set; }

        /// <summary>
        /// Thời gian kết thúc (DateTime với giờ cụ thể)
        /// </summary>
        public DateTime EndTime { get; set; }

        /// <summary>
        /// Có sẵn để đặt chỗ không
        /// </summary>
        public bool IsAvailable { get; set; }

        /// <summary>
        /// Số điểm sạc còn trống trong khung giờ này
        /// </summary>
        public int AvailablePointsCount { get; set; }

        /// <summary>
        /// Hiển thị giờ theo format "HH:00 - HH:00"
        /// </summary>
        public string DisplayTime => $"{Hour:00}:00 - {(Hour + 1):00}:00";
    }
}
