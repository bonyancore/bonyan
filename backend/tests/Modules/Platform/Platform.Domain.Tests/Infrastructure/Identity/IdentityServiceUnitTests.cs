using Construction.Kernel.Identity;
using Construction.Kernel.Tenancy;
using Construction.Platform.Contracts.Identity;
using Construction.Platform.Core.Domain.Identity;
using Construction.Platform.Infrastructure.Identity;
using Construction.Platform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Construction.Platform.Domain.Tests.Infrastructure.Identity;

public sealed class IdentityServiceUnitTests : IDisposable
{
    private readonly PlatformDbContext _dbContext;
    private readonly Argon2idPasswordHasher _hasher;
    private readonly JwtTokenService _tokenService;
    private readonly IdentityService _service;
    private readonly TestTenantContext _tenantContext;

    public void Dispose()
    {
        _dbContext.Dispose();
    }

    public IdentityServiceUnitTests()
    {
        _tenantContext = new TestTenantContext();

        var options = new DbContextOptionsBuilder<PlatformDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _dbContext = new PlatformDbContext(options, _tenantContext);

        var idOptions = Options.Create(new IdentityOptions
        {
            Argon2MemoryCostKiB = 1024,
            Argon2TimeCost = 1,
            Argon2Parallelism = 1,
            JwtSigningKey = "ASecretKeyThatIsAtLeast32BytesLongForTestingPurposes!"
        });

        _hasher = new Argon2idPasswordHasher(idOptions);
        _tokenService = new JwtTokenService(idOptions);
        _service = new IdentityService(
            _dbContext,
            _hasher,
            _tokenService,
            idOptions,
            NullLogger<IdentityService>.Instance);
    }

    [Fact]
    public async Task GetCurrentUserProfileAsync_UserExists_ReturnsSuccess()
    {
        var tenantId = TenantId.New();
        _tenantContext.Current = tenantId;
        var userId = Guid.NewGuid();
        var user = User.Create(
            userId,
            tenantId,
            "test@example.com",
            "أحمد",
            "Ahmed",
            phone: "+201012345678",
            jobTitle: "Senior Project Manager");

        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        var result = await _service.GetCurrentUserProfileAsync(UserId.From(userId), tenantId);

        Assert.True(result.IsSuccess);
        Assert.Equal(userId, result.Value.Id);
        Assert.Equal(tenantId.Value, result.Value.TenantId);
        Assert.Equal("test@example.com", result.Value.Email);
        Assert.Equal("Ahmed", result.Value.FullNameEn);
    }

    [Fact]
    public async Task GetCurrentUserProfileAsync_UserNotFound_ReturnsFailure()
    {
        var tenantId = TenantId.New();
        _tenantContext.Current = tenantId;
        var result = await _service.GetCurrentUserProfileAsync(UserId.From(Guid.NewGuid()), tenantId);

        Assert.True(result.IsFailure);
        Assert.Equal("platform.user_not_found", result.Error.Code);
    }

    [Fact]
    public async Task LogoutAsync_WithFamilyId_ExecutesSuccessfully()
    {
        var userId = UserId.New();
        var familyId = Guid.NewGuid();

        var result = await _service.LogoutAsync(userId, familyId);
        Assert.True(result.IsSuccess);
    }

    private sealed class TestTenantContext : ITenantContext
    {
        public bool HasTenant => true;
        public TenantId Current { get; set; } = TenantId.New();
    }
}
