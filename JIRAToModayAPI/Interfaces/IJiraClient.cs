using JIRAToModayAPI.Controllers;
using JIRAToModayAPI.Models;
using System.Text.Json;
using static System.Net.WebRequestMethods;

namespace JIRAToModayAPI.Interfaces
{
    public interface IJiraClient
    {
        Task<JiraSearchResponse> SearchAsync(JiraSearchRequest req, CancellationToken ct = default);
        Task<JiraIssue?> GetIssueAsync(string issueIdOrKey, string? fields = null, CancellationToken ct = default);
    }
}
