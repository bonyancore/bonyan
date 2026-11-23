using Construction.Kernel.Identity;
using Construction.Kernel.Tenancy;
using Construction.Platform.Core.Domain.Identity;
using Construction.Platform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Construction.Platform.Domain.Tests.Infrastructure.Identity;

public sealed class IdentityPersistenceTests
{
    [Fact]
    public void ModelBuilder_ContainsUsersAndRefreshTokensWithConfigurations()
    {
        var options = new DbContextOptionsBuilder<PlatformDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        var context = new PlatformDbContext(options, new DummyTenantContext());

        var userEntity = context.Model.FindEntityType(typeof(User));
        Assert.NotNull(userEntity);
        Assert.Equal("users", userEntity.GetTableName());
        Assert.Equal(PlatformDbContext.Schema, userEntity.GetSchema());

        var tokenEntity = context.Model.FindEntityType(typeof(RefreshToken));
        Assert.NotNull(tokenEntity);
        Assert.Equal("refresh_tokens", tokenEntity.GetTableName());
        Assert.Equal(PlatformDbContext.Schema, tokenEntity.GetSchema());
    }

    [Fact]
    public async Task CanPersistAndRetrieveUserAndRefreshToken()
    {
        var options = new DbContextOptionsBuilder<PlatformDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        var tenantId = TenantId.New();
        var context = new PlatformDbContext(options, new DummyTenantContext(tenantId));

        var userId = Guid.NewGuid();
        var user = User.Create(userId, tenantId, "persistence@example.com", "مهندس", "Engineer");
        user.SetPassword("hashedpass", DateTimeOffset.UtcNow);

        context.Users.Add(user);

        var tokenId = Guid.NewGuid();
        var familyId = Guid.NewGuid();
        var token = RefreshToken.Create(
            tokenId,
            tenantId,
            userId,
            familyId,
            "hash123",
            ClientKind.Web,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow.AddDays(30),
            ip: "127.0.0.1",
            userAgent: "Mozilla/5.0");

        context.RefreshTokens.Add(token);
        await context.SaveChangesAsync();

        var retrievedUser = await context.Users.FirstOrDefaultAsync(u => u.Id == userId);
        Assert.NotNull(retrievedUser);
        Assert.Equal("persistence@example.com", retrievedUser.Email);

        var retrievedToken = await context.RefreshTokens.FirstOrDefaultAsync(r => r.Id == tokenId);
        Assert.NotNull(retrievedToken);
        Assert.Equal("hash123", retrievedToken.TokenHash);
    }

    private sealed class DummyTenantContext(TenantId? tenantId = null) : ITenantContext
    {
        public bool HasTenant => true;
        public TenantId Current => tenantId ?? TenantId.New();
    }
}
