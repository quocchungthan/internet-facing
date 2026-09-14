using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Shed.Areas.Identity.Pages.Account
{
    [AllowAnonymous]
    public class RegisterConfirmationModel : PageModel
    {
        public string Email { get; private set; } = string.Empty;

        public void OnGet(string? email = null)
        {
            Email = email ?? string.Empty;
        }
    }
}


