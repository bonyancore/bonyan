# Tests

- `Modules/<Module>/`: Domain.Tests (no database) and Integration.Tests (Testcontainers + WebApplicationFactory)
- `Architecture.Tests/`: dependency and layering rules
- `Contract.Tests/`: event schemas and OpenAPI diff
- `Tenant.Isolation.Tests/`: tenant A cannot read tenant B, even with raw SQL
- `GoldenProject.Tests/`: plumbing and cement scenarios across modules
- `E2E/`: Playwright (web), Maestro (mobile). Critical flows F2 and F4
- `Load/`: k6 scripts
