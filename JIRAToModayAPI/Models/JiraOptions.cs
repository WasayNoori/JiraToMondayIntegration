namespace JIRAToModayAPI.Models
{
    public sealed class JiraOptions
    {
        public string Url { get; set; } = default!;
        public string Email { get; set; } = default!;
        public string ApiToken { get; set; } = default!;
    }
}
