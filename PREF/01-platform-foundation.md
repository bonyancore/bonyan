---
title: "01 Platform foundation"
doc_no: "01"
section: "Foundation"
status: "Proposal"
source: https://app.notion.com/p/be34cdd2129682af9af901adfbca3462?pvs=204
exported: 2026-10-29 (Notion MCP)
---

# 01 — Platform Foundation (key `PLT`, schema `platform`)

**Audience:** backend team (.NET 10, PostgreSQL). **Reads with:** `00-Backend-Architecture-and-Conventions.md` (called "00" below; conventions are not restated, they are referenced as "per 00 §x"), v3 business spec sections 1.5, 2.3, 4.0 and 5. <br> **Release:** the whole platform is **R1** unless a feature is tagged R2/R3. Sprint 0 builds the walking-skeleton subset (tenants, users, auth, RLS, outbox, audit interceptor) listed in the epic table of section 14.

---

## 1. Purpose and business context

### 1.1 In plain language

Construction Connect is used by contractors (companies). One company is one **tenant** (مستأجر), the isolated workspace in which all its projects, money and people live. Contractors run many things at once: a site foreman asks for cement from a phone, a project manager approves a purchase order, a finance controller pays a supplier, a client reviews a payment application (مستخلص). All of these share the same small set of "plumbing" needs. The platform module is that plumbing. It contains no construction logic. It answers:

| Business question | Platform answer |
|---|---|
| Who is this person and which company do they belong to? | Tenants, users, authentication (PLT-E1) |
| What may they see and do, on which projects, up to what amount, and which numbers are hidden from them? | Roles, a master catalogue of permission codes, project membership, amount limits, field visibility (PLT-E2) |
| Who must approve this purchase order or variation, in what order, who covers when the approver is away, what happens when nobody answers? | Approval engine (PLT-E3) |
| How do other modules learn that something happened, safely, exactly once in effect? | Outbox, dead-letter, processed-events (PLT-E4) |
| Who did what, when, with which old and new value; how did the status move? | Audit log and status history (PLT-E5) |
| Where are photos, delivery notes, invoices and drawings? | Files (PLT-E6) |
| How is the right person told, in Arabic, on the right channel? | Notifications (PLT-E7) |
| Where do "PO-1066" and "PA-04" come from, with no gaps and no duplicates? | Number series (PLT-E8) |
| How does an Excel BOQ or supplier list get in with an Arabic error list? | Import framework (PLT-E9) |
| How do I find a record by number, supplier or trade? | Search (PLT-E10) |
| Where do comments, @mentions and my to-do list live? | Comments and tasks inbox (PLT-E11) |
| How do nightly and scheduled things run reliably? | Jobs (PLT-E12) |
| What if a mobile phone sends the same request twice, or two users edit the same record? | Idempotency and concurrency (PLT-E13) |
| Arabic, RTL, Hijri dates, Friday weekend, Arabic numerals? | Localization and calendars (PLT-E14) |
| What does the Starter plan include compared with Enterprise? Can the company take its data and leave? | Plan feature flags, tenant export and deletion (PLT-E15) |
| Can each user keep their own grid layouts and filters? | Saved views (PLT-E16) |

### 1.2 Terms defined for engineers new to construction

| Term | Meaning |
|---|---|
| Contractor (مقاول) | The paying customer company; one tenant. |
| Project (مشروع) | A building or infrastructure job. Most records belong to exactly one project. |
| Work package / trade (حزمة عمل) | A slice of the project by trade: plumbing, concrete, finishes. Budgets and progress are tracked per work package. |
| BOQ (جدول الكميات) | Bill of quantities: the priced list of what the client pays for. Owned by `contracts`. |
| PO (أمر شراء) | Purchase order sent to a supplier. |
| Payment application / certificate (مستخلص / شهادة دفع) | The periodic claim for money earned, certified by the client or engineer. |
| Variation order (أمر تغيير) | A formal change to scope and price. |
| Retention (محتجزات) | A percentage held back from payments until the work is complete. |
| Foreman / superintendent (مشرف موقع / مهندس موقع) | Site users with weak connectivity who must not see costs and margins. |
| Segregation of duties (فصل المهام) | The person who raised a record may not approve it. |

### 1.3 How the platform fits the master lifecycle and flows F1-F7

The master lifecycle is: tender and estimate (F1) -> project setup -> plan -> site request -> purchase -> deliver -> invoice -> pay -> measure -> certify -> collect -> close. In every flow the platform is the common layer:

| Flow | Platform part |
|---|---|
| F1 Tender to project | Number series (`TN-`, `EST-`), approvals on bid decisions, import of tender BOQ |
| F2 Site request to purchase (mobile) | Idempotent mobile writes (file 15), approvals on POs by amount, notifications, tasks inbox |
| F3 Delivery to payment | Files (delivery notes, invoices), approvals on payment batches, status history |
| F4 Progress to certificate | Approvals on payment applications, comments, audit |
| F5 Variations and claims | Approvals, portal sessions (file 15), audit |
| F6 Subcontract valuation | Approvals, number series, notifications |
| F7 Close-out | Tasks, files, tenant export |

The v3 cross-flow rules (section 2.3) map to platform features:

| v3 rule | Platform feature |
|---|---|
| "Every cost record carries a project, work package and cost code" | Enforced by modules; platform provides `project_members` scope and the `IProjectContext` guard (BR-PLT-12) |
| "Approved records are not edited silently" | Audit log with before/after, versioning helpers, `If-Match` concurrency, immutability guard (BR-PLT-30, BR-PLT-31) |
| "Approval chains are configurable per company; limits by amount, project, trade" | `approval_policies` and `approval_policy_steps` (PLT-E3) |
| "Prices and terms stored as at decision time" | Approval instance stores a **snapshot** of the amount and policy version at request time (BR-PLT-41) |
| "Every approval and status change is in the audit trail" | `audit_log` + `status_history` written by interceptor (PLT-E5) |
| "Statuses visible to everyone concerned" | Status history API, portal-safe status projection (file 15), notifications |

### 1.4 What this module deliberately does not do

- It holds no business aggregates (PO, budget, ...), no cost logic, no company settings such as units and tax codes (those are `core`, COR-E1), no document control (that is `docs`).
- It is not a general BPM or workflow designer. The approval engine routes approvals of **records owned by other modules**; it does not model arbitrary flows (00 §1 non-goals).
- Identity is not federated in v1. SSO (Enterprise) is an R2 add-on described as an open question.

### 1.5 Plans (v3 section 1.5)

Plans (Starter, Growth, Enterprise) change limits and the availability of some features. All modules ship to everyone; the platform only gates **limits** (active projects, users) and **feature flags** (custom approval chains, full/branded portals, custom and scheduled reports, accounting/ETA export, API and SSO, AI pack add-on). The gating matrix is in section 4.15 and rules BR-PLT-80..BR-PLT-86.

---

## 2. Users and roles

Built-in roles come from 00 §4.4. Platform-specific duties:

| Role | What it does in the platform module |
|---|---|
| `tenant_admin` | Creates users, assigns roles and project membership, edits custom roles, manages approval policies (if the plan allows custom chains), number series, tenant settings, imports, exports the tenant's data, views the audit log. Has no business-data permissions by default (cannot approve POs). |
| `owner_executive` | Reads audit log summaries, approves "large" items (limits set in policies), sees the approval backlog across projects, can request a tenant export together with `tenant_admin`. |
| `project_manager` | Manages members of own projects (if granted `platform.project_member.manage`), approves by limit, delegates approvals, sees tasks and comments. |
| `superintendent`, `foreman` | Receive tasks and notifications, comment, upload photos, submit mobile writes. Fields hidden by visibility policy (cost, rates, margin). |
| `procurement_officer`, `finance_controller`, `cost_qs_engineer`, `contracts_manager`, `document_controller`, `qa_qc_engineer`, `safety_officer`, `estimator` | Use approvals, comments, tasks, imports, files and searches according to the permission catalogue (section 10). |
| `supplier_user`, `subcontractor_user`, `client_user` | **External users.** Not in `platform.users`; stored in `platform.external_users` (file 15). They use the same files, comments (external visibility only), notifications (email) and idempotency services, with a separate audience `aud=portal`. |
| `platform_operator` (staff of the product company, not a tenant role) | Console under `/api/v1/operator/...` for tenant lifecycle, plan changes, dead-letter replay across tenants, tenant export/delete execution. Authenticated against a separate operator realm; every action is audited into `platform.audit_log` with `actor_type='operator'`. |
| `system` | Jobs, outbox dispatcher and consumers. Audit rows carry `actor_type='system'`. |

---

## 3. Services and components

Services marked **Aggregate** carry business invariants and a state machine; the rest are CRUD or infrastructure.

| Component | Kind | Responsibility |
|---|---|---|
| `TenantProvisioningService` | Aggregate (`Tenant`) | Create tenant, seed system roles, permissions mapping, default approval policies, number series, calendar, message overrides, first `tenant_admin`; suspend, reactivate, plan change. |
| `TenantContext` / `TenantMiddleware` | Infra | Resolve `tid` from token, set `app.tenant_id` for RLS per connection scope, reject suspended tenants. |
| `AuthService` | Aggregate (`UserCredential`) | Password login, lockout, MFA (TOTP), refresh-token rotation, password reset, invitation acceptance, session list and revoke. |
| `UserService` | CRUD + rules | Invite, update profile, deactivate/reactivate (publishes events), role assignment, seat counting against plan. |
| `RoleService` / `PermissionCatalogue` | CRUD + seed | System roles (immutable), custom roles (Growth+), role-permission grants, catalogue seed and drift test. |
| `AuthorizationContext` (`IAuthorizationContext`) | Infra | Effective permissions = union of role grants, filtered by project membership and amount limits; cached per request and per user (cache keyed by `permissions_version`). |
| `ProjectMembershipService` | CRUD + rules | Members of a project, per-project role subset and limits, "visible project ids" for list trimming. |
| `FieldVisibilityService` (`IFieldVisibility`) | Infra | Omit fields per user, per project policy; used by DTO builders and exporters. |
| `ApprovalService` (`IApprovalService`) | Aggregate (`ApprovalInstance`) | Policy match, step building, decide, delegate, escalate, cancel, SoD check, snapshots, events. |
| `ApprovalPolicyAdmin` | CRUD + validation | Policies and steps, overlap detection, effective dating, dry-run ("which chain would this amount follow?"). |
| `DelegationService` | CRUD + rules | Delegations by period, scope, amount cap. |
| `EscalationJob` / `ApprovalReminderJob` | Job | Remind and escalate overdue steps. |
| `OutboxWriter` (`IOutbox`) | Infra | Insert event + deliveries in the caller's transaction. |
| `OutboxDispatcher` | Worker | Poll, dispatch per consumer in order per aggregate, retry with backoff, dead-letter. |
| `DeadLetterAdmin` | CRUD + action | List, inspect, replay, discard (with reason). |
| `EventSchemaRegistry` | Infra | JSON schema per event type, validated on publish in non-production and in CI. |
| `AuditInterceptor` / `AuditWriter` (`IAuditWriter`) | Infra | Capture entity changes, custom entries, hash chain. |
| `StatusHistoryInterceptor` | Infra | Write `status_history` when a `status` column changes. |
| `FileService` (`IFileService`) | Aggregate (`FileRecord`) | Upload sessions, completion, scan status, links, thumbnails, signed download URLs, retention. |
| `NotificationService` (`INotificationService`) | Infra + CRUD | Accept intents, resolve recipients, templates, channels, preferences, quiet hours, digests, delivery status. |
| `NumberSeriesService` (`INumberSeries`) | Aggregate (`NumberSeries`) | Gapless allocation per tenant/series/scope, formats, preview. |
| `ImportService` (`IImportService`) | Aggregate (`ImportBatch`) | Upload, parse, map, preview, commit, report; pluggable `IImportType` from modules. |
| `SearchIndexer` (`ISearchIndexer`) / `SearchQueryService` | Infra | Maintain `search_index` from events, permission-trimmed querying, Arabic normalisation. |
| `CommentService` (`ICommentService`) | CRUD + rules | Comments, mentions, watchers, visibility (internal/external). |
| `TaskInbox` (`ITaskInbox`) | CRUD + rules | Tasks for users, auto-created from approvals and module intents, completion, reminders. |
| `JobScheduler` / `JobRunRecorder` | Infra | Register jobs, per-tenant enablement, run records, locks. |
| `IdempotencyMiddleware` | Infra | Store and replay write responses (PLT-E13). |
| `ConcurrencyFilter` | Infra | ETag and `If-Match` handling. |
| `LocalizationService` | Infra + CRUD | Message catalogue, tenant overrides, Hijri conversion, numerals helper, working calendar and holidays. |
| `FeatureGate` (`IFeatureGate`) | Infra | `IsEnabled(feature)`, `GetLimit(limit)`, usage counters. |
| `SavedViewService` | CRUD | `user_views` for grids (columns, filters, sorts, sharing). |
| `TenantExportService` / `TenantDeletionService` | Aggregate (`TenantExport`, `TenantDeletion`) | Full export on request, scheduled and verified deletion. |
| `PlatformSeeder` | Infra | Idempotent seeding of catalogue, roles, templates, jobs, message catalogue. |
| Integration test fixtures | Tech | `Construction.Testing` tenant builders, two-tenant isolation suite, golden-project fixture. |

---

## 4. Domain model

Conventions: all tables are in schema `platform`, carry the **standard columns of 00 §3.2** (`id, tenant_id, created_at/by, updated_at/by, row_version, is_deleted, source`) unless the table says "Standard: no" or lists an exception. They are omitted from the column tables. All tables have RLS policy `tenant_id = current_setting('app.tenant_id')::uuid` unless stated. FKs inside `platform` are real constraints. FKs to `core.projects` and to `platform.users` are real (00 §5.4). Business numbers use `platform.number_series`.

### 4.1 Tenancy (PLT-E1)

#### `platform.tenants` (Standard: no `tenant_id` column; `id` is the tenant id; no RLS, readable only by the tenant itself through a view)

| column | type | null | notes / constraints |
|---|---|---|---|
| id | uuid | no | PK; this is `tid` in tokens |
| code | text | no | unique, lowercase `[a-z0-9-]{3,40}`, used in portal URLs and number-series seeds |
| name\_ar | text | yes | at least one of `name_ar`/`name_en` (check) |
| name\_en | text | yes |  |
| status | text | no | CHECK in (`provisioning`,`active`,`trial`,`suspended`,`pending_deletion`,`deleted`) |
| plan\_code | text | no | CHECK in (`starter`,`growth`,`enterprise`); FK-by-value to `plan_definitions.code` |
| base\_currency | char(3) | no | default `EGP` |
| time\_zone | text | no | default `Africa/Cairo` |
| default\_locale | text | no | `ar` or `en`, default `ar` |
| country\_code | char(2) | no | default `EG` |
| commercial\_reg\_no | text | yes | السجل التجاري |
| tax\_registration\_no | text | yes | البطاقة الضريبية |
| address | jsonb | yes | street, city, governorate, postal code |
| logo\_file\_id | uuid | yes | -> `files` |
| trial\_ends\_at | timestamptz | yes |  |
| suspended\_at | timestamptz | yes |  |
| suspension\_reason | text | yes |  |
| deletion\_requested\_at | timestamptz | yes |  |
| permissions\_version | bigint | no | default 1; bumped on any role/grant change; part of the authorisation cache key |
| Keys: PK(id), unique(code). Check: `status='suspended' => suspended_at not null`. Indexes: (status), (plan\_code). |  |  |  |

#### `platform.tenant_settings`

Key-value tenant configuration with typed registry (the registry is code; unknown keys rejected).

| column | type | null | notes |
|---|---|---|---|
| key | text | no | e.g. `security.mfa_required_roles`, `approvals.allow_self_approval`, `approvals.reminder_hours`, `locale.numerals`, `locale.calendar`, `notifications.quiet_hours`, `files.max_upload_mb` |
| value | jsonb | no | validated against the key's schema |
| description | text | yes |  |
| Keys: unique(tenant\_id, key). |  |  |  |

Registry of keys (seeded defaults):

| key | default | meaning |
|---|---|---|
| `security.mfa_required_roles` | `["finance_controller","owner_executive"]` | roles forced to enrol TOTP \[decide, 00 §3.8\] |
| `security.password_min_length` | 10 |  |
| `security.session_idle_minutes` | 60 (web), mobile 43200 (30 d) |  |
| `security.ip_allowlist` | `[]` | Enterprise only |
| `approvals.allow_self_approval` | false | BR-X-07 / BR-PLT-47 |
| `approvals.reminder_hours` | 24 | reminder cadence for pending steps |
| `approvals.escalation_default_hours` | 48 | used when a policy leaves it null |
| `approvals.delegation_max_days` | 30 |  |
| `locale.numerals` | `latin` | `latin` or `arabic_indic` default for new users |
| `locale.calendar` | `gregorian` | `gregorian` or `hijri` display default |
| `locale.week_start` | `sat` |  |
| `notifications.quiet_hours` | `{"from":"21:00","to":"06:30"}` |  |
| `files.max_upload_mb` | 25 | up to 100 for drawings (00 §3.11) |
| `export.require_dual_approval` | true | tenant export needs two admins |

#### `platform.tenant_subscriptions` (plan history)

| column | type | null | notes |
|---|---|---|---|
| plan\_code | text | no |  |
| starts\_on | date | no |  |
| ends\_on | date | yes | null = current |
| billing\_period | text | no | CHECK in (`annual`,`monthly`,`trial`) |
| price\_amount | numeric(18,4) | yes | contract price, EGP |
| currency | char(3) | no |  |
| limits\_override | jsonb | yes | Enterprise custom: `{ "max_active_projects": 25, "max_users": 120 }` |
| features\_override | jsonb | yes | list of feature codes added/removed |
| note | text | yes |  |
| Keys: exclusion constraint no overlapping ranges per tenant. Indexes: (tenant\_id, starts\_on desc). |  |  |  |

### 4.2 Identity (PLT-E1)

#### `platform.users`

| column | type | null | notes |
|---|---|---|---|
| email | citext | no | login name; unique across all tenants in v1 (a person who works for two companies uses two emails; 00 §13 Q1) |
| phone | text | yes | E.164, Egyptian mobile normalised (`+2010...`) |
| full\_name\_ar | text | yes | one of the names required |
| full\_name\_en | text | yes |  |
| job\_title | text | yes |  |
| status | text | no | CHECK in (`invited`,`active`,`deactivated`,`locked`) |
| password\_hash | text | yes | Argon2id; null until invitation accepted |
| password\_changed\_at | timestamptz | yes |  |
| failed\_login\_count | int | no | default 0 |
| locked\_until | timestamptz | yes |  |
| last\_login\_at | timestamptz | yes |  |
| mfa\_enabled | bool | no | default false |
| mfa\_secret\_enc | bytea | yes | AES-GCM with key from secret store |
| mfa\_recovery\_hashes | jsonb | yes | array of hashed one-time codes |
| locale | text | no | `ar` or `en` |
| numerals | text | no | CHECK in (`latin`,`arabic_indic`) |
| calendar\_pref | text | no | CHECK in (`gregorian`,`hijri`) |
| time\_zone | text | yes | overrides tenant |
| avatar\_file\_id | uuid | yes |  |
| signature\_file\_id | uuid | yes | image used on printed approvals (PO, certificate) |
| manager\_user\_id | uuid | yes | FK users; used by approval step type `requester_manager` |
| deactivated\_at / deactivated\_by | timestamptz / uuid | yes |  |
| deactivation\_reason | text | yes |  |
| Keys: unique(email), unique(tenant\_id, phone) where phone not null. Indexes: (tenant\_id, status), (tenant\_id, manager\_user\_id), GIN trigram on `full_name_ar`, `full_name_en` for pickers. Check: `status='active' => password_hash is not null`. |  |  |  |

#### `platform.user_invitations`

| column | type | null | notes |
|---|---|---|---|
| user\_id | uuid | no | FK users |
| token\_hash | text | no | SHA-256 of random 32-byte token |
| channel | text | no | `email` or `whatsapp` (channel to confirm) |
| expires\_at | timestamptz | no | default created\_at + 7 days |
| accepted\_at | timestamptz | yes |  |
| revoked\_at | timestamptz | yes |  |
| sent\_count | int | no | default 1 |
| Keys: unique(token\_hash). Index: (user\_id) where accepted\_at is null and revoked\_at is null. |  |  |  |

#### `platform.user_tokens` (password reset, email change, verification)

| column | type | null | notes |
|---|---|---|---|
| user\_id | uuid | no |  |
| purpose | text | no | CHECK in (`password_reset`,`email_verify`,`phone_verify`) |
| token\_hash | text | no |  |
| expires\_at | timestamptz | no | 1 hour for reset |
| used\_at | timestamptz | yes |  |
| Keys: unique(token\_hash). |  |  |  |

#### `platform.refresh_tokens` (Standard: no `updated_*`, no soft delete)

| column | type | null | notes |
|---|---|---|---|
| user\_id | uuid | no |  |
| family\_id | uuid | no | rotation family; reuse of a rotated token revokes the whole family |
| token\_hash | text | no |  |
| client\_kind | text | no | CHECK in (`web`,`mobile`,`api`) |
| device\_label | text | yes | e.g. "Samsung A14" |
| ip | inet | yes |  |
| user\_agent | text | yes |  |
| issued\_at / expires\_at | timestamptz | no | 30 days |
| rotated\_at | timestamptz | yes | set when exchanged |
| revoked\_at | timestamptz | yes |  |
| revoke\_reason | text | yes | `logout`,`reuse_detected`,`admin`,`password_changed`,`deactivated` |
| Keys: unique(token\_hash). Indexes: (user\_id, revoked\_at), (family\_id). |  |  |  |

### 4.3 Roles, permissions, membership, visibility (PLT-E2)

#### `platform.permissions` — the master permission catalogue (global, no `tenant_id`, no RLS, read-only to tenants; seeded by migration; section 10 lists every code)

| column | type | null | notes |
|---|---|---|---|
| code | text | no | PK; format `module.resource.action`, regex `^[a-z_]+\.[a-z_]+\.[a-z_]+$` |
| module | text | no | one of the schemas in 00 §5 plus `field` and `portal` |
| resource | text | no |  |
| action | text | no | CHECK in (`view`,`create`,`update`,`delete`,`approve`,`export`,`manage`,`submit`,`verify`,`post`,`close`,`reopen`,`import`,`waive`,`record`,`finalize`,`confirm`,`issue`,`view_amounts`,`view_own`,`configure`,`submit_late`) plus module-specific verbs listed in the catalogue; the list is not closed, the regex is |
| name\_ar / name\_en | text | no | shown in the role editor |
| description\_en | text | yes |  |
| is\_amount\_limited | bool | no | approval limit applies (`approve`, `issue`, `release`) |
| is\_project\_scoped | bool | no | restricted by project membership |
| is\_sensitive | bool | no | changes are audited with `reason` and alert `tenant_admin` (e.g. `cost.period.reopen`) |
| min\_plan | text | no | default `starter`; some codes need a higher plan (e.g. `platform.approval_policy.manage_custom` needs `growth`) |
| release | text | no | `R1`,`R2`,`R3` |
| deprecated\_at | timestamptz | yes |  |

#### `platform.roles`

| column | type | null | notes |
|---|---|---|---|
| code | text | no | system roles: the 13 internal roles and 3 external roles of 00 §4.4; custom roles: `custom.<slug>` |
| name\_ar / name\_en | text | no |  |
| kind | text | no | CHECK in (`system`,`custom`) |
| audience | text | no | CHECK in (`internal`,`external`) |
| description | text | yes |  |
| is\_assignable | bool | no | default true; false hides `tenant_admin` bootstrap role from non-admins |
| Keys: unique(tenant\_id, code). System roles are seeded **per tenant** (copy of the template with `kind='system'`, immutable by tenants; a tenant may clone one into a custom role). Check: system roles' `audience` matches the 00 list. |  |  |  |

#### `platform.role_permissions`

| column | type | null | notes |
|---|---|---|---|
| role\_id | uuid | no | FK roles |
| permission\_code | text | no | FK permissions.code |
| field\_scope | text | no | default `all`; CHECK in (`all`,`own`) where `own` = only records created by the user or assigned to them (e.g. procurement "own buys") |
| Keys: unique(role\_id, permission\_code). Index: (tenant\_id, permission\_code). |  |  |  |

#### `platform.role_limits` (default approval limit per role)

| column | type | null | notes |
|---|---|---|---|
| role\_id | uuid | no |  |
| record\_type | text | no | approval record type, e.g. `procurement.purchase_order` |
| max\_amount | numeric(18,4) | no | `>= 0`; null max means unlimited is expressed by omitting the row only when `is_unlimited` is true |
| is\_unlimited | bool | no | default false |
| currency | char(3) | no | tenant base currency |
| Keys: unique(role\_id, record\_type). |  |  |  |

#### `platform.user_roles`

| column | type | null | notes |
|---|---|---|---|
| user\_id | uuid | no |  |
| role\_id | uuid | no |  |
| valid\_from | date | yes | optional acting period |
| valid\_to | date | yes | check `valid_to >= valid_from` |
| Keys: unique(user\_id, role\_id). Index: (tenant\_id, role\_id). |  |  |  |

#### `platform.project_members`

| column | type | null | notes |
|---|---|---|---|
| project\_id | uuid | no | FK `core.projects` (allowed, 00 §5.4) |
| user\_id | uuid | no | FK users |
| role\_codes | text\[\] | no | subset of the user's tenant roles that apply on this project (empty array = all the user's roles) |
| limits | jsonb | no | default `{}`; `{ "procurement.purchase_order": {"maxAmount": 250000, "currency":"EGP"} }`; overrides `role_limits` |
| can\_see\_costs | bool | yes | per-project override of default hidden fields for this member (null = follow policy) |
| added\_by / added\_at | uuid / timestamptz | no |  |
| ended\_on | date | yes | membership end; after that date the user loses project scope |
| Keys: unique(project\_id, user\_id). Indexes: (tenant\_id, user\_id) where ended\_on is null (visible-projects lookup, the hottest query in the system), (project\_id). |  |  |  |
| Special: users with `platform.project.view_all` (default `owner_executive`, `tenant_admin` read-only) bypass membership for **view**. |  |  |  |

#### `platform.field_groups` (global catalogue, seeded)

| column | type | null | notes |
|---|---|---|---|
| code | text | no | PK: `cost_amounts`, `unit_rates`, `margins`, `supplier_prices`, `contract_values`, `budget_amounts`, `payroll_rates`, `bid_build_up` |
| name\_ar / name\_en | text | no |  |
| visibility\_permission | text | no | FK permissions: `field.cost_amounts.view`, ... |
| field\_paths | jsonb | no | list of `{ "dto": "PurchaseOrderLineDto", "path": "unitPrice" }`; the DTO registry is verified by an architecture test |
| default\_hidden\_roles | text\[\] | no | `{foreman,superintendent}` for all groups except where noted |

#### `platform.field_visibility_policies` (tenant and per-project overrides)

| column | type | null | notes |
|---|---|---|---|
| field\_group\_code | text | no | FK field\_groups |
| project\_id | uuid | yes | null = whole tenant |
| role\_code | text | no |  |
| effect | text | no | CHECK in (`show`,`hide`,`show_own`) |
| reason | text | yes | required when effect is `show` for a default-hidden role |
| Keys: unique(tenant\_id, field\_group\_code, coalesce(project\_id, zero-uuid), role\_code). Evaluation order is in BR-PLT-17. |  |  |  |

### 4.4 Approval engine (PLT-E3)

Concept: a **policy** says "for record type X, project P, trade T, amount range A..B, use this chain of steps". When a module asks to approve a record, the engine matches one policy, **copies** it into an **instance** with concrete **steps** (so later policy edits never alter running approvals, BR-PLT-41), then collects **decisions**. Delegation, reminders, escalation and segregation of duties are rules of the engine.

#### `platform.approval_policies`

| column | type | null | notes |
|---|---|---|---|
| code | text | no | unique per tenant, e.g. `PO-STD-100K` |
| name\_ar / name\_en | text | no |  |
| record\_type | text | no | registered record type (see enumeration 4.19 and the registry table `approval_record_types`) |
| project\_id | uuid | yes | null = all projects |
| trade\_code | text | yes | work-package trade code; null = all |
| min\_amount | numeric(18,4) | no | default 0; inclusive |
| max\_amount | numeric(18,4) | yes | exclusive; null = no upper bound |
| currency | char(3) | no | amounts are compared in the policy currency after conversion at the document rate (BR-PLT-46) |
| conditions | jsonb | yes | extra predicates on the record context: `{ "flags": ["over_budget"], "supplierBlocked": false }`; evaluated by whitelisted operators (eq, in, gt, lt, has\_flag) |
| priority | int | no | default 100; lower wins among equally specific policies |
| step\_deadline\_hours | int | yes | SLA per step; null falls back to `approvals.escalation_default_hours` |
| reminder\_hours | int | yes |  |
| auto\_approve\_below | numeric(18,4) | yes | if set and amount below, the engine returns `approved` immediately with reason `auto_below_threshold` (R1: only for `planning.site_request`) |
| is\_active | bool | no | default true |
| version | int | no | default 1; incremented on edit of an active policy (a new row is not created: instances snapshot the policy) |
| effective\_from | date | no |  |
| effective\_to | date | yes |  |
| is\_system\_template | bool | no | seeded standard template (Starter cannot edit, can only choose among templates) |
| Keys: unique(tenant\_id, code). Check: `max_amount is null or max_amount > min_amount`. Index: (tenant\_id, record\_type, is\_active, project\_id, trade\_code). |  |  |  |

#### `platform.approval_policy_steps`

| column | type | null | notes |
|---|---|---|---|
| policy\_id | uuid | no | FK approval\_policies on delete cascade |
| step\_no | int | no | 1..n, consecutive |
| name\_ar / name\_en | text | no | e.g. "Project manager" |
| approver\_type | text | no | CHECK in (`role`,`user`,`project_role`,`requester_manager`,`record_field`) |
| approver\_ref | text | no | role code / user id / project role code / record field name such as `buyerUserId` |
| mode | text | no | CHECK in (`any`,`all`,`n_of`) |
| required\_count | int | no | default 1; for `all` it is set at instance time to the number of resolved approvers |
| respect\_limit | bool | no | default true: approver must have an approval limit >= the amount (BR-PLT-45) |
| allow\_delegation | bool | no | default true |
| can\_skip\_if\_requester | bool | no | default false: skip when the requester already is the approver and SoD allows (BR-PLT-47) |
| sla\_hours | int | yes | overrides policy |
| Keys: unique(policy\_id, step\_no). Steps run sequentially; parallel approvers are expressed with `mode='all'` or `n_of`. |  |  |  |

#### `platform.approval_record_types` (registry, global seed)

| column | type | null | notes |
|---|---|---|---|
| record\_type | text | no | PK, `<module>.<aggregate>` |
| owner\_module | text | no |  |
| name\_ar / name\_en | text | no |  |
| amount\_field | text | yes | name in context of the amount |
| requires\_reason\_on\_reject | bool | no | default true |
| allows\_self\_approval\_flag | bool | no | whether the tenant setting may ever allow it (false for payments) |
| supports\_auto\_approve | bool | no |  |
| deep\_link\_template | text | no | e.g. `/projects/{projectId}/purchase-orders/{recordId}` |
| release | text | no |  |

#### `platform.approval_instances`

| column | type | null | notes |
|---|---|---|---|
| record\_type | text | no | FK registry |
| record\_id | uuid | no |  |
| record\_number | text | yes | `PO-1066` for display |
| record\_version | int | yes | the version approved (BR-X-03); a new version needs a new instance |
| project\_id | uuid | yes |  |
| trade\_code | text | yes |  |
| amount | numeric(18,4) | yes | snapshot at request time |
| currency | char(3) | yes |  |
| context | jsonb | no | snapshot facts used for conditions and display (title, counterparty, flags) |
| policy\_id | uuid | yes | FK, null when auto-approved without policy |
| policy\_snapshot | jsonb | no | copy of policy and steps at request time |
| status | text | no | CHECK in (`pending`,`approved`,`rejected`,`cancelled`) |
| current\_step\_no | int | yes |  |
| requested\_by | uuid | no | the requester (user id) |
| requested\_at | timestamptz | no |  |
| completed\_at | timestamptz | yes |  |
| completed\_reason | text | yes | `all_steps_approved`,`rejected`,`cancelled_by_requester`,`cancelled_by_owner_module`,`auto_below_threshold`,`superseded` |
| self\_approved | bool | no | default false; true only when the tenant allowed it and it was flagged |
| correlation\_id | uuid | no |  |
| Keys: unique(tenant\_id, record\_type, record\_id) where status = 'pending' (one open instance per record). Indexes: (tenant\_id, status, project\_id), (tenant\_id, record\_type, record\_id, requested\_at desc), (tenant\_id, requested\_by). |  |  |  |

#### `platform.approval_steps`

| column | type | null | notes |
|---|---|---|---|
| instance\_id | uuid | no | FK cascade |
| step\_no | int | no |  |
| name\_ar / name\_en | text | no |  |
| mode | text | no | `any`,`all`,`n_of` |
| required\_count | int | no |  |
| approved\_count | int | no | default 0 |
| status | text | no | CHECK in (`waiting`,`active`,`approved`,`rejected`,`skipped`,`escalated`,`cancelled`) |
| resolved\_approver\_ids | uuid\[\] | no | users resolved at activation time (role members with project scope and enough limit) |
| respect\_limit | bool | no |  |
| due\_at | timestamptz | yes | activation time + SLA |
| activated\_at / completed\_at | timestamptz | yes |  |
| last\_reminder\_at | timestamptz | yes |  |
| escalation\_level | int | no | default 0 |
| Keys: unique(instance\_id, step\_no). Indexes: (tenant\_id, status, due\_at) where status = 'active' (job scan), GIN(resolved\_approver\_ids) for "my approvals" queries. |  |  |  |

#### `platform.approval_decisions` (append-only)

| column | type | null | notes |
|---|---|---|---|
| step\_id | uuid | no |  |
| instance\_id | uuid | no |  |
| decided\_by | uuid | no | the actual user |
| on\_behalf\_of | uuid | yes | the delegator when acting by delegation |
| delegation\_id | uuid | yes |  |
| decision | text | no | CHECK in (`approve`,`reject`,`return`,`comment`) |
| comment | text | yes | required for reject and return |
| amount\_limit\_at\_decision | numeric(18,4) | yes | the approver's effective limit when deciding (evidence) |
| decided\_at | timestamptz | no |  |
| client | text | no | `web`,`mobile`,`portal`,`api` |
| ip | inet | yes |  |
| Index: (instance\_id, decided\_at). `return` sends the record back to its owner module for edit (instance status becomes `rejected` with `completed_reason='returned'`; the owner module creates a new request after edit). |  |  |  |

#### `platform.approval_delegations`

| column | type | null | notes |
|---|---|---|---|
| delegator\_id | uuid | no |  |
| delegate\_id | uuid | no | `<> delegator_id` |
| valid\_from / valid\_to | date | no | `valid_to >= valid_from`, span \<= `approvals.delegation_max_days` |
| record\_types | text\[\] | yes | null = all |
| project\_id | uuid | yes | null = all |
| max\_amount | numeric(18,4) | yes | cap on delegated authority; the delegate acting by delegation has authority = min(delegator's limit, `max_amount`) (BR-PLT-49) |
| reason | text | yes | e.g. "leave" |
| status | text | no | CHECK in (`active`,`revoked`,`expired`) |
| revoked\_at | timestamptz | yes |  |
| Indexes: (tenant\_id, delegator\_id, status), (tenant\_id, delegate\_id, status). Exclusion: no two active overlapping delegations with identical (delegator, delegate, scope) (check in service). |  |  |  |

#### `platform.approval_escalations` (append-only)

| column | type | null | notes |
|---|---|---|---|
| instance\_id / step\_id | uuid | no |  |
| level | int | no | 1, 2, ... |
| escalated\_from | uuid\[\] | no | approvers who did not respond |
| escalated\_to | uuid\[\] | no | resolved from the escalation target |
| reason | text | no | `sla_breached` or `approver_deactivated` or `no_eligible_approver` |
| at | timestamptz | no |  |

#### `platform.approval_escalation_rules` (per tenant, optional per record type)

| column | type | null | notes |
|---|---|---|---|
| record\_type | text | yes | null = all |
| after\_hours | int | no | after step due\_at |
| target\_type | text | no | CHECK in (`role`,`user`,`requester_manager`,`next_approver_manager`) |
| target\_ref | text | yes | role code or user id |
| notify\_only | bool | no | default false: true = remind without adding approvers |
| max\_levels | int | no | default 2 |

### 4.5 Outbox, dead-letter, processed events (PLT-E4)

Per 00 §3.9 events are written to `outbox` in the business transaction. To retry each consumer independently, delivery state is kept per consumer in `outbox_deliveries`.

#### `platform.outbox` (append-only; Standard: only `id`, `tenant_id`, `created_at`)

| column | type | null | notes |
|---|---|---|---|
| event\_id | uuid | no | unique; v7; same as envelope `eventId` |
| event\_type | text | no | `procurement.goods_receipt.posted.v1` |
| aggregate\_type | text | no |  |
| aggregate\_id | uuid | no | partition key: ordering per aggregate |
| aggregate\_seq | bigint | no | per-aggregate sequence assigned at insert (ordering guard) |
| project\_id | uuid | yes |  |
| actor\_id | uuid | yes |  |
| correlation\_id | uuid | no |  |
| causation\_id | uuid | yes |  |
| occurred\_at | timestamptz | no |  |
| payload | jsonb | no | validated against the event schema |
| schema\_version | int | no | `N` of `.vN` |
| dispatch\_status | text | no | CHECK in (`pending`,`fanned_out`,`completed`,`dead`) |
| fanned\_out\_at | timestamptz | yes | when deliveries rows were created |
| Keys: unique(event\_id), unique(tenant\_id, aggregate\_id, aggregate\_seq). Indexes: (dispatch\_status, id) where dispatch\_status = 'pending' (dispatcher scan), (tenant\_id, event\_type, occurred\_at desc) for admin search. |  |  |  |

#### `platform.outbox_subscriptions` (global seed; rebuilt from code at startup)

| column | type | null | notes |
|---|---|---|---|
| consumer | text | no | `<module>.<handler>` e.g. `cost.on_goods_receipt_posted` |
| event\_type\_pattern | text | no | exact or `procurement.goods_receipt.*` |
| ordered | bool | no | default true: per-aggregate ordering required |
| max\_attempts | int | no | default 5 |
| is\_enabled | bool | no | operator can pause a consumer |
| Keys: unique(consumer, event\_type\_pattern). No `tenant_id` (system table). |  |  |  |

#### `platform.outbox_deliveries`

| column | type | null | notes |
|---|---|---|---|
| outbox\_id | uuid | no | FK outbox |
| consumer | text | no |  |
| status | text | no | CHECK in (`pending`,`in_flight`,`done`,`failed`,`dead`) |
| attempts | int | no | default 0 |
| next\_attempt\_at | timestamptz | no |  |
| locked\_by / locked\_until | text / timestamptz | yes | worker lease (`FOR UPDATE SKIP LOCKED`) |
| last\_error | text | yes | truncated 4 KB |
| done\_at | timestamptz | yes |  |
| Keys: unique(outbox\_id, consumer). Index: (status, next\_attempt\_at) where status in ('pending','failed'). |  |  |  |

#### `platform.outbox_dead` (dead-letter)

| column | type | null | notes |
| --- | --- | --- | --- |
| outbox\_id | uuid | no |  |
| consumer | text | no |  |
| event\_type | text | no |  |
| payload | jsonb | no | copy |
| attempts | int | no |  |
| first\_failed\_at / last\_failed\_at | timestamptz | no |  |
| error\_class | text | no | exception type |
| error\_message | text | no |  |
| stack\_trace | text | yes |  |
| resolution | text | no | CHECK in (`open`,`replayed`,`discarded`) default `open` |
| resolved\_by / resolved\_at | uuid / timestamptz | yes |  |
| resolution\_note | text | yes | required when discarded |
| Index: (tenant\_id, resolution, last\_failed\_at desc). |  |  |  |

#### `platform.processed_events`

| column | type | null | notes |
|---|---|---|---|
| consumer | text | no |  |
| event\_id | uuid | no |  |
| processed\_at | timestamptz | no |  |
| Keys: PK(consumer, event\_id) (no `id`, no standard columns except `tenant_id`). Written in the **same transaction** as the consumer's side effects (BR-PLT-62). Retention: purged after 30 days by `purge_processed_events` (events older than the outbox retention cannot be redelivered). |  |  |  |

#### `platform.event_types` (global registry, seeded from `.Contracts/events/*.schema.json`)

| column | type | null | notes |
|---|---|---|---|
| type | text | no | PK `procurement.goods_receipt.posted.v1` |
| publisher\_module | text | no |  |
| json\_schema | jsonb | no |  |
| deprecated\_at | timestamptz | yes |  |
| replaced\_by | text | yes |  |

### 4.6 Audit log and status history (PLT-E5)

#### `platform.audit_log` (append-only; no `updated_*`, no soft delete; RLS read policy plus **no UPDATE/DELETE grant** to the application role)

| column | type | null | notes |
|---|---|---|---|
| id | uuid | no | v7 |
| occurred\_at | timestamptz | no |  |
| actor\_type | text | no | CHECK in (`user`,`external_user`,`operator`,`system`,`ai`) |
| actor\_id | uuid | yes |  |
| actor\_name | text | yes | denormalised at write time (survives user deactivation) |
| action | text | no | `create`,`update`,`delete`,`approve`,`reject`,`status_change`,`login`,`login_failed`,`logout`,`export`,`permission_change`,`role_change`,`import_commit`,`file_download`,`impersonate`,`policy_change`,`tenant_export`,`custom` |
| entity\_type | text | no | `procurement.purchase_order` |
| entity\_id | uuid | yes |  |
| entity\_label | text | yes | e.g. `PO-1066` |
| project\_id | uuid | yes |  |
| before | jsonb | yes | changed fields only, old values; sensitive fields (password hash, secrets) are never captured |
| after | jsonb | yes | changed fields only, new values |
| changed\_fields | text\[\] | yes |  |
| reason | text | yes | mandatory for rows created through `IAuditWriter.Write(custom)` flagged `requires_reason` and for sensitive permissions |
| ip | inet | yes |  |
| user\_agent | text | yes |  |
| client | text | yes | `web`,`mobile`,`portal`,`api`,`job` |
| correlation\_id | uuid | yes |  |
| prev\_hash | bytea | yes | hash chain per tenant (BR-PLT-33) |
| row\_hash | bytea | no | SHA-256(prev\_hash |
| Indexes: (tenant\_id, occurred\_at desc, id desc) keyset; (tenant\_id, entity\_type, entity\_id, occurred\_at desc); (tenant\_id, actor\_id, occurred\_at desc); (tenant\_id, project\_id, occurred\_at desc); BRIN on occurred\_at. Monthly partitioning is planned, not v1 (00 §5.9). |  |  |  |

#### `platform.status_history` (append-only)

| column | type | null | notes |
| --- | --- | --- | --- |
| entity\_type | text | no |  |
| entity\_id | uuid | no |  |
| project\_id | uuid | yes |  |
| from\_status | text | yes | null for the creation row |
| to\_status | text | no |  |
| actor\_id | uuid | yes |  |
| actor\_type | text | no | as audit |
| reason | text | yes |  |
| at | timestamptz | no |  |
| correlation\_id | uuid | yes |  |
| Indexes: (tenant\_id, entity\_type, entity\_id, at), (tenant\_id, project\_id, at desc). |  |  |  |

### 4.7 Files (PLT-E6)

#### `platform.files`

| column | type | null | notes |
|---|---|---|---|
| storage\_key | text | no | unique; `t/{tenantId}/{yyyy}/{mm}/{fileId}`; never contains the user file name |
| original\_name | text | no | UTF-8 Arabic preserved |
| content\_type | text | no | verified by magic bytes, not the header alone |
| size\_bytes | bigint | no | `> 0` and `<= tenant limit` |
| sha256 | text | yes | filled on completion |
| status | text | no | CHECK in (`pending_upload`,`uploaded`,`scanning`,`available`,`quarantined`,`deleted`) |
| scan\_status | text | no | CHECK in (`pending`,`clean`,`infected`,`skipped`,`error`) |
| width / height | int | yes | images |
| exif\_taken\_at | timestamptz | yes | photo capture time (site evidence) |
| exif\_lat / exif\_lng | numeric(9,6) | yes |  |
| uploaded\_by | uuid | no | user or external user |
| uploader\_type | text | no | `user` or `external_user` |
| linked\_entity\_type | text | yes | primary link |
| linked\_entity\_id | uuid | yes |  |
| project\_id | uuid | yes | for permission trimming |
| category | text | no | `photo`,`delivery_note`,`invoice`,`drawing`,`contract`,`certificate`,`report`,`import_source`,`export`,`other` |
| visibility | text | no | CHECK in (`internal`,`shared_external`) default `internal` |
| retention\_until | date | yes | legal hold or retention rule |
| legal\_hold | bool | no | default false |
| deleted\_at | timestamptz | yes | logical delete; physical purge by job after 30 days unless legal hold |
| Indexes: (tenant\_id, linked\_entity\_type, linked\_entity\_id), (tenant\_id, project\_id, category, created\_at desc), (tenant\_id, sha256) for duplicate detection, (status) where status in ('pending\_upload','scanning'). |  |  |  |

#### `platform.file_links` (a file attached to more than one record)

| column | type | null | notes |
| --- | --- | --- | --- |
| file\_id | uuid | no |  |
| entity\_type / entity\_id | text / uuid | no |  |
| link\_role | text | no | `attachment`,`evidence`,`primary`,`photo` |
| caption | text | yes |  |
| sort\_no | int | no | default 0 |
| Keys: unique(file\_id, entity\_type, entity\_id). Index: (tenant\_id, entity\_type, entity\_id). |  |  |  |

#### `platform.file_variants`

| column | type | null | notes |
| --- | --- | --- | --- |
| file\_id | uuid | no |  |
| variant | text | no | `thumb_256`,`web_1280`,`pdf_preview` |
| storage\_key | text | no |  |
| size\_bytes | bigint | no |  |
| Keys: unique(file\_id, variant). |  |  |  |

#### `platform.upload_sessions`

| column | type | null | notes |
| --- | --- | --- | --- |
| file\_id | uuid | no | the pending file |
| presigned\_expires\_at | timestamptz | no | 15 minutes |
| client\_ref | uuid | yes | mobile-generated id for idempotent upload requests |
| expected\_size\_bytes | bigint | no |  |
| expected\_sha256 | text | yes |  |
| completed\_at | timestamptz | yes |  |
| Index: (tenant\_id, client\_ref) unique where not null. |  |  |  |

### 4.8 Notifications (PLT-E7)

#### `platform.notification_templates` (global defaults plus tenant overrides; `tenant_id` null = global)

| column | type | null | notes |
|---|---|---|---|
| code | text | no | `PLT.APPROVAL.REQUESTED` (templates are listed per module in section 11 of each file) |
| locale | text | no | `ar`,`en` |
| channel | text | no | CHECK in (`in_app`,`push`,`email`,`whatsapp`) |
| version | int | no |  |
| subject | text | yes | email only |
| body | text | no | placeholders `{{param}}` plus ICU plural support |
| param\_schema | jsonb | no | required params with types (validated on intent) |
| category | text | no | `approval`,`task`,`security`,`alert`,`reminder`,`digest`,`system` |
| is\_mandatory | bool | no | cannot be disabled by user preferences (security, approvals) |
| status | text | no | `draft`,`active`,`retired` |
| Keys: unique(tenant\_id nulls not distinct, code, locale, channel, version). |  |  |  |

#### `platform.notifications` (one row per recipient per intent)

| column | type | null | notes |
| --- | --- | --- | --- |
| recipient\_type | text | no | `user` or `external_user` |
| recipient\_id | uuid | no |  |
| template\_code | text | no |  |
| category | text | no |  |
| params | jsonb | no |  |
| link | text | yes | deep link (`/projects/{id}/purchase-orders/{id}`) |
| entity\_type / entity\_id | text / uuid | yes |  |
| project\_id | uuid | yes |  |
| priority | text | no | `normal`,`high`; high bypasses quiet hours |
| dedupe\_key | text | yes | collapses repeated alerts (`cost.alert:{id}`) |
| read\_at | timestamptz | yes |  |
| archived\_at | timestamptz | yes |  |
| digest\_group | text | yes | role digest bucket |
| intent\_id | uuid | no | the producer's intent (idempotent send) |
| Keys: unique(tenant\_id, intent\_id, recipient\_id). Indexes: (tenant\_id, recipient\_id, read\_at, created\_at desc) for inbox, (tenant\_id, recipient\_id, dedupe\_key). |  |  |  |

#### `platform.notification_deliveries`

| column | type | null | notes |
| --- | --- | --- | --- |
| notification\_id | uuid | no |  |
| channel | text | no | `in_app`,`push`,`email`,`whatsapp` |
| status | text | no | CHECK in (`queued`,`sent`,`delivered`,`failed`,`suppressed`,`expired`) |
| suppressed\_reason | text | yes | `quiet_hours`,`preference_off`,`no_token`,`no_consent`,`plan_not_allowed` |
| provider | text | yes | `fcm`,`apns`,`smtp`,`wa_aggregator` |
| provider\_message\_id | text | yes |  |
| attempts | int | no |  |
| next\_attempt\_at | timestamptz | yes |  |
| sent\_at / delivered\_at | timestamptz | yes |  |
| error | text | yes |  |
| Index: (status, next\_attempt\_at) where status in ('queued','failed'). |  |  |  |

#### `platform.notification_preferences`

| column | type | null | notes |
| --- | --- | --- | --- |
| user\_id | uuid | no |  |
| category | text | no |  |
| channel | text | no |  |
| is\_enabled | bool | no |  |
| digest\_mode | text | no | `immediate`,`daily`,`off` |
| quiet\_from / quiet\_to | time | yes | per-user override |
| Keys: unique(user\_id, category, channel). |  |  |  |

Device push tokens live in file 15 (`platform.device_registrations`, `platform.push_tokens`).

### 4.9 Number series (PLT-E8)

#### `platform.number_series`

| column | type | null | notes |
| --- | --- | --- | --- |
| code | text | no | `PO`, `PR`, `VO`, `PA`, `RFQ`, `GRN`, `INV`, `NCR`, `RFI`, `TN`, `EST`, `SUB`, `VAL`, `TASK`, `IMP`; unique per tenant |
| name\_ar / name\_en | text | no |  |
| prefix | text | no | default = code + `-` |
| padding | int | no | 1..10, default 4 (`PO-1066`: the example numbers in 00 use 4 digits for PO/PR and 2 for VO/PA) |
| scope | text | no | CHECK in (`tenant`,`project`,`year`,`project_year`) |
| format | text | no | pattern with tokens `{prefix}{yyyy}{project}{seq}`, default `{prefix}{seq}` |
| start\_value | bigint | no | default 1 |
| step | int | no | default 1 |
| is\_locked | bool | no | true after first allocation: prefix/scope/format may not change (BR-PLT-22) |
| Keys: unique(tenant\_id, code). |  |  |  |

#### `platform.number_series_counters`

| column | type | null | notes |
| --- | --- | --- | --- |
| series\_id | uuid | no |  |
| scope\_key | text | no | `-` for tenant scope, project id for project scope, `2026` for year |
| next\_value | bigint | no | the value the next allocation returns |
| Keys: unique(series\_id, scope\_key). Allocation: `SELECT ... FOR UPDATE` on this row inside the caller's transaction, so a rolled-back transaction does not consume a number (gapless within committed work). |  |  |  |

#### `platform.number_allocations` (append-only audit of issued numbers)

| column | type | null | notes |
| --- | --- | --- | --- |
| series\_id | uuid | no |  |
| scope\_key | text | no |  |
| seq\_value | bigint | no |  |
| number | text | no | formatted, unique per (tenant, series) |
| entity\_type / entity\_id | text / uuid | yes | set by the owning module in the same transaction |
| allocated\_at | timestamptz | no |  |
| Keys: unique(tenant\_id, series\_id, number). |  |  |  |

### 4.10 Import framework (PLT-E9)

#### `platform.import_types` (global seed, registered in code by modules)

| column | type | null | notes |
|---|---|---|---|
| code | text | no | PK `core.cost_codes`, `contracts.boq_items`, `core.parties`, `core.materials`, `planning.schedule_activities`, `estimating.tender_boq`, `cost.budget_lines` |
| owner\_module | text | no |  |
| name\_ar / name\_en | text | no |  |
| permission\_code | text | no | required to run it (`<module>.<resource>.import`) |
| columns | jsonb | no | column definitions: `key`, `label_ar`, `label_en`, `type`, `required`, `aliases[]`, `example` |
| supports\_skip\_errors | bool | no | per 00 §3.14 |
| max\_rows | int | no | default 20000 |
| template\_file\_id | uuid | yes | downloadable Excel template |

#### `platform.import_batches`

| column | type | null | notes |
|---|---|---|---|
| number | text | no | `IMP-0007` from series `IMP` |
| import\_type | text | no |  |
| project\_id | uuid | yes |  |
| source\_file\_id | uuid | no | the uploaded file |
| source\_label | text | yes | e.g. "BOQ rev 3 from client" (record of source) |
| status | text | no | CHECK in (`uploaded`,`parsing`,`preview_ready`,`committing`,`committed`,`failed`,`cancelled`) |
| mode | text | no | `all_or_nothing` or `skip_errors` |
| mapping | jsonb | yes | detected column mapping, editable before commit |
| options | jsonb | yes | type-specific (e.g. `{ "boqId": "..." }`) |
| rows\_total / rows\_ok / rows\_error / rows\_warning / rows\_committed | int | no | default 0 |
| verification\_status | text | no | CHECK in (`unverified`,`verified`,`rejected`); the importing user (or a second user) marks the batch verified after checking; v3 requires "record of source and verification status" |
| verified\_by / verified\_at | uuid / timestamptz | yes |  |
| started\_at / committed\_at | timestamptz | yes |  |
| error\_report\_file\_id | uuid | yes | downloadable Arabic error list |
| committed\_by | uuid | yes |  |
| Indexes: (tenant\_id, import\_type, created\_at desc), (tenant\_id, status). |  |  |  |

#### `platform.import_rows`

| column | type | null | notes |
| --- | --- | --- | --- |
| batch\_id | uuid | no |  |
| row\_no | int | no | Excel row number |
| raw | jsonb | no | cell values as read |
| parsed | jsonb | yes | typed values after mapping |
| status | text | no | CHECK in (`ok`,`warning`,`error`,`skipped`,`committed`) |
| entity\_id | uuid | yes | created/updated record id after commit |
| Keys: unique(batch\_id, row\_no). Retention: rows purged 90 days after commit (the source file remains). |  |  |  |

#### `platform.import_row_errors`

| column | type | null | notes |
| --- | --- | --- | --- |
| batch\_id / row\_no | uuid / int | no |  |
| column\_key | text | yes |  |
| severity | text | no | `error` or `warning` |
| code | text | no | stable code, e.g. `import.value_required`, `import.duplicate_code`, `import.unknown_unit`; catalogue in section 12 |
| params | jsonb | yes |  |
| message\_ar / message\_en | text | no | rendered at preview time |
| Index: (batch\_id, row\_no). |  |  |  |

### 4.11 Search (PLT-E10)

#### `platform.search_index`

| column | type | null | notes |
| --- | --- | --- | --- |
| entity\_type | text | no |  |
| entity\_id | uuid | no |  |
| project\_id | uuid | yes | trimming key |
| number | text | yes | business number (exact and prefix match, boosted) |
| title | text | no | display title |
| subtitle | text | yes | e.g. supplier name, status label code |
| body\_normalized | text | no | Arabic-normalised concatenated searchable text (00 §3.5) |
| tsv | tsvector | no | `simple` config over title, number, body |
| trigram\_text | text | no | normalised title + number for `pg_trgm` |
| required\_permission | text | no | permission needed to see this hit (e.g. `procurement.purchase_order.view`) |
| status\_code | text | yes |  |
| updated\_event\_at | timestamptz | no | as\_of |
| rank\_boost | real | no | default 1.0 |
| Keys: unique(tenant\_id, entity\_type, entity\_id). Indexes: GIN(tsv), GIN(trigram\_text gin\_trgm\_ops), (tenant\_id, project\_id, entity\_type), (tenant\_id, number text\_pattern\_ops). |  |  |  |
| Fields that are cost-hidden (price, amounts) are **never** indexed in `body_normalized` for entity types that carry hidden field groups (BR-PLT-95). |  |  |  |

#### `platform.search_synonyms`

| column | type | null | notes |
| --- | --- | --- | --- |
| term | text | no | normalised term, e.g. `أسمنت` |
| synonyms | text\[\] | no | e.g. `{اسمنت,cement,بورتلاند}` |
| scope | text | no | `global` (seed) or `tenant` |
| Keys: unique(tenant\_id nulls not distinct, term). |  |  |  |

#### `platform.search_history`

| column | type | null | notes |
| --- | --- | --- | --- |
| user\_id | uuid | no |  |
| query | text | no |  |
| last\_used\_at | timestamptz | no |  |
| uses | int | no |  |
| Keys: unique(user\_id, query). Retained 90 days, last 20 per user. |  |  |  |

### 4.12 Comments, watchers, tasks (PLT-E11)

#### `platform.comments`

| column | type | null | notes |
| --- | --- | --- | --- |
| entity\_type / entity\_id | text / uuid | no | any record |
| project\_id | uuid | yes |  |
| parent\_comment\_id | uuid | yes | one level of replies |
| author\_type | text | no | `user` or `external_user` |
| author\_id | uuid | no |  |
| body | text | no | plain text with `@[user:uuid]` tokens, max 4,000 chars |
| visibility | text | no | CHECK in (`internal`,`shared_external`) default `internal`; external users see only `shared_external` on records shared with them |
| is\_system | bool | no | system-generated line (e.g. "status changed") |
| edited\_at | timestamptz | yes | edits allowed 15 minutes, history in audit |
| deleted\_at | timestamptz | yes | author or `platform.comment.moderate` |
| Indexes: (tenant\_id, entity\_type, entity\_id, created\_at), (tenant\_id, author\_id, created\_at desc). |  |  |  |

#### `platform.comment_mentions`

| column | type | null | notes |
| --- | --- | --- | --- |
| comment\_id | uuid | no |  |
| mentioned\_user\_id | uuid | no | must be a member of the record's project |
| notified\_at | timestamptz | yes |  |
| Keys: unique(comment\_id, mentioned\_user\_id). |  |  |  |

#### `platform.comment_attachments` — not a table: attachments use `file_links` with `entity_type='platform.comment'`.

#### `platform.record_watchers`

| column | type | null | notes |
| --- | --- | --- | --- |
| entity\_type / entity\_id | text / uuid | no |  |
| user\_id | uuid | no |  |
| reason | text | no | `author`,`mentioned`,`assignee`,`manual`,`approver` |
| Keys: unique(entity\_type, entity\_id, user\_id). |  |  |  |

#### `platform.tasks` (task inbox)

| column | type | null | notes |
|---|---|---|---|
| number | text | no | `TSK-00031` from series `TASK` |
| kind | text | no | CHECK in (`approval`,`action`,`review`,`snag`,`follow_up`,`manual`,`confirm`) |
| title\_code | text | yes | localisable code with `title_params` |
| title\_params | jsonb | yes |  |
| title\_text | text | yes | free text for manual tasks; one of code/text required |
| assignee\_id | uuid | no | user id |
| assigned\_by | uuid | yes | null = system |
| project\_id | uuid | yes |  |
| entity\_type / entity\_id | text / uuid | yes | the record to open |
| link | text | yes | deep link |
| due\_date | date | yes |  |
| priority | text | no | `low`,`normal`,`high`,`urgent` |
| status | text | no | CHECK in (`open`,`in_progress`,`done`,`cancelled`,`snoozed`) |
| source\_key | text | yes | idempotency key for system-created tasks (`approval:{stepId}:{userId}`) |
| snoozed\_until | timestamptz | yes |  |
| completed\_at | timestamptz | yes |  |
| completed\_by | uuid | yes |  |
| completion\_note | text | yes |  |
| Keys: unique(tenant\_id, source\_key) where source\_key not null. Indexes: (tenant\_id, assignee\_id, status, due\_date), (tenant\_id, entity\_type, entity\_id). |  |  |  |

### 4.13 Idempotency and concurrency (PLT-E13)

#### `platform.idempotency_keys` (Standard: only `id`, `tenant_id`, `created_at`; purged by job)

| column | type | null | notes |
|---|---|---|---|
| principal\_type / principal\_id | text / uuid | no | `user` or `external_user`; keys are scoped per principal so two users cannot collide |
| idem\_key | text | no | the `Idempotency-Key` header (uuid text, max 64 chars) |
| http\_method / path\_template | text | no | e.g. `POST`, `/api/v1/procurement/purchase-orders/{id}/issue` |
| request\_hash | bytea | no | SHA-256 of method + route values + canonical body |
| status | text | no | CHECK in (`in_progress`,`completed`) |
| response\_status | int | yes | stored for `completed` |
| response\_headers | jsonb | yes | whitelist: `Location`, `ETag`, `Content-Type` |
| response\_body | bytea | yes | max 256 KB; larger responses store only status and `Location` and replay as `409 platform.idempotency_replay_too_large` with the location |
| expires\_at | timestamptz | no | created\_at + 24 h |
| Keys: unique(tenant\_id, principal\_id, idem\_key). Index: (expires\_at). Write path: `INSERT ... ON CONFLICT DO NOTHING` first (claims the key), then the handler runs in its own transaction, then the response is stored in the same transaction as the business change (so a crash cannot leave a business change without a stored response). |  |  |  |

Concurrency has no table: `ETag` is the base64 of `row_version` (00 §3.2); `If-Match` is mandatory on `PATCH` and on mutating action endpoints that document it (BR-PLT-71).

### 4.14 Jobs and scheduling (PLT-E12)

#### `platform.job_definitions` (global, rebuilt from code at startup)

| column | type | null | notes |
|---|---|---|---|
| code | text | no | PK, `<module>_<name>` e.g. `platform_escalation_scan` |
| owner\_module | text | no |  |
| queue | text | no | CHECK in (`critical`,`default`,`reports`,`collectors`,`ai`) |
| cron | text | yes | UTC cron; null = trigger-only |
| scope | text | no | CHECK in (`global`,`per_tenant`) |
| timeout\_seconds | int | no | default 600 |
| max\_attempts | int | no | default 3 |
| min\_plan | text | no | default `starter` |
| default\_enabled | bool | no |  |
| release | text | no |  |

#### `platform.job_tenant_settings`

| column | type | null | notes |
| --- | --- | --- | --- |
| job\_code | text | no |  |
| is\_enabled | bool | no |  |
| cron\_override | text | yes | allowed only for jobs flagged `tenant_schedulable` in code (digests, scheduled reports) |
| params | jsonb | yes | validated by the job's schema |
| Keys: unique(tenant\_id, job\_code). |  |  |  |

#### `platform.job_runs`

| column | type | null | notes |
|---|---|---|---|
| job\_code | text | no |  |
| run\_key | text | no | idempotency key: `{job_code}:{scope}:{window}` e.g. `platform_escalation_scan:t1:2026-10-09T09:15` |
| trigger | text | no | `schedule`,`manual`,`event`,`retry` |
| status | text | no | CHECK in (`queued`,`running`,`succeeded`,`failed`,`timed_out`,`skipped`,`cancelled`) |
| started\_at / finished\_at | timestamptz | yes |  |
| attempts | int | no |  |
| items\_processed / items\_failed | int | no |  |
| summary | jsonb | yes | counts and warnings, never personal data |
| error | text | yes |  |
| requested\_by | uuid | yes | for `manual` |
| Keys: unique(job\_code, run\_key) (second enqueue of the same window is a no-op); Indexes: (tenant\_id, job\_code, started\_at desc), (status, started\_at) where status='running' (stuck-run detector). Per-job exclusive locking uses PostgreSQL advisory locks keyed by hash(job\_code, tenant\_id); no lock table. |  |  |  |

### 4.15 Plans, feature gating, usage (PLT-E15)

#### `platform.plan_definitions` (global seed)

| column | type | null | notes |
| --- | --- | --- | --- |
| code | text | no | PK `starter`,`growth`,`enterprise` |
| name\_ar / name\_en | text | no |  |
| limits | jsonb | no | `{ "max_active_projects": 3, "max_users": 10, "max_storage_gb": 50, "max_custom_roles": 0 }`; `null` = unlimited |
| features | text\[\] | no | feature codes enabled |
| Seeded values: Starter 3 projects / 10 users; Growth 10 / 40; Enterprise from `tenant_subscriptions.limits_override` (v3 section 1.5). External users do not count as seats. |  |  |  |

#### `platform.usage_counters`

| column | type | null | notes |
| --- | --- | --- | --- |
| metric | text | no | `active_projects`,`active_users`,`storage_bytes`,`custom_roles` |
| value | bigint | no |  |
| as\_of | timestamptz | no |  |
| Keys: unique(tenant\_id, metric). Recomputed by event handlers (`core.project.activated/closed`, `platform.user.*`) and verified nightly by `platform_usage_recount`. |  |  |  |

**Gating matrix** (feature code -> minimum plan; enforced by `IFeatureGate`, rules BR-PLT-80..86):

| Feature code | Meaning | Starter | Growth | Enterprise | Release |
| --- | --- | --- | --- | --- | --- |
| `approvals.custom_chains` | create/edit policies beyond system templates | no (choose templates) | yes | yes + rules `conditions` | R1 |
| `roles.custom` | custom roles | no | yes | yes | R1 |
| `portal.basic` / `portal.full` / `portal.branded` | portals | basic | full | branded | R1 basic, R2 full |
| `reports.custom` / `reports.scheduled` | report builder, scheduled delivery | no / no | yes / no | yes / yes | R2 |
| `export.accounting` / `export.eta` | accounting and e-invoice export | no | yes | yes | R2 |
| `api.access` / `auth.sso` / `security.ip_allowlist` | API keys, SSO, IP allow-list | no | no | yes | R2 |
| `ai.pack` | AI add-on | add-on | add-on | add-on or included | R2 |
| `support.priority` | SLA support (informational flag) | no | no | yes | R1 |

### 4.16 Tenant export and deletion (PLT-E15)

#### `platform.tenant_exports`

| column | type | null | notes |
|---|---|---|---|
| number | text | no | `EXP-0003` |
| status | text | no | CHECK in (`requested`,`awaiting_second_approval`,`approved`,`building`,`ready`,`downloaded`,`expired`,`rejected`,`failed`) |
| scope | text | no | `full` or `projects` (with `project_ids uuid[]`) |
| requested\_by / second\_approver\_id | uuid | no / yes | two different `tenant_admin` or `owner_executive` users when `export.require_dual_approval` is true (BR-PLT-87) |
| file\_id | uuid | yes | zip of CSV per table plus files manifest, encrypted; download link valid 7 days |
| manifest | jsonb | yes | table row counts and checksums |
| expires\_at | timestamptz | yes |  |

#### `platform.tenant_deletions`

| column | type | null | notes |
|---|---|---|---|
| status | text | no | CHECK in (`requested`,`cooling_off`,`executing`,`completed`,`cancelled`) |
| requested\_by | uuid | no |  |
| export\_id | uuid | no | a `ready` export must exist and be downloaded before deletion is allowed |
| cooling\_off\_until | timestamptz | no | 30 days |
| executed\_at | timestamptz | yes |  |
| certificate\_file\_id | uuid | yes | deletion certificate (row counts purged, timestamp) |

### 4.17 Localization (PLT-E14)

Holidays and fiscal calendars are owned by `core` (COR-E1, `core.holidays`, `core.fiscal_periods`); the platform only supplies the **message catalogue**, number/date formatting helpers and Hijri conversion.

#### `platform.message_overrides` (tenant-specific wording of catalogue messages)

| column | type | null | notes |
|---|---|---|---|
| message\_key | text | no | e.g. `procurement.po_over_budget` or notification template code |
| locale | text | no | `ar`,`en` |
| text | text | no | ICU message format; parameters must be a subset of the original (checked on save) |
| status | text | no | `active`,`retired` |
| Keys: unique(tenant\_id, message\_key, locale). The base catalogue is in files `messages.ar.json`, `messages.en.json` shipped with the build, not in the database. |  |  |  |

#### `platform.hijri_corrections` (global)

| column | type | null | notes |
| --- | --- | --- | --- |
| hijri\_year / hijri\_month | int | no |  |
| gregorian\_start | date | no | observed first day of month when it differs from the Umm al-Qura table |
| source | text | no |  |
| Keys: PK(hijri\_year, hijri\_month). Empty at launch \[open question 7\]. |  |  |  |

### 4.18 Saved views (PLT-E16)

#### `platform.user_views`

| column | type | null | notes |
|---|---|---|---|
| owner\_id | uuid | no |  |
| grid\_key | text | no | e.g. `procurement.purchase_orders` |
| name | text | no | unique per (owner, grid\_key) |
| definition | jsonb | no | `{ columns[], filters{}, sort, density }`; filters validated against the endpoint whitelist so a stale view cannot inject unknown fields |
| sharing | text | no | CHECK in (`private`,`project`,`tenant`); `tenant` needs `platform.view.share` |
| project\_id | uuid | yes | for `project` sharing |
| is\_default | bool | no | one default per (owner, grid\_key) |

### 4.19 Enumerations (platform-wide)

| Enumeration | Values and meaning |
|---|---|
| Tenant status | `provisioning` (seeding), `active`, `trial` (limits as Starter plus 30 days), `suspended` (read-only login blocked, data kept), `pending_deletion` (cooling off), `deleted` |
| User status | `invited` (no password), `active`, `deactivated` (cannot log in, history kept), `locked` (auto after 5 failures, until `locked_until`) |
| Approval instance status | `pending`, `approved`, `rejected` (includes returned), `cancelled` |
| Approval step status | `waiting`, `active`, `approved`, `rejected`, `skipped`, `escalated`, `cancelled` |
| Decision | `approve`, `reject`, `return` (back to owner for edit), `comment` (no state change) |
| Approval record types (seed, `approval_record_types`) | `procurement.purchase_request` R1, `procurement.purchase_order`, `procurement.purchase_order_amendment`, `procurement.supplier_invoice`, `procurement.payment_batch`, `planning.site_request`, `sourcing.recommendation`, `cost.budget`, `cost.budget_transfer`, `cost.period_close`, `contracts.contract`, `contracts.variation`, `contracts.payment_application`, `contracts.claim`, `estimating.estimate_review`, `estimating.bid_decision` (all R1 except estimating = R3, claims = R2), `subcontracts.subcontract`, `subcontracts.valuation`, `subcontracts.payment`, `subcontracts.backcharge` (R2), `core.party_bank_change`, `platform.tenant_export` R1 |
| Outbox dispatch status | `pending`, `fanned_out`, `completed`, `dead` |
| Delivery status | `pending`, `in_flight`, `done`, `failed`, `dead` |
| File status | `pending_upload` -> `uploaded` -> `scanning` -> `available`; `quarantined` (infected or type mismatch), `deleted` |
| Task kind / status | see table 4.12; `snoozed` returns to `open` at `snoozed_until` |
| Notification channel | `in_app`, `push`, `email`, `whatsapp` (channel to confirm) |
| Number series scope | `tenant` (PO-1066), `project` (restart per project), `year`, `project_year` |
| Import batch status | `uploaded`, `parsing`, `preview_ready`, `committing`, `committed`, `failed`, `cancelled` |
| Export status | see 4.16 |

### 4.20 ER summary

```txt
tenants 1--* users 1--* user_roles *--1 roles 1--* role_permissions *--1 permissions
users 1--* project_members *--1 core.projects (real FK)
roles 1--* role_limits ; field_groups 1--* field_visibility_policies (role_code, project?)
approval_policies 1--* approval_policy_steps
approval_record_types 1--* approval_instances 1--* approval_steps 1--* approval_decisions
approval_instances 1--* approval_escalations ; users 1--* approval_delegations (delegator/delegate)
outbox 1--* outbox_deliveries --(attempts exhausted)--> outbox_dead ; consumer+event -> processed_events
audit_log, status_history : append-only, keyed by (entity_type, entity_id)
files 1--* file_links ; files 1--* file_variants ; files 1--1 upload_sessions
notification_templates --code--> notifications 1--* notification_deliveries ; users 1--* notification_preferences
number_series 1--* number_series_counters ; number_series 1--* number_allocations
import_types 1--* import_batches 1--* import_rows 1--* import_row_errors
search_index (entity_type, entity_id) fed by events ; comments 1--* comment_mentions ; tasks keyed by source_key
job_definitions 1--* job_tenant_settings ; job_definitions 1--* job_runs ; plan_definitions --code--> tenants
tenant_exports 1--* tenant_deletions (export_id)
```

---

## 5. State machines

Status changes are written to `platform.status_history` by the interceptor (BR-PLT-32). "System" means a job or consumer.

### 5.1 Tenant

| From | Action | To | Guard | Who | Side effects |
|---|---|---|---|---|---|
| (none) | provision | `provisioning` -> `active`/`trial` | unique `code`; plan exists | operator | seeds roles, policies, series, templates; first `tenant_admin` invited; audit |
| `active`/`trial` | suspend | `suspended` | reason required | operator | refresh tokens revoked; API returns `403 platform.tenant_suspended`; jobs skipped |
| `suspended` | reactivate | `active` |  | operator | audit |
| `trial` | convert | `active` | subscription row created | operator | `tenant_subscriptions` row |
| `active`/`suspended` | request deletion | `pending_deletion` | ready and downloaded export exists (BR-PLT-88) | `tenant_admin` + operator | 30-day cooling off; read-only |
| `pending_deletion` | cancel | `active` | before `cooling_off_until` | `tenant_admin` |  |
| `pending_deletion` | execute | `deleted` | cooling off elapsed | operator via `TenantDeletionService` | purge all tenant rows and files, keep deletion certificate |

### 5.2 User

| From | Action | To | Guard | Who | Side effects |
|---|---|---|---|---|---|
| (none) | invite | `invited` | seat available (BR-PLT-81); email unique | `platform.user.manage` | `platform.user.invited.v1`; invitation notification |
| `invited` | accept | `active` | token valid, password policy | the invitee | audit `login` |
| `active` | lock | `locked` | 5 failed logins in 15 min | system | `locked_until` = +15 min; security notification |
| `locked` | unlock | `active` | time elapsed or admin | system / `platform.user.manage` |  |
| `active`/`locked` | deactivate | `deactivated` | not the last active `tenant_admin` (BR-PLT-04) | `platform.user.manage` | tokens revoked; open approval steps re-resolved (BR-PLT-53); `platform.user.deactivated.v1` |
| `deactivated` | reactivate | `active` | seat available | `platform.user.manage` |  |

### 5.3 Approval instance and step

| From | Action | To | Guard | Who | Side effects |
|---|---|---|---|---|---|
| (none) | request | `pending` (step 1 `active`) | one open instance per record; policy matched | owning module via `IApprovalService.Request` | `platform.approval.requested.v1`; tasks and notifications to resolved approvers |
| (none) | request | `approved` | amount below `auto_approve_below` and type supports it | system | `completed_reason='auto_below_threshold'`; `platform.approval.approved.v1` |
| step `active` | approve | step `approved` (next step `active`, or instance `approved` if last) | approver in `resolved_approver_ids` or valid delegate; limit ok (BR-PLT-45); SoD ok (BR-PLT-47); `mode` satisfied (`any` 1, `all` all, `n_of` N) | approver | decision row; `platform.approval.step_approved.v1`; task done; last step: `platform.approval.approved.v1` |
| step `active` | reject | instance `rejected` | comment required | approver | `platform.approval.rejected.v1`; other open tasks cancelled |
| step `active` | return | instance `rejected` (`completed_reason='returned'`) | comment required | approver | same event with `returned=true`; owner edits and re-requests |
| `pending` | cancel | `cancelled` | requester, or owner module on supersede/void | requester / system | `platform.approval.cancelled.v1`; tasks cancelled |
| step `active` | escalate | step `escalated` (stays decidable) | `due_at` passed and rule matched (BR-PLT-52) | system | escalation row; `platform.approval.escalated.v1`; target added to `resolved_approver_ids` |
| step `active` | skip | `skipped` | `can_skip_if_requester` and requester is the only approver and SoD allows, or no eligible approver and policy says skip (never for amount steps) | system | audit reason |

### 5.4 File

| From | Action | To | Guard | Who | Side effects |
| --- | --- | --- | --- | --- | --- |
| (none) | create upload | `pending_upload` | size/type allowed (BR-PLT-100) | uploader | presigned PUT, 15 min |
| `pending_upload` | complete | `uploaded` -> `scanning` | object exists, size equals expected | uploader | scan job queued |
| `scanning` | scan clean | `available` | magic bytes match | system | variants queued, search index entry updated |
| `scanning` | scan infected / mismatch | `quarantined` |  | system | notification to uploader and `tenant_admin`; no download |
| any | delete | `deleted` | not legal hold; permission | owner of link or admin | purge after 30 days (job) |

### 5.5 Import batch

| From | Action | To | Guard | Who | Side effects |
| --- | --- | --- | --- | --- | --- |
| (none) | upload | `uploaded` -> `parsing` | file type `.xlsx`/`.csv`, row limit | importer with type's permission |  |
| `parsing` | parsed | `preview_ready` |  | system | rows and errors stored |
| `preview_ready` | remap | `parsing` | mapping changed | importer | re-parse |
| `preview_ready` | commit | `committing` -> `committed` | `mode=all_or_nothing` needs 0 errors; `skip_errors` needs type support | importer | one transaction per 1,000 rows in skip mode; `import_commit` audit |
| `parsing`/`committing` | fail | `failed` |  | system | error file |
| `uploaded`/`preview_ready` | cancel | `cancelled` |  | importer |  |

### 5.6 Task, notification delivery, export

| Entity | Transitions |
|---|---|
| Task | `open` -> `in_progress` (assignee opens) -> `done`; `open`/`in_progress` -> `snoozed` -> `open` (at `snoozed_until`); any open -> `cancelled` (source record resolved); `done` is final. System tasks complete automatically when their approval step or source condition resolves (BR-PLT-131). |
| Notification delivery | `queued` -> `sent` -> `delivered`; `queued` -> `failed` (retry 3 times: 1 m, 10 m, 1 h) -> `failed` final; `queued` -> `suppressed` (quiet hours with later release, preference off, no consent); `queued` -> `expired` (push older than 24 h). |
| Tenant export | `requested` -> `awaiting_second_approval` -> `approved` -> `building` -> `ready` -> `downloaded` -> `expired`; `awaiting_second_approval` -> `rejected`; `building` -> `failed`. |

---

## 6. Business rules

Format: `BR-PLT-NN`, testable. Ranges: 01-09 tenancy and identity; 10-19 permissions and visibility; 20-29 number series; 30-39 audit and status; 40-59 approvals; 60-69 outbox; 70-79 idempotency and concurrency; 80-89 plans, export, deletion; 90-99 search; 100-109 files; 110-119 notifications; 120-129 import; 130-139 comments and tasks; 140-149 jobs; 150-159 localization.

### 6.1 Tenancy and identity

- **BR-PLT-01** Every tenant-owned row has `tenant_id`; RLS policy `tenant_id = current_setting('app.tenant_id')::uuid` is enabled and forced on every such table; the application role cannot bypass RLS. A request without a resolved tenant fails closed (`401`).
- **BR-PLT-02** `tid` is read only from the signed token. A client-sent tenant header or body field is ignored and logged as a security event.
- **BR-PLT-03** Login failure is generic ("invalid credentials") for unknown email and wrong password. 5 failures within 15 minutes lock the account for 15 minutes; `failed_login_count` resets on success.
- **BR-PLT-04** A tenant must always have at least one active `tenant_admin`; deactivating or demoting the last one fails with `platform.last_admin`.
- **BR-PLT-05** Refresh tokens rotate on every use. Presenting an already-rotated token revokes the whole `family_id` and writes audit `login_failed` with reason `reuse_detected`.
- **BR-PLT-06** Password policy: length >= `security.password_min_length` (10), not in the top-10,000 breached list, not equal to email or name. Argon2id parameters are configuration; hashes upgrade on login.
- **BR-PLT-07** MFA is mandatory for roles in `security.mfa_required_roles`; such a user without MFA may only call enrolment endpoints until enrolled (grace 7 days from role assignment).
- **BR-PLT-08** Invitation links expire after 7 days, are single-use, and are revoked when the user is deactivated or re-invited. A resend revokes the previous token.
- **BR-PLT-09** Suspended tenants: all requests return `403 platform.tenant_suspended` except `GET /auth/me`; outbox and jobs for the tenant pause.

### 6.2 Permissions and visibility

- **BR-PLT-10** Effective permissions of a user on a project = union of `role_permissions` of the user's roles (restricted by `project_members.role_codes` when non-empty and by `user_roles.valid_from/to` today) intersected with the project scope below.
- **BR-PLT-11** Cache key is `(user_id, tenants.permissions_version)`; any change to roles, grants, memberships or role limits bumps `permissions_version` in the same transaction; a stale cache entry may live at most 60 seconds.
- **BR-PLT-12** `IProjectContext` guard: a request addressing a project the user is not an active member of (no row, or `ended_on` in the past) returns `404` for reads and writes, unless the permission is non-project-scoped or the user holds `platform.project.view_all` for a read.
- **BR-PLT-13** List endpoints add `project_id in (visible_project_ids)`; the visible set is computed once per request from `project_members` (`ended_on is null or ended_on >= today`).
- **BR-PLT-14** System roles cannot be edited or deleted by tenants; a custom role cannot hold a permission whose `min_plan` exceeds the tenant plan, and cannot hold `platform.role.manage` unless cloned from `tenant_admin`.
- **BR-PLT-15** A role grant with `field_scope='own'` restricts `view/update` to records where `created_by` or the module's assignee field equals the user.
- **BR-PLT-16** Sensitive permissions (`is_sensitive`) can be granted only with a reason, audited as `permission_change`, and notify all `tenant_admin` users (`PLT.SECURITY.SENSITIVE_GRANT`).
- **BR-PLT-17** Field visibility evaluation for (user, project, field group): (1) user lacks the group's `visibility_permission` -> hidden; (2) project-level policy row for any of the user's roles with `effect='show'` -> shown, `hide` -> hidden (hide wins over show across roles at the same level); (3) tenant-level policy likewise; (4) `project_members.can_see_costs` true/false overrides default-hidden roles only; (5) default: hidden for roles in `field_groups.default_hidden_roles` (`foreman`, `superintendent`), shown otherwise. Hidden fields are **omitted**, never null (BR-X-06), also in exports, notifications params, search snippets and audit views.
- **BR-PLT-18** A `show` policy for a default-hidden role requires a `reason` and is audited as `policy_change`.
- **BR-PLT-19** Per-user amount limits resolve as: `project_members.limits[recordType]` if present, else the maximum `role_limits` across the user's applicable roles, else zero. A limit is compared in the tenant base currency after conversion at the document rate.

### 6.3 Number series

- **BR-PLT-20** Allocation happens inside the caller's transaction by `SELECT ... FOR UPDATE` on the counter row: rollback releases the number; committed numbers are gapless and never reused (cancelled records keep theirs).
- **BR-PLT-21** Format tokens: `{prefix}` `{yyyy}` `{project}` (project code) `{seq}` zero-padded to `padding`. Example: series `PO`, padding 4, scope `tenant`: `PO-1066`; series `PA`, padding 2, scope `project`: `PA-04` (fourth application of that project); series `VO` padding 2: `VO-05`.
- **BR-PLT-22** After the first allocation `is_locked=true`: `prefix`, `scope`, `format` and `start_value` are immutable; `padding` may only increase.
- **BR-PLT-23** `Preview` returns the next number without consuming it and is labelled non-binding. A year-scoped series starts a new counter on 1 January in the tenant time zone.
- **BR-PLT-24** `number_allocations.number` is unique per (tenant, series); a collision is a defect and raises `platform.number_collision` (alert).

### 6.4 Audit and status

- **BR-PLT-30** Audit rows are append-only: the application role has INSERT and SELECT only; a migration test fails if UPDATE/DELETE is granted.
- **BR-PLT-31** Immutability guard: for entities registered as `approved_immutable` (PO issued, certificate certified, budget approved, contract signed) the interceptor rejects updates to non-whitelisted fields with `platform.record_immutable` (422) and requires the owning module to create a new version (00 §3.3).
- **BR-PLT-32** Any change of a column named `status` on an audited entity writes one `status_history` row in the same transaction, with `from_status`, `to_status`, actor and reason (reason mandatory where the module marks the transition `requires_reason`).
- **BR-PLT-33** Hash chain: `row_hash = SHA256(prev_hash || canonical_json(row without row_hash))`, chained per tenant by insertion order (serialised by an advisory lock per tenant). `platform_audit_verify` recomputes nightly; a mismatch raises a critical alert and `platform.audit_chain_broken`.
- **BR-PLT-34** Never captured in `before/after`: `password_hash`, `mfa_secret_enc`, recovery hashes, token hashes, bank account numbers (module-flagged `audit_mask`). Masked fields log `"***"` with a changed flag.
- **BR-PLT-35** The following always write an audit row: login, failed login, logout, permission and role change, policy change, approval decision, export (any list export or tenant export), file download of category `contract`/`invoice`, import commit, impersonation by operator, number series change.
- **BR-PLT-36** Audit retention 7 years; users see audit for records they can view (`platform.audit.view_record`); the tenant-wide log needs `platform.audit.view`.

### 6.5 Approvals

- **BR-PLT-40** Policy match for (recordType, projectId, tradeCode, amount in policy currency): candidates are active policies with effective dates covering today, `min_amount <= amount < max_amount` (max exclusive), and passing `conditions`. Specificity score = 4 (project matches) + 2 (trade matches) ; highest score wins; tie broken by lower `priority`, then by earliest `code`. No match: `platform.approval_no_policy` (422). Two policies with equal score, overlapping range and equal priority are rejected at save time (`platform.approval_policy_overlap`).
- **BR-PLT-41** Snapshot: at request the engine copies amount, currency, context, policy version and all steps into the instance. Later policy edits, role changes of the record owner or amount changes in the source record never alter a running instance; a changed amount requires cancel and re-request (`superseded`).
- **BR-PLT-42** Steps run in `step_no` order; step `n+1` activates only when step `n` is `approved` or `skipped`.
- **BR-PLT-43** Approver resolution at activation: role -> active users with that role, project member of the record's project, and (if `respect_limit`) limit >= amount; `project_role` -> project members with that role code; `requester_manager` -> `users.manager_user_id` of the requester; `record_field` -> user id in the record context. Resolved ids are stored; a user outside the list cannot decide (except by delegation or escalation).
- **BR-PLT-44** Step modes: `any` completes on one approval; `all` needs every resolved approver; `n_of` needs `required_count`. Any rejection ends the instance, whatever the mode.
- **BR-PLT-45** Limit check: an approver with `respect_limit` may approve only if `amount <= effective limit` (BR-PLT-19); otherwise `platform.approval_limit_exceeded`. If no resolved approver has enough limit at activation, the step escalates immediately with reason `no_eligible_approver`.
- **BR-PLT-46** Currency: amounts in another currency are converted to the policy currency at the record's `fx_rate` (document rate), never at today's rate; the converted amount is stored in `context.amountPolicyCurrency`.
- **BR-PLT-47** Segregation of duties: `requested_by` cannot approve their own instance. Exception only if `approvals.allow_self_approval=true` **and** the record type has `allows_self_approval_flag=true`; the instance is then flagged `self_approved` and shown in the exceptions report. Payments never allow it.
- **BR-PLT-48** A user cannot decide twice on the same step; a second decision returns `platform.approval_already_decided`.
- **BR-PLT-49** Delegation: valid when today in `valid_from..valid_to`, status `active`, record type and project in scope. Delegate's authority = `min(delegator effective limit, delegation.max_amount)`; the delegate cannot re-delegate; SoD is checked for the delegate **and** the delegator.
- **BR-PLT-50** Reminder: for each active step, every `reminder_hours` (policy, else tenant 24 h) after `activated_at`, a reminder goes to approvers who have not decided, at most 3 reminders per step, never during quiet hours except `urgent`.
- **BR-PLT-51** Due date: `due_at = activated_at + sla_hours` (step, else policy, else `approvals.escalation_default_hours` 48), counted in working hours only if the tenant sets `approvals.sla_working_hours=true` \[open question 4\].
- **BR-PLT-52** Escalation: when `now > due_at + rule.after_hours` and `escalation_level < max_levels`, add the rule's target to the approvers (`notify_only=false`) or only notify (`notify_only=true`), write an escalation row, set `escalation_level+1`, extend `due_at` by the step SLA.
- **BR-PLT-53** When an approver is deactivated while a step is active, the step re-resolves; if the resolved set becomes empty it escalates with reason `approver_deactivated`.
- **BR-PLT-54** Reject and return require a comment of at least 10 characters (record types with `requires_reason_on_reject`).
- **BR-PLT-55** An approval decision must carry the `If-Match` version of the instance; a decision against a cancelled instance returns `412`.
- **BR-PLT-56** Dry-run (`POST /approval-policies/simulate`) returns the policy, steps and resolved approvers that would apply, without creating anything.
- **BR-PLT-57** The engine never changes the owning record; it publishes events and the owning module reacts. If the owning module is unavailable the outbox retries (BR-PLT-60).

**Worked approval example.** Policy `PO-STD-100K` for `procurement.purchase_order`: \[0, 100,000) one step project manager; `PO-MID-500K` \[100,000, 500,000): step 1 project manager (`any`), step 2 finance controller (`any`); `PO-LRG` \[500,000, no limit): add step 3 owner executive. Cement PO from offer A, value 179,340 EGP: matches `PO-MID-500K`; project manager (limit 250,000) approves step 1; finance controller (limit 300,000) approves step 2 -> instance `approved`, `platform.approval.approved.v1` with `amount: 179340.0000`. If the PM's limit were 150,000 on this project: the step has no eligible approver -> escalates with `no_eligible_approver` to the escalation rule target. Delegation: PM on leave delegates to the superintendent with `max_amount 100,000`: authority = min(250,000, 100,000) = 100,000, so approving 179,340 fails with `platform.delegation_limit_exceeded`. Payment application PA-04 payable 2,352,590 is routed by `contracts.payment_application` policy \[1,000,000, no limit\]: PM, then owner executive.

### 6.6 Outbox

- **BR-PLT-60** Events are inserted into `outbox` in the same transaction as the business change; `aggregate_seq` is assigned from a per-aggregate counter so consumers can detect gaps. No event is published outside a transaction.
- **BR-PLT-61** Fan-out creates one `outbox_deliveries` row per matching subscription; delivery for one aggregate is serial (`ordered=true`): a failing delivery blocks later events of the same aggregate for that consumer until it succeeds or is dead-lettered.
- **BR-PLT-62** A consumer must insert `processed_events(consumer, event_id)` in the same transaction as its side effects; a unique violation means "already processed" and the handler returns success without side effects.
- **BR-PLT-63** Retry schedule 10 s, 1 m, 5 m, 30 m, 2 h (00 §3.9), then `outbox_dead` with `resolution='open'` and an alert; discard needs a note.
- **BR-PLT-64** Replay of a dead letter re-queues the delivery with attempts reset; replay of a whole event type for a consumer (rebuild) is allowed only for `platform_operator` and only for events newer than outbox retention (90 days).
- **BR-PLT-65** Publish validates the payload against the registered JSON schema in non-production and in CI; in production an invalid payload raises and rolls back the business transaction.
- **BR-PLT-66** Outbox retention: `completed` rows are deleted after 90 days; `outbox_dead` rows are kept until resolved plus 1 year.
- **BR-PLT-67** Lag metric = `now - min(created_at)` of pending deliveries; alert above 60 s for 5 minutes (00 §3.16).

### 6.7 Idempotency and concurrency

- **BR-PLT-70** Every `POST` that creates or acts requires `Idempotency-Key` for mobile and portal clients and accepts it for web; absent key on a mobile client returns `400 platform.idempotency_key_required`.
- **BR-PLT-71** `PATCH` and documented action endpoints require `If-Match`; absent -> `428 platform.precondition_required`; stale -> `412 platform.precondition_failed` with the current ETag and current representation.
- **BR-PLT-72** Same key and same `request_hash` replays the stored response with header `Idempotent-Replay: true` (also while `in_progress`: `409 platform.request_in_progress` with `Retry-After: 2`); same key and different hash -> `422 platform.idempotency_key_reuse`.
- **BR-PLT-73** Keys expire after 24 h; a failed (5xx) response is not stored, so the client may retry with the same key.

### 6.8 Plans, export and deletion

- **BR-PLT-80** `IFeatureGate.IsEnabled(code)` = plan features plus `features_override` of the current subscription; checked at the application service, not only the UI. Failure: `403 platform.feature_not_in_plan` with `params.requiredPlan`.
- **BR-PLT-81** Seat limit: invite or reactivate fails with `platform.user_limit_reached` when `active_users + invited_users >= max_users`. Deactivated users do not count.
- **BR-PLT-82** Active project limit: `core.project.activated` is blocked when `active_projects >= max_active_projects` (check exposed as `IFeatureGate.GetLimit`, enforced by Core). Closed and draft projects do not count.
- **BR-PLT-83** Downgrade is refused while usage exceeds the target plan limits; existing custom policies stay readable but cannot be edited after downgrade to Starter (system templates are used for new requests).
- **BR-PLT-84** Starter tenants choose among `is_system_template` policies (enable/disable, edit limits within allowed bands, no new steps); Growth+ can create and edit steps; `conditions` rules are Enterprise.
- **BR-PLT-85** Storage limit 50 GB per tenant (Starter and Growth) warns at 80% and blocks uploads at 100%; Enterprise by contract.
- **BR-PLT-86** Trial: `trial_ends_at` = start + 30 days; after expiry the tenant becomes `suspended` (read-only export allowed for 30 days).
- **BR-PLT-87** Tenant export requires two different approvers (`tenant_admin` and one other `tenant_admin` or `owner_executive`) when `export.require_dual_approval`; the requester cannot be the second approver.
- **BR-PLT-88** Deletion requires a `downloaded` export younger than 30 days, a 30-day cooling off, and both actions audited; the deletion certificate lists tables and counts and is retained by the operator.
- **BR-PLT-89** Export content obeys BR-X-06: a full export by `tenant_admin` includes all fields (the company owns its data) but is flagged in audit; project-scoped exports obey the requester's visibility.

### 6.9 Search

- **BR-PLT-90** Index updates come only from events (`entity.created/updated`) with idempotent upsert on (tenant, entity\_type, entity\_id); `updated_event_at` prevents an older event overwriting a newer one.
- **BR-PLT-91** Query = normalised text (BR-PLT-150); a number-like token (`PO-1066`, `1066`) tries exact and prefix on `number` first and ranks above text hits; otherwise `tsv @@ plainto_tsquery('simple', ...)` unioned with trigram similarity >= 0.3; synonyms expand the term.
- **BR-PLT-92** Results are trimmed by `project_id in visible projects` **and** `required_permission` held by the user; counts shown to users never include trimmed hits.
- **BR-PLT-93** Deleted, cancelled-and-hidden or archived records are removed from the index within 1 minute of the event.
- **BR-PLT-94** Search history stores the normalised query per user, last 20, 90 days.
- **BR-PLT-95** Entity types carrying hidden field groups never index amounts, rates or margins into `body_normalized`, `title`, `subtitle`; an architecture test lists indexed DTO fields and fails if one belongs to a `field_groups.field_paths` entry.

### 6.10 Files

- **BR-PLT-100** Allowed content types: PDF, JPEG, PNG, HEIC (converted), WebP, DOCX, XLSX, CSV, DWG/DXF (drawings), ZIP only for imports; verified by magic bytes. Size \<= `files.max_upload_mb` (25; 100 for category `drawing`); violation `platform.file_too_large` / `platform.file_type_not_allowed`.
- **BR-PLT-101** Download only for `available` files; permission = permission of the primary linked record (or any link) and project scope; the URL is presigned for 5 minutes and every download of categories `contract`, `invoice`, `certificate` is audited.
- **BR-PLT-102** `quarantined` files cannot be downloaded or linked; `skipped` scan (scanner down) holds the file in `scanning` for up to 30 minutes then `quarantined` with reason `scan_unavailable`.
- **BR-PLT-103** Duplicate detection by `(tenant, sha256)` is advisory: upload succeeds and returns `duplicateOf` so the UI can warn.
- **BR-PLT-104** Photos keep EXIF capture time and GPS in `exif_*` as site evidence; the stored original is never altered; thumbnails strip EXIF.
- **BR-PLT-105** External users download only files with `visibility='shared_external'` linked to a record shared with them.
- **BR-PLT-106** Physical deletion occurs 30 days after logical delete unless `legal_hold`; files linked to a posted financial record cannot be deleted by users (`platform.file_in_use`).

### 6.11 Notifications

- **BR-PLT-110** Producers send an intent `(intent_id, template_code, recipients[], params, link)`; the same `intent_id` for the same recipient creates one notification (idempotent).
- **BR-PLT-111** Recipients can be users, roles-on-project (resolved to members) or external users; deactivated users are dropped silently.
- **BR-PLT-112** Channels per category follow `notification_preferences`, except `is_mandatory` templates (approval requests, security): `in_app` always, `email` fallback when push not delivered within 15 minutes.
- **BR-PLT-113** Quiet hours (tenant default 21\:00-06\:30, user override): non-priority deliveries are held (`suppressed`, `quiet_hours`) and released at the end; `priority='high'`, approvals and security bypass.
- **BR-PLT-114** `dedupe_key` collapses repeated alerts: within 24 hours an existing unread notification with the same key is updated (counter in `params.count`) instead of inserting a new one.
- **BR-PLT-115** Params containing hidden field groups are filtered per recipient before rendering (BR-PLT-17).
- **BR-PLT-116** Daily digest: one message per user per day at 07:30 local time summarising unread approvals, overdue tasks and alerts, only if non-empty.
- **BR-PLT-117** WhatsApp-style messages require recorded consent (`no_consent` suppression) and an approved template; Arabic is default, language follows `users.locale`.

### 6.12 Import

- **BR-PLT-120** Pipeline: upload -> parse -> preview -> commit (3 steps, 00 §3.14). Preview shows the first 200 rows, totals, per-row errors and warnings in Arabic or English per user locale.
- **BR-PLT-121** Column mapping auto-detects by header aliases (Arabic and English, normalised); unmapped required columns block commit.
- **BR-PLT-122** `all_or_nothing`: any error row blocks commit. `skip_errors`: valid rows commit, error rows are exported in the error file; availability per type.
- **BR-PLT-123** Commit is idempotent per batch (`committing` batches are resumable; rows already `committed` are skipped).
- **BR-PLT-124** Every created record carries `source='import'` and `import_batch_id`; `verification_status` starts `unverified` and a different user than the importer must verify when the type flags `requires_second_verification` (BOQ).
- **BR-PLT-125** Numeric parsing accepts Arabic-Indic digits and the Arabic decimal and thousands separators; Excel dates and Hijri strings are rejected with `import.date_unparseable` (Gregorian only on import).
- **BR-PLT-126** Max rows per type (default 20,000), max file 25 MB; 5,000 rows preview under 10 s (00 §3.17).

### 6.13 Comments and tasks

- **BR-PLT-130** A comment can be added only by a user who can view the record; `@mention` must be a member of the record's project, otherwise ignored with a warning.
- **BR-PLT-131** System tasks use `source_key`; creating the same key twice is a no-op; a system task completes or cancels automatically when its source resolves (approval step decided, snag closed).
- **BR-PLT-132** External users see and write only comments with `visibility='shared_external'` on records shared with them; internal comments never appear in portal payloads.
- **BR-PLT-133** Comments may be edited for 15 minutes by the author; deletion is soft and audited; moderation needs `platform.comment.moderate`.
- **BR-PLT-134** Watchers: author, assignee, approvers and mentioned users are added automatically; they receive `PLT.COMMENT.NEW` per category preference.

### 6.14 Jobs

- **BR-PLT-140** Every job is idempotent per `run_key`; a duplicate enqueue is a no-op (`unique(job_code, run_key)`).
- **BR-PLT-141** Per-tenant jobs run once per tenant per window with the tenant context; one tenant's failure does not stop others.
- **BR-PLT-142** A `running` job past `timeout_seconds` is marked `timed_out` by the stuck-run detector and re-queued up to `max_attempts`.
- **BR-PLT-143** Jobs of suspended tenants are skipped with status `skipped`.

### 6.15 Localization

- **BR-PLT-150** Arabic normalisation (search and duplicate detection): alef forms (أ إ آ ٱ) -> ا; ى -> ي; ة -> ه; remove tashkeel and tatweel; Arabic-Indic digits -> Western.
- **BR-PLT-151** Stored dates are Gregorian; Hijri is converted at the edges with the Umm al-Qura table plus `hijri_corrections`; the API never accepts Hijri input except in fields documented as `hijriInput`.
- **BR-PLT-152** All API errors and validation messages carry `code` + `params`; server-side documents render in the tenant or user locale; a missing Arabic message fails CI (catalogue completeness test).
- **BR-PLT-153** Week start Saturday, weekend Friday-Saturday by default (`locale.week_start`, tenant setting); working-day arithmetic uses `core.holidays`.

---

## 7. API endpoints

Base `/api/v1/platform`. All conventions per 00 §3.6 (keyset pagination, actions as `POST`, `If-Match` on mutations, `Idempotency-Key`, whitelisted filters). Errors use the codes of section 12. "Tenant" below is always the token's `tid`. Operator endpoints (`/api/v1/operator/...`) are served by a separate host with `aud=operator` and are not tenant-scoped.

### 7.1 Tenancy, settings, plan (PLT-E1, PLT-E15)

| Method | Path | Purpose | Key request fields | Key response fields | Errors | Permission |
|---|---|---|---|---|---|---|
| GET | `/tenant` | Current tenant profile and plan | - | `code, nameAr, nameEn, status, plan, trialEndsAt, limits, usage, features` | - | any authenticated |
| PATCH | `/tenant` | Edit profile (names, logo, tax ids) | `nameAr, nameEn, logoFileId` | tenant | 412 | `platform.tenant.manage` |
| GET | `/tenant/settings` | All settings by key | - | `[{key, value, valueType}]` | - | `platform.tenant.view_settings` |
| PATCH | `/tenant/settings` | Change settings (batch) | `{ "approvals.allow_self_approval": false, ... }` | settings | `setting_unknown, setting_invalid` | `platform.tenant.manage` |
| GET | `/tenant/usage` | Seats, projects, storage vs limits | - | counters, limits | - | `platform.tenant.view_settings` |
| POST | `/operator/tenants` | Provision tenant | `code, nameAr, planCode, adminEmail` | tenant | `tenant_code_taken` | operator |
| POST | `/operator/tenants/{id}:suspend` / `:reactivate` / `:convert` | Lifecycle actions | `reason` | tenant | `tenant_invalid_transition` | operator |
| POST | `/tenant-exports` | Request export | `scope, projectIds?` | export (`awaiting_second_approval` or `approved`) | `export_not_allowed` | `platform.export.request` |
| POST | `/tenant-exports/{id}/approve` / `/reject` | Second approval | `comment` | export | `export_self_approval` | `platform.export.approve` |
| GET | `/tenant-exports/{id}/download` | Signed link, 7 days | - | `url, expiresAt` | `export_expired` | `platform.export.request` |
| POST | `/tenant-deletions` | Request deletion | `exportId, confirmCode` | deletion | `deletion_export_required` | `platform.tenant.delete` |
| POST | `/tenant-deletions/{id}/cancel` | Cancel in cooling off | - | deletion |  | `platform.tenant.delete` |

### 7.2 Identity (PLT-E1)

| Method | Path | Purpose | Key request fields | Key response fields | Errors | Permission |
| --- | --- | --- | --- | --- | --- | --- |
| POST | `/auth/login` | Password login | `email, password, mfaCode?` | tokens or `mfaRequired` | `invalid_credentials, account_locked, tenant_suspended, mfa_required` | public |
| POST | `/auth/refresh` | Rotate refresh token | `refreshToken` | tokens | `refresh_token_reused` (revokes family) | public |
| POST | `/auth/logout` | Revoke current session | - | 204 |  | authenticated |
| POST | `/auth/password/forgot` / `/reset` | Reset flow | `email` / `token, newPassword` | 202 / 204 | `token_invalid, password_policy` | public |
| POST | `/auth/password/change` | Change own password | `current, new` | 204 | `password_policy, invalid_credentials` | authenticated |
| POST | `/auth/mfa/enroll` / `/verify` / `/disable` | TOTP | `code` | secret URI, recovery codes | `mfa_code_invalid` | authenticated |
| GET / DELETE | `/auth/sessions`, `/auth/sessions/{id}` | List and revoke sessions | - | `deviceLabel, lastUsedAt, ip` |  | authenticated |
| POST | `/invitations/{token}/accept` | Accept invitation | `name, password, phone?` | tokens | `token_invalid` | public |
| GET | `/users` | List users | filters below | `id, name, email, status, roles, lastLoginAt` |  | `platform.user.view` |
| POST | `/users` (invite) | Invite user | `email, name, roleCodes[], projectIds[]` | user | `user_limit_reached, email_taken` | `platform.user.manage` |
| GET / PATCH | `/users/{id}` | Read, edit profile and roles | `name, phone, roleCodes` | user | 412 | `platform.user.view` / `.manage` |
| POST | `/users/{id}/deactivate` / `/reactivate` / `/unlock` / `/resend-invitation` | Lifecycle actions | `reason` | user | `last_tenant_admin, user_limit_reached` | `platform.user.manage` |
| GET / PATCH | `/me` | Own profile, locale, calendar, numerals | `locale, calendar, numerals, timezone` | profile + effective permissions summary |  | authenticated |

`GET /users` filters: `status, roleCode, projectId, q`; sort: `name, email, createdAt, lastLoginAt`.

### 7.3 Roles, permissions, membership, visibility (PLT-E2)

| Method | Path | Purpose | Key request fields | Key response fields | Errors | Permission |
| --- | --- | --- | --- | --- | --- | --- |
| GET | `/permissions` | Catalogue | filter `module` | code, names, flags, minPlan |  | `platform.role.view` |
| GET / POST | `/roles` | List, create custom role | `code, nameAr, nameEn, cloneFrom?` | role | `role_code_taken, feature_not_in_plan` | `platform.role.manage` |
| GET / PATCH / DELETE | `/roles/{id}` | Read, edit, delete custom |  | role | `role_system_immutable, role_in_use` | `platform.role.manage` |
| PUT | `/roles/{id}/permissions` | Replace grants | `[{permissionCode, fieldScope}]`, `reason` | role | `permission_plan_blocked, sensitive_reason_required` | `platform.role.manage` |
| GET / PUT | `/roles/{id}/limits` | Approval limits by record type | `[{recordType, maxAmount, currency}]` | limits |  | `platform.role.manage` |
| PUT | `/users/{id}/roles` | Replace user's roles | `[{roleCode, validFrom?, validTo?}]` | roles | `last_tenant_admin` | `platform.user.manage` |
| GET / POST | `/projects/{projectId}/members` | List, add member | `userId, roleCodes[], limits, canSeeCosts?` | member | `member_exists` | `platform.project_member.manage` |
| PATCH / DELETE | `/projects/{projectId}/members/{userId}` | Edit, end membership | `endedOn` | member |  | `platform.project_member.manage` |
| GET / PUT | `/field-visibility` | Policies per field group, role, project | `[{fieldGroupCode, roleCode, projectId?, effect, reason}]` | policies | `visibility_reason_required` | `platform.visibility.manage` |
| GET | `/me/permissions` | Effective permissions and visible projects | `projectId?` | codes, limits, hiddenFieldGroups |  | authenticated |

### 7.4 Approvals (PLT-E3)

| Method | Path | Purpose | Key request fields | Key response fields | Errors | Permission |
| --- | --- | --- | --- | --- | --- | --- |
| GET | `/approval-policies` | List | filter `recordType, status` | policies with steps |  | `platform.approval_policy.view` |
| POST | `/approval-policies` | Create (Growth+) | `recordType, name, minAmount, maxAmount, steps[]` | policy | `policy_overlap, feature_not_in_plan` | `platform.approval_policy.manage_custom` |
| PATCH | `/approval-policies/{id}` | Edit limits or steps (creates new version) |  | policy | `policy_overlap` | `platform.approval_policy.manage` |
| POST | `/approval-policies/{id}/activate` / `/retire` | Activate or retire | `effectiveFrom` | policy | `policy_overlap` | `platform.approval_policy.manage` |
| POST | `/approval-policies/simulate` | Dry run (BR-PLT-56) | `recordType, projectId, tradeCode, amount, currency` | policy, steps, resolved approvers | `policy_not_found` | `platform.approval_policy.view` |
| GET | `/approvals` | Instances (admin view) | filters below | instance summary |  | `platform.approval.view_all` |
| GET | `/approvals/mine` | Approvals waiting for me (incl. delegated) | `status` | items with `stepNo, dueAt, amount, recordRef` |  | authenticated |
| GET | `/approvals/{id}` | Instance, steps, decisions |  | full instance |  | requester, approver, or `platform.approval.view_all` |
| POST | `/approvals/{id}/steps/{stepNo}/approve` | Approve | `comment?` | instance | `approval_limit_exceeded, approval_self_approval, approval_already_decided, approval_not_active_step` | `platform.approval.decide` (record type's own permission also checked) |
| POST | `/approvals/{id}/steps/{stepNo}/reject` and `/return` | Reject, return | `comment` (min 10) | instance | `approval_comment_required` | `platform.approval.decide` |
| POST | `/approvals/{id}/cancel` | Cancel | `reason` | instance | `approval_not_pending` | requester or `platform.approval.cancel_any` |
| POST | `/approvals/{id}/reassign` | Admin reassign step | `stepNo, userId, reason` | instance |  | `platform.approval.reassign` |
| GET / POST | `/approval-delegations` | List mine / create | `delegateId, validFrom, validTo, recordTypes[], projectIds[], maxAmount` | delegation | `delegation_invalid_period, delegation_self` | `platform.approval.delegate` |
| POST | `/approval-delegations/{id}/revoke` | Revoke |  | delegation |  | owner or `platform.approval.manage` |
| GET / PUT | `/approval-escalation-rules` | Rules | `recordType, afterHours, target, notifyOnly, maxLevels` | rules |  | `platform.approval_policy.manage` |

`GET /approvals` filters: `status, recordType, projectId, requestedBy, approverId, dueBefore, amountMin, amountMax`; sort: `requestedAt, dueAt, amount`.

Owning modules do not call these endpoints to request an approval: they call `IApprovalService.Request` in-process inside their own transaction. The record type's own permission (for example `procurement.purchase_order.approve`) is also required on approve and is enforced by the engine through the registry column `permission_code`.

### 7.5 Outbox admin and audit (PLT-E4, PLT-E5)

| Method | Path | Purpose | Key request fields | Key response fields | Errors | Permission |
| --- | --- | --- | --- | --- | --- | --- |
| GET | `/outbox/dead` | Dead letters | filter `consumer, eventType, resolution` | event, consumer, error, attempts |  | `platform.outbox.view` |
| POST | `/outbox/dead/{id}/replay` | Requeue | - | delivery | `dead_letter_resolved` | `platform.outbox.manage` |
| POST | `/outbox/dead/{id}/discard` | Discard | `note` (required) | dead letter |  | `platform.outbox.manage` |
| GET | `/outbox/lag` | Lag metric | - | `lagSeconds, pendingCount` |  | `platform.outbox.view` |
| GET | `/audit` | Audit search | filters below | `at, userId, entityType, entityId, action, before, after, reason, ip` |  | `platform.audit.view` |
| GET | `/audit/records/{entityType}/{entityId}` | One record's history |  | same |  | `platform.audit.view_record` (and read right on the record) |
| GET | `/status-history/{entityType}/{entityId}` | Status timeline |  | `from, to, at, userId, reason` |  | read right on the record |
| POST | `/audit/verify-chain` | Check hash chain for a date range | `from, to` | `ok, firstBrokenId?` |  | `platform.audit.verify` |

`GET /audit` filters: `userId, entityType, entityId, action, projectId, from, to`; sort: `-at` only. Audit view omits hidden field groups for users without `field.cost_amounts.view` (BR-PLT-17).

### 7.6 Files (PLT-E6)

| Method | Path | Purpose | Key request fields | Key response fields | Errors | Permission |
| --- | --- | --- | --- | --- | --- | --- |
| POST | `/files/uploads` | Start upload session | `fileName, contentType, sizeBytes, sha256?, entityType?, entityId?` | `uploadId, fileId, url (PUT), expiresAt` | `file_type_blocked, file_too_large, storage_limit_reached` | `platform.file.upload` |
| POST | `/files/uploads/{uploadId}/complete` | Finish | `etag?` | file (`scanning`) | `upload_incomplete` | `platform.file.upload` |
| GET | `/files/{id}` | Metadata |  | `name, size, status, links, thumbnails` |  | read right on a linked record |
| GET | `/files/{id}/download` | Signed URL (5 min) | `variant?` | `url` | `file_quarantined, file_not_available` | read right on a linked record |
| POST | `/files/{id}/links` / DELETE `/files/{id}/links/{linkId}` | Attach to more records | `entityType, entityId, visibility` | link |  | `platform.file.upload` |
| DELETE | `/files/{id}` | Soft delete | `reason` | 204 | `file_legal_hold` | owner or `platform.file.manage` |
| GET | `/files` | Search library | filters: `entityType, entityId, projectId, status, mimeGroup, uploadedBy, from, to`; sort `createdAt, name, size` | page |  | `platform.file.view` |

### 7.7 Notifications, tasks, comments (PLT-E7, PLT-E11)

| Method | Path | Purpose | Key request fields | Key response fields | Errors | Permission |
| --- | --- | --- | --- | --- | --- | --- |
| GET | `/notifications` | My inbox | filter `unread, category, projectId` | items, `unreadCount` |  | authenticated |
| POST | `/notifications/{id}/read` / `/archive`, `POST /notifications:read-all` | Mark |  | 204 |  | authenticated |
| GET / PUT | `/notification-preferences` | My preferences | `[{category, channel, isEnabled, digestMode, quietFrom, quietTo}]` | prefs | `preference_mandatory` | authenticated |
| GET / PATCH | `/notification-templates` | Tenant overrides of wording | `code, locale, channel, body` | template | `template_param_missing` | `platform.notification.manage_templates` |
| POST | `/push-tokens` | Register device | `platform, token` | 204 |  | authenticated |
| GET | `/tasks` | My tasks (or others with permission) | filters: `status, kind, projectId, dueBefore, assigneeId`; sort `dueDate, priority, createdAt` | tasks |  | authenticated; `assigneeId` other than own needs `platform.task.view_all` |
| POST | `/tasks` | Manual task | `title, assigneeId, dueDate, entityRef?` | task |  | `platform.task.create` |
| POST | `/tasks/{id}/start` / `/complete` / `/snooze` / `/reassign` / `/cancel` | Actions | `note, snoozedUntil, assigneeId` | task | `task_not_open` | assignee or `platform.task.manage` |
| GET / POST | `/comments` | List by record / add | `entityType, entityId, body, mentions[], visibility, parentId?, fileIds[]` | comment | `comment_body_empty, mention_not_visible` | read right on the record; add needs `platform.comment.create` |
| PATCH / DELETE | `/comments/{id}` | Edit within 15 min / soft delete | `body` | comment | `comment_edit_window_closed` | author; delete also `platform.comment.moderate` |
| GET / POST / DELETE | `/records/{entityType}/{entityId}/watchers` | Watch |  | watchers |  | read right |

### 7.8 Numbers, import, search, jobs, views, localization (PLT-E8, E9, E10, E12, E14, E16)

| Method | Path | Purpose | Key request fields | Key response fields | Errors | Permission |
| --- | --- | --- | --- | --- | --- | --- |
| GET | `/number-series` | List series |  | `code, format, scope, nextPreview` |  | `platform.number_series.view` |
| PATCH | `/number-series/{code}` | Change prefix, padding, scope, start (only if unused or higher) | `format, padding, startAt` | series | `series_start_conflict` | `platform.number_series.manage` |
| GET | `/number-series/{code}/allocations` | Issued numbers | `from, to` | allocations |  | `platform.number_series.view` |
| GET | `/import-types` | Available imports |  | `code, permission, templateUrl` |  | authenticated, filtered by permission |
| POST | `/imports` | Upload and parse | multipart `typeCode, file, projectId?, mode` | batch | `import_type_unknown, import_too_many_rows, file_type_blocked` | the import type's permission |
| GET | `/imports/{id}` / `/imports/{id}/rows` / `/imports/{id}/errors` | Preview | filters `status` | rows, errors (Arabic messages) |  | importer |
| PUT | `/imports/{id}/mapping` | Re-map columns | `{column: field}` | batch |  | importer |
| POST | `/imports/{id}/commit` / `/cancel` | Commit or cancel | `mode` | batch | `import_has_errors` | importer |
| GET | `/imports/{id}/error-file` | Errors as xlsx |  | file |  | importer |
| GET | `/search` | Global search | `q, types[], projectId, limit` | hits with `entityType, entityId, title, subtitle, snippet, link, rank` | `search_query_too_short` | authenticated (trimmed by permission and project) |
| GET | `/search/suggest` | Autocomplete | `q` | suggestions |  | authenticated |
| GET / PUT | `/search/synonyms` | Tenant synonyms | `term, synonyms[]` | list |  | `platform.search.manage` |
| GET | `/jobs` | Job definitions with last run |  | `code, cron, lastRun, enabled` |  | `platform.job.view` |
| POST | `/jobs/{code}/run` | Manual run | `params` | run (202) | `job_not_manual` | `platform.job.run` |
| PATCH | `/jobs/{code}/settings` | Enable, cron override, params |  | settings | `job_not_schedulable` | `platform.job.manage` |
| GET | `/job-runs` | Runs | filters `jobCode, status, from, to` | runs |  | `platform.job.view` |
| GET / POST / PATCH / DELETE | `/views` | Saved grid views | `gridKey, name, columns, filters, sort, isShared` | view |  | authenticated; sharing needs `platform.view.share` |
| GET | `/localization/messages` | Catalogue with overrides | `locale, prefix` | messages |  | `platform.localization.view` |
| PUT | `/localization/overrides/{code}` | Tenant wording | `locale, text` | override | `message_param_mismatch` | `platform.localization.manage` |
| GET | `/calendar/convert` | Gregorian to Hijri and back (helper) | `date, to` | date |  | authenticated |

### 7.9 Allowed filters and sorts (summary)

| Endpoint | `filter[...]` | `sort` |
|---|---|---|
| `/users` | `status, roleCode, projectId, q` | `name, email, createdAt, lastLoginAt` |
| `/approvals` | `status, recordType, projectId, requestedBy, approverId, dueBefore, amountMin, amountMax` | `requestedAt, dueAt, amount` |
| `/audit` | `userId, entityType, entityId, action, projectId, from, to` | `-at` |
| `/files` | `entityType, entityId, projectId, status, mimeGroup, uploadedBy, from, to` | `createdAt, name, size` |
| `/notifications` | `unread, category, projectId` | `-createdAt` |
| `/tasks` | `status, kind, projectId, dueBefore, assigneeId` | `dueDate, priority, createdAt` |
| `/outbox/dead` | `consumer, eventType, resolution` | `-createdAt` |
| `/job-runs` | `jobCode, status, from, to` | `-startedAt` |
| `/comments` | `entityType, entityId, visibility` | `createdAt` |

### 7.10 Examples

**Approve a step** (PO of 183,000 EGP at the second step):

```txt
POST /api/v1/platform/approvals/7c1e.../steps/2/approve
Idempotency-Key: 5f0c0a52-9d2a-4c8e-8a53-5d8e0b0a6c11
If-Match: "9"
{ "comment": "Approved, price in line with comparison." }
```

```json
{ "id": "7c1e...", "recordType": "procurement.purchase_order", "recordNumber": "PO-1066",
  "status": "approved", "currentStepNo": 2, "completedReason": "all_steps_approved",
  "steps": [
    { "stepNo": 1, "status": "approved", "approvedCount": 1 },
    { "stepNo": 2, "status": "approved", "approvedCount": 1 } ],
  "etag": "10" }
```

**Limit exceeded** (approver limit 100,000, amount 183,000):

```json
{ "type": "https://errors.constructionconnect.example/platform/approval_limit_exceeded",
  "title": "approval_limit_exceeded", "status": 422, "code": "platform.approval_limit_exceeded",
  "detail": "Your approval limit is below the amount.", "traceId": "00-...",
  "params": { "limit": 100000.0000, "amount": 183000.0000, "currency": "EGP" } }
```

**Simulate a policy:**

```json
POST /approval-policies/simulate
{ "recordType": "procurement.purchase_order", "projectId": "a1...", "tradeCode": "PLUMB", "amount": 183000, "currency": "EGP" }
-> { "policyCode": "PO-MID-500K", "version": 3,
     "steps": [ { "stepNo": 1, "mode": "any", "approvers": [ { "userId": "u1", "name": "..." } ], "slaHours": 24 },
                { "stepNo": 2, "mode": "any", "approvers": [ { "userId": "u7", "name": "..." } ], "slaHours": 48 } ] }
```

**Import preview** (`POST /imports` then `GET /imports/{id}`):

```json
{ "id": "b3...", "typeCode": "core.materials", "status": "preview_ready", "mode": "skip_errors",
  "counts": { "rows": 412, "valid": 405, "errors": 7, "warnings": 12 },
  "columnMapping": { "الصنف": "nameAr", "الوحدة": "unitCode", "السعر": "referencePrice" } }
```

**Upload a photo:**

```json
POST /files/uploads  { "fileName": "slab-L3.jpg", "contentType": "image/jpeg", "sizeBytes": 2481920, "entityType": "planning.daily_report", "entityId": "d2..." }
-> { "uploadId": "...", "fileId": "...", "url": "https://storage.../put?sig=...", "expiresAt": "2026-10-10T10:15:00Z", "headers": { "x-amz-checksum-sha256": "required" } }
```

---

## 8. Events

Envelope per 00 §3.9; `aggregate` is the partition key. Platform publishes the registry events below. Payloads carry ids and facts, never whole aggregates. All amounts `numeric(18,4)` serialised as JSON numbers with 4 decimals, with `currency`.

### 8.1 Published

| Event | Aggregate | Fires when | Payload |
|---|---|---|---|
| `platform.user.invited.v1` | user | Invitation created or resent | `userId, email, roleCodes[], invitedBy, invitationId, expiresAt` |
| `platform.user.deactivated.v1` | user | User moves to `deactivated` | `userId, deactivatedBy, reason, openApprovalStepIds[], openTaskCount` |
| `platform.approval.requested.v1` | approval instance | Instance created in `pending` | `approvalId, recordType, recordId, recordNumber, recordVersion, projectId, tradeCode, amount, currency, requestedBy, policyId, policyVersion, step: { stepNo, mode, approverIds[], dueAt }` |
| `platform.approval.step_approved.v1` | approval instance | A step completes and a later step exists | `approvalId, recordType, recordId, stepNo, approvedBy[], nextStepNo, nextApproverIds[]` |
| `platform.approval.approved.v1` | approval instance | Last step approved, or auto-approval | `approvalId, recordType, recordId, recordVersion, projectId, amount, currency, approvedBy[], completedReason, selfApproved` |
| `platform.approval.rejected.v1` | approval instance | Rejected or returned | `approvalId, recordType, recordId, recordVersion, rejectedBy, comment, returned (bool)` |
| `platform.approval.escalated.v1` | approval instance | Escalation row created | `approvalId, recordType, recordId, stepNo, level, targetUserIds[], notifyOnly, reason` |
| `platform.approval.cancelled.v1` | approval instance | Cancelled | `approvalId, recordType, recordId, cancelledBy, reason (cancelled_by_requester / cancelled_by_owner_module / superseded)` |

Ordering: events of one instance carry `aggregate_seq` so a consumer sees `requested` before `step_approved` before `approved`. Event JSON schemas live in `Platform.Contracts/events/`.

### 8.2 Consumed

| Event | Publisher | Handler | Idempotency |
|---|---|---|---|
| `core.project.activated.v1` / `.closed.v1` | core | `UsageCounterProjector` recomputes `active_projects`; `SearchIndexer` marks project status | `processed_events`; recount is a set (not an increment) |
| `core.project.created.v1` | core | Seed project-level defaults: creator becomes project member `project_manager`; default approval policies are tenant-level and need no seeding | unique(project\_id, user\_id) upsert |
| `core.work_package.*`, `core.party.*`, `core.material.*`, `core.cost_code.*` | core | `SearchIndexer` upserts or archives the entity in `search_index` | upsert by `(entity_type, entity_id)` with `source_version`; older versions ignored |
| `procurement.*`, `contracts.*`, `cost.alert.raised`, `docs.*`, `planning.daily_report.submitted` (index events) | owning modules | `SearchIndexer` (record numbers, titles, parties) | same |
| `cost.alert.raised.v1`, `sourcing.recommendation.ready.v1`, `contracts.guarantee.expiring.v1`, `docs.rfi.overdue.v1`, `docs.permit.expiring.v1`, `docs.incident.reported.v1`, `sourcing.source.failed.v1`, `cash.shortfall.detected.v1`, `planning.constraint.blocked.v1`, `docs.rfi.raised.v1` | modules | `NotificationEventBridge` maps event to a notification intent per section 11 and the module file's template list | intent id = `eventId` + template code; unique(tenant\_id, intent\_id, recipient\_id) |
| Approval events (own) | platform | `ApprovalTaskProjector` creates and completes tasks; `NotificationEventBridge` notifies | `tasks.source_key = approval:{stepId}:{userId}` |
| `platform.user.deactivated.v1` (own) | platform | Re-resolve open approval steps (BR-PLT-53); reassign open tasks to the manager if set, else leave and flag | step re-resolution is a pure function of current state |

The platform also exposes the registry table `platform.approval_record_types.owner_event_consumer`: each owning module consumes `platform.approval.approved.v1/rejected.v1/cancelled.v1` filtered by `recordType` and changes its own record status (BR-PLT-57). Each owning module's handler must be idempotent on `approvalId`.

### 8.3 Proposed registry additions

| Event | Why | Consumers |
|---|---|---|
| `platform.tenant.suspended.v1` / `.reactivated.v1` | Modules pause external collectors and portals | sourcing, portals, notifications |
| `platform.import.committed.v1` | Search reindex and module caches after a bulk import (`importType, batchId, entityType, count`) | search, owning module |
| `platform.role.changed.v1` (`userIds[]`, `permissionsVersion`) | Portals and caches invalidate effective permissions | portals, reporting |
| `platform.export.ready.v1` | Notify requester and second approver | notifications |

---

## 9. Background jobs and schedules

Cron is UTC. `per tenant` jobs run once per active tenant per window with the tenant context (BR-PLT-141). All jobs record `job_runs` and are idempotent through `run_key` (BR-PLT-140). Time windows in Cairo local time are noted for user-facing jobs (Cairo is UTC+2, UTC+3 in summer; the scheduler converts using the tenant `timezone` setting for digests).

| Job code | Schedule | Scope | What | Idempotency |
|---|---|---|---|---|
| `platform_outbox_dispatch` | continuous worker, poll 500 ms | global | Fan out `pending` outbox rows, dispatch deliveries serial per aggregate | row state machine; `processed_events` on the consumer |
| `platform_outbox_retention` | `0 2 * * *` | global | Delete `completed` outbox older than 90 days; archive `outbox_dead` resolved older than 1 year | date predicate |
| `platform_escalation_scan` | `*/15 * * * *` | per tenant | Active steps past `due_at + after_hours`: escalate | `run_key` per 15-min window; escalation unique(step\_id, level) |
| `platform_approval_reminder` | `*/30 * * * *` | per tenant | Reminders to approvers (max 3 per step) | `last_reminder_at` guard |
| `platform_delegation_expiry` | `5 0 * * *` | per tenant | Mark ended delegations `expired`; re-resolve nothing | status predicate |
| `platform_notification_dispatch` | continuous worker | global | Send queued deliveries, retries 1 m / 10 m / 1 h, release quiet-hour holds | `status`, `next_attempt_at` |
| `platform_digest_daily` | `0 4 * * *` (about 07:00 Cairo, tenant-schedulable) | per tenant | Build role digests: approvals pending, overdue tasks, alerts | `digest_group` + date key |
| `platform_task_reminders` | `0 5 * * *` | per tenant | Remind tasks due today or overdue; release snoozed tasks at `snoozed_until` | `source_key` dedupe on reminder notifications |
| `platform_file_scan` | event-triggered by `uploaded` | global | Antivirus and magic-byte check, variants | file status machine |
| `platform_file_purge` | `0 3 * * *` | global | Remove `deleted` files after 30 days, abandoned `pending_upload` after 24 h | status and age predicate |
| `platform_search_reindex` | manual; nightly delta `30 1 * * *` | per tenant | Rebuild entities missed by events (compare `source_version`) | upsert |
| `platform_usage_recount` | `0 1 * * *` | per tenant | Verify counters against truth; fix drift and alert | set, not increment |
| `platform_audit_chain_verify` | `0 3 * * 0` | per tenant | Verify audit hash chain for the last week | read-only; alert on break |
| `platform_idempotency_purge` | `*/30 * * * *` | global | Delete idempotency keys older than 24 h | age predicate |
| `platform_token_cleanup` | `0 3 * * *` | global | Delete expired refresh tokens, user tokens, invitations older than 30 days past expiry | age predicate |
| `platform_trial_expiry` | `10 0 * * *` | global | Suspend trials past `trial_ends_at` (BR-PLT-86) | status predicate |
| `platform_stuck_run_detector` | `*/5 * * * *` | global | Mark `running` jobs past timeout, requeue (BR-PLT-142) | `attempts` |
| `platform_tenant_export_build` | trigger on approval | per tenant | Build export zip, checksum, encrypt, notify | `export_id` unique run |
| `platform_tenant_deletion_execute` | `0 22 * * *` | per tenant | Execute deletions whose cooling off ended | status `cooling_off` -> `executing` with advisory lock |
| `platform_number_gap_check` | `0 4 * * 1` | per tenant | Report gaps in allocations versus used numbers (expected none; voided numbers are allocated, not missing) | read-only |
| `platform_comment_digest` | folded into `platform_digest_daily` | per tenant | Unread mentions summary |  |

---

## 10. Permissions

### 10.1 Role key

OE `owner_executive`, PM `project_manager`, SU `superintendent`, FM `foreman`, PO `procurement_officer`, FC `finance_controller`, QS `cost_qs_engineer`, CM `contracts_manager`, DC `document_controller`, QA `qa_qc_engineer`, SO `safety_officer`, ES `estimator`, TA `tenant_admin`. External roles have no row in `role_permissions`; they use portal grants (`portal.*`). "All" = every internal role. Flags: **L** amount-limited, **P** project-scoped, **S** sensitive, **X** `min_plan` above `starter`. The seeded `role_permissions` for system roles follow this table and the v3 matrix (section 5.2); tenants may add grants to custom roles only (BR-PLT-14).

### 10.2 Platform permissions

| Code | Roles | Notes |
|---|---|---|
| `platform.tenant.manage` | TA | Profile and settings |
| `platform.tenant.view_settings` | TA, OE, PM, FC |  |
| `platform.tenant.delete` | TA | S; also needs operator action |
| `platform.export.request` | TA, OE | S |
| `platform.export.approve` | TA, OE | S; must differ from the requester |
| `platform.user.view` | TA, OE, PM |  |
| `platform.user.manage` | TA | S for role assignment of `tenant_admin` |
| `platform.role.view` | TA, OE |  |
| `platform.role.manage` | TA | S, X (custom roles on Growth+) |
| `platform.project.view_all` | TA, OE | Reads all projects regardless of membership; never writes |
| `platform.project_member.manage` | TA, PM (P, own projects) |  |
| `platform.visibility.manage` | TA | S (showing costs to site roles) |
| `platform.approval_policy.view` | TA, OE, PM, FC |  |
| `platform.approval_policy.manage` | TA | Edit limits of system templates |
| `platform.approval_policy.manage_custom` | TA | X (Growth+) |
| `platform.approval.view_all` | TA, OE | Admin view of instances |
| `platform.approval.decide` | PM, FC, OE, QS, CM, SU, PO (L, P) | Necessary, not sufficient: the record type's own approve code also applies |
| `platform.approval.cancel_any` | TA, OE |  |
| `platform.approval.reassign` | TA |  |
| `platform.approval.delegate` | PM, FC, OE, CM, QS, PO | Own delegations only |
| `platform.approval.manage` | TA | Revoke others' delegations |
| `platform.outbox.view` / `.manage` | TA (view), operator (manage) | `manage` is operator plus TA for own tenant dead letters of non-financial events |
| `platform.audit.view` | TA, OE, FC | S for exports of audit |
| `platform.audit.view_record` | PM, FC, CM, QS, DC (P) | Needs read right on the record |
| `platform.audit.verify` | TA |  |
| `platform.file.view` | All | Needs read on linked record |
| `platform.file.upload` | All |  |
| `platform.file.manage` | TA, DC | Delete others' files |
| `platform.notification.manage_templates` | TA | X |
| `platform.task.view_all` | TA, PM (P) |  |
| `platform.task.create` | All except FM | FM may create tasks only from the mobile snag flow (`planning`) |
| `platform.task.manage` | TA, PM (P) |  |
| `platform.comment.create` | All |  |
| `platform.comment.moderate` | TA, PM (P) |  |
| `platform.number_series.view` / `.manage` | TA, FC (view) / TA | S for manage |
| `platform.import.view` | All with some import right |  |
| `platform.search.manage` | TA |  |
| `platform.job.view` / `.run` / `.manage` | TA | `run` limited to jobs flagged manual |
| `platform.view.share` | TA, PM | Share saved views |
| `platform.localization.view` / `.manage` | TA |  |
| `platform.plan.view` | TA, OE |  |

### 10.3 Field permissions

| Code | Default roles | Meaning |
|---|---|---|
| `field.cost_amounts.view` | OE, PM, PO (own buys), FC, QS, CM, ES, TA | Unit costs, totals, budgets, margins, rates, supplier prices. Hidden (omitted) from SU and FM unless `project_members.can_see_costs` or a project `show` policy (BR-PLT-17) |
| `field.margin.view` | OE, PM, FC, ES | Margin and markup only |
| `field.party_bank.view` | FC, PM (view masked), TA | Bank account detail; masked to last 4 digits otherwise |
| `field.personal_data.view` | TA, PM | National id, phone of workers and parties |

### 10.4 Master permission catalogue of the other modules

Codes are listed per module. Module files carry their own section 10 with notes; the seed migration builds `platform.permissions` from the union of all files and a **drift test** (PLT-032) fails the build if a code is used in an endpoint attribute but missing here, or listed here but unused. Where a module file already fixes a code it appears below as written; codes marked "(proposed)" are derived from the v3 matrix and must be confirmed by the module owner.

| Module | Codes | Roles (summary) |
|---|---|---|
| core | `core.company.manage`, `core.unit.manage`, `core.tax_code.manage`, `core.exchange_rate.manage` | TA; `core.exchange_rate.manage` also FC |
| core | `core.project.view`, `core.project.create`, `core.project.update`, `core.project.activate`, `core.project.close` (S), `core.project.reopen` (S) | view: all in project (P); create/update: TA, OE, PM; activate: TA, PM; close/reopen: OE, PM |
| core | `core.zone.manage`, `core.work_package.view`, `core.work_package.manage`, `core.cost_code.view`, `core.cost_code.manage`, `core.trade_template.manage` | view: all (P); manage: PM, QS, TA |
| core | `core.party.view`, `core.party.create`, `core.party.update`, `core.party.block`, `core.party.merge`, `core.party.bank_view` (field), `core.party.bank_account_verified` (S) | view: all except FM; create/update: PO, FC, QS, CM; block/merge: FC, PM; bank verify: FC only |
| core | `core.material.view`, `core.material.manage`, `core.material.merge` | view: all; manage: PO, QS, ES, TA |
| core | `core.cost_visibility.view` | Core's per-project cost-visibility toggle; OE, PM, TA |
| estimating | `estimating.tender.read`, `.create`, `.update`, `.submit`, `.submit_late` (S) | ES, PM, OE |
| estimating | `estimating.estimate.create`, `.update`, `.finalize`, `.approve` (L) | ES create/update/finalize; PM, OE approve |
| estimating | `estimating.check.waive` (S), `estimating.decision.record`, `estimating.outcome.record`, `estimating.margin.view`, `estimating.rate.manage` | OE, PM; ES for `rate.manage` |
| planning | `planning.schedule.view`, `.manage`, `.baseline` (L-free), `planning.activity.view_own`, `planning.progress.record`, `.verify`, `planning.site_request.create`, `.confirm`, `planning.daily_report.submit`, `planning.constraint.manage` (proposed) | FM record/create; SU verify/confirm; PM baseline/manage |
| procurement | `procurement.purchase_request.view`, `.create`, `.approve` (L), `procurement.rfq.manage`, `procurement.quote.capture`, `procurement.quote.capture_late`, `procurement.purchase_order.view`, `.create`, `.issue` (L), `.approve` (L), `.cancel`, `procurement.goods_receipt.create`, `.accept_over`, `.reverse`, `procurement.invoice.register`, `.match`, `.approve` (L), `procurement.payment.schedule`, `.approve` (L), `.execute`, `.confirm_new_bank_account` (S), `procurement.supplier.view`, `.rate`, `.override_status` (S), `procurement.price.view` (field), `procurement.budget.override` (S) | PO create/issue within limit; PM/FC/OE approve by limit; FM/SU goods receipt create; FC payments and bank confirm |
| sourcing | `sourcing.request.create`, `sourcing.offer.view`, `sourcing.recommendation.view`, `.decide` (L), `sourcing.source.manage`, `sourcing.price.view` | PO, PM; `source.manage` TA, PO |
| cost | `cost.budget.view`, `cost.budget.view_amounts` (field), `cost.budget.create`, `.approve` (L), `cost.budget_transfer.create`, `.approve` (L), `cost.entry.view`, `cost.entry.post_manual` (S), `cost.forecast.update`, `cost.period.close` (S), `cost.period.reopen` (S), `cost.alert.view`, `cost.alert.configure`, `cost.project_settings.manage`, `cost.project_settings.budget_check_level` (S) | QS create/prepare; PM approve; FC period close and transfers; OE reopen |
| contracts | `contracts.contract.view`, `.create`, `.sign` (S), `contracts.boq.manage`, `contracts.measurement.manage`, `.verify`, `contracts.variation.view`, `.create`, `.submit`, `.approve` (L), `contracts.claim.manage`, `contracts.payment_application.view`, `.prepare`, `.submit`, `.approve` (L), `.waive_document` (S), `contracts.invoice.issue`, `contracts.receipt.record`, `contracts.retention.release` (L), `contracts.guarantee.manage` | CM contracts, variations, claims; QS prepares applications; PM approves; FC invoices and receipts |
| subcontracts (R2, proposed) | `subcontracts.subcontract.view`, `.create`, `.sign` (S), `subcontracts.valuation.measure`, `.prepare`, `.certify` (L), `subcontracts.payment.schedule`, `.approve` (L), `subcontracts.backcharge.raise`, `.approve` | SU measure; QS prepare; PM certify; FC pay |
| cash (proposed) | `cash.forecast.view`, `cash.forecast.manage`, `cash.schedule.view`, `cash.shortfall.acknowledge` | FC manage; PM, OE view |
| docs (proposed) | `docs.document.view`, `.create`, `.approve`, `docs.rfi.create`, `.answer`, `docs.inspection.request`, `.record`, `docs.ncr.raise`, `.close`, `docs.incident.report`, `.manage`, `docs.permit.manage` | DC documents; QA inspections/NCR; SO safety; FM/SU create requests |
| closeout (R3, proposed) | `closeout.punch_item.raise`, `.close`, `closeout.handover.manage`, `closeout.lesson.publish` | QA, PM |
| intelligence (R2, proposed) | `intelligence.suggestion.view`, `.review`, `intelligence.copilot.use` | per underlying record permission |
| reporting (proposed) | `reporting.dashboard.view`, `reporting.report.view`, `.export`, `reporting.report.build` (X, R2), `reporting.report.schedule` (X, R2) | OE, PM, FC, CM, QS by scope |
| portal | `portal.rfq.respond`, `portal.po.confirm`, `portal.delivery.schedule`, `portal.invoice.upload`, `portal.application.review`, `portal.variation.approve`, `portal.progress.view`, `portal.claim.submit` | External roles only; scoped to records shared with the party (BR-X-07) |

**Amount limits.** Codes marked L are checked against `role_limits` and `project_members.limits` per record type (BR-PLT-19) in addition to the grant. **Project scope** applies to every code with `is_project_scoped=true`. **Field visibility**: the field groups in `platform.field_groups` (`cost_amounts`, `margin`, `party_bank`, `personal_data`) are applied by `IFieldVisibility` to responses, exports, search snippets, notifications and audit views; hidden fields are omitted, never nulled (BR-X-06). FM and SU never receive: unit prices, line amounts, budgets, committed/actual cost, supplier prices, margins, retention values, payment amounts; they do receive quantities, dates, statuses and photos.

---

## 11. Notifications

Template codes `PLT.<AREA>.<EVENT>`; each has `ar` and `en` bodies and channels. Mandatory = cannot be turned off (BR-PLT-110). Module files define their own templates. Recipients are resolved from approval steps, task assignees, or user ids in the intent.

| Template code | Recipients | Trigger | Channel | Mandatory | Arabic sample |
|---|---|---|---|---|---|
| `PLT.APPROVAL.REQUESTED` | Resolved approvers of the active step | `approval.requested`, `step_approved` (next step) | in\_app, push, email | yes | `طلب موافقة: {{recordNumber}} بقيمة {{amount}} {{currency}} من {{requesterName}}` |
| `PLT.APPROVAL.REMINDER` | Approvers who have not decided | Reminder job | in\_app, push | no | `تذكير: {{recordNumber}} بانتظار موافقتك منذ {{hours}} ساعة` |
| `PLT.APPROVAL.ESCALATED` | New approver; requester (notify) | `approval.escalated` | in\_app, push, email | yes | `تم تصعيد {{recordNumber}} إليك لتأخر الموافقة` |
| `PLT.APPROVAL.APPROVED` | Requester, record owner | `approval.approved` | in\_app, push | no | `تمت الموافقة على {{recordNumber}}` |
| `PLT.APPROVAL.REJECTED` | Requester | `approval.rejected` | in\_app, push, email | yes | `تم رفض {{recordNumber}}: {{comment}}` |
| `PLT.APPROVAL.RETURNED` | Requester | rejected with `returned=true` | in\_app, push | yes | `أُعيد {{recordNumber}} للتعديل: {{comment}}` |
| `PLT.APPROVAL.DELEGATED` | Delegate; delegator | Delegation created | in\_app, email | no | `فوّضك {{delegatorName}} للموافقة من {{from}} إلى {{to}}` |
| `PLT.TASK.ASSIGNED` | Assignee | Task created by someone else | in\_app, push | no | `مهمة جديدة: {{title}}، الاستحقاق {{dueDate}}` |
| `PLT.TASK.DUE` | Assignee | Reminder job | in\_app, push | no | `مهمة مستحقة اليوم: {{title}}` |
| `PLT.COMMENT.NEW` | Mentioned users, watchers | Comment added | in\_app, push | no | `{{authorName}} ذكرك في {{recordNumber}}` |
| `PLT.USER.INVITED` | Invitee | Invitation | email | yes | `دعوة للانضمام إلى {{companyName}} على كونستركشن كونكت` |
| `PLT.SECURITY.LOCKED` | User; TA | 5 failed logins | email, in\_app | yes | `تم قفل حسابك مؤقتا بعد محاولات دخول فاشلة` |
| `PLT.SECURITY.NEW_DEVICE` | User | Login from new device | email | yes | `تسجيل دخول جديد من جهاز غير معروف` |
| `PLT.SECURITY.SENSITIVE_GRANT` | All TA | Sensitive permission granted | in\_app, email | yes | `تم منح صلاحية حساسة {{permission}} إلى {{userName}}` |
| `PLT.FILE.QUARANTINED` | Uploader; TA | Scan infected | in\_app, email | yes | `تم عزل الملف {{fileName}} لاحتمال وجود فيروس` |
| `PLT.IMPORT.DONE` | Importer | Commit finished or failed | in\_app | no | `اكتمل الاستيراد: {{ok}} صف ناجح و{{errors}} بأخطاء` |
| `PLT.EXPORT.APPROVAL_NEEDED` | Second approver | Export requested | in\_app, email | yes | `طلب تصدير بيانات الشركة بانتظار موافقتك` |
| `PLT.EXPORT.READY` | Requester | Export built | email | yes | `ملف تصدير البيانات جاهز للتحميل لمدة 7 أيام` |
| `PLT.PLAN.LIMIT_NEAR` | TA | Usage at 80% of seats, projects or storage | in\_app, email | no | `اقتربت من حد الباقة: {{metric}} {{percent}}%` |
| `PLT.JOB.FAILED` | TA | Tenant job failed after max attempts | in\_app | no | `فشل تنفيذ المهمة {{jobName}}` |
| `PLT.DIGEST.DAILY` | Per role | Digest job | email, push | no | `ملخصك اليومي: {{approvals}} موافقات و{{tasks}} مهام` |

Rules: quiet hours default 21\:00-07\:00 tenant local, bypassed by `priority=high` (BR-PLT-112); WhatsApp is off until consent and the channel decision (Open question 3); amounts and rates are removed from notification params for users without `field.cost_amounts.view` (the template falls back to the variant without amount).

---

## 12. Error codes

Prefix `platform.`; all have Arabic and English text in the catalogue (BR-PLT-152).

| Code | HTTP | When | Arabic message | English message |
|---|---|---|---|---|
| `platform.invalid_credentials` | 401 | Wrong email or password | بيانات الدخول غير صحيحة | Invalid email or password |
| `platform.account_locked` | 423 | Locked after failures | الحساب مقفل مؤقتا، حاول بعد \{minutes\} دقيقة | Account locked, retry in \{minutes\} minutes |
| `platform.mfa_required` | 401 | MFA code missing | مطلوب رمز التحقق | Verification code required |
| `platform.mfa_code_invalid` | 401 | Wrong TOTP | رمز التحقق غير صحيح | Invalid verification code |
| `platform.refresh_token_reused` | 401 | Reuse detected, family revoked | انتهت الجلسة، سجل الدخول مجددا | Session ended, sign in again |
| `platform.token_invalid` | 400 | Invitation or reset token invalid or expired | الرابط غير صالح أو منتهي | Link invalid or expired |
| `platform.password_policy` | 422 | Password too weak | كلمة المرور لا تستوفي الشروط | Password does not meet policy |
| `platform.forbidden` | 403 | Missing permission | لا تملك صلاحية لهذا الإجراء | You do not have permission |
| `platform.tenant_suspended` | 403 | Tenant suspended | الحساب موقوف، تواصل مع الدعم | Account suspended, contact support |
| `platform.tenant_invalid_transition` | 409 | Lifecycle action not allowed | لا يمكن تنفيذ الإجراء في الحالة الحالية | Action not allowed in current state |
| `platform.tenant_code_taken` | 409 | Duplicate tenant code | رمز الشركة مستخدم | Tenant code already used |
| `platform.feature_not_in_plan` | 403 | Feature above plan | الميزة غير متاحة في باقتك | Feature not in your plan |
| `platform.user_limit_reached` | 422 | Seat limit | بلغت الحد الأقصى للمستخدمين (\{max\}) | User limit reached (\{max\}) |
| `platform.project_limit_reached` | 422 | Active project limit (raised by core) | بلغت الحد الأقصى للمشاريع النشطة | Active project limit reached |
| `platform.storage_limit_reached` | 422 | Storage 100% | تم استهلاك مساحة التخزين | Storage limit reached |
| `platform.email_taken` | 409 | Duplicate email in tenant | البريد مستخدم | Email already in use |
| `platform.last_tenant_admin` | 422 | Removing last admin | لا يمكن إزالة آخر مدير للنظام | Cannot remove the last administrator |
| `platform.role_system_immutable` | 422 | Edit system role | الدور النظامي لا يمكن تعديله | System role cannot be changed |
| `platform.role_in_use` | 409 | Delete role with users | الدور مستخدم | Role is in use |
| `platform.permission_plan_blocked` | 422 | Grant above plan | الصلاحية غير متاحة في الباقة | Permission not available in plan |
| `platform.sensitive_reason_required` | 422 | No reason on sensitive grant | السبب مطلوب | A reason is required |
| `platform.visibility_reason_required` | 422 | Show policy without reason | السبب مطلوب لإظهار التكاليف | Reason required to show costs |
| `platform.member_exists` | 409 | Duplicate project member | العضو مضاف مسبقا | Already a project member |
| `platform.policy_overlap` | 422 | Overlapping amount ranges | نطاقات المبالغ متداخلة | Policy amount ranges overlap |
| `platform.policy_not_found` | 404 | No policy matches | لا توجد سياسة موافقة مطابقة | No matching approval policy |
| `platform.approval_limit_exceeded` | 422 | Approver limit too low | المبلغ يتجاوز حد صلاحيتك | Amount exceeds your limit |
| `platform.approval_no_eligible_approver` | 422 | No approver with enough limit | لا يوجد معتمد بصلاحية كافية | No approver with sufficient limit |
| `platform.approval_self_approval` | 403 | Own request | لا يمكنك اعتماد طلبك | You cannot approve your own request |
| `platform.approval_already_decided` | 409 | Second decision | تم اتخاذ قرارك مسبقا | You already decided |
| `platform.approval_not_active_step` | 409 | Step not active | الخطوة ليست نشطة | Step is not active |
| `platform.approval_not_pending` | 409 | Instance closed | الطلب لم يعد معلقا | Approval is not pending |
| `platform.approval_comment_required` | 422 | Reject without comment | التعليق مطلوب (10 أحرف على الأقل) | Comment required (min 10 characters) |
| `platform.approval_already_open` | 409 | Open instance exists | يوجد طلب موافقة مفتوح | An approval is already open |
| `platform.delegation_invalid_period` | 422 | Bad dates | فترة التفويض غير صحيحة | Invalid delegation period |
| `platform.delegation_self` | 422 | Delegate to self | لا يمكن التفويض لنفسك | Cannot delegate to yourself |
| `platform.dead_letter_resolved` | 409 | Replay of resolved item | تمت معالجة العنصر | Item already resolved |
| `platform.file_type_blocked` | 422 | Type not allowed | نوع الملف غير مسموح | File type not allowed |
| `platform.file_too_large` | 422 | Over size limit | حجم الملف كبير | File too large |
| `platform.upload_incomplete` | 422 | Size mismatch | الرفع غير مكتمل | Upload incomplete |
| `platform.file_quarantined` | 403 | Download infected | الملف معزول | File is quarantined |
| `platform.file_not_available` | 409 | Still scanning | الملف قيد الفحص | File is still being scanned |
| `platform.file_legal_hold` | 409 | Delete under hold | الملف محجوز قانونيا | File under legal hold |
| `platform.preference_mandatory` | 422 | Disable mandatory category | هذا الإشعار إلزامي | This notification is mandatory |
| `platform.template_param_missing` | 422 | Missing placeholder | متغير مفقود في القالب | Template parameter missing |
| `platform.task_not_open` | 409 | Action on closed task | المهمة مغلقة | Task is closed |
| `platform.comment_body_empty` | 422 | Empty | التعليق فارغ | Comment is empty |
| `platform.mention_not_visible` | 422 | Mentioned user cannot see record | المستخدم لا يملك صلاحية رؤية السجل | Mentioned user cannot see the record |
| `platform.comment_edit_window_closed` | 422 | After 15 min | انتهت مهلة التعديل | Edit window closed |
| `platform.series_start_conflict` | 409 | Start below issued | بداية الترقيم أقل من آخر رقم | Start below last issued number |
| `platform.import_type_unknown` | 404 | Bad type | نوع الاستيراد غير معروف | Unknown import type |
| `platform.import_too_many_rows` | 422 | Over row limit | عدد الصفوف يتجاوز الحد | Too many rows |
| `platform.import_has_errors` | 422 | All-or-nothing with errors | يوجد أخطاء تمنع الاستيراد | Errors block import |
| `platform.search_query_too_short` | 422 | Under 2 chars | كلمة البحث قصيرة | Search term too short |
| `platform.job_not_manual` | 422 | Run non-manual job | لا يمكن تشغيل هذه المهمة يدويا | Job cannot be run manually |
| `platform.job_not_schedulable` | 422 | Cron override not allowed | لا يمكن تغيير جدول المهمة | Schedule cannot be changed |
| `platform.export_not_allowed` | 403 | Export disabled | التصدير غير مسموح | Export not allowed |
| `platform.export_self_approval` | 403 | Requester approves | لا يمكنك الموافقة على طلبك | Requester cannot approve |
| `platform.export_expired` | 410 | Link expired | انتهت صلاحية الرابط | Download link expired |
| `platform.deletion_export_required` | 422 | No downloaded export | يجب تحميل نسخة البيانات أولا | Download your export first |
| `platform.setting_unknown` / `platform.setting_invalid` | 422 | Bad setting key/value | إعداد غير معروف / قيمة غير صحيحة | Unknown setting / invalid value |
| `platform.message_param_mismatch` | 422 | Override changes placeholders | المتغيرات لا تطابق الأصل | Placeholders differ from original |
| `platform.idempotency_key_required` | 400 | Missing on mobile/portal | مفتاح التكرار مطلوب | Idempotency key required |
| `platform.idempotency_key_reuse` | 422 | Same key, different body | مفتاح التكرار مستخدم لطلب آخر | Idempotency key reused |
| `platform.request_in_progress` | 409 | Concurrent replay | الطلب قيد التنفيذ | Request in progress |
| `platform.precondition_required` | 428 | Missing `If-Match` | رقم الإصدار مطلوب | Version header required |
| `platform.precondition_failed` | 412 | Stale version | تم تعديل السجل، حدّث الصفحة | Record changed, reload |
| `platform.rate_limited` | 429 | Over limits | طلبات كثيرة، حاول لاحقا | Too many requests |
| `platform.validation_failed` | 400/422 | Generic field errors in `errors[]` | بيانات غير صحيحة | Validation failed |
| `platform.not_found` | 404 | Not found or not visible (BR-PLT-12) | غير موجود | Not found |

---

## 13. Reports and read models

Platform read models serve the approval backlog and exception views of the R1 dashboard (v3 reporting) and operations. Standard columns apply (`tenant_id`, `created_at`, `updated_at`); RLS on all.

#### `platform.rm_approval_backlog` (one row per open approval step)

| column | type | null | notes |
| --- | --- | --- | --- |
| step\_id | uuid | no | PK |
| instance\_id | uuid | no |  |
| record\_type / record\_id / record\_number | text / uuid / text | no |  |
| project\_id | uuid | yes |  |
| amount / currency | numeric(18,4) / char(3) | yes | `amount` hidden from users without `field.cost_amounts.view` (the API omits it) |
| step\_no | int | no |  |
| approver\_ids | uuid\[\] | no | GIN index |
| requested\_by | uuid | no |  |
| requested\_at / due\_at | timestamptz | no / yes |  |
| age\_hours | numeric(10,2) | no | refreshed by job |
| is\_overdue / is\_escalated | bool | no |  |
| Refresh: maintained by `ApprovalBacklogProjector` on approval events (upsert/delete); `age_hours` recomputed every 15 minutes by `platform_escalation_scan`. Index: (tenant\_id, project\_id, is\_overdue), GIN(approver\_ids). |  |  |  |

#### `platform.rm_approval_stats_daily` (per tenant, day, record type)

Columns: `day date, record_type, requested int, approved int, rejected int, returned int, cancelled int, auto_approved int, avg_decision_hours numeric(10,2), p90_decision_hours numeric(10,2), escalated int`. Refresh: nightly job `platform_approval_stats` (`20 1 * * *`) recomputes the last 3 days (idempotent upsert).

#### `platform.rm_task_summary` (per user)

Columns: `user_id, open_count, overdue_count, due_today_count, oldest_open_at`. Refresh: projector on task events; verified nightly.

#### `platform.rm_notification_health` (per tenant, day, channel)

Columns: `day, channel, sent, delivered, failed, suppressed, avg_latency_seconds`. Refresh: nightly; used by operations and the `PLT.JOB.FAILED` alert.

#### `platform.rm_usage_snapshot` (daily)

Columns: `day, active_users, invited_users, active_projects, storage_bytes, files_count, api_calls`. Refresh: `platform_usage_recount` writes a row per day; supports plan-limit warnings and billing.

**Reports (list and export, xlsx/pdf/csv; queued above 5,000 rows):**

| Report | Columns | Permission |
|---|---|---|
| Approval backlog | record number, type, project, requester, amount (if visible), step, approver names, age hours, overdue | `platform.approval.view_all` or own |
| Approval turnaround | record type, count, average and p90 hours, escalated %, rejected % per month | `platform.approval.view_all` |
| User and access register | user, status, roles, projects, last login, MFA on, limits | `platform.user.view` |
| Permission matrix | role x permission with flags | `platform.role.view` |
| Audit extract | at, user, entity, id, action, before, after, reason | `platform.audit.view` (export is audited) |
| Outbox health | lag, dead letters open, oldest, by event type | `platform.outbox.view` |
| Import history | batch, type, user, rows, errors, committed at | `platform.import.view` |
| Number series register | series, format, last number, allocations in period, voided numbers | `platform.number_series.view` |

---

---

## 15. Test scenarios, non-functional notes, security, open questions

### 15.1 Named scenarios

| # | Scenario | Inputs | Expected |
|---|---|---|---|
| S1 | Tenant isolation | Tenants A and B each with 3 projects, 10 POs; user of A calls every list endpoint, direct GET of a B id, and a crafted query with EF filters disabled | Zero B ids returned; direct GET gives `404`; RLS returns zero rows even without EF filters |
| S2 | Approval chain by amount | Policies PO-STD-100K \[0, 100,000) one step PM; PO-MID-500K \[100,000, 500,000) step 1 PM, step 2 finance controller; PO of 183,000 EGP (cement example, offer A effective 179,340 plus freight) | PO-MID-500K selected; step 2 activates only after step 1; final `approved` event once; PM with limit 100,000 cannot decide step 1 alone when `respect_limit` (`approval_limit_exceeded`) unless his member limit is 250,000 |
| S3 | Boundary amounts | 99,999.9999 and 100,000.0000 | Former uses STD policy, latter MID (upper bound exclusive) |
| S4 | Segregation of duties | Requester is also resolved approver, tenant default | `403 approval_self_approval`; with setting on and flag on record type: approved with `self_approved=true` and flagged in audit |
| S5 | Delegation with cap | Delegator limit 500,000, delegation `max_amount` 200,000; amounts 150,000 and 183,000 and 250,000 | 150,000 and 183,000 allowed; 250,000 `approval_limit_exceeded` (min of limits, BR-PLT-49) |
| S6 | Escalation once | Step due 48 h ago, rule after 24 h, level 1 target finance controller; scan runs 3 times | One escalation row, one event, target in `resolved_approver_ids`, `escalation_level=1` |
| S7 | Outbox ordering and replay | Events E1, E2 of one aggregate; E1 handler fails twice then succeeds | E2 delivered after E1; consumer side effects exactly once (`processed_events`); lag metric returns to 0 |
| S8 | Dead letter | Handler always fails | After 10 s, 1 m, 5 m, 30 m, 2 h attempts a dead letter appears; replay after fix completes; discard needs note |
| S9 | Gapless numbering | 50 parallel transactions each allocate `PO` and 10 roll back | Used numbers are contiguous, rolled-back numbers are reissued; `number_allocations` has no gaps |
| S10 | Field visibility golden | Plumbing work package BAC 1,200,000, AC 540,000, 40% complete; foreman, superintendent, PM read the trade page and export it | PM sees amounts; foreman and superintendent receive quantities and percent complete but no BAC/AC/CPI/EAC fields (omitted); same in xlsx, search snippet, notification params and audit view |
| S11 | Site role with cost grant | Superintendent with `can_see_costs=true` on project P only | Amounts visible on P, hidden on Q |
| S12 | Idempotent goods receipt | Mobile posts receipt of 60 t with key K twice, then K with 55 t | First creates; second replays with `Idempotent-Replay: true`; third `422 idempotency_key_reuse` |
| S13 | Concurrent edit | Two users PATCH the same PO with the same ETag | One succeeds; the other `412` with the current version |
| S14 | File safety | Upload EICAR, a `.exe` renamed `.pdf`, a 30 MB photo over the limit | Quarantine, quarantine, `file_too_large` |
| S15 | External file visibility | Supplier portal user downloads a delivery note `shared_external` and an `internal` price comparison | First allowed; second `404` |
| S16 | Import | Materials workbook, 412 rows, 7 bad (unknown unit, duplicate code, negative price), Arabic headers | Preview: 405 valid, 7 errors with Arabic messages and row numbers; `skip_errors` commits 405; rerun of the commit does not duplicate |
| S17 | Arabic search | Party `شركة الألفي للتوريدات`; queries `الالفي`, `الألفى`, `ALFY` (alias) | First two found; alias found when synonym registered |
| S18 | Quiet hours | Normal notification at 23:00 Cairo, high priority at 23:05 | Normal suppressed until 07:00; high sent immediately |
| S19 | Plan limits | Starter with 10 users, 3 projects | 11th invite blocked; 4th project activation blocked; downgrade from Growth with 5 projects refused |
| S20 | Export and deletion | Dual approval on; requester approves own request; then a second admin; deletion without download | `export_self_approval`; second admin approves; deletion request blocked until `downloaded`; after 30 days no tenant rows remain, certificate present |
| S21 | Audit integrity | 1,000 changes, then manual UPDATE of one row by a DBA | Chain verification reports first broken row id; application role cannot UPDATE |
| S22 | Hijri and working days | 2026-10-10 conversion; add 5 working days from Thursday with Fri-Sat weekend and a holiday on the following Sunday | Round-trip date equal; result skips Friday, Saturday and the holiday |

### 15.2 Non-functional notes

| Topic | Target and approach |
|---|---|
| Volumes (per large tenant, 3 years) | audit\_log 20-40 million rows; outbox 5 million (90-day retention keeps 1 million); notifications 8 million; search\_index 2-5 million; files 500,000 (storage in object store); tasks 2 million |
| Partitioning | `audit_log`, `outbox`, `notifications`, `status_history` partitioned by month on `created_at`; hash sub-partition not needed in R1; partition creation job monthly (`platform_partition_maintain`, per 00 §5) |
| Indexes | As listed per table in section 4; critical: `(tenant_id, recipient_id, read_at, created_at desc)` inbox; `(tenant_id, assignee_id, status, due_date)` tasks; GIN on `approval_steps.resolved_approver_ids`; trigram and tsvector on `search_index`; `(dispatch_status, id)` outbox partial |
| Latency (p95) | Permission resolution 5 ms cached; list of my approvals 150 ms; inbox 120 ms; global search 300 ms; file download URL 100 ms; approval decision 250 ms including outbox write |
| Throughput | Outbox dispatcher 500 events/s per node; notification dispatcher 200/s; scanning 20 files/s |
| Consistency | Approval decision, status change of instance, tasks and outbox row commit in one transaction; notifications are asynchronous |
| Availability | Dispatcher and scheduler run as multiple instances with advisory locks; a node loss delays but never duplicates work |
| Data retention | Audit 7 years (legal); outbox per BR-PLT-66; notifications 12 months then archived; idempotency 24 h; deleted files 30 days; refresh tokens until expiry plus 30 days |
| Backup | Daily full plus WAL; monthly restore drill (00 §3.8); object storage versioning |
| Observability | Metrics: outbox lag, dead letters, job failures, approval turnaround, notification failure rate, login failures; all logs carry `tenant_id`, `trace_id` |

### 15.3 Security notes

1. Passwords hashed with Argon2id; refresh tokens stored hashed; reuse detection revokes the family.
2. Signed file URLs are short-lived (5 min download, 15 min upload) and scoped to one object; file names are never used as storage keys.
3. RLS is the last line of defence; the application role has no `BYPASSRLS`, and the operator host uses a separate role with audited, tenant-scoped sessions.
4. Hidden fields are omitted at the DTO layer, in exports, search, audit view and notification params; an architecture test enumerates `[CostField]` properties.
5. Audit and approval decisions are append-only by database grants; exports and permission changes are audited with reason.
6. Sensitive permissions (period reopen, payment bank confirmation, budget override, sign contract, export) need a reason and alert admins; MFA mandatory for finance roles pending Open question 5.
7. Rate limits and lockout protect login; portal tokens use a separate audience and signing key; CSRF protection for the cookie session.
8. Personal data minimised (name, phone, email); tenant export and deletion fulfil data-subject requests (Egypt Personal Data Protection Law, to be confirmed by counsel).
9. Uploads limited by type allow-list, magic-byte check and antivirus; images are re-encoded for variants.
10. Message overrides cannot inject HTML; templates render with escaping; placeholders validated.

### 15.4 Open questions (for the business)

1. How many internal approval steps does a typical contractor need for a PO above 500,000 EGP, and should Starter tenants have any editable chain at all, or only the three templates?
2. May a small company allow a manager to approve their own request when no other approver exists (self-approval), and for which record types?
3. Which notification channel do site users actually use, push or WhatsApp? WhatsApp sending needs a provider, consent and approved templates; do you accept the extra cost and the delay to R2?
4. Should a user be able to belong to more than one company (consultants)? The spec says one tenant per user in v1; separate logins are needed for consultants today.
5. Is MFA mandatory for finance controllers and owners from day one, or optional with a reminder in R1?
6. How long must audit records be kept, and does any client or authority require a longer period than 7 years?
7. When a company leaves, must data be deleted within 30 days, or does your contract require a longer retention (for tax or disputes)?
8. Should approval limits be set per person, per role, or both (current design: both, with the person-specific limit overriding)? Do limits apply in EGP only for foreign-currency contracts?
9. Are approval working hours counted in business days only (Sunday-Thursday), and should Friday site work count for escalation?
10. Who may see costs on a given project: only roles by default, or should the project manager decide per person? Should the superintendent ever see unit prices of materials he receives?
11. Is the Hijri date an input option for users or display only? (Current design: display only, except documented fields.)
12. Which business numbers must follow a fixed legal format (tax invoices, certificates) and which can be the company's own prefix? Do any series restart each year?
13. Which file types and sizes must be accepted (drawings in DWG, large PDFs of 200 MB, site videos)?
14. Do you want a company-wide "legal hold" on files and records during disputes, and who sets it?
15. Do external users (suppliers, clients) need MFA, and is e-mail plus password acceptable for them in R1?
