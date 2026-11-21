using System.Data.Common;
using Construction.Kernel.Tenancy;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Construction.Persistence;

/// <summary>
/// CC-01: the connection-open interceptor sets <c>app.tenant_id</c> at session level. That is not
/// enough when a pooled connection is reused or when a Worker iterates tenants on one connection.
/// This interceptor re-applies the setting with <c>is_local = true</c> as soon as a transaction
/// starts, so the value can never outlive the transaction.
/// </summary>
public sealed class TenantTransactionInterceptor(ITenantContext tenant) : DbTransactionInterceptor
{
    private const string SentinelTenantId = "00000000-0000-0000-0000-000000000000";

    public override DbTransaction TransactionStarted(
        DbConnection connection,
        TransactionEndEventData eventData,
        DbTransaction result)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(result);

        using var cmd = CreateCommand(connection, result);
        cmd.ExecuteNonQuery();
        return result;
    }

    public override async ValueTask<DbTransaction> TransactionStartedAsync(
        DbConnection connection,
        TransactionEndEventData eventData,
        DbTransaction result,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(result);

        await using var cmd = CreateCommand(connection, result);
        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        return result;
    }

    private DbCommand CreateCommand(DbConnection connection, DbTransaction transaction)
    {
        var cmd = connection.CreateCommand();
        cmd.Transaction = transaction;
        cmd.CommandText = "select set_config('app.tenant_id', @t, true)";
        var parameter = cmd.CreateParameter();
        parameter.ParameterName = "t";
        parameter.Value = tenant.HasTenant ? tenant.Current.ToString() : SentinelTenantId;
        cmd.Parameters.Add(parameter);
        return cmd;
    }
}
