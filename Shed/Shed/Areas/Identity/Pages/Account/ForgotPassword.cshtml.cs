// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
#nullable disable
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.Extensions;
using Shed.Services;

namespace Shed.Areas.Identity.Pages.Account
{
    [AllowAnonymous]
    public class ForgotPasswordModel : PageModel
    {
        private readonly UserManager<IdentityUser> _userManager;
        private readonly IEmailSender _emailSender;
        private readonly IWebHostEnvironment _env;
        private readonly ITimeLimitedDataProtector _protector;

        public ForgotPasswordModel(UserManager<IdentityUser> userManager, IEmailSender emailSender, IWebHostEnvironment env, IDataProtectionProvider dataProtection)
        {
            _userManager = userManager;
            _emailSender = emailSender;
            _env = env;
            _protector = dataProtection.CreateProtector("ResetPasswordProtector").ToTimeLimitedDataProtector();
        }

        [BindProperty]
        public InputModel Input { get; set; }

        public class InputModel
        {
            [Required, EmailAddress]
            public string Email { get; set; }
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid) return Page();

            var user = await _userManager.FindByEmailAsync(Input.Email);
            // Don't reveal that the user does not exist or is not confirmed
            if (user == null || !await _userManager.IsEmailConfirmedAsync(user))
                return RedirectToPage("./ForgotPasswordConfirmation");

            var token = await _userManager.GeneratePasswordResetTokenAsync(user);

            // Protect userId + token together so the link does not expose the user id directly
            var combined = user.Id + "|" + token;
            var protectedPayload = _protector.Protect(combined, TimeSpan.FromHours(2));
            var encoded = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(protectedPayload));

            var callbackUrl = Url.Page(
                "/Account/ResetPassword",
                pageHandler: null,
                values: new { area = "Identity", code = encoded },
                protocol: Request.Scheme);

            // Load reset-password template and inline site CSS
            var webroot = _env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
            var cssPath = Path.Combine(webroot, "css", "site.css");
            var emailTemplatePath = Path.Combine(webroot, "emails", "reset-password.html");
            var cssContent = System.IO.File.Exists(cssPath) ? await System.IO.File.ReadAllTextAsync(cssPath) : string.Empty;
            var template = System.IO.File.Exists(emailTemplatePath)
                ? await System.IO.File.ReadAllTextAsync(emailTemplatePath)
                : $"Please reset your password by visiting: {HtmlEncoder.Default.Encode(callbackUrl)}";

            var html = template.Replace("{SITE_CSS}", cssContent)
                               .Replace("{RESET_LINK}", HtmlEncoder.Default.Encode(callbackUrl))
                               .Replace("{EMAIL}", Input.Email)
                               .Replace("{SITE_TITLE}", "Storage");

            await _emailSender.SendAsync(EmailMessage.Html(
                Input.Email,
                "Reset Password",
                html),
                HttpContext.RequestAborted);
            return RedirectToPage("./ForgotPasswordConfirmation");
        }
    }
}


