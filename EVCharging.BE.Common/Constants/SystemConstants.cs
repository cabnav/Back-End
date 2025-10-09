using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EVCharging.BE.Common.Constants
{
    public static class SystemConstants
    {
        public const decimal VAT_RATE = 0.1m; // 10%
        public const int SESSION_TIMEOUT_MINUTES = 15;
        public const int RESERVATION_HOLD_MINUTES = 15;
        public const decimal MAX_WALLET_BALANCE = 10000000; // 10 million
        public const decimal MIN_TOP_UP_AMOUNT = 50000; // 50k
    }
}
