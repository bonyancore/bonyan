using System.Data;
using System.Data.Common;
using Construction.Kernel;
using Construction.Kernel.Identity;
using Construction.Kernel.Tenancy;
using Construction.Platform.Contracts.Identity;
using Construction.Platform.Core.Domain.Identity;
using Construction.Platform.Core.Domain.Tenancy;
using Construction.Platform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Construction.Platform.Infrastructure.Identity;

/// <summary>
/// Production implementation of IIdentityService (Spec 01 §3 AuthService, §7.2, BR-PLT-02, BR-PLT-03, BR-PLT-05, BR-PLT-09).
/// </summary>
public sealed class IdentityService : IIdentityService
{
    private readonly PlatformDbContext _dbContext;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ITokenService _tokenService;
    private readonly IdentityOptions _options;
    private readonly ILogger<IdentityService> _logger;

    public IdentityService(
        PlatformDbContext dbContext,
        IPasswordHasher passwordHasher,
        ITokenService tokenService,
        IOptions<IdentityOptions> options,
        ILogger<IdentityService> logger)
    {
        _dbContext = dbContext;
        _passwordHasher = passwordHasher;
        _tokenService = tokenService;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<Result<AuthTokenResponse>> LoginAsync(
        LoginRequest request,
        string? ip = null,
        string? userAgent = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
        {
            _passwordHasher.VerifyDummy();
            return Result.Failure<AuthTokenResponse>(new Error("platform.invalid_credentials", "Invalid credentials."));
        }

        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        var now = DateTimeOffset.UtcNow;

        // 1. Pre-auth user lookup via SECURITY DEFINER function to cross RLS boundary safely
        var lookupUser = await LookupUserForLoginAsync(normalizedEmail, cancellationToken);
        if (lookupUser == null)
        {
            _passwordHasher.VerifyDummy();
            return Result.Failure<AuthTokenResponse>(new Error("platform.invalid_credentials", "Invalid credentials."));
        }

        // 2. Open an explicit transaction and acquire pessimistic row-level lock on the user record
        await using var transaction = await _dbContext.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);

        // Promote ambient tenant context within this explicit transaction
        await SetLocalTenantAsync(lookupUser.TenantId, cancellationToken);

        // Pessimistic row-lock on user row to serialize concurrent login attempts
        var user = await _dbContext.Users
            .FromSqlInterpolated($"SELECT * FROM platform.users WHERE id = {lookupUser.UserId} FOR UPDATE")
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(cancellationToken);

        if (user == null)
        {
            await transaction.RollbackAsync(cancellationToken);
            _passwordHasher.VerifyDummy();
            return Result.Failure<AuthTokenResponse>(new Error("platform.invalid_credentials", "Invalid credentials."));
        }

        // 3. Evaluate account lockout
        now = DateTimeOffset.UtcNow;
        if (user.IsLockedOut(now))
        {
            await transaction.CommitAsync(cancellationToken);
            return Result.Failure<AuthTokenResponse>(new Error("platform.account_locked", "Account is locked due to too many failed attempts."));
        }

        // Check if expired lockout can be cleared
        user.ClearExpiredLockout(now);

        // 4. Verify password hash using Argon2id
        var isPasswordValid = !string.IsNullOrWhiteSpace(user.PasswordHash) &&
                              _passwordHasher.VerifyPassword(request.Password, user.PasswordHash);

        if (!isPasswordValid)
        {
            // Record failure (BR-PLT-03: 5 failures within 15 min locks for 15 min)
            var wasLocked = user.RecordFailedLogin(now);
            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            if (wasLocked)
            {
                return Result.Failure<AuthTokenResponse>(new Error("platform.account_locked", "Account is locked due to too many failed attempts."));
            }

            return Result.Failure<AuthTokenResponse>(new Error("platform.invalid_credentials", "Invalid credentials."));
        }

        // 5. Password is verified: check tenant status (BR-PLT-09: password MUST be verified before 403)
        var tenant = await _dbContext.Tenants
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == user.TenantId.Value, cancellationToken);

        if (tenant == null || tenant.Status.Equals("suspended", StringComparison.OrdinalIgnoreCase))
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result.Failure<AuthTokenResponse>(new Error("platform.tenant_suspended", "Tenant workspace is suspended."));
        }

        // 6. Successful authentication
        user.RecordSuccessfulLogin(now);

        // Generate Access Token and Refresh Token
        var accessToken = _tokenService.GenerateAccessToken(
            UserId.From(user.Id),
            user.TenantId,
            user.Email,
            roles: Array.Empty<string>(),
            permissions: Array.Empty<string>());

        var rawRefreshToken = _tokenService.GenerateRefreshToken();
        var tokenHash = _tokenService.HashRefreshToken(rawRefreshToken);
        var familyId = Guid.NewGuid();
        var refreshTokenId = Guid.NewGuid();
        var expiresAt = now.AddDays(_options.RefreshTokenExpirationDays);

        var refreshToken = RefreshToken.Create(
            refreshTokenId,
            user.TenantId,
            user.Id,
            familyId,
            tokenHash,
            ClientKind.Web,
            now,
            expiresAt,
            deviceLabel: null,
            ip: ip,
            userAgent: userAgent);

        _dbContext.RefreshTokens.Add(refreshToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Result.Success(new AuthTokenResponse(
            accessToken,
            rawRefreshToken,
            _options.AccessTokenExpirationMinutes * 60,
            "Bearer"));
    }

    public async Task<Result<AuthTokenResponse>> RefreshTokenAsync(
        string rawRefreshToken,
        string? ip = null,
        string? userAgent = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(rawRefreshToken))
        {
            return Result.Failure<AuthTokenResponse>(new Error("platform.invalid_credentials", "Refresh token is required."));
        }

        var tokenHash = _tokenService.HashRefreshToken(rawRefreshToken);
        var now = DateTimeOffset.UtcNow;

        // 1. Open an explicit transaction
        await using var transaction = await _dbContext.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);

        // 2. Locate and row-lock the refresh token via minimized SECURITY DEFINER function
        var lockedToken = await LocateAndLockRefreshTokenAsync(tokenHash, cancellationToken);
        if (lockedToken == null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result.Failure<AuthTokenResponse>(new Error("platform.invalid_credentials", "Invalid refresh token."));
        }

        // Promote tenant context locally within this connection & transaction
        await SetLocalTenantAsync(lockedToken.TenantId, cancellationToken);

        // 3. Inspect reuse detection: presenting an already-rotated token revokes the entire family (BR-PLT-05)
        if (lockedToken.RotatedAt.HasValue)
        {
            _logger.LogWarning("Refresh token reuse detected for family {FamilyId}. Revoking family.", lockedToken.FamilyId);

            await _dbContext.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE platform.refresh_tokens SET revoked_at = {now}, revoke_reason = 'reuse_detected' WHERE family_id = {lockedToken.FamilyId} AND tenant_id = {lockedToken.TenantId} AND revoked_at IS NULL",
                cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return Result.Failure<AuthTokenResponse>(new Error("platform.token_revoked", "Refresh token has already been used and entire family has been revoked."));
        }

        // 4. Check if revoked or expired
        if (lockedToken.RevokedAt.HasValue)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result.Failure<AuthTokenResponse>(new Error("platform.token_revoked", "Refresh token has been revoked."));
        }

        if (now >= lockedToken.ExpiresAt)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result.Failure<AuthTokenResponse>(new Error("platform.token_expired", "Refresh token has expired."));
        }

        // 5. Check user and tenant status
        var user = await _dbContext.Users
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == lockedToken.UserId && u.TenantId == TenantId.From(lockedToken.TenantId), cancellationToken);

        if (user == null || user.Status != UserStatus.Active)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result.Failure<AuthTokenResponse>(new Error("platform.invalid_credentials", "User account is not active."));
        }

        var tenant = await _dbContext.Tenants
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == lockedToken.TenantId, cancellationToken);

        if (tenant == null || tenant.Status.Equals("suspended", StringComparison.OrdinalIgnoreCase))
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result.Failure<AuthTokenResponse>(new Error("platform.tenant_suspended", "Tenant workspace is suspended."));
        }

        // 6. Rotate current token and issue successor
        await _dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE platform.refresh_tokens SET rotated_at = {now} WHERE id = {lockedToken.Id} AND tenant_id = {lockedToken.TenantId}",
            cancellationToken);

        var newRawRefreshToken = _tokenService.GenerateRefreshToken();
        var newTokenHash = _tokenService.HashRefreshToken(newRawRefreshToken);
        var newExpiresAt = now.AddDays(_options.RefreshTokenExpirationDays);

        var successorToken = RefreshToken.Create(
            Guid.NewGuid(),
            user.TenantId,
            user.Id,
            lockedToken.FamilyId, // preserve family
            newTokenHash,
            ClientKind.Web,
            now,
            newExpiresAt,
            deviceLabel: null,
            ip: ip,
            userAgent: userAgent);

        _dbContext.RefreshTokens.Add(successorToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        var newAccessToken = _tokenService.GenerateAccessToken(
            UserId.From(user.Id),
            user.TenantId,
            user.Email,
            roles: Array.Empty<string>(),
            permissions: Array.Empty<string>());

        return Result.Success(new AuthTokenResponse(
            newAccessToken,
            newRawRefreshToken,
            _options.AccessTokenExpirationMinutes * 60,
            "Bearer"));
    }

    public async Task<Result> LogoutAsync(
        UserId userId,
        Guid? familyId = null,
        CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;

        if (_dbContext.Database.IsRelational())
        {
            if (familyId.HasValue && familyId.Value != Guid.Empty)
            {
                // Revoke specific session family
                await _dbContext.Database.ExecuteSqlInterpolatedAsync(
                    $"UPDATE platform.refresh_tokens SET revoked_at = {now}, revoke_reason = 'logout' WHERE user_id = {userId.Value} AND family_id = {familyId.Value} AND revoked_at IS NULL",
                    cancellationToken);
            }
            else
            {
                // Revoke all user refresh tokens on logout
                await _dbContext.Database.ExecuteSqlInterpolatedAsync(
                    $"UPDATE platform.refresh_tokens SET revoked_at = {now}, revoke_reason = 'logout' WHERE user_id = {userId.Value} AND revoked_at IS NULL",
                    cancellationToken);
            }
        }
        else
        {
            // In-memory or non-relational fallback (unit testing)
            var tokens = await _dbContext.RefreshTokens
                .Where(t => t.UserId == userId.Value && (!familyId.HasValue || t.FamilyId == familyId.Value) && t.RevokedAt == null)
                .ToListAsync(cancellationToken);

            foreach (var token in tokens)
            {
                token.Revoke(RevokeReason.Logout, now);
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        return Result.Success();
    }

    public async Task<Result<UserProfileResponse>> GetCurrentUserProfileAsync(
        UserId userId,
        TenantId tenantId,
        CancellationToken cancellationToken = default)
    {
        var user = await _dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId.Value && u.TenantId == tenantId, cancellationToken);

        if (user == null)
        {
            return Result.Failure<UserProfileResponse>(new Error("platform.user_not_found", "User profile not found."));
        }

        return Result.Success(new UserProfileResponse(
            user.Id,
            user.TenantId.Value,
            user.Email,
            user.FullNameAr,
            user.FullNameEn,
            user.Phone,
            user.JobTitle,
            user.Status.ToString().ToLowerInvariant(),
            user.Locale,
            user.Numerals,
            user.CalendarPref,
            user.TimeZone,
            user.MfaEnabled,
            user.LastLoginAt,
            Roles: Array.Empty<string>(),
            Permissions: Array.Empty<string>()));
    }

    private async Task SetLocalTenantAsync(Guid tenantId, CancellationToken ct)
    {
        var conn = _dbContext.Database.GetDbConnection();
        if (conn.State != ConnectionState.Open)
        {
            await conn.OpenAsync(ct);
        }

        await using var cmd = conn.CreateCommand();
        cmd.Transaction = _dbContext.Database.CurrentTransaction?.GetDbTransaction();
        cmd.CommandText = "SELECT set_config('app.tenant_id', @t, true)";
        var p = cmd.CreateParameter();
        p.ParameterName = "t";
        p.Value = tenantId.ToString();
        cmd.Parameters.Add(p);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private async Task<PreAuthUserRecord?> LookupUserForLoginAsync(string email, CancellationToken ct)
    {
        var conn = _dbContext.Database.GetDbConnection();
        if (conn.State != ConnectionState.Open)
        {
            await conn.OpenAsync(ct);
        }

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT id, tenant_id, email, password_hash, status, failed_login_count, locked_until, failed_login_window_started_at FROM platform.lookup_user_for_login(@email)";
        var p = cmd.CreateParameter();
        p.ParameterName = "email";
        p.Value = email;
        cmd.Parameters.Add(p);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (await reader.ReadAsync(ct))
        {
            return new PreAuthUserRecord(
                reader.GetGuid(0),
                reader.GetGuid(1),
                reader.GetString(2),
                reader.IsDBNull(3) ? null : reader.GetString(3),
                reader.GetString(4),
                reader.GetInt32(5),
                reader.IsDBNull(6) ? null : reader.GetDateTime(6),
                reader.IsDBNull(7) ? null : reader.GetDateTime(7));
        }

        return null;
    }

    private async Task<LockedRefreshTokenRecord?> LocateAndLockRefreshTokenAsync(string tokenHash, CancellationToken ct)
    {
        var conn = _dbContext.Database.GetDbConnection();
        if (conn.State != ConnectionState.Open)
        {
            await conn.OpenAsync(ct);
        }

        await using var cmd = conn.CreateCommand();
        cmd.Transaction = _dbContext.Database.CurrentTransaction?.GetDbTransaction();
        cmd.CommandText = "SELECT id, tenant_id, user_id, family_id, expires_at, rotated_at, revoked_at FROM platform.locate_and_lock_refresh_token(@hash)";
        var p = cmd.CreateParameter();
        p.ParameterName = "hash";
        p.Value = tokenHash;
        cmd.Parameters.Add(p);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (await reader.ReadAsync(ct))
        {
            return new LockedRefreshTokenRecord(
                reader.GetGuid(0),
                reader.GetGuid(1),
                reader.GetGuid(2),
                reader.GetGuid(3),
                reader.GetDateTime(4),
                reader.IsDBNull(5) ? null : reader.GetDateTime(5),
                reader.IsDBNull(6) ? null : reader.GetDateTime(6));
        }

        return null;
    }

    private sealed record PreAuthUserRecord(
        Guid UserId,
        Guid TenantId,
        string Email,
        string? PasswordHash,
        string Status,
        int FailedLoginCount,
        DateTimeOffset? LockedUntil,
        DateTimeOffset? FailedLoginWindowStartedAt);

    private sealed record LockedRefreshTokenRecord(
        Guid Id,
        Guid TenantId,
        Guid UserId,
        Guid FamilyId,
        DateTimeOffset ExpiresAt,
        DateTimeOffset? RotatedAt,
        DateTimeOffset? RevokedAt);
}
