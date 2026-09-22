using Xunit;

namespace Farm.Azure.Tests;

public sealed class AzureIdentityMapperTests
{
    [Fact]
    public void ToDomain_maps_identity_self_fields()
    {
        var self = new Microsoft.VisualStudio.Services.Identity.IdentitySelf
        {
            Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            DisplayName = "Person Example",
            AccountName = "person@example.com"
        };

        var result = AzureIdentityMapper.ToDomain(self);

        Assert.Equal("11111111-1111-1111-1111-111111111111", result.Id);
        Assert.Equal("Person Example", result.DisplayName);
        Assert.Equal("person@example.com", result.UniqueName);
    }

    [Fact]
    public void ToGroup_maps_identity_fields_with_no_members()
    {
        var group = new Microsoft.VisualStudio.Services.Identity.Identity
        {
            Id = Guid.Parse("22222222-2222-2222-2222-222222222222"),
            ProviderDisplayName = "[Project]\\Contributors"
        };

        var result = AzureIdentityMapper.ToGroup(group);

        Assert.Equal("22222222-2222-2222-2222-222222222222", result.Id);
        Assert.Equal("[Project]\\Contributors", result.DisplayName);
        Assert.Empty(result.Members);
    }
}
