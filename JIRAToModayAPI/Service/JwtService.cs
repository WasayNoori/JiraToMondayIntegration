using JIRAToModayAPI.Models;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using JIRAToModayAPI.Interfaces;
namespace JIRAToModayAPI.Service
{


    public class JwtService: IJwtService
    {
        private readonly IConfiguration _configuration;
        private readonly List<ClientCredential> _trustedClients;

        public JwtService(IConfiguration configuration)
        {
            _configuration = configuration;
            // In a real application, you'd load this from a secure source
            // like Azure Key Vault or a database, not appsettings.json.
            _trustedClients = _configuration.GetSection("Authentication:Clients").Get<List<ClientCredential>>();
        }


        public string GenerateToken(string clientId)
        {
            // Find the client in your list of trusted clients
            var client = _trustedClients.FirstOrDefault(c => c.ClientId == clientId);
            if (client == null)
            {
                throw new UnauthorizedAccessException("Invalid client ID.");
            }

            // Create claims for the token.
            // These claims represent the client, not a human user.
            var claims = new[]
            {
            new Claim(JwtRegisteredClaimNames.Sub, clientId),
            new Claim("client_scope", "api_access") // Example of a custom claim
        };

            // Create the token
            var keyBytes = Convert.FromBase64String(_configuration["JwtConfig:Key"]);
            var key = new SymmetricSecurityKey(keyBytes);
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: _configuration["JwtConfig:Issuer"],
                audience: _configuration["JwtConfig:Audience"],
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(30), // Token lifetime
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}

