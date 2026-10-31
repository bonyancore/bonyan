---
title: "07 Automated sourcing"
doc_no: "07"
section: "Supply Chain"
status: "Proposal"
source: https://app.notion.com/p/23e4cdd2129683cb8a1981dc49560e11?pvs=204
exported: 2026-10-29 (Notion MCP)
---

# 07 — Automated Sourcing (Module 4A) — Backend Specification

**Key:** `SRC` · **Schema:** `sourcing` · **Release:** R1 core (Excel price lists, automatic RFQ by link/email, normalised comparison, recommendation, officer decision), R2 (extraction, one-tap confirmation at scale, consolidation, scorecards, web adapters, full portal), R3 (market index benchmarks, trend signals). <br> **Reads with:** `00` (conventions, cited as "per 00 §x"), v3 section 4.4A (lines 590-673), `06-Procurement.md` section 4.9 (the only coupling with procurement), `02-Core...` section 4.5 (material master, validity periods), `01-Platform...` (approvals, files, import, notifications). Items tagged **\[verify\]** need a fact check before build.

---

## 1. Purpose and business context

### 1.1 The problem in plain language

A Cairo contractor needs 60 tonnes of cement on site in two days. Today the procurement officer (the person who buys materials for all projects) phones four or five suppliers, writes the prices on paper, compares them in his head, forgets that one supplier gives 30 days of credit while another wants cash, and ends up with a decision nobody can reconstruct later. Prices change daily for steel and cement, so yesterday's list is already wrong. Most Egyptian suppliers have no API: they send prices by WhatsApp, PDF or Excel.

**Automated sourcing** means the system does the legwork and the officer does the judgement. The system gathers prices from four layers, asks suppliers for fresh quotes when prices are stale, converts every offer into one comparable number (the **landed cost**: what it really costs to have the material delivered to the site, including transport and discounts, with the value of credit terms taken into account), excludes offers that cannot work (for example delivery after the date the site needs it), ranks the rest with a transparent formula and shows a **recommendation package**. The officer **approves, picks another offer with a reason, or rejects and asks for a manual RFQ** (request for quotation, طلب عروض أسعار). Approval stays human by default; automatic approval is an optional policy for small, low-risk buys.

Who benefits: the officer (minutes instead of hours per request), the project manager (every choice has stored evidence), the finance controller (credit terms are priced in, so cash and credit offers are comparable), estimators (the price book grows from real purchases), and site staff (material arrives when needed).

### 1.2 The four price layers

The assignment for this file defines four layers by **how fresh and how binding** a price is. They map to the v3 4.4A.1 layers as shown.

| Layer | Meaning | Examples of arrival | Binding for an offer? | v3 mapping | Release |
| --- | --- | --- | --- | --- | --- |
| **L1 Supplier list / API / sheet** | A price the supplier published or sent in bulk, with validity dates | Excel/CSV upload, scheduled file pull, supplier API, inbound email attachment; R2 PDF/photo extraction; R2 web adapter | Yes, while fresh (inside its validity period) | v3 layer 1 and 2 (portal updates) | R1 (Excel, file, email, manual upload, API), R2 (PDF, web, WhatsApp) |
| **L2 Recent quotes** | A price a supplier quoted for a specific enquiry, or confirmed with one tap | Replies to an automatic RFQ (portal link, email), one-tap "still valid? yes / new price" | Yes, until `valid_until` | v3 layer 3 (automatic RFQ) and layer 2 | R1 (RFQ reply), R2 (one-tap at scale) |
| **L3 Past PO prices** | What the company actually paid: PO, receipt and invoice prices stored as price observations | Events from procurement | No. **Reference only** (reasonableness check, "price vs recent purchases", budget) | v3 layer 4 (price history) | R1 |
| **L4 Market index** | An external or internal index per material category (for example a published steel index) | Scheduled API/file, manual entry | No. **Reference only** (alerts, benchmark) | v3 4.4A.8 benchmarks | R2 (manual entry), R3 (benchmarks and trend signals) |

Only L1 and L2 can become **offers** in a comparison. L3 and L4 never create an offer; they decorate it ("this price is 4.2% above your last 90-day purchase") and drive market alerts.

### 1.3 Validity periods ("daily update" rule)

Each material category has a **price-validity period** in days (steel 1, cement 2, ready-mix 2, aggregates 3, plumbing 7, paints 7, finishing 30; owned by `core.material_categories.validity_days` with an optional per-material override, BR-COR-33). A price older than its period is **stale**. A stale price is never ranked silently: the system asks the supplier for a one-tap confirmation, sends an automatic RFQ if none arrives, and blocks a stale price from a recommendation unless the officer explicitly confirms it (BR-SRC-14).

### 1.4 How it fits the master lifecycle and flow F2

```txt
Site request (planning) -> Purchase request approved (procurement, route = auto_sourcing)
   -> sourcing.request created      (this module)
   -> prices gathered (L1/L2) or RFQ sent, deadline
   -> offers normalised -> comparison engine -> recommendation
   -> officer decision (approve / choose alternative / reject)
   -> sourcing.recommendation.decided -> procurement creates PO draft (PO-1066) -> cost commits
```

This is steps 2-9 of v3 4.4A.3 and F2. Procurement (file 06 section 4.9) owns the purchase request, the PO and the budget check; this module owns prices, offers, comparison and the decision. Materials without automated sourcing (free text, unusual items) take the manual RFQ path in file 06, which uses the same arithmetic.

### 1.5 Golden example (used in tests, BR and tickets)

PR-0412, line 1: **60 t Portland cement CEM I 42.5N**, needed in 2 days, budget rate 3,000 EGP/t (budget 180,000), financing rate 2% per month.

| Supplier | Price (ex VAT) | Transport | Lead time | Payment | Landed | Credit value | Effective | Result |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| A | 3,050 x 60 = 183,000 | included | 1 day | 30 days credit | 183,000 | 3,660 | **179,340** | Recommended, rank 1 |
| B | 2,950 x 60 = 177,000 | 4,200 | 2 days | cash | 181,200 | 0 | **181,200** | Alternative, rank 2 |
| C | 2,900 x 60 = 174,000 | 3,000 | 5 days | 60 days credit | 177,000 | 7,080 | 169,920 | **Excluded: too late** |

B looks cheaper on the price column by 1,800 EGP, but A's 30-day credit is worth 3,660, so A wins by 1,860 on effective cost. The package flags that A's landed total is **3,000 EGP (1.67%) above the work-package budget**. The officer approves A; PO-1066 for **183,000** (the landed amount, not the effective amount) goes to supplier A. Effective cost is a comparison number only; the commitment is what the PO says.

### 1.6 Out of scope

Purchase orders, goods receipt, invoices, payments, supplier master (file 06); material master CRUD (file 02); the general approval engine (file 01); supplier portal shell and WhatsApp channel (file 15, R2); AI extraction models (file 14).

---

## 2. Users and roles

| Role | What they do in this module | Cost visibility |
|---|---|---|
| `procurement_officer` | Primary user. Reviews recommendations, decides (within personal limit), enters manual offers, maintains price lists, resolves unmatched supplier items, manages sources, handles exceptions. | Sees prices and landed costs (field permission `field.cost_amounts.view`). |
| `project_manager` | Sees recommendations for own projects, receives escalations, approves over-limit decisions, may decide when delegated. | Yes. |
| `finance_controller` | Reads comparison and credit-value logic, owns the financing rate and weights in the policy, approves over-limit decisions by policy. | Yes. |
| `cost_qs_engineer` | Views budget impact, price history, market alerts. Read only. | Yes. |
| `estimator` | Consumes the price book (`sourcing.price.updated`), reads price history and market indices. | Yes. |
| `tenant_admin` | Manages price sources, credentials references, health, policy, legal approval for web adapters, user routing rules. | Yes. |
| `owner_executive` | Reads savings and source-health reports. | Yes. |
| `foreman`, `superintendent` | Raise site requests in planning; **see nothing from sourcing except the status of their request** (`queued`, `collecting`, `in review`, `decided`). Prices, offers, suppliers' names and scores are **omitted** (BR-X-06; v3 matrix). | Hidden. |
| `supplier_user` (external, portal audience) | Opens an RFQ link or portal inbox, submits or declines an offer, uploads a price list, answers a one-tap confirmation. Sees only invitations addressed to their own supplier party and never sees other suppliers' offers. | Own prices only. |

Routing: the responsible officer is taken from the purchase request (`assignedOfficerId`, file 06). If the officer does not open the recommendation within the SLA the module escalates to the project manager (section 6, BR-SRC-45).

---

## 3. Services and components

| Component | Kind | Responsibility |
|---|---|---|
| `SourcingRequestAggregate` | Aggregate | One request per purchase-request line with `auto_sourcing` route; owns its state machine, collection window, invitations, exceptions. |
| `OfferAggregate` | Aggregate (child of request) | A supplier's offer for the request; raw terms plus provenance; immutable after the recommendation freezes it. |
| `RecommendationAggregate` | Aggregate | Frozen result of the engine (inputs, per-offer results, explanation, formula version); owns decision state. Immutable apart from status and decision. |
| `PriceListAggregate` | Aggregate | A supplier price list version with lines, validity, verification and activation. |
| `PriceSourceRegistry` | CRUD + health | The 46-source registry (adapter type, schedule, secret reference, health). |
| `CollectionOrchestrator` | Job service | Daily collection at 06:00 tenant time; creates runs, calls adapters through the queue `collectors`, retries, writes `source_runs`. |
| `IPriceSourceAdapter` implementations | Adapters | `ApiAdapter`, `ScheduledFileAdapter`, `EmailIngestAdapter`, `ManualUploadAdapter`, `WebScrapeAdapter` (R2, disabled by default), `MarketIndexAdapter`. Each returns rows, never writes the database. |
| `PriceRowPipeline` | Service | Parse, validate, normalise, match to supplier items and materials, upsert lines (shared by all adapters). |
| `MaterialMatcher` | Service | Alias and trigram matching of free text to `core.materials`; produces the review queue. |
| `ValidityService` | Service | Computes freshness from line dates and `IMaterialCatalog.GetValidityDays`; runs the stale sweep. |
| `SupplierSelector` | Service | Chooses eligible suppliers (approved, category, delivers to site, documents valid) via procurement contract. |
| `RfqDispatcher` | Service | Creates invitations, tokens, portal links, emails; reminders; closes the window. |
| `OfferNormaliser` | Pure calculator | Raw offer to landed cost, credit value, effective cost, feasibility (section 6). |
| `ComparisonEngine` | **Pure function** | `Compare(ComparisonInput) -> ComparisonResult`; no I/O, no clock, no database; versioned formula `src-1`. |
| `RecommendationBuilder` | Service | Gathers inputs (offers, policy, budget, reference prices, supplier scores), calls the engine, stores the frozen result, publishes the event. |
| `DecisionService` | Service | Enforces decision rules and segregation of duties; routes over-limit decisions to the platform approval engine; publishes `decided`. |
| `AutoApprovalEvaluator` | Service | Optional policy check; logs every automatic approval. |
| `ExceptionManager` | Service | Raises and resolves exceptions (fewer than two valid offers, stale price, unresponsive supplier). |
| `PriceObservationRecorder` | Consumer | Writes observations from PO, receipt, invoice and quote events. |
| `PriceBookBuilder` | Job service | Daily reference price per material (median of fresh L1/L2 prices); publishes `sourcing.price.updated.v1`. |
| `MarketAlertEvaluator` | Job service | Price spike/drop and above-index alerts. |
| `SourceHealthEvaluator` | Job service | Health status, incidents, failure event. |
| `ConsolidationPlanner` | Service (R2) | Proposes combined sourcing for same-material requests in a window. |
| `SourcingQueries` (public in `Sourcing.Contracts`) | Contract | `GetCurrentPrice(materialId, supplierId?)`, `GetPriceBook(materialId)`; used by estimating and cost. Nothing procurement reads synchronously (file 06 section 4.9). |
| Consumers | Handlers | `procurement.purchase_request.created/approved`, `procurement.purchase_order.issued/changed/cancelled`, `procurement.goods_receipt.posted`, `procurement.invoice.matched`, `procurement.supplier.rated`, `core.party.*`, `core.material.*`, `platform.approval.*` (record type `sourcing.recommendation`), `platform.tenant.suspended/reactivated`, `closeout.lesson.published` (R3). |

Aggregates: request, offer, recommendation, price list. Plain CRUD: sources, supplier items, policy, market index series, alerts, exceptions.

Cross-module reads (all through contracts, none by table): `IMaterialCatalog` and `IUnitConverter` and exchange rates (core), `IProcurementQuery.GetEligibleSuppliers` and `GetRecentPurchasePrice` (procurement), `IBudgetCheck` (cost, **proposed new dependency ****`sourcing -> cost (contracts)`**, section 8.3 and open question 3), `IFileService`, `INotificationService`, `IApprovalService` (platform).

---

## 4. Domain model

All tables are in schema `sourcing`, carry the standard columns of 00 §3.2 (`id`, `tenant_id`, `created_at/by`, `updated_at/by`, `row_version`, `is_deleted` where reference data, `source`) which are **omitted below**, and have RLS on `tenant_id`. Ledger-style tables (`price_observations`, `source_runs`, `recommendation_offer_results`, `decisions`) are append-only (no `updated_*`). Money `numeric(18,4)` + `currency`; quantities `numeric(18,4)` + `unit_code`. References to other modules' tables are by value without FK, except `core.parties`, `core.projects`, `core.work_packages` and `platform.users` (real FKs allowed, noted). Business numbers from `platform.number_series`: `SRC_REQUEST` (`SR-0001`), `SRC_PRICE_LIST` (`PL-0001`), `SRC_RECOMMENDATION` (`RC-0001`).

### 4.1 ER summary

```txt
price_sources 1--* source_runs 1--* staging_price_rows
price_sources 1--* source_incidents
price_sources 1--* price_lists 1--* price_list_lines *--1 supplier_items *--0..1 core.materials
price_list_lines 1--* price_confirmations
sourcing_requests 1--* rfq_invitations
sourcing_requests 1--* offers 1--1 offer_normalisations
sourcing_requests 1--* recommendations 1--* recommendation_offer_results *--1 offers
recommendations 1--0..1 decisions
sourcing_requests 1--* sourcing_exceptions
price_observations (append-only; material, supplier, type, date)
market_index_series 1--* market_index_points ; market_alerts (material or category)
consolidation_proposals 1--* consolidation_proposal_requests *--1 sourcing_requests   (R2)
sourcing_policies (versioned, one current row per tenant) ; material_cache, supplier_cache (replicated read tables)
```

### 4.2 Enumerations

| Enum | Values and meaning |
|---|---|
| `source.adapter_type` | `api` (supplier or index REST/CSV endpoint), `file_schedule` (scheduled pull of a file from URL/SFTP/drive), `email_ingest` (attachment arriving at the tenant's price mailbox), `manual_upload` (officer or supplier uploads), `web_scrape` (R2, only legally approved), `portal` (supplier edits in portal; no run) |
| `source.layer` | `supplier_list` (L1), `quote_reply` (L2), `market_index` (L4). L3 is internal (events) and not a source row. |
| `source.health_status` | `unknown` never ran; `ok`; `degraded` partial data or late; `failing` repeated failure or stale beyond limit; `disabled`; `suspended` auto-suspended after prolonged failure |
| `run.status` | `queued`, `running`, `success`, `partial` (>= 1 row rejected), `skipped_unchanged` (same file hash), `failed` |
| `run.error_code` | `auth_failed`, `timeout`, `http_error`, `schema_changed`, `file_not_found`, `no_new_file`, `parse_error`, `all_rows_rejected`, `blocked_by_site`, `legal_hold`, `sender_not_allowed`, `quota_exceeded`, `mapping_missing`, `internal_error` |
| `price_list.status` | `draft` (parsed, in preview), `pending_review` (unmatched or flagged lines), `active`, `superseded`, `expired` (all lines past `valid_to`), `rejected` |
| `line.status` | `pending_match`, `active`, `stale`, `superseded`, `rejected` |
| `line.verification_status` | `unverified`, `verified` (human or trusted automatic source), `rejected` |
| `item.mapping_status` | `mapped`, `pending_review`, `rejected` (never offer this item) |
| `transport.mode` | `included` (delivered, price includes), `per_unit` (extra per unit), `flat` (extra per delivery), `ex_works` (collect from supplier; needs `transport_estimate`) |
| `payment.kind` | `cash`, `credit` (`credit_days`), `advance_balance` (`advance_pct` up front, balance cash) |
| `request.status` | `queued`, `collecting`, `evaluating`, `ready`, `exception`, `pending_approval`, `decided`, `rejected_to_manual`, `cancelled` |
| `invitation.status` | `queued`, `sent`, `opened`, `replied`, `declined`, `no_reply`, `bounced` |
| `offer.origin` | `price_list`, `recent_quote`, `rfq_reply`, `confirmation`, `manual_entry` |
| `offer.status` | `received`, `normalised`, `valid`, `excluded`, `withdrawn`, `expired` |
| `offer.spec_match` | `match`, `partial`, `no_match`, `unknown` |
| `exclusion.code` | `too_late`, `spec_no_match`, `supplier_blocked`, `supplier_ineligible`, `expired`, `partial_quantity`, `below_min_order`, `transport_unknown`, `price_stale_unconfirmed`, `unit_conversion_missing`, `withdrawn` |
| `recommendation.status` | `ready`, `needs_refresh`, `pending_approval`, `decided`, `rejected`, `superseded`, `void` |
| `decision.kind` | `approved` (recommended offer accepted), `adjusted` (alternative offer or changed quantity), `rejected` (manual RFQ requested); flag `auto_approved` |
| `decision.reason_code` | for `adjusted`/`rejected`: `better_relationship`, `quality_history`, `delivery_certainty`, `price_not_credible`, `supplier_confirmed_new_price`, `spec_preference`, `consolidation`, `no_supplier_available`, `other` (text mandatory with `other`) |
| `exception.type` | `fewer_than_two_valid_offers`, `no_valid_offer`, `no_fresh_price_no_quote`, `stale_price_in_recommendation`, `price_far_above_recent`, `supplier_unresponsive`, `spec_mismatch`, `auto_limit_reached`, `source_failure_blocking`, `unmatched_request_item` |
| `exception.status` | `open`, `acknowledged`, `resolved`, `dismissed` |
| `observation.type` | `list_line`, `quote`, `confirmation`, `po`, `receipt`, `invoice`, `market_index` |
| `alert.type` | `price_spike`, `price_drop`, `above_recent`, `above_index`, `stale_market`, `supplier_price_gap` |

### 4.3 `price_sources`

| Column | Type | Null | Notes |
|---|---|---|---|
| `code` | text | no | unique per tenant, e.g. `SRC-014` |
| `name` | text | no | e.g. "Delta Steel API" |
| `supplier_party_id` | uuid | yes | FK `core.parties`; null for indices |
| `layer` | text | no | `source.layer` |
| `adapter_type` | text | no | enum |
| `config` | jsonb | no | adapter settings: URL, path, column mapping/template id, sender allow-list, sheet name, currency, `confirms_validity` bool, `trust_level` (`trusted`, `needs_verification`) |
| `secret_ref` | text | yes | key in the secret store; **never** the secret itself |
| `schedule_cron` | text | yes | default `0 6 * * *` in tenant time zone; null for manual/portal |
| `expected_freshness_hours` | int | no | default 30 daily, 192 weekly; drives health |
| `is_enabled` | bool | no |  |
| `health_status` | text | no | enum, maintained by `SourceHealthEvaluator` |
| `last_attempt_at`, `last_success_at` | timestamptz | yes |  |
| `consecutive_failures` | int | no | default 0 |
| `last_error_code`, `last_error_message` | text | yes | message without secrets or PII |
| `legal_basis_ref` | text | yes | mandatory for `web_scrape`: reference to written permission or terms review |
| `legal_approved_by`, `legal_approved_at` | uuid, timestamptz | yes | `web_scrape` cannot be enabled without them (BR-SRC-09) |
| `owner_user_id` | uuid | yes | FK `platform.users`; receives failure notices |

Keys: unique `(tenant_id, code)`. Index `(tenant_id, health_status)`, partial `(tenant_id, schedule_cron) where is_enabled`. Checks: `adapter_type='web_scrape'` implies `legal_basis_ref is not null` when enabled; `expected_freshness_hours > 0`.

### 4.4 `source_runs` (append-only), `staging_price_rows`, `source_incidents`

`source_runs`: `source_id` (FK), `run_date date`, `attempt int`, `trigger` (`schedule`, `retry`, `manual`, `webhook`), `status`, `started_at`, `finished_at`, `rows_read int`, `rows_accepted int`, `rows_rejected int`, `rows_unmatched int`, `rows_changed int`, `error_code`, `error_detail text`, `raw_file_id uuid null` (platform file, retention 90 days), `file_sha256 text null`, `price_list_id uuid null`. Unique `(tenant_id, source_id, run_date, attempt)`. Index `(tenant_id, source_id, started_at desc)`.

`staging_price_rows` (also used for manual upload preview; purged 30 days after commit): `run_id` (FK), `row_no int`, `raw jsonb`, `parsed jsonb null`, `parse_status` (`parsed`, `rejected`, `warning`), `error_code text null`, `error_params jsonb null`. Unique `(run_id, row_no)`.

`source_incidents`: `source_id` (FK), `opened_at`, `reason_code` (a `run.error_code` or `stale_beyond_limit`), `reason_detail`, `status` (`open`, `acknowledged`, `resolved`), `acknowledged_by/at`, `resolved_by/at`, `resolution_note`. Partial unique `(tenant_id, source_id) where status <> 'resolved'` (one open incident per source).

### 4.5 `supplier_items` (mapping supplier wording to material master)

| Column | Type | Null | Notes |
| --- | --- | --- | --- |
| `supplier_party_id` | uuid | no | FK `core.parties` |
| `supplier_item_code` | text | yes | supplier's own code |
| `item_text` | text | no | as supplier writes it |
| `normalized_text` | text | no | Arabic-normalised per 00 §3.5 |
| `spec_text` | text | yes |  |
| `supplier_unit` | text | yes | as written ("طن", "bag") |
| `unit_code` | text | yes | resolved `core.units` code |
| `pack_qty` | numeric(18,4) | yes | e.g. 50 with `pack_unit_code` kg per bag |
| `pack_unit_code` | text | yes |  |
| `material_id` | uuid | yes | `core.materials` by value; null while pending |
| `mapping_status` | text | no | enum |
| `match_method` | text | yes | `exact_alias`, `trigram`, `manual`, `ai_suggestion` (R2; accepted by a human) |
| `match_score` | numeric(7,4) | yes | 0-1 |
| `candidates` | jsonb | yes | top 5 `{materialId, score}` for the review screen |
| `mapped_by`, `mapped_at` | uuid, timestamptz | yes |  |

Unique `(tenant_id, supplier_party_id, coalesce(supplier_item_code,''), normalized_text)`. Index `(tenant_id, mapping_status)`, `(tenant_id, material_id)`, trigram GIN on `normalized_text`.

### 4.6 `price_lists` and `price_list_lines` (layer L1)

`price_lists`: `number` (`PL-0001`), `supplier_party_id` (FK), `source_id uuid null`, `title`, `currency`, `valid_from date`, `valid_to date null`, `status`, `verification_status`, `file_id uuid null`, `version int`, `supersedes_id uuid null`, `is_current bool`, `uploaded_by uuid`, `verified_by/at`, `activated_by/at`, `region text null` (delivery region the list applies to), `notes`. Unique `(tenant_id, number)`. Partial unique `(tenant_id, supplier_party_id, source_id) where is_current`. Check `valid_to is null or valid_to >= valid_from`.

`price_list_lines`:

| Column | Type | Null | Notes |
| --- | --- | --- | --- |
| `price_list_id` | uuid | no | FK |
| `supplier_item_id` | uuid | no | FK |
| `material_id` | uuid | yes | copy of the mapping at load time; null if `pending_match` |
| `unit_code` | text | no | price unit (supplier's) |
| `unit_price` | numeric(18,4) | no | > 0 |
| `currency` | text | no |  |
| `vat_included` | bool | no | default false |
| `tax_code` | text | yes | `core.tax_codes`; default tenant VAT 14% |
| `transport_mode` | text | no | enum |
| `transport_amount` | numeric(18,4) | yes | per unit or flat by mode; null for `included` |
| `min_order_qty` | numeric(18,4) | yes | in `unit_code` |
| `order_multiple` | numeric(18,4) | yes | e.g. 50 (rounding up) |
| `price_breaks` | jsonb | yes | `[{"fromQty":100,"unitPrice":2980}]`, ascending; applied by BR-SRC-18 |
| `payment_kind`, `credit_days`, `advance_pct` | text, int, numeric(7,4) |  | terms the price assumes; null = supplier default from profile |
| `lead_time_days` | int | yes | >= 0 |
| `available_qty` | numeric(18,4) | yes | null = not stated |
| `valid_from`, `valid_to` | date | no / yes |  |
| `last_confirmed_on` | date | no | load date, or date of the last unchanged re-confirmation (BR-SRC-12) |
| `status`, `verification_status` | text | no | enums |
| `is_stale` | bool | no | cache maintained by `StalePriceSweeper`; query-time check is authoritative |
| `source_run_id` | uuid | yes |  |
| `supersedes_id` | uuid | yes | previous line for same item |

Indexes: `(tenant_id, material_id, status, last_confirmed_on desc)`, `(tenant_id, supplier_item_id) where status in ('active','stale')`, `(tenant_id, price_list_id)`. Checks: `unit_price > 0`, `min_order_qty > 0`, `lead_time_days >= 0`.

### 4.7 `price_confirmations` (one-tap, R1 basic / R2 full)

`price_list_line_id` (FK), `supplier_party_id`, `token_hash text` (never the raw token), `channel` (`portal_link`, `email`, `whatsapp` R2), `status` (`sent`, `confirmed`, `changed`, `no_reply`, `expired`), `sent_at`, `responded_at`, `new_unit_price numeric(18,4) null`, `new_valid_to date null`, `responded_by_external_user_id uuid null`. Index `(tenant_id, status, sent_at)`.

### 4.8 `sourcing_policies` (versioned; one current per tenant)

| Column | Type | Default | Notes |
| --- | --- | --- | --- |
| `version` | int | 1 | new version on change; `is_current` bool; `supersedes_id` |
| `financing_rate_monthly_pct` | numeric(7,4) | 2.0000 | value of credit time (BR-SRC-22); file 06 keeps its own copy (open question 6) |
| `weights` | jsonb | `{"cost":70,"lead_time":15,"reliability":15}` | must sum to 100 (BR-SRC-30) |
| `reliability_default_score` | numeric(7,4) | 70 | when supplier has fewer than 3 rated events |
| `spec_partial_penalty` | numeric(7,4) | 10 | points deducted from total |
| `min_valid_offers` | int | 2 | below it: exception (BR-SRC-36) |
| `rfq_deadline_hours` / `rfq_deadline_hours_urgent` | int | 24 / 2 |  |
| `rfq_min_suppliers` / `rfq_max_suppliers` | int | 3 / 5 |  |
| `always_rfq_above_amount` | numeric(18,4) null | null | RFQ even if fresh prices exist when estimated value exceeds it |
| `quote_default_validity_hours` | int | 24 | when the supplier states none |
| `confirm_before_expiry_days` | int | 1 | one-tap confirmation sent this long before expiry |
| `reference_days` | int | 90 | window for `GetRecentPurchasePrice` |
| `price_above_recent_pct` | numeric(7,4) | 5 | flag threshold (same as file 06) |
| `price_jump_pct` | numeric(7,4) | 40 | ingestion jump check (BR-SRC-08) |
| `review_sla_hours` / `review_sla_hours_urgent` | int | 8 / 2 |  |
| `auto_approve_enabled` | bool | false |  |
| `auto_approve_limit_amount` | numeric(18,4) | 0 | landed amount ceiling |
| `auto_approve_max_over_reference_pct` | numeric(7,4) | 3 |  |
| `allow_self_decision` | bool | false | segregation switch (BR-SRC-43) |
| `collection_time_local` | text | `06:00` | tenant time zone |
| `formula_version` | text | `src-1` | engine version used for new recommendations |

Index unique `(tenant_id) where is_current`.

### 4.9 `sourcing_requests`

| Column | Type | Null | Notes |
| --- | --- | --- | --- |
| `number` | text | no | `SR-0001` |
| `pr_id`, `pr_line_id` | uuid | no | by value (procurement); unique `(tenant_id, pr_line_id) where status not in ('cancelled','rejected_to_manual')` makes the queue handler idempotent |
| `pr_number` | text | no | denormalised, e.g. `PR-0412` |
| `project_id` | uuid | no | FK `core.projects` |
| `work_package_id`, `cost_code_id` | uuid | no | BR-X-01 |
| `material_id` | uuid | no | must be set (auto-sourcing route requires a material) |
| `quantity` | numeric(18,4) | no | > 0 |
| `unit_code` | text | no | request unit |
| `need_by_date` | date | no | hard filter basis |
| `delivery_zone_id`, `delivery_region` | uuid, text | yes | for supplier selection and price-list region match |
| `is_urgent` | bool | no |  |
| `spec_requirements` | jsonb | no | from material specs and PR line notes; default `{}` |
| `budget_reference_amount` | numeric(18,4) | yes | budget rate x quantity (180,000 in the golden example) |
| `budget_unit_rate` | numeric(18,4) | yes | 3,000 |
| `assigned_officer_id` | uuid | no | FK `platform.users` |
| `status` | text | no | `request.status` |
| `collect_until` | timestamptz | yes | window end |
| `collection_closed_at` | timestamptz | yes |  |
| `current_recommendation_id` | uuid | yes |  |
| `route_check_status` | text | no | `ok` or `line_rerouted` (set when procurement reports the line no longer routed here) |
| `cancel_reason` | text | yes |  |

Index `(tenant_id, status, assigned_officer_id)`, partial `(tenant_id, collect_until) where status = 'collecting'`, `(tenant_id, project_id, created_at desc)`.

### 4.10 `rfq_invitations`

`request_id` (FK), `supplier_party_id` (FK), `channel` (`portal_link`, `email`, `whatsapp` R2), `token_hash`, `sent_at`, `deadline_at`, `status`, `opened_at`, `replied_at`, `declined_reason text null`, `reminder_count int default 0`, `last_reminder_at`, `bounce_reason text null`, `selection_reason` (`fresh_price_missing`, `stale_price`, `min_offers`, `high_value`, `manual`). Unique `(tenant_id, request_id, supplier_party_id)`. Token validity: until `deadline_at + 24 h`; single supplier scope.

### 4.11 `offers`

| Column | Type | Null | Notes |
| --- | --- | --- | --- |
| `request_id` | uuid | no | FK |
| `supplier_party_id` | uuid | no | FK |
| `origin` | text | no | enum |
| `price_list_line_id` | uuid | yes | when from a list or confirmation |
| `invitation_id` | uuid | yes | when an RFQ reply |
| `offered_qty`, `qty_unit_code` | numeric(18,4), text | no | quantity the supplier offers, in its own unit |
| `unit_price`, `price_unit_code` | numeric(18,4), text | no | price per `price_unit_code` |
| `currency`, `fx_rate`, `fx_date` | text, numeric(18,8), date | no/yes | `fx_rate` required if currency is not base |
| `vat_included`, `vat_rate_pct` | bool, numeric(7,4) | no |  |
| `discount_pct`, `discount_amount` | numeric(7,4), numeric(18,4) | yes | one of them |
| `transport_mode`, `transport_amount` | text, numeric(18,4) |  |  |
| `other_charges_amount` | numeric(18,4) | yes |  |
| `payment_kind`, `credit_days`, `advance_pct`, `payment_terms_code` |  |  | `payment_terms_code` references the procurement terms code by value |
| `min_order_qty`, `order_multiple` | numeric(18,4) | yes |  |
| `availability` | text | no | `in_stock`, `on_order`, `partial`, `unknown` |
| `available_qty` | numeric(18,4) | yes |  |
| `lead_time_days` | int | no | >= 0 |
| `quoted_at` | timestamptz | no |  |
| `valid_until` | timestamptz | no | BR-SRC-13 |
| `spec_match`, `spec_notes` | text |  | `unknown` counts as `match` with flag unless policy says otherwise |
| `is_price_stale` | bool | no | price older than validity at the time of use |
| `stale_confirmed_by/at` | uuid, timestamptz | yes | BR-SRC-14 |
| `entered_by` | uuid | yes | manual entry or edit; used for segregation (BR-SRC-43) |
| `attachment_file_id` | uuid | yes | PDF/photo evidence |
| `status` | text | no | enum |
| `exclusion_codes` | text\[\] | yes | last engine result |

Unique `(tenant_id, request_id, supplier_party_id) where status not in ('withdrawn','expired')` (one live offer per supplier; a new quote withdraws the old one). Index `(tenant_id, request_id, status)`.

### 4.12 `offer_normalisations` (derived, recomputable)

`offer_id` (FK, unique), `qty_in_request_unit`, `request_unit_code`, `unit_price_ex_vat_request_unit numeric(18,4)`, `line_amount numeric(18,4)`, `discount_amount`, `transport_amount`, `other_charges_amount`, `landed_amount` (ex VAT), `landed_unit_price`, `vat_amount`, `gross_amount`, `credit_days int`, `credit_value_amount`, `advance_cost_amount`, `effective_amount`, `earliest_delivery_date date`, `feasibility` (`feasible`, `late`, `unknown`), `rounded_up_qty numeric(18,4) null`, `flags jsonb` (e.g. `["transport_estimated","unit_converted"]`), `formula_version`, `computed_at`. All amounts in **base currency** after fx.

### 4.13 `recommendations`, `recommendation_offer_results`, `decisions`

`recommendations`: `number` (`RC-0001`), `request_id` (FK), `version int`, `is_current bool`, `status`, `formula_version`, `policy_snapshot jsonb`, `inputs jsonb` (the full `ComparisonInput`, section 6.6), `input_hash text` (sha256 of canonical JSON), `result jsonb` (the full `ComparisonResult`), `recommended_offer_id uuid null`, `valid_offer_count int`, `excluded_count int`, `flags text[]`, `budget_reference_amount`, `budget_overrun_amount numeric null`, `budget_overrun_pct numeric null`, `reference_unit_price numeric null` (recent purchase), `reference_date date null`, `explanation jsonb` (list of `{code, params}`), `assigned_officer_id`, `ready_at timestamptz`, `review_due_at timestamptz`, `last_reminder_at`, `escalated_at`, `supersedes_id`. Unique `(tenant_id, request_id, version)`; partial unique `(tenant_id, request_id) where is_current`.

`recommendation_offer_results` (append-only copy of per-offer results for reporting; the JSON in `recommendations.result` is the legal record): `recommendation_id`, `offer_id`, `supplier_party_id`, `rank int null`, `is_excluded bool`, `exclusion_codes text[]`, `landed_amount`, `credit_value_amount`, `effective_amount`, `lead_time_days`, `cost_score`, `lead_score`, `reliability_score`, `penalty_points`, `total_score` (numeric(9,4)), `flags text[]`. Unique `(recommendation_id, offer_id)`.

`decisions` (append-only): `recommendation_id` (FK, unique when `status='final'`), `decision` (`approved`/`adjusted`/`rejected`), `selected_offer_id uuid null`, `awarded_qty numeric(18,4) null`, `award_unit_code`, `reason_code`, `reason_text`, `decided_by uuid null` (null when `auto_approved`), `auto_approved bool`, `auto_policy_snapshot jsonb null`, `decided_at`, `approval_id uuid null` (platform approval when over limit), `approved_by uuid null`, `status` (`final`, `awaiting_approval`, `approval_rejected`), `award_snapshot jsonb` (the exact offer terms in base and offer currency copied at decision time, BR-SRC-48), `override_of_rank int null` (rank of the chosen offer when not 1).

### 4.14 Exceptions, observations, alerts, consolidation, caches

`sourcing_exceptions`: `request_id` uuid null, `source_id` uuid null, `type`, `severity` (`info`, `warning`, `critical`), `status`, `details jsonb`, `raised_at`, `acknowledged_by/at`, `resolved_by/at`, `resolution` (`manual_rfq`, `accepted_single_offer`, `extended_deadline`, `confirmed_stale_price`, `dismissed`), `dedupe_key text`. Unique `(tenant_id, dedupe_key) where status in ('open','acknowledged')`.

`price_observations` (append-only; partition by month when > 50M rows \[plan\]): `material_id`, `supplier_party_id uuid null`, `project_id uuid null`, `observation_type`, `observed_on date`, `unit_price_ex_vat numeric(18,4)` (per **base unit**, base currency), `landed_unit_price numeric(18,4) null`, `base_unit_code`, `qty numeric(18,4) null`, `payment_terms_code`, `credit_days int null`, `lead_time_days int null`, `region text null`, `source_ref_type`, `source_ref_id uuid`, `is_outlier bool` (BR-SRC-52), `event_id uuid null`. Unique `(tenant_id, source_ref_type, source_ref_id, material_id)` (idempotent consumers). Index `(tenant_id, material_id, observed_on desc)`, `(tenant_id, material_id, supplier_party_id, observed_on desc)`.

`market_index_series`: `code`, `name_ar`, `name_en`, `category_id uuid`, `material_id uuid null`, `unit_code`, `currency`, `source_id uuid null`, `is_active`. `market_index_points`: `series_id` (FK), `point_date date`, `value numeric(18,4)`, unique `(series_id, point_date)`. (R2 manual, R3 benchmarks)

`market_alerts`: `material_id uuid null`, `category_id uuid null`, `type`, `severity`, `details jsonb` (`{previousPrice, newPrice, changePct, windowDays, sampleSize}`), `status` (`open`, `acknowledged`, `closed`), `raised_at`, `acknowledged_by/at`, `dedupe_key`. Unique `(tenant_id, dedupe_key) where status = 'open'`.

`consolidation_proposals` (R2): `material_id`, `window_start date`, `window_end date`, `total_quantity`, `unit_code`, `estimated_saving_pct numeric null`, `status` (`proposed`, `accepted`, `dismissed`, `expired`); `consolidation_proposal_requests`: `proposal_id`, `request_id`.

Replicated read tables (fed by events, no FKs): `material_cache` (`material_id`, `code`, `name_ar`, `name_en`, `category_id`, `category_code`, `base_unit_code`, `validity_days`, `status`, `auto_sourcing_enabled`) and `supplier_cache` (`party_id`, `name`, `status`, `is_blocked`, `reliability_score numeric null`, `reliability_events int`, `as_of`). Idempotent upserts keyed by id with `source_event_at` guard.

---

## 5. State machines

Invalid transitions return `409 invalid_state_transition` (00 §2.3). Every status change is written to `platform.status_history` (00 §3.4).

### 5.1 Sourcing request

| From | Action | To | Guard | Who | Side effects |
| --- | --- | --- | --- | --- | --- |
| (none) | `procurement.purchase_request.approved` consumed | `queued` | line `route=auto_sourcing`, material set, no live request for `pr_line_id` | system | number `SR-`; event `sourcing.request.queued.v1`; material and supplier eligibility resolved |
| `queued` | `start_collection` (worker, immediate) | `collecting` | at least one eligible supplier, or at least one fresh price | system | fresh L1/L2 prices become offers; RFQ invitations for the rest; `collect_until` set |
| `queued` | `start_collection` | `exception` | no eligible supplier | system | exception `no_fresh_price_no_quote`; officer notified |
| `collecting` | `close_collection` (deadline, or all invitations answered or declined, or `collect-now`) | `evaluating` |  | system, officer | event `sourcing.offers.collected.v1`; engine runs |
| `evaluating` | `recommend` | `ready` | `valid_offer_count >= 1` | system | recommendation stored; event `sourcing.recommendation.ready.v1`; task and notification; `review_due_at` set |
| `evaluating` | `recommend` | `exception` | `valid_offer_count = 0` | system | exception `no_valid_offer`; `ready` event published with `recommendedOfferId = null` so procurement shows "awaiting review" with the exception |
| `ready` | `decide` approve/adjust, officer within limit | `decided` | BR-SRC-40..44 | officer | decision stored; event `sourcing.recommendation.decided.v1` |
| `ready` | `decide`, officer over limit | `pending_approval` | amount > officer limit | officer | platform approval requested (record type `sourcing.recommendation`) |
| `pending_approval` | approval approved | `decided` |  | approver (PM/Finance) | `decided` event with `approvedBy` |
| `pending_approval` | approval rejected or cancelled | `ready` |  | approver | decision row `approval_rejected`; officer re-decides |
| `ready`, `exception` | `decide` reject | `rejected_to_manual` | reason mandatory | officer | event `decided` with `decision=rejected`; procurement routes line to manual RFQ |
| `exception` | `resolve` with `accept_single_offer` | `ready` | exactly 1 valid offer; reason | officer | recommendation flagged `single_offer` |
| `exception` | `extend_deadline` / `recompute` | `collecting` / `evaluating` | not past need date | officer | new invitations or recompute |
| `ready` | `recompute` | `evaluating` | no final decision | officer, system (BR-SRC-46) | previous recommendation `superseded` |
| `queued`, `collecting`, `evaluating`, `ready`, `exception` | `cancel` | `cancelled` | PR line cancelled, rerouted by officer to `manual_rfq`, or PR cancelled | system, officer | invitations closed; recommendation `void` |

### 5.2 Recommendation

| From | Action | To | Guard | Side effects |
| --- | --- | --- | --- | --- |
| (none) | engine result stored | `ready` |  | frozen inputs and result (BR-SRC-39) |
| `ready` | selected-offer expiry detected | `needs_refresh` | `OfferExpiryScanner` | officer notified; decide blocked until refresh or explicit `confirm_stale` (BR-SRC-14) |
| `needs_refresh`, `ready` | `recompute` | `superseded` |  | new row `version+1` |
| `ready` | over-limit decide | `pending_approval` |  | approval instance |
| `ready`, `pending_approval` | final decision | `decided` or `rejected` |  | `decisions` row |
| any non-final | request cancelled | `void` |  |  |

### 5.3 Price list and lines

| From | Action | To | Guard | Who |
| --- | --- | --- | --- | --- |
| (none) | upload or collect | `draft` | file parsed | uploader, system |
| `draft` | `commit` | `pending_review` (flagged or unmatched lines) or `active` (source trusted, no flags) | rows valid | uploader, system |
| `pending_review` | `verify` | `active` | verifier is not uploader unless `allow_self_decision`; flagged lines resolved or rejected | officer |
| `active` | newer list for same supplier/source becomes current | `superseded` |  | system |
| `active` | all lines past `valid_to` | `expired` |  | `StalePriceSweeper` |
| `draft`, `pending_review` | `reject` | `rejected` | reason | officer |

Line: `pending_match` -> `active` (mapping approved), `active` -> `stale` (BR-SRC-14 sweep), `stale` -> `active` (confirmation or new price), `active/stale` -> `superseded`, any -> `rejected`.

### 5.4 Source health and incident

`unknown -> ok` (first success); `ok -> degraded` (BR-SRC-10); `degraded -> failing`; any -> `ok` on success within freshness; `failing -> suspended` after 14 days; `disabled` by admin. Incident `open -> acknowledged -> resolved` (automatic resolve on first success, with note `auto_recovered`).

### 5.5 Invitation

`queued -> sent -> opened -> replied | declined`; `sent/opened -> no_reply` at deadline; `queued/sent -> bounced` (email bounce, notification sent to the officer to choose another channel).

---

## 6. Business rules

### 6.1 Sources and collection

| ID | Rule |
|---|---|
| BR-SRC-01 | Only L1 (price list lines, confirmations) and L2 (RFQ replies, recent quotes) can become offers. L3 observations and L4 index values are reference data and never create or rank an offer. |
| BR-SRC-02 | `CollectionOrchestrator` runs at `collection_time_local` (default 06:00, tenant time zone `Africa/Cairo`) every day, once per enabled source whose cron is due. A run is identified by `(source_id, run_date, attempt)`; creating the same run twice returns the existing one (idempotency). |
| BR-SRC-03 | Transient errors (`timeout`, `http_error` 5xx, `quota_exceeded`) retry at +15 min, +1 h, +3 h (attempts 2-4). Non-transient errors (`auth_failed`, `schema_changed`, `blocked_by_site`, `legal_hold`, `sender_not_allowed`, `mapping_missing`) are not retried and open an incident at once. |
| BR-SRC-04 | If a pulled file has the same `sha256` as the last successful run the run is `skipped_unchanged`: health stays `ok` while within `expected_freshness_hours`; line dates are re-confirmed only if the source has `confirms_validity = true` (BR-SRC-12). |
| BR-SRC-05 | Row validation (per row, never per file): item text or code present; `unit_price > 0`; unit resolvable to `core.units`; currency known; `valid_to >= run_date`; `min_order_qty > 0` if given. A rejected row is kept in staging with an error code and does not fail the run. A run with at least one rejected row is `partial`; with all rows rejected it is `failed` (`all_rows_rejected`). |
| BR-SRC-06 | Matching: a supplier item already `mapped` is used as is. Otherwise `MaterialMatcher` tries exact normalised alias (per supplier alias first, then global). Trigram similarity >= 0.85 produces a **suggestion** only (`pending_review`); the line stays `pending_match`. A line without material never becomes an offer. |
| BR-SRC-07 | Unmapped items are listed in the review queue, grouped by supplier, with top-5 candidates. Mapping an item applies to all its existing and future lines and publishes nothing; a decision-time mapping change never alters past recommendations (frozen inputs). |
| BR-SRC-08 | Price jump check: if a new line's price differs from the previous line of the same supplier item by more than `price_jump_pct` (default 40%), the line is loaded `unverified` with flag `price_jump` and cannot be used in an offer until a person verifies it. |
| BR-SRC-09 | A `web_scrape` source can be enabled only if `legal_basis_ref`, `legal_approved_by` and `legal_approved_at` are set by a `tenant_admin` **\[verify with counsel: Egyptian terms-of-use and data-protection position; v3 4.4A.1 says "we do not scrape supplier websites", so this adapter is R2, off by default, and limited to pages the supplier has permitted in writing or public official indices\]**. It must honour `robots.txt` and a rate limit of 1 request per 5 seconds; `blocked_by_site` is non-transient. |
| BR-SRC-10 | Health: `ok` if the last run succeeded (or was skipped unchanged) and `now - last_success_at <= expected_freshness_hours`; `degraded` if the last run was `partial` with more than 20% rejected rows, or freshness is exceeded by up to 1x; `failing` if `consecutive_failures >= 2` or freshness exceeded by more than 1x; `suspended` after 14 days failing; `disabled` is set only by an admin. Manual-upload and portal sources are measured by freshness only. |
| BR-SRC-11 | When a source becomes `failing` (or on first non-transient failure) the module opens one incident, publishes `sourcing.source.failed.v1` once per incident, and notifies the owner and the tenant admin. Lines from the failing source are not deleted: they age and become stale by BR-SRC-14, so offers built from them are replaced by RFQ. A request that is `collecting` and depends on a failing source triggers immediate RFQ for the affected suppliers. |
| BR-SRC-12 | Re-confirmation: an unchanged row in a successful run of a source with `confirms_validity = true` sets `last_confirmed_on = run_date` and extends validity (BR-SRC-13) without creating a new line; a changed price or terms supersede the line with a new row. |
| BR-SRC-13 | **Freshness and expiry.** `validity_days = IMaterialCatalog.GetValidityDays(material)` (override, else category; BR-COR-33). A line is fresh on date `d` if `d <= valid_to` (when set) and `d <= last_confirmed_on + validity_days - 1`... expressed as `price_expires_on = min(valid_to, last_confirmed_on + validity_days)`; fresh iff `today <= price_expires_on`. An RFQ reply expires at `valid_until` stated by the supplier, else `quoted_at + quote_default_validity_hours`. Example: cement validity 2, confirmed 10 Oct: expires 12 Oct. |
| BR-SRC-14 | A stale price (past `price_expires_on`) is never ranked silently. The engine marks the offer excluded `price_stale_unconfirmed` unless an officer with `sourcing.offer.confirm_stale` confirmed it for this request (`stale_confirmed_by/at`, reason audited); a confirmed stale offer is ranked with flag `stale_price_confirmed`. The sweeper sets `line.status = stale` and sends a one-tap confirmation request `confirm_before_expiry_days` before expiry. |
| BR-SRC-15 | A manual-upload or `needs_verification` list is activated only after `verify` by a user who is not the uploader (BR-SRC-43 switch applies). Trusted API/file sources load `active` directly unless a flag (BR-SRC-08) applies. |

### 6.2 Supplier selection, normalisation and offers

| ID | Rule |
|---|---|
| BR-SRC-16 | Eligible suppliers = `IProcurementQuery.GetEligibleSuppliers(categoryCode, projectId)`: profile `approved` (not blocked), supplies the material category, delivers to sites, documents valid. Eligibility is re-checked at comparison time; a supplier blocked meanwhile is excluded `supplier_blocked`. |
| BR-SRC-17 | Offer gathering for a request: (a) every eligible supplier with a **fresh** L1/L2 price for the material and region yields an offer with `origin=price_list` or `recent_quote`; (b) RFQ invitations go to eligible suppliers without a fresh price; (c) if fewer than `min_valid_offers` fresh offers exist, invitations also go to additional eligible suppliers up to `rfq_max_suppliers` (at least `rfq_min_suppliers` when available), best `reliability` first; (d) if `always_rfq_above_amount` is set and the estimated value exceeds it, all selected suppliers are invited even if prices are fresh. `collect_until = now + rfq_deadline_hours` (urgent: `rfq_deadline_hours_urgent`), never later than `need_by_date - 1 day` start of business; if that is already past the window is 0 and only fresh prices count. |
| BR-SRC-18 | Price break: if the line has `price_breaks`, the applicable unit price is the one with the largest `fromQty <= offered quantity in price unit`; otherwise `unit_price`. |
| BR-SRC-19 | Unit normalisation: offer quantity and price are converted to the **request unit** with `IUnitConverter.Convert(qty, from, to, materialId)` (e.g. 60 t = 1,200 bags of 50 kg; price per bag 152.5 = 3,050 per t). No conversion path: offer excluded `unit_conversion_missing` and a mapping task is created. |
| BR-SRC-20 | VAT: if `vat_included`, `unit_price_ex_vat = unit_price / (1 + vat_rate_pct/100)`. All comparison amounts are **ex VAT** (VAT is recoverable input tax **\[confirm with an accountant\]**); `vat_amount = landed * vat_rate_pct/100` is stored for display and for the PO. Foreign currency amounts convert with the offer `fx_rate`. |
| BR-SRC-21 | **Landed amount** (ex VAT, base currency) = `line_amount + transport_amount + other_charges - discount`, where `line_amount = qty_request_unit * unit_price_ex_vat_request_unit` (price break applied); transport: `included` = 0 extra, `per_unit` = `rate * qty`, `flat` = amount, `ex_works` = `transport_estimate` supplied on the offer (else excluded `transport_unknown`); discount: `discount_pct` of `line_amount`, or `discount_amount`. `landed_unit_price = landed / qty`. Golden: A 183,000; B 177,000 + 4,200 = 181,200; C 174,000 + 3,000 = 177,000. |
| BR-SRC-22 | **Payment-terms value.** `credit_value = landed * rate/100 * credit_days/30` with `rate = financing_rate_monthly_pct` (default 2). `advance_cost = landed * advance_pct/100 * rate/100 * lead_time_days/30`. **`effective = landed - credit_value + advance_cost`**, each rounded to 4 decimals (half away from zero). Golden: A 183,000 x 0.02 x 1 = 3,660, effective 179,340; B cash = 181,200; C 177,000 x 0.02 x 2 = 7,080, effective 169,920. Same arithmetic as BR-PRC-15 in file 06. |
| BR-SRC-23 | Minimum order and multiples: if `min_order_qty > requested qty` the offer is excluded `below_min_order` (the officer may choose **adjusted** with a larger quantity, BR-SRC-41); if `order_multiple` is set the quantity is rounded up to a multiple and the surplus recorded in `rounded_up_qty` with flag `qty_rounded_up`; the landed amount is computed on the rounded quantity. |
| BR-SRC-24 | **Delivery feasibility.** `earliest_delivery_date = max(quoted_date, today) + lead_time_days` (business dates in project local time). `late` if `earliest_delivery_date > need_by_date`. Golden: today D, need D+2; A lead 1 feasible; B lead 2 feasible (equal is allowed); C lead 5 late. |
| BR-SRC-25 | Availability: `partial` or `available_qty < requested qty` excludes the offer as `partial_quantity` in R1 (shown, not ranked, same as BR-PRC-16). Split awards across suppliers are R2 (open question 5). |
| BR-SRC-26 | Foreign-currency offers need `fx_rate`; if missing the system uses the latest `core.exchange_rates` for the offer date and flags `fx_estimated`. |
| BR-SRC-27 | A new offer from the same supplier for the same request withdraws the previous one (`withdrawn`); history is kept. An offer is immutable once frozen into a recommendation; a correction is a new offer. |
| BR-SRC-28 | Manual offer entry (officer types a phone/WhatsApp quote) requires `quoted_at`, validity, terms and a photo/PDF/note as evidence; `origin = manual_entry`, `entered_by` stored. |
| BR-SRC-29 | Specification match: `no_match` excludes (`spec_no_match`); `partial` stays eligible with penalty `spec_partial_penalty` points; `unknown` is treated as `match` with flag `spec_unverified`. Material alternatives (`core.material_alternatives`) are offered only if the officer adds the alternative material to the request manually (R2: automatic). |

### 6.3 Comparison engine

| ID | Rule |
|---|---|
| BR-SRC-30 | Policy weights `{cost, lead_time, reliability}` are non-negative and sum to 100; default 70/15/15 (same as file 06 manual-1). A policy change creates a new policy version; it never alters existing recommendations. |
| BR-SRC-31 | **Hard exclusions** are evaluated first, an offer may carry several codes: `too_late`, `spec_no_match`, `supplier_blocked`, `supplier_ineligible`, `expired` (`valid_until < asOf`), `partial_quantity`, `below_min_order`, `transport_unknown`, `price_stale_unconfirmed`, `unit_conversion_missing`, `withdrawn`. Excluded offers get `rank = null`, are listed with their codes, and are not used for min values in scores. |
| BR-SRC-32 | Among non-excluded offers: `cost_score = 100 * min_effective / effective`; `lead_score = 100 * max(min_lead,1) / max(lead,1)`; `reliability_score` = supplier score or `reliability_default_score`. |
| BR-SRC-33 | `total = (w_cost*cost_score + w_lead*lead_score + w_rel*reliability_score)/100 - penalty`, where `penalty = spec_partial_penalty` if `spec_match = partial` else 0; rounded to 4 decimals. Rank 1 = highest total; ties broken by lower effective amount, then lower lead time, then earlier `quoted_at`, then lower offer id. |
| BR-SRC-34 | **Golden scores.** Valid offers A and B, reliability 70 each (default). A: cost 100, lead 100, rel 70 -> `(7,000 + 1,500 + 1,050)/100 = 95.5000`. B: cost `100*179,340/181,200 = 98.9735`, lead `100*1/2 = 50`, rel 70 -> `(6,928.1457 + 750 + 1,050)/100 = 87.2815`. Rank A=1, B=2, C excluded `too_late`. |
| BR-SRC-35 | The recommendation is the rank-1 offer. Alternatives are the other ranked offers; excluded offers are shown with reasons. The engine never recommends an excluded offer. |
| BR-SRC-36 | If valid offers `< min_valid_offers` (default 2): with exactly 1 the recommendation is created with flag `single_offer`, the request enters `exception` first (`fewer_than_two_valid_offers`) and moves to `ready` only when the officer resolves it with `accept_single_offer` and a reason, or extends the deadline; with 0 the exception is `no_valid_offer` and no recommendation is produced (outcome: manual RFQ or extension). |
| BR-SRC-37 | Flags (informational, do not change rank): `over_budget` when `landed - budget_reference_amount > 0` (golden: 183,000 - 180,000 = 3,000, 1.6667%); `price_above_recent` when `landed_unit_price > reference_unit_price * (1 + price_above_recent_pct/100)`; `close_call` when top-two totals differ by less than 2 points; `single_offer`; `stale_price_confirmed`; `qty_rounded_up`; `fx_estimated`; `transport_estimated`. Budget impact never blocks (v3 4.4A.4); hard budget blocking is procurement's `IBudgetCheck` at PO time. |
| BR-SRC-38 | The explanation is a list of `{code, params}` items rendered client-side in Arabic/English. Golden: `recommended_effective_lowest {supplier:A, effective:179340}`, `credit_offsets_price_gap {creditValue:3660, nominalGap:-1800, supplier:A, versus:B}`, `alternative_cheaper_nominal {supplier:B, landed:181200}`, `excluded_late {supplier:C, leadDays:5, needByDate}`, `over_budget {amount:3000, pct:1.6667}`. |
| BR-SRC-39 | **Pure function and reproducibility (BR-X-10).** `ComparisonEngine.Compare` takes one immutable `ComparisonInput` and returns one `ComparisonResult` with no database, clock, random or culture dependence; `asOfDate` and all reference data are inputs. The full input, `input_hash`, result, `formula_version` and the policy snapshot are stored on the recommendation. Re-running the engine on the stored input with the same version must return byte-identical JSON (tested). A formula change ships as new version (`src-2`); old versions remain executable for audits. |

### 6.4 Decision workflow

| ID | Rule |
|---|---|
| BR-SRC-40 | A decision is one of: **approve recommended** (kind `approved`, selected = recommended offer, quantity = requested); **choose alternative** (kind `adjusted`, any non-excluded offer different from the recommended one, or the same offer with a changed quantity); **reject** (kind `rejected`, manual RFQ). One final decision per recommendation (unique). |
| BR-SRC-41 | `adjusted` and `rejected` require `reason_code` and, for `other` or when the chosen offer is not rank 1, `reason_text` (min 10 characters). An excluded offer cannot be chosen except `below_min_order` or `partial_quantity` with an explicit changed quantity (then the engine is re-run on the adjusted quantity and the new result stored as a new recommendation version before the decision). Choosing any offer with an exclusion code `too_late`, `spec_no_match`, `supplier_blocked` is not allowed. |
| BR-SRC-42 | Permission `sourcing.recommendation.decide` carries an **amount limit** (landed amount, base currency) from `platform.project_members.limits`. A decision above the limit goes to the approval engine (BR-X-07). Project scope applies (BR-X-09). |
| BR-SRC-43 | **Segregation of duties.** The deciding user must not be the user who entered or edited the selected offer (`offers.entered_by`), who verified/uploaded the price list line behind it, or who created the purchase request line (`requestedBy`), unless `allow_self_decision = true` and the decision is flagged `self_decision`. Approver of an over-limit decision cannot be the deciding officer (platform rule). |
| BR-SRC-44 | Over-limit: `IApprovalService.Request(recordRef = sourcing.recommendation, amount = landed)`; policy and approvers come from `approval_policy` (PM, then Finance per limits). While pending the recommendation is `pending_approval`; a rejected approval returns it to `ready`. The `decided` event fires after approval and carries `approvalId`. File 06 may treat the PO approval as already satisfied for the same amount only if its own policy says so (open question 4). |
| BR-SRC-45 | **SLA.** `review_due_at = ready_at + review_sla_hours` (urgent: `review_sla_hours_urgent`; working hours ignored in R1). Reminder at 50% elapsed (to the officer), at 100% (officer and PM), escalation task at 150% (PM can decide when delegated). Each reminder is idempotent via `last_reminder_stage`. Requests whose `need_by_date` is within 1 day get the urgent SLA automatically. |
| BR-SRC-46 | Late offers and refresh: after `ready`, new offers do not change the recommendation silently. The UI shows `newer_offers_available` and the officer may `recompute`, which supersedes the recommendation (new version, new SLA). If the recommended offer (or the chosen one) passes `valid_until` before the decision, the recommendation becomes `needs_refresh` and the decision is blocked until recompute or `confirm_stale` (BR-SRC-14). |
| BR-SRC-47 | `reject` returns the line to manual RFQ in procurement (via the `decided` event) and closes the request `rejected_to_manual`. If `reason_code = no_supplier_available`, procurement raises an alert (file 06 4.9). |
| BR-SRC-48 | **Frozen evidence.** On decision the selected offer's terms are copied to `decisions.award_snapshot` (price, unit, quantity, transport, VAT, payment terms, lead time, validity, landed, effective, formula version, input hash). Later price-list changes, policy changes or supplier-score changes do not alter it or the PO draft. |
| BR-SRC-49 | **Automatic approval** (default off): applies only when `auto_approve_enabled`, the recommended offer is rank 1, `landed <= auto_approve_limit_amount`, supplier approved, no flag in (`single_offer`, `stale_price_confirmed`, `price_above_recent`, `over_budget`, `close_call` is allowed), `valid_offer_count >= min_valid_offers`, and `landed_unit_price <= reference_unit_price * (1 + auto_approve_max_over_reference_pct/100)` (a missing reference blocks automation). Every automatic decision is stored with `auto_approved = true`, the policy snapshot, the same event and a daily review list; reaching the limit raises exception `auto_limit_reached` and falls back to the officer. |
| BR-SRC-50 | Decision endpoints are idempotent: same `Idempotency-Key` replays; a second decision on a final recommendation returns `409 recommendation_already_decided`; stale `If-Match` returns `412`. |
| BR-SRC-51 | Learning: every `adjusted`/`rejected` decision stores `override_of_rank`, `reason_code` and offer deltas (effective difference vs recommended). A monthly report groups overrides by reason and supplier; the data feeds weight tuning and supplier ratings (file 06) by human review only (BR-X-08). |

### 6.5 History, alerts, exceptions, visibility

| ID | Rule |
|---|---|
| BR-SRC-52 | Price observations: each PO line (issued/changed), receipt line, invoice line, accepted quote and list line becomes an observation in **base unit, ex VAT, base currency**. An observation is flagged `is_outlier` if it is more than 3 median absolute deviations from the 90-day median of the same material (needs >= 5 samples); outliers are excluded from the price book and from reference prices but kept. Consumers are idempotent by `(source_ref_type, source_ref_id, material_id)`. |
| BR-SRC-53 | Price book: `PriceBookBuilder` computes daily per material the **median** of fresh L1/L2 prices (ex VAT, base unit, supplier-delivered), publishes `sourcing.price.updated.v1` when it differs from the last published value by at least 0.5% or the month changes (matches BR-EST-18). `sampleSize` = number of prices used; fewer than 2 prices publish nothing. |
| BR-SRC-54 | Market alerts: `price_spike` if the price book rises by at least 10% within 7 days (cost uses the same 10% threshold); `price_drop` at -10%; `above_recent` if a recommended landed unit price exceeds the 90-day reference by more than `price_above_recent_pct`; `above_index` if above the L4 index by more than 15% (R3); `stale_market` if a material with open demand has no fresh price from any supplier. Alerts dedupe by `(material, type, week)`. |
| BR-SRC-55 | Exceptions: raised for fewer than two valid offers, no valid offer, no fresh price and no quote by the deadline, stale price used (confirmed), price more than `price_above_recent_pct` above reference, a supplier unresponsive (no reply to 3 consecutive invitations or 5 days), spec mismatch on every offer, auto-approval limit reached, a failing source blocking the request. Each has a `dedupe_key`, a severity and a mandatory resolution (`resolved` with reason). Unresolved critical exceptions older than the SLA escalate to the PM. |
| BR-SRC-56 | Visibility: `foreman` and `superintendent` receive only `status` and `statusLabel` of their requests; every price, offer, supplier, score and flag is **omitted** (not nulled) by DTO builders and exports (BR-X-06). `supplier_user` sees only own invitation and own previous prices. Unit prices of other suppliers are never exposed to a supplier. |
| BR-SRC-57 | Portal link security: invitation tokens are 256-bit random, stored only as SHA-256 hash, scoped to one `(request, supplier)`, expire at `deadline_at + 24 h`, are rate limited (120 req/min per token, per 00 §3.6) and revocable; submission is idempotent per token; an offer submitted after the window closes is stored as `late` (`received`, not used until recompute). |
| BR-SRC-58 | When `platform.tenant.suspended` arrives all collections and invitations pause (runs `skipped`, no outbound messages); on `reactivated` the next scheduled run resumes. |
| BR-SRC-59 | Consolidation (R2): requests for the same material, delivery regions within the same zone group and `need_by_date` within 3 days of each other are proposed as one combined sourcing; accepting creates one request with the summed quantity linked to the source lines; award splits back per line proportionally to quantity. |
| BR-SRC-60 | Supplier reliability score is read from `supplier_cache` (fed by `procurement.supplier.rated.v1`); R2 adds on-time/quality scorecard fields to the input without changing `src-1` (new version `src-2` when they enter the formula). |

### 6.6 Engine contract (pseudo-code)

```txt
ComparisonInput {
  formulaVersion, asOfDate, needByDate, requestedQty, unitCode, currency,
  policy { financingRateMonthlyPct, weights{cost,leadTime,reliability}, reliabilityDefault,
           specPartialPenalty, minValidOffers, priceAboveRecentPct },
  budget { referenceAmount?, unitRate? }, reference { unitPrice?, date? },
  offers[ { offerId, supplierId, qty, landedAmount, creditDays, advancePct, leadTimeDays,
            earliestDeliveryDate, specMatch, supplierBlocked, supplierEligible, validUntil,
            transportKnown, priceStale, staleConfirmed, minOrderOk, qtyOk, unitConvertible,
            withdrawn, reliability?, flags[] } ]
}
Compare(input):
  for o in offers: o.creditValue = round4(o.landed * rate/100 * o.creditDays/30)
                   o.advanceCost = round4(o.landed * o.advancePct/100 * rate/100 * o.leadTimeDays/30)
                   o.effective   = round4(o.landed - o.creditValue + o.advanceCost)
                   o.exclusions  = hardRules(o, input)               // BR-SRC-31, order fixed
  valid = offers without exclusions;  minEff = min(valid.effective);  minLead = max(min(valid.lead),1)
  for v in valid: scores per BR-SRC-32/33 (round4);  sort by total desc, effective asc, lead asc, quotedAt asc, id asc
  recommended = valid[0] or null;  flags per BR-SRC-37;  explanation per BR-SRC-38
  return ComparisonResult { ranking[], excluded[], recommendedOfferId, flags[], explanation[], budgetImpact, formulaVersion }
```

---

## 7. API endpoints

Base `/api/v1/sourcing` (per 00 §3.6). Portal endpoints live under `/api/v1/portal/sourcing` with audience `aud=portal` (BR-SRC-57). All mutating actions accept `Idempotency-Key`; `PATCH` and actions on aggregates require `If-Match` (ETag from the read). Money fields are `{ "amount": 183000.0000, "currency": "EGP" }` in responses where a currency can differ; base-currency amounts are named `*Amount`. **Every response field that holds a price, cost, supplier name, score, flag or offer is omitted for ****`foreman`**** and ****`superintendent`** (BR-SRC-56); those roles only reach `GET /requests` and `GET /requests/{id}` and receive `id, number, prNumber, status, statusLabel, needByDate, materialId, quantity, unitCode`.

### 7.1 Price sources and collection (SRC-E1)

| Method | Path | Purpose | Key request fields | Key response fields | Errors | Permission |
| --- | --- | --- | --- | --- | --- | --- |
| GET | `/price-sources` | List sources | filters below | `items[{id, code, name, layer, adapterType, supplierPartyId, isEnabled, healthStatus, lastSuccessAt, consecutiveFailures, expectedFreshnessHours}]` |  | `sourcing.source.view` |
| POST | `/price-sources` | Create source | `name, layer, adapterType, supplierPartyId?, config, secretRef?, scheduleCron?, expectedFreshnessHours, ownerUserId` | source | `sourcing.source_config_invalid` 422, `sourcing.source_code_duplicate` 409 | `sourcing.source.manage` |
| GET | `/price-sources/{id}` | Read |  | source + `config` (secrets never returned), `openIncidentId?` | 404 | `sourcing.source.view` |
| PATCH | `/price-sources/{id}` | Edit settings | merge patch of editable fields | source | `412`, `sourcing.source_config_invalid` | `sourcing.source.manage` |
| POST | `/price-sources/{id}/enable` | Enable (checks legal fields for web) |  | source | `sourcing.web_source_not_approved` 422 | `sourcing.source.manage` |
| POST | `/price-sources/{id}/disable` | Disable | `reason` | source |  | `sourcing.source.manage` |
| POST | `/price-sources/{id}/legal-approval` | Record legal basis for `web_scrape` | `legalBasisRef` | source | `sourcing.invalid_adapter_for_action` 422 | `sourcing.source.legal_approve` |
| POST | `/price-sources/{id}/run` | Manual run now | `reason?` | `202 { runId }` | `sourcing.source_disabled` 409, `sourcing.run_in_progress` 409 | `sourcing.source.run` |
| GET | `/price-sources/{id}/runs` | Run history | filters | `items[{id, runDate, attempt, trigger, status, rowsRead, rowsAccepted, rowsRejected, rowsUnmatched, errorCode}]` |  | `sourcing.source.view` |
| GET | `/source-runs/{runId}` | One run |  | run + counts + `rawFileId?` | 404 | `sourcing.source.view` |
| GET | `/source-runs/{runId}/rows` | Staging rows (preview/errors) | `filter[parseStatus]` | `items[{rowNo, raw, parsed, parseStatus, errorCode, errorParams}]` |  | `sourcing.source.view` |
| GET | `/source-incidents` | Incidents | filters | `items[{id, sourceId, openedAt, reasonCode, status}]` |  | `sourcing.source.view` |
| POST | `/source-incidents/{id}/acknowledge` | Acknowledge | `note?` | incident | 409 if resolved | `sourcing.source.manage` |
| POST | `/source-incidents/{id}/resolve` | Resolve manually | `note` | incident |  | `sourcing.source.manage` |
| GET | `/source-health` | Dashboard of all sources |  | `items[{sourceId, healthStatus, lastSuccessAt, hoursSinceSuccess, openIncident}]` |  | `sourcing.source.view` |

Filters: `price-sources`: `filter[layer]`, `filter[adapterType]`, `filter[healthStatus]`, `filter[isEnabled]`, `filter[supplierPartyId]`; sort `code`, `-lastSuccessAt`, `healthStatus`. `runs`: `filter[status]`, `filter[runDate][gte|lte]`; sort `-startedAt`. `incidents`: `filter[status]`, `filter[sourceId]`; sort `-openedAt`.

### 7.2 Price lists, lines, supplier items (SRC-E1, SRC-E2)

| Method | Path | Purpose | Key request fields | Key response fields | Errors | Permission |
| --- | --- | --- | --- | --- | --- | --- |
| POST | `/price-lists/uploads` | Start manual upload (file already completed via `POST /files/{id}/complete`) | `supplierPartyId, fileId, templateId?, validFrom, validTo?, currency, region?` | `202 { runId, priceListId }` (status `draft`) | `sourcing.file_type_not_allowed` 422, `sourcing.template_unknown` 422 | `sourcing.price_list.upload` |
| GET | `/price-lists/{id}/preview` | Parsed rows with errors (00 §3.14 step 2) | `?limit&cursor` | `{ counts{read,accepted,rejected,unmatched}, rows[...] }` |  | `sourcing.price_list.view` |
| POST | `/price-lists/{id}/commit` | Commit staging to lines | `skipRejected: bool` | list (`pending_review` or `active`) | `sourcing.price_list_has_errors` 422 when `skipRejected=false` and rejects exist | `sourcing.price_list.upload` |
| POST | `/price-lists/{id}/verify` | Verify and activate | `acknowledgedFlags[]` | list `active` | `sourcing.self_verification_not_allowed` 422, `sourcing.unresolved_lines` 422 | `sourcing.price_list.verify` |
| POST | `/price-lists/{id}/reject` | Reject draft/pending list | `reason` | list `rejected` | 409 | `sourcing.price_list.verify` |
| GET | `/price-lists` | List | filters | `items[{id, number, supplierPartyId, title, status, validFrom, validTo, lineCount, pendingLineCount, version, isCurrent}]` |  | `sourcing.price_list.view` |
| GET | `/price-lists/{id}` | Read |  | list header |  | `sourcing.price_list.view` |
| GET | `/price-lists/{id}/lines` | Lines | filters | `items[{id, supplierItemId, itemText, materialId, unitCode, unitPrice, currency, vatIncluded, transportMode, transportAmount, minOrderQty, leadTimeDays, validTo, lastConfirmedOn, priceExpiresOn, isStale, status, verificationStatus, flags[]}]` |  | `sourcing.price_list.view` |
| GET | `/price-list-lines` | Cross-list search for current lines | `filter[materialId]` required or `filter[supplierPartyId]` | as above + supplier |  | `sourcing.price_list.view` |
| POST | `/price-list-lines/{id}/verify` | Verify a flagged line (e.g. `price_jump`) | `note` | line | `sourcing.self_verification_not_allowed` | `sourcing.price_list.verify` |
| POST | `/price-list-lines/{id}/confirm` | Officer re-confirms a price after a phone call | `confirmedOn, note, evidenceFileId?` | line (`last_confirmed_on` updated) | `sourcing.evidence_required` 422 | `sourcing.price_list.confirm` |
| GET | `/supplier-items` | Mapping review queue | filters | `items[{id, supplierPartyId, itemText, specText, supplierUnit, mappingStatus, matchScore, candidates[{materialId, name, score}]}]` |  | `sourcing.supplier_item.view` |
| POST | `/supplier-items/{id}/map` | Map to a material | `materialId, unitCode?, packQty?, packUnitCode?` | item `mapped` | `sourcing.material_not_found` 422 | `sourcing.supplier_item.map` |
| POST | `/supplier-items/{id}/reject` | Never offer this item | `reason` | item `rejected` |  | `sourcing.supplier_item.map` |
| POST | `/supplier-items:bulk-map` | Up to 500 mappings | `items[{id, materialId}]` | per-item results |  | `sourcing.supplier_item.map` |
| GET | `/price-lists/templates` | Excel templates (column mapping) |  | `items[{id, name, columns[]}]`; template file via `GET /price-lists/templates/{id}/download` |  | `sourcing.price_list.view` |

Filters: `price-lists`: `filter[supplierPartyId]`, `filter[status]`, `filter[isCurrent]`, `filter[validTo][lte]`, sort `-createdAt`, `number`. `lines`: `filter[status]`, `filter[verificationStatus]`, `filter[isStale]`, `filter[materialId]`, `filter[mappingStatus]`, sort `unitPrice`, `-lastConfirmedOn`. `supplier-items`: `filter[mappingStatus]`, `filter[supplierPartyId]`, `q` (Arabic-normalised trigram), sort `-matchScore`, `-createdAt`.

### 7.3 Requests, invitations, offers (SRC-E1, SRC-E2)

| Method | Path | Purpose | Key request fields | Key response fields | Errors | Permission |
| --- | --- | --- | --- | --- | --- | --- |
| GET | `/requests` | Request queue | filters | `items[{id, number, prNumber, projectId, materialId, quantity, unitCode, needByDate, isUrgent, status, statusLabel, collectUntil, validOfferCount, recommendationId?, reviewDueAt?, assignedOfficerId}]` (foreman view: restricted set above) |  | `sourcing.request.view` |
| GET | `/requests/{id}` | Read with `include=invitations,offers,exceptions` |  | request + children | 404 | `sourcing.request.view` |
| POST | `/requests/{id}/collect-now` | Close the window early | `reason?` | request `evaluating` | `sourcing.request_not_collecting` 409 | `sourcing.request.manage` |
| POST | `/requests/{id}/extend-deadline` | Extend collection (also resolves exception) | `newCollectUntil, supplierIds[]?` | request `collecting` | `sourcing.deadline_after_need_date` 422 | `sourcing.request.manage` |
| POST | `/requests/{id}/invitations` | Invite another supplier | `supplierPartyId, channel` | invitation | `sourcing.supplier_ineligible` 422, `sourcing.invitation_exists` 409 | `sourcing.request.manage` |
| POST | `/invitations/{id}/resend` | Resend (rotates the token) | `channel?` | invitation | `sourcing.invitation_closed` 409 | `sourcing.request.manage` |
| POST | `/invitations/{id}/revoke` | Revoke link | `reason` | invitation |  | `sourcing.request.manage` |
| POST | `/requests/{id}/cancel` | Cancel (officer reroute) | `reason` | request `cancelled` | `sourcing.request_final` 409 | `sourcing.request.manage` |
| GET | `/requests/{id}/offers` | Offers with normalisation | `?include=normalisation` | `items[{id, supplierPartyId, origin, status, unitPrice, priceUnitCode, offeredQty, transportMode, paymentKind, creditDays, leadTimeDays, validUntil, isPriceStale, normalisation{landedAmount, creditValueAmount, effectiveAmount, earliestDeliveryDate, feasibility, flags}, exclusionCodes[]}]` |  | `sourcing.offer.view` |
| POST | `/requests/{id}/offers` | Manual offer entry (BR-SRC-28) | `supplierPartyId, offeredQty, qtyUnitCode, unitPrice, priceUnitCode, currency, fxRate?, vatIncluded, vatRatePct, discountPct?, transportMode, transportAmount?, otherChargesAmount?, paymentKind, creditDays?, advancePct?, leadTimeDays, quotedAt, validUntil, specMatch, availability, availableQty?, minOrderQty?, orderMultiple?, evidenceFileId or evidenceNote` | offer + normalisation | `sourcing.evidence_required` 422, `sourcing.fx_rate_required` 422, `sourcing.supplier_ineligible` 422, `sourcing.request_final` 409 | `sourcing.offer.enter` |
| POST | `/offers/{id}/withdraw` | Withdraw a live offer | `reason` | offer | `sourcing.offer_frozen` 409 | `sourcing.offer.enter` |
| POST | `/offers/{id}/confirm-stale` | Confirm a stale price for this request (BR-SRC-14) | `reason` (min 10 chars) | offer + recomputed flags | `sourcing.offer_not_stale` 409 | `sourcing.offer.confirm_stale` |

Filters: `requests`: `filter[status]` (multi), `filter[projectId]`, `filter[materialId]`, `filter[assignedOfficerId]`, `filter[isUrgent]`, `filter[needByDate][lte]`, `filter[prNumber]`; sort `needByDate`, `-createdAt`, `reviewDueAt`. `offers`: `filter[status]`, `filter[origin]`, `filter[supplierPartyId]`; sort `effectiveAmount`.

### 7.4 Recommendations and decisions (SRC-E3, SRC-E4)

| Method | Path | Purpose | Key request fields | Key response fields | Errors | Permission |
| --- | --- | --- | --- | --- | --- | --- |
| GET | `/recommendations` | Review queue ("awaiting review") | filters | `items[{id, number, requestId, prNumber, materialId, recommendedSupplierId, recommendedLandedAmount, validOfferCount, flags[], status, reviewDueAt, isOverdue}]` |  | `sourcing.recommendation.view` |
| GET | `/recommendations/{id}` | Full package | `?include=inputs` | see JSON 7.6.1: `ranking[], excluded[], explanation[], budgetImpact, reference, flags, formulaVersion, inputHash, decision?` | 404 | `sourcing.recommendation.view` |
| GET | `/requests/{id}/recommendation` | Current recommendation of a request |  | same | 404 if none | `sourcing.recommendation.view` |
| GET | `/recommendations/{id}/versions` | Supersession chain |  | `items[{id, version, status, readyAt}]` |  | `sourcing.recommendation.view` |
| POST | `/recommendations/{id}/recompute` | New version after late offers or expiry | `reason?` | new recommendation | `sourcing.recommendation_already_decided` 409 | `sourcing.recommendation.recompute` |
| POST | `/recommendations/{id}/approve` | Approve rank 1 | `note?` | decision (`final` or `awaiting_approval`) | `sourcing.recommendation_already_decided` 409, `sourcing.recommendation_needs_refresh` 409, `sourcing.self_decision_not_allowed` 422 | `sourcing.recommendation.decide` |
| POST | `/recommendations/{id}/adjust` | Choose another offer or change quantity | `selectedOfferId, awardedQty?, reasonCode, reasonText?` | decision | `sourcing.offer_excluded` 422, `sourcing.reason_required` 422, plus the approve errors | `sourcing.recommendation.decide` |
| POST | `/recommendations/{id}/reject` | Reject, request manual RFQ | `reasonCode, reasonText?` | decision + request `rejected_to_manual` | `sourcing.reason_required` 422 | `sourcing.recommendation.decide` |
| GET | `/recommendations/{id}/decision` | Decision and evidence |  | decision incl. `awardSnapshot`, `approvalId?` | 404 | `sourcing.recommendation.view` |
| GET | `/decisions/auto-approved` | Daily review list of automatic decisions | `filter[decidedAt][gte]` | `items[...]` |  | `sourcing.recommendation.view` |
| GET | `/exceptions` | Exceptions | filters | `items[{id, type, severity, status, requestId?, sourceId?, raisedAt, details}]` |  | `sourcing.exception.view` |
| POST | `/exceptions/{id}/acknowledge` | Acknowledge |  | exception |  | `sourcing.exception.resolve` |
| POST | `/exceptions/{id}/resolve` | Resolve | `resolution` (`manual_rfq`, `accepted_single_offer`, `extended_deadline`, `confirmed_stale_price`, `dismissed`), `reason` | exception + request status | `sourcing.exception_resolution_invalid` 422 | `sourcing.exception.resolve` |

Filters: `recommendations`: `filter[status]`, `filter[projectId]`, `filter[assignedOfficerId]`, `filter[isOverdue]`, `filter[flag]`, `filter[materialId]`, sort `reviewDueAt`, `-readyAt`. `exceptions`: `filter[status]`, `filter[type]`, `filter[severity]`, `filter[requestId]`, sort `-raisedAt`, `severity`.

### 7.5 Price book, history, alerts, policy, consolidation, reports (SRC-E3, E4 and read models)

| Method | Path | Purpose | Key request fields | Key response fields | Errors | Permission |
| --- | --- | --- | --- | --- | --- | --- |
| GET | `/price-book` | Price book grid | filters | `items[{materialId, code, nameAr, nameEn, categoryCode, unitCode, supplierCount, bestPrice, medianPrice, marketIndexPrice?, lastApprovedPrice, change30dPct, ageDays, validUntil, status}]` (section 13) |  | `sourcing.price_book.view` |
| GET | `/price-book/{materialId}` | Material detail | `?include=suppliers,quotes,orders` | row + `suppliers[], recentQuotes[], pastOrders[], history[]` | 404 | `sourcing.price_book.view` |
| GET | `/price-history` | Observations series | `filter[materialId]` required, `filter[type]`, `filter[from]`, `filter[to]`, `filter[supplierPartyId]` | `items[{observedOn, type, unitPriceExVat, supplierPartyId, qty, isOutlier}]` |  | `sourcing.price_book.view` |
| GET | `/market-alerts` | Alerts | filters | `items[{id, type, severity, materialId, details, status, raisedAt}]` |  | `sourcing.alert.view` |
| POST | `/market-alerts/{id}/acknowledge` | Acknowledge/close | `close: bool` | alert |  | `sourcing.alert.manage` |
| GET/POST/PATCH | `/market-index-series`, `/market-index-series/{id}/points` | Index series and manual points (R2); benchmarks R3 | `code, nameAr, unitCode, categoryId, points[{pointDate, value}]` | series | `sourcing.index_point_duplicate` 409 | `sourcing.index.manage` |
| GET | `/policy` | Current policy |  | policy (section 4.8) |  | `sourcing.policy.view` |
| PUT | `/policy` | Create new policy version | all fields; `weights` sum 100 | policy version n+1 | `sourcing.weights_invalid` 422, `sourcing.policy_value_invalid` 422 | `sourcing.policy.manage` |
| GET | `/consolidation-proposals` (R2) | Proposals | filters | `items[...]` |  | `sourcing.consolidation.view` |
| POST | `/consolidation-proposals/{id}/accept` / `/dismiss` (R2) | Combine requests or dismiss |  | new request | `sourcing.consolidation_not_possible` 409 | `sourcing.consolidation.manage` |
| GET | `/reports/{code}` | Reports (section 13) | \`format=json | xlsx | pdf | csv\` |

Filters: `price-book`: `filter[categoryCode]`, `filter[materialId]`, `filter[status]` (`fresh`, `aging`, `stale`, `no_price`), `q`, sort `nameAr`, `-change30dPct`, `ageDays`. `market-alerts`: `filter[type]`, `filter[status]`, `filter[materialId]`, `filter[categoryId]`, sort `-raisedAt`.

### 7.6 Supplier portal endpoints (R1 link + form, R2 full portal)

Authentication: the invitation token in the path (`aud=portal` token exchange for the R2 inbox). Rate limit 120 req/min per token (BR-SRC-57). Responses never contain other suppliers' data or the buyer's budget.

| Method | Path | Purpose | Key request fields | Key response fields | Errors |
| --- | --- | --- | --- | --- | --- |
| GET | `/portal/sourcing/rfq/{token}` | Open invitation |  | `{ number, material{name, spec}, quantity, unitCode, needByDate, deliveryRegion, deadlineAt, previousOwnPrice?, form{fields[], currencies[]} }` | `sourcing.invitation_expired` 410, `sourcing.invitation_revoked` 410, 404 |
| POST | `/portal/sourcing/rfq/{token}/offer` | Submit offer | offer fields of 7.3 (no evidence needed) | `{ receivedAt, isLate }` | `sourcing.invitation_expired` 410, `sourcing.fx_rate_required` 422 |
| POST | `/portal/sourcing/rfq/{token}/decline` | Decline | `reasonCode?` | `{ status: "declined" }` | 410 |
| GET | `/portal/sourcing/confirm/{token}` | One-tap confirmation page |  | `{ itemText, currentPrice, validTo }` | 410 |
| POST | `/portal/sourcing/confirm/{token}` | `confirmed` or `changed` | `outcome`, `newUnitPrice?`, `newValidTo?` | `{ status }` | `sourcing.confirmation_expired` 410 |
| POST | `/portal/sourcing/price-lists` (R2) | Supplier uploads a price list | `fileId, validFrom, validTo?` | `{ priceListId, status }` | `sourcing.file_type_not_allowed` 422 |

### 7.7 JSON examples

#### 7.7.1 `GET /recommendations/{id}` (golden cement example, `procurement_officer`)

```json
{
  "id": "0198a7c2-5e11-7c2b-9a53-1f0b2f6d9a01", "number": "RC-0021", "version": 1, "status": "ready",
  "requestId": "0198a7c1-9b3d-7a10-8c44-2d1e0a7c5b10", "prNumber": "PR-0412",
  "material": { "id": "0198a7a0-1111-7000-8000-000000000001", "nameAr": "أسمنت بورتلاندي CEM I 42.5N", "unitCode": "t" },
  "requestedQty": 60.0000, "needByDate": "2026-10-14", "formulaVersion": "src-1",
  "recommendedOfferId": "0198a7c3-0001-7000-8000-00000000000a",
  "ranking": [
    { "rank": 1, "offerId": "0198a7c3-0001-7000-8000-00000000000a", "supplierId": "S-A",
      "landedAmount": 183000.0000, "creditValueAmount": 3660.0000, "effectiveAmount": 179340.0000,
      "leadTimeDays": 1, "scores": { "cost": 100.0000, "lead": 100.0000, "reliability": 70.0000, "penalty": 0.0000, "total": 95.5000 }, "flags": ["over_budget"] },
    { "rank": 2, "offerId": "0198a7c3-0001-7000-8000-00000000000b", "supplierId": "S-B",
      "landedAmount": 181200.0000, "creditValueAmount": 0.0000, "effectiveAmount": 181200.0000,
      "leadTimeDays": 2, "scores": { "cost": 98.9735, "lead": 50.0000, "reliability": 70.0000, "penalty": 0.0000, "total": 87.2815 }, "flags": [] }
  ],
  "excluded": [ { "offerId": "0198a7c3-0001-7000-8000-00000000000c", "supplierId": "S-C", "exclusionCodes": ["too_late"],
                  "landedAmount": 177000.0000, "effectiveAmount": 169920.0000, "leadTimeDays": 5 } ],
  "budgetImpact": { "referenceAmount": 180000.0000, "overrunAmount": 3000.0000, "overrunPct": 1.6667, "currency": "EGP" },
  "reference": { "unitPrice": 3010.0000, "date": "2026-10-02", "supplierId": "S-B" },
  "explanation": [
    { "code": "recommended_effective_lowest", "params": { "supplier": "A", "effective": 179340.0000 } },
    { "code": "credit_offsets_price_gap", "params": { "creditValue": 3660.0000, "nominalGap": -1800.0000, "supplier": "A", "versus": "B" } },
    { "code": "alternative_cheaper_nominal", "params": { "supplier": "B", "landed": 181200.0000 } },
    { "code": "excluded_late", "params": { "supplier": "C", "leadDays": 5, "needByDate": "2026-10-14" } },
    { "code": "over_budget", "params": { "amount": 3000.0000, "pct": 1.6667 } } ],
  "flags": ["over_budget"], "reviewDueAt": "2026-10-12T17:00:00Z", "inputHash": "sha256:9f2c...e1"
}
```

(`reference.unitPrice` 3,010 is an illustrative fixture; the recommended unit 3,050 is 1.33% above it, below the 5% flag threshold.)

#### 7.7.2 `POST /recommendations/{id}/approve`

```txt
POST /api/v1/sourcing/recommendations/0198a7c2-5e11-7c2b-9a53-1f0b2f6d9a01/approve
If-Match: "17"        Idempotency-Key: 6f1d3c7e-4b0a-4c55-9b1a-0d9e2c1a7a10
{ "note": "Credit terms worth the 3,000 overrun; PM informed" }
```

```json
{ "id": "0198a7d0-0000-7000-8000-000000000001", "recommendationId": "0198a7c2-5e11-7c2b-9a53-1f0b2f6d9a01",
  "decision": "approved", "status": "final", "selectedOfferId": "0198a7c3-0001-7000-8000-00000000000a",
  "awardedQty": 60.0000, "awardUnitCode": "t", "landedAmount": 183000.0000, "currency": "EGP",
  "decidedBy": "0198a700-aaaa-7000-8000-000000000042", "decidedAt": "2026-10-12T09:14:03Z",
  "warnings": [ { "code": "sourcing.over_budget_soft", "params": { "amount": 3000.0000, "pct": 1.6667 } } ] }
```

#### 7.7.3 `POST /recommendations/{id}/adjust` (override, rank 2 chosen)

```json
{ "selectedOfferId": "0198a7c3-0001-7000-8000-00000000000b", "reasonCode": "delivery_certainty",
  "reasonText": "Supplier A failed the last two deliveries on site 3" }
```

Response `decision: "adjusted"`, `overrideOfRank: 2`, `effectiveDeltaAmount: 1860.0000` (181,200 - 179,340). Error example for a bad choice:

```json
{ "type": "https://errors.constructionconnect.example/sourcing/offer_excluded", "title": "offer_excluded", "status": 422,
  "code": "sourcing.offer_excluded", "detail": "Offer is excluded and cannot be chosen.",
  "params": { "offerId": "0198a7c3-0001-7000-8000-00000000000c", "exclusionCodes": ["too_late"] } }
```

#### 7.7.4 `POST /requests/{id}/offers` (manual entry from a phone quote)

```json
{ "supplierPartyId": "0198a6f0-0000-7000-8000-0000000000b2", "offeredQty": 60.0000, "qtyUnitCode": "t",
  "unitPrice": 2950.0000, "priceUnitCode": "t", "currency": "EGP", "vatIncluded": false, "vatRatePct": 14.0000,
  "transportMode": "flat", "transportAmount": 4200.0000, "paymentKind": "cash", "leadTimeDays": 2,
  "quotedAt": "2026-10-12T08:20:00Z", "validUntil": "2026-10-13T08:20:00Z", "specMatch": "match", "availability": "in_stock",
  "evidenceNote": "WhatsApp voice note, Eng. Hassan, 08:20" }
```

Response: offer with `normalisation{ landedAmount: 181200.0000, creditValueAmount: 0.0000, effectiveAmount: 181200.0000, earliestDeliveryDate: "2026-10-14", feasibility: "feasible" }`.

#### 7.7.5 `POST /portal/sourcing/rfq/{token}/offer`

```json
{ "offeredQty": 60.0000, "qtyUnitCode": "t", "unitPrice": 3050.0000, "priceUnitCode": "t", "currency": "EGP",
  "vatIncluded": false, "transportMode": "included", "paymentKind": "credit", "creditDays": 30, "leadTimeDays": 1,
  "validUntil": "2026-10-13T12:00:00Z", "specMatch": "match", "availability": "in_stock" }
```

Response `{ "receivedAt": "2026-10-12T07:41:10Z", "isLate": false }`; the supplier's own page shows its submitted terms only.

---

## 8. Events

Envelope per 00 §3.9; below only `payload`. Partition key = `requestId` (request, offers, recommendation events), `materialId` (price events), `sourceId` (source events). Amounts are base currency unless a `currency` field says otherwise; offers in a foreign currency also carry `fxRate`. Events are internal, so payloads include prices; visibility rules apply at read time in each consumer (BR-PRC-76 in file 06, BR-SRC-56 here).

### 8.1 Published

| Event | Fires when | Payload |
|---|---|---|
| `sourcing.request.queued.v1` | A request is created from `procurement.purchase_request.approved.v1` (state 5.1 row 1). Consumed by internal workers only (starts `start_collection`). | `requestId, requestNumber, prId, prLineId, projectId, workPackageId, costCodeId, materialId, quantity, unitCode, needByDate, isUrgent, deliveryZoneId?, assignedOfficerId` |
| `sourcing.offers.collected.v1` | Collection closes (deadline, all answered, or `collect-now`). Internal consumer: `RecommendationBuilder`. | \`requestId, closedAt, closeReason (deadline |
| `sourcing.recommendation.ready.v1` | A recommendation is stored (state 5.1 `evaluating -> ready` or `-> exception`) and again for each new version after `recompute`. | `recommendationId, recommendationNumber, version, requestId, prId, prLineId, projectId, materialId, recommendedOfferId? (null when no valid offer), recommendedSupplierId?, landedAmount?, effectiveAmount?, currency, validOfferCount, excludedCount, flags[], formulaVersion, readyAt, reviewDueAt, assignedOfficerId, exceptionType?` |
| `sourcing.recommendation.decided.v1` | A decision becomes `final` (officer within limit, approval granted, or automatic). Not fired for `awaiting_approval`. | \`recommendationId, recommendationNumber, requestId, prId, prLineId, projectId, workPackageId, costCodeId, materialId, decision (approved |
| `sourcing.price.updated.v1` | `PriceBookBuilder` finds the median moved by >= 0.5% or the month changed (BR-SRC-53). | `materialId, unitCode, currency, price (median, ex VAT, base unit), previousPrice?, changePct?, sampleSize, minPrice, maxPrice, asOfDate, validUntil, layerMix{l1, l2}` |
| `sourcing.source.failed.v1` | A source becomes `failing`, or has its first non-transient failure; once per incident (BR-SRC-11). | `sourceId, sourceCode, sourceName, incidentId, errorCode, consecutiveFailures, lastSuccessAt?, ownerUserId?, affectedSupplierId?` |

Worked payload (golden decision, `approved`):

```json
{ "recommendationNumber": "RC-0021", "decision": "approved", "supplierId": "S-A", "awardedQty": 60.0000, "unitCode": "t",
  "unitPrice": 3050.0000, "landedAmount": 183000.0000, "landedUnitPrice": 3050.0000, "effectiveAmount": 179340.0000,
  "vatRatePct": 14.0000, "vatAmount": 25620.0000, "transportMode": "included", "paymentKind": "credit", "creditDays": 30,
  "leadTimeDays": 1, "autoApproved": false, "overrideOfRank": null, "formulaVersion": "src-1" }
```

The PO commitment is `landedAmount` 183,000 (BR-SRC-48, file 06 section 4.9), never `effectiveAmount`.

### 8.2 Consumed

| Event | Handler | Action | Idempotency |
| --- | --- | --- | --- |
| `procurement.purchase_request.created.v1` | `PrCreatedHandler` | Warms `material_cache` and supplier eligibility for the lines (no request yet); nothing is stored if no line has a material | Pure cache warm, safe to repeat |
| `procurement.purchase_request.approved.v1` | `PrApprovedHandler` | For each line with `route = auto_sourcing`: creates `sourcing_requests` row `queued` (BR-SRC-17 inputs: zone, urgency, spec requirements from material), takes `SR-` number, publishes `request.queued`. Lines whose material is missing raise exception `unmatched_request_item` | Unique `(tenant_id, pr_line_id)` where live; duplicate event finds the request and returns |
| `procurement.purchase_order.issued.v1` / `.changed.v1` | `PoObservationHandler` | One observation per line (type `po`), converted to base unit, ex VAT, base currency; changed events add observations with the new price and keep the old ones | Unique `(source_ref_type='po_line', source_ref_id = poId:versionNo:lineNo hash, material_id)` |
| `procurement.purchase_order.cancelled.v1` | `PoCancelledHandler` | Marks the PO observations `is_cancelled` so the price book and reference ignore them (kept for audit) | State guard |
| `procurement.goods_receipt.posted.v1` | `ReceiptObservationHandler` | Observation type `receipt` from `acceptedUnitPrice` (only for observed unit conversions; skipped when a PO observation of the same line exists with equal price) | Unique `(receipt_line, material_id)` |
| `procurement.invoice.matched.v1` | `InvoiceObservationHandler` | Observation type `invoice` from invoice lines with `materialId` (credit notes ignored) | Unique `(invoice_line, material_id)` |
| `procurement.supplier.rated.v1` | `SupplierRatedHandler` | Updates `supplier_cache.reliability_score` and `reliability_events` | Upsert with `source_event_at` guard |
| `core.party.created/updated/blocked.v1` | `PartyCacheHandler` | Upserts `supplier_cache`; `blocked` sets `is_blocked`, closes open invitations of that supplier, flags live offers `supplier_blocked`, marks recommendations whose recommended offer is that supplier `needs_refresh` | Upsert; recompute is a state guard |
| `core.material.created/updated.v1` | `MaterialCacheHandler` | Upserts `material_cache` (validity days, base unit, `auto_sourcing_enabled`); a validity change re-runs the stale sweep for that material | Upsert with `source_event_at` guard |
| `platform.approval.approved/rejected/cancelled/escalated.v1` | `ApprovalOutcomeHandler` (`recordType = sourcing.recommendation`) | Approved: decision `final`, request `decided`, publish `decided` with `approvedBy`. Rejected/cancelled: decision `approval_rejected`, recommendation back to `ready`. Escalated: notification only | Decision state guard plus `approval_id` match |
| `platform.tenant.suspended.v1` / `.reactivated.v1` (see 8.3) | `TenantStateHandler` | Sets `tenant_collection_paused`; jobs read it (BR-SRC-58) | Flag upsert |
| `closeout.lesson.published.v1` (R3) | `LessonHandler` | Stores supplier/material lessons as input to the human-reviewed learning report | Unique `lesson_id` |

All handlers store `(consumer, event_id)` per 00 §3.9; failures use the 5-step retry and dead letter. Handlers run in their own transaction and publish follow-up events through the outbox.

### 8.3 Proposed registry additions

1. `platform.tenant.suspended.v1` and `platform.tenant.reactivated.v1` are cited by v3 and by BR-SRC-58 but are not in the 00 §7 registry. Add them with consumers: all modules that run schedulers (sourcing, planning, cash, reporting).
2. Add `sourcing` as consumer of `procurement.goods_receipt.posted.v1` and `procurement.invoice.matched.v1` (same as file 06 section 8.3 item 2) and of `procurement.purchase_order.cancelled.v1` (so a cancelled PO stops influencing reference prices).
3. Optional, not required in R1: `procurement.purchase_request.cancelled.v1` and `procurement.purchase_request.line_rerouted.v1`. File 06 section 4.9 deliberately publishes nothing on re-route; sourcing therefore detects both cases by polling `IProcurementQuery.GetPurchaseLinesForRequest` in `PrLineRouteChecker` (section 9). Adding the events would remove the polling job.
4. `sourcing.exception.raised.v1` is **not** proposed; exceptions notify through the notification service directly (section 11).

---

## 9. Background jobs and schedules

Hangfire per 00 §3.10; all jobs are idempotent, have a timeout and a `platform.job_runs` record. "PT" = per tenant with a tenant enable flag; every schedule is evaluated in the tenant's time zone (default `Africa/Cairo`). Queue `collectors` is used by adapter work so a slow supplier site cannot starve `default`.

| Job | Queue | Schedule / trigger | What | Idempotency | PT |
| --- | --- | --- | --- | --- | --- |
| `CollectionDispatcher` | `default` | every minute | Finds enabled sources whose cron is due in tenant local time (default `0 6 * * *`, from `collection_time_local`) and enqueues `CollectionRun` | Run key `(source_id, run_date, attempt)` unique (BR-SRC-02) | yes |
| `CollectionRun` | `collectors` | triggered | Runs one adapter: fetch, hash check, `PriceRowPipeline`, lines, run counters, health update; transient failure schedules retry at +15 min, +1 h, +3 h (BR-SRC-03) | Existing run for the key is returned; file hash dedupe (BR-SRC-04) | yes |
| `StartCollectionWorker` | `default` | on `sourcing.request.queued.v1` and every minute sweep for `queued` older than 1 min | Executes `queued -> collecting`: eligible suppliers, fresh offers, invitations | State guard | yes |
| `RfqDeadlineScanner` | `default` | every 5 min | Closes windows where `collect_until` passed or all invitations are answered; marks `no_reply`; publishes `offers.collected` | State guard on request | yes |
| `InvitationReminderJob` | `default` | every 15 min | Reminder to unanswered suppliers at 50% of the window (one reminder, max `reminder_count`=1 in R1) | `reminder_count` guard | yes |
| `RecommendationWorker` | `default` | on `offers.collected` | Builds inputs, runs engine, stores recommendation, publishes `ready`, evaluates auto-approval | Unique `(request_id, version)`; input hash dedupes identical recompute | yes |
| `OfferExpiryScanner` | `default` | every 10 min | Offers with `valid_until < now` that are selected or recommended: recommendation `needs_refresh`, notification (state 5.2) | Status guard | yes |
| `StalePriceSweeper` | `default` | hourly and after each `CollectionRun` | Sets `line.status = stale`, `is_stale` cache, line `expired` lists (BR-SRC-13/14) | Recomputed from dates | yes |
| `OneTapConfirmationSender` | `default` | daily 07:30 | Creates `price_confirmations` and sends links for lines expiring within `confirm_before_expiry_days`; no resend if one is `sent` | Unique open confirmation per line | yes |
| `ConfirmationTimeoutJob` | `default` | hourly | `sent` confirmations past expiry become `no_reply` | Status guard | yes |
| `PriceBookBuilder` | `default` | daily 07:30, catch-up 12:00 (after collection retries) | Medians per material, `rm_price_book` refresh, `price.updated` events (BR-SRC-53) | `(material_id, as_of_date)` marker; event only when change rule met | yes |
| `MarketAlertEvaluator` | `default` | after `PriceBookBuilder` | Spike/drop/stale-market/above-index alerts (BR-SRC-54) | Alert `dedupe_key = material:type:isoWeek` | yes |
| `SourceHealthEvaluator` | `default` | every 15 min | Health states, incidents, `source.failed` once per incident, 14-day suspension (BR-SRC-10/11) | Incident partial unique index | yes |
| `ReviewSlaScanner` | `default` | every 5 min | Reminders at 50%/100%, escalation at 150% (BR-SRC-45) | `last_reminder_stage` guard | yes |
| `ExceptionEscalationJob` | `default` | every 15 min | Critical exceptions older than SLA escalate to PM (BR-SRC-55) | `escalated_at` guard | yes |
| `PrLineRouteChecker` | `default` | every 15 min | For open requests asks `GetPurchaseLinesForRequest`; line cancelled or no longer `auto_sourcing` sets `route_check_status = line_rerouted`, cancels request | State guard | yes |
| `AutoApprovalDigest` | `default` | daily 08:00 | Review list of automatic decisions of the previous day to officer and PM | Digest key `date` | yes |
| `OutlierFlagger` | `default` | nightly 02:00 | Recomputes `is_outlier` with 90-day median and MAD (BR-SRC-52) | Deterministic per window | yes |
| `StagingPurgeJob` | `default` | daily 03:00 | Purges `staging_price_rows` > 30 days after commit; expires raw files > 90 days through the file service | Date predicate | yes |
| `rebuild_rm_price_book` | `reports` | nightly 03:30 | Full rebuild of `rm_price_book` and `rm_source_health` | Replace in one transaction | yes |
| `OverrideLearningReport` | `reports` | 1st day of month 05:00 | Overrides grouped by reason and supplier (BR-SRC-51) | Report key `yyyy-mm` | yes |
| `SupplierCacheReconciler` | `default` | weekly Sunday 04:00 | Compares `supplier_cache` with `core.parties` through contract and repairs drift | Upsert | yes |
| `ConsolidationScanner` (R2) | `default` | every 30 min | Proposals per BR-SRC-59 | Proposal unique by material + window | yes |
| `TokenCleanupJob` | `default` | hourly | Marks tokens past `deadline_at + 24 h` expired; no deletion | Date predicate | yes |

While `tenant_collection_paused` is set, `CollectionDispatcher`, `StartCollectionWorker`, `InvitationReminderJob`, `OneTapConfirmationSender` write runs as `skipped` and send nothing (BR-SRC-58).

---

## 10. Permissions

Codes `module.resource.action` (00 §3.8). Roles are the built-ins of 00 §4.4. `PM` = `project_manager`, `PO` = `procurement_officer`, `FC` = `finance_controller`, `QS` = `cost_qs_engineer`, `EST` = `estimator`, `ADM` = `tenant_admin`, `EXE` = `owner_executive`. `foreman`, `superintendent`, `client_user`, `subcontractor_user` hold **no sourcing permission** except `sourcing.request.view_status` for foreman and superintendent.

| Permission | Roles | Notes |
|---|---|---|
| `sourcing.request.view_status` | foreman, superintendent | Status and label only, own-project requests |
| `sourcing.request.view` | PO, PM, FC, QS, EXE, ADM | Project scope (BR-X-09); requires `field.cost_amounts.view` for amounts |
| `sourcing.request.manage` | PO, ADM | Collect now, extend, invite, cancel |
| `sourcing.offer.view` | PO, PM, FC, QS, ADM |  |
| `sourcing.offer.enter` | PO | Manual entry, withdraw; stored as `entered_by` (BR-SRC-43) |
| `sourcing.offer.confirm_stale` | PO (limited), PM | Needs reason; audited |
| `sourcing.recommendation.view` | PO, PM, FC, QS, EXE, ADM | Hidden for foreman/superintendent |
| `sourcing.recommendation.recompute` | PO, PM |  |
| `sourcing.recommendation.decide` | PO, PM (when delegated), FC (by policy) | **Amount limit** on landed base amount from `project_members.limits`; above limit goes to approval (BR-SRC-42/44); project scope; segregation of duties (BR-SRC-43) |
| `sourcing.exception.view` | PO, PM, FC, ADM |  |
| `sourcing.exception.resolve` | PO, PM | `accepted_single_offer` needs `recommendation.decide` as well |
| `sourcing.price_list.view` | PO, PM, FC, QS, EST, ADM |  |
| `sourcing.price_list.upload` | PO, ADM |  |
| `sourcing.price_list.verify` | PO, ADM | Not the uploader unless `allow_self_decision` (BR-SRC-15/43) |
| `sourcing.price_list.confirm` | PO |  |
| `sourcing.supplier_item.view` / `.map` | PO, ADM |  |
| `sourcing.source.view` | PO, FC, ADM, EXE |  |
| `sourcing.source.manage` / `.run` | ADM; PO for `.run` |  |
| `sourcing.source.legal_approve` | ADM only | `web_scrape` (BR-SRC-09) |
| `sourcing.price_book.view` | PO, PM, FC, QS, EST, EXE, ADM | Price and supplier columns need `field.cost_amounts.view` |
| `sourcing.alert.view` | PO, PM, FC, QS, EST, EXE |  |
| `sourcing.alert.manage` | PO, QS |  |
| `sourcing.index.manage` | ADM, EST | R2 |
| `sourcing.policy.view` | PO, PM, FC, ADM |  |
| `sourcing.policy.manage` | FC, ADM | Weights, financing rate, auto-approval; audited with before/after |
| `sourcing.consolidation.view` / `.manage` | PO | R2 |
| `sourcing.report.view` | PO, PM, FC, QS, EXE, ADM | Export audited |
| `sourcing.portal.respond` | supplier\_user (`aud=portal`) | Own invitation or confirmation only |

Field visibility (00 BR-X-06): the field group `cost_amounts` covers every price, amount, score and supplier identity in responses and exports of this module; hidden fields are omitted. `supplier_user` sees only own offer terms, `previousOwnPrice` and deadlines. Self-decision and amount-limit checks live in `DecisionService` (handler level) in addition to the endpoint attribute.

---

## 11. Notifications

Producers publish intents (00 §3.12); templates exist in `ar` and `en`, versioned. Channels: `in_app`, `push`, `email`; supplier messages use `email` (R1) and WhatsApp (R2, **\[channel to confirm\]**). Quiet hours apply except where marked. No notification ever contains another supplier's price. Foreman/superintendent receive only the status notices.

| Template code | Recipients | Trigger | Channel | Arabic sample |
| --- | --- | --- | --- | --- |
| `SRC_RFQ_INVITE` | supplier contact (external) | Invitation sent (BR-SRC-17) | email, WhatsApp (R2) | طلب عرض سعر رقم \{number\}: \{item\} بكمية \{qty\} \{unit\}، التسليم بحلول \{needBy\}. آخر موعد للرد \{deadline\}. الرابط: \{link\} |
| `SRC_RFQ_REMINDER` | supplier contact | 50% of window, no reply | email | تذكير: لم يصلنا ردكم على طلب عرض السعر \{number\}. ينتهي الموعد في \{deadline\}. |
| `SRC_CONFIRM_PRICE` | supplier contact | One-tap confirmation before expiry | email, WhatsApp (R2) | هل سعر \{item\} (\{price\} جنيه لكل \{unit\}) ما زال ساريا؟ اضغط للتأكيد أو لإدخال السعر الجديد: \{link\} |
| `SRC_REQUEST_STATUS` | requester (foreman, superintendent) and PO | Request status moves (`collecting`, `ready`, `decided`) | in\_app | حالة طلب \{item\}: \{statusLabel\}. (لا تعرض الأسعار) |
| `SRC_RECO_READY` | assigned officer | `ready` | in\_app, push | توصية جاهزة للمراجعة: \{item\}، الأفضل \{supplier\} بتكلفة فعلية \{effective\} \{currency\}. المراجعة قبل \{dueAt\}. |
| `SRC_RECO_REMINDER_50` | assigned officer | 50% of SLA | in\_app, push | توصية \{number\} تنتظر قرارك. متبقي \{remaining\} ساعة. |
| `SRC_RECO_OVERDUE` | officer, PM | 100% of SLA | in\_app, push, email | تجاوزت التوصية \{number\} مهلة المراجعة. الاحتياج في \{needBy\}. |
| `SRC_RECO_ESCALATED` | PM | 150% of SLA | in\_app, push (bypass quiet hours) | تم تصعيد التوصية \{number\} إليك لعدم اتخاذ قرار. يمكنك اتخاذ القرار عند التفويض. |
| `SRC_RECO_NEEDS_REFRESH` | officer | Selected offer expired or supplier blocked | in\_app, push | انتهت صلاحية العرض المختار في التوصية \{number\}. أعد الحساب قبل القرار. |
| `SRC_DECISION_APPROVAL_REQ` | approver (PM/FC) | Over-limit decision | in\_app, push, email (bypass quiet hours) | قرار شراء بقيمة \{amount\} \{currency\} للبند \{item\} يحتاج اعتمادك. |
| `SRC_DECISION_DONE` | PM, requester role | `decided` | in\_app | تم اعتماد توريد \{item\} من \{supplier\} بقيمة \{amount\} \{currency\}. (يظهر المورد والقيمة لمن لديه الصلاحية فقط) |
| `SRC_AUTO_APPROVED_DIGEST` | officer, PM | Daily 08:00 | email, in\_app | تم اعتماد \{count\} توصية تلقائيا أمس بإجمالي \{amount\} \{currency\}. راجعها. |
| `SRC_EXCEPTION_RAISED` | officer (critical also PM) | Exception raised | in\_app, push | استثناء في طلب \{number\}: \{typeLabel\}. اختر الإجراء المناسب. |
| `SRC_EXCEPTION_ESCALATED` | PM | Critical exception beyond SLA | in\_app, push | استثناء حرج في الطلب \{number\} لم يُعالج منذ \{hours\} ساعة. |
| `SRC_SOURCE_FAILED` | source owner, tenant admin | `source.failed` | in\_app, email | تعذر تحديث مصدر الأسعار \{sourceName\}: \{errorLabel\}. آخر نجاح \{lastSuccess\}. |
| `SRC_SOURCE_RECOVERED` | source owner | Incident auto-resolved | in\_app | عاد مصدر الأسعار \{sourceName\} للعمل. |
| `SRC_PRICE_LIST_REVIEW` | officers | List `pending_review` | in\_app | قائمة أسعار \{supplier\} تحتاج مراجعة: \{unmatched\} بند غير مطابق. |
| `SRC_MAPPING_TASK` | officer | Unit conversion missing or new unmapped items > 0 | in\_app | يوجد \{count\} بند مورد بحاجة لربطه بالمواد. |
| `SRC_MARKET_ALERT` | officer, QS, estimator | Alert opened | in\_app, push | تنبيه سوق: سعر \{material\} \{direction\} \{pct\}% خلال \{days\} أيام. |
| `SRC_OFFER_LATE` | officer | Late offer received after `ready` | in\_app | وصل عرض جديد من \{supplier\} بعد إعداد التوصية. يمكنك إعادة الحساب. |

---

## 12. Error codes

Format and mapping per 00 §3.7 (`code = sourcing.<snake_case>`). Platform codes (`precondition_failed` 412, `idempotency_key_reuse` 422, `forbidden` 403, `not_found` 404, `rate_limited` 429) are reused unchanged and not repeated.

| Code | HTTP | When | Arabic message | English message |
|---|---|---|---|---|
| `sourcing.invalid_state_transition` | 409 | Action not allowed in the current status (state machines 5.x) | الإجراء غير مسموح في الحالة الحالية | Action is not allowed in the current status. |
| `sourcing.source_config_invalid` | 422 | Adapter config fails validation (URL, mapping, cron, sender list) | إعدادات المصدر غير صحيحة | Source configuration is invalid. |
| `sourcing.source_code_duplicate` | 409 | Code already used in tenant | رمز المصدر مستخدم | Source code already exists. |
| `sourcing.web_source_not_approved` | 422 | Enabling `web_scrape` without legal basis (BR-SRC-09) | لا يمكن تفعيل مصدر الويب دون موافقة قانونية | Web source cannot be enabled without legal approval. |
| `sourcing.invalid_adapter_for_action` | 422 | Action not applicable to this adapter type | الإجراء لا ينطبق على نوع المصدر | Action does not apply to this source type. |
| `sourcing.source_disabled` | 409 | Run requested on disabled or suspended source | المصدر معطل | Source is disabled. |
| `sourcing.run_in_progress` | 409 | A run for the source is already running | هناك تشغيل جار لهذا المصدر | A run is already in progress for this source. |
| `sourcing.tenant_paused` | 409 | Collection while tenant suspended (BR-SRC-58) | التجميع متوقف لأن الحساب موقوف | Collection is paused because the tenant is suspended. |
| `sourcing.file_type_not_allowed` | 422 | Upload not xlsx/csv (R1) | نوع الملف غير مسموح | File type is not allowed. |
| `sourcing.template_unknown` | 422 | Template id not found or columns missing | قالب الأعمدة غير معروف | Column template is unknown. |
| `sourcing.price_list_has_errors` | 422 | Commit with rejected rows and `skipRejected=false` | القائمة بها أخطاء، صحح أو تجاهل الصفوف المرفوضة | The list has errors; fix them or skip rejected rows. |
| `sourcing.self_verification_not_allowed` | 422 | Verifier is the uploader (BR-SRC-15/43) | لا يمكنك اعتماد قائمة رفعتها بنفسك | You cannot verify a list you uploaded. |
| `sourcing.unresolved_lines` | 422 | Verify with unmatched or unflagged-open lines | توجد بنود غير محلولة | Unresolved lines remain. |
| `sourcing.evidence_required` | 422 | Manual entry or confirmation without attachment/note (BR-SRC-28) | المرفق أو الملاحظة المؤيدة مطلوبة | Evidence is required. |
| `sourcing.material_not_found` | 422 | Mapping target missing or inactive | المادة غير موجودة | Material not found. |
| `sourcing.request_not_collecting` | 409 | `collect-now` outside `collecting` | الطلب ليس في مرحلة جمع العروض | Request is not collecting offers. |
| `sourcing.deadline_after_need_date` | 422 | Extended deadline later than `need_by_date` | الموعد الجديد بعد تاريخ الاحتياج | New deadline is after the need-by date. |
| `sourcing.supplier_ineligible` | 422 | Supplier not approved, blocked, wrong category or documents expired (BR-SRC-16) | المورد غير مؤهل | Supplier is not eligible. |
| `sourcing.invitation_exists` | 409 | Supplier already invited to this request | تمت دعوة المورد سابقا | Supplier already invited. |
| `sourcing.invitation_closed` | 409 | Resend or revoke on a closed invitation | الدعوة مغلقة | Invitation is closed. |
| `sourcing.request_final` | 409 | Change on a decided, rejected or cancelled request | الطلب منتهي ولا يقبل التعديل | Request is final. |
| `sourcing.fx_rate_required` | 422 | Foreign currency without `fxRate` and no latest rate (BR-SRC-26) | سعر الصرف مطلوب | Exchange rate is required. |
| `sourcing.offer_frozen` | 409 | Edit or withdraw an offer frozen in a recommendation (BR-SRC-27) | العرض مثبت في توصية ولا يمكن تعديله | Offer is frozen in a recommendation. |
| `sourcing.offer_not_stale` | 409 | Stale confirmation on a fresh price | سعر العرض ما زال ساريا | Offer price is not stale. |
| `sourcing.recommendation_already_decided` | 409 | Second decision (BR-SRC-50) | تم اتخاذ قرار لهذه التوصية | Recommendation is already decided. |
| `sourcing.recommendation_needs_refresh` | 409 | Decide while `needs_refresh` (BR-SRC-46) | انتهت صلاحية العرض، أعد الحساب | Offer expired; recompute first. |
| `sourcing.approval_pending` | 409 | Decide while `pending_approval` | القرار بانتظار الاعتماد | A decision is awaiting approval. |
| `sourcing.self_decision_not_allowed` | 422 | Segregation breach (BR-SRC-43) | لا يمكنك اتخاذ قرار على عرض أدخلته بنفسك | You cannot decide on an offer you entered yourself. |
| `sourcing.offer_excluded` | 422 | Choosing an excluded offer (BR-SRC-41) | العرض مستبعد ولا يمكن اختياره | Offer is excluded and cannot be chosen. |
| `sourcing.reason_required` | 422 | Missing reason code or text (BR-SRC-41) | السبب مطلوب | A reason is required. |
| `sourcing.quantity_invalid` | 422 | Adjusted quantity \<= 0 or not allowed for the offer | الكمية غير صالحة | Quantity is invalid. |
| `sourcing.recompute_not_allowed` | 409 | Recompute after final decision or on cancelled request | لا يمكن إعادة الحساب | Recompute is not allowed. |
| `sourcing.exception_resolution_invalid` | 422 | Resolution not valid for the exception type | طريقة الحل غير صالحة لهذا الاستثناء | Resolution is not valid for this exception. |
| `sourcing.weights_invalid` | 422 | Weights negative or sum not 100 (BR-SRC-30) | مجموع الأوزان يجب أن يكون 100 | Weights must sum to 100. |
| `sourcing.policy_value_invalid` | 422 | Out-of-range policy value (rate \< 0, SLA \<= 0) | قيمة السياسة غير صالحة | Policy value is invalid. |
| `sourcing.index_point_duplicate` | 409 | Index point exists for date | نقطة المؤشر موجودة لهذا التاريخ | Index point already exists. |
| `sourcing.consolidation_not_possible` | 409 | Requests changed since proposal (R2) | لا يمكن دمج الطلبات | Requests can no longer be consolidated. |
| `sourcing.invitation_expired` | 410 | Portal token past `deadline_at + 24 h` (BR-SRC-57) | انتهت صلاحية الرابط | The link has expired. |
| `sourcing.invitation_revoked` | 410 | Token revoked or rotated | تم إلغاء الرابط | The link was revoked. |
| `sourcing.confirmation_expired` | 410 | One-tap link expired | انتهت صلاحية رابط التأكيد | The confirmation link has expired. |
| `sourcing.over_budget_soft` (warning, `200`) | 200 | Landed amount above work-package budget reference (BR-SRC-37) | القيمة تتجاوز الميزانية بمقدار \{amount\} | Amount exceeds budget by \{amount\}. |

---

## 13. Reports and read models

Per 00 §3.15: `rm_*` tables carry `as_of`, are updated by handlers/jobs and rebuilt nightly. All contain `tenant_id` and are under RLS.

### 13.1 `sourcing.rm_price_book` (one row per material per tenant)

| Column | Type | Null | Notes |
|---|---|---|---|
| `material_id` | uuid | no | PK with `tenant_id` |
| `material_code`, `name_ar`, `name_en`, `category_code` | text | no | denormalised from `material_cache` |
| `base_unit_code` | text | no | prices in base unit |
| `supplier_count` | int | no | suppliers with a fresh L1/L2 price |
| `suppliers` | jsonb | no | `[{supplierId, name, unitPrice, landedUnitPrice, layer, lastConfirmedOn, priceExpiresOn, leadTimeDays, creditDays, region}]` ordered by landed price; up to 10 |
| `recent_quotes` | jsonb | no | last 5 L2 quotes `[{supplierId, unitPrice, quotedAt, validUntil}]` |
| `past_orders` | jsonb | no | last 5 L3 `[{poNumber, supplierId, unitPrice, orderedOn, qty}]`, outliers and cancelled POs excluded |
| `best_price` | numeric(18,4) | yes | lowest fresh landed unit price |
| `median_price` | numeric(18,4) | yes | the reference published in `price.updated` (BR-SRC-53) |
| `market_index_price` | numeric(18,4) | yes | latest L4 value converted to base unit (R2/R3) |
| `approved_price` | numeric(18,4) | yes | landed unit price of the last **decided** recommendation for the material |
| `approved_price_date` | date | yes | decision date |
| `change_30d_pct` | numeric(9,4) | yes | `(median_price - median 30 days ago) / median 30 days ago * 100` from `price_observations` |
| `age_days` | int | yes | days since the freshest L1/L2 confirmation |
| `valid_until` | date | yes | earliest `price_expires_on` among prices used |
| `status` | text | no | `fresh` (>= 1 fresh price), `aging` (expires within 1 day), `stale`, `no_price` |
| `currency` | text | no | base |
| `as_of` | timestamptz | no |  |

Index `(tenant_id, category_code)`, `(tenant_id, status)`, `(tenant_id, change_30d_pct)`. Refresh: row rebuilt by `PriceBookBuilder` (daily and after a committed price list for the affected materials), `recommendation.decided` updates `approved_price`, `invoice`/`po` observation handlers update `past_orders`; nightly full rebuild.

### 13.2 Other read models

| Table | Columns (besides `tenant_id`, `as_of`) | Refresh |
|---|---|---|
| `rm_request_queue` | `request_id, number, pr_number, project_id, material_id, status, need_by_date, is_urgent, collect_until, valid_offer_count, recommendation_id, review_due_at, is_overdue, assigned_officer_id` | Handler on state changes; `is_overdue` by `ReviewSlaScanner` |
| `rm_source_health` | `source_id, code, name, layer, adapter_type, health_status, last_success_at, hours_since_success, consecutive_failures, open_incident_id, rows_accepted_last_run, rows_rejected_last_run` | `SourceHealthEvaluator` every 15 min |
| `rm_sourcing_savings` | `month, project_id, category_code, recommendations_decided, decided_amount, baseline_amount (median of other valid offers' landed), saving_amount, saving_pct, override_count, auto_approved_count` | On `decided`; monthly close of the report |
| `rm_supplier_offer_stats` | `supplier_id, invited_count, replied_count, declined_count, no_reply_count, avg_reply_hours, win_count, avg_rank, excluded_count` | On invitation/offer/decision events |

### 13.3 Reports and exports

| Code | Columns | Filters |
|---|---|---|
| `price_book` | material code, name, category, unit, supplier count, best price, median price, index price, last approved price, change 30d %, age days, valid until, status | category, status |
| `price_history` | date, material, supplier, type, unit price ex VAT, qty, outlier flag | material, from, to |
| `source_health` | source, adapter, layer, health, last success, failures, open incident | health, adapter |
| `collection_runs` | source, date, attempt, status, rows read/accepted/rejected/unmatched, error | date range, status |
| `unmapped_items` | supplier, item text, spec, unit, top candidate, score, first seen | supplier |
| `sourcing_decisions` | number, project, PR, material, qty, selected supplier, landed, effective, rank chosen, decision kind, reason, decided by, decided at, auto flag | project, date, kind |
| `override_analysis` (monthly) | reason, supplier, count, average effective delta, share of decisions | month |
| `exceptions` | type, severity, request, raised at, resolved at, resolution | status, type |
| `market_alerts` | date, material, type, change %, window, sample size | type, status |
| `savings` | month, project, category, decided amount, baseline, saving, saving % | month, project |

Exports follow 00 §3.14 (same columns as list endpoints); the price and supplier columns are removed from exports for users without `field.cost_amounts.view`; every export is audited. More than 5,000 rows queue a job and return `202`.

---

---

## 15. Test scenarios, non-functional notes, security and open questions

### 15.1 Named scenarios

| Scenario | Inputs | Expected |
|---|---|---|
| **S1 Golden cement** | PR-0412 line 1: 60 t CEM I 42.5N, need in 2 days, budget 3,000 per t (180,000), rate 2%/month, reliability default 70, weights 70/15/15. A: 3,050 per t, transport included, 30 days credit, lead 1 day. B: 2,950, transport 4,200, cash, lead 2. C: 2,900, transport 3,000, 60 days credit, lead 5 | A landed 183,000, credit value 3,660, effective 179,340, total 95.5000, rank 1. B landed 181,200, effective 181,200, cost score 98.9735, total 87.2815, rank 2. C landed 177,000, credit value 7,080, effective 169,920, excluded `too_late`. Flag `over_budget` 3,000 (1.6667%). Approval: PO commitment 183,000, event `landedAmount` 183,000, `effectiveAmount` 179,340 |
| **S2 Credit beats nominal price** | Same as S1 | Landed totals: B 181,200 is cheaper than A 183,000 by 1,800 (nominal gap -1,800 for A). A's credit value 3,660 exceeds the gap, so A is cheaper by 1,860 on effective cost (181,200 - 179,340). Explanation includes `credit_offsets_price_gap` with `creditValue 3660`, `nominalGap -1800` |
| **S3 Equal lead time allowed** | Need in 2 days, offer lead 2 days | `feasible`; lead 3 `late` |
| **S4 Single offer** | Only A valid | Request `exception` `fewer_than_two_valid_offers`; resolve `accepted_single_offer` with reason -> `ready`, flag `single_offer`; automatic approval blocked |
| **S5 No valid offer** | All offers late | Exception `no_valid_offer` (critical), `ready` event with `recommendedOfferId` null, no recommendation row; reject -> `rejected_to_manual` |
| **S6 Override** | Officer picks B for `delivery_certainty` with 20-char text | Decision `adjusted`, `override_of_rank` 2, effective delta 1,860; PO draft from B's snapshot; report counts one override |
| **S7 Over limit** | Officer limit 150,000; landed 183,000 | `pending_approval`; PM approves -> `decided` event with `approvedBy`; PM rejects -> `ready` again |
| **S8 Stale price** | Cement validity 2 days, last confirmed 10 Oct; used on 13 Oct | Offer excluded `price_stale_unconfirmed`; after `confirm-stale` with reason ranked with flag `stale_price_confirmed` |
| **S9 Expiry before decision** | Recommended offer `valid_until` passes at 14:00, officer approves at 15:00 | 409 `recommendation_needs_refresh`; recompute creates version 2 |
| **S10 Unit conversion** | Supplier prices 152.5 per 50 kg bag; request 60 t | 1,200 bags; unit price 3,050 per t; landed 183,000 |
| **S11 VAT included** | Supplier quotes 3,477 per t incl. 14% VAT | Ex VAT 3,050.0000 |
| **S12 Min order and multiple** | Min 100 t, request 60 t; second offer multiple 50 t | First `below_min_order`; second quantity 100 t, flag `qty_rounded_up`, landed on 100 t |
| **S13 Price jump** | Previous 3,000, new 4,300 | Line `unverified`, flag `price_jump`, not used until verified |
| **S14 Source failure** | API returns 500 three times then 200 | Attempts at +15 min, +1 h, +3 h; success on attempt 4 clears; no incident if recovered within the day, incident and `source.failed` after two failed days |
| **S15 Late reply** | Supplier replies after window, before `deadline_at + 24 h` | Offer `received`, `is_late`; recommendation unchanged; UI `newer_offers_available`; after 24 h token returns 410 |
| **S16 Price book** | Fresh prices 3,050, 3,100, 3,000 on day 1; 3,060 median day 2 | Day 1 median 3,050 event; day 2 +0.33% no event; median 3,065 (+0.49%) no event; 3,070 (+0.66%) event |
| **S17 Outlier** | 5 samples 3,000-3,100 and one 4,500 | Flagged outlier (more than 3 MAD), excluded from median, kept in history |
| **S18 Visibility** | Foreman opens request | Only status fields; no price key anywhere; exports contain none |
| **S19 Reproducibility** | Re-run S1 stored input | Byte-identical result and same `input_hash` |
| **S20 Tenant isolation** | Two tenants with S1 data | No cross-tenant read or event handling |

### 15.2 Non-functional notes

| Area | Volumes and patterns | Target |
|---|---|---|
| Volumes (per tenant per year) | 100 sources; 300 daily runs; price list lines 200k active, 2M history; offers 40k; requests 10k; observations 1M (50M platform-wide triggers partitioning by month) | n/a |
| Collection | 06:00 burst for all tenants in Cairo time; adapters on queue `collectors` with per-host concurrency 2; jittered start within 5 minutes per tenant | All due runs queued \< 60 s; a 5,000-row file processed \< 60 s |
| Hot queries | Offers by material and freshness (`(tenant_id, material_id, status, last_confirmed_on desc)`); request queue by `(tenant_id, status, assigned_officer_id)`; recommendation queue by `review_due_at`; price history by `(tenant_id, material_id, observed_on desc)` | p95 \< 300 ms single, \< 800 ms list of 50 |
| Engine | Pure CPU, no I/O; 20 offers | \< 5 ms |
| Recommendation latency | From `offers.collected` to `ready` | p95 \< 10 s |
| Outbox | Events per decision 1-2 | lag p95 \< 5 s (00 §3.17) |
| Read models | `rm_price_book` row per material (about 2,000) | rebuild \< 30 s per tenant |
| Idempotency | All consumers and decisions | duplicates produce no second PO, observation or invitation |
| Retention | Raw files 90 days; staging 30 days; recommendations, decisions, offers, observations indefinitely | per 00 |
| Observability | Metrics: run duration and outcome per source, rows rejected rate, time to `ready`, SLA breaches, exceptions open, outlier rate, portal 4xx/429, token failures | alerts on `failing` sources and DLQ |

### 15.3 Security notes

- Portal tokens: 256-bit random, hash only stored, scoped to one `(request, supplier)`, revocable, expire `deadline_at + 24 h`, rate limited 120/min; the portal DTOs are separate classes without cost fields of the buyer (budget, other offers).
- Secrets for APIs and mailboxes live in the secret store; `secret_ref` only in the database; logs and run errors pass a redactor; `error_detail` never stores raw payloads containing credentials.
- Inbound email: sender allow-list, antivirus scan through the file service, no macros execution, attachment types whitelist; file hash stored.
- Uploaded sheets are parsed by a sandboxed parser with row/size caps (50 MB, 100k rows) to resist zip bombs and formula injection; cells starting with `=`, `+`, , `@` are treated as text on export.
- Web adapter off by default and legally gated (BR-SRC-09); outbound calls only from the `collectors` workers through the egress proxy with host allow-list.
- Segregation of duties and amount limits are enforced in handlers, not only in controllers; every decision, stale confirmation, manual offer, policy change and export is audited with before/after.
- Price confidentiality: DTOs tagged `cost_amounts`, omitted for site roles; supplier A never sees supplier B prices, even aggregated (no "your price is above the lowest" hints in R1).
- Data protection: supplier contact names and numbers are personal data; minimal fields in events; portal access logs retained 90 days.

### 15.4 Open questions (for the business)

1. **Web price collection.** v3 states "we do not scrape supplier websites". May we collect prices from public supplier pages or official indices under written permission, and who signs the legal basis (BR-SRC-09)? Until answered the web adapter stays R2 and off.
2. **VAT treatment.** Are all comparisons correct ex VAT because input VAT is recoverable for every tenant, or are there tenants (exempt activities) for whom the VAT-inclusive price is the real cost? Which of the two must the engine use per tenant?
3. **Budget check dependency.** May sourcing read the work-package budget reference through a cost contract (`IBudgetCheck`, new dependency `sourcing -> cost`), or must procurement attach the budget rate to the purchase request line instead?
4. **Approval reuse.** When the officer's over-limit decision has been approved by the PM or finance, should the resulting PO skip its own approval for the same amount, or must it be approved again?
5. **Split awards.** Do you want one request split between two suppliers (for example 40 t from A and 20 t from B when no single supplier has enough stock)? R1 excludes `partial_quantity` offers. If yes, the engine needs an allocation rule.
6. **Financing rate owner.** Is there one company financing rate (2% per month) used by both manual and automatic comparison, owned by finance, or may procurement and sourcing differ?
7. **Default reliability.** Is 70 the right neutral score for suppliers without history, and should new suppliers be prevented from winning on reliability alone?
8. **Validity per category.** Are the validity days (steel 1, cement 2, ready-mix 2, aggregates 3, plumbing 7, paints 7, finishing 30) final, and who may override per material?
9. **Review SLA.** Are 8 hours (2 hours when urgent) acceptable, and should the SLA count working hours and weekends (Friday, Saturday)?
10. **Automatic approval.** Do you want automatic approval at all in the first release, and if so what amount ceiling and which materials?
11. **Supplier channels.** Which channels are acceptable for RFQ at launch: email only, or WhatsApp business messages (cost and Meta approval)? Who pays?
12. **Transport estimates.** For `ex_works` offers, who supplies the transport estimate (officer, fixed rate card per zone), and should delivery zones carry a standard rate?
13. **Late offers.** Should an offer arriving after the deadline but before the decision be allowed to change the recommendation automatically for urgent requests, or always require officer recompute?
14. **Reference price.** Is "recent purchase in the last 90 days across all projects" the right comparison for the 5% flag, or should it be same region and similar quantity only?
15. **Supplier price confidentiality.** May a supplier ever be told that their price was not competitive (without numbers), or never?
