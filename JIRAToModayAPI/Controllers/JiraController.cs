using Microsoft.AspNetCore.Mvc;
using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using JIRAToModayAPI.Models;

namespace JIRAToModayAPI.Controllers
{
    public class JiraSearchRequest
    {
        public string Jql { get; set; } = "project = \"VPI\"";
        public int MaxResults { get; set; } = 50;
        public string[] Fields { get; set; } = new[] { "summary", "status", "assignee", "project", "created", "updated", "description" };
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

        private string ProcessJiraResponse(string response)
        {
            // Parse the JSON, process only description fields, then serialize back
            try
            {
                using var document = JsonDocument.Parse(response);
                var processedJson = ProcessJsonElement(document.RootElement);
                return JsonSerializer.Serialize(processedJson, new JsonSerializerOptions { WriteIndented = true });
            }
            catch
            {
                // If JSON parsing fails, return original response
                return response;
            }
        }

        private object ProcessJsonElement(JsonElement element)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.Object:
                    var obj = new Dictionary<string, object>();
                    foreach (var property in element.EnumerateObject())
                    {
                        if (property.Name == "description" && property.Value.ValueKind == JsonValueKind.String)
                        {
                            // Convert \n to actual newlines only in description fields
                            obj[property.Name] = property.Value.GetString().Replace("\\n", "\n");
                        }
                        else
                        {
                            obj[property.Name] = ProcessJsonElement(property.Value);
                        }
                    }
                    return obj;

                case JsonValueKind.Array:
                    var array = new List<object>();
                    foreach (var item in element.EnumerateArray())
                    {
                        array.Add(ProcessJsonElement(item));
                    }
                    return array;

                case JsonValueKind.String:
                    return element.GetString();

                case JsonValueKind.Number:
                    return element.GetDecimal();

                case JsonValueKind.True:
                    return true;

                case JsonValueKind.False:
                    return false;

                case JsonValueKind.Null:
                    return null;

                default:
                    return element.GetRawText();
            }
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
                            var processedResult = ProcessJiraResponse(result);
                            return Content(processedResult, "application/json");
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
                    Fields = new[] { "summary", "status", "assignee", "project", "created", "updated", "description" }
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
                            var processedResult = ProcessJiraResponse(result);
                            return Content(processedResult, "application/json");
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
                    fields = new[] { "summary", "status", "assignee", "project", "created", "updated", "priority", "issuetype", "description" }
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
                            var processedResult = ProcessJiraResponse(result);
                            return Content(processedResult, "application/json");
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

                return Ok(new
                {
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

        [HttpGet("issues/all-fields")]
        public async Task<IActionResult> GetJiraIssuesWithAllFields(string projectKey = null, string assignee = null, int maxResults = 50)
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

                // Request comprehensive set of fields including description
                var searchBody = new
                {
                    jql = jql,
                    maxResults = maxResults,
                    fields = new[] {
                        "summary", "status", "assignee", "project", "created", "updated",
                        "priority", "issuetype", "description", "labels", "components",
                        "fixVersions", "reporter", "resolution"
                    }
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
                            var processedResult = ProcessJiraResponse(result);
                            return Content(processedResult, "application/json");
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

        [HttpGet("issues/with-description")]
        public async Task<IActionResult> GetJiraIssuesWithDescription(string projectKey = null, string assignee = null, int maxResults = 50)
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

                // Simple approach - just add description to the working fields
                var searchBody = new
                {
                    jql = jql,
                    maxResults = maxResults,
                    fields = new[] {
                        "summary", "status", "assignee", "project", "created", "updated",
                        "priority", "issuetype", "description"
                    }
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
                            var processedResult = ProcessJiraResponse(result);
                            return Content(processedResult, "application/json");
                        }
                        else
                        {
                            var errorContent = await response.Content.ReadAsStringAsync();
                            Console.WriteLine($"API {version} failed: {response.StatusCode} - {errorContent}");
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Exception with API {version}: {ex.Message}");
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

        [HttpGet("issues/model")]
        public async Task<ActionResult<JiraSearchResponse>> GetJiraIssuesModel(string projectKey = null, string assignee = null, int maxResults = 50)
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

                // Simple approach - just add description to the working fields
                var searchBody = new
                {
                    jql = jql,
                    maxResults = maxResults,
                    fields = new[] {
                        "summary", "status", "assignee", "project", "created", "updated",
                        "priority", "issuetype", "description"
                    }
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
                            var processedResult = ProcessJiraResponse(result);
                            return Content(processedResult, "application/json");
                        }
                        else
                        {
                            var errorContent = await response.Content.ReadAsStringAsync();
                            Console.WriteLine($"API {version} failed: {response.StatusCode} - {errorContent}");
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Exception with API {version}: {ex.Message}");
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

        [HttpGet("issues/typed")]
        public async Task<ActionResult<JiraSearchResponse>> GetJiraIssuesTyped(string projectKey = null, string assignee = null, int maxResults = 50)
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

                // Request only the fields we need for our model
                var searchBody = new
                {
                    jql = jql,
                    maxResults = maxResults,
                    fields = new[] {
                        "summary", "description", "created", "updated", "attachment"
                    }
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
                            //var processedResult = ProcessJiraResponse(result);

                            // Deserialize the processed JSON into our model
                            var jiraResponse = JsonSerializer.Deserialize<JiraSearchResponse>(result);
                            return Ok(jiraResponse);
                        }
                        else
                        {
                            var errorContent = await response.Content.ReadAsStringAsync();
                            Console.WriteLine($"API {version} failed: {response.StatusCode} - {errorContent}");
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Exception with API {version}: {ex.Message}");
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
                        var processedResult = ProcessJiraResponse(result);
                        return Content(processedResult, "application/json");
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
