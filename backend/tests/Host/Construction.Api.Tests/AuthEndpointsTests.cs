using System.Net;
using System.Net.Http.Json;
using Construction.Kernel.Identity;
using Construction.Kernel.Tenancy;
using Construction.Platform.Contracts.Identity;
using Construction.Platform.Core.Domain.Identity;
using Construction.Platform.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace Construction.Api.Tests;

public sealed class AuthEndpointsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public AuthEndpointsTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<DbContextOptions<PlatformDbContext>>();
                services.AddDbContext<PlatformDbContext>((sp, options) =>
                {
                    options.UseInMemoryDatabase("AuthEndpointsTestsDb_" + Guid.NewGuid());
                });
            });
        });
    }

    [Fact]
    public async Task Login_EmptyBody_Returns400Or401()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/v1/platform/auth/login", new LoginRequest("", ""));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Refresh_EmptyBody_Returns400Or401()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/v1/platform/auth/refresh", new RefreshTokenRequest(""));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Me_WithoutAuthorization_Returns401()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/v1/platform/auth/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
