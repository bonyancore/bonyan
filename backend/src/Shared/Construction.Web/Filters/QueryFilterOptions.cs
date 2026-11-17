namespace Construction.Web.Filters;

/// <summary>
/// Base record for validated list-query parameters.
/// Subclass per endpoint, override <see cref="AllowedSortFields"/> to whitelist permitted sort targets.
/// </summary>
public abstract record QueryFilterOptions
{
    private const int DefaultPageSize = 50;
    public const int MinPageSize = 1;
    public const int MaxPageSize = 200;

    /// <summary>Number of items per page. Defaults to 50. Must be between 1 and 200.</summary>
    public int PageSize { get; init; } = DefaultPageSize;

    /// <summary>Opaque keyset cursor from a previous response. Null for the first page.</summary>
    public string? After { get; init; }

    /// <summary>Field name to sort by. Must be a member of <see cref="AllowedSortFields"/>.</summary>
    public string? SortField { get; init; }

    /// <summary>Sort direction: <c>"asc"</c> or <c>"desc"</c>. Defaults to descending when null.</summary>
    public string? SortDirection { get; init; }

    /// <summary>
    /// The set of field names permitted as sort targets for this endpoint.
    /// Override in subclasses to restrict the whitelist.
    /// </summary>
    protected abstract IReadOnlySet<string> AllowedSortFields { get; }

    /// <summary>
    /// Validates the options and returns a <see cref="QueryValidationResult"/>.
    /// Rejects invalid page size and malformed cursor with 422 errors.
    /// </summary>
    public QueryValidationResult Validate()
    {
        var errors = new List<string>();

        if (PageSize < MinPageSize || PageSize > MaxPageSize)
        {
            errors.Add($"pageSize must be between {MinPageSize} and {MaxPageSize}. Received: {PageSize}.");
        }

        if (After is not null)
        {
            if (string.IsNullOrWhiteSpace(After) || !Construction.Kernel.Pagination.PageCursor.TryDecode(After, out _))
            {
                errors.Add("The 'after' cursor is invalid or malformed.");
            }
        }

        if (!string.IsNullOrWhiteSpace(SortField) && !AllowedSortFields.Contains(SortField))
        {
            var allowed = string.Join(", ", AllowedSortFields.Order());
            errors.Add($"sortField '{SortField}' is not permitted. Allowed: {allowed}.");
        }

        if (!string.IsNullOrWhiteSpace(SortDirection) &&
            SortDirection is not ("asc" or "desc"))
        {
            errors.Add("sortDirection must be 'asc' or 'desc'.");
        }

        return errors.Count == 0
            ? QueryValidationResult.Success
            : QueryValidationResult.Failure(errors);
    }
}

/// <summary>
/// Result of <see cref="QueryFilterOptions.Validate()"/>.
/// </summary>
public sealed record QueryValidationResult(bool IsValid, IReadOnlyList<string> Errors)
{
    public static readonly QueryValidationResult Success = new(true, []);

    public static QueryValidationResult Failure(IReadOnlyList<string> errors)
    {
        ArgumentNullException.ThrowIfNull(errors);
        return new QueryValidationResult(false, errors);
    }
}
