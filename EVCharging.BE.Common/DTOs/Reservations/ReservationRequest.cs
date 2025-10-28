﻿using EVCharging.BE.Common.DTOs.Stations;
using EVCharging.BE.Common.DTOs.Users;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EVCharging.BE.Common.DTOs.Reservations
{
    public class ReservationRequest
    {
        /// <summary>
        /// ID điểm sạc được chọn
        /// </summary>
        public int PointId { get; set; }

        /// <summary>
        /// Ngày đặt chỗ (chỉ ngày, không có giờ)
        /// </summary>
        public DateTime Date { get; set; }

        /// <summary>
        /// Giờ bắt đầu (0-23)
        /// </summary>
        public int Hour { get; set; }

        /// <summary>
        /// Thời gian bắt đầu được tính tự động từ Date + Hour
        /// </summary>
        public DateTime StartTime => Date.Date.AddHours(Hour);

        /// <summary>
        /// Thời gian kết thúc được tính tự động (1 tiếng sau StartTime)
        /// </summary>
        public DateTime EndTime => StartTime.AddHours(1);

        // Giữ lại các thuộc tính cũ để tương thích ngược
        [Obsolete("Use Date and Hour instead")]
        public DateTime? LegacyStartTime { get; set; }
        
        [Obsolete("Duration is fixed to 1 hour")]
        public int DurationMinutes { get; set; } = 60;
    }
}
