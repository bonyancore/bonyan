using System.Data;
using System.Data.Common;
using Construction.Platform.Core.Domain.Jobs;

namespace Construction.Platform.Infrastructure.Jobs.Runner;

/// <summary>
/// PostgreSQL job-run store for <c>platform.job_runs</c>. Every statement runs in a transaction that
/// sets the tenant first, so the forced RLS policy applies exactly as it does for requests (S1/S2).
/// The unique index <c>uq_job_runs_tenant_job_run_key</c> is the duplicate-window guard (BR-PLT-140).
/// </summary>
public sealed class PostgresJobRunStore : IJobRunStore
{
    private readonly Func<CancellationToken, Task<DbConnection>> _connectionFactory;

    public PostgresJobRunStore(Func<CancellationToken, Task<DbConnection>> connectionFactory)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
    }

    public async Task<bool> TryInsertAsync(JobRun run, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(run);

        await using var connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await SetTenantAsync(connection, transaction, run.TenantId, cancellationToken).ConfigureAwait(false);

        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO platform.job_runs
                (id, tenant_id, job, run_key, trigger, queue, started_at, finished_at, status, error_code, attempts)
            VALUES (@id, @tenantId, @job, @runKey, @trigger, @queue, @startedAt, @finishedAt, @status, @errorCode, @attempts)
            ON CONFLICT (tenant_id, job, run_key) DO NOTHING
            RETURNING id;
            """;
        AddParameter(command, "id", run.Id);
        AddParameter(command, "tenantId", run.TenantId);
        AddParameter(command, "job", run.Job);
        AddParameter(command, "runKey", run.RunKey);
        AddParameter(command, "trigger", run.Trigger);
        AddParameter(command, "queue", run.Queue);
        AddParameter(command, "startedAt", run.StartedAt);
        AddParameter(command, "finishedAt", (object?)run.FinishedAt ?? DBNull.Value);
        AddParameter(command, "status", run.Status);
        AddParameter(command, "errorCode", (object?)run.ErrorCode ?? DBNull.Value);
        AddParameter(command, "attempts", run.Attempts);

        var inserted = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return inserted is not null;
    }

    public async Task UpdateAsync(JobRun run, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(run);

        await using var connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await SetTenantAsync(connection, transaction, run.TenantId, cancellationToken).ConfigureAwait(false);

        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            UPDATE platform.job_runs
            SET finished_at = @finishedAt, status = @status, error_code = @errorCode, attempts = @attempts
            WHERE id = @id AND tenant_id = @tenantId;
            """;
        AddParameter(command, "id", run.Id);
        AddParameter(command, "tenantId", run.TenantId);
        AddParameter(command, "finishedAt", (object?)run.FinishedAt ?? DBNull.Value);
        AddParameter(command, "status", run.Status);
        AddParameter(command, "errorCode", (object?)run.ErrorCode ?? DBNull.Value);
        AddParameter(command, "attempts", run.Attempts);

        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<JobRun?> GetAsync(Guid tenantId, Guid id, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(tenantId, Guid.Empty);

        await using var connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await SetTenantAsync(connection, transaction, tenantId, cancellationToken).ConfigureAwait(false);

        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            SELECT id, tenant_id, job, run_key, trigger, queue, started_at, finished_at, status, error_code, attempts
            FROM platform.job_runs WHERE id = @id AND tenant_id = @tenantId;
            """;
        AddParameter(command, "id", id);
        AddParameter(command, "tenantId", tenantId);

        JobRun? run;
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
        {
            run = await reader.ReadAsync(cancellationToken).ConfigureAwait(false) ? ReadRun(reader) : null;
        }

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return run;
    }

    public async Task<bool> ExistsForWindowAsync(
        Guid tenantId,
        string job,
        string runKey,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await SetTenantAsync(connection, transaction, tenantId, cancellationToken).ConfigureAwait(false);

        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            SELECT 1 FROM platform.job_runs
            WHERE tenant_id = @tenantId AND job = @job AND run_key = @runKey LIMIT 1;
            """;
        AddParameter(command, "tenantId", tenantId);
        AddParameter(command, "job", job);
        AddParameter(command, "runKey", runKey);

        var found = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return found is not null;
    }

    private async Task<DbConnection> OpenAsync(CancellationToken cancellationToken)
    {
        var connection = await _connectionFactory(cancellationToken).ConfigureAwait(false);
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        }

        return connection;
    }

    private static async Task SetTenantAsync(
        DbConnection connection,
        DbTransaction transaction,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT set_config('app.tenant_id', @tenantId, true);";
        AddParameter(command, "tenantId", tenantId.ToString());
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static void AddParameter(DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }

    private static JobRun ReadRun(DbDataReader reader) => new()
    {
        Id = reader.GetGuid(0),
        TenantId = reader.GetGuid(1),
        Job = reader.GetString(2),
        RunKey = reader.GetString(3),
        Trigger = reader.GetString(4),
        Queue = reader.GetString(5),
        StartedAt = reader.GetFieldValue<DateTimeOffset>(6),
        FinishedAt = reader.IsDBNull(7) ? null : reader.GetFieldValue<DateTimeOffset>(7),
        Status = reader.GetString(8),
        ErrorCode = reader.IsDBNull(9) ? null : reader.GetString(9),
        Attempts = reader.GetInt32(10)
    };
}
