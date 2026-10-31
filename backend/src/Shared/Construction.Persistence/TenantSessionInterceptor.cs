using System.Data.Common;
using Construction.Kernel.Tenancy;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Construction.Persistence;

public sealed class TenantSessionInterceptor(ITenantContext tenant) : DbConnectionInterceptor
{
    public override async Task ConnectionOpenedAsync(
        DbConnection connection, ConnectionEndEventData eventData,
        CancellationToken cancellationToken = default)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "select set_config('app.tenant_id', @t, false)";
        var p = cmd.CreateParameter();
        p.ParameterName = "t";
        p.Value = tenant.HasTenant ? tenant.Current.ToString() : string.Empty;
        cmd.Parameters.Add(p);
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
        => ConnectionOpenedAsync(connection, eventData).GetAwaiter().GetResult();
}
