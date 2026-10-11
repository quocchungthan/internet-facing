namespace Farm.Auth;

public static class ReturnUrl
{
    /// <summary>Only same-site relative paths are allowed, which blocks open redirects.</summary>
    public static string Sanitize(string? returnUrl)
    {
        if (string.IsNullOrWhiteSpace(returnUrl))
        {
            return "/";
        }

        if (!returnUrl.StartsWith('/') || returnUrl.StartsWith("//") || returnUrl.Contains('\\'))
        {
            return "/";
        }

        return returnUrl;
    }
}
