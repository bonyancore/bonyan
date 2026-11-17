using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Transactions;
using Construction.Kernel.Persistence;
using Construction.Kernel.Tenancy;
using Construction.Web.ETag;
using Construction.Web;
using Construction.Web.Errors;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;

namespace Construction.Persistence.Tests.Integration;

public class PostgreSqlConcurrencyFixture : IAsyncLifetime
{
    public PostgreSqlContainer? Container { get; private set; }
    public string ConnectionString => Container?.GetConnectionString() ?? string.Empty;

    public async Task InitializeAsync()
    {
        var enabled = Environment.GetEnvironmentVariable("INTEGRATION_TESTS_ENABLED");
        if (!string.Equals(enabled, "true", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

#pragma warning disable CS0618
        Container = new PostgreSqlBuilder()
            .WithImage("postgres:17-alpine")
            .WithDatabase("concurrency_test_db")
            .WithUsername("postgres")
            .WithPassword("postgres_dev")
            .Build();
#pragma warning restore CS0618

        await Container.StartAsync();

        await using var conn = new NpgsqlConnection(ConnectionString);
        await conn.OpenAsync();

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            DO $$
            BEGIN
                IF NOT EXISTS (SELECT FROM pg_roles WHERE rolname = 'app') THEN
                    CREATE ROLE app LOGIN PASSWORD 'app_dev_pw' NOSUPERUSER NOBYPASSRLS;
                END IF;
            END $$;

            GRANT CONNECT ON DATABASE concurrency_test_db TO app;
            GRANT USAGE ON SCHEMA public TO app;

            CREATE TABLE IF NOT EXISTS test_concurrency_entities (
                id uuid PRIMARY KEY,
                tenant_id uuid NOT NULL,
                title text NOT NULL
            );

            GRANT SELECT, INSERT, UPDATE, DELETE ON test_concurrency_entities TO app;

            ALTER TABLE test_concurrency_entities ENABLE ROW LEVEL SECURITY;
            ALTER TABLE test_concurrency_entities FORCE ROW LEVEL SECURITY;

            DROP POLICY IF EXISTS ti_public_test_concurrency_entities ON test_concurrency_entities;
            CREATE POLICY ti_public_test_concurrency_entities ON test_concurrency_entities
                FOR ALL
                TO app
                USING (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid)
                WITH CHECK (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid);
            """;
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task DisposeAsync()
    {
        if (Container is not null)
        {
            await Container.DisposeAsync();
        }
    }
}

public class PostgreSqlConcurrencyRecoveryTests(PostgreSqlConcurrencyFixture fixture)
    : IClassFixture<PostgreSqlConcurrencyFixture>
{
    public class ConcurrencyTestEntity : ITenantScopedEntity, IConcurrencyVersioned
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public TenantId TenantId { get; set; }
        public string Title { get; set; } = string.Empty;
        public uint RowVersion { get; set; }
    }

    private class ConcurrencyDbContext(
        DbContextOptions<ConcurrencyDbContext> options,
        ITenantContext tenantContext)
        : ModuleDbContext(options, "public", tenantContext)
    {
        public DbSet<ConcurrencyTestEntity> Entities => Set<ConcurrencyTestEntity>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<ConcurrencyTestEntity>(b =>
            {
                b.ToTable("test_concurrency_entities", "public");
                b.HasKey(e => e.Id);
            });
        }
    }

    private class FakeTenantContext : ITenantContext
    {
        public bool HasTenant { get; set; } = true;
        public TenantId Current { get; set; } = TenantId.New();
    }

    [DockerFact]
    public async Task ConcurrentModification_WithIfMatch_Returns412_WithFreshOpaqueETag()
    {
        var tenant = TenantId.New();
        var entityId = Guid.NewGuid();

        // 1. Seed entity with initial title
        var csb = new NpgsqlConnectionStringBuilder(fixture.ConnectionString)
        {
            Username = "app",
            Password = "app_dev_pw"
        };

        var tenantCtx = new FakeTenantContext { Current = tenant, HasTenant = true };
        var interceptor = new TenantSessionInterceptor(tenantCtx);
        var dbOptions = new DbContextOptionsBuilder<ConcurrencyDbContext>()
            .UseNpgsql(csb.ConnectionString)
            .UseSnakeCaseNamingConvention()
            .AddInterceptors(interceptor)
            .Options;

        await using (var seedDb = new ConcurrencyDbContext(dbOptions, tenantCtx))
        {
            var seedEntity = new ConcurrencyTestEntity
            {
                Id = entityId,
                TenantId = tenant,
                Title = "Original Title"
            };
            seedDb.Entities.Add(seedEntity);
            await seedDb.SaveChangesAsync();
        }

        // 2. Fetch fresh xmin from DB
        uint initialXmin;
        await using (var readDb = new ConcurrencyDbContext(dbOptions, tenantCtx))
        {
            var fetched = await readDb.Entities.FirstAsync(e => e.Id == entityId);
            initialXmin = fetched.RowVersion;
        }

        // 3. Setup TestServer host with ConstructionExceptionHandler
        using var host = await new HostBuilder()
            .ConfigureWebHost(webBuilder =>
            {
                webBuilder.UseTestServer();
                webBuilder.ConfigureServices(services =>
                {
                    services.AddRouting();
                    services.AddSingleton<ITenantContext>(tenantCtx);
                    services.AddDbContext<ConcurrencyDbContext>(opts =>
                    {
                        opts.UseNpgsql(csb.ConnectionString)
                            .UseSnakeCaseNamingConvention()
                            .AddInterceptors(new TenantSessionInterceptor(tenantCtx));
                    });
                    services.AddWebInfrastructure();
                });
                webBuilder.Configure(app =>
                {
                    app.UseWebExceptionHandler();
                    app.UseRouting();
                    app.UseEndpoints(endpoints =>
                    {
                        endpoints.MapPatch("/entities/{id}", async (Guid id, HttpContext httpContext, ConcurrencyDbContext db) =>
                        {
                            // Enforce If-Match guard & register target key
                            httpContext.Items[HttpContextIfMatchAccessor.EnforcedMarkerItemName] = true;
                            httpContext.Items[HttpContextIfMatchAccessor.TargetKeyItemName] =
                                new ConcurrencyTargetKey(typeof(ConcurrencyTestEntity), id);

                            // Load entity
                            var entity = await db.Entities.FindAsync(id);
                            if (entity == null)
                            {
                                return Results.NotFound();
                            }

                            // Simulate concurrent mutation in DB by another transaction BEFORE saving changes
                            await using (var otherConn = new NpgsqlConnection(fixture.ConnectionString))
                            {
                                await otherConn.OpenAsync();
                                await using var updateCmd = otherConn.CreateCommand();
                                updateCmd.CommandText = "UPDATE test_concurrency_entities SET title = 'Concurrently Modified' WHERE id = @id";
                                updateCmd.Parameters.AddWithValue("id", id);
                                await updateCmd.ExecuteNonQueryAsync();
                            }

                            // Now try to update entity with old tracked xmin; throws DbUpdateConcurrencyException
                            entity.Title = "My New Title";
                            await db.SaveChangesAsync();

                            return Results.Ok();
                        });
                    });
                });
            })
            .StartAsync();

        var client = host.GetTestClient();
        client.DefaultRequestHeaders.TryAddWithoutValidation("If-Match", $"\"{initialXmin}\"");

        var response = await client.PatchAsync($"/entities/{entityId}", null);

        Assert.Equal(HttpStatusCode.PreconditionFailed, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        using var jsonDoc = JsonDocument.Parse(body);
        var root = jsonDoc.RootElement;

        Assert.Equal("concurrency.precondition_failed", root.GetProperty("code").GetString());
        Assert.Equal("precondition_failed", root.GetProperty("title").GetString());
        Assert.True(root.TryGetProperty("currentVersion", out var currentVersionProp));

        var currentVersionEtag = currentVersionProp.GetString();
        Assert.NotNull(currentVersionEtag);
        // ETag must be quoted and not match the initial version
        Assert.StartsWith("\"", currentVersionEtag);
        Assert.EndsWith("\"", currentVersionEtag);
        Assert.NotEqual($"\"{initialXmin}\"", currentVersionEtag);
    }

    [DockerFact]
    public async Task ConcurrentDeletion_WithIfMatch_Returns412_AndOmitsCurrentVersion()
    {
        var tenant = TenantId.New();
        var entityId = Guid.NewGuid();

        var csb = new NpgsqlConnectionStringBuilder(fixture.ConnectionString)
        {
            Username = "app",
            Password = "app_dev_pw"
        };

        var tenantCtx = new FakeTenantContext { Current = tenant, HasTenant = true };
        var interceptor = new TenantSessionInterceptor(tenantCtx);
        var dbOptions = new DbContextOptionsBuilder<ConcurrencyDbContext>()
            .UseNpgsql(csb.ConnectionString)
            .UseSnakeCaseNamingConvention()
            .AddInterceptors(interceptor)
            .Options;

        await using (var seedDb = new ConcurrencyDbContext(dbOptions, tenantCtx))
        {
            var seedEntity = new ConcurrencyTestEntity
            {
                Id = entityId,
                TenantId = tenant,
                Title = "To Be Deleted"
            };
            seedDb.Entities.Add(seedEntity);
            await seedDb.SaveChangesAsync();
        }

        uint initialXmin;
        await using (var readDb = new ConcurrencyDbContext(dbOptions, tenantCtx))
        {
            var fetched = await readDb.Entities.FirstAsync(e => e.Id == entityId);
            initialXmin = fetched.RowVersion;
        }

        using var host = await new HostBuilder()
            .ConfigureWebHost(webBuilder =>
            {
                webBuilder.UseTestServer();
                webBuilder.ConfigureServices(services =>
                {
                    services.AddRouting();
                    services.AddSingleton<ITenantContext>(tenantCtx);
                    services.AddDbContext<ConcurrencyDbContext>(opts =>
                    {
                        opts.UseNpgsql(csb.ConnectionString)
                            .UseSnakeCaseNamingConvention()
                            .AddInterceptors(new TenantSessionInterceptor(tenantCtx));
                    });
                    services.AddWebInfrastructure();
                });
                webBuilder.Configure(app =>
                {
                    app.UseWebExceptionHandler();
                    app.UseRouting();
                    app.UseEndpoints(endpoints =>
                    {
                        endpoints.MapPatch("/entities/{id}", async (Guid id, HttpContext httpContext, ConcurrencyDbContext db) =>
                        {
                            httpContext.Items[HttpContextIfMatchAccessor.EnforcedMarkerItemName] = true;
                            httpContext.Items[HttpContextIfMatchAccessor.TargetKeyItemName] =
                                new ConcurrencyTargetKey(typeof(ConcurrencyTestEntity), id);

                            var entity = await db.Entities.FindAsync(id);
                            if (entity == null)
                            {
                                return Results.NotFound();
                            }

                            // Delete row directly in database before saving
                            await using (var otherConn = new NpgsqlConnection(fixture.ConnectionString))
                            {
                                await otherConn.OpenAsync();
                                await using var deleteCmd = otherConn.CreateCommand();
                                deleteCmd.CommandText = "DELETE FROM test_concurrency_entities WHERE id = @id";
                                deleteCmd.Parameters.AddWithValue("id", id);
                                await deleteCmd.ExecuteNonQueryAsync();
                            }

                            entity.Title = "Deleted Update Attempt";
                            await db.SaveChangesAsync();

                            return Results.Ok();
                        });
                    });
                });
            })
            .StartAsync();

        var client = host.GetTestClient();
        client.DefaultRequestHeaders.TryAddWithoutValidation("If-Match", $"\"{initialXmin}\"");

        var response = await client.PatchAsync($"/entities/{entityId}", null);

        Assert.Equal(HttpStatusCode.PreconditionFailed, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        using var jsonDoc = JsonDocument.Parse(body);
        var root = jsonDoc.RootElement;

        Assert.Equal("concurrency.precondition_failed", root.GetProperty("code").GetString());
        Assert.Equal("The resource was deleted by another transaction.", root.GetProperty("detail").GetString());
        // Under Decision A, currentVersion MUST be omitted
        Assert.False(root.TryGetProperty("currentVersion", out _));
    }

    [DockerFact]
    public async Task ConcurrencyConflict_WithoutIfMatch_Returns409Conflict()
    {
        var tenant = TenantId.New();
        var entityId = Guid.NewGuid();

        var csb = new NpgsqlConnectionStringBuilder(fixture.ConnectionString)
        {
            Username = "app",
            Password = "app_dev_pw"
        };

        var tenantCtx = new FakeTenantContext { Current = tenant, HasTenant = true };
        var interceptor = new TenantSessionInterceptor(tenantCtx);
        var dbOptions = new DbContextOptionsBuilder<ConcurrencyDbContext>()
            .UseNpgsql(csb.ConnectionString)
            .UseSnakeCaseNamingConvention()
            .AddInterceptors(interceptor)
            .Options;

        await using (var seedDb = new ConcurrencyDbContext(dbOptions, tenantCtx))
        {
            var seedEntity = new ConcurrencyTestEntity
            {
                Id = entityId,
                TenantId = tenant,
                Title = "Conflict Test"
            };
            seedDb.Entities.Add(seedEntity);
            await seedDb.SaveChangesAsync();
        }

        using var host = await new HostBuilder()
            .ConfigureWebHost(webBuilder =>
            {
                webBuilder.UseTestServer();
                webBuilder.ConfigureServices(services =>
                {
                    services.AddRouting();
                    services.AddSingleton<ITenantContext>(tenantCtx);
                    services.AddDbContext<ConcurrencyDbContext>(opts =>
                    {
                        opts.UseNpgsql(csb.ConnectionString)
                            .UseSnakeCaseNamingConvention()
                            .AddInterceptors(new TenantSessionInterceptor(tenantCtx));
                    });
                    services.AddWebInfrastructure();
                });
                webBuilder.Configure(app =>
                {
                    app.UseWebExceptionHandler();
                    app.UseRouting();
                    app.UseEndpoints(endpoints =>
                    {
                        // Endpoint WITHOUT If-Match enforcement marker
                        endpoints.MapPatch("/unconditional/{id}", async (Guid id, HttpContext httpContext, ConcurrencyDbContext db) =>
                        {
                            var entity = await db.Entities.FindAsync(id);
                            if (entity == null)
                            {
                                return Results.NotFound();
                            }

                            await using (var otherConn = new NpgsqlConnection(fixture.ConnectionString))
                            {
                                await otherConn.OpenAsync();
                                await using var updateCmd = otherConn.CreateCommand();
                                updateCmd.CommandText = "UPDATE test_concurrency_entities SET title = 'External Edit' WHERE id = @id";
                                updateCmd.Parameters.AddWithValue("id", id);
                                await updateCmd.ExecuteNonQueryAsync();
                            }

                            entity.Title = "Conflicting Unconditional Save";
                            await db.SaveChangesAsync();

                            return Results.Ok();
                        });
                    });
                });
            })
            .StartAsync();

        var client = host.GetTestClient();
        var response = await client.PatchAsync($"/unconditional/{entityId}", null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        using var jsonDoc = JsonDocument.Parse(body);
        var root = jsonDoc.RootElement;

        Assert.Equal("concurrency.conflict", root.GetProperty("code").GetString());
        Assert.Equal("conflict", root.GetProperty("title").GetString());
    }

    [DockerFact]
    public async Task ConcurrencyConflict_WithMissingTargetKey_FailsSafelyTo503()
    {
        var tenant = TenantId.New();
        var entityId = Guid.NewGuid();

        var csb = new NpgsqlConnectionStringBuilder(fixture.ConnectionString)
        {
            Username = "app",
            Password = "app_dev_pw"
        };

        var tenantCtx = new FakeTenantContext { Current = tenant, HasTenant = true };
        var interceptor = new TenantSessionInterceptor(tenantCtx);
        var dbOptions = new DbContextOptionsBuilder<ConcurrencyDbContext>()
            .UseNpgsql(csb.ConnectionString)
            .UseSnakeCaseNamingConvention()
            .AddInterceptors(interceptor)
            .Options;

        await using (var seedDb = new ConcurrencyDbContext(dbOptions, tenantCtx))
        {
            var seedEntity = new ConcurrencyTestEntity
            {
                Id = entityId,
                TenantId = tenant,
                Title = "Missing Target Key"
            };
            seedDb.Entities.Add(seedEntity);
            await seedDb.SaveChangesAsync();
        }

        using var host = await new HostBuilder()
            .ConfigureWebHost(webBuilder =>
            {
                webBuilder.UseTestServer();
                webBuilder.ConfigureServices(services =>
                {
                    services.AddRouting();
                    services.AddSingleton<ITenantContext>(tenantCtx);
                    services.AddDbContext<ConcurrencyDbContext>(opts =>
                    {
                        opts.UseNpgsql(csb.ConnectionString)
                            .UseSnakeCaseNamingConvention()
                            .AddInterceptors(new TenantSessionInterceptor(tenantCtx));
                    });
                    services.AddWebInfrastructure();
                });
                webBuilder.Configure(app =>
                {
                    app.UseWebExceptionHandler();
                    app.UseRouting();
                    app.UseEndpoints(endpoints =>
                    {
                        endpoints.MapPatch("/broken-guard/{id}", async (Guid id, HttpContext httpContext, ConcurrencyDbContext db) =>
                        {
                            // Enforce If-Match but OMIT TargetKey
                            httpContext.Items[HttpContextIfMatchAccessor.EnforcedMarkerItemName] = true;

                            var entity = await db.Entities.FindAsync(id);
                            if (entity == null)
                            {
                                return Results.NotFound();
                            }

                            await using (var otherConn = new NpgsqlConnection(fixture.ConnectionString))
                            {
                                await otherConn.OpenAsync();
                                await using var updateCmd = otherConn.CreateCommand();
                                updateCmd.CommandText = "UPDATE test_concurrency_entities SET title = 'External Edit' WHERE id = @id";
                                updateCmd.Parameters.AddWithValue("id", id);
                                await updateCmd.ExecuteNonQueryAsync();
                            }

                            entity.Title = "Trigger Conflict";
                            await db.SaveChangesAsync();

                            return Results.Ok();
                        });
                    });
                });
            })
            .StartAsync();

        var client = host.GetTestClient();
        var response = await client.PatchAsync($"/broken-guard/{entityId}", null);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        using var jsonDoc = JsonDocument.Parse(body);
        var root = jsonDoc.RootElement;

        Assert.Equal("concurrency.recovery_failed", root.GetProperty("code").GetString());
    }

    [DockerFact]
    public async Task AmbientTransactionScope_WithIndependentExternalUpdate_Returns412_MatchingExactCommittedXmin()
    {
        var tenant = TenantId.New();
        var entityId = Guid.NewGuid();

        var csb = new NpgsqlConnectionStringBuilder(fixture.ConnectionString)
        {
            Username = "app",
            Password = "app_dev_pw"
        };

        var tenantCtx = new FakeTenantContext { Current = tenant, HasTenant = true };
        var interceptor = new TenantSessionInterceptor(tenantCtx);
        var dbOptions = new DbContextOptionsBuilder<ConcurrencyDbContext>()
            .UseNpgsql(csb.ConnectionString)
            .UseSnakeCaseNamingConvention()
            .AddInterceptors(interceptor)
            .Options;

        await using (var seedDb = new ConcurrencyDbContext(dbOptions, tenantCtx))
        {
            seedDb.Entities.Add(new ConcurrencyTestEntity
            {
                Id = entityId,
                TenantId = tenant,
                Title = "Initial Title"
            });
            await seedDb.SaveChangesAsync();
        }

        uint initialXmin;
        await using (var readDb = new ConcurrencyDbContext(dbOptions, tenantCtx))
        {
            var fetched = await readDb.Entities.FirstAsync(e => e.Id == entityId);
            initialXmin = fetched.RowVersion;
        }

        uint committedXmin = 0;

        using var host = await new HostBuilder()
            .ConfigureWebHost(webBuilder =>
            {
                webBuilder.UseTestServer();
                webBuilder.ConfigureServices(services =>
                {
                    services.AddRouting();
                    services.AddSingleton<ITenantContext>(tenantCtx);
                    services.AddDbContext<ConcurrencyDbContext>(opts =>
                    {
                        opts.UseNpgsql(csb.ConnectionString)
                            .UseSnakeCaseNamingConvention()
                            .AddInterceptors(new TenantSessionInterceptor(tenantCtx));
                    });
                    services.AddWebInfrastructure();
                });
                webBuilder.Configure(app =>
                {
                    app.UseWebExceptionHandler();
                    app.UseRouting();
                    app.UseEndpoints(endpoints =>
                    {
                        endpoints.MapPatch("/scoped-update/{id}", async (Guid id, HttpContext httpContext, ConcurrencyDbContext db) =>
                        {
                            httpContext.Items[HttpContextIfMatchAccessor.EnforcedMarkerItemName] = true;
                            httpContext.Items[HttpContextIfMatchAccessor.TargetKeyItemName] =
                                new ConcurrencyTargetKey(typeof(ConcurrencyTestEntity), id);

                            // Inner TransactionScope created inside endpoint
                            using var scope = new TransactionScope(
                                TransactionScopeOption.Required,
                                new TransactionOptions { IsolationLevel = System.Transactions.IsolationLevel.ReadCommitted },
                                TransactionScopeAsyncFlowOption.Enabled);

                            var entity = await db.Entities.FindAsync(id);
                            if (entity == null)
                            {
                                return Results.NotFound();
                            }

                            // Independent external connection with Enlist=false and TransactionScopeOption.Suppress
                            var externalCsb = new NpgsqlConnectionStringBuilder(fixture.ConnectionString)
                            {
                                Enlist = false
                            };

                            using (new TransactionScope(TransactionScopeOption.Suppress, TransactionScopeAsyncFlowOption.Enabled))
                            {
                                await using var externalConn = new NpgsqlConnection(externalCsb.ConnectionString);
                                await externalConn.OpenAsync();

                                await using (var updateCmd = externalConn.CreateCommand())
                                {
                                    updateCmd.CommandText = "UPDATE test_concurrency_entities SET title = 'External Independent Edit' WHERE id = @id";
                                    updateCmd.Parameters.AddWithValue("id", id);
                                    await updateCmd.ExecuteNonQueryAsync();
                                }

                                await using (var queryCmd = externalConn.CreateCommand())
                                {
                                    queryCmd.CommandText = "SELECT xmin::text FROM test_concurrency_entities WHERE id = @id";
                                    queryCmd.Parameters.AddWithValue("id", id);
                                    committedXmin = Convert.ToUInt32(await queryCmd.ExecuteScalarAsync(), CultureInfo.InvariantCulture);
                                }
                            }

                            // Now mutate and save in endpoint -> throws DbUpdateConcurrencyException
                            entity.Title = "Inner Scope Update";
                            await db.SaveChangesAsync();

                            scope.Complete();
                            return Results.Ok();
                        });
                    });
                });
            })
            .StartAsync();

        var client = host.GetTestClient();
        client.DefaultRequestHeaders.TryAddWithoutValidation("If-Match", $"\"{initialXmin}\"");

        var response = await client.PatchAsync($"/scoped-update/{entityId}", null);

        Assert.Equal(HttpStatusCode.PreconditionFailed, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        using var jsonDoc = JsonDocument.Parse(body);
        var root = jsonDoc.RootElement;

        Assert.Equal("concurrency.precondition_failed", root.GetProperty("code").GetString());
        Assert.True(root.TryGetProperty("currentVersion", out var currentVersionProp));

        var currentVersionEtag = currentVersionProp.GetString();
        Assert.NotNull(currentVersionEtag);

        // Crucial verification: currentVersion must strictly match the exact committed xmin queried from PostgreSQL
        Assert.Equal($"\"{committedXmin}\"", currentVersionEtag);
        Assert.NotEqual($"\"{initialXmin}\"", currentVersionEtag);
    }

    [DockerFact]
    public async Task OuterAmbientTransaction_ActiveOnRecovery_FailsSafelyTo503()
    {
        var tenant = TenantId.New();
        var entityId = Guid.NewGuid();

        var csb = new NpgsqlConnectionStringBuilder(fixture.ConnectionString)
        {
            Username = "app",
            Password = "app_dev_pw"
        };

        var tenantCtx = new FakeTenantContext { Current = tenant, HasTenant = true };
        var queryInterceptor = new ConcurrencyQueryCountingInterceptor();
        var handlerWitness = new AmbientTransactionWitnessExceptionHandler();
        var trackingState = new AmbientTrackingState();

        var dbOptions = new DbContextOptionsBuilder<ConcurrencyDbContext>()
            .UseNpgsql(csb.ConnectionString)
            .UseSnakeCaseNamingConvention()
            .AddInterceptors(new TenantSessionInterceptor(tenantCtx), queryInterceptor)
            .Options;

        // 1. Seed entity
        await using (var seedDb = new ConcurrencyDbContext(dbOptions, tenantCtx))
        {
            seedDb.Entities.Add(new ConcurrencyTestEntity
            {
                Id = entityId,
                TenantId = tenant,
                Title = "Outer Scope Seed"
            });
            await seedDb.SaveChangesAsync();
        }

        uint initialXmin;
        await using (var readDb = new ConcurrencyDbContext(dbOptions, tenantCtx))
        {
            var fetched = await readDb.Entities.FirstAsync(e => e.Id == entityId);
            initialXmin = fetched.RowVersion;
        }

        uint externalCommittedXmin = 0;

        using var host = await new HostBuilder()
            .ConfigureWebHost(webBuilder =>
            {
                webBuilder.UseTestServer();
                webBuilder.ConfigureServices(services =>
                {
                    services.AddRouting();
                    services.AddSingleton<ITenantContext>(tenantCtx);
                    // Register test-only witness before AddWebInfrastructure to run first in handler chain
                    services.AddSingleton<Microsoft.AspNetCore.Diagnostics.IExceptionHandler>(handlerWitness);
                    services.AddDbContext<ConcurrencyDbContext>(opts =>
                    {
                        opts.UseNpgsql(csb.ConnectionString)
                            .UseSnakeCaseNamingConvention()
                            .AddInterceptors(new TenantSessionInterceptor(tenantCtx), queryInterceptor);
                    });
                    services.AddWebInfrastructure();
                });
                webBuilder.Configure(app =>
                {
                    // Outer middleware wraps request pipeline in an ambient transaction
                    app.Use(async (context, next) =>
                    {
                        if (context.Request.Path.StartsWithSegments("/outer-scope"))
                        {
                            var outerScope = new TransactionScope(
                                TransactionScopeOption.Required,
                                new TransactionOptions { IsolationLevel = System.Transactions.IsolationLevel.ReadCommitted },
                                TransactionScopeAsyncFlowOption.Enabled);

                            trackingState.OuterTransaction = Transaction.Current;
                            trackingState.TransactionBeforeNext = Transaction.Current;

                            try
                            {
                                await next();
                                trackingState.NextCompletedNormally = true;
                            }
                            catch (Exception ex)
                            {
                                trackingState.NextThrew = true;
                                trackingState.EscapedException = ex;
                                throw;
                            }
                            finally
                            {
                                trackingState.TransactionAfterNext = Transaction.Current;
                                try
                                {
                                    outerScope.Dispose();
                                    trackingState.ScopeDisposed = true;
                                }
                                catch (Exception dex)
                                {
                                    trackingState.DisposalException = dex;
                                }
                                trackingState.TransactionAfterDispose = Transaction.Current;
                            }
                        }
                        else
                        {
                            await next();
                        }
                    });

                    app.UseWebExceptionHandler();
                    app.UseRouting();
                    app.UseEndpoints(endpoints =>
                    {
                        endpoints.MapPatch("/outer-scope/{id}", async (Guid id, HttpContext httpContext, ConcurrencyDbContext db) =>
                        {
                            httpContext.Items[HttpContextIfMatchAccessor.EnforcedMarkerItemName] = true;
                            httpContext.Items[HttpContextIfMatchAccessor.TargetKeyItemName] =
                                new ConcurrencyTargetKey(typeof(ConcurrencyTestEntity), id);

                            var entity = await db.Entities.FindAsync(id);
                            if (entity == null) return Results.NotFound();

                            // External independent update with app credentials, Enlist=false, and TransactionScopeOption.Suppress
                            var externalCsb = new NpgsqlConnectionStringBuilder(fixture.ConnectionString)
                            {
                                Username = "app",
                                Password = "app_dev_pw",
                                Enlist = false
                            };

                            using (new TransactionScope(TransactionScopeOption.Suppress, TransactionScopeAsyncFlowOption.Enabled))
                            {
                                await using var extConn = new NpgsqlConnection(externalCsb.ConnectionString);
                                await extConn.OpenAsync();

                                // Establish tenant RLS session context on external connection and verify result
                                await using (var rlsCmd = extConn.CreateCommand())
                                {
                                    rlsCmd.CommandText = "SELECT set_config('app.tenant_id', @tenantId, false);";
                                    rlsCmd.Parameters.AddWithValue("tenantId", tenant.Value.ToString());
                                    var setConfigResult = Convert.ToString(await rlsCmd.ExecuteScalarAsync(), CultureInfo.InvariantCulture);
                                    Assert.Equal(tenant.Value.ToString(), setConfigResult);
                                }

                                // Mutate row under tenant RLS and assert exact 1 row affected
                                await using (var updateCmd = extConn.CreateCommand())
                                {
                                    updateCmd.CommandText = "UPDATE test_concurrency_entities SET title = 'External Mutate' WHERE id = @id";
                                    updateCmd.Parameters.AddWithValue("id", id);
                                    var rowsAffected = await updateCmd.ExecuteNonQueryAsync();
                                    Assert.Equal(1, rowsAffected);
                                }

                                // Query and record fresh committed xmin directly from PostgreSQL
                                await using (var xminCmd = extConn.CreateCommand())
                                {
                                    xminCmd.CommandText = "SELECT xmin::text FROM test_concurrency_entities WHERE id = @id";
                                    xminCmd.Parameters.AddWithValue("id", id);
                                    externalCommittedXmin = Convert.ToUInt32(await xminCmd.ExecuteScalarAsync(), CultureInfo.InvariantCulture);
                                }
                            }

                            // Scope interceptor strictly to this DbContext instance and enable recording
                            queryInterceptor.TargetContextId = db.ContextId;
                            queryInterceptor.RecordingEnabled = true;

                            entity.Title = "Trigger Conflict Under Outer Scope";
                            // Throws DbUpdateConcurrencyException because external update changed xmin
                            await db.SaveChangesAsync();

                            return Results.Ok();
                        });
                    });
                });
            })
            .StartAsync();

        var client = host.GetTestClient();
        client.DefaultRequestHeaders.TryAddWithoutValidation("If-Match", $"\"{initialXmin}\"");

        var response = await client.PatchAsync($"/outer-scope/{entityId}", null);
        var body = await response.Content.ReadAsStringAsync();

        // 1. Transaction lifecycle, pipeline execution, and cleanup assertions
        Assert.NotNull(trackingState.OuterTransaction);
        Assert.NotNull(trackingState.TransactionBeforeNext);
        Assert.Same(trackingState.OuterTransaction, trackingState.TransactionBeforeNext);
        Assert.True(trackingState.NextCompletedNormally, "Downstream pipeline must complete normally with handled exception response.");
        Assert.False(trackingState.NextThrew, "Exception must be handled by exception middleware rather than escaping.");
        Assert.Null(trackingState.EscapedException);
        Assert.NotNull(trackingState.TransactionAfterNext);
        Assert.True(trackingState.ScopeDisposed, "The outer TransactionScope must be disposed in finally.");
        Assert.Null(trackingState.DisposalException);
        Assert.Null(trackingState.TransactionAfterDispose);

        // 2. Production exception handler observed Transaction.Current
        Assert.NotNull(handlerWitness.ObservedTransactionAtHandler);
        Assert.Same(trackingState.OuterTransaction, handlerWitness.ObservedTransactionAtHandler);

        // 3. HTTP 503 response verification with exact ambient guard detail and code
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);

        using var jsonDoc = JsonDocument.Parse(body);
        var root = jsonDoc.RootElement;
        Assert.Equal("concurrency.recovery_failed", root.GetProperty("code").GetString());
        Assert.Equal(503, root.GetProperty("status").GetInt32());
        Assert.Equal(
            "A concurrency conflict occurred inside an active ambient transaction. Recovery is not possible; the ambient transaction must be handled locally.",
            root.GetProperty("detail").GetString());

        // 4. Assert recovery query (GetDatabaseValuesAsync) was NEVER attempted on this DbContext
        Assert.Equal(0, queryInterceptor.RecoverySelectCount);

        // 5. Test runner ambient transaction isolation
        Assert.Null(Transaction.Current);

        // 6. Assert independent external update committed successfully and persisted
        Assert.NotEqual(0u, externalCommittedXmin);
        Assert.NotEqual(initialXmin, externalCommittedXmin);

        await using (var verifyDb = new ConcurrencyDbContext(dbOptions, tenantCtx))
        {
            var updatedEntity = await verifyDb.Entities.AsNoTracking().FirstAsync(e => e.Id == entityId);
            Assert.Equal("External Mutate", updatedEntity.Title);
            Assert.Equal(externalCommittedXmin, updatedEntity.RowVersion);
        }
    }

    [DockerFact]
    public async Task RepeatableRead_ConcurrentUpdate_Returns409_WithSerializationFailureCode()
    {
        var tenant = TenantId.New();
        var entityId = Guid.NewGuid();

        var csb = new NpgsqlConnectionStringBuilder(fixture.ConnectionString)
        {
            Username = "app",
            Password = "app_dev_pw"
        };

        var tenantCtx = new FakeTenantContext { Current = tenant, HasTenant = true };
        var interceptor = new TenantSessionInterceptor(tenantCtx);
        var dbOptions = new DbContextOptionsBuilder<ConcurrencyDbContext>()
            .UseNpgsql(csb.ConnectionString)
            .UseSnakeCaseNamingConvention()
            .AddInterceptors(interceptor)
            .Options;

        await using (var seedDb = new ConcurrencyDbContext(dbOptions, tenantCtx))
        {
            seedDb.Entities.Add(new ConcurrencyTestEntity
            {
                Id = entityId,
                TenantId = tenant,
                Title = "RepeatableRead Seed"
            });
            await seedDb.SaveChangesAsync();
        }

        using var host = await new HostBuilder()
            .ConfigureWebHost(webBuilder =>
            {
                webBuilder.UseTestServer();
                webBuilder.ConfigureServices(services =>
                {
                    services.AddRouting();
                    services.AddSingleton<ITenantContext>(tenantCtx);
                    services.AddDbContext<ConcurrencyDbContext>(opts =>
                    {
                        opts.UseNpgsql(csb.ConnectionString)
                            .UseSnakeCaseNamingConvention()
                            .AddInterceptors(new TenantSessionInterceptor(tenantCtx));
                    });
                    services.AddWebInfrastructure();
                });
                webBuilder.Configure(app =>
                {
                    app.UseWebExceptionHandler();
                    app.UseRouting();
                    app.UseEndpoints(endpoints =>
                    {
                        endpoints.MapPatch("/repeatable-read/{id}", async (Guid id, HttpContext httpContext, ConcurrencyDbContext db) =>
                        {
                            await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.RepeatableRead);

                            var entity = await db.Entities.FindAsync(id);
                            if (entity == null) return Results.NotFound();

                            // External update commits while snapshot is active
                            await using (var extConn = new NpgsqlConnection(fixture.ConnectionString))
                            {
                                await extConn.OpenAsync();
                                await using var updateCmd = extConn.CreateCommand();
                                updateCmd.CommandText = "UPDATE test_concurrency_entities SET title = 'External Mutate Under RR' WHERE id = @id";
                                updateCmd.Parameters.AddWithValue("id", id);
                                await updateCmd.ExecuteNonQueryAsync();
                            }

                            entity.Title = "RR Conflict";
                            await db.SaveChangesAsync();

                            await tx.CommitAsync();
                            return Results.Ok();
                        });
                    });
                });
            })
            .StartAsync();

        var client = host.GetTestClient();
        var response = await client.PatchAsync($"/repeatable-read/{entityId}", null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        using var jsonDoc = JsonDocument.Parse(body);
        var root = jsonDoc.RootElement;

        Assert.Equal("concurrency.serialization_failure", root.GetProperty("code").GetString());
        Assert.Equal("conflict", root.GetProperty("title").GetString());
        Assert.False(root.TryGetProperty("currentVersion", out _));
    }

    [DockerFact]
    public async Task Serializable_ConcurrentUpdate_Returns409_WithSerializationFailureCode()
    {
        var tenant = TenantId.New();
        var entityId = Guid.NewGuid();

        var csb = new NpgsqlConnectionStringBuilder(fixture.ConnectionString)
        {
            Username = "app",
            Password = "app_dev_pw"
        };

        var tenantCtx = new FakeTenantContext { Current = tenant, HasTenant = true };
        var interceptor = new TenantSessionInterceptor(tenantCtx);
        var dbOptions = new DbContextOptionsBuilder<ConcurrencyDbContext>()
            .UseNpgsql(csb.ConnectionString)
            .UseSnakeCaseNamingConvention()
            .AddInterceptors(interceptor)
            .Options;

        await using (var seedDb = new ConcurrencyDbContext(dbOptions, tenantCtx))
        {
            seedDb.Entities.Add(new ConcurrencyTestEntity
            {
                Id = entityId,
                TenantId = tenant,
                Title = "Serializable Seed"
            });
            await seedDb.SaveChangesAsync();
        }

        using var host = await new HostBuilder()
            .ConfigureWebHost(webBuilder =>
            {
                webBuilder.UseTestServer();
                webBuilder.ConfigureServices(services =>
                {
                    services.AddRouting();
                    services.AddSingleton<ITenantContext>(tenantCtx);
                    services.AddDbContext<ConcurrencyDbContext>(opts =>
                    {
                        opts.UseNpgsql(csb.ConnectionString)
                            .UseSnakeCaseNamingConvention()
                            .AddInterceptors(new TenantSessionInterceptor(tenantCtx));
                    });
                    services.AddWebInfrastructure();
                });
                webBuilder.Configure(app =>
                {
                    app.UseWebExceptionHandler();
                    app.UseRouting();
                    app.UseEndpoints(endpoints =>
                    {
                        endpoints.MapPatch("/serializable/{id}", async (Guid id, HttpContext httpContext, ConcurrencyDbContext db) =>
                        {
                            await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);

                            var entity = await db.Entities.FindAsync(id);
                            if (entity == null) return Results.NotFound();

                            // External update commits while snapshot is active
                            await using (var extConn = new NpgsqlConnection(fixture.ConnectionString))
                            {
                                await extConn.OpenAsync();
                                await using var updateCmd = extConn.CreateCommand();
                                updateCmd.CommandText = "UPDATE test_concurrency_entities SET title = 'External Mutate Under Ser' WHERE id = @id";
                                updateCmd.Parameters.AddWithValue("id", id);
                                await updateCmd.ExecuteNonQueryAsync();
                            }

                            entity.Title = "Serializable Conflict";
                            await db.SaveChangesAsync();

                            await tx.CommitAsync();
                            return Results.Ok();
                        });
                    });
                });
            })
            .StartAsync();

        var client = host.GetTestClient();
        var response = await client.PatchAsync($"/serializable/{entityId}", null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        using var jsonDoc = JsonDocument.Parse(body);
        var root = jsonDoc.RootElement;

        Assert.Equal("concurrency.serialization_failure", root.GetProperty("code").GetString());
        Assert.Equal("conflict", root.GetProperty("title").GetString());
        Assert.False(root.TryGetProperty("currentVersion", out _));
    }

    [DockerFact]
    public async Task ConcurrencyRecovery_WithActiveEfDatabaseTransaction_FailsSafelyTo503()
    {
        var tenant = TenantId.New();
        var entityId = Guid.NewGuid();

        var csb = new NpgsqlConnectionStringBuilder(fixture.ConnectionString)
        {
            Username = "app",
            Password = "app_dev_pw"
        };

        var tenantCtx = new FakeTenantContext { Current = tenant, HasTenant = true };
        var interceptor = new TenantSessionInterceptor(tenantCtx);
        var dbOptions = new DbContextOptionsBuilder<ConcurrencyDbContext>()
            .UseNpgsql(csb.ConnectionString)
            .UseSnakeCaseNamingConvention()
            .AddInterceptors(interceptor)
            .Options;

        await using (var seedDb = new ConcurrencyDbContext(dbOptions, tenantCtx))
        {
            var seedEntity = new ConcurrencyTestEntity
            {
                Id = entityId,
                TenantId = tenant,
                Title = "Active EF Tx Test"
            };
            seedDb.Entities.Add(seedEntity);
            await seedDb.SaveChangesAsync();
        }

        using var host = await new HostBuilder()
            .ConfigureWebHost(webBuilder =>
            {
                webBuilder.UseTestServer();
                webBuilder.ConfigureServices(services =>
                {
                    services.AddRouting();
                    services.AddSingleton<ITenantContext>(tenantCtx);
                    services.AddDbContext<ConcurrencyDbContext>(opts =>
                    {
                        opts.UseNpgsql(csb.ConnectionString)
                            .UseSnakeCaseNamingConvention()
                            .AddInterceptors(new TenantSessionInterceptor(tenantCtx));
                    });
                    services.AddWebInfrastructure();
                });
                webBuilder.Configure(app =>
                {
                    app.UseWebExceptionHandler();
                    app.UseRouting();
                    app.UseEndpoints(endpoints =>
                    {
                        endpoints.MapPatch("/active-ef-tx/{id}", async (Guid id, HttpContext httpContext, ConcurrencyDbContext db) =>
                        {
                            httpContext.Items[HttpContextIfMatchAccessor.EnforcedMarkerItemName] = true;
                            httpContext.Items[HttpContextIfMatchAccessor.TargetKeyItemName] =
                                new ConcurrencyTargetKey(typeof(ConcurrencyTestEntity), id);

                            // Explicit EF transaction kept open on the DbContext
                            await db.Database.BeginTransactionAsync();

                            var entity = await db.Entities.FindAsync(id);
                            if (entity == null) return Results.NotFound();

                            // External update to create a concurrency conflict
                            await using (var extConn = new NpgsqlConnection(fixture.ConnectionString))
                            {
                                await extConn.OpenAsync();
                                await using var updateCmd = extConn.CreateCommand();
                                updateCmd.CommandText = "UPDATE test_concurrency_entities SET title = 'External Edit' WHERE id = @id";
                                updateCmd.Parameters.AddWithValue("id", id);
                                await updateCmd.ExecuteNonQueryAsync();
                            }

                            entity.Title = "Conflicted Update Under Open EF Tx";
                            // SaveChangesAsync throws DbUpdateConcurrencyException while db.Database.CurrentTransaction is not null
                            await db.SaveChangesAsync();

                            return Results.Ok();
                        });
                    });
                });
            })
            .StartAsync();

        var client = host.GetTestClient();
        var response = await client.PatchAsync($"/active-ef-tx/{entityId}", null);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        using var jsonDoc = JsonDocument.Parse(body);
        var root = jsonDoc.RootElement;

        Assert.Equal(503, root.GetProperty("status").GetInt32());
        Assert.Equal("concurrency.recovery_failed", root.GetProperty("code").GetString());
        Assert.Contains("open transaction", root.GetProperty("detail").GetString(), StringComparison.OrdinalIgnoreCase);
    }

    [DockerFact]
    public async Task ConcurrencyRecovery_OperationCanceledException_RethrowsAndAborts()
    {
        var tenant = TenantId.New();
        var entityId = Guid.NewGuid();

        var csb = new NpgsqlConnectionStringBuilder(fixture.ConnectionString)
        {
            Username = "app",
            Password = "app_dev_pw"
        };

        var tenantCtx = new FakeTenantContext { Current = tenant, HasTenant = true };
        var interceptor = new TenantSessionInterceptor(tenantCtx);
        var dbOptions = new DbContextOptionsBuilder<ConcurrencyDbContext>()
            .UseNpgsql(csb.ConnectionString)
            .UseSnakeCaseNamingConvention()
            .AddInterceptors(interceptor)
            .Options;

        await using (var seedDb = new ConcurrencyDbContext(dbOptions, tenantCtx))
        {
            var seedEntity = new ConcurrencyTestEntity
            {
                Id = entityId,
                TenantId = tenant,
                Title = "Cancellation Test"
            };
            seedDb.Entities.Add(seedEntity);
            await seedDb.SaveChangesAsync();
        }

        using var cts = new CancellationTokenSource();

        using var host = await new HostBuilder()
            .ConfigureWebHost(webBuilder =>
            {
                webBuilder.UseTestServer();
                webBuilder.ConfigureServices(services =>
                {
                    services.AddRouting();
                    services.AddSingleton<ITenantContext>(tenantCtx);
                    services.AddDbContext<ConcurrencyDbContext>(opts =>
                    {
                        opts.UseNpgsql(csb.ConnectionString)
                            .UseSnakeCaseNamingConvention()
                            .AddInterceptors(new TenantSessionInterceptor(tenantCtx));
                    });
                    services.AddWebInfrastructure();
                });
                webBuilder.Configure(app =>
                {
                    app.UseWebExceptionHandler();
                    app.UseRouting();
                    app.UseEndpoints(endpoints =>
                    {
                        endpoints.MapPatch("/cancelled-recovery/{id}", async (Guid id, HttpContext httpContext, ConcurrencyDbContext db) =>
                        {
                            httpContext.Items[HttpContextIfMatchAccessor.EnforcedMarkerItemName] = true;
                            httpContext.Items[HttpContextIfMatchAccessor.TargetKeyItemName] =
                                new ConcurrencyTargetKey(typeof(ConcurrencyTestEntity), id);

                            var entity = await db.Entities.FindAsync(id);
                            if (entity == null) return Results.NotFound();

                            // External update to create a concurrency conflict
                            await using (var extConn = new NpgsqlConnection(fixture.ConnectionString))
                            {
                                await extConn.OpenAsync();
                                await using var updateCmd = extConn.CreateCommand();
                                updateCmd.CommandText = "UPDATE test_concurrency_entities SET title = 'External Edit' WHERE id = @id";
                                updateCmd.Parameters.AddWithValue("id", id);
                                await updateCmd.ExecuteNonQueryAsync();
                            }

                            entity.Title = "Conflicted Update Before Cancel";

                            try
                            {
                                await db.SaveChangesAsync(CancellationToken.None);
                            }
                            finally
                            {
                                // Cancel the request token immediately after SaveChangesAsync throws
                                // so the exception handler receives a cancelled token when invoking GetDatabaseValuesAsync
                                cts.Cancel();
                            }

                            return Results.Ok();
                        });
                    });
                });
            })
            .StartAsync();

        var client = host.GetTestClient();

        // Send request with cts.Token; when the handler runs GetDatabaseValuesAsync(cancellationToken),
        // it throws OperationCanceledException, which is rethrown out of the handler.
        // In ASP.NET Core TestHost, rethrown exceptions propagate out of ClientHandler.SendAsync.
        var ex = await Assert.ThrowsAnyAsync<Exception>(async () =>
        {
            await client.SendAsync(new HttpRequestMessage(HttpMethod.Patch, $"/cancelled-recovery/{entityId}"), cts.Token);
        });

        // The exception escaping the pipeline must be or contain OperationCanceledException,
        // or be DbUpdateConcurrencyException (the original unhandled error when cancellation occurs),
        // proving it was NOT silently swallowed and converted into HTTP 503.
        Assert.True(
            ex is OperationCanceledException ||
            ex is DbUpdateConcurrencyException ||
            ex.InnerException is OperationCanceledException,
            $"Expected OperationCanceledException or unhandled DbUpdateConcurrencyException, but got: {ex.GetType().Name}");
    }

    private sealed class AmbientTrackingState
    {
        public Transaction? OuterTransaction { get; set; }
        public Transaction? TransactionBeforeNext { get; set; }
        public Transaction? TransactionAfterNext { get; set; }
        public Transaction? TransactionAfterDispose { get; set; }
        public bool ScopeDisposed { get; set; }
        public bool NextCompletedNormally { get; set; }
        public bool NextThrew { get; set; }
        public Exception? EscapedException { get; set; }
        public Exception? DisposalException { get; set; }
    }

    private sealed class AmbientTransactionWitnessExceptionHandler : Microsoft.AspNetCore.Diagnostics.IExceptionHandler
    {
        public Transaction? ObservedTransactionAtHandler { get; private set; }

        public ValueTask<bool> TryHandleAsync(
            HttpContext httpContext,
            Exception exception,
            CancellationToken cancellationToken)
        {
            ObservedTransactionAtHandler = Transaction.Current;
            return ValueTask.FromResult(false); // Return false so production handler handles the exception
        }
    }

    private sealed class ConcurrencyQueryCountingInterceptor : Microsoft.EntityFrameworkCore.Diagnostics.DbCommandInterceptor
    {
        public DbContextId? TargetContextId { get; set; }
        public int RecoverySelectCount { get; private set; }
        public bool RecordingEnabled { get; set; }

        public override ValueTask<Microsoft.EntityFrameworkCore.Diagnostics.InterceptionResult<System.Data.Common.DbDataReader>> ReaderExecutingAsync(
            System.Data.Common.DbCommand command,
            Microsoft.EntityFrameworkCore.Diagnostics.CommandEventData eventData,
            Microsoft.EntityFrameworkCore.Diagnostics.InterceptionResult<System.Data.Common.DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            // Fail closed: only count if TargetContextId is explicitly set AND matches eventData.Context.ContextId
            if (RecordingEnabled &&
                TargetContextId.HasValue &&
                eventData.Context != null &&
                eventData.Context.ContextId.Equals(TargetContextId.Value) &&
                command.CommandText.Contains("test_concurrency_entities", StringComparison.OrdinalIgnoreCase) &&
                command.CommandText.TrimStart().StartsWith("SELECT", StringComparison.OrdinalIgnoreCase))
            {
                RecoverySelectCount++;
            }
            return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
        }

        public override ValueTask<Microsoft.EntityFrameworkCore.Diagnostics.InterceptionResult<object>> ScalarExecutingAsync(
            System.Data.Common.DbCommand command,
            Microsoft.EntityFrameworkCore.Diagnostics.CommandEventData eventData,
            Microsoft.EntityFrameworkCore.Diagnostics.InterceptionResult<object> result,
            CancellationToken cancellationToken = default)
        {
            // Fail closed: only count if TargetContextId is explicitly set AND matches eventData.Context.ContextId
            if (RecordingEnabled &&
                TargetContextId.HasValue &&
                eventData.Context != null &&
                eventData.Context.ContextId.Equals(TargetContextId.Value) &&
                command.CommandText.Contains("test_concurrency_entities", StringComparison.OrdinalIgnoreCase) &&
                command.CommandText.TrimStart().StartsWith("SELECT", StringComparison.OrdinalIgnoreCase))
            {
                RecoverySelectCount++;
            }
            return base.ScalarExecutingAsync(command, eventData, result, cancellationToken);
        }
    }
}
