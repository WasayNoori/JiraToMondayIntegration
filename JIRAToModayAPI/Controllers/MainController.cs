using Azure.Core;
using JIRAToModayAPI.DTO;
using JIRAToModayAPI.Interfaces;
using JIRAToModayAPI.Models;
using JIRAToModayAPI.Monday;
using JIRAToModayAPI.Service;
using JIRAToModayAPI.Storage;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace JIRAToModayAPI.Controllers
{

    [Route("api/[controller]")]
    [ApiController]
    public class MainController : Controller
    {
        private readonly ILogger<MainController> _logger;
        private readonly JiraMondayMappingService _mappingService;
        private readonly IJiraClient _jiraClient;
        private readonly IConfiguration _configuration;
        public MainController(ILogger<MainController> logger, IConfiguration configuration, JiraMondayMappingService mappingservice,IJiraClient jiraClient)
        {
            _logger = logger;
            _configuration = configuration;
            _mappingService = mappingservice;
            _jiraClient = jiraClient;
        }

        [HttpGet("TransferIssues")]
        public async Task<IActionResult> TransferIssuesToMonday()
        {
            string projectKey = "VPI";
            string assignee = "Daryl Speed";

            try
            {
                // Step 1: Get Jira issues using the typed endpoint
                var jiraController = new JiraWebhookController(_configuration,_jiraClient);
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
                    if (!string.IsNullOrEmpty(existingId))
                    {
                        continue;
                    }

                    try
                    {
                        var response = await mondayClient.CreateItemAsync(request);
                        var itemId = response.Data.Id;
                        await _mappingService.AddMappingAsync(request.issueId, itemId);

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

        [HttpGet("TransferIssuesTest")]
        public async Task<IActionResult> TransferIssuesToMondayTest()
        {
            string projectKey = "VPI";
            string assignee = "Daryl Speed";


            // Step 1: Get Jira issues using the typed endpoint
            var jiraController = new JiraWebhookController(_configuration, _jiraClient);
            var jiraResult = await jiraController.GetJiraIssuesTyped(projectKey, assignee, 50);

            if (jiraResult.Result is not OkObjectResult okResult || okResult.Value is not JiraSearchResponse jiraResponse)
            {
                var (status, payload) = UnwrapResult(jiraResult.Result);

                return StatusCode(
                    status == StatusCodes.Status200OK ? StatusCodes.Status400BadRequest : status,
                    new
                    {
                        message = "Jira call failed",
                        detail = payload,                  // raw payload from inner result
                        traceId = HttpContext.TraceIdentifier
                    });
            }

            // Step 2: Convert Jira issues to Monday.com requests
            var mondayRequests = MondayItemMapper.ToCreateItemRequest(jiraResponse);

            // Step 3: Create Monday.com client and process each request
            var mondayApiToken = _configuration["MondayApiToken"]; // Add this to your appsettings
            var mondayClient = new MondayAPIClient(mondayApiToken);
            var results = new List<object>();
            if (mondayRequests == null || !mondayRequests.Any())
            {
                return Ok(new
                {
                    TotalIssues = jiraResponse.Issues.Count,
                    ProcessedRequests = 0,
                    Results = new List<object>()
                });
            }

            //select one which also has attachments if any of them have attachments
            var sampleRequest =
     mondayRequests.FirstOrDefault(r => r.attachments?.Any() == true)
     ?? mondayRequests.FirstOrDefault();






            try
            {
                if (sampleRequest == null)
                {
                    return BadRequest("No valid request found to process.");
                }
                var response = await mondayClient.CreateItemAsync(sampleRequest);
                var itemId = response.Data.Id;
                await _mappingService.AddMappingAsync(sampleRequest.issueId, itemId);
                foreach (var attachment in sampleRequest.attachments)
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
                        _logger.LogError(attachmentEx, $"Failed to upload attachment {attachment.Filename} for item {sampleRequest.itemName}");
                        // Continue with other attachments even if one fails
                    }
                }


                return Ok(new
                {
                    IssueKey = sampleRequest.itemName,
                    Status = "Success",
                    ItemId = itemId
                });

            }
            catch (Exception ex)
            {
                results.Add(new
                {
                    IssueKey = sampleRequest.itemName,
                    Status = "Failed",
                    Error = ex.Message
                });
            }
            return Ok(new
            {
                TotalIssues = jiraResponse.Issues.Count,
                ProcessedRequests = mondayRequests.Count,
                Results = results
            });
        }



        //this just gives details from the body..for troubleshooting.
        private static (int Status, object? Payload) UnwrapResult(IActionResult r) => r switch
        {
            ObjectResult o => (o.StatusCode ?? StatusCodes.Status400BadRequest, o.Value),
            ContentResult c => (c.StatusCode ?? StatusCodes.Status400BadRequest, c.Content),
            StatusCodeResult s => (s.StatusCode, null),
            _ => (StatusCodes.Status400BadRequest, r.ToString())
        };

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
                var jiraUsername = _configuration["Jira:Username"];
                var jiraToken = _configuration["JiraToken"];

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


        [Authorize]
        [HttpGet("testAuthorization")]
        public IActionResult TestAuthorization()
        {
            // This endpoint is just for testing if the authorization works
            // You can add any logic here to verify the JWT token or other auth mechanisms
            return Ok(new { Message = "Authorization is working!" });
        }

        [HttpGet("GetAndTestToken")]
        public async Task<IActionResult> GetAndTestToken()
        {
            //first get the token
            //call the TOKEN endpoint to get a token
            var clientId = "sampleAppID";
            var clientSecret = "12345";
            var url = "https://localhost:7298/token"; // Adjust the URL as needed
            using var client = new HttpClient();
            var requestBody = new
            {
                ClientId = clientId,
                ClientSecret = clientSecret
            };
            var json = System.Text.Json.JsonSerializer.Serialize(requestBody);
            var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
            var response = await client.PostAsync(url, content);
            if (response.IsSuccessStatusCode)
            {
                try
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    if (string.IsNullOrEmpty(responseContent))
                    {
                        return BadRequest("Failed to get a Token");
                    }
                    TokenResponse tokenResponse = JsonSerializer.Deserialize<TokenResponse>(responseContent);

                    var url2 = "https://localhost:7298/api/Main/testAuthorization"; // Adjust the URL as needed
                    using var client2 = new HttpClient();
                    client2.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", tokenResponse.Token);
                    var response2 = await client2.GetAsync(url2);
                    if (response2.IsSuccessStatusCode)
                    {
                        var content2 = await response2.Content.ReadAsStringAsync();
                        return Ok("Working");
                    }
                }
                catch (Exception)
                {

                   return BadRequest("Failed to deserialize the token response");
                }
              
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                return StatusCode((int)response.StatusCode, new { Error = errorContent });
            }

            return BadRequest("Failed to get a Token");

        }

        //[HttpPost("updatemondayitem")]
        //public async Task<IActionResult> UpdateMondayItem([FromBody] UpdateItemRequest updateRequest, [FromBody] string itemId)
        //{
            
        //    try
        //    {
        //         UpdateItemRequest updateItem=MondayItemMapper.ToUpdateItemRequest(updateRequest,itemId);
        //    }
        //    catch (Exception ex)
        //    {
               
        //    }
        //}





    }
}
