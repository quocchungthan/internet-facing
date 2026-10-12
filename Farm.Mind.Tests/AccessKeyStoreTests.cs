using Farm.Mind.Data;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Farm.Mind.Tests;

public class AccessKeyStoreTests
{
    private static AccessKeyStore CreateStore()
    {
        var options = new DbContextOptionsBuilder<MindDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        return new AccessKeyStore(new MindDbContext(options));
    }

    [Fact]
    public async Task CreatedKey_ValidatesToItsOwner_AndIsNotStoredInPlainText()
    {
        var store = CreateStore();
        var created = await store.CreateAsync("github:1", "Copilot CLI");

        Assert.StartsWith("fmk_", created.Secret);
        var principal = await store.ValidateAsync(created.Secret, default);
        Assert.Equal("github:1", principal!.FindFirst("sub")!.Value);
        Assert.DoesNotContain(created.Secret, created.Key.Preview);
    }

    [Fact]
    public async Task UnknownKey_DoesNotValidate() =>
        Assert.Null(await CreateStore().ValidateAsync("fmk_nope", default));

    [Fact]
    public async Task Revoke_IsOwnerScoped_AndInvalidatesTheKey()
    {
        var store = CreateStore();
        var created = await store.CreateAsync("u1", "k");

        Assert.False(await store.RevokeAsync("u2", created.Key.Id));
        Assert.NotNull(await store.ValidateAsync(created.Secret, default));
        Assert.True(await store.RevokeAsync("u1", created.Key.Id));
        Assert.Null(await store.ValidateAsync(created.Secret, default));
    }
}
