namespace Construction.Platform.Infrastructure.Jobs.Outbox;

/// <summary>Produces bounded error metadata without persisting exception messages or payload data.</summary>
public static class DeadLettering
{
    public static (string Category, string Summary) Summarise(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        var category = exception switch
        {
            Npgsql.NpgsqlException => "database",
            System.Net.Http.HttpRequestException or TimeoutException => "network",
            System.Text.Json.JsonException => "serialization",
            _ => "application"
        };

        var summary = $"{category}: delivery failed ({exception.GetType().Name}).";
        return (category, summary.Length > 500 ? summary[..500] : summary);
    }
}
