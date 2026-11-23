using System.Data;
using Construction.Kernel.Identity;
using Construction.Kernel.Tenancy;
using Construction.Platform.Contracts.Identity;
using Construction.Platform.Core.Domain.Identity;
using Construction.Platform.Core.Domain.Tenancy;
using Construction.Platform.Infrastructure.Identity;
using Construction.Platform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Testcontainers.PostgreSql;
using Xunit;

namespace Construction.Platform.Domain.Tests.Infrastructure.Identity;

public sealed class LivePostgreSqlIdentityRlsTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("bonyan_test")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        var connStr = _container.GetConnectionString();
        Npgsql.NpgsqlConnection.ClearAllPools();

        // 1. Provision prerequisite database roles (app, etc.) and extensions before running EF migrations
        await using (var rawConn = new Npgsql.NpgsqlConnection(connStr))
        {
            await rawConn.OpenAsync();
            await using var initCmd = rawConn.CreateCommand();
            initCmd.CommandText = """
                CREATE EXTENSION IF NOT EXISTS citext SCHEMA public;

                DO $$
                BEGIN
                    IF NOT EXISTS (SELECT FROM pg_roles WHERE rolname = 'app') THEN
                        CREATE ROLE app WITH LOGIN PASSWORD 'app_pass';
                    END IF;
                END $$;
            """;
            await initCmd.ExecuteNonQueryAsync();
            await rawConn.ReloadTypesAsync();
        }

        Npgsql.NpgsqlConnection.ClearAllPools();

        // 2. Run migrations on the test container without warning suppression
        var dataSourceBuilder = new Npgsql.NpgsqlDataSourceBuilder(connStr);
        var dataSource = dataSourceBuilder.Build();

        var options = new DbContextOptionsBuilder<PlatformDbContext>()
            .UseNpgsql(dataSource)
            .UseSnakeCaseNamingConvention()
            .Options;

        await using var context = new PlatformDbContext(options, new DummyTenantContext());
        await context.Database.MigrateAsync();

        // Apply app role schema permissions for RLS
        await using var conn = context.Database.GetDbConnection();
        await conn.OpenAsync();

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            GRANT USAGE ON SCHEMA platform TO app;
            GRANT ALL PRIVILEGES ON ALL TABLES IN SCHEMA platform TO app;
        """;
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task DisposeAsync()
    {
        await _container.DisposeAsync();
    }

    private DbContextOptions<PlatformDbContext> CreateContextOptions()
    {
        var connStr = _container.GetConnectionString();
        var dataSourceBuilder = new Npgsql.NpgsqlDataSourceBuilder(connStr);
        var dataSource = dataSourceBuilder.Build();

        return new DbContextOptionsBuilder<PlatformDbContext>()
            .UseNpgsql(dataSource)
            .UseSnakeCaseNamingConvention()
            .Options;
    }

    private static IOptions<IdentityOptions> CreateTestIdentityOptions()
    {
        return Options.Create(new IdentityOptions
        {
            Argon2MemoryCostKiB = 1024,
            Argon2TimeCost = 1,
            Argon2Parallelism = 1,
            JwtSigningKey = "ASecretKeyThatIsAtLeast32BytesLongForTestingPurposes!",
            Issuer = "https://identity.test.bonyan.example",
            Audience = "https://api.test.bonyan.example",
            AccessTokenExpirationMinutes = 15,
            RefreshTokenExpirationDays = 30
        });
    }

    [Fact]
    public async Task ConcurrentLogin_10ParallelFailedAttempts_SerializesAndLocksAccountExactlyAtFifthFailure()
    {
        var options = CreateContextOptions();
        var tenantId = TenantId.New();
        var userId = Guid.NewGuid();
        var email = "concurrent_user@example.com";

        var idOptions = CreateTestIdentityOptions();
        var hasher = new Argon2idPasswordHasher(idOptions);
        var passwordHash = hasher.HashPassword("RealPassword123!");

        // Seed tenant and user
        await using (var seedCtx = new PlatformDbContext(options, new DummyTenantContext(tenantId)))
        {
            var tenant = Tenant.Create(tenantId.Value, "concurrent-co", "شركة متزامنة", "Concurrent Co", "starter");
            seedCtx.Tenants.Add(tenant);

            var user = User.Create(userId, tenantId, email, "مستخدم", "User");
            user.SetPassword(passwordHash, DateTimeOffset.UtcNow);
            seedCtx.Users.Add(user);
            await seedCtx.SaveChangesAsync();
        }

        // Dispatch 10 parallel login attempts with WRONG password
        var tasks = Enumerable.Range(1, 10).Select(async _ =>
        {
            await using var callCtx = new PlatformDbContext(options, new DummyTenantContext());
            var tokenService = new JwtTokenService(idOptions);
            var service = new IdentityService(callCtx, hasher, tokenService, idOptions, NullLogger<IdentityService>.Instance);
            return await service.LoginAsync(new LoginRequest(email, "WrongPassword123!"));
        }).ToList();

        var results = await Task.WhenAll(tasks);

        // Verification: Exactly 4 attempts return 401 (failed attempts 1-4), 1 attempt triggers the lock (attempt 5 returns 423), and remaining 5 return 423 (account_locked)
        // Per Spec 01 §6.1 BR-PLT-03: 5 failures lock the account for 15 minutes.
        var invalidCredsCount = results.Count(r => r.IsFailure && r.Error.Code == "platform.invalid_credentials");
        var accountLockedCount = results.Count(r => r.IsFailure && r.Error.Code == "platform.account_locked");

        Assert.Equal(10, invalidCredsCount + accountLockedCount);
        Assert.Equal(4, invalidCredsCount);
        Assert.Equal(6, accountLockedCount);

        // Verify final state in PostgreSQL
        await using (var verifyCtx = new PlatformDbContext(options, new DummyTenantContext(tenantId)))
        {
            var user = await verifyCtx.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId);
            Assert.NotNull(user);
            Assert.Equal(UserStatus.Locked, user.Status);
            Assert.Equal(0, user.FailedLoginCount);
            Assert.NotNull(user.LockedUntil);
            Assert.True(user.LockedUntil.Value > DateTimeOffset.UtcNow);
        }
    }

    [Fact]
    public async Task LoginAsync_ValidCredentials_ReturnsTokensAndRecordsSuccessfulLogin()
    {
        var options = CreateContextOptions();
        var tenantId = TenantId.New();
        var userId = Guid.NewGuid();
        var email = "valid_login@example.com";
        var rawPassword = "CorrectPassword123!";

        var idOptions = CreateTestIdentityOptions();
        var hasher = new Argon2idPasswordHasher(idOptions);
        var passwordHash = hasher.HashPassword(rawPassword);

        await using (var seedCtx = new PlatformDbContext(options, new DummyTenantContext(tenantId)))
        {
            var tenant = Tenant.Create(tenantId.Value, "valid-co", "شركة صالحة", "Valid Co", "starter");
            seedCtx.Tenants.Add(tenant);

            var user = User.Create(userId, tenantId, email, "مستخدم", "User");
            user.SetPassword(passwordHash, DateTimeOffset.UtcNow);
            seedCtx.Users.Add(user);
            await seedCtx.SaveChangesAsync();
        }

        await using var callCtx = new PlatformDbContext(options, new DummyTenantContext());
        var tokenService = new JwtTokenService(idOptions);
        var service = new IdentityService(callCtx, hasher, tokenService, idOptions, NullLogger<IdentityService>.Instance);

        var result = await service.LoginAsync(new LoginRequest(email, rawPassword));

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value.AccessToken);
        Assert.NotNull(result.Value.RefreshToken);
        Assert.Equal("Bearer", result.Value.TokenType);

        // Validate access token claims
        var principal = tokenService.ValidateToken(result.Value.AccessToken);
        Assert.NotNull(principal);
        Assert.Equal(userId.ToString(), principal.FindFirst("sub")?.Value);
        Assert.Equal(tenantId.Value.ToString(), principal.FindFirst("tid")?.Value);
        Assert.Equal(email, principal.FindFirst("email")?.Value);

        // Verify database user record
        await using (var verifyCtx = new PlatformDbContext(options, new DummyTenantContext(tenantId)))
        {
            var user = await verifyCtx.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId);
            Assert.NotNull(user);
            Assert.Equal(0, user.FailedLoginCount);
            Assert.NotNull(user.LastLoginAt);

            // Verify active refresh token in PostgreSQL
            var rawHash = tokenService.HashRefreshToken(result.Value.RefreshToken);
            var tokenRow = await verifyCtx.RefreshTokens.AsNoTracking().FirstOrDefaultAsync(t => t.TokenHash == rawHash);
            Assert.NotNull(tokenRow);
            Assert.Equal(userId, tokenRow.UserId);
            Assert.Equal(tenantId, tokenRow.TenantId);
            Assert.True(tokenRow.IsActive);
        }
    }

    [Fact]
    public async Task RefreshTokenAsync_ValidToken_RotatesSuccessorAndPreservesFamily()
    {
        var options = CreateContextOptions();
        var tenantId = TenantId.New();
        var userId = Guid.NewGuid();
        var email = "refresh_user@example.com";
        var rawPassword = "CorrectPassword123!";

        var idOptions = CreateTestIdentityOptions();
        var hasher = new Argon2idPasswordHasher(idOptions);
        var passwordHash = hasher.HashPassword(rawPassword);

        await using (var seedCtx = new PlatformDbContext(options, new DummyTenantContext(tenantId)))
        {
            var tenant = Tenant.Create(tenantId.Value, "refresh-co", "شركة تحديث", "Refresh Co", "starter");
            seedCtx.Tenants.Add(tenant);

            var user = User.Create(userId, tenantId, email, "مستخدم", "User");
            user.SetPassword(passwordHash, DateTimeOffset.UtcNow);
            seedCtx.Users.Add(user);
            await seedCtx.SaveChangesAsync();
        }

        // Login to issue initial token
        await using var loginCtx = new PlatformDbContext(options, new DummyTenantContext());
        var tokenService = new JwtTokenService(idOptions);
        var service1 = new IdentityService(loginCtx, hasher, tokenService, idOptions, NullLogger<IdentityService>.Instance);
        var loginResult = await service1.LoginAsync(new LoginRequest(email, rawPassword));
        Assert.True(loginResult.IsSuccess);
        var initialRawRefreshToken = loginResult.Value.RefreshToken;
        var initialHash = tokenService.HashRefreshToken(initialRawRefreshToken);

        // Perform rotation via RefreshTokenAsync
        await using var refreshCtx = new PlatformDbContext(options, new DummyTenantContext());
        var service2 = new IdentityService(refreshCtx, hasher, tokenService, idOptions, NullLogger<IdentityService>.Instance);
        var refreshResult = await service2.RefreshTokenAsync(initialRawRefreshToken);

        Assert.True(refreshResult.IsSuccess);
        Assert.NotNull(refreshResult.Value.AccessToken);
        Assert.NotNull(refreshResult.Value.RefreshToken);
        Assert.NotEqual(initialRawRefreshToken, refreshResult.Value.RefreshToken);

        var successorHash = tokenService.HashRefreshToken(refreshResult.Value.RefreshToken);

        // Verify in database: initial token is rotated, successor token is active in same family
        await using (var verifyCtx = new PlatformDbContext(options, new DummyTenantContext(tenantId)))
        {
            var initialTokenRow = await verifyCtx.RefreshTokens.AsNoTracking().FirstOrDefaultAsync(t => t.TokenHash == initialHash);
            Assert.NotNull(initialTokenRow);
            Assert.NotNull(initialTokenRow.RotatedAt);
            Assert.Null(initialTokenRow.RevokedAt);

            var successorTokenRow = await verifyCtx.RefreshTokens.AsNoTracking().FirstOrDefaultAsync(t => t.TokenHash == successorHash);
            Assert.NotNull(successorTokenRow);
            Assert.Equal(initialTokenRow.FamilyId, successorTokenRow.FamilyId);
            Assert.Null(successorTokenRow.RotatedAt);
            Assert.Null(successorTokenRow.RevokedAt);
            Assert.True(successorTokenRow.IsActive);
        }
    }

    [Fact]
    public async Task RefreshTokenAsync_ReuseDetected_RevokesEntireFamily()
    {
        var options = CreateContextOptions();
        var tenantId = TenantId.New();
        var userId = Guid.NewGuid();
        var email = "reuse_user@example.com";
        var rawPassword = "CorrectPassword123!";

        var idOptions = CreateTestIdentityOptions();
        var hasher = new Argon2idPasswordHasher(idOptions);
        var passwordHash = hasher.HashPassword(rawPassword);

        await using (var seedCtx = new PlatformDbContext(options, new DummyTenantContext(tenantId)))
        {
            var tenant = Tenant.Create(tenantId.Value, "reuse-co", "شركة إعادة استخدام", "Reuse Co", "starter");
            seedCtx.Tenants.Add(tenant);

            var user = User.Create(userId, tenantId, email, "مستخدم", "User");
            user.SetPassword(passwordHash, DateTimeOffset.UtcNow);
            seedCtx.Users.Add(user);
            await seedCtx.SaveChangesAsync();
        }

        // 1. Initial Login
        await using var loginCtx = new PlatformDbContext(options, new DummyTenantContext());
        var tokenService = new JwtTokenService(idOptions);
        var service1 = new IdentityService(loginCtx, hasher, tokenService, idOptions, NullLogger<IdentityService>.Instance);
        var loginResult = await service1.LoginAsync(new LoginRequest(email, rawPassword));
        var initialRawRefreshToken = loginResult.Value.RefreshToken;

        // 2. Legitimate Rotation (Initial token becomes Rotated)
        await using var refreshCtx = new PlatformDbContext(options, new DummyTenantContext());
        var service2 = new IdentityService(refreshCtx, hasher, tokenService, idOptions, NullLogger<IdentityService>.Instance);
        var refreshResult = await service2.RefreshTokenAsync(initialRawRefreshToken);
        Assert.True(refreshResult.IsSuccess);
        var successorRawRefreshToken = refreshResult.Value.RefreshToken;

        // 3. Malicious Replay of the already-rotated initial token
        await using var replayCtx = new PlatformDbContext(options, new DummyTenantContext());
        var service3 = new IdentityService(replayCtx, hasher, tokenService, idOptions, NullLogger<IdentityService>.Instance);
        var replayResult = await service3.RefreshTokenAsync(initialRawRefreshToken);

        // Verification: Replay must fail with platform.token_revoked
        Assert.True(replayResult.IsFailure);
        Assert.Equal("platform.token_revoked", replayResult.Error.Code);

        // 4. Verify in PostgreSQL that ALL tokens in the family are revoked with reuse_detected
        var initialHash = tokenService.HashRefreshToken(initialRawRefreshToken);
        await using (var verifyCtx = new PlatformDbContext(options, new DummyTenantContext(tenantId)))
        {
            var initialTokenRow = await verifyCtx.RefreshTokens.AsNoTracking().FirstOrDefaultAsync(t => t.TokenHash == initialHash);
            Assert.NotNull(initialTokenRow);
            var familyId = initialTokenRow.FamilyId;

            var familyTokens = await verifyCtx.RefreshTokens.AsNoTracking()
                .Where(t => t.FamilyId == familyId)
                .ToListAsync();

            Assert.Equal(2, familyTokens.Count);
            Assert.All(familyTokens, t =>
            {
                Assert.NotNull(t.RevokedAt);
                Assert.Equal(RevokeReason.ReuseDetected, t.RevokeReason);
            });
        }

        // 5. Successor token must also no longer be usable
        await using var followUpCtx = new PlatformDbContext(options, new DummyTenantContext());
        var service4 = new IdentityService(followUpCtx, hasher, tokenService, idOptions, NullLogger<IdentityService>.Instance);
        var followUpResult = await service4.RefreshTokenAsync(successorRawRefreshToken);

        Assert.True(followUpResult.IsFailure);
        Assert.Equal("platform.token_revoked", followUpResult.Error.Code);
    }

    [Fact]
    public async Task LoginAsync_SuspendedTenant_RejectsWithTenantSuspendedOnlyAfterPasswordVerified()
    {
        var options = CreateContextOptions();
        var tenantId = TenantId.New();
        var userId = Guid.NewGuid();
        var email = "suspended_user@example.com";
        var rawPassword = "CorrectPassword123!";
        var wrongPassword = "WrongPassword123!";

        var idOptions = CreateTestIdentityOptions();
        var hasher = new Argon2idPasswordHasher(idOptions);
        var passwordHash = hasher.HashPassword(rawPassword);

        // 1. Seed tenant with 'suspended' status and user with valid password hash
        await using (var seedCtx = new PlatformDbContext(options, new DummyTenantContext(tenantId)))
        {
            var tenant = Tenant.Create(tenantId.Value, "suspended-co", "شركة موقوفة", "Suspended Co", "starter");
            seedCtx.Tenants.Add(tenant);

            var user = User.Create(userId, tenantId, email, "مستخدم", "User");
            user.SetPassword(passwordHash, DateTimeOffset.UtcNow);
            seedCtx.Users.Add(user);
            await seedCtx.SaveChangesAsync();

            // Set tenant status to 'suspended' via SQL
            await seedCtx.Database.ExecuteSqlRawAsync(
                "UPDATE platform.tenants SET status = 'suspended' WHERE id = {0}", tenantId.Value);
        }

        // Verify seeded tenant is indeed suspended in DB
        await using (var checkCtx = new PlatformDbContext(options, new DummyTenantContext(tenantId)))
        {
            var seededTenant = await checkCtx.Tenants.AsNoTracking().FirstOrDefaultAsync(t => t.Id == tenantId.Value);
            Assert.NotNull(seededTenant);
            Assert.Equal("suspended", seededTenant.Status);
        }

        var tokenService = new JwtTokenService(idOptions);

        // 2. Attempt login with WRONG password on suspended tenant:
        // Must reject with platform.invalid_credentials and NOT reveal suspension status (BR-PLT-09).
        // Must increment failed login count in database.
        await using (var wrongPwdCtx = new PlatformDbContext(options, new DummyTenantContext()))
        {
            var wrongPwdService = new IdentityService(wrongPwdCtx, hasher, tokenService, idOptions, NullLogger<IdentityService>.Instance);
            var wrongPwdResult = await wrongPwdService.LoginAsync(new LoginRequest(email, wrongPassword));

            Assert.True(wrongPwdResult.IsFailure);
            Assert.Equal("platform.invalid_credentials", wrongPwdResult.Error.Code);
        }

        // Verify in DB that failed attempt was recorded (proving password check ran before any tenant check)
        await using (var verifyCtx = new PlatformDbContext(options, new DummyTenantContext(tenantId)))
        {
            var user = await verifyCtx.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId);
            Assert.NotNull(user);
            Assert.Equal(1, user.FailedLoginCount);
        }

        // 3. Attempt login with CORRECT password on suspended tenant:
        // Password is verified successfully, THEN tenant suspension check fails (BR-PLT-09).
        // Returns platform.tenant_suspended (403 Forbidden equivalent).
        await using (var validPwdCtx = new PlatformDbContext(options, new DummyTenantContext()))
        {
            var validPwdService = new IdentityService(validPwdCtx, hasher, tokenService, idOptions, NullLogger<IdentityService>.Instance);
            var validPwdResult = await validPwdService.LoginAsync(new LoginRequest(email, rawPassword));

            Assert.True(validPwdResult.IsFailure);
            Assert.Equal("platform.tenant_suspended", validPwdResult.Error.Code);
        }

        // 4. Verify in DB that NO tokens (access or refresh) were issued or persisted
        await using (var verifyTokensCtx = new PlatformDbContext(options, new DummyTenantContext(tenantId)))
        {
            var tokenCount = await verifyTokensCtx.RefreshTokens.AsNoTracking()
                .CountAsync(t => t.UserId == userId);
            Assert.Equal(0, tokenCount);

            // Also verify user failed_login_count was not modified by the suspended rejection (transaction rolled back)
            var user = await verifyTokensCtx.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId);
            Assert.NotNull(user);
            Assert.Null(user.LastLoginAt);
        }
    }

    private sealed class DummyTenantContext(TenantId? tenantId = null) : ITenantContext
    {
        public bool HasTenant => tenantId.HasValue;
        public TenantId Current => tenantId ?? TenantId.New();
    }
}
