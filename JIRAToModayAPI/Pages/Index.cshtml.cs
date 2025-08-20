using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace JIRAToModayAPI.Pages
{
    public class IndexModel : PageModel
    {
        private readonly IConfiguration _configuration;

        public IndexModel(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public string KeyVaultStatus { get; set; }

        public void OnGet()
        {
            // Add dynamic logic here
            KeyVaultStatus = _configuration["KeyVault:VaultUrl"] != null ? "Connected" : "Not Configured";
        }
    }
}
