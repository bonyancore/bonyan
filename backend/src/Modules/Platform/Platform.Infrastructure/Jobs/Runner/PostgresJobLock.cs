using System.Data;
using System.Data.Common;

namespace Construction.Platform.Infrastructure.Jobs.Runner;

/// <summary>
/// pg_try_advisory_lock(hashtext(job||tenant)) for the duration of the returned lease.
/// </summary>
public sealed class PostgresJobLock : IJobLock
{
    private readonly Func<CancellationToken, Task<DbConnection>> _connectionFactory;

    public PostgresJobLock(Func<CancellationToken, Task<DbConnection>> connectionFactory)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
    }

    public async Task<IAsyncDisposable?> TryAcquireAsync(
        string jobName,
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var connection = await _connectionFactory(cancellationToken).ConfigureAwait(false);
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        }

        var key = $"{jobName}{tenantId:N}";
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT pg_try_advisory_lock(hashtext(@key))";
        var p = cmd.CreateParameter();
        p.ParameterName = "key";
        p.Value = key;
        cmd.Parameters.Add(p);

        var result = await cmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        var acquired = result is bool b && b;

        if (!acquired)
        {
            await connection.DisposeAsync().ConfigureAwait(false);
            return null;
        }

        return new AdvisoryLease(connection, key);
    }

    private sealed class AdvisoryLease(DbConnection connection, string key) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            try
            {
                await using var cmd = connection.CreateCommand();
                cmd.CommandText = "SELECT pg_advisory_unlock(hashtext(@key))";
                var p = cmd.CreateParameter();
                p.ParameterName = "key";
                p.Value = key;
                cmd.Parameters.Add(p);
                await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
            }
            finally
            {
                await connection.DisposeAsync().ConfigureAwait(false);
            }
        }
    }
}
