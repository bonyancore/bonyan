# Bonyan Backend Setup: Agent Guide

Audience: a coding agent (Claude Code or similar) working inside `D:\Documents\GitHub\bonyan\backend`.
Goal: finish the backend bootstrap in the order given below. Each step ends with a build gate. Do not move on while the gate fails.

Scope of this file:

1. Repo state and ground rules
2. Fix the broken `Directory.Build.props`
3. `Construction.Kernel` (shared building blocks)
4. PostgreSQL connection (latest version, EF Core, RLS, migrations)
5. OpenAPI and Swagger UI
6. `AppModules.cs` wiring
7. Known errors and fixes
8. Acceptance checklist

---

## 1. Repo state and ground rules

### Current state

- Repo was re-cloned fresh. Branch: `core/initial/setup/backend`.
- Template folders exist under `backend/src`, `backend/tests`, `backend/tools`, `backend/services/intelligence`. **Do not delete or rename template folders.** `.gitkeep` files are removed only when a real file is added to that folder.
- Solution `backend/Construction.sln` currently contains 7 projects, all building before the last edit:
  - `src/Shared/Construction.Kernel`
  - `src/Modules/Platform/Platform.Contracts`, `Platform.Core`, `Platform.Infrastructure`, `Platform.Api`
  - `src/Host/Construction.Api`, `src/Host/Construction.Worker`
- SDK installed: `10.0.403`. `global.json` pins `10.0.100` with `rollForward: latestFeature`.
- `Worker.cs` and the WeatherForecast sample were already removed. `Construction.Api/Program.cs` only maps `/health`.

### Architecture (from the project spec, files 00 and 20)

- .NET 10 modular monolith. One PostgreSQL database, **one schema per module**, one `DbContext` and one migration history per module.
- Folder meaning:
  - `Host/`: composition roots (`Construction.Api` for HTTP, `Construction.Worker` for jobs, outbox, consumers).
  - `Shared/`: technical building blocks, no business rules (`Kernel`, `Messaging`, `Persistence`, `Web`, `Testing`).
  - `Modules/`: business modules, all with the same four projects: `X.Contracts`, `X.Core`, `X.Infrastructure`, `X.Api`.
  - `Integrations/`: adapters to external systems behind ports.
- Dependency rules (will be enforced later by `tests/Architecture.Tests`, respect them now):
  - `X.Contracts` is the only project other modules may reference.
  - `X.Core` has **no** EF Core, ASP.NET, Hangfire or vendor SDKs.
  - `Shared/*` never references `Modules/*`.
  - `X.Api` never references `X.Infrastructure`, and the reverse.
  - No module reads another module's tables.
- Naming: folders keep short names (`Platform.Core`), but **assembly name and root namespace are prefixed with `Construction.`** (`Construction.Platform.Core`). Projects already named `Construction.*` are not double-prefixed. This is done in `Directory.Build.props` (section 2).
- Central Package Management is mandatory (`Directory.Packages.props`).
- Tenant is read **only** from the signed token claim `tid`. A client header or body field is ignored (BR-PLT-02).

### Ground rules for the agent

1. Never guess package versions. Run `dotnet add <project> package <Name>` without `--version`; with CPM it writes the resolved version into `Directory.Packages.props`. Then confirm the version is the latest stable for .NET 10.
2. Before using an API from a library whose major version changed recently (Microsoft.OpenApi, Npgsql, EF Core 10), read its current docs. Compile errors are the truth, not memory.
3. Commit after each numbered step with a conventional message. Never commit `bin/`, `obj/`, `.vs/`, `.idea/`, `*.DotSettings.user`. Check `git status --short` before every commit.
4. `TreatWarningsAsErrors` is on. Prefer fixing code. If an analyzer rule is genuinely noisy (list in section 7), downgrade it in `.editorconfig` instead of contorting code.
5. Do not edit anything under `backend/services/intelligence` (Python service, separate scope).

---

## 2. Fix `Directory.Build.props` (blocking)

The current file is invalid XML: a second `<PropertyGroup>` was nested **inside** the first one, which produces `MSB4024 ... 'PropertyGroup' start tag on line 2 does not match the end tag of 'Project'` on every project, and cascades into `CS0234` and `CS1061` in `AppModules.cs`.

Replace the whole file `backend/Directory.Build.props` with:

```xml
<Project>
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <LangVersion>latest</LangVersion>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <AnalysisLevel>latest-recommended</AnalysisLevel>
    <EnforceCodeStyleInBuild>true</EnforceCodeStyleInBuild>
  </PropertyGroup>

  <!-- Prefix assembly and namespace with "Construction." for projects that do not already start with it -->
  <PropertyGroup Condition="!$(MSBuildProjectName.StartsWith('Construction.'))">
    <AssemblyName>Construction.$(MSBuildProjectName)</AssemblyName>
    <RootNamespace>Construction.$(MSBuildProjectName)</RootNamespace>
  </PropertyGroup>
</Project>
```

Also create `backend/Directory.Packages.props`:

```xml
<Project>
  <PropertyGroup>
    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
  </PropertyGroup>
  <ItemGroup>
    <!-- versions are added by `dotnet add package` -->
  </ItemGroup>
</Project>
```

Remove any `<TargetFramework>`, `<Nullable>`, `<ImplicitUsings>` duplicates from individual `.csproj` files. Keep `UserSecretsId`, `DockerDefaultTargetOS` and similar.

Make sure the hosts reference the module registration projects:

```powershell
cd D:\Documents\GitHub\bonyan\backend
dotnet add src/Host/Construction.Api    reference src/Modules/Platform/Platform.Infrastructure src/Modules/Platform/Platform.Api
dotnet add src/Host/Construction.Worker reference src/Modules/Platform/Platform.Infrastructure
```

**Gate 2:** `dotnet build` succeeds with 0 errors. Commit: `chore(backend): fix Directory.Build.props, add CPM`.

Note: after the namespace change, the namespaces are `Construction.Platform.Core`, `Construction.Platform.Infrastructure`, etc. Any file created earlier with another namespace must be updated.

---

## 3. `Construction.Kernel`

Purpose: tiny, dependency-free domain primitives. **No NuGet packages, no references to other projects.** Everything is `Construction.Kernel` namespace unless shown.

Create these files in `src/Shared/Construction.Kernel/` (delete `Class1.cs`).

### 3.1 `Ids.cs`

```csharp
namespace Construction.Kernel;

/// <summary>Id generation. All ids are UUID v7 (time-ordered), matching the spec.</summary>
public static class Ids
{
    public static Guid New() => Guid.CreateVersion7();
}
```

### 3.2 `Tenancy/TenantContext.cs` (namespace `Construction.Kernel.Tenancy`)

```csharp
namespace Construction.Kernel.Tenancy;

public readonly record struct TenantId(Guid Value)
{
    public static TenantId New() => new(Ids.New());
    public override string ToString() => Value.ToString();
}

/// <summary>Tenant is resolved from the token claim "tid", never from a client header.</summary>
public interface ITenantContext
{
    bool HasTenant { get; }

    /// <summary>Throws if there is no tenant.</summary>
    TenantId Current { get; }
}
```

### 3.3 `IClock.cs`

```csharp
namespace Construction.Kernel;

public interface IClock
{
    DateTimeOffset UtcNow { get; }
}

public sealed class SystemClock(TimeProvider time) : IClock
{
    public SystemClock() : this(TimeProvider.System) { }
    public DateTimeOffset UtcNow => time.GetUtcNow();
}
```

### 3.4 `DomainRuleViolation.cs`

```csharp
namespace Construction.Kernel;

/// <summary>Thrown when a domain invariant is broken. Code is a stable, translatable key, e.g. "platform.last_admin".</summary>
public sealed class DomainRuleViolation : Exception
{
    public DomainRuleViolation(string code) : base(code) => Code = code;
    public DomainRuleViolation(string code, string message) : base(message) => Code = code;
    public DomainRuleViolation(string code, string message, Exception inner) : base(message, inner) => Code = code;

    public string Code { get; }
}
```

### 3.5 `Error.cs`

```csharp
namespace Construction.Kernel;

public enum ErrorType { Failure, Validation, NotFound, Conflict, Unauthorized, Forbidden }

public sealed record Error(string Code, string Message, ErrorType Type = ErrorType.Failure)
{
    public static readonly Error None = new(string.Empty, string.Empty);

    public static Error Failure(string code, string message)      => new(code, message, ErrorType.Failure);
    public static Error Validation(string code, string message)   => new(code, message, ErrorType.Validation);
    public static Error NotFound(string code, string message)     => new(code, message, ErrorType.NotFound);
    public static Error Conflict(string code, string message)     => new(code, message, ErrorType.Conflict);
    public static Error Unauthorized(string code, string message) => new(code, message, ErrorType.Unauthorized);
    public static Error Forbidden(string code, string message)    => new(code, message, ErrorType.Forbidden);
}
```

### 3.6 `Result.cs`

```csharp
namespace Construction.Kernel;

public class Result
{
    protected Result(bool isSuccess, Error error)
    {
        if (isSuccess && error != Error.None)
            throw new InvalidOperationException("A successful result cannot carry an error.");
        if (!isSuccess && error == Error.None)
            throw new InvalidOperationException("A failed result must carry an error.");

        IsSuccess = isSuccess;
        Error = error;
    }

    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    public Error Error { get; }

    public static Result Success() => new(true, Error.None);
    public static Result Failure(Error error) => new(false, error);
    public static Result<T> Success<T>(T value) => new(value, true, Error.None);
    public static Result<T> Failure<T>(Error error) => new(default, false, error);
}

public sealed class Result<T> : Result
{
    private readonly T? _value;

    internal Result(T? value, bool isSuccess, Error error) : base(isSuccess, error) => _value = value;

    /// <summary>Throws if the result is a failure.</summary>
    public T Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException("Cannot read Value of a failed result.");
}
```

### 3.7 `Money.cs`

Rules: amounts are `numeric(18,4)` in the database; currency is `char(3)`, default `EGP`; mixing currencies is a domain error.

```csharp
namespace Construction.Kernel;

public readonly record struct Money
{
    public const string DefaultCurrency = "EGP";

    public Money(decimal amount, string currency = DefaultCurrency)
    {
        if (currency is null || currency.Length != 3)
            throw new DomainRuleViolation("kernel.invalid_currency");

        Amount = amount;
        Currency = currency.ToUpperInvariant();
    }

    public decimal Amount { get; }

    /// <remarks>default(Money) has a null Currency; always construct through the constructor or <see cref="Zero"/>.</remarks>
    public string Currency { get; }

    public static Money Zero(string currency = DefaultCurrency) => new(0m, currency);

    public Money Add(Money other)
    {
        EnsureSameCurrency(other);
        return new Money(Amount + other.Amount, Currency);
    }

    public Money Subtract(Money other)
    {
        EnsureSameCurrency(other);
        return new Money(Amount - other.Amount, Currency);
    }

    public Money Multiply(decimal factor) => new(Amount * factor, Currency);

    /// <summary>Banker's rounding is NOT used: money rounds away from zero to the given decimals.</summary>
    public Money Round(int decimals = 2) =>
        new(Math.Round(Amount, decimals, MidpointRounding.AwayFromZero), Currency);

    public static Money operator +(Money a, Money b) => a.Add(b);
    public static Money operator -(Money a, Money b) => a.Subtract(b);

    private void EnsureSameCurrency(Money other)
    {
        if (!string.Equals(Currency, other.Currency, StringComparison.Ordinal))
            throw new DomainRuleViolation("kernel.currency_mismatch");
    }
}
```

> If the spec (file 00, section on money and rounding) states a different rounding rule or scale, the spec wins. Re-read it before finalizing `Round`.

### 3.8 `.editorconfig` additions

Kernel will trigger a few analyzers under `latest-recommended` + warnings-as-errors. Add to `backend/.editorconfig` (or the repo root one) instead of distorting the primitives:

```ini
[*.cs]
dotnet_diagnostic.CA1716.severity = none        # "Error" is a VB keyword; type name is deliberate
dotnet_diagnostic.CA1000.severity = suggestion  # static members on generic types (Result<T>)
dotnet_diagnostic.CA2225.severity = none        # operator alternates provided as Add/Subtract anyway
dotnet_diagnostic.CA1032.severity = none        # exception constructors: DomainRuleViolation is intentionally code-based
dotnet_diagnostic.CA1848.severity = suggestion  # LoggerMessage delegates

[**/Migrations/*.cs]
generated_code = true
```

**Gate 3:** `dotnet build` passes. Commit: `feat(kernel): add Ids, TenantId, IClock, Error, Result, Money, DomainRuleViolation`.

---

## 4. PostgreSQL connection

### 4.1 Versions

- **PostgreSQL:** use the latest GA major. As of the last verified information that is **PostgreSQL 18**. Check <https://www.postgresql.org/support/versioning/> and Docker Hub tag `postgres`; if a newer major is GA, use it, otherwise `postgres:18`. Do not use `latest` tag in compose, pin the major.
- **EF Core 10** with **Npgsql.EntityFrameworkCore.PostgreSQL 10.x** (the provider major matches the EF Core major). Let `dotnet add package` pick the version.
- **EFCore.NamingConventions** for snake_case, only if a build for EF Core 10 exists; otherwise map names explicitly. Verify on NuGet.
- PostgreSQL 18 ships a native `uuidv7()` function. The app generates ids itself (`Ids.New()`), but SQL seeds and defaults may use `uuidv7()`.

### 4.2 `docker-compose.yml` (repo root)

Important PostgreSQL 18+ image change: the data volume mounts at **`/var/lib/postgresql`**, not `/var/lib/postgresql/data` (the image keeps versioned subdirectories there). Mounting the old path on 18+ causes a startup error or a wiped volume on upgrade.

```yaml
services:
  postgres:
    image: postgres:18
    container_name: bonyan-postgres
    environment:
      POSTGRES_USER: postgres
      POSTGRES_PASSWORD: postgres_dev
      POSTGRES_DB: construction
    ports:
      - "5432:5432"
    volumes:
      - pgdata:/var/lib/postgresql
      - ./deploy/docker/initdb:/docker-entrypoint-initdb.d:ro
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U postgres -d construction"]
      interval: 5s
      timeout: 3s
      retries: 10

volumes:
  pgdata:
```

If the folder `deploy/docker/initdb` does not exist, create it. Init scripts run **only on first start of an empty volume**. To re-run: `docker compose down -v` (destroys local data).

### 4.3 `deploy/docker/initdb/01-roles-schemas.sql`

```sql
-- Application role: cannot bypass RLS (BR-PLT-01). Migrations run as `postgres` (owner), the app runs as `app`.
CREATE ROLE app LOGIN PASSWORD 'app_dev_pw' NOSUPERUSER NOBYPASSRLS;

CREATE EXTENSION IF NOT EXISTS citext;
CREATE EXTENSION IF NOT EXISTS pg_trgm;
CREATE EXTENSION IF NOT EXISTS unaccent;

DO $$
DECLARE s text;
BEGIN
  FOREACH s IN ARRAY ARRAY['platform','core','reporting','estimating','planning',
    'procurement','sourcing','cost','contracts','cash','subcontracts','docs',
    'closeout','intelligence']
  LOOP
    EXECUTE format('CREATE SCHEMA IF NOT EXISTS %I', s);
    EXECUTE format('GRANT USAGE ON SCHEMA %I TO app', s);
    EXECUTE format('ALTER DEFAULT PRIVILEGES IN SCHEMA %I GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO app', s);
    EXECUTE format('ALTER DEFAULT PRIVILEGES IN SCHEMA %I GRANT USAGE, SELECT ON SEQUENCES TO app', s);
  END LOOP;
END $$;
```

Run `docker compose up -d`, then verify:

```powershell
docker compose ps
docker compose exec postgres psql -U postgres -d construction -c "\dn"
```

Expected: 14 module schemas plus `public`.

### 4.4 Packages

```powershell
cd D:\Documents\GitHub\bonyan\backend

dotnet new classlib -n Construction.Persistence -o src/Shared/Construction.Persistence --framework net10.0
Remove-Item src/Shared/Construction.Persistence/Class1.cs
dotnet sln add src/Shared/Construction.Persistence
dotnet add src/Shared/Construction.Persistence reference src/Shared/Construction.Kernel
dotnet add src/Modules/Platform/Platform.Infrastructure reference src/Shared/Construction.Persistence

dotnet add src/Shared/Construction.Persistence package Npgsql.EntityFrameworkCore.PostgreSQL
dotnet add src/Shared/Construction.Persistence package EFCore.NamingConventions
dotnet add src/Host/Construction.Api package Microsoft.EntityFrameworkCore.Design

dotnet tool install --global dotnet-ef   # or: dotnet tool update --global dotnet-ef
```

`Microsoft.EntityFrameworkCore.Design` goes on the **startup project** (`Construction.Api`) with `PrivateAssets=all` (the template adds it automatically through `dotnet add` on recent SDKs; verify).

`Platform.Core` must stay free of EF Core. EF types appear only in `Platform.Infrastructure` and `Construction.Persistence`.

### 4.5 `Construction.Persistence`

`ModuleDbContext.cs`:

```csharp
using Microsoft.EntityFrameworkCore;

namespace Construction.Persistence;

/// <summary>Base for every module DbContext: sets the module's schema.</summary>
public abstract class ModuleDbContext(DbContextOptions options, string schema) : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(schema);
        base.OnModelCreating(modelBuilder);
    }
}
```

`TenantSessionInterceptor.cs`: sets `app.tenant_id` on every opened connection. With no tenant it sets an empty string, so `current_setting('app.tenant_id')::uuid` in the RLS policy throws, which makes the system **fail closed**.

```csharp
using System.Data.Common;
using Construction.Kernel.Tenancy;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Construction.Persistence;

public sealed class TenantSessionInterceptor(ITenantContext tenant) : DbConnectionInterceptor
{
    public override async Task ConnectionOpenedAsync(
        DbConnection connection, ConnectionEndEventData eventData,
        CancellationToken cancellationToken = default)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "select set_config('app.tenant_id', @t, false)";
        var p = cmd.CreateParameter();
        p.ParameterName = "t";
        p.Value = tenant.HasTenant ? tenant.Current.ToString() : string.Empty;
        cmd.Parameters.Add(p);
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
        => ConnectionOpenedAsync(connection, eventData).GetAwaiter().GetResult();
}
```

Add a `Construction.Persistence` extension later for shared conventions (standard columns, soft-delete filter, audit stamping). The standard columns are defined in spec file 00, section 3.2: `id, tenant_id, created_at/by, updated_at/by, row_version, is_deleted, source`. **Read that section before implementing them; do not invent the shape.**

### 4.6 First module context: Platform

`Platform.Core/Domain/Tenancy/Tenant.cs` (no EF references):

```csharp
using Construction.Kernel;

namespace Construction.Platform.Core.Domain.Tenancy;

public sealed class Tenant
{
    private Tenant() { }   // for EF

    public Guid Id { get; private set; }
    public string Code { get; private set; } = default!;
    public string? NameAr { get; private set; }
    public string? NameEn { get; private set; }
    public string Status { get; private set; } = "provisioning";
    public string PlanCode { get; private set; } = "starter";
    public string BaseCurrency { get; private set; } = "EGP";
    public string TimeZone { get; private set; } = "Africa/Cairo";
    public string DefaultLocale { get; private set; } = "ar";
    public string CountryCode { get; private set; } = "EG";
    public long PermissionsVersion { get; private set; } = 1;

    public static Tenant Create(Guid id, string code, string? nameAr, string? nameEn, string planCode)
    {
        if (string.IsNullOrWhiteSpace(nameAr) && string.IsNullOrWhiteSpace(nameEn))
            throw new DomainRuleViolation("platform.tenant_name_required");

        return new Tenant
        {
            Id = id,
            Code = code.ToLowerInvariant(),
            NameAr = nameAr,
            NameEn = nameEn,
            PlanCode = planCode,
        };
    }
}
```

This is the subset needed for the first migration. The full `platform.tenants` definition (spec file 01, section 4.1) also has `commercial_reg_no, tax_registration_no, address (jsonb), logo_file_id, trial_ends_at, suspended_at, suspension_reason, deletion_requested_at`. Add them when the corresponding features are built, with a migration each.

`Platform.Infrastructure/Persistence/PlatformDbContext.cs`:

```csharp
using Construction.Persistence;
using Construction.Platform.Core.Domain.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace Construction.Platform.Infrastructure.Persistence;

public sealed class PlatformDbContext(DbContextOptions<PlatformDbContext> options)
    : ModuleDbContext(options, Schema)
{
    public const string Schema = "platform";

    public DbSet<Tenant> Tenants => Set<Tenant>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PlatformDbContext).Assembly);
    }
}
```

`Platform.Infrastructure/Persistence/Configurations/TenantConfiguration.cs`:

```csharp
using Construction.Platform.Core.Domain.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Construction.Platform.Infrastructure.Persistence.Configurations;

internal sealed class TenantConfiguration : IEntityTypeConfiguration<Tenant>
{
    public void Configure(EntityTypeBuilder<Tenant> b)
    {
        b.ToTable("tenants", t =>
        {
            t.HasCheckConstraint("ck_tenants_name", "name_ar is not null or name_en is not null");
            t.HasCheckConstraint("ck_tenants_status",
                "status in ('provisioning','active','trial','suspended','pending_deletion','deleted')");
            t.HasCheckConstraint("ck_tenants_plan", "plan_code in ('starter','growth','enterprise')");
            t.HasCheckConstraint("ck_tenants_code", "code ~ '^[a-z0-9-]{3,40}$'");
        });

        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedNever();
        b.HasIndex(x => x.Code).IsUnique();
        b.HasIndex(x => x.Status);
        b.HasIndex(x => x.PlanCode);
        b.Property(x => x.BaseCurrency).HasColumnType("char(3)");
        b.Property(x => x.CountryCode).HasColumnType("char(2)");
    }
}
```

`platform.tenants` has **no `tenant_id` column and no RLS** (its `id` is the tenant id). Every other tenant-owned table gets `tenant_id` and the policy in 4.8.

`Platform.Infrastructure/PlatformInfrastructureRegistration.cs`:

```csharp
using Construction.Persistence;
using Construction.Platform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Construction.Platform.Infrastructure;

public static class PlatformInfrastructureRegistration
{
    public static IServiceCollection AddPlatformInfrastructure(
        this IServiceCollection services, IConfiguration config)
    {
        services.AddScoped<TenantSessionInterceptor>();

        services.AddDbContext<PlatformDbContext>((sp, o) => o
            .UseNpgsql(
                config.GetConnectionString("Default"),
                n => n.MigrationsHistoryTable("__ef_migrations_history", PlatformDbContext.Schema))
            .UseSnakeCaseNamingConvention()
            .AddInterceptors(sp.GetRequiredService<TenantSessionInterceptor>()));

        return services;
    }
}
```

If the build complains about missing `Microsoft.Extensions.*` types in `Platform.Infrastructure`, add `Microsoft.Extensions.DependencyInjection.Abstractions` and `Microsoft.Extensions.Configuration.Abstractions` with `dotnet add package`.

For `jsonb` columns later (Npgsql 8+ requires explicit opt-in for dynamic JSON), prefer typed owned/complex JSON mapping or configure `NpgsqlDataSourceBuilder.EnableDynamicJson()` deliberately, not globally by default.

### 4.7 Connection strings and the Api host

`src/Host/Construction.Api/appsettings.Development.json`:

```json
{
  "ConnectionStrings": {
    "Default": "Host=localhost;Port=5432;Database=construction;Username=app;Password=app_dev_pw"
  }
}
```

Never commit real credentials. For non-dev environments use environment variables (`ConnectionStrings__Default`) or the secret store. Migrations use the **owner** connection (below), not `Default`.

Temporary tenant context until auth exists. In `Program.cs` (full file in section 6):

```csharp
builder.Services.AddScoped<ITenantContext, AnonymousTenantContext>(); // TEMP: replaced by the "tid" claim reader
```

### 4.8 RLS pattern (use from the first table that has `tenant_id`, e.g. `platform.users`)

Add as raw SQL inside the migration's `Up` (`migrationBuilder.Sql(...)`), for every tenant-owned table:

```sql
ALTER TABLE platform.users ENABLE ROW LEVEL SECURITY;
ALTER TABLE platform.users FORCE  ROW LEVEL SECURITY;

CREATE POLICY tenant_isolation ON platform.users
  USING      (tenant_id = current_setting('app.tenant_id')::uuid)
  WITH CHECK (tenant_id = current_setting('app.tenant_id')::uuid);
```

Open design point for the architect (do not decide alone): login looks a user up by email **before** a tenant is known, and `platform.users.email` is unique across tenants. That path needs a narrowly scoped `SECURITY DEFINER` function or a dedicated role. Leave it out of this bootstrap.

### 4.9 Migrations

```powershell
cd D:\Documents\GitHub\bonyan\backend

dotnet ef migrations add InitPlatform `
  --project src/Modules/Platform/Platform.Infrastructure `
  --startup-project src/Host/Construction.Api `
  --context PlatformDbContext `
  --output-dir Persistence/Migrations

dotnet ef database update `
  --project src/Modules/Platform/Platform.Infrastructure `
  --startup-project src/Host/Construction.Api `
  --context PlatformDbContext `
  --connection "Host=localhost;Port=5432;Database=construction;Username=postgres;Password=postgres_dev"
```

Verify:

```powershell
docker compose exec postgres psql -U postgres -d construction -c "\dt platform.*"
docker compose exec postgres psql -U postgres -d construction -c "select * from platform.__ef_migrations_history"
```

Conventions: one migration history table per schema (`<schema>.__ef_migrations_history`), migrations live in `X.Infrastructure/Persistence/Migrations`, one `DbContext` per module, never one shared context.

**Gate 4:** build passes; `docker compose up -d` healthy; migration applied; `platform.tenants` exists with its check constraints. Commit: `feat(platform): PostgreSQL connection, PlatformDbContext, tenants table`.

---

## 5. OpenAPI and Swagger UI

### 5.1 Decision

.NET 10 ships **built-in OpenAPI document generation** (`Microsoft.AspNetCore.OpenApi`, OpenAPI 3.1 by default). The project templates no longer include Swashbuckle. So:

- **Document generation:** `Microsoft.AspNetCore.OpenApi` (`AddOpenApi` / `MapOpenApi`). Do **not** add Swashbuckle's generator (`Swashbuckle.AspNetCore` meta-package or `SwaggerGen`).
- **UI:** Swagger UI via `Swashbuckle.AspNetCore.SwaggerUI` only (UI assets, pointed at the built-in JSON). Optional alternative: `Scalar.AspNetCore`. Pick Swagger UI because it was requested; keep Scalar as a documented alternative only.
- Expose both the JSON and the UI **only in Development** for now. Production exposure is a later decision (needs auth).

### 5.2 Packages (host only)

```powershell
dotnet add src/Host/Construction.Api package Microsoft.AspNetCore.OpenApi
dotnet add src/Host/Construction.Api package Swashbuckle.AspNetCore.SwaggerUI
```

Check that the resolved `Microsoft.AspNetCore.OpenApi` major is 10 and `Swashbuckle.AspNetCore.SwaggerUI` is the latest stable. If the template already referenced `Microsoft.AspNetCore.OpenApi`, keep a single entry in `Directory.Packages.props`.

Swagger UI must be able to render OpenAPI 3.1 documents; if the chosen Swashbuckle UI version bundles an old Swagger UI that fails on 3.1, either upgrade it or serve a 3.0 document by setting `options.OpenApiVersion = Microsoft.OpenApi.OpenApiSpecVersion.OpenApi3_0;` in `AddOpenApi`. Verify in the browser, do not assume.

### 5.3 Module API projects need the ASP.NET shared framework

`Platform.Api` (and every `X.Api`, and `Construction.Web`) are class libraries that define endpoints. They need the framework reference, not a package:

```xml
<ItemGroup>
  <FrameworkReference Include="Microsoft.AspNetCore.App" />
</ItemGroup>
```

Add it to `Platform.Api.csproj` and `src/Shared/Construction.Web/Construction.Web.csproj` (when that project is created). `X.Core`, `X.Contracts`, `X.Infrastructure` must **not** get it.

### 5.4 `Program.cs` for `Construction.Api`

```csharp
using Construction.Api;
using Construction.Kernel.Tenancy;
using Microsoft.AspNetCore.OpenApi;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddScoped<ITenantContext, AnonymousTenantContext>(); // TEMP until the "tid" claim reader exists
builder.Services.AddAppModules(builder.Configuration);

builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer((document, _, _) =>
    {
        document.Info.Title = "Construction API";
        document.Info.Version = "v1";
        document.Info.Description = "Bonyan construction ERP, modular monolith.";
        return Task.CompletedTask;
    });
    options.AddDocumentTransformer<BearerSecuritySchemeTransformer>();
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();                       // GET /openapi/v1.json
    app.UseSwaggerUI(ui =>
    {
        ui.SwaggerEndpoint("/openapi/v1.json", "Construction API v1");
        ui.RoutePrefix = "swagger";         // UI at /swagger
    });
}

app.MapGet("/health", () => Results.Ok(new { status = "ok" }))
   .WithTags("Health")
   .WithName("Health");

app.MapAppModules();

await app.RunAsync();

internal sealed class AnonymousTenantContext : ITenantContext
{
    public bool HasTenant => false;
    public TenantId Current => throw new InvalidOperationException("No tenant in this request.");
}
```

### 5.5 JWT bearer scheme in the document

`src/Host/Construction.Api/BearerSecuritySchemeTransformer.cs`. `Microsoft.OpenApi` moved to a 2.x object model in .NET 10 (namespaces and collection types changed; for example `Microsoft.OpenApi.Models` became `Microsoft.OpenApi`). Write it against the version actually installed and let the compiler guide small differences:

```csharp
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Construction.Api;

internal sealed class BearerSecuritySchemeTransformer : IOpenApiDocumentTransformer
{
    public Task TransformAsync(
        OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
    {
        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
        document.Components.SecuritySchemes["Bearer"] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            Description = "Access token. The tenant is taken from the 'tid' claim, never from a header.",
        };
        return Task.CompletedTask;
    }
}
```

Do not add a global `SecurityRequirement` until authentication is actually implemented; adding it earlier makes Swagger UI show a lock on endpoints that are open.

### 5.6 Conventions for module endpoints (apply when Platform.Api gets endpoints)

- One `RouteGroupBuilder` per module, prefix `/api/v1/<module>` (e.g. `/api/v1/platform`), `.WithTags("Platform")`.
- Every endpoint: `.WithName(...)` (unique operationId), `.Produces<T>()` / `.ProducesProblem(...)`. Errors use RFC 7807 `ProblemDetails` mapped from `Result.Error.Type`.
- DTOs come from `X.Contracts` only; never expose Core entities.
- Optional: enable XML doc comments in the Api projects for summaries (`GenerateDocumentationFile` true on `X.Api` projects only, and silence CS1591 there).

### 5.7 Verify

```powershell
dotnet run --project src/Host/Construction.Api
```

Open (port is printed in the console, `launchSettings.json` default was 5045):

- `http://localhost:5045/health` returns `{"status":"ok"}`
- `http://localhost:5045/openapi/v1.json` returns the document
- `http://localhost:5045/swagger` shows Swagger UI with the `Health` endpoint

**Gate 5:** all three URLs work. Commit: `feat(api): OpenAPI document and Swagger UI (development only)`.

---

## 6. `AppModules.cs` wiring

### 6.1 Known gap in the spec (do not solve silently)

The spec describes an `IModule` per module with `AddServices` and `MapEndpoints`, but also forbids `X.Api` from referencing `X.Infrastructure` and the reverse. The module's entry class has no project that may reference both. **For now:** each host keeps an explicit list of registration calls. Raise this as an ADR (`docs/adr`) for the architect: options are a fifth tiny `X.Module` project, putting the entry class in the Host, or relaxing the rule.

### 6.2 `src/Host/Construction.Api/AppModules.cs`

```csharp
using Construction.Platform.Infrastructure;

namespace Construction.Api;

internal static class AppModules
{
    public static IServiceCollection AddAppModules(this IServiceCollection services, IConfiguration config)
    {
        services.AddPlatformInfrastructure(config);
        // services.AddPlatformApi();   // when Platform.Api exposes its registration
        return services;
    }

    public static IEndpointRouteBuilder MapAppModules(this IEndpointRouteBuilder app)
    {
        // app.MapPlatformEndpoints();  // when Platform.Api defines endpoints
        return app;
    }
}
```

### 6.3 `src/Host/Construction.Worker/AppModules.cs`

Services only (no endpoints). The Worker also needs an `ITenantContext` registration (job-scoped, set per job from the outbox event or job payload); until jobs exist register the same anonymous stub.

```csharp
using Construction.Platform.Infrastructure;

namespace Construction.Worker;

internal static class AppModules
{
    public static IServiceCollection AddAppModules(this IServiceCollection services, IConfiguration config)
    {
        services.AddPlatformInfrastructure(config);
        return services;
    }
}
```

Worker `Program.cs`:

```csharp
using Construction.Kernel.Tenancy;
using Construction.Worker;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddScoped<ITenantContext, AnonymousTenantContext>(); // TEMP
builder.Services.AddAppModules(builder.Configuration);

await builder.Build().RunAsync();

internal sealed class AnonymousTenantContext : ITenantContext
{
    public bool HasTenant => false;
    public TenantId Current => throw new InvalidOperationException("No tenant in this job.");
}
```

Copy the connection string into `Construction.Worker/appsettings.Development.json`.

**Gate 6:** `dotnet build` passes; both hosts start. Commit: `feat(hosts): AppModules registration`.

---

## 7. Known errors and fixes

| Symptom | Cause | Fix |
|---|---|---|
| `MSB4024 ... Directory.Build.props could not be loaded ... PropertyGroup start tag ... does not match the end tag of 'Project'` | Second `PropertyGroup` nested inside the first | Replace the file with the one in section 2 |
| `CS0234 type or namespace 'Platform' does not exist in 'Construction'` in `AppModules.cs` | Platform projects failed to build (props error) or the host lacks a `ProjectReference` | Fix props, then `dotnet add src/Host/Construction.Api reference src/Modules/Platform/Platform.Infrastructure` |
| `CS1061 'IServiceCollection' does not contain 'AddPlatformInfrastructure'` | Same cause, or the registration class is in a different namespace | Namespace must be `Construction.Platform.Infrastructure`; class must be `public static` |
| `dotnet sln add (Get-ChildItem ...)` says "Could not find project" | `Get-ChildItem` objects are passed as bare file names | Use `Get-ChildItem ... \| ForEach-Object FullName` |
| `CA1848`, `CA1727` on logging calls | Analyzer rules under warnings-as-errors | Use `[LoggerMessage]` source generator, or downgrade CA1848 (section 3.8) |
| `CA1852 Type can be sealed` | Template code | Seal the type |
| `CA1716` on `Error` | VB keyword rule | Suppressed in `.editorconfig` (section 3.8) |
| Analyzer errors inside `Migrations/*.cs` | Generated code | `generated_code = true` for `**/Migrations/*.cs` |
| `git mv` fails with `.../Construction.Api/Construction.Api` | Destination folder already existed | Move contents, not the folder; or use `Move-Item` into the existing folder |
| Postgres container restarts or volume looks empty on 18 | Data volume mounted at the old `/var/lib/postgresql/data` path | Mount `/var/lib/postgresql` (section 4.2) |
| Init SQL not applied | Init scripts run only on an empty volume | `docker compose down -v` then `up -d` |
| `dotnet ef` cannot create the context | Missing `ITenantContext` registration in the host, or missing Design package | Register the stub (section 5.4), add `Microsoft.EntityFrameworkCore.Design` to the startup project |
| `42501 permission denied` at runtime | App role lacks privileges on a new table | Default privileges in `01-roles-schemas.sql` only cover tables created by the role that ran it; run migrations as `postgres` or add `ALTER DEFAULT PRIVILEGES FOR ROLE postgres ...` |
| Swagger UI blank or "unsupported OpenAPI version" | Swagger UI build cannot read 3.1 | Upgrade `Swashbuckle.AspNetCore.SwaggerUI` or emit 3.0 (section 5.2) |

---

## 8. Acceptance checklist

Run from `backend/`:

```powershell
dotnet build                                   # 0 errors, 0 warnings-as-errors
docker compose up -d                           # postgres healthy
dotnet ef database update ...                  # InitPlatform applied (section 4.9)
dotnet run --project src/Host/Construction.Api
```

- [ ] `Directory.Build.props` valid; assemblies named `Construction.*`
- [ ] `Directory.Packages.props` present; no `Version=` on `PackageReference` in `.csproj`
- [ ] Kernel has no package or project references
- [ ] `Platform.Core` has no EF Core / ASP.NET reference
- [ ] `platform.tenants` exists; constraints present; no RLS on it
- [ ] `app` role is `NOSUPERUSER NOBYPASSRLS`
- [ ] `/health`, `/openapi/v1.json`, `/swagger` all work in Development
- [ ] No `bin/`, `obj/`, `.vs/`, `.idea/` in `git status`
- [ ] ADR drafted for the module entry-point gap (section 6.1)

### Out of scope for this bootstrap (next tasks, in order)

1. `tests/Architecture.Tests` enforcing the dependency rules.
2. `platform.users` with RLS and the login lookup design.
3. Outbox tables (`platform.outbox`, `outbox_deliveries`, `processed_events`) and the dispatcher in the Worker.
4. Remaining `Shared` projects (`Messaging`, `Web`, `Testing`) and the other 13 modules, created only when needed.
