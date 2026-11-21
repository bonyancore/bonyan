using System.Data;
using System.Text.Json;
using Construction.Kernel.Tenancy;
using Construction.Platform.Infrastructure.Persistence;
using Construction.Web.Idempotency;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace Construction.Platform.Infrastructure.Idempotency;

/// <summary>
/// PostgreSQL store for <c>platform.idempotency_keys</c> (spec 01 §4.13). Claims are atomic through
/// the unique <c>(tenant_id, principal_id, idem_key)</c> index and <c>ON CONFLICT</c>; expiry and
/// takeover use database time, never the application clock.
/// </summary>
public sealed class PostgresIdempotencyStore : IIdempotencyStore
{
    private static readonly JsonSerializerOptions HeaderJson = new(JsonSerializerDefaults.Web);

    private readonly string _connectionString;
    private readonly ITenantContext _tenantContext;

    public PostgresIdempotencyStore(IConfiguration configuration, ITenantContext tenantContext)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        _connectionString = configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException("Connection string 'Default' is not configured.");
        _tenantContext = tenantContext ?? throw new ArgumentNullException(nameof(tenantContext));
    }

    public async Task<bool> TryClaimAsync(
        IdempotencyScope scope,
        byte[] requestHash,
        TimeSpan lifetime,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(requestHash);

        await using var connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await SetTenantAsync(connection, transaction, scope.TenantId, cancellationToken).ConfigureAwait(false);

        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO platform.idempotency_keys
                (id, tenant_id, created_at, principal_type, principal_id, idem_key,
                 http_method, path_template, request_hash, status, expires_at)
            VALUES
                (@id, @tenantId, now(), @principalType, @principalId, @key,
                 @method, @path, @hash, 'in_progress',
                 now() + (@ttlSeconds * interval '1 second'))
            ON CONFLICT (tenant_id, principal_id, idem_key) DO UPDATE
            SET http_method = EXCLUDED.http_method,
                path_template = EXCLUDED.path_template,
                request_hash = EXCLUDED.request_hash,
                status = 'in_progress',
                response_status = NULL,
                response_headers = NULL,
                response_body = NULL,
                replay_too_large = false,
                created_at = now(),
                expires_at = EXCLUDED.expires_at
            WHERE platform.idempotency_keys.expires_at < now()
            RETURNING id;
            """;
        command.Parameters.AddWithValue("id", Guid.CreateVersion7());
        command.Parameters.AddWithValue("tenantId", scope.TenantId);
        command.Parameters.AddWithValue("principalType", scope.PrincipalType);
        command.Parameters.AddWithValue("principalId", scope.PrincipalId);
        command.Parameters.AddWithValue("key", scope.Key);
        command.Parameters.AddWithValue("method", scope.HttpMethod);
        command.Parameters.AddWithValue("path", scope.PathTemplate);
        command.Parameters.AddWithValue("hash", requestHash);
        command.Parameters.AddWithValue("ttlSeconds", (int)Math.Max(1, lifetime.TotalSeconds));

        var claimed = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) is not null;
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return claimed;
    }

    public async Task<IdempotencyRecord?> FindAsync(
        IdempotencyScope scope,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(scope);

        await using var connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await SetTenantAsync(connection, transaction, scope.TenantId, cancellationToken).ConfigureAwait(false);

        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            SELECT status, request_hash, response_status, response_headers::text, response_body,
                   replay_too_large, created_at, expires_at
            FROM platform.idempotency_keys
            WHERE tenant_id = @tenantId AND principal_id = @principalId AND idem_key = @key;
            """;
        command.Parameters.AddWithValue("tenantId", scope.TenantId);
        command.Parameters.AddWithValue("principalId", scope.PrincipalId);
        command.Parameters.AddWithValue("key", scope.Key);

        IdempotencyRecord? record = null;
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
        {
            if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                record = ReadRecord(reader);
            }
        }

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return record;
    }

    public async Task<bool> TryTakeOverAsync(
        IdempotencyScope scope,
        TimeSpan staleAfter,
        TimeSpan lifetime,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(scope);

        await using var connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await SetTenantAsync(connection, transaction, scope.TenantId, cancellationToken).ConfigureAwait(false);

        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            UPDATE platform.idempotency_keys
            SET created_at = now(),
                expires_at = now() + (@ttlSeconds * interval '1 second')
            WHERE tenant_id = @tenantId
              AND principal_id = @principalId
              AND idem_key = @key
              AND status = 'in_progress'
              AND created_at < now() - (@staleSeconds * interval '1 second')
            RETURNING id;
            """;
        command.Parameters.AddWithValue("tenantId", scope.TenantId);
        command.Parameters.AddWithValue("principalId", scope.PrincipalId);
        command.Parameters.AddWithValue("key", scope.Key);
        command.Parameters.AddWithValue("ttlSeconds", (int)Math.Max(1, lifetime.TotalSeconds));
        command.Parameters.AddWithValue("staleSeconds", (int)Math.Max(1, staleAfter.TotalSeconds));

        var takenOver = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) is not null;
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return takenOver;
    }

    public async Task CompleteAsync(
        IdempotencyScope scope,
        int statusCode,
        string? contentType,
        byte[]? body,
        IReadOnlyDictionary<string, string> headers,
        bool tooLarge,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(headers);

        await using var connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await SetTenantAsync(connection, transaction, scope.TenantId, cancellationToken).ConfigureAwait(false);

        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            UPDATE platform.idempotency_keys
            SET status = 'completed',
                response_status = @status,
                response_headers = @headers::jsonb,
                response_body = @body,
                replay_too_large = @tooLarge
            WHERE tenant_id = @tenantId AND principal_id = @principalId AND idem_key = @key;
            """;
        command.Parameters.AddWithValue("tenantId", scope.TenantId);
        command.Parameters.AddWithValue("principalId", scope.PrincipalId);
        command.Parameters.AddWithValue("key", scope.Key);
        command.Parameters.AddWithValue("status", statusCode);
        command.Parameters.AddWithValue("headers", JsonSerializer.Serialize(headers, HeaderJson));
        command.Parameters.AddWithValue("body", (object?)body ?? DBNull.Value);
        command.Parameters.AddWithValue("tooLarge", tooLarge);

        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task ReleaseAsync(IdempotencyScope scope, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(scope);

        await using var connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await SetTenantAsync(connection, transaction, scope.TenantId, cancellationToken).ConfigureAwait(false);

        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            DELETE FROM platform.idempotency_keys
            WHERE tenant_id = @tenantId AND principal_id = @principalId AND idem_key = @key
              AND status = 'in_progress';
            """;
        command.Parameters.AddWithValue("tenantId", scope.TenantId);
        command.Parameters.AddWithValue("principalId", scope.PrincipalId);
        command.Parameters.AddWithValue("key", scope.Key);

        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<int> PurgeExpiredAsync(int batchSize, CancellationToken cancellationToken = default)
    {
        if (!_tenantContext.HasTenant)
        {
            return 0;
        }

        var tenantId = _tenantContext.Current.Value;

        await using var connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await SetTenantAsync(connection, transaction, tenantId, cancellationToken).ConfigureAwait(false);

        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            DELETE FROM platform.idempotency_keys
            WHERE id IN (
                SELECT id FROM platform.idempotency_keys
                WHERE tenant_id = @tenantId AND expires_at < now()
                LIMIT @batchSize
            );
            """;
        command.Parameters.AddWithValue("tenantId", tenantId);
        command.Parameters.AddWithValue("batchSize", Math.Max(1, batchSize));

        var removed = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return removed;
    }

    private async Task<NpgsqlConnection> OpenAsync(CancellationToken cancellationToken)
    {
        var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        return connection;
    }

    private static async Task SetTenantAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT set_config('app.tenant_id', @tenantId, true);";
        command.Parameters.AddWithValue("tenantId", tenantId.ToString());
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static IdempotencyRecord ReadRecord(NpgsqlDataReader reader)
    {
        var headersJson = reader.IsDBNull(3) ? null : reader.GetString(3);
        var headers = string.IsNullOrWhiteSpace(headersJson)
            ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            : JsonSerializer.Deserialize<Dictionary<string, string>>(headersJson, HeaderJson)
              ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        return new IdempotencyRecord(
            reader.GetString(0),
            (byte[])reader[1],
            reader.IsDBNull(2) ? null : reader.GetInt32(2),
            headers.TryGetValue("Content-Type", out var contentType) ? contentType : null,
            reader.IsDBNull(4) ? null : (byte[])reader[4],
            reader.GetBoolean(5),
            headers,
            reader.GetFieldValue<DateTimeOffset>(6),
            reader.GetFieldValue<DateTimeOffset>(7));
    }
}
