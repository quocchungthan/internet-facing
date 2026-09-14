using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Text.Json;
using Shed.Services;

namespace Shed.Pages;

[AllowAnonymous]
public class SignInModel : PageModel
{
    private const string LastLoginEmailCookie = "last_login_email";

    private readonly SignInManager<IdentityUser> _signInManager;
    private readonly IConfiguration _configuration;
    private readonly IdentityBridgeOptions _identityBridgeOptions;

    public SignInModel(
        SignInManager<IdentityUser> signInManager,
        IConfiguration configuration,
        Microsoft.Extensions.Options.IOptions<IdentityBridgeOptions> identityBridgeOptions)
    {
        _signInManager = signInManager;
        _configuration = configuration;
        _identityBridgeOptions = identityBridgeOptions.Value;
    }

    [BindProperty]
    public string Email { get; set; } = string.Empty;

    [BindProperty]
    public string Password { get; set; } = string.Empty;

    [BindProperty]
    public bool RememberMe { get; set; }

    public string? ReturnUrl { get; private set; }

    public bool IsBridgeEnabled => _identityBridgeOptions.Enabled;

    public string BridgeSignInUrl { get; private set; } = "/auth/login";

    public bool UseRememberedAccount { get; private set; }

    public string? RememberedEmail { get; private set; }

    public bool ShowForgotPassword { get; private set; } = true;

    public bool ShowRegister { get; private set; } = true;

    public string ForgotPasswordText { get; private set; } = "Forgot password?";

    public string RegisterText { get; private set; } = "Register";

    public string CardTextColor { get; private set; } = "#f4f5f7";

    public string LabelTextColor { get; private set; } = "#e2e5ec";

    public string AuxTextColor { get; private set; } = "#d7dae2";

    public string LinkTextColor { get; private set; } = "#ffc9b7";

    public string? ErrorMessage { get; private set; }

    public string? InfoMessage { get; private set; }

    public IActionResult OnGet(string? returnUrl = null, bool switchAccount = false, bool emailConfirmed = false, bool passwordReset = false)
    {
        if (_identityBridgeOptions.Enabled)
        {
            return Redirect(BuildBridgeSignInUrl(returnUrl));
        }

        if (User.Identity?.IsAuthenticated == true)
        {
            return LocalRedirect("/");
        }

        ReturnUrl = returnUrl;

        LoadGlobalLoginSettings();
        LoadRememberedEmail(switchAccount);

        if (emailConfirmed)
        {
            InfoMessage = "Email confirmed. You can sign in now.";
        }
        else if (passwordReset)
        {
            InfoMessage = "Password updated. Please sign in with your new password.";
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(string? returnUrl = null)
    {
        if (_identityBridgeOptions.Enabled)
        {
            return Redirect(BuildBridgeSignInUrl(returnUrl));
        }

        if (User.Identity?.IsAuthenticated == true)
        {
            return LocalRedirect("/");
        }

        if (string.IsNullOrWhiteSpace(Email)
            && Request.Cookies.TryGetValue(LastLoginEmailCookie, out var remembered)
            && IsLikelyEmail(remembered))
        {
            Email = remembered.Trim();
        }

        if (string.IsNullOrWhiteSpace(Email) || string.IsNullOrWhiteSpace(Password))
        {
            ReturnUrl = returnUrl;
            LoadGlobalLoginSettings();
            LoadRememberedEmail(false);
            ErrorMessage = "Email and password are required.";
            return Page();
        }

        var result = await _signInManager.PasswordSignInAsync(Email.Trim(), Password, RememberMe, lockoutOnFailure: false);
        if (result.Succeeded)
        {
            Response.Cookies.Append(LastLoginEmailCookie, Email.Trim(), new CookieOptions
            {
                Expires = DateTimeOffset.UtcNow.AddDays(180),
                HttpOnly = true,
                IsEssential = true,
                Secure = Request.IsHttps,
                SameSite = SameSiteMode.Lax
            });

            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return LocalRedirect(returnUrl);
            }

            return LocalRedirect("/");
        }

        if (result.IsLockedOut)
        {
            ReturnUrl = returnUrl;
            LoadGlobalLoginSettings();
            LoadRememberedEmail(false);
            ErrorMessage = "Your account is locked. Please try again later.";
            return Page();
        }

        if (result.IsNotAllowed)
        {
            ReturnUrl = returnUrl;
            LoadGlobalLoginSettings();
            LoadRememberedEmail(false);
            ErrorMessage = "You must confirm your email before logging in.";
            return Page();
        }

        ReturnUrl = returnUrl;
        LoadGlobalLoginSettings();
        LoadRememberedEmail(false);
        ErrorMessage = "Invalid credentials.";
        return Page();
    }

    private void LoadRememberedEmail(bool switchAccount)
    {
        UseRememberedAccount = false;
        RememberedEmail = null;

        if (switchAccount)
        {
            return;
        }

        if (Request.Cookies.TryGetValue(LastLoginEmailCookie, out var remembered)
            && IsLikelyEmail(remembered))
        {
            RememberedEmail = remembered.Trim();
            Email = RememberedEmail;
            UseRememberedAccount = true;
        }
    }

    private static bool IsLikelyEmail(string? value)
    {
        return !string.IsNullOrWhiteSpace(value)
            && value.Contains('@')
            && value.IndexOf(' ') < 0;
    }

    private void LoadGlobalLoginSettings()
    {
        var storageRoot = _configuration["Storage:RootPath"]
            ?? @"/app/data/prodstorage";
        var publicOwnerId = _configuration["Storage:PublicOwnerId"]
            ?? Guid.Empty.ToString();

        var settingsPath = Path.Combine(storageRoot, publicOwnerId, ".settings", "ui-settings.json");
        if (!System.IO.File.Exists(settingsPath))
        {
            return;
        }

        var json = System.IO.File.ReadAllText(settingsPath);
        if (string.IsNullOrWhiteSpace(json))
        {
            return;
        }

        var payload = JsonSerializer.Deserialize<PublicLoginSettings>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        if (payload?.Login is null)
        {
            return;
        }

        ShowForgotPassword = payload.Login.ShowForgotPassword;
        ShowRegister = payload.Login.ShowRegister;
        ForgotPasswordText = string.IsNullOrWhiteSpace(payload.Login.ForgotPasswordText) ? ForgotPasswordText : payload.Login.ForgotPasswordText;
        RegisterText = string.IsNullOrWhiteSpace(payload.Login.RegisterText) ? RegisterText : payload.Login.RegisterText;
        CardTextColor = NormalizeCssColor(payload.Login.CardTextColor, CardTextColor);
        LabelTextColor = NormalizeCssColor(payload.Login.LabelTextColor, LabelTextColor);
        AuxTextColor = NormalizeCssColor(payload.Login.AuxTextColor, AuxTextColor);
        LinkTextColor = NormalizeCssColor(payload.Login.LinkTextColor, LinkTextColor);
    }

    private static string NormalizeCssColor(string? value, string fallback)
    {
        return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
    }

    private string BuildBridgeSignInUrl(string? returnUrl)
    {
        var target = "/";
        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            target = returnUrl;
        }

        return $"/auth/login?returnUrl={Uri.EscapeDataString(target)}";
    }

    private sealed class PublicLoginSettings
    {
        public LoginSettings? Login { get; set; }
    }

    private sealed class LoginSettings
    {
        public bool ShowForgotPassword { get; set; } = true;
        public bool ShowRegister { get; set; } = true;
        public string ForgotPasswordText { get; set; } = "Forgot password?";
        public string RegisterText { get; set; } = "Register";
        public string CardTextColor { get; set; } = "#f4f5f7";
        public string LabelTextColor { get; set; } = "#e2e5ec";
        public string AuxTextColor { get; set; } = "#d7dae2";
        public string LinkTextColor { get; set; } = "#ffc9b7";
    }
}


