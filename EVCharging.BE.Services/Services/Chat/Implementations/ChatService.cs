using EVCharging.BE.Common.DTOs.Chat;
using EVCharging.BE.Services.Services.Chat;
using System.Linq;

namespace EVCharging.BE.Services.Services.Chat.Implementations
{
    public class ChatService : IChatService
    {
        private readonly IChatDataService _dataService;
        private ChatQAData? _cachedData;
        private DateTime _lastCacheUpdate = DateTime.MinValue;
        private readonly TimeSpan _cacheExpiry = TimeSpan.FromMinutes(5); // Cache 5 phút

        public ChatService(IChatDataService dataService)
        {
            _dataService = dataService ?? throw new ArgumentNullException(nameof(dataService));
        }

        private async Task<ChatQAData> GetQADataAsync()
        {
            // Cache để tránh đọc file quá nhiều
            if (_cachedData == null || DateTime.UtcNow - _lastCacheUpdate > _cacheExpiry)
            {
                _cachedData = await _dataService.LoadQADataAsync();
                _lastCacheUpdate = DateTime.UtcNow;
            }
            return _cachedData;
        }

        public async Task<ChatResponse> GetAnswerAsync(string question)
        {
            var data = await GetQADataAsync();
            var activeQuestions = data.Questions.Where(q => q.IsActive).ToList();

            // Tìm câu trả lời chính xác
            var exactMatch = activeQuestions.FirstOrDefault(q => 
                q.Question.Equals(question, StringComparison.OrdinalIgnoreCase));

            if (exactMatch != null)
            {
                // Tăng usage count
                exactMatch.UsageCount++;
                exactMatch.UpdatedAt = DateTime.UtcNow;
                await _dataService.AddOrUpdateQAAsync(exactMatch);
                InvalidateCache();

                return new ChatResponse
                {
                    Answer = exactMatch.Answer,
                    IsFromSample = true
                };
            }

            // Tìm kiếm không phân biệt hoa thường, tìm câu hỏi chứa từ khóa
            var normalizedQuestion = question.Trim().ToLowerInvariant();
            
            // Tìm theo keyword matching
            var keywordMatch = activeQuestions
                .Where(q => q.Keywords.Any(kw => normalizedQuestion.Contains(kw.ToLowerInvariant())))
                .OrderByDescending(q => q.Keywords.Count(kw => normalizedQuestion.Contains(kw.ToLowerInvariant())))
                .FirstOrDefault();

            if (keywordMatch != null)
            {
                keywordMatch.UsageCount++;
                keywordMatch.UpdatedAt = DateTime.UtcNow;
                await _dataService.AddOrUpdateQAAsync(keywordMatch);
                InvalidateCache();

                return new ChatResponse
                {
                    Answer = keywordMatch.Answer,
                    IsFromSample = true
                };
            }

            // Tìm theo partial match trong question
            var partialMatch = activeQuestions.FirstOrDefault(q =>
                q.Question.ToLowerInvariant().Contains(normalizedQuestion) ||
                normalizedQuestion.Contains(q.Question.ToLowerInvariant()));

            if (partialMatch != null)
            {
                partialMatch.UsageCount++;
                partialMatch.UpdatedAt = DateTime.UtcNow;
                await _dataService.AddOrUpdateQAAsync(partialMatch);
                InvalidateCache();

                return new ChatResponse
                {
                    Answer = partialMatch.Answer,
                    IsFromSample = true
                };
            }

            // Nếu không tìm thấy, trả về câu trả lời mặc định
            return new ChatResponse
            {
                Answer = "Xin lỗi, tôi chưa hiểu câu hỏi của bạn. " +
                        "Vui lòng chọn một trong các câu hỏi mẫu bên dưới hoặc đặt câu hỏi rõ ràng hơn. " +
                        "Nếu cần hỗ trợ thêm, vui lòng liên hệ bộ phận hỗ trợ khách hàng qua email: support@evcharging.com hoặc hotline: 1900-xxxx.",
                IsFromSample = false
            };
        }

        public async Task<SampleQuestionResponse> GetSampleQuestionsAsync()
        {
            var data = await GetQADataAsync();
            var activeQuestions = data.Questions
                .Where(q => q.IsActive)
                .OrderByDescending(q => q.UsageCount)
                .ThenBy(q => q.Question)
                .ToList();

            var questions = activeQuestions.Select(q => new SampleQuestion
            {
                Question = q.Question,
                Answer = q.Answer
            }).ToList();

            return new SampleQuestionResponse
            {
                Questions = questions
            };
        }

        private void InvalidateCache()
        {
            _cachedData = null;
            _lastCacheUpdate = DateTime.MinValue;
        }
    }
}
