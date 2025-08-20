namespace JIRAToModayAPI.Service
{
    public class JiraService
    {
        private readonly IConfiguration _configuration;
        private const string JiraBaseUrlKey = "Jira:BaseUrl";
      
        public JiraService(IConfiguration configuration)
        {
            _configuration = configuration;
        }
        public string GetJiraBaseUrl()
        {
            // Retrieve the Jira base URL from the configuration
            return _configuration["Jira:BaseUrl"];
        }
        public string GetJiraApiToken()
        {
            // Retrieve the Jira API token from the configuration
            return _configuration["Jira:ApiToken"];
        }
        public string GetJiraEmail()
        {
            // Retrieve the Jira email from the configuration
            return _configuration["Jira:Email"];
        }
    }
}
