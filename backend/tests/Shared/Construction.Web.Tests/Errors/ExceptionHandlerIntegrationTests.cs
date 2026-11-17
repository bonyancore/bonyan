using Construction.Kernel;
using Construction.Web.Errors;
using Construction.Web.RateLimiting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Data.Common;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.RateLimiting;
using Xunit;

namespace Construction.Web.Tests.Errors;

public sealed class ExceptionHandlerIntegrationTests : IAsyncLifetime
{
    private class FakePostgresException : DbException
    {
        public FakePostgresException(string message, string sqlState) : base(message)
        {
            SqlState = sqlState;
        }

        public override string SqlState { get; }
    }
    private IHost? _host;
    private HttpClient? _client;

    public async Task InitializeAsync()
    {
        _host = await new HostBuilder()
            .ConfigureWebHost(web =>
            {
                web.UseTestServer();
                web.ConfigureServices(services =>
                {
                    services.AddRouting();
                    services.AddWebInfrastructure();
                    services.AddRateLimiter(options =>
                    {
                        options.AddFixedWindowLimiter("test_policy", opt =>
                        {
                            opt.PermitLimit = 1;
                            opt.Window = TimeSpan.FromSeconds(10);
                            opt.QueueLimit = 0;
                        });
                        options.UseProblemDetailsOnRejected();
                    });
                });
                web.Configure(app =>
                {
                    app.Use(async (context, next) =>
                    {
                        if (context.Request.Path == "/throw-domain-under-ambient")
                        {
                            using var scope = new System.Transactions.TransactionScope(
                                System.Transactions.TransactionScopeOption.Required,
                                System.Transactions.TransactionScopeAsyncFlowOption.Enabled);
                            await next();
                            return;
                        }

                        await next();
                    });

                    app.UseExceptionHandler();
                    app.UseRouting();
                    app.UseRateLimiter();
                    app.UseEndpoints(endpoints =>
                    {
                        endpoints.MapGet("/throw-domain", () =>
                        {
                            throw new DomainRuleViolation("platform.last_admin",
                                "Cannot remove the last administrator.");
                        });

                        endpoints.MapGet("/throw-unhandled", () =>
                        {
                            throw new InvalidOperationException("Something exploded.");
                        });

                        endpoints.MapGet("/throw-sqlstate-40001", () =>
                        {
                            throw new FakePostgresException("could not serialize access", "40001");
                        });

                        endpoints.MapGet("/throw-nested-sqlstate-40001", () =>
                        {
                            var pgEx = new FakePostgresException("could not serialize access", "40001");
                            throw new DbUpdateException("An error occurred updating database", pgEx);
                        });

                        endpoints.MapGet("/throw-sqlstate-23505", () =>
                        {
                            throw new FakePostgresException("unique constraint violation", "23505");
                        });

                        endpoints.MapGet("/throw-sqlstate-40p01", () =>
                        {
                            throw new FakePostgresException("deadlock detected", "40P01");
                        });

                        endpoints.MapGet("/throw-domain-under-ambient", () =>
                        {
                            throw new DomainRuleViolation("platform.invalid_operation", "Business rule violation under ambient.");
                        });

                        endpoints.MapGet("/rate-limited", () => Results.Ok("passed"))
                            .RequireRateLimiting("test_policy");
                    });
                });
            })
            .StartAsync();

        _client = _host.GetTestClient();
    }

    public async Task DisposeAsync()
    {
        _client?.Dispose();
        if (_host is not null)
        {
            await _host.StopAsync();
            _host.Dispose();
        }
    }

    [Fact]
    public async Task DomainRuleViolation_Returns422WithProblemJson()
    {
        var response = await _client!.GetAsync("/throw-domain");

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("application/problem+json",
            response.Content.Headers.ContentType?.MediaType);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(422, body.GetProperty("status").GetInt32());

        var hasTopLevelCode = body.TryGetProperty("code", out var codeProp);
        if (!hasTopLevelCode)
        {
            hasTopLevelCode = body.GetProperty("extensions").TryGetProperty("code", out codeProp);
        }

        Assert.True(hasTopLevelCode);
        Assert.Equal("platform.last_admin", codeProp.GetString());
    }

    [Fact]
    public async Task UnhandledException_Returns500WithProblemJson()
    {
        var response = await _client!.GetAsync("/throw-unhandled");

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("application/problem+json",
            response.Content.Headers.ContentType?.MediaType);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(500, body.GetProperty("status").GetInt32());

        // Must not leak internal exception message
        var detail = body.TryGetProperty("detail", out var d) ? d.GetString() : null;
        Assert.DoesNotContain("exploded", detail ?? "", StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PostgresException_SqlState40001_Direct_Returns409Conflict()
    {
        var response = await _client!.GetAsync("/throw-sqlstate-40001");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(409, body.GetProperty("status").GetInt32());
        Assert.Equal("conflict", body.GetProperty("title").GetString());
        Assert.Equal("https://construction.example.com/errors/conflict", body.GetProperty("type").GetString());
        Assert.Equal("concurrency.serialization_failure", body.GetProperty("code").GetString());
    }

    [Fact]
    public async Task PostgresException_SqlState40001_NestedInDbUpdateException_Returns409Conflict()
    {
        var response = await _client!.GetAsync("/throw-nested-sqlstate-40001");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(409, body.GetProperty("status").GetInt32());
        Assert.Equal("conflict", body.GetProperty("title").GetString());
        Assert.Equal("concurrency.serialization_failure", body.GetProperty("code").GetString());
    }

    [Fact]
    public async Task PostgresException_SqlState23505_UniqueViolation_DoesNotMapToSerializationFailure_FallsBackSafely()
    {
        var response = await _client!.GetAsync("/throw-sqlstate-23505");

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(500, body.GetProperty("status").GetInt32());
        Assert.Equal("internal_error", body.GetProperty("code").GetString());
    }

    [Fact]
    public async Task PostgresException_SqlState40P01_Deadlock_DoesNotMapToSerializationFailure_FallsBackSafely()
    {
        var response = await _client!.GetAsync("/throw-sqlstate-40p01");

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(500, body.GetProperty("status").GetInt32());
        Assert.Equal("internal_error", body.GetProperty("code").GetString());
    }

    [Fact]
    public async Task DomainRuleViolation_UnderAmbientTransaction_MapsTo422_Not503()
    {
        var response = await _client!.GetAsync("/throw-domain-under-ambient");

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(422, body.GetProperty("status").GetInt32());
        Assert.Equal("platform.invalid_operation", body.GetProperty("code").GetString());
        Assert.Equal("Business rule violation under ambient.", body.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task RateLimiterMiddleware_Rejection_Returns429WithProblemDetails()
    {
        // 1st request succeeds
        var res1 = await _client!.GetAsync("/rate-limited");
        Assert.Equal(HttpStatusCode.OK, res1.StatusCode);

        // 2nd request within window is rejected by rate limiter
        var res2 = await _client!.GetAsync("/rate-limited");
        Assert.Equal(HttpStatusCode.TooManyRequests, res2.StatusCode);
        Assert.Equal("application/problem+json", res2.Content.Headers.ContentType?.MediaType);

        var body = await res2.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(429, body.GetProperty("status").GetInt32());
        Assert.Equal("rate_limit_exceeded", body.GetProperty("title").GetString());
        Assert.Equal("https://construction.example.com/errors/rate-limit-exceeded", body.GetProperty("type").GetString());
        Assert.Equal("rate_limit_exceeded", body.GetProperty("code").GetString());
    }
}
