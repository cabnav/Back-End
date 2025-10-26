using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EVCharging.BE.Services.Services.Payment
{
    public interface IMockPayService
    {
        Task<(string Code, string QrBase64, DateTime ExpiresAt)> CreateTopUpAsync(int userId, decimal amount);
        Task<bool> ConfirmAsync(string code, bool success);
        Task<string?> GetStatusAsync(string code);
    }
}

