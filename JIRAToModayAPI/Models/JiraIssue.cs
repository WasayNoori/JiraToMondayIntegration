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

        [JsonPropertyName("attachment")]
        public List<JiraAttachment> Attachments { get; set; } = new List<JiraAttachment>();
    }

    public class JiraAttachment
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("filename")]
        public string Filename { get; set; } = string.Empty;

        [JsonPropertyName("size")]
        public long Size { get; set; }

        [JsonPropertyName("mimeType")]
        public string MimeType { get; set; } = string.Empty;

        [JsonPropertyName("content")]
        public string Content { get; set; } = string.Empty;

        [JsonPropertyName("created")]
        public string Created { get; set; } = string.Empty;

        [JsonPropertyName("author")]
        public JiraUser Author { get; set; } = new JiraUser();
    }

    public class JiraUser
    {
        [JsonPropertyName("accountId")]
        public string AccountId { get; set; } = string.Empty;

        [JsonPropertyName("displayName")]
        public string DisplayName { get; set; } = string.Empty;

        [JsonPropertyName("emailAddress")]
        public string EmailAddress { get; set; } = string.Empty;
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