using JIRAToModayAPI.DTO;
using JIRAToModayAPI.Interfaces;
using JIRAToModayAPI.Models;
using JIRAToModayAPI.Monday;
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
        public string[] Fields { get; set; } = new[] { "summary", "status", "assignee", "project", "created", "updated", "description" };
    }

    [Route("api/[controller]")]
    [ApiController]
    public class JiraWebhookController : Controller
    {
        private readonly IConfiguration _configuration;
        private readonly string _jiraUrl;
        private readonly string _jiraUsername;
        private readonly string _jiraToken;
        private readonly string _jiraApiVersion = "3"; // default to latest known version
        private readonly IJiraClient _jiraClient;
        public JiraWebhookController(IConfiguration configuration,IJiraClient jiraClient)
        {
            _configuration = configuration;
            _jiraUrl = configuration["Jira:Url"] ?? throw new InvalidOperationException("Jira URL not found in configuration");
            _jiraUsername = configuration["Jira:Username"] ?? throw new InvalidOperationException("Jira username not found in configuration");
            _jiraToken = configuration["JiraToken"] ?? throw new InvalidOperationException("Jira token not found in configuration");
            _jiraApiVersion = configuration["Jira:ApiVersion"] ?? "3"; // default to 3 if not set
            _jiraClient = jiraClient ?? throw new ArgumentNullException(nameof(jiraClient), "Jira client cannot be null");
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


        [HttpGet("statuscheck")]
        public  IActionResult StatusCheck()
        {
            try
            {
                // helpers (local to this endpoint)
                string? Mask(string? s) =>
                    string.IsNullOrEmpty(s) ? null : (s!.Length <= 2 ? s + "***" : s[..2] + "***");

                string? MaskBegning(string? s) =>
                    string.IsNullOrEmpty(s) ? null : (s!.Length <= 2 ? s + "***" : "***" + s[^2..]);

                string? Get(params string[] keys)
                {
                    foreach (var k in keys)
                    {
                        var v = _configuration[k];
                        if (!string.IsNullOrEmpty(v)) return v;
                    }
                    return null;
                }

                var blobContainer = Get("AzureBlob:ContainerName", "BlobContainerName");
                var blobConn = Get("AzureBlob:ConnectionString", "BlobConnectionString");
                var appName = Get("AppName", "App:Name"); // sentinel in Key Vault if you added one

                return Ok(new
                {
                    appName,                      // safe to show
                    jira = new
                    {
                        url = _jiraUrl,           // safe to show
                        username = _jiraUsername, // safe to show
                        tokenPreview = MaskBegning(_jiraToken)
                    },
                    blob = new
                    {
                        containerName = blobContainer,               // safe to show
                        connectionStringPreview = Mask(blobConn)     // masked
                    }
                });
            }
            catch (Exception ex)
            {
               
                return StatusCode(500, new { error = ex.Message });
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

     
      

        [HttpGet("issues/typed")]
        public async Task<ActionResult<JiraSearchResponse>> GetJiraIssuesTyped(string projectKey = null, string assignee = null, int maxResults = 50)
        {
            try
            {
                var client = new HttpClient();
                var authToken = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{_jiraUsername}:{_jiraToken}"));
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", authToken);

                // Build JQL query
                var project = string.IsNullOrWhiteSpace(projectKey) ? "VPI" : projectKey;
                var jql = $"project = \"{project}\"";
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

             
                var errorDetails = new List<object>();

              
                    try
                    {
                        var url = $"{_jiraUrl}/rest/api/{_jiraApiVersion}/search";
                        var json = JsonSerializer.Serialize(searchBody);
                        var content = new StringContent(json, Encoding.UTF8, "application/json");

                        var response = await client.PostAsync(url, content);
                        var body = await response.Content.ReadAsStringAsync();
                        Console.WriteLine(body); // or log it
                                                 // Typical: {"errorMessages":["The value '...' is invalid"],"errors":{"jql":"..."}}

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
                           
                            errorDetails.Add(new
                            {
                                ApiVersionTried = _jiraApiVersion,
                                StatusCode = (int)response.StatusCode,
                                Error = errorContent
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Exception with API {_jiraApiVersion}: {ex.Message}");
                        // Continue to next version
                    }
                

                return StatusCode(500, new
                {
                    Error = "Failed to retrieve issues from both API versions",
                    Details = errorDetails
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Error = ex.Message });
            }
        }

        [HttpGet("issues/testClient")]
        public async Task<ActionResult<JiraSearchResponse>>GetJiraIssueFields(string jiraId)
        {
            var jiraissue= await _jiraClient.GetIssueAsync(jiraId, "summary,description,created,updated,attachment");
            var mondayRequests = MondayItemMapper.ToCreateItemRequest(jiraissue);
            if (jiraissue == null)
            {
                return NotFound(new { Error = $"Jira issue with ID {jiraId} not found." });
            }
            return Ok(jiraissue);
        }
        
        [HttpGet("issues/testissues")]
        public async Task<ActionResult<JiraSearchResponse>> GetJiraIssuesTest()
        {
            try
            {
                var client = new HttpClient();
                var authToken = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{_jiraUsername}:{_jiraToken}"));
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", authToken);

                // Build JQL query
                // Request only the fields we need for our model
                // Build a simple JQL
                var jql = "ORDER BY created DESC";   // no project filter

                // Build the search body
                var searchBody = new
                {
                    jql = jql,
                    maxResults = 5,
                    fields = new[]
                    {
        "summary", "description", "created", "updated", "attachment"
    }
                };


            
                var errorDetails = new List<object>();

               
                    try
                    {
                        var url = $"{_jiraUrl}/rest/api/3/search";
                        var json = JsonSerializer.Serialize(searchBody);
                        var content = new StringContent(json, Encoding.UTF8, "application/json");

                        var response = await client.PostAsync(url, content);
                        var body = await response.Content.ReadAsStringAsync();
                        Console.WriteLine(body); // or log it
                                                 // Typical: {"errorMessages":["The value '...' is invalid"],"errors":{"jql":"..."}}

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
                            Console.WriteLine($"A failed: {response.StatusCode} - {errorContent}");
                            errorDetails.Add(new
                            {
                               
                                StatusCode = (int)response.StatusCode,
                                Error = errorContent
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Exception with API: {ex.Message}");
                        // Continue to next version
                    }
                

                return StatusCode(500, new
                {
                    Error = "Failed to retrieve issues from both API versions",
                    Details = errorDetails
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Error = ex.Message });
            }
        }


        [HttpGet("issues/whoami")]
        public async Task<ActionResult<JiraSearchResponse>> WhoamI()
        {
            
            try
            {
                var client = new HttpClient();
                var authToken = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{_jiraUsername}:{_jiraToken}"));
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", authToken);

               

                var errorDetails = new List<object>();


                try
                {

                    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                    // Call /myself
                    var url = $"https://solidprofessor.atlassian.net/rest/api/2/myself";
                    var response = await client.GetAsync(url);

                    Console.WriteLine($"Status: {response.StatusCode}");
                    var body = await response.Content.ReadAsStringAsync();
                    Console.WriteLine("Response body:");
                    Console.WriteLine(body);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Exception with API: {ex.Message}");
                    // Continue to next version
                }


                return StatusCode(500, new
                {
                    Error = "Failed to retrieve issues from both API versions",
                    Details = errorDetails
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Error = ex.Message });
            }
        }

        

        
    }
}
