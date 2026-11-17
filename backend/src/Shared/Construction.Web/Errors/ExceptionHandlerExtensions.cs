using System.Data.Common;
using System.Text.Json;
using System.Transactions;
using Construction.Kernel;
using Construction.Kernel.Persistence;
using Construction.Web.ETag;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace Construction.Web.Errors;

/// <summary>
/// Registers the global RFC 7807/9457 exception-to-ProblemDetails handler.
/// </summary>
public static class ExceptionHandlerExtensions
{
    public static IApplicationBuilder UseWebExceptionHandler(this IApplicationBuilder app)
        => app.UseExceptionHandler();

    internal sealed class ConstructionExceptionHandler : IExceptionHandler
    {
        public async ValueTask<bool> TryHandleAsync(
            HttpContext httpContext,
            Exception exception,
            CancellationToken cancellationToken)
        {
            var (statusCode, problem) = await MapExceptionAsync(httpContext, exception, cancellationToken);

            httpContext.Response.StatusCode = statusCode;
            await httpContext.Response.WriteAsJsonAsync(
                problem,
                options: null,
                contentType: "application/problem+json",
                cancellationToken: cancellationToken);

            return true;
        }

        private static async ValueTask<(int StatusCode, ProblemDetails Problem)> MapExceptionAsync(
            HttpContext httpContext,
            Exception exception,
            CancellationToken cancellationToken)
        {
            // 0. PostgreSQL serialization failure (SQLSTATE 40001 under REPEATABLE READ / SERIALIZABLE)
            if (HasPostgresSqlState(exception, "40001"))
            {
                var serializationProblem = new ProblemDetails
                {
                    Status = StatusCodes.Status409Conflict,
                    Title = "conflict",
                    Type = "https://construction.example.com/errors/conflict",
                    Detail = "The request could not be completed due to a serialization failure. Retry the operation."
                };
                serializationProblem.Extensions["code"] = "concurrency.serialization_failure";
                return (StatusCodes.Status409Conflict, serializationProblem);
            }

            // 1. Handle DbUpdateConcurrencyException
            if (exception is DbUpdateConcurrencyException concurrencyEx)
            {
                return await HandleConcurrencyExceptionAsync(httpContext, concurrencyEx, cancellationToken);
            }

            // 2. Handle DomainRuleViolation (mapped to 422 Validation)
            if (exception is DomainRuleViolation drv)
            {
                var domainError = Error.Validation(drv.Code, drv.Message);
                return domainError.ToProblemDetails();
            }

            // 3. Unhandled exceptions: safe 500 without internal detail leakage
            var serverError = Error.Failure("internal_error", "An unexpected error occurred.");
            return serverError.ToProblemDetails();
        }

        private static async ValueTask<(int StatusCode, ProblemDetails Problem)> HandleConcurrencyExceptionAsync(
            HttpContext httpContext,
            DbUpdateConcurrencyException concurrencyEx,
            CancellationToken cancellationToken)
        {
            // Fail closed if an explicit EF transaction is still open on any affected entry's context
            var hasOpenEfTransaction = concurrencyEx.Entries
                .Any(e => e.Context.Database.CurrentTransaction is not null);

            if (hasOpenEfTransaction)
            {
                return ConcurrencyRecoveryFailure("A concurrency conflict occurred inside an open transaction. Recovery is not possible; the transaction must be handled locally.");
            }

            // Fail closed if an active ambient TransactionScope exists
            if (Transaction.Current is not null)
            {
                return ConcurrencyRecoveryFailure("A concurrency conflict occurred inside an active ambient transaction. Recovery is not possible; the ambient transaction must be handled locally.");
            }

            var isIfMatchEnforced = httpContext.Items.ContainsKey(HttpContextIfMatchAccessor.EnforcedMarkerItemName);

            // If the route was NOT guarded by If-Match, collisions return standard 409 Conflict
            if (!isIfMatchEnforced)
            {
                var conflictProblem = new ProblemDetails
                {
                    Status = StatusCodes.Status409Conflict,
                    Title = "conflict",
                    Type = "https://construction.example.com/errors/conflict",
                    Detail = "A concurrency conflict occurred on the requested resource."
                };
                conflictProblem.Extensions["code"] = "concurrency.conflict";
                return (StatusCodes.Status409Conflict, conflictProblem);
            }

            // Route was guarded by If-Match: perform strict target identification and database recovery
            var targetKey = httpContext.Items[HttpContextIfMatchAccessor.TargetKeyItemName] as ConcurrencyTargetKey;
            if (targetKey == null)
            {
                return ConcurrencyRecoveryFailure("A concurrency conflict occurred, but the affected resource could not be identified.");
            }

            // Require exactly one trusted entry match
            EntityEntry? matchedEntry = null;

            foreach (var entry in concurrencyEx.Entries)
            {
                if (!targetKey.ClrType.IsAssignableFrom(entry.Entity.GetType()))
                {
                    continue;
                }

                var primaryKey = entry.Metadata.FindPrimaryKey();
                if (primaryKey == null || primaryKey.Properties.Count != 1)
                {
                    // Composite or missing primary key cannot be safely matched
                    continue;
                }

                var pkProp = primaryKey.Properties[0];
                var originalVal = entry.Property(pkProp.Name).OriginalValue;

                if (originalVal != null && Equals(originalVal, targetKey.PrimaryKeyValue))
                {
                    if (matchedEntry != null)
                    {
                        // Multiple ambiguous matches detected
                        return ConcurrencyRecoveryFailure("A concurrency conflict occurred across multiple entities and could not be resolved uniquely.");
                    }

                    matchedEntry = entry;
                }
            }

            if (matchedEntry == null)
            {
                return ConcurrencyRecoveryFailure("The conflicted entity did not match the intended request target.");
            }

            // Attempt to retrieve fresh database values
            PropertyValues? dbValues;
            try
            {
                dbValues = await matchedEntry.GetDatabaseValuesAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                // Recovery query failed (connection dropped, aborted transaction, etc.)
                return ConcurrencyRecoveryFailure("A concurrency conflict occurred, but the current state could not be verified due to a database error.");
            }

            // Decision A: If entity was deleted concurrently, return 412 with deletion detail and omit currentVersion
            if (dbValues == null)
            {
                var deletedProblem = new ProblemDetails
                {
                    Status = StatusCodes.Status412PreconditionFailed,
                    Title = "precondition_failed",
                    Type = "https://construction.example.com/errors/precondition-failed",
                    Detail = "The resource was deleted by another transaction."
                };
                deletedProblem.Extensions["code"] = "concurrency.precondition_failed";
                return (StatusCodes.Status412PreconditionFailed, deletedProblem);
            }

            // Decision B: Resource still exists; return 412 with complete opaque strong ETag in currentVersion
            if (!dbValues.Properties.Any(p => p.Name == nameof(IConcurrencyVersioned.RowVersion)))
            {
                return ConcurrencyRecoveryFailure("Entity does not support optimistic concurrency versioning.");
            }

            var freshXmin = (uint)dbValues[nameof(IConcurrencyVersioned.RowVersion)]!;
            var currentEtag = ETagHelper.ToETag(freshXmin);

            var modifiedProblem = new ProblemDetails
            {
                Status = StatusCodes.Status412PreconditionFailed,
                Title = "precondition_failed",
                Type = "https://construction.example.com/errors/precondition-failed",
                Detail = "The resource has been modified by another transaction. The current version must be fetched before retrying."
            };
            modifiedProblem.Extensions["code"] = "concurrency.precondition_failed";
            modifiedProblem.Extensions["currentVersion"] = currentEtag;

            return (StatusCodes.Status412PreconditionFailed, modifiedProblem);
        }

        private static bool HasPostgresSqlState(Exception? exception, string expectedSqlState)
        {
            while (exception is not null)
            {
                // Inspect DbException / PostgresException SqlState property via standard DbException.SqlState or reflection
                if (exception is DbException dbEx &&
                    string.Equals(dbEx.SqlState, expectedSqlState, StringComparison.Ordinal))
                {
                    return true;
                }

                var prop = exception.GetType().GetProperty("SqlState");
                if (prop != null && prop.PropertyType == typeof(string))
                {
                    var val = prop.GetValue(exception) as string;
                    if (string.Equals(val, expectedSqlState, StringComparison.Ordinal))
                    {
                        return true;
                    }
                }

                if (exception is AggregateException aggEx)
                {
                    foreach (var inner in aggEx.InnerExceptions)
                    {
                        if (HasPostgresSqlState(inner, expectedSqlState))
                        {
                            return true;
                        }
                    }
                }

                exception = exception.InnerException;
            }

            return false;
        }

        private static (int StatusCode, ProblemDetails Problem) ConcurrencyRecoveryFailure(string detail)
        {
            var problem = new ProblemDetails
            {
                Status = StatusCodes.Status503ServiceUnavailable,
                Title = "service_unavailable",
                Type = "https://construction.example.com/errors/service-unavailable",
                Detail = detail
            };
            problem.Extensions["code"] = "concurrency.recovery_failed";
            return (StatusCodes.Status503ServiceUnavailable, problem);
        }
    }
}
