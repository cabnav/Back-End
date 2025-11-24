namespace EVCharging.BE.Common.DTOs.Chat
{
    public class SampleQuestionResponse
    {
        public List<SampleQuestion> Questions { get; set; } = new();
    }

    public class SampleQuestion
    {
        public string Question { get; set; } = string.Empty;
        public string Answer { get; set; } = string.Empty;
    }
}

