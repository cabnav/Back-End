using EVCharging.BE.Common.DTOs.Chat;

namespace EVCharging.BE.Services.Services.Chat
{
    public interface IChatService
    {
        Task<ChatResponse> GetAnswerAsync(string question);
        Task<SampleQuestionResponse> GetSampleQuestionsAsync();
    }
}

