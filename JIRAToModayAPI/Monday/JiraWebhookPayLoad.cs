namespace JIRAToModayAPI.Monday
{
    
    public sealed class JiraWebhookPayload
    {
        public JiraIssue? Issue { get; set; }
    }

    public sealed class JiraIssue
    {
        public string? Id { get; set; }   // numeric string from Jira
        public string? Key { get; set; }  // e.g., "PROJ-123"
    }

}

