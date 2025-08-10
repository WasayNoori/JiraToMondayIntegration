using Microsoft.AspNetCore.Mvc;
using JIRAToModayAPI.Models;
using JIRAToModayAPI.Monday;
using JIRAToModayAPI.DTO;
using JIRAToModayAPI.Storage;

namespace JIRAToModayAPI.Controllers
{ 

    [Route("api/[controller]")]
    [ApiController]
    public class MainController : Controller
    {
        private readonly ILogger<MainController> _logger;
        private readonly JiraMondayMappingService _mappingService;

        private readonly IConfiguration _configuration;
        public MainController(ILogger<MainController> logger, IConfiguration configuration,JiraMondayMappingService mappingservice)
        {
            _logger = logger;
            _configuration = configuration;
            _mappingService = mappingservice;
        }

        [HttpGet("TransferIssue")]
        public async Task<IActionResult> TransferIssuesToMonday()
        {
        string projectKey = "VPI";
        string assignee = "Daryl Speed";
        
        try
            {
                // Step 1: Get Jira issues using the typed endpoint
                var jiraController = new JiraController(_configuration);
                var jiraResult = await jiraController.GetJiraIssuesTyped(projectKey, assignee, 50);
                
                if (jiraResult.Result is not OkObjectResult okResult || okResult.Value is not JiraSearchResponse jiraResponse)
                {
                    return BadRequest("Failed to retrieve Jira issues");
                }

                // Step 2: Convert Jira issues to Monday.com requests
                var mondayRequests = MondayItemMapper.ToCreateItemRequest(jiraResponse);
                
                                 // Step 3: Create Monday.com client and process each request
                 var mondayApiToken = _configuration["MondayApiToken"]; // Add this to your appsettings
                 var mondayClient = new MondayAPIClient(mondayApiToken);
                var results = new List<object>();
                
                foreach (var request in mondayRequests)
                {

                    //check if this exists already
                    var existingId = await existingItemId(request.issueId);
                    if(!string.IsNullOrEmpty(existingId))
                    {
                        continue;                    }

                    try
                    {
                        var response = await mondayClient.CreateItemAsync(request);
                        var itemId = response.Data.Id;
                     await  _mappingService.AddMappingAsync(request.issueId, itemId);

                        results.Add(new
                        {
                            IssueKey = request.itemName,
                            Status = "Success"
                         
                        });

                                                 //now we can add the attachments to the created item
                         if (request.attachments != null && request.attachments.Any())
                         {
                             foreach (var attachment in request.attachments)
                             {
                                 try
                                 {
                                     // Download attachment from Jira
                                     var fileBytes = await DownloadJiraAttachmentAsync(attachment.Content);
                                     
                                     // Upload to Monday.com (you'll need to specify the correct column ID)
                                     var fileColumnId = "files"; // Replace with your actual file column ID
                                    var uploadResult = await mondayClient.UploadFileToMondayAsync(
                                        itemId,
                                        fileColumnId,
                                        fileBytes,
                                        attachment.Filename,
                                        attachment.MimeType
                                    );

                                    _logger.LogInformation($"Successfully uploaded attachment {attachment.Filename} to Monday item {itemId}");
                                 }
                                 catch (Exception attachmentEx)
                                 {
                                     _logger.LogError(attachmentEx, $"Failed to upload attachment {attachment.Filename} for item {request.itemName}");
                                     // Continue with other attachments even if one fails
                                 }
                             }
                         }
                    }
                    catch (Exception ex)
                    {
                        results.Add(new
                        {
                            IssueKey = request.itemName,
                            Status = "Failed",
                            Error = ex.Message
                        });
                    }
                }

                return Ok(new
                {
                    TotalIssues = jiraResponse.Issues.Count,
                    ProcessedRequests = mondayRequests.Count,
                    Results = results
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error transferring issues to Monday.com");
                return StatusCode(500, new { Error = ex.Message });
                         }
         }

     
        private async Task<string> existingItemId(string jiraId)
        {
            // Simulate a Monday ID to add if it's not found
            var result = await _mappingService.GetMondayItemIdAsync(jiraId);
            if (!string.IsNullOrEmpty(result))
            {
                return result;
            }
            else
            {

               return string.Empty;
            }
          
        }
        private async Task<byte[]> DownloadJiraAttachmentAsync(string contentUrl)
         {
             try
             {
                 // Get Jira credentials from configuration
                 var jiraUsername = _configuration["jirausername"];
                 var jiraToken = _configuration["jiratoken"];
                 
                 if (string.IsNullOrEmpty(jiraUsername) || string.IsNullOrEmpty(jiraToken))
                 {
                     throw new InvalidOperationException("Jira credentials not found in configuration");
                 }

                 using var client = new HttpClient();
                 var authToken = Convert.ToBase64String(System.Text.Encoding.ASCII.GetBytes($"{jiraUsername}:{jiraToken}"));
                 client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", authToken);

                 var response = await client.GetAsync(contentUrl);
                 
                 if (response.IsSuccessStatusCode)
                 {
                     return await response.Content.ReadAsByteArrayAsync();
                 }
                 else
                 {
                     throw new Exception($"Failed to download attachment from Jira: {response.StatusCode} - {response.ReasonPhrase}");
                 }
             }
             catch (Exception ex)
             {
                 _logger.LogError(ex, $"Error downloading attachment from URL: {contentUrl}");
                 throw;
             }
         }

     }
 }
