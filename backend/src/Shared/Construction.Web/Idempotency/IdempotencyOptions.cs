using Microsoft.Extensions.Configuration;

namespace Construction.Web.Idempotency;

/// <summary>Configuration for the idempotency middleware (spec 01 §4.13 defaults).</summary>
public sealed record IdempotencyOptions
{
    public TimeSpan KeyLifetime { get; init; } = TimeSpan.FromHours(24);

    public TimeSpan InProgressTakeover { get; init; } = TimeSpan.FromSeconds(60);

    public int MaxStoredBodyBytes { get; init; } = 262_144;

    public int MaxRequestBodyBytes { get; init; } = 1_048_576;

    public string ClientHeader { get; init; } = "X-Client";

    public static IdempotencyOptions FromConfiguration(IConfiguration? configuration)
    {
        if (configuration is null)
        {
            return new IdempotencyOptions();
        }

        var defaults = new IdempotencyOptions();
        var hours = ReadInt(configuration, "Idempotency:KeyTtlHours", (int)defaults.KeyLifetime.TotalHours);
        var takeover = ReadInt(configuration, "Idempotency:InProgressTakeoverSeconds", 60);
        var stored = ReadInt(configuration, "Idempotency:MaxStoredBodyBytes", defaults.MaxStoredBodyBytes);
        var request = ReadInt(configuration, "Idempotency:MaxRequestBodyBytes", defaults.MaxRequestBodyBytes);
        var clientHeader = configuration["Idempotency:ClientHeader"];

        return new IdempotencyOptions
        {
            KeyLifetime = TimeSpan.FromHours(hours),
            InProgressTakeover = TimeSpan.FromSeconds(takeover),
            MaxStoredBodyBytes = stored,
            MaxRequestBodyBytes = request,
            ClientHeader = string.IsNullOrWhiteSpace(clientHeader) ? defaults.ClientHeader : clientHeader
        };
    }

    private static int ReadInt(IConfiguration configuration, string key, int fallback) =>
        int.TryParse(configuration[key], System.Globalization.NumberStyles.Integer,
            System.Globalization.CultureInfo.InvariantCulture, out var value) && value > 0
            ? value
            : fallback;
}
