using EVCharging.BE.Common.DTOs.Chat;

namespace EVCharging.BE.Services.Services.Chat
{
    public interface IChatDataService
    {
        Task<ChatQAData> LoadQADataAsync();
        Task SaveQADataAsync(ChatQAData data);
        Task AddOrUpdateQAAsync(ChatQAItem item);
        Task<bool> DeleteQAAsync(string id);
        Task<ChatQAItem?> GetQAByIdAsync(string id);
    }
}

