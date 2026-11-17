using System.Text.Json.Serialization;

namespace Construction.Kernel.Pagination;

/// <summary>
/// Standard keyset pagination response envelope.
/// <para>
/// <see cref="NextCursor"/> is <c>null</c> when there are no further pages.
/// Consumers should treat a null cursor as the end of the result set.
/// </para>
/// </summary>
/// <typeparam name="T">The item type.</typeparam>
public sealed record KeysetPage<T>(
    IReadOnlyList<T> Items,
    string? NextCursor)
{
    /// <summary>
    /// Total count of items matching the base query (before cursor application).
    /// Populated only when the caller requested <c>withTotal=true</c>.
    /// Serialized as <c>"total"</c>; omitted entirely from JSON when null.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public long? Total { get; init; }
}
