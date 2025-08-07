
using System.Text.Json.Serialization;

namespace JIRAToModayAPI.Models
{
    public class CreateItemRequest
    {
        public long boardId { get; set; }
        public string groupId { get; set; }
        public string itemName { get; set; }
        
        public Dictionary<string, string> columnValues { get; set; }
        public List<JiraAttachment> attachments { get; set; } = new List<JiraAttachment>();

    }

    public class CreateItemResponse
    {
        [JsonPropertyName("success")]
        public bool Success { get; set; }
        
        [JsonPropertyName("message")]
        public string Message { get; set; }
        
        [JsonPropertyName("data")]
        public ItemData Data { get; set; }
    }

    public class ItemData
    {
        [JsonPropertyName("id")]
        public string Id { get; set; }
        
        [JsonPropertyName("name")]
        public string Name { get; set; }
        
        [JsonPropertyName("created_at")]
        public DateTime CreatedAt { get; set; }
    }

    
}
