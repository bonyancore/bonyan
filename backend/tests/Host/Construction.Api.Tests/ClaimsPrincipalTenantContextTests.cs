using System.Security.Claims;
using Construction.Api.Tenancy;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace Construction.Api.Tests;

public class ClaimsPrincipalTenantContextTests
{
    private class FakeHttpContextAccessor : IHttpContextAccessor
    {
        public HttpContext? HttpContext { get; set; }
    }

    [Fact]
    public void HttpContext_IsNull_HasTenantIsFalse_AndCurrentThrows()
    {
        var accessor = new FakeHttpContextAccessor { HttpContext = null };
        var sut = new ClaimsPrincipalTenantContext(accessor);

        Assert.False(sut.HasTenant);
        var ex = Assert.Throws<InvalidOperationException>(() => sut.Current);
        Assert.Contains("No active HttpContext", ex.Message);
    }

    [Fact]
    public void User_PrincipalHasNoIdentity_HasTenantIsFalse_AndCurrentThrows()
    {
        var httpContext = new DefaultHttpContext { User = new ClaimsPrincipal() };
        var accessor = new FakeHttpContextAccessor { HttpContext = httpContext };
        var sut = new ClaimsPrincipalTenantContext(accessor);

        Assert.False(sut.HasTenant);
        var ex = Assert.Throws<InvalidOperationException>(() => sut.Current);
        Assert.Contains("Request is unauthenticated", ex.Message);
    }

    [Fact]
    public void User_IsUnauthenticated_EvenWithValidTidClaim_HasTenantIsFalse_AndCurrentThrows()
    {
        var tenantId = Guid.NewGuid();
        var identity = new ClaimsIdentity(new[] { new Claim("tid", tenantId.ToString()) }); // unauthenticated (authenticationType is null)
        var principal = new ClaimsPrincipal(identity);

        var httpContext = new DefaultHttpContext { User = principal };
        var accessor = new FakeHttpContextAccessor { HttpContext = httpContext };
        var sut = new ClaimsPrincipalTenantContext(accessor);

        Assert.False(sut.HasTenant);
        var ex = Assert.Throws<InvalidOperationException>(() => sut.Current);
        Assert.Contains("Request is unauthenticated", ex.Message);
    }

    [Fact]
    public void User_IsAuthenticated_WithValidTid_HasTenantIsTrue_AndCurrentReturnsTenantId()
    {
        var tenantId = Guid.NewGuid();
        var identity = new ClaimsIdentity(
            new[] { new Claim("tid", tenantId.ToString()) },
            authenticationType: "Bearer");
        var principal = new ClaimsPrincipal(identity);

        var httpContext = new DefaultHttpContext { User = principal };
        var accessor = new FakeHttpContextAccessor { HttpContext = httpContext };
        var sut = new ClaimsPrincipalTenantContext(accessor);

        Assert.True(sut.HasTenant);
        Assert.Equal(tenantId, sut.Current.Value);
    }

    [Fact]
    public void User_IsAuthenticated_MissingTid_HasTenantIsFalse_AndCurrentThrows()
    {
        var identity = new ClaimsIdentity(
            new[] { new Claim(ClaimTypes.Name, "testuser") },
            authenticationType: "Bearer");
        var principal = new ClaimsPrincipal(identity);

        var httpContext = new DefaultHttpContext { User = principal };
        var accessor = new FakeHttpContextAccessor { HttpContext = httpContext };
        var sut = new ClaimsPrincipalTenantContext(accessor);

        Assert.False(sut.HasTenant);
        var ex = Assert.Throws<InvalidOperationException>(() => sut.Current);
        Assert.Contains("Authenticated principal does not contain a 'tid' claim", ex.Message);
    }

    [Fact]
    public void User_IsAuthenticated_MalformedTid_HasTenantIsFalse_AndCurrentThrows()
    {
        var identity = new ClaimsIdentity(
            new[] { new Claim("tid", "not-a-guid") },
            authenticationType: "Bearer");
        var principal = new ClaimsPrincipal(identity);

        var httpContext = new DefaultHttpContext { User = principal };
        var accessor = new FakeHttpContextAccessor { HttpContext = httpContext };
        var sut = new ClaimsPrincipalTenantContext(accessor);

        Assert.False(sut.HasTenant);
        var ex = Assert.Throws<InvalidOperationException>(() => sut.Current);
        Assert.Contains("Malformed or empty 'tid' claim value", ex.Message);
    }

    [Fact]
    public void User_IsAuthenticated_EmptyGuidTid_HasTenantIsFalse_AndCurrentThrows()
    {
        var identity = new ClaimsIdentity(
            new[] { new Claim("tid", Guid.Empty.ToString()) },
            authenticationType: "Bearer");
        var principal = new ClaimsPrincipal(identity);

        var httpContext = new DefaultHttpContext { User = principal };
        var accessor = new FakeHttpContextAccessor { HttpContext = httpContext };
        var sut = new ClaimsPrincipalTenantContext(accessor);

        Assert.False(sut.HasTenant);
        var ex = Assert.Throws<InvalidOperationException>(() => sut.Current);
        Assert.Contains("Malformed or empty 'tid' claim value", ex.Message);
    }

    [Fact]
    public void User_IsAuthenticated_MultipleTidClaims_HasTenantIsFalse_AndCurrentThrows()
    {
        var identity = new ClaimsIdentity(
            new[]
            {
                new Claim("tid", Guid.NewGuid().ToString()),
                new Claim("tid", Guid.NewGuid().ToString())
            },
            authenticationType: "Bearer");
        var principal = new ClaimsPrincipal(identity);

        var httpContext = new DefaultHttpContext { User = principal };
        var accessor = new FakeHttpContextAccessor { HttpContext = httpContext };
        var sut = new ClaimsPrincipalTenantContext(accessor);

        Assert.False(sut.HasTenant);
        var ex = Assert.Throws<InvalidOperationException>(() => sut.Current);
        Assert.Contains("Multiple 'tid' claims detected", ex.Message);
    }

    [Fact]
    public void HeaderSpoofing_IsIgnored_WhenUserIsUnauthenticated()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["X-Tenant-ID"] = Guid.NewGuid().ToString();

        var accessor = new FakeHttpContextAccessor { HttpContext = httpContext };
        var sut = new ClaimsPrincipalTenantContext(accessor);

        Assert.False(sut.HasTenant);
        Assert.Throws<InvalidOperationException>(() => sut.Current);
    }
}

