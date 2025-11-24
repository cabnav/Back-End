using System.Text.Json.Serialization;

namespace EVCharging.BE.Common.DTOs.Chat
{
    public class ChatQAData
    {
        [JsonPropertyName("version")]
        public string Version { get; set; } = "1.0";

        [JsonPropertyName("lastUpdated")]
        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;

        [JsonPropertyName("questions")]
        public List<ChatQAItem> Questions { get; set; } = new();
    }

    public class ChatQAItem
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        [JsonPropertyName("question")]
        public string Question { get; set; } = string.Empty;

        [JsonPropertyName("answer")]
        public string Answer { get; set; } = string.Empty;

        [JsonPropertyName("keywords")]
        public List<string> Keywords { get; set; } = new();

        [JsonPropertyName("category")]
        public string Category { get; set; } = "general";

        [JsonPropertyName("isActive")]
        public bool IsActive { get; set; } = true;

        [JsonPropertyName("createdAt")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [JsonPropertyName("updatedAt")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        [JsonPropertyName("usageCount")]
        public int UsageCount { get; set; } = 0;
    }
}

