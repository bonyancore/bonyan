# Procurement module

The module is registered through `Procurement.Module` and exposes its HTTP routes under `/api/v1/procurement`.
Its PostgreSQL data lives in the `procurement` schema. Every tenant-owned table has a tenant filter and an RLS
policy; related rows use composite `(tenant_id, id)` foreign keys.

## Implemented work

- **PRC-401/402:** purchase request domain, item validation, persistence, migrations, audit fields, optimistic
  concurrency, and tenant isolation.
- **PRC-403:** request creation, numbering, submit/approve/reject workflow, and ports for PLT-206 numbering and
  PLT-208 approvals. Development settings use clearly marked `PR-DEV-*` and `PLT208-DEV-*` adapters. Replace
  these registrations with the platform adapters when those contracts are ready.
- **PRC-404:** idempotent handler for the temporary Core site-confirmation event. It saves a local WorkPackage
  snapshot and creates a numbered draft; replace the inbound contract when Core publishes its final event.
- **PRC-405/406:** tenant-scoped supplier registry using Core party IDs, manual RFQ quote entry, comparison, and award.
- **PRC-407/408:** purchase orders from approved requests or awarded quotes, project budget reservations, approval,
  immutable issue-time revision snapshots, issue/cancel events, JSON schemas, and an outbox written in the same save.
- **PRC-409/410/411:** partial goods receipts and reversals, invoice three-way matching with dispute states, and
  payment scheduling events.

The project budget endpoint stores the local limit under `/api/v1/procurement/projects/{projectId}/budget`.
Orders reserve against that limit before approval; a failed approval or cancellation releases the reservation.

## Verification

Run the Procurement domain, contract, and integration test projects. PostgreSQL RLS integration tests require
`INTEGRATION_TESTS_ENABLED=true` and a working Docker installation. Development event numbering and approval are
mock adapters; they are not production platform services.
