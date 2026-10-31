using System.Data.Common;
using Construction.Kernel.Tenancy;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Construction.Persistence;

public sealed class TenantSessionInterceptor(ITenantContext tenant) : DbConnectionInterceptor
{
    private const string SentinelTenantId = "00000000-0000-0000-0000-000000000000";

    public override async Task ConnectionOpenedAsync(
        DbConnection connection, ConnectionEndEventData eventData,
        CancellationToken cancellationToken = default)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "select set_config('app.tenant_id', @t, false)";
        var p = cmd.CreateParameter();
        p.ParameterName = "t";

        if (tenant.HasTenant)
        {
            var current = tenant.Current;
            current.EnsureValid();
            p.Value = current.ToString();
        }
        else
        {
            p.Value = SentinelTenantId;
        }

        cmd.Parameters.Add(p);
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "select set_config('app.tenant_id', @t, false)";
        var p = cmd.CreateParameter();
        p.ParameterName = "t";

        if (tenant.HasTenant)
        {
            var current = tenant.Current;
            current.EnsureValid();
            p.Value = current.ToString();
        }
        else
        {
            p.Value = SentinelTenantId;
        }

        cmd.Parameters.Add(p);
        cmd.ExecuteNonQuery();
    }
}
