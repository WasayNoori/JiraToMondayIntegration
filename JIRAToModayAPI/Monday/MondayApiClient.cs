using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using JIRAToModayAPI.Models;

namespace JIRAToModayAPI.Monday
{
    public class MondayAPIClient
    {

        private readonly HttpClient _httpClient;
        private readonly string _baseUrl = "https://spmondayapi.azurewebsites.net"; // Your Azure URL
        private readonly string _mondayApiToken;

        public MondayAPIClient(string mondayApiToken = null)
        {
            _httpClient = new HttpClient();
            _mondayApiToken = mondayApiToken;
            
            // Set Monday.com API token if provided
            if (!string.IsNullOrEmpty(_mondayApiToken))
            {
                _httpClient.DefaultRequestHeaders.Add("Authorization", _mondayApiToken);
            }
        }
        

        public async Task<CreateItemResponse> CreateItemAsync(CreateItemRequest request)
        {
            var json = JsonSerializer.Serialize(request);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync($"{_baseUrl}/api/create-item", content);

            if (response.IsSuccessStatusCode)
            {
                var responseContent = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<CreateItemResponse>(responseContent);
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                throw new Exception($"API call failed: {response.StatusCode} - {errorContent}");
            }
        }

        public async Task<string> UploadFileToMondayAsync(string itemId, string columnId, byte[] fileBytes, string fileName, string mimeType = "application/octet-stream")
        {
            try
            {
                // Create multipart form data for your proxy API
                using var form = new MultipartFormDataContent();
                
                // Add itemId as form field
                form.Add(new StringContent(itemId), "itemId");
                
                // Add columnId as form field  
                form.Add(new StringContent(columnId), "columnId");
                
                // Add the file
                var fileContent = new ByteArrayContent(fileBytes);
                fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(mimeType);
                form.Add(fileContent, "file", fileName);

                // Make the request to your proxy API
                var response = await _httpClient.PostAsync($"{_baseUrl}/api/upload-file", form);
                
                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    return responseContent;
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    throw new Exception($"Proxy API file upload failed: {response.StatusCode} - {errorContent}");
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Error uploading file via proxy API: {ex.Message}", ex);
            }
        }


    }
}

