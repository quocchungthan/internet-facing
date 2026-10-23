using System.Security.Claims;
using System.Text.Json;
using Fences.Controllers;
using Fences.Models;
using Fences.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;
using Xunit;

namespace Fences.Tests;

public sealed class FrontendTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Session_ExposesConfiguredBrandForBothAuthenticationStates(bool authenticated)
    {
        var collection = new ServiceCollection();
        collection.AddLogging();
        collection.AddOptions<IdentityAppOptions>().Bind(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["BrandName"] = "Elder & Vibe <Identity>" })
            .Build());
        collection.AddAuthentication("test").AddCookie("test");
        using var services = collection.BuildServiceProvider();
        var options = services.GetRequiredService<IOptionsMonitor<IdentityAppOptions>>();
        var controller = new SessionController(new AppCatalog(options), options)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    RequestServices = services,
                    User = new ClaimsPrincipal(authenticated
                        ? new ClaimsIdentity([new Claim(ClaimTypes.Name, "Fixture")], "test")
                        : new ClaimsIdentity())
                }
            }
        };
        var response = Assert.IsType<OkObjectResult>(await controller.GetSession());
        var session = JsonSerializer.SerializeToElement(response.Value);
        Assert.Equal(authenticated, session.GetProperty("isAuthenticated").GetBoolean());
        Assert.Equal("Elder & Vibe <Identity>", session.GetProperty("brandName").GetString());
        Assert.Equal("no-store", controller.Response.Headers.CacheControl);
    }

    [Fact]
    public void MissingBundle_ReturnsExplicitUnavailable()
    {
        using var fixture = new Fixture();
        var result = Assert.IsType<ObjectResult>(fixture.Controller.Index(null, null));
        Assert.Equal(503, result.StatusCode);
    }

    [Fact]
    public void PublicAndAuthenticatedAppPaths_ServeTheSameBundleWithoutCaching()
    {
        using var fixture = new Fixture();
        Directory.CreateDirectory(Path.Combine(fixture.Root, "frontend"));
        var entry = Path.Combine(fixture.Root, "frontend", "index.html");
        File.WriteAllText(entry, "<app-root></app-root>");
        Assert.Equal(entry, Assert.IsType<PhysicalFileResult>(fixture.Controller.Index("/apps", null)).FileName);
        Assert.Equal(entry, Assert.IsType<PhysicalFileResult>(fixture.Controller.Apps()).FileName);
        Assert.Equal("no-store", fixture.Controller.Response.Headers.CacheControl);
        Assert.NotEmpty(typeof(HomeController).GetMethod(nameof(HomeController.Apps))!
            .GetCustomAttributes(typeof(AuthorizeAttribute), true));
    }

    [Fact]
    public void SignedInRoot_KeepsExistingRedirectPolicy_ButExplicitTargetsShowFrontend()
    {
        using var fixture = new Fixture();
        fixture.Controller.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, "fixture")], "test"));
        Assert.Equal("https://eldervibe.dev", Assert.IsType<RedirectResult>(fixture.Controller.Index(null, null)).Url);
        var missing = Assert.IsType<ObjectResult>(fixture.Controller.Index("https://mail.example.invalid/", null));
        Assert.Equal(503, missing.StatusCode);
    }

    private sealed class Fixture : IDisposable
    {
        public string Root { get; } = Path.Combine(Path.GetTempPath(), "identity-frontend-" + Guid.NewGuid().ToString("N"));
        private readonly ServiceProvider services;
        public HomeController Controller { get; }

        public Fixture()
        {
            Directory.CreateDirectory(Root);
            var collection = new ServiceCollection();
            collection.AddOptions<IdentityAppOptions>();
            services = collection.BuildServiceProvider();
            Controller = new HomeController(new EnvironmentStub { ContentRootPath = Root },
                new ReturnUrlPolicy(services.GetRequiredService<IOptionsMonitor<IdentityAppOptions>>()))
            {
                ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
            };
        }

        public void Dispose()
        {
            services.Dispose();
            Directory.Delete(Root, true);
        }
    }

    private sealed class EnvironmentStub : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "Fences";
        public string EnvironmentName { get; set; } = "Development";
        public string ContentRootPath { get; set; } = "";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = "";
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
    }
}
