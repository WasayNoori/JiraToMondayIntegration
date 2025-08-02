using Microsoft.AspNetCore.Mvc;
using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace JIRAToModayAPI.Controllers
{
    public class JiraSearchRequest
    {
        public string Jql { get; set; } = "project = \"VPI\"";
        public int MaxResults { get; set; } = 50;
        public string[] Fields { get; set; } = new[] { "summary", "status", "assignee", "project", "created", "updated" };
    }

    [Route("api/[controller]")]
    [ApiController]
    public class JiraController : Controller
    {
        private readonly IConfiguration _configuration;
        private readonly string _jiraUrl;
        private readonly string _jiraUsername;
        private readonly string _jiraToken;

        public JiraController(IConfiguration configuration)
        {
            _configuration = configuration;
            _jiraUrl = configuration["jiraurl"] ?? throw new InvalidOperationException("Jira URL not found in configuration");
            _jiraUsername = configuration["jirausername"] ?? throw new InvalidOperationException("Jira username not found in configuration");
            _jiraToken = configuration["jiratoken"] ?? throw new InvalidOperationException("Jira token not found in configuration");
        }




        [HttpGet("projects")]
        public async Task<IActionResult> GetJiraProjects()
        {
            try
            {
                var client = new HttpClient();
                var authToken = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{_jiraUsername}:{_jiraToken}"));
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", authToken);

                // Try both API versions for projects
                var apiVersions = new[] { "2", "3" };
                
                foreach (var version in apiVersions)
                {
                    try
                    {
                        var url = $"{_jiraUrl}/rest/api/{version}/project";
                        var response = await client.GetAsync(url);
                        
                        if (response.IsSuccessStatusCode)
                        {
                            var result = await response.Content.ReadAsStringAsync();
                            return Content(result, "application/json");
                        }
                    }
                    catch (Exception ex)
                    {
                        // Continue to next version
                    }
                }
                
                return StatusCode(500, "Failed to retrieve projects from both API versions");
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Error = ex.Message });
            }
        }

        [HttpPost("search")]
        public async Task<IActionResult> SearchJiraIssues([FromBody] JiraSearchRequest request)
        {
            try
            {
                var client = new HttpClient();
                var authToken = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{_jiraUsername}:{_jiraToken}"));
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", authToken);

                // Use default search if no request provided
                var searchBody = request ?? new JiraSearchRequest
                {
                    Jql = "project = \"VPI\"",
                    MaxResults = 50,
                    Fields = new[] { "summary", "status", "assignee", "project", "created", "updated" }
                };

                var apiVersions = new[] { "2", "3" };
                
                foreach (var version in apiVersions)
                {
                    try
                    {
                        var url = $"{_jiraUrl}/rest/api/{version}/search";
                        var json = JsonSerializer.Serialize(searchBody);
                        var content = new StringContent(json, Encoding.UTF8, "application/json");

                        var response = await client.PostAsync(url, content);
                        
                        if (response.IsSuccessStatusCode)
                        {
                            var result = await response.Content.ReadAsStringAsync();
                            return Content(result, "application/json");
                        }
                    }
                    catch (Exception ex)
                    {
                        // Continue to next version
                    }
                }
                
                return StatusCode(500, "Failed to search issues from both API versions");
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Error = ex.Message });
            }
        }

        [HttpGet("issues")]
        public async Task<IActionResult> GetJiraIssues(string projectKey = null, string assignee = null, int maxResults = 50)
        {
            try
            {
                var client = new HttpClient();
                var authToken = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{_jiraUsername}:{_jiraToken}"));
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", authToken);

                // Build JQL query
                var jql = "project = \"VPI\"";
                if (!string.IsNullOrEmpty(assignee))
                {
                    jql += $" AND assignee = \"{assignee}\"";
                }

                var searchBody = new
                {
                    jql = jql,
                    maxResults = maxResults,
                    fields = new[] { "summary", "status", "assignee", "project", "created", "updated", "priority", "issuetype" }
                };

                var apiVersions = new[] { "2", "3" };
                
                foreach (var version in apiVersions)
                {
                    try
                    {
                        var url = $"{_jiraUrl}/rest/api/{version}/search";
                        var json = JsonSerializer.Serialize(searchBody);
                        var content = new StringContent(json, Encoding.UTF8, "application/json");

                        var response = await client.PostAsync(url, content);
                        
                        if (response.IsSuccessStatusCode)
                        {
                            var result = await response.Content.ReadAsStringAsync();
                            return Content(result, "application/json");
                        }
                    }
                    catch (Exception ex)
                    {
                        // Continue to next version
                    }
                }
                
                return StatusCode(500, "Failed to retrieve issues from both API versions");
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Error = ex.Message });
            }
        }

        [HttpGet("test-connection")]
        public async Task<IActionResult> TestConnection()
        {
            try
            {
                var client = new HttpClient();
                
                // Debug authentication info (without exposing the full token)
                var authInfo = new
                {
                    Username = _jiraUsername,
                    TokenLength = _jiraToken?.Length ?? 0,
                    TokenPreview = _jiraToken?.Substring(0, Math.Min(10, _jiraToken.Length)) + "..." ?? "No token",
                    JiraUrl = _jiraUrl
                };
                
                // Test different API versions
                var apiVersions = new[] { "2", "3" };
                var results = new List<object>();
                
                foreach (var version in apiVersions)
                {
                    try
                    {
                        // Clear headers for each request
                        client.DefaultRequestHeaders.Clear();
                        
                        var authToken = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{_jiraUsername}:{_jiraToken}"));
                        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", authToken);
                        client.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
                        
                        var url = $"{_jiraUrl}/rest/api/{version}/myself";
                        var response = await client.GetAsync(url);
                        
                        var responseContent = await response.Content.ReadAsStringAsync();
                        
                        results.Add(new
                        {
                            ApiVersion = version,
                            StatusCode = (int)response.StatusCode,
                            IsSuccess = response.IsSuccessStatusCode,
                            Content = responseContent,
                            Headers = response.Headers.ToDictionary(h => h.Key, h => h.Value)
                        });
                    }
                    catch (Exception ex)
                    {
                        results.Add(new
                        {
                            ApiVersion = version,
                            Error = ex.Message,
                            StackTrace = ex.StackTrace
                        });
                    }
                }
                
                return Ok(new { 
                    AuthInfo = authInfo,
                    Results = results 
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Error = ex.Message, StackTrace = ex.StackTrace });
            }
        }

        [HttpGet("test-auth")]
        public IActionResult TestAuth()
        {
            try
            {
                var authString = $"{_jiraUsername}:{_jiraToken}";
                var authToken = Convert.ToBase64String(Encoding.ASCII.GetBytes(authString));
                
                return Ok(new
                {
                    Username = _jiraUsername,
                    TokenLength = _jiraToken?.Length ?? 0,
                    AuthString = authString,
                    Base64Token = authToken,
                    FullAuthHeader = $"Basic {authToken}"
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Error = ex.Message });
            }
        }

        [HttpGet("issue")]
        public async Task<IActionResult> GetJiraIssue(string issueKey)
        {
            if (string.IsNullOrEmpty(issueKey))
                return BadRequest("Issue key is required");
                
            try
            {
                var client = new HttpClient();
                
                // Try API version 2 first (more commonly supported)
                var authToken = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{_jiraUsername}:{_jiraToken}"));
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", authToken);
                
                // Try both API versions
                var apiVersions = new[] { "2", "3" };
                
                foreach (var version in apiVersions)
                {
                    var url = $"{_jiraUrl}/rest/api/{version}/issue/{issueKey}";
                    var response = await client.GetAsync(url);
                    
                    if (response.IsSuccessStatusCode)
                    {
                        var result = await response.Content.ReadAsStringAsync();
                        return Content(result, "application/json");
                    }
                }
                
                // If both versions fail, return the last error
                var lastUrl = $"{_jiraUrl}/rest/api/3/issue/{issueKey}";
                var lastResponse = await client.GetAsync(lastUrl);
                return StatusCode((int)lastResponse.StatusCode, await lastResponse.Content.ReadAsStringAsync());
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Error = ex.Message });
            }
        }
    }
}
