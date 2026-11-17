using System.Linq.Expressions;
using Construction.Kernel.Pagination;
using Construction.Kernel.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;

namespace Construction.Persistence.Pagination;

/// <summary>
/// EF Core extension for keyset (cursor-based) pagination against a
/// <c>(created_at DESC, id DESC)</c> index.
/// </summary>
public static class EfKeysetQueryExtensions
{
    public const int MinPageSize = 1;
    public const int MaxPageSize = 200;

    /// <summary>
    /// Applies keyset pagination to a query ordered by <c>(created_at DESC, id DESC)</c>.
    /// Strictly rejects invalid page sizes outside [1, 200].
    /// </summary>
    public static Task<KeysetPage<TResult>> ToKeysetPageAsync<TSource, TResult>(
        this IQueryable<TSource> source,
        Expression<Func<TSource, Guid>> idExpression,
        string? afterCursor,
        int pageSize,
        Func<TSource, TResult> selector,
        CancellationToken cancellationToken = default)
        where TSource : IAuditableEntity
    {
        return source.ToKeysetPageAsync(
            idExpression,
            afterCursor,
            pageSize,
            selector,
            withTotal: false,
            cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Applies keyset pagination to a query ordered by <c>(created_at DESC, id DESC)</c>,
    /// optionally calculating the total count of matching items before cursor application.
    /// Strictly rejects invalid page sizes outside [1, 200].
    /// </summary>
    public static async Task<KeysetPage<TResult>> ToKeysetPageAsync<TSource, TResult>(
        this IQueryable<TSource> source,
        Expression<Func<TSource, Guid>> idExpression,
        string? afterCursor,
        int pageSize,
        Func<TSource, TResult> selector,
        bool withTotal,
        CancellationToken cancellationToken = default)
        where TSource : IAuditableEntity
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(idExpression);
        ArgumentNullException.ThrowIfNull(selector);

        if (pageSize < MinPageSize || pageSize > MaxPageSize)
        {
            throw new ArgumentOutOfRangeException(
                nameof(pageSize),
                pageSize,
                $"Page size must be between {MinPageSize} and {MaxPageSize}.");
        }

        long? total = null;
        if (withTotal)
        {
            total = await source.LongCountAsync(cancellationToken);
        }

        var idSelector = idExpression.Compile();

        if (afterCursor is not null)
        {
            if (string.IsNullOrWhiteSpace(afterCursor) || !PageCursor.TryDecode(afterCursor, out var cursor) || cursor is null)
            {
                throw new ArgumentException(
                    "The supplied afterCursor is malformed or invalid.",
                    nameof(afterCursor));
            }

            source = source.Where(BuildSeekPredicate(idExpression, cursor.CreatedAt, cursor.Id));
        }

        var rawItems = await source
            .OrderByDescending(e => e.CreatedAt)
            .ThenByDescending(idExpression)
            .Take(pageSize + 1)
            .ToListAsync(cancellationToken);

        string? nextCursor = null;

        if (rawItems.Count > pageSize)
        {
            rawItems.RemoveAt(pageSize);
            var last = rawItems[^1];
            nextCursor = new PageCursor(last.CreatedAt, idSelector(last)).Encode();
        }

        return new KeysetPage<TResult>(
            rawItems.Select(selector).ToList(),
            nextCursor)
        {
            Total = total
        };
    }

    /// <summary>
    /// Builds the composite keyset seek predicate for <c>(created_at DESC, id DESC)</c>:
    /// <c>e.CreatedAt &lt; seek.CreatedAt || (e.CreatedAt == seek.CreatedAt &amp;&amp; e.Id &lt; seek.Id)</c>
    /// Uses ReplacingExpressionVisitor to safely rebind the caller's ID expression.
    /// </summary>
    private static Expression<Func<TSource, bool>> BuildSeekPredicate<TSource>(
        Expression<Func<TSource, Guid>> idExpression,
        DateTime seekCreatedAt,
        Guid seekId)
        where TSource : IAuditableEntity
    {
        var param = Expression.Parameter(typeof(TSource), "e");

        // Safely rebind idExpression's parameter to our new parameter
        var idBody = ReplacingExpressionVisitor.Replace(
            idExpression.Parameters[0],
            param,
            idExpression.Body);

        var createdAtProp = Expression.Property(param, nameof(IAuditableEntity.CreatedAt));
        var seekCreatedAtConst = Expression.Constant(seekCreatedAt, typeof(DateTime));
        var seekIdConst = Expression.Constant(seekId, typeof(Guid));

        var createdAtLess = Expression.LessThan(createdAtProp, seekCreatedAtConst);
        var createdAtEq = Expression.Equal(createdAtProp, seekCreatedAtConst);

        var compareToMethod = typeof(Guid).GetMethod(
            nameof(Guid.CompareTo),
            [typeof(Guid)])!;

        var compareCall = Expression.Call(idBody, compareToMethod, seekIdConst);
        var idLess = Expression.LessThan(compareCall, Expression.Constant(0));

        var secondarySeek = Expression.AndAlso(createdAtEq, idLess);
        var seekBody = Expression.OrElse(createdAtLess, secondarySeek);

        return Expression.Lambda<Func<TSource, bool>>(seekBody, param);
    }
}
