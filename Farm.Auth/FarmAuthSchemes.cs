namespace Farm.Auth;

public static class FarmAuthSchemes
{
    /// <summary>The browser session cookie shared by the Farm UI and Farm.Mind.</summary>
    public const string Cookie = "FarmCookie";

    /// <summary>The OIDC challenge used for browser sign-in (redirects to Fences).</summary>
    public const string Oidc = "FarmOidc";

    /// <summary>Bearer tokens issued by Fences, presented by MCP and other API clients.</summary>
    public const string Bearer = "FarmBearer";

    /// <summary>Picks Bearer when an Authorization header is present, otherwise the cookie.</summary>
    public const string Auto = "FarmAuto";

    public const string UserPolicy = "FarmUser";

    /// <summary>Bearer-only policy for the MCP endpoint, so a 401 carries the resource metadata hint.</summary>
    public const string McpPolicy = "FarmMcp";
}
