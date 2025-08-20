using System.Text.Json.Serialization;

namespace JIRAToModayAPI.Models
{
    public class TokenResponse
    {
        [JsonPropertyName("token")]
        public string Token { get; set; }
    }
}
