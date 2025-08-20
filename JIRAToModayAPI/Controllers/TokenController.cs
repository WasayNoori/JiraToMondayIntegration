using JIRAToModayAPI.Interfaces;
using Microsoft.AspNetCore.Mvc;
using JIRAToModayAPI.Models;
namespace JIRAToModayAPI.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class TokenController : ControllerBase
    {
        private readonly IJwtService _jwtService;

        public TokenController(IJwtService jwtService)
        {
            _jwtService = jwtService;
        }

        [HttpPost]
        public IActionResult GenerateToken([FromBody] ClientCredential credentials)
        {
            // This is a simplified check.
            // In a real app, you'd have logic to validate the ClientSecret as well.
            
            if (credentials.ClientId == "sampleAppID" && credentials.ClientSecret == "123456")
            {
                var token = _jwtService.GenerateToken(credentials.ClientId);
                return Ok(new { token });
            }

            return Unauthorized();
        }

    }
}
