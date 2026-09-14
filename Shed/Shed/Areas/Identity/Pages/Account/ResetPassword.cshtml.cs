// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
#nullable disable
using System.ComponentModel.DataAnnotations;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.AspNetCore.DataProtection.Extensions;

namespace Shed.Areas.Identity.Pages.Account
{
    [AllowAnonymous]
    public class ResetPasswordModel : PageModel
    {
        private readonly UserManager<IdentityUser> _userManager;
        private readonly ITimeLimitedDataProtector _protector;

        public ResetPasswordModel(UserManager<IdentityUser> userManager, IDataProtectionProvider dataProtection)
        {
            _userManager = userManager;
            _protector = dataProtection.CreateProtector("ResetPasswordProtector").ToTimeLimitedDataProtector();
        }

        [BindProperty]
        public InputModel Input { get; set; }

        [BindProperty]
        public string Code { get; set; }

        public class InputModel
        {
            [Required, StringLength(100, MinimumLength = 6)]
            [DataType(DataType.Password)]
            public string Password { get; set; }

            [DataType(DataType.Password)]
            [Compare("Password", ErrorMessage = "The password and confirmation password do not match.")]
            public string ConfirmPassword { get; set; }
        }

        public void OnGet(string code = null)
        {
            Code = code;
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid) return Page();

            if (string.IsNullOrEmpty(Code))
            {
                ModelState.AddModelError(string.Empty, "Invalid reset token.");
                return Page();
            }

            string protectedPayload;
            try
            {
                protectedPayload = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(Code));
            }
            catch
            {
                ModelState.AddModelError(string.Empty, "Invalid reset token.");
                return Page();
            }

            string combined;
            try
            {
                combined = _protector.Unprotect(protectedPayload, out _);
            }
            catch
            {
                ModelState.AddModelError(string.Empty, "Invalid or expired reset token.");
                return Page();
            }

            var parts = combined.Split('|', 2);
            if (parts.Length != 2)
            {
                ModelState.AddModelError(string.Empty, "Invalid reset token.");
                return Page();
            }

            var userId = parts[0];
            var token = parts[1];

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                // Don't reveal that the user does not exist
                return RedirectToPage("./ResetPasswordConfirmation");
            }

            var result = await _userManager.ResetPasswordAsync(user, token, Input.Password);
            if (result.Succeeded)
            {
                return RedirectToPage("./ResetPasswordConfirmation");
            }

            foreach (var error in result.Errors)
                ModelState.AddModelError(string.Empty, error.Description);

            return Page();
        }
    }
}


