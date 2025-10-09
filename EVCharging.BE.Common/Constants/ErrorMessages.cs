using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EVCharging.BE.Common.Constants
{
    public static class ErrorMessages
    {
        public const string USER_NOT_FOUND = "User not found";
        public const string INVALID_CREDENTIALS = "Invalid email or password";
        public const string EMAIL_EXISTS = "Email already exists";
        public const string STATION_NOT_FOUND = "Charging station not found";
        public const string POINT_NOT_FOUND = "Charging point not found";
        public const string POINT_UNAVAILABLE = "Charging point is not available";
        public const string INSUFFICIENT_BALANCE = "Insufficient wallet balance";
        public const string SESSION_NOT_FOUND = "Charging session not found";
        public const string INVALID_QR_CODE = "Invalid QR code";
        public const string RESERVATION_CONFLICT = "Time slot is already reserved";
    }
}
