using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EVCharging.BE.Common.DTOs.Auth
{
    public class ResetPasswordRequest
    {
        /// <summary>
        /// Token hoặc mã xác nhận được gửi qua email.
        /// </summary>
        public string Token { get; set; } = string.Empty;

        /// <summary>
        /// Mật khẩu mới mà người dùng muốn đặt.
        /// </summary>
        public string NewPassword { get; set; } = string.Empty;

        /// <summary>
        /// Xác nhận lại mật khẩu mới để đảm bảo nhập đúng.
        /// </summary>
        public string ConfirmPassword { get; set; } = string.Empty;
    }
}

