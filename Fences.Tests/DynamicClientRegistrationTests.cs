using Xunit;
using Fences.Services;

namespace Fences.Tests;

public class DynamicClientRegistrationTests
{
    private static ClientRegistrationResult Validate(ClientRegistrationRequest request) =>
        DynamicClientRegistration.Validate(request, DynamicClientRegistration.DefaultCustomSchemes, DynamicClientRegistration.DefaultHttpsHosts);

    [Theory]
    [InlineData("http://127.0.0.1:33418/callback")]
    [InlineData("http://localhost:8123/oauth/callback")]
    [InlineData("http://[::1]:9000/cb")]
    [InlineData("vscode://vscode.github-authentication/did-authenticate")]
    [InlineData("https://vscode.dev/redirect")]
    [InlineData("https://claude.ai/api/mcp/auth_callback")]
    public void Accepts_loopback_known_schemes_and_known_https_hosts(string redirect)
    {
        var result = Validate(new() { RedirectUris = [redirect] });
        Assert.True(result.IsValid, result.Description);
    }

    [Theory]
    [InlineData("https://evil.example/cb")]
    [InlineData("http://evil.example/cb")]
    [InlineData("http://localhost@evil.example/cb")]
    [InlineData("javascript:alert(1)")]
    [InlineData("myapp://cb")]
    [InlineData("http://127.0.0.1/cb#frag")]
    [InlineData("/relative")]
    public void Rejects_redirects_that_could_leak_the_code(string redirect)
    {
        var result = Validate(new() { RedirectUris = [redirect] });
        Assert.False(result.IsValid);
        Assert.Equal("invalid_redirect_uri", result.Error);
    }

    [Fact]
    public void Requires_at_least_one_and_at_most_ten_redirect_uris()
    {
        Assert.False(Validate(new()).IsValid);
        Assert.False(Validate(new() { RedirectUris = [] }).IsValid);
        Assert.False(Validate(new() { RedirectUris = Enumerable.Range(0, 11).Select(i => $"http://127.0.0.1:{3000 + i}/cb").ToList() }).IsValid);
    }

    [Fact]
    public void Rejects_confidential_clients_and_unsupported_grants()
    {
        var redirect = new List<string> { "http://127.0.0.1:1/cb" };
        Assert.False(Validate(new() { RedirectUris = redirect, TokenEndpointAuthMethod = "client_secret_basic" }).IsValid);
        Assert.False(Validate(new() { RedirectUris = redirect, GrantTypes = ["client_credentials"] }).IsValid);
        Assert.False(Validate(new() { RedirectUris = redirect, ResponseTypes = ["token"] }).IsValid);
        Assert.True(Validate(new() { RedirectUris = redirect, TokenEndpointAuthMethod = "none", GrantTypes = ["authorization_code", "refresh_token"], ResponseTypes = ["code"] }).IsValid);
    }

    [Fact]
    public void Configured_extra_hosts_and_schemes_are_honoured()
    {
        var request = new ClientRegistrationRequest { RedirectUris = ["https://client.example/cb", "tool://cb"] };
        Assert.False(Validate(request).IsValid);
        var result = DynamicClientRegistration.Validate(request, ["tool"], ["client.example"]);
        Assert.True(result.IsValid, result.Description);
    }
}
