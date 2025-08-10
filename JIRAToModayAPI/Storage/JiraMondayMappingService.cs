using Azure.Storage.Blobs;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace JIRAToModayAPI.Storage
{
    public class JiraMondayMappingService
    {
        private readonly string _connectionString;
        private readonly string _containerName;
        private readonly string _blobName;

        private Dictionary<string, string> _mappings;

        public JiraMondayMappingService(string connectionString, string containerName, string blobName = "jira-monday-map.json")
        {
            _connectionString = connectionString;
            _containerName = containerName;
            _blobName = blobName;
            _mappings = new Dictionary<string, string>();
        }
        public async Task<string> GetMondayItemIdAsync(string jiraIssueId)
        {
            try
            {
                await LoadMappingsAsync();
                if (_mappings.TryGetValue(jiraIssueId, out var mondayId))
                {
                    return mondayId;
                }
                return string.Empty; // Not found
            }
            catch (Exception)
            {

                return string.Empty;
            }
          
        }


        public async Task<bool> AddMappingAsync(string jiraIssueId, string mondayItemIdIfNew)
        {
            await LoadMappingsAsync();

            _mappings[jiraIssueId] = mondayItemIdIfNew;
            await SaveMappingsAsync();

            return true; // indicate it was new
        }

        private async Task LoadMappingsAsync()
        {
            var blobClient = new BlobClient(_connectionString, _containerName, _blobName);

            if (!await blobClient.ExistsAsync())
            {
                _mappings = new Dictionary<string,string>();
                return;
            }

            var downloadResult = await blobClient.DownloadContentAsync();
            if (downloadResult.Value.Content == null || downloadResult.Value.Content.ToStream().Length == 0)
            {
                _mappings = new Dictionary<string, string>();
                return;
            }

            try
            {
                var json = downloadResult.Value.Content.ToString();

                _mappings = JsonSerializer.Deserialize<Dictionary<string, string>>(json)
                            ?? new Dictionary<string, string>();
            }
            catch (Exception ex)
            {

                throw;
            }
          
        }

        private async Task SaveMappingsAsync()
        {
            var blobClient = new BlobClient(_connectionString, _containerName, _blobName);
            var json = JsonSerializer.Serialize(_mappings, new JsonSerializerOptions { WriteIndented = true });
            using var ms = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json));
            await blobClient.UploadAsync(ms, overwrite: true);
        }
    }
}
