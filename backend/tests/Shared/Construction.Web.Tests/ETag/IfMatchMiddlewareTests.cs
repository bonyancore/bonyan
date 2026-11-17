using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Construction.Kernel.Persistence;
using Construction.Web.ETag;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Construction.Web.Tests.ETag;

public sealed class IfMatchMiddlewareTests : IAsyncLifetime
{
    private sealed class MockEntity : IConcurrencyVersioned
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public uint RowVersion { get; set; } = 42;
    }

    private IHost? _host;
    private HttpClient? _client;
    private readonly MockEntity _testEntity = new();

    public async Task InitializeAsync()
    {
        _host = await new HostBuilder()
            .ConfigureWebHost(web =>
            {
                web.UseTestServer();
                web.ConfigureServices(services =>
                {
                    services.AddHttpContextAccessor();
                    services.AddRouting();
                    services.AddWebInfrastructure();
                });
                web.Configure(app =>
                {
                    app.UseRouting();
                    app.UseEndpoints(endpoints =>
                    {
                        endpoints.MapPut("/items/{id}", (Guid id, IIfMatchAccessor accessor) =>
                        {
                            var evalResult = accessor.EvaluatePrecondition(_testEntity, id);
                            if (evalResult != null)
                            {
                                return evalResult; // returns 412
                            }

                            return Results.Ok(new { updated = true });
                        }).WithIfMatchRequired();
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
    public async Task MissingIfMatch_Returns428PreconditionRequired()
    {
        var response = await _client!.PutAsync($"/items/{_testEntity.Id}", null);
        Assert.Equal(HttpStatusCode.PreconditionRequired, response.StatusCode);
    }

    [Fact]
    public async Task EmptyWhitespaceIfMatch_Returns400BadRequest()
    {
        var request = new HttpRequestMessage(HttpMethod.Put, $"/items/{_testEntity.Id}");
        request.Headers.TryAddWithoutValidation("If-Match", "   ");

        var response = await _client!.SendAsync(request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UnquotedIfMatch_Returns400BadRequest()
    {
        var request = new HttpRequestMessage(HttpMethod.Put, $"/items/{_testEntity.Id}");
        request.Headers.TryAddWithoutValidation("If-Match", "42");

        var response = await _client!.SendAsync(request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task WildcardMixedWithTag_Returns400BadRequest()
    {
        var request = new HttpRequestMessage(HttpMethod.Put, $"/items/{_testEntity.Id}");
        request.Headers.TryAddWithoutValidation("If-Match", "*, \"42\"");

        var response = await _client!.SendAsync(request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ValidEmptyOpaqueTag_MismatchReturns412WithCurrentVersion()
    {
        // If-Match: "" is valid syntax, but does not match RowVersion 42 -> 412
        var request = new HttpRequestMessage(HttpMethod.Put, $"/items/{_testEntity.Id}");
        request.Headers.TryAddWithoutValidation("If-Match", "\"\"");

        var response = await _client!.SendAsync(request);
        Assert.Equal(HttpStatusCode.PreconditionFailed, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(412, body.GetProperty("status").GetInt32());
        Assert.Equal("\"42\"", body.GetProperty("currentVersion").GetString());
    }

    [Fact]
    public async Task ValidWeakTag_DoesNotSatisfyStrong_Returns412()
    {
        var request = new HttpRequestMessage(HttpMethod.Put, $"/items/{_testEntity.Id}");
        request.Headers.TryAddWithoutValidation("If-Match", "W/\"42\"");

        var response = await _client!.SendAsync(request);
        Assert.Equal(HttpStatusCode.PreconditionFailed, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("\"42\"", body.GetProperty("currentVersion").GetString());
    }

    [Fact]
    public async Task MismatchedNumericTag_Returns412WithCurrentVersion()
    {
        var request = new HttpRequestMessage(HttpMethod.Put, $"/items/{_testEntity.Id}");
        request.Headers.TryAddWithoutValidation("If-Match", "\"99\"");

        var response = await _client!.SendAsync(request);
        Assert.Equal(HttpStatusCode.PreconditionFailed, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("\"42\"", body.GetProperty("currentVersion").GetString());
    }

    [Fact]
    public async Task MatchingStrongTag_Succeeds200()
    {
        var request = new HttpRequestMessage(HttpMethod.Put, $"/items/{_testEntity.Id}");
        request.Headers.TryAddWithoutValidation("If-Match", "\"42\"");

        var response = await _client!.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task MultiTagWithOneMatch_Succeeds200()
    {
        var request = new HttpRequestMessage(HttpMethod.Put, $"/items/{_testEntity.Id}");
        request.Headers.TryAddWithoutValidation("If-Match", "\"10\", \"42\", \"99\"");

        var response = await _client!.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task WildcardSingle_ResourceExists_Succeeds200()
    {
        var request = new HttpRequestMessage(HttpMethod.Put, $"/items/{_testEntity.Id}");
        request.Headers.TryAddWithoutValidation("If-Match", "*");

        var response = await _client!.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
