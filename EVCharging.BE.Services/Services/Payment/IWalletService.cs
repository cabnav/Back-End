using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace EVCharging.BE.Services.Services.Payment
{
    public interface IWalletService
    {
        Task<decimal> GetBalanceAsync(int userId);

        Task<(IEnumerable<EVCharging.BE.DAL.Entities.WalletTransaction> Items, int Total)>
            GetTransactionsAsync(int userId, int skip = 0, int take = 20,
                                 DateTime? from = null, DateTime? to = null, string? type = null);

        Task CreditAsync(int userId, decimal amount, string description, int? referenceId = null);
        Task DebitAsync(int userId, decimal amount, string description, int? referenceId = null);
    }
}
