using System.Text.Json;
using Xunit;

namespace Fences.Tests;

public sealed class UsersConfigurationTests
{
    [Fact]
    public void UsersOriginIsAllowedAndAppearsInTheApplicationCatalog()
    {
        var configPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Fences/appsettings.json"));
        using var document = JsonDocument.Parse(File.ReadAllText(configPath));
        var identity = document.RootElement.GetProperty("IdentityApp");
        Assert.Contains(identity.GetProperty("AllowedReturnHosts").EnumerateArray(), value => value.GetString() == "users.eldervibe.dev");
        Assert.Contains(identity.GetProperty("AllowedCorsOrigins").EnumerateArray(), value => value.GetString() == "https://users.eldervibe.dev");
        Assert.Contains(identity.GetProperty("Apps").EnumerateArray(), value =>
            value.GetProperty("Key").GetString() == "users" && value.GetProperty("Url").GetString() == "https://users.eldervibe.dev");
    }
}
