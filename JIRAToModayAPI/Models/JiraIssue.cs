using System.Text.Json.Serialization;

namespace JIRAToModayAPI.Models
{
    public class JiraIssue
    {
        [JsonPropertyName("key")]
        public string Key { get; set; } = string.Empty;

        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("fields")]
        public JiraIssueFields Fields { get; set; } = new JiraIssueFields();
    }

    public class JiraIssueFields
    {
        [JsonPropertyName("summary")]
        public string Summary { get; set; } = string.Empty;

        [JsonPropertyName("description")]
        public string Description { get; set; } = string.Empty;

        [JsonPropertyName("created")]
        public string Created { get; set; } = string.Empty;

        [JsonPropertyName("updated")]
        public string Updated { get; set; } = string.Empty;
    }

    public class JiraSearchResponse
    {
        [JsonPropertyName("expand")]
        public string Expand { get; set; } = string.Empty;

        [JsonPropertyName("startAt")]
        public int StartAt { get; set; }

        [JsonPropertyName("maxResults")]
        public int MaxResults { get; set; }

        [JsonPropertyName("total")]
        public int Total { get; set; }

        [JsonPropertyName("issues")]
        public List<JiraIssue> Issues { get; set; } = new List<JiraIssue>();
    }
} 