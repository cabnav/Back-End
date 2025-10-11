using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EVCharging.BE.Common.DTOs.Auth
{
    public class ForgotPasswordRequest
    {
        /// <summary>
        /// Địa chỉ email của người dùng cần khôi phục mật khẩu.
        /// </summary>
        public string Email { get; set; } = string.Empty;
    }
}
