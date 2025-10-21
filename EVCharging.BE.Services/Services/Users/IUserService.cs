using EVCharging.BE.Common.DTOs.Payments;
using EVCharging.BE.Common.DTOs.Users;
using EVCharging.BE.DAL.Entities;

namespace EVCharging.BE.Services.Services.Users
{
    public interface IUserService
    {
        Task<IEnumerable<User>> GetAllAsync();
        Task<User?> GetByIdAsync(int id);
        Task<User?> GetByEmailAsync(string email);
        Task<User> CreateAsync(User user);
        Task<bool> UpdateAsync(int id, User updated);
        Task<bool> DeleteAsync(int id);
        Task<WalletTransactionDTO> WalletTopUpAsync(int userId, decimal amount, string? description);
        Task<IEnumerable<WalletTransactionDTO>> GetWalletTransactionsAsync(int userId, int page = 1, int pageSize = 50);
        Task<bool> UpdateUserProfileAsync(int userId, UserUpdateRequest req);
    }
}

