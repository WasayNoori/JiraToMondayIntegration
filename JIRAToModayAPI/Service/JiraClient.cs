using JIRAToModayAPI.Controllers;
using JIRAToModayAPI.Interfaces;
using JIRAToModayAPI.Models;
using JIRAToModayAPI.Storage;
using Microsoft.AspNetCore.Http.HttpResults;
using System.Text.Json;

namespace JIRAToModayAPI.Service
{
    public sealed class JiraClient : IJiraClient
    {
        private readonly HttpClient _http;
        private readonly ILogger<JiraClient> _logger;       
        private readonly JiraMondayMappingService _mappingService;
        private static readonly JsonSerializerOptions JsonOpts = new()
        {
            PropertyNameCaseInsensitive = true
        };

        public JiraClient(HttpClient http,
            JiraMondayMappingService mappingService,
            ILogger<JiraClient> logger)
        {
            _http = http;
            _mappingService = mappingService;
        }
      

        public async Task<JiraSearchResponse> SearchAsync(JiraSearchRequest req, CancellationToken ct = default)
        {
            var resp = await _http.PostAsJsonAsync("/rest/api/3/search", req, ct);
            resp.EnsureSuccessStatusCode();
            return (await resp.Content.ReadFromJsonAsync<JiraSearchResponse>(JsonOpts, ct))!;
        }

        public async Task<JiraIssue?> GetIssueAsync(string issueIdOrKey, string? fields = null, CancellationToken ct = default)
        {
            // keep payload small; pass only what you need
            var query = fields is null ? "" : $"?fields={Uri.EscapeDataString(fields)}";
            var url = $"/rest/api/3/issue/{Uri.EscapeDataString(issueIdOrKey)}{query}";

            using var resp = await _http.GetAsync(url, ct);
            if (resp.StatusCode == System.Net.HttpStatusCode.NotFound) return null;

            var json = await resp.Content.ReadAsStringAsync(ct);

            try
            {
                var issue = JsonSerializer.Deserialize<JiraIssue>(json, JsonOpts);
                return issue;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to deserialize Jira issue response for issue {IssueIdOrKey}", issueIdOrKey);
                return null;
            }
          


          
        }
    }

}
