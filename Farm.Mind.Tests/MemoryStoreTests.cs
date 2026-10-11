using Farm.Mind.Data;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Farm.Mind.Tests;

public class MemoryStoreTests
{
    private static MemoryStore CreateStore()
    {
        var options = new DbContextOptionsBuilder<MindDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        return new MemoryStore(new MindDbContext(options));
    }

    [Fact]
    public async Task Save_NormalizesTags_AndGetReturnsIt()
    {
        var store = CreateStore();
        var saved = await store.SaveAsync("u1", " Title ", "Body", ["Home Lab", "home lab", " "], null);

        var loaded = await store.GetAsync("u1", saved.Id);

        Assert.NotNull(loaded);
        Assert.Equal("Title", loaded.Title);
        Assert.Equal(["home-lab"], loaded.Tags);
    }

    [Fact]
    public async Task OtherSubject_CannotReadUpdateOrForget()
    {
        var store = CreateStore();
        var saved = await store.SaveAsync("u1", "Secret", "Body", null, null);

        Assert.Null(await store.GetAsync("u2", saved.Id));
        Assert.Null(await store.UpdateAsync("u2", saved.Id, "x", null, null, null));
        Assert.False(await store.ForgetAsync("u2", saved.Id));
        Assert.NotNull(await store.GetAsync("u1", saved.Id));
    }

    [Fact]
    public async Task Update_ChangesOnlyProvidedFields()
    {
        var store = CreateStore();
        var saved = await store.SaveAsync("u1", "Old", "Keep me", ["a"], "ws");

        var updated = await store.UpdateAsync("u1", saved.Id, "New", null, null, null);

        Assert.Equal("New", updated!.Title);
        Assert.Equal("Keep me", updated.Content);
        Assert.Equal(["a"], updated.Tags);
        Assert.Equal("ws", updated.Workspace);
    }

    [Fact]
    public async Task Forget_RemovesMemory()
    {
        var store = CreateStore();
        var saved = await store.SaveAsync("u1", "T", "B", null, null);

        Assert.True(await store.ForgetAsync("u1", saved.Id));
        Assert.Null(await store.GetAsync("u1", saved.Id));
    }

    [Fact]
    public async Task Search_FiltersByWorkspace_AndEmptyQueryReturnsRecent()
    {
        var store = CreateStore();
        await store.SaveAsync("u1", "One", "alpha", null, "a");
        await store.SaveAsync("u1", "Two", "alpha", null, "b");

        Assert.Single(await store.SearchAsync("u1", "alpha", "a"));
        Assert.Equal(2, (await store.SearchAsync("u1", "")).Count);
    }

    [Theory]
    [InlineData("", "body")]
    [InlineData("title", " ")]
    public async Task Save_RejectsInvalidInput(string title, string content)
    {
        await Assert.ThrowsAsync<ArgumentException>(() => CreateStore().SaveAsync("u1", title, content, null, null));
    }

    [Fact]
    public async Task LinkWorkspace_IsIdempotentPerSubject()
    {
        var store = CreateStore();
        await store.LinkWorkspaceAsync("u1", "Main", null);
        await store.LinkWorkspaceAsync("u1", "Main", "affine-1");
        await store.LinkWorkspaceAsync("u2", "Main", null);

        var mine = await store.ListWorkspacesAsync("u1");
        Assert.Equal("affine-1", Assert.Single(mine).AffineWorkspaceId);
        Assert.Single(await store.ListWorkspacesAsync("u2"));
    }
}
