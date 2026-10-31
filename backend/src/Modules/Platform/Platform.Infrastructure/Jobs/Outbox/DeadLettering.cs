namespace Construction.Platform.Infrastructure.Jobs.Outbox;

/// <summary>
/// Produces bounded dead-letter metadata: a coarse category, the exception class (spec 01 §4.5
/// <c>error_class</c>) and a short summary. Exception messages and payload data are never persisted,
/// because they routinely contain client data (S13).
/// </summary>
public static class DeadLettering
{
    public static (string Category, string ErrorClass, string Summary) Summarise(Exception exception)
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
        return (category, exception.GetType().Name, summary.Length > 500 ? summary[..500] : summary);
    }
}
