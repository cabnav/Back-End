using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EVCharging.BE.Common.Enums
{
    public enum NotificationType
    {
        ChargingComplete,
        ReservationReminder,
        Promotion,
        SystemAlert,
        PaymentSuccess,
        LowBalance
    }
}
