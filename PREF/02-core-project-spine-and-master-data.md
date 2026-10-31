---
title: "02 Core project spine and master data"
doc_no: "02"
section: "Foundation"
status: "Proposal"
source: https://app.notion.com/p/95c4cdd2129682749a4a0105d20e3c6d?pvs=204
exported: 2026-10-29 (Notion MCP)
---

# 02 — Core: Project Spine and Master Data (key `COR`, schema `core`)

**Audience:** backend team. **Conventions:** this file obeys `00-Backend-Architecture-and-Conventions.md` (called "00" below: standard columns 00 §3.2, API rules 00 §3.6, errors 00 §3.7, events 00 §3.9 and §7, ticket template 00 §10). It does not restate them. <br> **Business source:** v3 spec section 3 (project spine, trade library 3.2), 4.0.2 (shared data model), 4.0.3 (setup wizard). <br> **Release:** the whole module is R1 (it is the foundation every other module reads). Exceptions are marked: BOQ item catalog \[R2\], automatic exchange-rate feed \[R2\].

---

## 1. Purpose and business context

### 1.1 In plain language

A construction company runs many projects at once. On each project it buys cement, hires plumbers, pays suppliers and bills the client. For the owner to ask "how is plumbing doing on Building 3?" and get one honest answer, every purchase order, delivery, invoice and progress entry must point at the **same project, the same trade, the same cost code and the same supplier record**. If one engineer types the supplier as "Al Nasr Co." and another as "النصر للتوريدات", the company cannot see how much it owes that supplier. If one project calls plumbing "Sanitary" and another "Plumbing", the company cannot compare them.

Module **Core** is the module that creates and guards those shared records. It owns no money. It owns the **vocabulary** of the company:

| Concept | What it is, in business words | Example |
|---|---|---|
| Company settings | The legal identity and defaults of the contractor | "Delta Construction", tax registration number, EGP, VAT 14%, fiscal year January to December |
| Units and conversions | How quantities are measured and how one unit turns into another | 1 tonne = 1,000 kg; 1 bag of cement = 50 kg, so 60 t = 1,200 bags |
| Tax codes | VAT and other taxes with rates and effective dates | VAT 14% |
| Fiscal calendar | The accounting years and months of the company | Period 10 of 2026 = 1 Oct to 31 Oct |
| Exchange rates | Dated rates when a contract or supplier uses USD, SAR, EUR | 1 USD = 48.50 EGP on 2026-10-01 |
| Project | One construction job: client, place, value, dates | "Golden Compound, 6 buildings" |
| Zone (منطقة / مرحلة) | A physical part of a project | Building 3, Floors 1-4 |
| Work package (حزمة عمل) = **trade** (بند / تخصص) | The unit of **budget, spend, progress and timeline** | Plumbing and drainage, Building 3 |
| Cost code (كود التكلفة) | A finer label under a trade that says what kind of cost it is | `07.M.010` Supply pipes and fittings (material) |
| Party (طرف) | Any outside organisation: supplier, subcontractor, client, bank, consultant | A cement supplier |
| Material master (دليل المواد) | The company's catalogue of what it buys, with Arabic and English names, so that quotes, orders and deliveries talk about the same thing | "Portland cement 42.5 N, 50 kg bag" |
| Trade template (قالب) | A ready list of trades for a type of project, so a new project starts in minutes | Residential, commercial, infrastructure, fit-out |

Glossary used in this file: **BOQ** (bill of quantities, جدول الكميات) is the priced list of work items in the client contract; **BAC** (budget at completion) is the total budget of a trade; **tenant** is one contractor company in the system; **go-live** is the moment a project is declared ready for daily use.

### 1.2 How it fits the lifecycle and flows F1-F7

```txt
Tender won (Estimating) --creates draft--> PROJECT --setup wizard--> go-live --> daily flows F1-F7 --> close (Closeout)
```

- F1 (site request to purchase) uses `project_id`, `zone_id`, `work_package_id`, `material_id`, `unit_code`.
- F2 (buy: request, RFQ, PO, receipt, invoice, payment) uses `party_id` (supplier), `material_id`, `cost_code_id`, `tax_code`.
- F3-F7 (cost, contract, cash, subcontract, closeout) use the same keys plus `fiscal_period`, `exchange_rate`.
- Rule BR-X-01 (00 §11) says every transaction carries tenant, project, work package, cost code and party. **This module defines and validates those ids**; other modules store them by value and call Core contract queries to validate on write.

### 1.3 Why correctness here matters (the detail)

1. **Single source of identity.** Duplicate suppliers and duplicate materials split totals and break the "what do we owe X" question. Core therefore detects duplicates by tax id, normalised Arabic name and alias, and supports a controlled **merge**.
2. **Money safety.** A supplier's bank account is the classic fraud point. A changed account must be verified by a second person before payments can use it.
3. **Blocked suppliers.** A supplier blocked for poor performance or legal reasons must stop receiving new orders immediately everywhere. Core owns the blocked flag and publishes `core.party.blocked.v1`; Procurement, Sourcing and Subcontracts must also check it on write.
4. **Fast project start.** Small companies will not tolerate a 2-day setup. The wizard plus trade templates must take a project from empty to "ready to go live" in about 30 minutes, and the go-live checklist shows exactly what is missing.
5. **Matching text to materials.** Site users write "اسمنت" or "أسمنت بورتلاند 50 كيلو"; suppliers' quotes write "Cement CEM I 42.5N bag". The material master holds aliases in Arabic and English so Sourcing can match free text to one material, and holds a **validity period per category** (how many days a quoted price stays valid: reinforcement steel 1 day, cement 2 days, plumbing items 7 days, finishing items 30 days) which Sourcing uses to age offers.
6. **History.** Master data that has been used is never deleted, only deactivated or merged. Transactions copy the facts they need (BR-X-03), so editing a name never rewrites history.

---

## 2. Users and roles

Built-in roles per 00 §4.4. "Edit" below means create and update.

| Role | What they do in Core |
|---|---|
| `tenant_admin` | Company settings, currencies, units, tax codes, fiscal calendar, trade library, cost-code structure, trade templates, seed re-apply, party and material administration, merges |
| `owner_executive` | Views everything; creates projects; may override a failing go-live checklist with a reason; reopens closed projects |
| `project_manager` | Runs the setup wizard for own projects, edits zones and work packages, activates/holds/closes projects (close requires `core.project.close`), requests new parties and materials |
| `finance_controller` | Edits exchange rates and tax codes (with `tenant_admin`), fiscal calendar, **verifies supplier bank accounts**, blocks/unblocks parties, sees bank accounts |
| `procurement_officer` | Creates and edits suppliers and materials (new records may be `pending_review` if the tenant enables review), edits aliases, requests a party block |
| `cost_qs_engineer` | Edits cost codes, work-package cost-code links, material categories and unit conversions; sees reference rates in the BOQ catalog |
| `contracts_manager` | Creates and edits clients and consultants; links the client to the project |
| `superintendent`, `foreman` | Read-only lookups of zones, work packages, materials, units, suppliers' names. **Hidden from them:** project `contract_value`, party bank accounts and payment terms, BOQ catalog reference rates, exchange rates (BR-X-06) |
| `estimator` | Edits BOQ catalog items \[R2\], reads materials and cost codes; Estimating creates project drafts through events, not through this role |
| `document_controller`, `qa_qc_engineer`, `safety_officer` | Read-only lookups; `document_controller` may edit consultant contacts |
| External (`supplier_user`, `subcontractor_user`, `client_user`) | No direct Core access. Portals read the party's own record (name, contacts, tax ids) through the portal module, never the Core endpoints |
| System | Seeder (tenant provisioning), nightly consistency job, rate importer \[R2\], event handler for tender-won |

---

## 3. Services and components

Aggregates carry rules and publish events; the rest is CRUD with validation.

| Component | Kind | Responsibility |
|---|---|---|
| `CompanySettingsService` | CRUD (single row per tenant) | Read/update legal identity and defaults; validates currency, timezone, fiscal start |
| `UnitCatalog`, `UnitConverter` (`IUnitConverter`) | CRUD + calculator | Unit CRUD; `Convert(qty, from, to, materialId?)` using `to_base_factor` for same dimension and `unit_conversions` for cross-dimension and material pack conversions; rejects unknown paths |
| `TaxCodeService` (`ITaxCodeProvider`) | CRUD + resolver | Versioned tax codes; `Resolve(code, onDate)` returns the rate in force |
| `FiscalCalendarService` (`IFiscalCalendar`) | CRUD + generator | Years and periods; `PeriodOf(date)`; generation of 12 periods; overlap prevention. **Does not store open/closed** (that is Cost, period close) |
| `ExchangeRateService` (`IExchangeRateProvider`) | CRUD + resolver | `GetRate(from, to, date, type)` with staleness rule; bulk entry; import \[R2 feed\] |
| `HolidayService` | CRUD | Company/project holidays used by Planning |
| **`Project`**** aggregate** + `ProjectSetupService` | Aggregate | State machine, setup steps, go-live gate, close; publishes project events |
| `ProjectReadinessEvaluator` | Domain service | Evaluates the go-live checklist: own checks plus plug-in providers `IProjectReadinessProvider` implemented by other modules (see 4.3.4) |
| `ZoneService` | CRUD with tree rules | Zone tree, floor generator, archive/move |
| **`WorkPackage`**** aggregate** | Aggregate | Lifecycle, uniqueness, cost-code links, archive guard via usage providers; publishes events |
| `TradeLibraryService` | CRUD | Per-tenant trades (19 default + 3 optional), default units and default cost codes |
| `TradeTemplateService` + `TemplateApplier` | CRUD + calculator | Templates; `Apply(project, template, options)` creates work packages and links cost codes, idempotently |
| `CostCodeService` (`ICostCodeDirectory`) | CRUD with tree rules | Hierarchy, postable leaves, deactivate, move; events |
| **`Party`**** aggregate** | Aggregate | Roles, identifiers, contacts, bank accounts with verification, block/unblock, merge, duplicate detection; publishes events |
| `PartyDuplicateDetector` | Domain service | Tax-id, normalised-name and trigram checks |
| `MaterialCatalogService` (`IMaterialCatalog`) | CRUD + matcher | Categories, spec attributes, materials, aliases; `Match(text, hints)` returns ranked candidates |
| `MaterialMergeService` | Domain service | Merge duplicate materials, re-pointing aliases and recording the redirect |
| `BoqCatalogService` \[R2\] | CRUD | Shared standard BOQ item catalog |
| `TenantSeeder` | System | Idempotent seed of units, tax codes, currencies, trade library, cost codes, material categories and \~300 materials, templates; re-apply updates only non-customised rows (`seed_key`, `is_customized`) |
| `ReferenceUsageService` (`IReferenceUsageProvider`) | Contract | Asks every module "is this party/work package/cost code/material used?" before deactivate, archive, merge |
| `TenderWonHandler` | Event consumer | Creates a draft project from `estimating.tender.won.v1` |
| `RatingCacheHandler` | Event consumer | Stores supplier rating snapshots (read-only in Core) |
| Jobs | see section 9 | `core_consistency_check`, `core_duplicate_scan`, `core_fx_stale_check`, `core_fiscal_year_reminder`, `core_rate_import` \[R2\] |

**Contract queries Core exposes (in ****`Core.Contracts`****)**, used by every other module and by Reporting:

| Interface | Methods | Notes |
|---|---|---|
| `IProjectDirectory` | `Get(projectId)`, `Search(filter)`, `GetVisibleProjectIds(userId)` | Status, dates, currency, client party id, timezone |
| `IWorkPackageDirectory` | `Get(id)`, `ListByProject(projectId, status?)`, `Validate(projectId, workPackageId, costCodeId?)` | `Validate` is the write-time check required by BR-X-01 |
| `ICostCodeDirectory` | `Get(id)`, `Resolve(code)`, `IsPostable(id)`, `AllowedForWorkPackage(wpId, costCodeId)` |  |
| `IPartyDirectory` | `Get(id)`, `Search`, `IsBlocked(id, scope)`, `GetPrimaryBankAccount(id)` (verified only) | Bank data only for callers with permission |
| `IMaterialCatalog` | `Get(id)`, `Match(text, categoryHint?, supplierId?)`, `GetValidityDays(materialId)` | Used by Sourcing and Procurement |
| `IUnitConverter` | `Convert(qty, from, to, materialId?)` | Throws `core.unit_conversion_unavailable` |
| `ITaxCodeProvider` | `Resolve(code, date)` |  |
| `IExchangeRateProvider` | `GetRate(from, to, date, rateType)` |  |
| `IFiscalCalendar` | `PeriodOf(date)`, `YearOf(date)`, `PeriodsOfYear(year)` |  |

**Contracts Core consumes from other modules (plug-ins):**

| Interface (defined in `Core.Contracts`, implemented by) | Purpose |
|---|---|
| `IProjectReadinessProvider` (Contracts, Cost, Planning, Platform) | Go-live checklist items owned by those modules |
| `IReferenceUsageProvider` (Procurement, Sourcing, Cost, Contracts, Subcontracts, Planning, Estimating, Docs) | Usage counts for deactivate/archive/merge guards |

---

## 4. Domain model

All tables are in schema `core`, have `tenant_id` with RLS, and carry the standard columns of 00 §3.2 (`id`, `tenant_id`, `created_at/by`, `updated_at/by`, `row_version`, `is_deleted`, `source`), which are **omitted from the column tables below**. Master data is soft-deleted only (`is_deleted`) and only when unused; "used" is decided by `ReferenceUsageService` (section 3). Tables marked **(append-only)** have no `updated_*` and no deletes. Table-level notes: "FK" = real foreign key inside `core`; "by value" = uuid with no constraint (00 §5.4). Percent values are stored as percent numbers in `numeric(9,4)` (`14.0000` = 14 %). Every composite index starts with `tenant_id` (not repeated in the index lists). Text codes use collation `und-x-icu` and are normalised (trim, upper-case Latin, Arabic-Indic digits to Western) by the application before insert.

Seeded rows carry `seed_key text null` (stable identifier from the seed file) and `is_customized bool default false` (set to true by any user edit); the seeder re-applies only rows where `is_customized = false` (BR-COR-30).

### 4.1 Company settings, units, tax, calendar, exchange rates (epic COR-E1)

#### 4.1.1 `core.company_settings` (one row per tenant)

| column | type | null | notes / constraints |
|---|---|---|---|
| `legal_name_ar` | text | no |  |
| `legal_name_en` | text | yes |  |
| `trade_name` | text | yes | display name on documents |
| `commercial_register_no` | text | yes | السجل التجاري |
| `tax_registration_no` | text | yes | الرقم الضريبي; format check configurable (9 digits for Egypt by default) |
| `tax_office` | text | yes |  |
| `address_ar`, `address_en` | text | yes |  |
| `city`, `governorate` | text | yes |  |
| `country_code` | text | no | ISO-3166 alpha-2, default `EG` |
| `phone`, `email`, `website` | text | yes |  |
| `logo_file_id` | uuid | yes | `platform.files` by value |
| `base_currency` | text | no | ISO-4217, default `EGP`; **immutable once any project exists** (BR-COR-01) |
| `default_timezone` | text | no | IANA, default `Africa/Cairo` |
| `default_language` | text | no | `ar` or `en`, default `ar` |
| `numeral_system` | text | no | `western` or `arabic_indic`, default `western` (display only) |
| `calendar_display` | text | no | `gregorian` or `hijri` (display only; storage is always Gregorian, 00 §3.5) |
| `fiscal_year_start_month` | int | no | 1-12, default 1 |
| `weekend_days` | int\[\] | no | ISO day numbers, default `{5,6}` (Friday, Saturday) |
| `default_vat_tax_code_id` | uuid | yes | FK `tax_codes` (type `vat`) |
| `default_payment_terms_days` | int | yes | >= 0 |
| `party_review_required` | bool | no | default false: new parties created by non-admins start `pending_review` |
| `material_review_required` | bool | no | default false: same for materials |
| `fx_max_age_days` | int | no | default 7; staleness window for exchange rates (BR-COR-06) |
| `schedule_required_for_go_live` | bool | no | default false: makes the schedule check a blocker |
| `seed_version` | text | yes | last seed version applied |
| `onboarding_completed_at` | timestamptz | yes |  |

Keys: unique `(tenant_id)`. Checks: `fiscal_year_start_month between 1 and 12`; `numeral_system in (...)`; `calendar_display in (...)`.

#### 4.1.2 `core.currencies`

| column | type | null | notes |
|---|---|---|---|
| `code` | text | no | ISO-4217 (`EGP`, `USD`, `SAR`, `EUR`, `AED`, `GBP`) |
| `name_ar`, `name_en` | text | no |  |
| `symbol` | text | yes |  |
| `decimals` | int | no | display decimals, default 2 (storage is always 4) |
| `is_enabled` | bool | no | tenant enables currencies it may use on documents |

Keys: unique `(tenant_id, code)`. The base currency row cannot be disabled.

#### 4.1.3 `core.units`

| column | type | null | notes |
|---|---|---|---|
| `code` | text | no | machine code, e.g. `m3`, `m2`, `kg`, `t`, `bag`, `pt`, `ls`, `month` |
| `name_ar`, `name_en` | text | no | e.g. `متر مكعب` / `cubic metre` |
| `symbol_ar`, `symbol_en` | text | yes | `م3` / `m³` |
| `dimension` | text | no | `length`, `area`, `volume`, `mass`, `count`, `time`, `lump`, `power`, `other` |
| `base_unit_code` | text | yes | null for the base unit of a dimension; otherwise the base unit's `code` |
| `to_base_factor` | numeric(18,8) | yes | quantity in base = quantity x factor; 1 for base units; null for `other` and for pack units whose conversion is material-specific |
| `quantity_decimals` | int | no | decimals allowed on data entry (0 for `ea`, `pt`; 3 for `m3`), default 2 |
| `is_system` | bool | no | seeded unit; code cannot change or be deleted |
| `is_active` | bool | no |  |
| `sort_order` | int | no |  |

Keys: unique `(tenant_id, code)`. Checks: `to_base_factor > 0`; `dimension in (...)`; `(base_unit_code is null) = (to_base_factor = 1 or to_base_factor is null)` for base units is enforced in the service. FK `(tenant_id, base_unit_code)` references `units(tenant_id, code)`. <br> Seeded system units (about 40): length `m`, `cm`, `mm`, `km`; area `m2`; volume `m3`, `l`; mass `kg`, `t`; count `ea` (عدد), `pt` (نقطة), `set` (طقم), `pc` (قطعة), `unit` (وحدة), `bag` (كيس), `roll` (لفة), `drum` (برميل), `pallet`, `sheet` (لوح), `bar` (سيخ), `panel` (لوحة); time `day`, `week`, `month`, `hour`; lump `ls` (مقطوعية); power `kw`, `tr` (طن تبريد). Base units: `m`, `m2`, `m3`, `kg`, `ea`, `day`, `ls`, `kw`.

#### 4.1.4 `core.unit_conversions`

Conversions that are **not** simple same-dimension scaling: pack sizes and densities, general or per material.

| column | type | null | notes |
| --- | --- | --- | --- |
| `from_unit_code` | text | no |  |
| `to_unit_code` | text | no | must differ from `from_unit_code` |
| `factor` | numeric(18,8) | no | `qty_to = qty_from x factor`, > 0 |
| `material_id` | uuid | yes | FK `materials`; null = applies to all materials |
| `kind` | text | no | `pack` (1 bag = 50 kg), `density` (1 m3 concrete = 2,400 kg), `cross` (other) |
| `note` | text | yes |  |

Keys: unique `(tenant_id, from_unit_code, to_unit_code, coalesce(material_id, '00000000-0000-0000-0000-000000000000'))`. Index `(material_id)`. Resolution order is in BR-COR-02.

#### 4.1.5 `core.tax_codes` and `core.tax_code_rates`

`tax_codes`:

| column | type | null | notes |
|---|---|---|---|
| `code` | text | no | `VAT14`, `VAT0`, `VATEX` (exempt), `WHT3` ... |
| `name_ar`, `name_en` | text | no |  |
| `tax_type` | text | no | `vat`, `withholding`, `stamp`, `other` |
| `applies_to` | text | no | `purchases`, `sales`, `both` |
| `is_recoverable` | bool | no | input VAT recoverable (default true for `vat` on purchases) |
| `is_default` | bool | no | at most one default per `tax_type` and `applies_to` (partial unique index) |
| `is_active` | bool | no |  |

Keys: unique `(tenant_id, code)`.

`tax_code_rates` (rate history, **never edited, only superseded**):

| column | type | null | notes |
|---|---|---|---|
| `tax_code_id` | uuid | no | FK `tax_codes` |
| `rate_percent` | numeric(9,4) | no | 0 \<= rate \<= 100 |
| `effective_from` | date | no |  |
| `effective_to` | date | yes | null = open; ranges of one tax code must not overlap (exclusion constraint on `daterange`) |

Seed: `VAT14` 14.0000 from 2000-01-01 (default sales and purchases), `VAT0`, `VATEX`, `WHT` codes as configurable placeholders with rate 0 and `is_active=false` **\[confirm Egyptian withholding and stamp rules with an accountant\]**.

#### 4.1.6 `core.fiscal_years` and `core.fiscal_periods`

`fiscal_years`:

| column | type | null | notes |
|---|---|---|---|
| `year_label` | int | no | e.g. 2026 (the year in which the fiscal year starts when it does not start in January, configurable label rule in BR-COR-07) |
| `start_date`, `end_date` | date | no | `end_date >= start_date`; contiguous with previous year (service check) |
| `status` | text | no | `planned`, `current`, `past` (derived by job `core_fiscal_year_reminder`; informational) |

Keys: unique `(tenant_id, year_label)`; exclusion constraint on `daterange(start_date, end_date, '[]')` per tenant.

`fiscal_periods`:

| column | type | null | notes |
|---|---|---|---|
| `fiscal_year_id` | uuid | no | FK |
| `period_no` | int | no | 1-12 (13 allowed for an adjustment period) |
| `start_date`, `end_date` | date | no |  |
| `name_ar`, `name_en` | text | no | e.g. `أكتوبر 2026` / `Oct 2026` |

Keys: unique `(tenant_id, fiscal_year_id, period_no)`; exclusion constraint no overlap per tenant. Index `(start_date, end_date)` for `PeriodOf(date)`. **Open/closed status is not stored here**; Cost owns `cost.cost_periods` and references `fiscal_periods.id` by value (BR-COR-08).

#### 4.1.7 `core.exchange_rates`

| column | type | null | notes |
|---|---|---|---|
| `from_currency` | text | no |  |
| `to_currency` | text | no | default tenant base currency; both must be enabled in `currencies` |
| `rate_type` | text | no | `spot` (daily), `contract` (fixed by a contract), `budget` (planning rate) |
| `rate` | numeric(18,8) | no | 1 unit of `from_currency` = `rate` units of `to_currency`; > 0 |
| `rate_date` | date | no |  |
| `valid_to_date` | date | yes | for `contract`/`budget` rates only |
| `source_kind` | text | no | `manual`, `import`, `feed` \[R2\], `bank_quote` |
| `reference` | text | yes | e.g. bank notice number |
| `project_id` | uuid | yes | FK `projects`; contract rates may be project-specific |

Keys: unique `(tenant_id, from_currency, to_currency, rate_type, rate_date, coalesce(project_id, zero-uuid))`. Check `from_currency <> to_currency`. Index `(from_currency, to_currency, rate_type, rate_date desc)`. Visibility: hidden from `foreman` and `superintendent` (BR-X-06 reading of matrix: costs and rates).

#### 4.1.8 `core.holidays`

| column | type | null | notes |
| --- | --- | --- | --- |
| `scope` | text | no | `company` or `project` |
| `project_id` | uuid | yes | required when scope = `project` |
| `holiday_date` | date | no |  |
| `kind` | text | no | `holiday` (non-working) or `working_day` (override of a weekend) |
| `name_ar`, `name_en` | text | no |  |

Keys: unique `(tenant_id, scope, coalesce(project_id, zero-uuid), holiday_date)`. Seeded: no dates (Egyptian public holidays vary by lunar calendar; the tenant admin enters them yearly; open question 6).

### 4.2 Projects, setup and readiness (epic COR-E2)

#### 4.2.1 `core.projects`

| column | type | null | notes |
|---|---|---|---|
| `code` | text | no | business number from `platform.number_series` series `PRJ` (default `P-2026-001` pattern, per tenant); unique `(tenant_id, code)` |
| `name_ar` | text | no |  |
| `name_en` | text | yes |  |
| `project_type` | text | no | `residential`, `commercial`, `infrastructure`, `fit_out`, `industrial`, `mixed` |
| `status` | text | no | see 4.2.6 and section 5 |
| `client_party_id` | uuid | yes (required to leave `draft`) | FK `parties`; party must have role `client` |
| `consultant_party_id` | uuid | yes | FK `parties`; role `consultant` |
| `contract_type` | text | yes | `lump_sum`, `remeasurable`, `cost_plus`, `design_build`, `other` |
| `contract_value_amount` | numeric(18,4) | yes | **hidden from ****`foreman`****/****`superintendent`** unless the project flag `site_roles_see_costs` is on; Contracts owns the authoritative contract value, this is the wizard figure used until a contract exists (BR-COR-12) |
| `contract_currency` | text | no | default base currency |
| `contract_value_includes_vat` | bool | no | default false |
| `vat_tax_code_id` | uuid | yes | FK `tax_codes` |
| `address` | text | yes |  |
| `city`, `governorate` | text | yes |  |
| `country_code` | text | no | default company country |
| `latitude`, `longitude` | numeric(9,6) | yes | for weather and delivery routing |
| `site_timezone` | text | no | default company timezone; used to interpret business dates (00 §2.3) |
| `planned_start_date`, `planned_end_date` | date | yes | required to leave `draft`; `end >= start` |
| `actual_start_date` | date | yes | set at activation (date in the site timezone) |
| `actual_end_date` | date | yes | set at close |
| `gross_floor_area_m2` | numeric(18,4) | yes | for per-m2 benchmarks |
| `trade_template_id` | uuid | yes | FK `trade_templates` (last applied) |
| `origin_type` | text | no | `manual`, `tender`, `import`; default `manual` |
| `origin_id` | uuid | yes | tender id when origin is `tender` |
| `site_roles_see_costs` | bool | no | default false; per-project override of the default cost hiding (v3 section 5.2 note) |
| `current_setup_step` | text | no | `details`, `template`, `boq_budget`, `schedule`, `team`, `review` |
| `go_live_override_reason` | text | yes | set when activated with failing blockers (BR-COR-16) |
| `go_live_override_by` | uuid | yes | `platform.users` by value |
| `activated_at`, `closed_at` | timestamptz | yes |  |
| `hold_reason`, `close_reason` | text | yes |  |

Keys and checks: unique `(tenant_id, code)`; unique `(tenant_id, origin_type, origin_id)` where `origin_type <> 'manual'` (idempotent handoff, BR-COR-13); `status in (...)`; `planned_end_date >= planned_start_date`; `contract_value_amount >= 0`; `(status <> 'draft') implies client_party_id is not null and planned_start_date is not null and planned_end_date is not null` is enforced by the aggregate, not by a table check (drafts from tenders may be incomplete). Indexes: `(status, planned_end_date)`, `(client_party_id)`, `(created_at desc, id desc)`. Project membership and roles live in `platform.project_members` (00 §3.8), not here.

#### 4.2.2 `core.project_setup_steps`

One row per wizard step per project, created with the project (v3 4.0.3).

| column | type | null | notes |
|---|---|---|---|
| `project_id` | uuid | no | FK |
| `step_key` | text | no | `details` (1 create project), `template` (2 trades), `boq_budget` (3 BOQ and budget), `schedule` (4), `team` (5), `review` (6) |
| `status` | text | no | `not_started`, `in_progress`, `complete`, `skipped` |
| `completed_at`, `completed_by` | timestamptz, uuid | yes |  |
| `skip_reason` | text | yes | required when `skipped` (allowed for `template`, `boq_budget`, `schedule`) |

Unique `(tenant_id, project_id, step_key)`. Steps 3-5 are *completed by other modules' actions*: Core marks them complete when it receives the matching readiness result, see 4.2.4.

#### 4.2.3 `core.project_readiness_checks` (cache of the latest evaluation)

| column | type | null | notes |
|---|---|---|---|
| `project_id` | uuid | no | FK |
| `check_key` | text | no | stable key such as `core.client_set`, `contracts.boq_loaded`, `cost.budget_approved`, `planning.schedule_baselined`, `platform.team_assigned` |
| `provider` | text | no | `core`, `contracts`, `cost`, `planning`, `platform` |
| `step_key` | text | no | the wizard step the check belongs to |
| `severity` | text | no | `blocker` or `warning` |
| `result` | text | no | `pass`, `fail`, `not_applicable` |
| `message_code` | text | no | e.g. `core.checklist.client_missing` |
| `params` | jsonb | no | counts and ids for the message and for the deep link |
| `evaluated_at` | timestamptz | no |  |

Unique `(tenant_id, project_id, check_key)`. Rewritten wholesale by `ProjectReadinessEvaluator`; never the source of truth for activation (activation re-evaluates, BR-COR-15).

#### 4.2.4 Readiness checklist definition

| Check key | Step | Severity | Pass condition | Provider |
|---|---|---|---|---|
| `core.client_set` | details | blocker | `client_party_id` set, party active and not blocked | core |
| `core.dates_set` | details | blocker | both planned dates set and `end >= start` | core |
| `core.fiscal_period_covers` | details | blocker | a fiscal period exists for `planned_start_date` | core |
| `core.fx_available` | details | blocker | `contract_currency = base` or a rate exists within `fx_max_age_days` | core |
| `core.work_packages_exist` | template | blocker | at least 1 active work package | core |
| `core.work_packages_have_cost_codes` | template | warning | every active work package has at least 1 linked cost code | core |
| `core.zones_exist` | template | warning | at least 1 active zone (or `no_zones` flag accepted by the user) | core |
| `contracts.boq_loaded` | boq\_budget | blocker | BOQ imported, or the project is marked "no BOQ" (cost-plus or internal) | contracts |
| `cost.budget_approved` | boq\_budget | blocker | an approved budget version covering every active work package that has a BOQ link (zero-budget work packages allowed with a reason) | cost |
| `planning.schedule_baselined` | schedule | warning (blocker when `company_settings.schedule_required_for_go_live`) | a baselined schedule exists | planning |
| `platform.team_assigned` | team | blocker | at least one `project_manager` member | platform |
| `platform.approval_limits_set` | team | warning | each approver on the project has limits for `purchase_order` | platform |

A step is `complete` when all its blockers pass (warnings do not hold a step back). Providers that are not yet deployed (feature flag off, e.g. Planning in a thin release) return `not_applicable`.

#### 4.2.5 `core.project_status_log` is not a table

Status changes are written by the interceptor to `platform.status_history` (00 §3.4). Core adds no separate log table.

#### 4.2.6 Project status values

| status | meaning |
|---|---|
| `draft` | created from a won tender, incomplete, not visible to site roles |
| `setup` | wizard in progress |
| `active` | live; transactions may be posted |
| `on_hold` | paused by the client or company; reads allowed, new commitments blocked by consumers |
| `closing` | handover and close-out in progress (set by Closeout through `POST .../begin-closing` or by the PM) |
| `closed` | read-only; all postings rejected |
| `cancelled` | abandoned before go-live; hidden from default lists |

### 4.3 Zones, trades, work packages, cost codes, templates (epics COR-E3, COR-E4, COR-E7)

#### 4.3.1 `core.zones`

| column | type | null | notes |
|---|---|---|---|
| `project_id` | uuid | no | FK `projects` |
| `parent_id` | uuid | yes | FK `zones` (same project); max depth 5 |
| `code` | text | no | e.g. `B3`, `B3-F2`; unique per project |
| `name_ar`, `name_en` | text | no | at least one required |
| `zone_type` | text | no | `phase`, `building`, `floor`, `area`, `unit`, `external` |
| `level_no` | int | yes | floor number (negative for basements, 0 ground) |
| `gross_area_m2` | numeric(18,4) | yes |  |
| `path` | text | no | materialised path of codes (`/B3/B3-F2`), maintained by the service |
| `depth` | int | no | 1-5 |
| `sort_order` | int | no |  |
| `status` | text | no | `active`, `archived` |

Keys: unique `(tenant_id, project_id, code)`; indexes `(project_id, parent_id, sort_order)`, `(project_id, path text_pattern_ops)`. Check `depth between 1 and 5`; `status in (...)`.

#### 4.3.2 `core.trades` (the per-tenant trade library)

| column | type | null | notes |
|---|---|---|---|
| `code` | text | no | two digits `01`-`22`; tenant-added trades use `23` upward |
| `name_ar`, `name_en` | text | no | both required for trades |
| `primary_unit_code` | text | no | default measure unit, FK `units` (tenant, code) |
| `secondary_unit_code` | text | yes |  |
| `typical_measure_text` | text | yes | e.g. `point / m / set` |
| `default_progress_method` | text | no | `quantity`, `stage`, `milestone` |
| `is_time_related` | bool | no | true for Preliminaries (cost accrues by time); Cost reads it for EV (see file 08) |
| `is_optional` | bool | no | seeded but disabled by default (trades 20-22) |
| `is_enabled` | bool | no |  |
| `sort_order` | int | no |  |
| `seed_key`, `is_customized` | text, bool |  |  |

Keys: unique `(tenant_id, code)`.

`core.trade_default_activities` (hand-off to Planning when it seeds a schedule): `trade_id` FK, `sort_order int`, `name_ar`, `name_en`, `default_unit_code` text null, `typical_duration_days int null`. Unique `(tenant_id, trade_id, sort_order)`. Seed: 3 to 6 activities per default trade (for example plumbing: first-fix drainage, first-fix supply, pressure test, fixtures, final test).

#### 4.3.3 `core.cost_codes`

A tenant-wide hierarchy. Level 1 is the trade root (`07`), level 2 is the cost category under it (`07.M`), level 3 is the postable leaf (`07.M.010`). Tenants may add levels (up to 6) and non-trade roots.

| column | type | null | notes |
|---|---|---|---|
| `code` | text | no | dotted code, unique per tenant |
| `parent_id` | uuid | yes | FK `cost_codes`; null only for roots |
| `level` | int | no | 1-6 |
| `path` | text | no | e.g. `07.M.010`, maintained |
| `name_ar`, `name_en` | text | no |  |
| `trade_id` | uuid | yes | FK `trades`; null = shared code usable in any work package |
| `category` | text | yes | `material`, `labour`, `subcontract`, `equipment`, `other`; required on postable codes; matches budget split of v3 section 3.3 |
| `is_postable` | bool | no | only leaves are postable; a code with children is never postable |
| `default_unit_code` | text | yes |  |
| `is_active` | bool | no | deactivated codes stay on old transactions, rejected for new ones |
| `sort_order` | int | no |  |
| `seed_key`, `is_customized` | text, bool |  |  |

Keys: unique `(tenant_id, code)`. Indexes `(parent_id, sort_order)`, `(trade_id, category)`, `(path text_pattern_ops)`. Checks: `level between 1 and 6`; `is_postable implies category is not null`; `category in (...)`.

#### 4.3.4 `core.work_packages`

| column | type | null | notes |
|---|---|---|---|
| `project_id` | uuid | no | FK `projects` |
| `trade_id` | uuid | no | FK `trades` |
| `zone_id` | uuid | yes | FK `zones`; null = whole project (for example Preliminaries) |
| `parent_id` | uuid | yes | FK `work_packages` (sub-package, one level only in R1) |
| `code` | text | no | default `<zoneCode>-<tradeCode>` (e.g. `B3-07`) or `<tradeCode>` when no zone; unique per project |
| `name_ar`, `name_en` | text | no | default from trade and zone names, editable |
| `progress_method` | text | no | `quantity`, `stage`, `milestone`; defaults from trade; locked once Planning records progress (BR-COR-20) |
| `default_unit_code` | text | yes |  |
| `is_time_related` | bool | no | copied from trade, editable only before activation |
| `responsible_user_id` | uuid | yes | `platform.users`, FK allowed (00 §5.4) |
| `status` | text | no | `planned`, `active`, `on_hold`, `completed`, `archived` |
| `template_item_id` | uuid | yes | origin, for idempotent re-apply |
| `sort_order` | int | no |  |

Keys: unique `(tenant_id, project_id, code)`; unique `(tenant_id, project_id, trade_id, coalesce(zone_id, zero-uuid))` where `parent_id is null and status <> 'archived'` (one top-level package per trade per zone); indexes `(project_id, status)`, `(project_id, trade_id)`, `(responsible_user_id)`. No money columns: budget, spend, progress and dates belong to Cost and Planning and are joined by `work_package_id`.

`core.work_package_cost_codes`: `work_package_id` FK, `cost_code_id` FK, `is_default bool`; unique `(tenant_id, work_package_id, cost_code_id)`; at most one default per work package and category (partial unique index). Only postable, active cost codes whose `trade_id` equals the work package trade or is null may be linked (BR-COR-21).

#### 4.3.5 `core.trade_templates` and `core.trade_template_items`

`trade_templates`: `code` text, `name_ar`, `name_en`, `project_type` text (same list as projects), `description_ar`, `description_en`, `is_system bool`, `is_active bool`, `seed_key`, `is_customized`. Unique `(tenant_id, code)`.

`trade_template_items`: `template_id` FK, `trade_id` FK, `scope` text (`project`, `per_building`, `per_floor`, `per_zone_type_area`), `include_by_default bool`, `progress_method` text null (override), `cost_code_mode` text (`trade_defaults` links every active postable code of the trade; `none`), `sort_order int`. Unique `(tenant_id, template_id, trade_id)`.

Seeded templates (all trades below by code; scope `per_building` for trades marked B, `project` otherwise):

| Template code | Name (AR / EN) | Trades included |
|---|---|---|
| `RES` | سكني / Residential | 01 P, 02 B, 03 B, 04 B, 05 B, 06 B, 07 B, 08 B, 09 B, 10 B, 11 B, 12 B, 13 B, 14 B, 15 P, 16 B (off by default), 17 P, 18 B, 19 P |
| `COM` | تجاري وإداري / Commercial | all of `RES` plus 16 on by default and 09, 10 forced on |
| `INF` | بنية تحتية / Infrastructure | 01, 02, 03, 06, 08, 15, 17, 19 on; 20, 21, 22 enabled (optional trades) |
| `FIT` | تشطيبات / Fit-out | 04, 05, 07, 08, 09, 10, 11, 12, 13, 14, 15, 18, 19 (no structure trades) |

The 19 default trades below are the seed contract for `trades`, `trade_default_activities`, and the level 1-3 skeleton of `cost_codes`.

#### 4.3.6 Seed data: default trade library (19 + 3 optional)

`Default unit` = `primary_unit_code`; `Method` = `default_progress_method` (Q quantity, S stage-weighted, M milestone). Cost codes are `<trade>.<category letter>.<nnn>`; letters M material, L labour, S subcontract, E equipment, O other. Each trade also receives a postable `O.010` "Other / miscellaneous" leaf. Leaves listed are the seeded defaults; tenants add their own.

| # | Trade (EN) | Arabic | Default unit | Second unit | Method | Seeded postable cost codes (code: name EN / AR) |
|---|---|---|---|---|---|---|
| 01 | Site preparation and excavation | تجهيز الموقع والحفر | `m3` |  | Q | `01.M.010` Backfill and fill material / مواد ردم; `01.L.010` Excavation labour / عمالة حفر; `01.S.010` Excavation and hauling subcontract / مقاول حفر ونقل; `01.E.010` Excavator and loader hire / إيجار حفار ولودر; `01.E.020` Dewatering / نزح مياه |
| 02 | Foundations | الأساسات | `m3` | `t` | Q | `02.M.010` Ready-mix concrete / خرسانة جاهزة; `02.M.020` Reinforcement steel / حديد تسليح; `02.M.030` Formwork materials / مواد شدات; `02.L.010` Steel fixing and casting labour / عمالة تسليح وصب; `02.S.010` Piling subcontract / مقاول خوازيق |
| 03 | Concrete structure | الهيكل الخرساني | `m3` | `t` | Q | `03.M.010` Ready-mix concrete / خرسانة جاهزة; `03.M.020` Reinforcement steel / حديد تسليح; `03.M.030` Formwork and shuttering / شدات وسقالات; `03.L.010` Structure labour / عمالة هيكل; `03.S.010` Formwork subcontract / مقاول نجارة مسلح; `03.E.010` Concrete pump hire / إيجار طلمبة خرسانة |
| 04 | Masonry | المباني | `m2` | `m3` | Q | `04.M.010` Bricks and blocks / طوب وبلوك; `04.M.020` Mortar materials / مواد مونة; `04.L.010` Mason labour / عمالة مباني; `04.S.010` Masonry subcontract / مقاول مباني |
| 05 | Plastering | المحارة | `m2` |  | Q | `05.M.010` Cement, sand and lime / أسمنت ورمل وجير; `05.M.020` Plaster mesh and beads / شبك وزوايا; `05.L.010` Plastering labour / عمالة محارة; `05.S.010` Plastering subcontract / مقاول محارة |
| 06 | Waterproofing and insulation | العزل | `m2` |  | Q | `06.M.010` Waterproofing membranes / رولات عزل; `06.M.020` Thermal insulation boards / ألواح عزل حراري; `06.L.010` Application labour / عمالة عزل; `06.S.010` Waterproofing subcontract / مقاول عزل |
| 07 | Plumbing and drainage | السباكة والصرف | `pt` | `m`, `set` | S | `07.M.010` Supply pipes and fittings / مواسير وتوصيلات تغذية; `07.M.020` Drainage pipes uPVC / مواسير صرف; `07.M.030` Sanitary fixtures / أدوات صحية; `07.L.010` Plumbing labour / عمالة سباكة; `07.S.010` Plumbing subcontract / مقاول سباكة; `07.E.010` Test pumps and tools / معدات اختبار |
| 08 | Electrical | الكهرباء | `pt` | `m`, `panel` | S | `08.M.010` Cables and wires / كابلات وأسلاك; `08.M.020` Conduits and trunking / مواسير وحوامل كابلات; `08.M.030` Panels and breakers / لوحات وقواطع; `08.M.040` Lighting and accessories / إنارة وإكسسوارات; `08.L.010` Electrical labour / عمالة كهرباء; `08.S.010` Electrical subcontract / مقاول كهرباء |
| 09 | HVAC | التكييف والتهوية | `unit` | `kw` | S | `09.M.010` AC units and chillers / وحدات تكييف وشيلر; `09.M.020` Ducts and insulation / مجاري هواء وعزل; `09.M.030` Pipes and fittings / مواسير وتوصيلات; `09.L.010` HVAC labour / عمالة تكييف; `09.S.010` HVAC subcontract / مقاول تكييف |
| 10 | Fire fighting and alarm | مكافحة الحريق | `pt` | `set` | S | `10.M.010` Sprinklers and pipes / رشاشات ومواسير; `10.M.020` Alarm devices and panels / أجهزة إنذار ولوحات; `10.M.030` Extinguishers and hose reels / طفايات وخراطيم; `10.L.010` Installation labour / عمالة تركيب; `10.S.010` Fire system subcontract / مقاول مكافحة حريق |
| 11 | Flooring and tiles | البلاط والسيراميك | `m2` |  | Q | `11.M.010` Ceramic and porcelain tiles / سيراميك وبورسلين; `11.M.020` Marble and granite / رخام وجرانيت; `11.M.030` Adhesives and grout / لاصق ومونة; `11.L.010` Tiling labour / عمالة بلاط; `11.S.010` Flooring subcontract / مقاول أرضيات |
| 12 | Painting | الدهانات | `m2` |  | Q | `12.M.010` Paints and primers / دهانات ومعجون; `12.M.020` Decorative finishes / دهانات ديكور; `12.L.010` Painting labour / عمالة دهانات; `12.S.010` Painting subcontract / مقاول دهانات |
| 13 | Carpentry and doors | النجارة والأبواب | `pc` | `m2` | Q | `13.M.010` Doors and frames / أبواب وحلوق; `13.M.020` Wood and boards / أخشاب وألواح; `13.M.030` Hardware and locks / أكسسوارات وأقفال; `13.L.010` Carpentry labour / عمالة نجارة; `13.S.010` Carpentry subcontract / مقاول نجارة |
| 14 | Aluminium and glazing | الألمنيوم والزجاج | `m2` |  | Q | `14.M.010` Aluminium profiles / قطاعات ألمنيوم; `14.M.020` Glass / زجاج; `14.M.030` Sealants and accessories / سيليكون وإكسسوارات; `14.S.010` Aluminium and glazing subcontract / مقاول ألمنيوم وزجاج |
| 15 | Metalwork | الأعمال المعدنية | `t` | `m` | Q | `15.M.010` Structural and misc steel / حديد إنشائي ومشغولات; `15.M.020` Handrails and gates / درابزين وبوابات; `15.L.010` Fabrication labour / عمالة تصنيع; `15.S.010` Metalwork subcontract / مقاول أعمال معدنية |
| 16 | Elevators | المصاعد | `unit` |  | S | `16.M.010` Elevator equipment supply / توريد مصاعد; `16.S.010` Elevator installation subcontract / مقاول تركيب مصاعد |
| 17 | External works and landscaping | الأعمال الخارجية واللاندسكيب | `m2` |  | Q | `17.M.010` Paving and kerbs / بلاط انترلوك وبردورات; `17.M.020` Plants and soil / نباتات وتربة; `17.M.030` Irrigation / ري; `17.L.010` External labour / عمالة أعمال خارجية; `17.S.010` Landscaping subcontract / مقاول تنسيق موقع |
| 18 | Finishing, cleaning, snags | التشطيبات والتنظيف | `ls` | `m2` | M | `18.M.010` Finishing materials / مواد تشطيب; `18.L.010` Snagging labour / عمالة تسليم; `18.S.010` Cleaning subcontract / مقاول نظافة |
| 19 | Preliminaries and site overheads | التكاليف العامة للموقع | `month` |  | M (time-related) | `19.L.010` Site staff salaries / مرتبات طاقم الموقع; `19.E.010` Site office, cabins and fencing / مكاتب وسور موقع; `19.E.020` Cranes and hoists / أوناش ورافعات; `19.O.010` Utilities / مرافق مؤقتة; `19.O.020` Insurance and permits / تأمينات وتصاريح; `19.O.030` Safety and PPE / سلامة ومهمات وقاية; `19.O.040` Site transport / نقل وانتقالات |
| 20 | Roads and asphalt (optional) | الطرق والأسفلت | `m2` | `t` | Q | `20.M.010` Asphalt / أسفلت; `20.M.020` Aggregate base / ركام; `20.S.010` Paving subcontract / مقاول رصف |
| 21 | Utility networks (optional) | شبكات المرافق | `m` |  | Q | `21.M.010` Water and sewer pipes / مواسير مياه وصرف; `21.M.020` Cables and ducts / كابلات ومجاري; `21.S.010` Network subcontract / مقاول شبكات |
| 22 | Temporary works and scaffolding (optional) | الأعمال المؤقتة والسقالات | `m2` | `month` | Q | `22.E.010` Scaffolding hire / إيجار سقالات; `22.S.010` Temporary works subcontract / مقاول أعمال مؤقتة |

Trades 01-18 and 20-22 have `is_time_related=false`; trade 19 has `true`. Material-category default cost codes (4.4.1) point at the `M` leaves above.

### 4.4 Parties (epic COR-E5)

A **party** is one real-world organisation or person-as-business. One party may hold several roles (a company can be both supplier and subcontractor). Roles are rows, not a column, so the same record is found once.

#### 4.4.1 `core.parties`

| column | type | null | notes |
|---|---|---|---|
| `party_no` | text | no | `platform.number_series` series `PTY` (e.g. `PTY-0042`), unique per tenant |
| `name_ar` | text | yes | at least one of `name_ar`, `name_en` required |
| `name_en` | text | yes |  |
| `legal_name` | text | yes | as on the commercial register |
| `normalized_name` | text | no | Arabic/English normalised form (00 §3.5, plus removal of legal suffixes such as `شركة`, `للتوريدات`, `Co.`, `Ltd`) used for duplicate detection and search |
| `party_kind` | text | no | `company` or `individual_business` |
| `status` | text | no | `pending_review`, `active`, `blocked`, `inactive`, `merged` |
| `merged_into_id` | uuid | yes | FK `parties`; set when `status = merged` |
| `country_code` | text | no | default company country |
| `city`, `governorate`, `address` | text | yes |  |
| `website` | text | yes |  |
| `default_currency` | text | yes |  |
| `payment_terms_days` | int | yes | **hidden from site roles**; default terms for new POs; Procurement may override per PO |
| `credit_limit_amount` | numeric(18,4) | yes | hidden from site roles; informational |
| `block_scope` | text | yes | when blocked: `new_orders` (no new RFQ, PO, subcontract; payments of existing invoices continue) or `all` (also payment hold) |
| `block_reason_code` | text | yes | `poor_performance`, `legal`, `fraud_suspected`, `documents_expired`, `client_request`, `other` |
| `blocked_at`, `blocked_by` | timestamptz, uuid | yes |  |
| `rating_score` | numeric(5,2) | yes | 0-100 cached from `procurement.supplier.rated.v1`; read-only in Core |
| `rating_as_of` | timestamptz | yes |  |
| `notes` | text | yes |  |

Keys: unique `(tenant_id, party_no)`. Indexes: `(status)`, `(normalized_name)` plus a trigram GIN index on `normalized_name`, `(merged_into_id)`. Checks: `status in (...)`; `payment_terms_days >= 0`; `(status = 'blocked') = (block_scope is not null)`; `status = 'merged' implies merged_into_id is not null`.

#### 4.4.2 `core.party_roles`

| column | type | null | notes |
|---|---|---|---|
| `party_id` | uuid | no | FK |
| `role` | text | no | `supplier`, `subcontractor`, `client`, `bank`, `consultant` |
| `qualification_status` | text | no | `not_required` (client, bank, consultant), `pending`, `approved`, `conditional`, `expired`; meaningful for `supplier` and `subcontractor` |
| `qualified_until` | date | yes | prequalification expiry |
| `qualified_at`, `qualified_by` | timestamptz, uuid | yes |  |

Unique `(tenant_id, party_id, role)`. Index `(role, qualification_status)`. Qualification here is the *flag*; the documents, scoring and performance history that justify it live in Procurement (suppliers) and Subcontracts (open question 9).

#### 4.4.3 `core.party_identifiers`

| column | type | null | notes |
|---|---|---|---|
| `party_id` | uuid | no | FK |
| `id_type` | text | no | `tax_registration` (الرقم الضريبي), `commercial_register` (السجل التجاري), `tax_card` (البطاقة الضريبية), `vat_registration`, `social_insurance`, `contractor_union`, `other` |
| `value` | text | no | as entered |
| `normalized_value` | text | no | digits only for numeric types; Arabic-Indic digits converted |
| `issued_by` | text | yes |  |
| `valid_to` | date | yes | expiry; job warns 30 days before |
| `file_id` | uuid | yes | scan, `platform.files` by value |

Keys: unique `(tenant_id, id_type, normalized_value)` among non-deleted rows of **different parties** (the service checks across parties; the index enforces uniqueness at all), so one tax number maps to one party. Index `(party_id)`, `(valid_to)`.

#### 4.4.4 `core.party_contacts`

| column | type | null | notes |
|---|---|---|---|
| `party_id` | uuid | no | FK |
| `full_name` | text | no |  |
| `job_title` | text | yes |  |
| `contact_type` | text | no | `commercial`, `technical`, `accounts`, `legal`, `site`, `management` |
| `email` | text | yes | lower-cased; format-checked |
| `phone` | text | yes | normalised to E.164 (`+20...`) when parsable |
| `whatsapp_phone` | text | yes | for the notification channel (open question in 00 §13) |
| `language` | text | no | `ar` (default) or `en`; used for RFQ and notification language |
| `is_primary` | bool | no | one primary per party and `contact_type` |
| `portal_user_id` | uuid | yes | `platform.external_users` by value, set when invited |
| `is_active` | bool | no |  |

Indexes `(party_id)`, `(lower(email))`. Personal data minimal (name, phone, email only, 00 §3.8).

#### 4.4.5 `core.party_bank_accounts` and verification

| column | type | null | notes |
|---|---|---|---|
| `party_id` | uuid | no | FK |
| `bank_party_id` | uuid | yes | FK `parties` with role `bank` |
| `bank_name` | text | no |  |
| `branch` | text | yes |  |
| `account_name` | text | no | must resemble the party legal name (warning, BR-COR-27) |
| `account_no` | text | yes | stored encrypted at column level; API returns masked (`******1234`) unless `core.party.bank_view` |
| `iban` | text | yes | validated (mod-97) when present; Egyptian IBAN is 29 characters |
| `swift` | text | yes |  |
| `currency` | text | no |  |
| `is_primary` | bool | no | one verified primary per party and currency |
| `status` | text | no | `pending_verification`, `verified`, `rejected`, `inactive` |
| `verification_method` | text | yes | `call_back`, `bank_letter`, `document`, `portal_confirmation` |
| `evidence_file_id` | uuid | yes |  |
| `verified_by`, `verified_at` | uuid, timestamptz | yes | `verified_by <> created_by` (BR-COR-26) |
| `replaces_id` | uuid | yes | FK self: new account row that replaces a changed one |
| `rejected_reason` | text | yes |  |

Keys: unique `(tenant_id, party_id, coalesce(iban,''), coalesce(account_no_hash,''))` using a stored hash column `account_no_hash text` so uniqueness works on encrypted values. Index `(party_id, status)`. **Account number, IBAN and SWIFT are never edited in place**: a change inserts a new row `pending_verification` that `replaces_id` the old one; the old row stays `verified` and usable until the new one is verified, then becomes `inactive` (BR-COR-26).

#### 4.4.6 Other party tables

| table | columns (besides standard) | notes |
|---|---|---|
| `core.party_scopes` | `party_id` FK, `material_category_id` uuid null FK, `trade_id` uuid null FK | exactly one of the two refs set (check); says which material categories a supplier supplies and which trades a subcontractor performs; Sourcing uses it to select eligible suppliers. Unique `(tenant_id, party_id, coalesce(material_category_id, zero), coalesce(trade_id, zero))` |
| `core.party_rating_snapshots` (append-only) | `party_id` FK, `source_event_id uuid`, `overall_score numeric(5,2)`, `on_time_pct numeric(9,4)`, `quantity_accuracy_pct numeric(9,4)`, `quality_score numeric(5,2)`, `as_of timestamptz` | unique `(tenant_id, source_event_id)` gives consumer idempotency; latest copied to `parties.rating_score` |
| `core.party_block_history` (append-only) | `party_id` FK, `action` (`block`,`unblock`,`request_block`), `scope`, `reason_code`, `reason_text`, `actor_id`, `at`, `request_id` | the audit trail for BR-COR-24 |
| `core.party_duplicate_candidates` | `party_a_id`, `party_b_id` (FK, `a < b`), `match_type` (`tax_id`, `name_exact`, `name_similar`, `phone`, `iban`), `score numeric(5,2)`, `status` (`open`, `dismissed`, `merged`), `reviewed_by`, `reviewed_at` | filled by job `core_duplicate_scan` and at create time; unique `(tenant_id, party_a_id, party_b_id, match_type)` |
| `core.merge_log` (append-only) | `entity_type` (`party`,`material`), `from_id`, `to_id`, `merged_by`, `merged_at`, `reason`, `repointed_counts jsonb` (per module counts), `undo_until timestamptz` | audit and the redirect table: lookups of `from_id` resolve to `to_id` |

### 4.5 Material master (epic COR-E6)

#### 4.5.1 `core.material_categories`

| column | type | null | notes |
|---|---|---|---|
| `code` | text | no | three-letter family code (`CEM`, `STL`) or longer for tenant-added |
| `parent_id` | uuid | yes | FK self; seed is flat (23 categories), tenants may nest |
| `name_ar`, `name_en` | text | no |  |
| `trade_id` | uuid | yes | FK `trades`; primary trade |
| `default_cost_code_id` | uuid | yes | FK `cost_codes`, postable `M` leaf |
| `default_unit_code` | text | yes |  |
| `validity_days` | int | no | **price validity period** in days, >= 1 (BR-COR-33); read by Sourcing through `IMaterialCatalog.GetValidityDays` |
| `volatility_class` | text | no | `high`, `medium`, `low` (informational, explains the default) |
| `sort_order` | int | no |  |
| `seed_key`, `is_customized` | text, bool |  |  |

Unique `(tenant_id, code)`. Check `validity_days >= 1`. Seeded categories (about 300 materials):

| Code | Category (AR / EN) | `validity_days` | Seeded items | Default cost code | Example seeded materials |
|---|---|---|---|---|---|
| `STL` | حديد التسليح والإنشائي / Reinforcement and structural steel | 1 | 20 | `03.M.020` | rebar 8-32 mm grade 40/52 (حديد تسليح عز), mesh, tie wire, I-beams, plates |
| `CEM` | الأسمنت ومواد الربط / Cement and binders | 2 | 12 | `05.M.010` | CEM I 42.5N bag 50 kg, CEM I 52.5N, white cement, lime, gypsum plaster |
| `RMC` | الخرسانة الجاهزة / Ready-mix concrete | 2 | 10 | `03.M.010` | C25, C30, C35, C40, sulphate-resistant, with admixtures |
| `AGG` | الركام والرمل والردم / Aggregates, sand and fill | 3 | 10 | `01.M.010` | sand, 10 mm and 20 mm gravel, base course, backfill, crushed stone |
| `MAS` | الطوب والبلوك / Bricks and blocks | 7 | 14 | `04.M.010` | red brick, cement block 10/15/20 cm, autoclaved aerated block, hollow block |
| `WPF` | العزل / Waterproofing and insulation | 14 | 14 | `06.M.010` | bitumen membrane 4 mm, cementitious waterproofing, XPS and EPS boards, rock wool |
| `FRM` | الشدات والأخشاب / Formwork and timber | 7 | 12 | `03.M.030` | plywood 18 mm, timber beams, steel props, shuttering oil |
| `PLB` | مواسير وتوصيلات السباكة / Plumbing pipes and fittings | 7 | 24 | `07.M.010` | PPR pipe 20-110 mm, fittings, copper pipe, valves, mixers |
| `DRN` | الصرف / Drainage and sewer | 7 | 14 | `07.M.020` | uPVC pipe 50-160 mm, manholes, floor drains, HDPE |
| `SAN` | الأدوات الصحية / Sanitary ware | 30 | 14 | `07.M.030` | WC, basin, shower mixer, bidet, kitchen sink |
| `ELC` | الكابلات والأسلاك / Cables and wires | 7 | 20 | `08.M.010` | copper cable 1.5-240 mm2, armoured cable, data cable |
| `ELP` | المواسير واللوحات والقواطع / Conduits, panels and breakers | 7 | 18 | `08.M.030` | PVC conduit, cable tray, MCB, MCCB, distribution board |
| `LGT` | الإنارة والإكسسوارات / Lighting and accessories | 30 | 12 | `08.M.040` | LED panel, downlight, switch, socket |
| `HVC` | التكييف والتهوية / HVAC equipment and ducting | 14 | 14 | `09.M.020` | split unit, duct, flexible duct, insulation, chilled-water pipe |
| `FFT` | مكافحة الحريق / Fire fighting | 14 | 12 | `10.M.010` | sprinkler head, black steel pipe, smoke detector, extinguisher |
| `TIL` | البلاط والأحجار / Tiles and stone | 30 | 16 | `11.M.010` | ceramic 60x60, porcelain, marble, granite, skirting |
| `ADH` | اللواصق والسيليكون / Adhesives, grout and sealants | 14 | 8 | `11.M.030` | tile adhesive, grout, PU sealant, silicone |
| `PNT` | الدهانات / Paints | 7 | 14 | `12.M.010` | plastic emulsion, primer, putty, enamel |
| `DRS` | الأبواب والأخشاب والإكسسوارات / Doors, wood and hardware | 30 | 12 | `13.M.010` | flush door, fire door, MDF, hinges, locks |
| `ALG` | الألمنيوم والزجاج / Aluminium and glass | 30 | 10 | `14.M.010` | window profile, 6 mm clear glass, tempered glass |
| `MTL` | الأعمال المعدنية والمسامير / Metalwork and fasteners | 7 | 8 | `15.M.010` | angles, hollow sections, anchor bolts, handrail |
| `EXT` | مواد الأعمال الخارجية / External works materials | 30 | 6 | `17.M.010` | interlock paver, kerb, topsoil, irrigation pipe |
| `SAF` | السلامة ومهمات الوقاية / Safety and PPE | 30 | 6 | `19.O.030` | helmet, safety shoes, harness, barrier tape |

Total 300. The numbers follow v3 4.4A.1 (steel and cement 1-2 days; plumbing and paint one week; finishing one month), and all are configurable (BR-COR-33).

#### 4.5.2 `core.material_category_attributes`

Defines the **specification attributes** for a category, so specs are comparable across suppliers.

| column | type | null | notes |
|---|---|---|---|
| `category_id` | uuid | no | FK |
| `attr_key` | text | no | snake\_case (`grade`, `diameter_mm`, `strength_class`) |
| `name_ar`, `name_en` | text | no |  |
| `data_type` | text | no | `text`, `number`, `enum`, `bool` |
| `unit_code` | text | yes | for numbers |
| `enum_values` | jsonb | yes | list of `{value, name_ar, name_en}` for `enum` |
| `is_required` | bool | no |  |
| `is_matching_key` | bool | no | participates in Sourcing's "specification match" check |
| `sort_order` | int | no |  |

Unique `(tenant_id, category_id, attr_key)`.

#### 4.5.3 `core.materials`

| column | type | null | notes |
|---|---|---|---|
| `code` | text | no | `<category code>-<nnnn>` e.g. `CEM-0001`, generated from series `MAT-<cat>`; unique per tenant |
| `category_id` | uuid | no | FK |
| `name_ar`, `name_en` | text | no | both required for materials (aliases cover other wordings) |
| `spec_text_ar`, `spec_text_en` | text | yes | human specification, e.g. `Portland cement CEM I 42.5N, 50 kg bag` |
| `standard_ref` | text | yes | e.g. `EN 197-1`, `ES 4756-1`, `ASTM A615` |
| `specs` | jsonb | no | values for the category's attributes, validated on write (BR-COR-34); default `{}` |
| `base_unit_code` | text | no | unit used to compare quantities and prices (cement: `t`) |
| `default_purchase_unit_code` | text | yes | unit normally ordered in (cement: `bag`) |
| `trade_id` | uuid | yes | override of category trade |
| `default_cost_code_id` | uuid | yes | override of category default |
| `validity_days_override` | int | yes | >= 1; wins over the category value |
| `search_text` | text | no | normalised concatenation of names, spec and aliases for trigram search |
| `status` | text | no | `active`, `pending_review`, `inactive`, `merged` |
| `merged_into_id` | uuid | yes | FK self |
| `is_standard` | bool | no | from the seed catalogue |
| `seed_key`, `is_customized` | text, bool |  |  |

Keys: unique `(tenant_id, code)`. Indexes `(category_id, status)`, trigram GIN on `search_text`, `(merged_into_id)`. Check `validity_days_override is null or validity_days_override >= 1`.

Related small tables:

| table | columns | notes |
|---|---|---|
| `core.material_aliases` | `material_id` FK, `alias_text`, `normalized_alias`, `language` (`ar`,`en`,`mixed`), `alias_kind` (`synonym`,`abbreviation`,`brand_term`,`misspelling`,`supplier_term`), `supplier_party_id` uuid null FK `parties` | unique `(tenant_id, normalized_alias, coalesce(supplier_party_id, zero))` so one wording maps to exactly one material in its scope (BR-COR-35). Seed gives each material 2-6 aliases (for example cement: `اسمنت`, `أسمنت بورتلاند`, `اسمنت عادي`, `OPC`, `Cement CEM I 42.5N bag`) |
| `core.material_alternatives` | `material_id` FK, `alternative_material_id` FK, `note` | symmetric pairs stored once (`a < b`); "typical alternatives" of v3 4.4A.2 |

Unit conversions specific to a material (1 bag = 50 kg; 1 m of 12 mm rebar = 0.888 kg) are rows of `core.unit_conversions` with `material_id` set (4.1.4).

#### 4.5.4 `core.boq_catalog_items` \[R2\]

Shared standard BOQ item catalogue for Estimating and Contracts (reference rates are costs: hidden from site roles).

| column | type | null | notes |
| --- | --- | --- | --- |
| `code` | text | no | unique per tenant |
| `name_ar`, `name_en` | text | no |  |
| `unit_code` | text | no |  |
| `trade_id` | uuid | yes | FK |
| `cost_code_id` | uuid | yes | by value |
| `material_id` | uuid | yes | FK |
| `reference_rate_amount` | numeric(18,4) | yes | indicative rate |
| `rate_currency`, `rate_as_of` | text, date | yes |  |
| `is_active` | bool | no |  |

### 4.6 Seeding and housekeeping tables

| table | columns | notes |
|---|---|---|
| `core.seed_runs` (append-only) | `seed_name`, `seed_version`, `applied_at`, `applied_rows int`, `skipped_customized int`, `summary jsonb` | one row per seed per tenant per version; idempotency marker |
| `core.reference_usage_cache` | `entity_type`, `entity_id`, `module`, `usage_count int`, `as_of` | optional cache for the consistency job; never authoritative |

### 4.7 ER summary

```txt
tenant --1:1-- company_settings
tenant --< currencies, units --< unit_conversions >-- materials
tenant --< tax_codes --< tax_code_rates
tenant --< fiscal_years --< fiscal_periods ;  tenant --< exchange_rates ; tenant --< holidays
tenant --< trades --< trade_default_activities
trades --< cost_codes (tree via parent_id; trade_id nullable for shared)
trade_templates --< trade_template_items >-- trades
parties --< party_roles / party_identifiers / party_contacts / party_bank_accounts / party_scopes
parties --< party_rating_snapshots, party_block_history ; parties >-- parties (merged_into_id, bank_party_id)
projects >-- parties (client, consultant) ; projects --< project_setup_steps / project_readiness_checks
projects --< zones (tree) ; projects --< work_packages >-- trades ; work_packages >-- zones
work_packages --< work_package_cost_codes >-- cost_codes
material_categories --< material_category_attributes ; material_categories --< materials --< material_aliases
materials --< material_alternatives ; materials >-- cost_codes (default) ; material_categories >-- cost_codes, trades
```

### 4.8 Enumerations (value lists with meaning)

| Enum | Values and meaning |
|---|---|
| `project.status` | `draft`, `setup`, `active`, `on_hold`, `closing`, `closed`, `cancelled` (4.2.6) |
| `project.project_type` | `residential`, `commercial`, `infrastructure`, `fit_out`, `industrial`, `mixed` |
| `project.contract_type` | `lump_sum`, `remeasurable`, `cost_plus`, `design_build`, `other` |
| `setup step status` | `not_started`, `in_progress`, `complete`, `skipped` |
| `readiness severity / result` | `blocker`, `warning` / `pass`, `fail`, `not_applicable` |
| `zone.zone_type` | `phase`, `building`, `floor`, `area`, `unit`, `external` |
| `work_package.status` | `planned` (created, not started), `active`, `on_hold`, `completed`, `archived` |
| `progress_method` | `quantity` (percent by measured quantity), `stage` (weighted stages), `milestone` (lump items) |
| `cost_code.category` | `material`, `labour`, `subcontract`, `equipment`, `other` (the budget split of v3 section 3.3) |
| `party.status` | `pending_review`, `active`, `blocked`, `inactive` (retired, no new use), `merged` |
| `party_role.role` | `supplier`, `subcontractor`, `client`, `bank`, `consultant` |
| `qualification_status` | `not_required`, `pending`, `approved`, `conditional`, `expired` |
| `block_scope` | `new_orders`, `all` |
| `bank_account.status` | `pending_verification`, `verified`, `rejected`, `inactive` |
| `material.status` | `active`, `pending_review`, `inactive`, `merged` |
| `tax_type` | `vat`, `withholding`, `stamp`, `other` |
| `exchange rate_type` | `spot`, `contract`, `budget` |
| `unit.dimension` | `length`, `area`, `volume`, `mass`, `count`, `time`, `lump`, `power`, `other` |
| `source` | `ui`, `import`, `mobile`, `portal`, `api`, `system` (00 §3.2); seeded rows use `system` |

---

## 5. State machines

Every transition is an explicit action endpoint (00 §3.6), writes `platform.status_history` (00 §3.4) and, where listed, a notification intent (section 11). Guard failures return the error code named in section 12.

### 5.1 Project

| from | action | to | guard | who | side effects |
|---|---|---|---|---|---|
| (none) | `create` (manual) | `setup` | name, type, currency valid; code from series `PRJ`; creates the 6 `project_setup_steps`; creator becomes project member `project_manager` | `project_manager`, `owner_executive` | `core.project.created.v1` |
| (none) | `create` (tender handler) | `draft` | event not seen before (BR-COR-13) | System | `core.project.created.v1` (origin `tender`); notification `COR_PROJECT_DRAFT_FROM_TENDER` |
| `draft` | `start-setup` | `setup` | caller is `project_manager` or `owner_executive` | same | task-inbox item "complete setup" |
| `setup` | `activate` | `active` | readiness re-evaluated now: zero failing blockers, or override by `owner_executive` with reason (BR-COR-15, 16) | `project_manager` (no override), `owner_executive` (override) | sets `actual_start_date` (site timezone), `activated_at`; all `planned` work packages become `active`; `core.project.activated.v1`; notification `COR_PROJECT_ACTIVATED` |
| `active` | `hold` | `on_hold` | reason | `project_manager`, `owner_executive` | `core.project.status_changed.v1` (proposed); notification |
| `on_hold` | `resume` | `active` | none | same | `core.project.status_changed.v1` |
| `active` | `begin-closing` | `closing` | none | `project_manager`; Closeout via contract call | `core.project.status_changed.v1` |
| `closing` | `close` | `closed` | `core.project.close`; close providers return no blocker, or override with reason (BR-COR-17) | `owner_executive`, `project_manager` with `core.project.close` | `actual_end_date`, `closed_at`; `core.project.closed.v1` |
| `closed` | `reopen` | `closing` | reason; `core.project.reopen` | `owner_executive` | audited; `core.project.status_changed.v1` |
| `draft`, `setup` | `cancel` | `cancelled` | no usage reported by any `IReferenceUsageProvider` | `project_manager`, `owner_executive` | hidden from default lists; code never reused |

Terminal: `cancelled`. `closed` is terminal except `reopen`.

### 5.2 Work package

| from | action | to | guard | who | side effects |
|---|---|---|---|---|---|
| (none) | `create` | `planned` (project `draft`/`setup`) or `active` (project `active`) | BR-COR-19; trade enabled; zone active | `project_manager`, `cost_qs_engineer` | `core.work_package.created.v1` |
| `planned` | `activate` | `active` | project not `closed`/`cancelled` | `project_manager` | `core.work_package.updated.v1` |
| `active` | `hold` | `on_hold` | reason | `project_manager` | `.updated.v1`; consumers warn on new commitments |
| `on_hold` | `resume` | `active` |  | `project_manager` | `.updated.v1` |
| `active` | `complete` | `completed` | no open site or purchase requests (usage providers); percent complete below 100 is a warning to acknowledge | `project_manager` | `.updated.v1` |
| `completed` | `reopen` | `active` | project not closed | `project_manager` | `.updated.v1` |
| `planned`, `on_hold`, `completed` | `archive` | `archived` | BR-COR-22 | `project_manager` | `core.work_package.archived.v1`; frees the uniqueness slot |
| `archived` | `unarchive` | `planned` | uniqueness slot free | `project_manager` | `.updated.v1` |

### 5.3 Party

| from | action | to | guard | who | side effects |
| --- | --- | --- | --- | --- | --- |
| (none) | `create` | `active` (admin, or flag `party_review_required` off) or `pending_review` | duplicate check BR-COR-25 | `procurement_officer`, `contracts_manager`, `tenant_admin` | `core.party.created.v1` |
| `pending_review` | `approve` | `active` | approver is not the creator when the flag is on | `tenant_admin`, `finance_controller` | `core.party.updated.v1` |
| `pending_review` | `reject` | `inactive` | reason | same | audited |
| `active` | `block` | `blocked` | `scope`, `reason_code` | `finance_controller`, `tenant_admin`, `owner_executive` | `core.party.blocked.v1`; `party_block_history`; notification `COR_PARTY_BLOCKED` |
| `active` | `request-block` | `active` (unchanged) | reason | `procurement_officer`, `project_manager` | history row `request_block`; task to `finance_controller`; notification `COR_PARTY_BLOCK_REQUESTED` |
| `blocked` | `unblock` | `active` | reason; if reason code was `fraud_suspected`, only `owner_executive` | `finance_controller`, `owner_executive` | `core.party.unblocked.v1` (proposed); history |
| `active` | `deactivate` | `inactive` | none (existing documents unaffected, new use refused) | `tenant_admin` | `core.party.updated.v1` |
| `inactive` | `reactivate` | `active` |  | `tenant_admin` | `.updated.v1` |
| `active`, `inactive` | `merge` | `merged` | BR-COR-28 | `tenant_admin` | `core.party.merged.v1` (proposed); `merge_log` |

### 5.4 Party bank account

| from | action | to | guard | who | side effects |
| --- | --- | --- | --- | --- | --- |
| (none) | `create` | `pending_verification` | IBAN mod-97 valid when present | `procurement_officer`, `finance_controller` | notification `COR_BANK_VERIFY_REQUESTED` |
| `pending_verification` | `verify` | `verified` | `verified_by <> created_by` (BR-COR-26); method given | `finance_controller` | replaced account becomes `inactive`; `core.party.bank_account_verified.v1` (proposed) |
| `pending_verification` | `reject` | `rejected` | reason | `finance_controller` | notification to creator |
| `verified` | `deactivate` | `inactive` | warning if it is the only verified account and payments are open | `finance_controller` |  |

### 5.5 Material

| from | action | to | guard | who | side effects |
| --- | --- | --- | --- | --- | --- |
| (none) | `create` | `active` or `pending_review` (flag `material_review_required` and creator not admin) | BR-COR-34; duplicate warning | `procurement_officer`, `cost_qs_engineer`, `tenant_admin` | `core.material.created.v1` |
| `pending_review` | `approve` | `active` |  | `tenant_admin`, `cost_qs_engineer` | `core.material.updated.v1` |
| `active` | `deactivate` | `inactive` |  | `tenant_admin` | `.updated.v1` |
| `inactive` | `reactivate` | `active` |  | `tenant_admin` | `.updated.v1` |
| `active`, `inactive` | `merge` | `merged` | BR-COR-36 | `tenant_admin` | `core.material.merged.v1` (proposed) |

Zones, cost codes, trades, templates, units and tax codes only toggle `active`/`inactive` (`archived` for zones), guarded by BR-COR-03, 18 and 31; the only status event is `core.cost_code.updated.v1`.

---

## 6. Business rules

### 6.1 Company, units, tax, calendar, rates

| ID | Rule |
|---|---|
| BR-COR-01 | `base_currency` can change only while the tenant has zero projects; afterwards read-only (`core.base_currency_locked`). The base currency row in `currencies` cannot be disabled. |
| BR-COR-02 | `Convert(qty, from, to, materialId)` resolves in order, first hit wins: (1) `from = to` returns `qty`; (2) `unit_conversions` row `(from, to, materialId)`; (3) its inverse (`qty / factor`); (4) a general row (`material_id` null) or its inverse; (5) same `dimension` with both `to_base_factor` set: `qty x from.factor / to.factor`; (6) a two-hop path through one intermediate unit using steps 2-5 (bag to kg to t). No path: `core.unit_conversion_unavailable`. Result rounded to the target's `quantity_decimals + 2` places and a non-zero input never rounds to zero. Example: cement has row `bag -> kg` factor 50; 60 t to bag = 60 x 1000 / 50 = **1,200 bag**. |
| BR-COR-03 | System units (`is_system`) cannot change `code`, `dimension` or `to_base_factor` and cannot be deleted. A custom unit used by any record is deactivated, not deleted. A quantity with more decimals than `quantity_decimals` is rejected by consumers calling `IUnitConverter.ValidatePrecision` (`core.quantity_precision_exceeded`). |
| BR-COR-04 | Tax rate in force = the `tax_code_rates` row whose range contains the document date. Ranges of one code never overlap and are never edited; a new rate sets `effective_to` of the open row to `new.effective_from - 1 day`. Tax = `round_half_away_from_zero(base x rate_percent / 100, currency.decimals)`. Example: VAT 14 % on 2,063,675.00 = 288,914.50, rounded to **288,915.00** (payment application 4). Tax is computed on the document total unless the consuming module states otherwise. |
| BR-COR-05 | At most one default tax code per `(tax_type, applies_to)`; `default_vat_tax_code_id` must be an active code of type `vat`. A tax code used on any record is deactivated, not deleted. |
| BR-COR-06 | `GetRate(from, to, date, type)`: `from = to` gives 1; else the row with the greatest `rate_date <= date` for the type (project-specific beats general); `contract` and `budget` rows also need `valid_to_date is null or >= date`. If `date - rate_date > fx_max_age_days` the result has `isStale = true` and write paths raise warning `core.fx_rate_stale`; no row raises `core.fx_rate_missing`. Only the opposite pair existing returns `1 / rate` with `isInverse = true`. Rates are never edited after use: a correction inserts a new row; consumers copy `rate` and `rate_date` into their documents. Example: USD to EGP on 2026-10-05 with the latest row 48.50 dated 2026-10-01 returns 48.50 (age 4 days, not stale). |
| BR-COR-07 | Fiscal years are contiguous (`start = previous end + 1 day`); 12 periods are generated per year from `fiscal_year_start_month`, each a calendar month; an optional period 13 is an audit-adjustment period with dates equal to the end date of period 12. `year_label` = calendar year in which the fiscal year starts. A year with periods referenced by Cost cannot be deleted. |
| BR-COR-08 | Core stores no open/closed flag. `PeriodOf(date)` returns the containing period or `core.fiscal_period_missing`. Cost owns open/closed (BR-X-05). |
| BR-COR-09 | Working-day calendar of a project = company `weekend_days`, minus project `working_day` overrides, plus project and company `holiday` rows (project scope wins). `IsWorkingDay(projectId, date)` is exposed; Planning owns scheduling. Default weekend is Friday and Saturday. |

### 6.2 Project, zones, work packages

| ID | Rule |
|---|---|
| BR-COR-10 | Project `code` comes from series `PRJ` (default `P-{yyyy}-{nnn}`, yearly reset) inside the create transaction; cancelled projects keep their code. |
| BR-COR-11 | Creating a project requires `name_ar`, `project_type`, an enabled `contract_currency`. Leaving `draft` requires an active party with role `client`, and planned start and end dates with `end >= start`. |
| BR-COR-12 | `contract_value_amount` is the wizard figure. After `contracts.contract.signed.v1`, Contracts' value is authoritative for reports and Core's field becomes read-only ("from contract"). Core never recalculates it. |
| BR-COR-13 | The `estimating.tender.won.v1` handler is idempotent on `(origin_type='tender', origin_id)`: a replay returns the existing project and publishes nothing. The draft copies name, client party (by id), contract value, currency and dates; an unmatched client leaves `client_party_id` null and the readiness check fails. |
| BR-COR-14 | Setup steps may be done in any order except `review`, enabled only when `details` is `complete`. `skipped` is allowed only for `template`, `boq_budget`, `schedule` with a reason; a skipped step counts as complete in the step bar but its blocker checks still run at activation unless `not_applicable`. |
| BR-COR-15 | `activate` re-evaluates every readiness check inside the same transaction; the cached `project_readiness_checks` is display only. A provider that does not answer within 2 s counts as `fail` for blockers (`core.checklist.provider_unavailable`). |
| BR-COR-16 | If blockers fail, only `owner_executive` may activate, with a reason of at least 10 characters; failing check keys go to the audit record and `go_live_override_reason/by` are set. Warnings never need an override. |
| BR-COR-17 | `closed` projects reject new transactions in every module (`IProjectDirectory.Get(id).Status`). `on_hold` rejects new commitments (POs, subcontracts) but allows receipts, invoices and payments for existing ones. Close needs `core.project.close`; blocking close checks need an override reason as in BR-COR-16. |
| BR-COR-18 | Zones form a tree of depth \<= 5 inside one project; `code` unique per project; child and parent share `project_id`; cycles rejected; a zone with active children or active work packages cannot be archived; moving a zone rewrites `path` and `depth` of the subtree in one transaction. The floor generator creates floors `level_no` a..b under a building with codes `<building>-F<n>` (basements `-B<n>`). Example: building `B3`, levels -1..4 creates `B3-B1`, `B3-F0`, `B3-F1` ... `B3-F4` (6 zones). |
| BR-COR-19 | A work package has one project, one trade, optional zone. One non-archived top-level package per `(project, trade, zone)`. Default `code` = `<zoneCode>-<tradeCode>` or `<tradeCode>`; `progress_method` and `is_time_related` default from the trade. Creation in a `closed`/`cancelled` project is rejected. |
| BR-COR-20 | `progress_method` and `is_time_related` are locked (`core.progress_method_locked`) once Planning's usage provider reports progress records for the package, or the project is active and Cost has an approved budget line for it. |
| BR-COR-21 | `work_package_cost_codes` hold only active postable cost codes whose `trade_id` equals the package trade or is null. `Validate(projectId, wpId, costCodeId)` is ok only when the code is linked to the package (or the package has no links and the code is an active postable code of the trade or shared, with warning `core.work_package_no_cost_codes`), the package belongs to the project and is `planned` or `active`, and the project is not `closed`/`cancelled`. Every cost-bearing write calls it (BR-X-01). |
| BR-COR-22 | A work package is archived only if usage providers report zero **open** items (open site and purchase requests, POs not closed, undelivered receipts, unposted invoices) and it is `completed`, or zero usage at all; else `core.work_package_in_use` with counts per module in `params`. Archiving never deletes. |

### 6.3 Cost codes and templates

| ID | Rule |
|---|---|
| BR-COR-23 | Only leaves are postable; a code with children is never postable and a postable code gets no children (`core.cost_code_has_children`, `core.cost_code_is_postable`). `path` = parent's path + `.` + own segment and `code` = `path`. Moving a code rewrites descendants only when usage is 0; otherwise refused (create a new code and deactivate the old). A postable code's `category` must equal its category ancestor. |
| BR-COR-31 | A cost code used by any transaction is deactivated, never deleted or re-coded. Inactive codes are rejected on new postings (`core.cost_code_inactive`) and hidden from pickers. Deactivating a code unlinks it from `planned` work packages and warns for `active` ones. |
| BR-COR-32 | `Apply(project, template, options)` creates work packages by item scope: `project` one package without zone; `per_building` one per active zone of type `building`; `per_floor` per `floor`; `per_zone_type_area` per `area`. Options `includeOptionalTrades`, `zoneIds`, `tradeIds`. Idempotent: an item that already produced a package (`template_item_id` + zone) is skipped and reported; archived packages are not resurrected; nothing is deleted. `cost_code_mode = trade_defaults` links all active postable codes of the trade. Example: `RES` on 6 building zones creates 14 per-building trades (02-14 and 18; trade 16 is off) x 6 = 84 plus 4 project-wide trades (01, 15, 17, 19) = **88 work packages**; re-applying creates 0. |

### 6.4 Parties

| ID | Rule |
|---|---|
| BR-COR-24 | A party blocked with `new_orders` cannot be selected for new RFQs, POs or subcontracts; with `all` it also cannot be paid (Procurement holds payment lines). `IsBlocked(id, 'new_orders')` is true for both scopes. Block and unblock write `party_block_history` with actor and reason. Consumers check on write and handle `core.party.blocked.v1` by flagging open drafts. |
| BR-COR-25 | Duplicate detection runs at create and nightly. Same `normalized_value` for `tax_registration` or `commercial_register` is a hard error `core.party_duplicate_identifier` naming the existing party. Equal `normalized_name`, trigram similarity >= 0.75, same IBAN or same phone yields candidate rows and warning `core.party_possible_duplicate`; the user may mark "not a duplicate" (`dismissed`). Example: `شركة النصر للتوريدات` and `النصر للتوريدات` both normalise to `النصر` and match exactly. |
| BR-COR-26 | A bank account is usable by payments only when `status = verified`. The verifier differs from the creator (`core.bank_verifier_same_as_creator`), gives a method and, for `bank_letter`/`document`, an evidence file. Account number, IBAN, SWIFT and currency are immutable: a change is a new row with `replaces_id`; the old verified row stays primary until the new one is verified, then becomes `inactive` in the same transaction. `GetPrimaryBankAccount(partyId, currency)` returns verified rows only. All actions are audited. |
| BR-COR-27 | If normalised `account_name` has trigram similarity \< 0.5 with the party names, creation succeeds with warning `core.bank_account_name_mismatch` that the verifier must acknowledge. An IBAN passes mod-97 and, for `EG`, has 29 characters (`core.iban_invalid`). |
| BR-COR-28 | Merge `from -> to`: both `active` or `inactive`, neither merged, same `party_kind`. In one transaction: roles and scopes union into the target (stronger qualification wins); identifiers and contacts re-point (duplicates dropped); verified bank accounts re-point and keep their status; `from` becomes `merged` with `merged_into_id`; `merge_log` records per-module counts; every module re-points by-value references through `IPartyMergeParticipant`. Any participant failure rolls everything back. Lookups of the old id resolve to the target permanently; undo is offered for 90 days (`undo_until`). |
| BR-COR-29 | `supplier` and `subcontractor` roles start `pending` when the tenant enables qualification, else `not_required`; `qualified_until` in the past turns the status `expired` (job). `blocked` is a party status, not a role status. A party needs at least one role to appear in pickers, which filter by role. |

### 6.5 Seeds, materials, visibility, cross-cutting

| ID | Rule |
|---|---|
| BR-COR-30 | The seeder is idempotent per `(tenant, seed_name, seed_version)`: inserts missing rows by `seed_key`, updates rows with `is_customized = false`, skips customised rows (`skipped_customized`), never deletes. Two runs give identical counts. Provisioning applies seed version `1`: \~40 units, 6 currencies, tax codes, 22 trades, cost codes of 4.3.6, 23 material categories, 300 materials, 4 templates. |
| BR-COR-33 | Price validity of a material = `validity_days_override` if set, else its category's `validity_days` (integer >= 1). Defaults: steel 1, cement 2, ready-mix 2, aggregates 3, plumbing 7, paints 7, finishing 30. Sourcing computes `offer_expires_at = quoted_at + validity_days`; Core only serves the number. |
| BR-COR-34 | `materials.specs` is validated against the category attributes: required keys present; `number` parses; `enum` value in `enum_values`; unknown keys rejected (`core.material_spec_invalid` lists failing keys). Attributes with `is_matching_key` are compared by Sourcing. |
| BR-COR-35 | A normalised alias maps to exactly one material within its scope (global or one supplier). A conflicting alias is rejected `core.alias_conflict` naming the owner. Normalisation = 00 §3.5 plus lower-casing Latin and collapsing spaces and punctuation. |
| BR-COR-36 | Material merge `from -> to`: same unit dimension or an existing conversion; aliases and alternatives re-point (duplicates dropped); `from` becomes `merged`; its name and code become aliases of the target; modules re-point through `IMaterialMergeParticipant`; redirect permanent, undo 90 days. |
| BR-COR-37 | `Match(text, categoryHint, supplierId)` score = 0.50 x trigram similarity(normalised text, `search_text`) + 0.30 if an alias equals the normalised text (supplier-scoped alias beats global) + 0.10 if `categoryHint` matches + 0.10 x share of `is_matching_key` attributes parsed from the text that agree. Up to 5 candidates with score and reason codes; `autoMatch = true` only if top score >= 0.85 and gap to second >= 0.15; inactive excluded, merged return their target. Response carries `matchVersion` (BR-X-10). Example: text `اسمنت 50 كيلو` hits alias `اسمنت` (0.30) with similarity 0.80 (0.40), category hint cement (0.10) = 0.80 plus attribute agreement 0.10 x 1 = **0.90**, gap to the next candidate 0.35, so `autoMatch = true`. |
| BR-COR-38 | Field visibility (BR-X-06): omitted for `foreman`, `superintendent` and any role without `core.cost_visibility.view`: project `contract_value_amount` and `contract_value_includes_vat`; party `payment_terms_days`, `credit_limit_amount`, `rating_score`, all bank data; all of `exchange_rates`; `boq_catalog_items.reference_rate_amount`. Project flag `site_roles_see_costs = true` reveals only the project contract value to them. Exports apply the same rules. |
| BR-COR-39 | Before deactivate, archive, merge or delete, Core calls every `IReferenceUsageProvider` and gets `{module, usageCount, openCount}`; a provider failure blocks the action (fail closed, `core.usage_check_failed`). |
| BR-COR-40 | `IWorkPackageDirectory.Validate`, `ICostCodeDirectory.IsPostable` and `IPartyDirectory.IsBlocked` use a per-request cache invalidated by `core.*` events within 5 s; payment and PO-issue callers pass `fresh=true`. |

---

## 7. API endpoints

Base `/api/v1/core`. All rules of 00 §3.6 apply: keyset pagination (`limit`, `cursor`, `withTotal`), `fields`, `include`, `If-Match` on `PATCH` and actions that mutate, `Idempotency-Key` on every `POST` that creates or runs an action, bulk as `POST /{resource}:bulk` (max 500), export as `GET /{resource}/export?format=`. Permission shorthand: `R` = `.view`, `W` = `.manage`; full codes in section 10. Error codes are in section 12. Every list below also accepts `filter[isDeleted]=false` implicitly; `q` is a trigram text search on the name/code fields.

### 7.1 Company, units, tax, calendar, rates (COR-E1)

| method | path | purpose | key request fields | key response fields | errors | permission |
| --- | --- | --- | --- | --- | --- | --- |
| GET / PATCH | `/company-settings` | read / update the single row | any column of 4.1.1 | settings, `etag` | `base_currency_locked`, `fiscal_start_invalid` | `core.company.view` / `.manage` |
| GET, POST | `/currencies`, `/currencies/{code}` PATCH | list, enable currency | `code`, `isEnabled` | currency | `currency_unknown` | `core.company.*` |
| GET, POST, PATCH | `/units`, `/units/{id}` | unit CRUD | `code`, `nameAr`, `dimension`, `toBaseFactor` | unit | `unit_system_immutable`, `unit_in_use` | `core.unit.view` / `.manage` |
| POST | `/units/{id}/deactivate`, `/reactivate` | toggle | none | unit | `unit_in_use` (warning only for deactivate) | `core.unit.manage` |
| GET, POST, PATCH, DELETE | `/unit-conversions` | cross-dimension and material pack conversions | `fromUnitCode`, `toUnitCode`, `factor`, `materialId?` | conversion | `unit_conversion_duplicate` | `core.unit.manage` |
| POST | `/units/convert` | calculator | `qty`, `from`, `to`, `materialId?` | `qty`, `path` | `unit_conversion_unavailable` | `core.unit.view` |
| GET, POST, PATCH | `/tax-codes`; `POST /tax-codes/{id}/rates` | tax codes; new rate range (closes the previous) | `code`, `taxType`, `ratePercent`, `effectiveFrom` | code, rates | `tax_rate_overlap`, `tax_code_in_use` | `core.tax_code.*` |
| GET | `/tax-codes/resolve?code=&date=` | rate in force | query | `ratePercent` | `tax_rate_missing` | `core.tax_code.view` |
| GET, POST | `/fiscal-years`, `POST /fiscal-years/{id}/generate-periods`, GET `/fiscal-periods` | years, periods | `startDate`, `endDate` | year, 12 periods | `fiscal_year_gap`, `fiscal_year_overlap`, `fiscal_period_missing` | `core.fiscal_calendar.*` |
| GET, POST, DELETE | `/exchange-rates`, `POST /exchange-rates:bulk` | rates (hidden from site roles) | `fromCurrency`, `toCurrency`, `rateDate`, `rate`, `rateType`, `projectId?` | rate | `fx_rate_invalid`, `fx_rate_stale` (on resolve) | `core.exchange_rate.view` / `.manage` |
| GET | `/exchange-rates/resolve` | `GetRate` | `from`, `to`, `date`, `type` | `rate`, `rateDate`, `ageDays` | `fx_rate_missing`, `fx_rate_stale` | `core.exchange_rate.view` |
| GET, POST, PATCH, DELETE | `/holidays` | holidays | `holidayDate`, `nameAr`, `projectId?` | holiday | `holiday_duplicate` | `core.calendar.*` |

Allowed filters/sorts: units `filter[dimension|isActive|isSystem]`, `sort=code|nameAr`; tax codes `filter[taxType|isActive]`; exchange rates `filter[fromCurrency|toCurrency|rateType|projectId|rateDate.from|rateDate.to]`, `sort=-rateDate`; holidays `filter[projectId|year]`, `sort=holidayDate`.

### 7.2 Projects and setup (COR-E2)

| method | path | purpose | key request fields | key response fields | errors | permission |
| --- | --- | --- | --- | --- | --- | --- |
| GET | `/projects` | list visible projects |  | `id, code, nameAr, status, projectType, clientPartyId, plannedStartDate, plannedEndDate, readinessPercent`; `contractValueAmount` only with visibility |  | `core.project.view` |
| POST | `/projects` | create (starts in `setup`) | `nameAr`, `projectType`, `contractCurrency`, `clientPartyId?`, dates, `siteTimezone` | project | `project_name_required`, `currency_not_enabled` | `core.project.create` |
| GET, PATCH | `/projects/{id}` | read / edit | any editable column | project, `etag` | `project_closed`, `project_contract_value_locked` | `core.project.view` / `.manage` |
| POST | `/projects/{id}/start-setup`, `/activate`, `/hold`, `/resume`, `/begin-closing`, `/close`, `/reopen`, `/cancel` | state actions (5.1) | `reason` (hold, close override, reopen, activate override), `override: bool` | project | `readiness_blockers_open`, `activation_override_reason_required`, `project_close_blocked`, `project_in_use`, `project_invalid_transition` | `core.project.manage`; `.activate_override`, `.close`, `.reopen` |
| GET | `/projects/{id}/setup` | 6 steps and status |  | steps |  | `core.project.view` |
| POST | `/projects/{id}/setup/{step}/complete`, `/skip`, `/reopen` | wizard step | `payload` per step | step | `setup_step_not_skippable`, `setup_step_blocked` | `core.project.manage` |
| GET | `/projects/{id}/readiness` | cached checks |  | checks with severity/result/message |  | `core.project.view` |
| POST | `/projects/{id}/readiness:evaluate` | force evaluation (live) |  | checks | `readiness_provider_unavailable` | `core.project.manage` |
| GET | `/projects/{id}/members` (proxy to Platform) | convenience |  | members |  | `core.project.view` |

Filters/sorts for `/projects`: `filter[status|projectType|clientPartyId|contractType|code|plannedStartDate.from|to]`, `q`, `sort=code|-createdAt|plannedStartDate|nameAr`.

### 7.3 Zones, trades, work packages, cost codes, templates (COR-E3, E4, E7)

| method | path | purpose | key request fields | key response fields | errors | permission |
| --- | --- | --- | --- | --- | --- | --- |
| GET, POST, PATCH | `/projects/{id}/zones`, `/zones/{zid}` | zone tree | `parentId`, `code`, `nameAr`, `zoneType`, `sortOrder` | zone, `path` | `zone_depth_exceeded`, `zone_cycle`, `zone_code_duplicate` | `core.zone.view` / `.manage` |
| POST | `/projects/{id}/zones:generate-floors` | floor generator | `buildingZoneId`, `basements`, `floors`, `roof`, `namePattern` | created zones | `zone_code_duplicate` | `core.zone.manage` |
| POST | `/zones/{zid}/archive`, `/move` | archive; reparent | `newParentId` | zone | `zone_has_active_children`, `zone_in_use`, `zone_cycle` | `core.zone.manage` |
| GET, POST, PATCH | `/trades`, `/trades/{id}` | trade library | `code`, `nameAr`, `defaultUnitCode`, `defaultCostCodeId`, `isEnabled` | trade | `trade_in_use` | `core.trade.view` / `.manage` |
| GET, POST, PATCH | `/cost-codes`, `/cost-codes/{id}` | cost code tree | `parentId`, `code`, `nameAr`, `category`, `tradeId?`, `isPostable` | code, `path`, `level` | `cost_code_duplicate`, `cost_code_has_children`, `cost_code_is_postable`, `cost_code_in_use` | `core.cost_code.view` / `.manage` |
| POST | `/cost-codes/{id}/deactivate`, `/reactivate`, `/move` | lifecycle | `newParentId` | code | `cost_code_in_use`, `cost_code_cycle` | `core.cost_code.manage` |
| GET | `/cost-codes/{id}/usage` | usage per module |  | `[{module, usageCount, openCount}]` | `usage_check_failed` | `core.cost_code.view` |
| GET, POST, PATCH | `/projects/{id}/work-packages`, `/work-packages/{wid}` | WP CRUD | `tradeId`, `zoneId?`, `code?`, `nameAr`, `progressMethod`, `isTimeRelated`, `plannedStart/End`, `costCodeIds[]` | WP, cost codes | `work_package_duplicate`, `progress_method_locked`, `zone_inactive`, `trade_disabled` | `core.work_package.view` / `.manage` |
| POST | `/work-packages/{wid}/activate`, `/hold`, `/resume`, `/complete`, `/reopen`, `/archive`, `/unarchive` | state actions (5.2) | `reason`, `acknowledgeWarnings` | WP, `warnings[]` | `work_package_open_items`, `work_package_invalid_transition` | `core.work_package.manage` |
| PUT | `/work-packages/{wid}/cost-codes` | replace linked codes | `costCodeIds[]`, `defaultCostCodeId` | codes | `cost_code_not_allowed_for_wp` | `core.work_package.manage` |
| POST | `/work-packages:bulk` | create many | items | per-item result |  | `core.work_package.manage` |
| GET | `/work-packages/validate?projectId=&workPackageId=&costCodeId=` | write-time check (BR-X-01) |  | `{valid, reasonCode}` |  | any authenticated |
| GET, POST, PATCH | `/trade-templates`, `/trade-templates/{id}/items` | templates | `code`, `nameAr`, items (`tradeId`, `scope`, `defaultCostCodeId`, `sortOrder`) | template | `template_in_use` | `core.template.view` / `.manage` |
| POST | `/projects/{id}/apply-template` | apply (idempotent) | `templateId`, `options` (`skipExisting`, `zoneTypes`) | `{created, skipped, applyRunId}` | `template_empty`, `project_closed` | `core.template.apply` |

Filters/sorts: zones `filter[zoneType|parentId|status]`; cost codes `filter[category|tradeId|parentId|isPostable|isActive]`, `q`, `sort=code`; work packages `filter[status|tradeId|zoneId|isTimeRelated|projectId]`, `q`, `sort=code|nameAr|-createdAt|plannedStartDate`.

### 7.4 Parties (COR-E5)

| method | path | purpose | key request fields | key response fields | errors | permission |
| --- | --- | --- | --- | --- | --- | --- |
| GET | `/parties` | list |  | `id, code, nameAr, partyKind, status, roles[], qualificationStatus`; `paymentTermsDays`, `ratingScore` only with visibility |  | `core.party.view` |
| POST | `/parties` | create (duplicate check) | `nameAr`, `nameEn?`, `partyKind`, `roles[]`, `identifiers[]`, `contacts[]` | party, `warnings[]` | `party_duplicate_hard`, `party_role_invalid` | `core.party.create` |
| GET, PATCH | `/parties/{id}` | read / edit |  | party (`include=roles,identifiers,contacts,scopes`) |  | `core.party.view` / `.manage` |
| POST | `/parties/{id}/approve`, `/reject`, `/block`, `/request-block`, `/unblock`, `/deactivate`, `/reactivate` | state actions (5.3) | `scope`, `reasonCode`, `reason` | party | `party_review_same_user`, `party_block_reason_required`, `party_unblock_forbidden` | `core.party.approve`, `.block`, `.manage` |
| POST | `/parties/{id}/merge` | merge into target | `targetPartyId`, `reason` | `mergeLogId` | `party_merge_invalid`, `usage_check_failed` | `core.party.merge` |
| GET | `/parties/duplicates?nameAr=&taxNo=` | pre-check |  | candidates with score |  | `core.party.view` |
| POST, PATCH, DELETE | `/parties/{id}/roles`, `/identifiers`, `/contacts`, `/scopes` | sub-resources | per 4.4 | row | `party_identifier_invalid` | `core.party.manage` |
| GET, POST | `/parties/{id}/bank-accounts` | accounts (hidden without bank permission) | `bankPartyId`, `accountName`, `accountNumber`, `iban`, `currency` | account | `iban_invalid` | `core.party_bank.view` / `.manage` |
| POST | `/bank-accounts/{bid}/verify`, `/reject`, `/deactivate` | verification (5.4) | `method`, `reason` | account | `bank_verifier_same_as_creator`, `bank_verification_method_required` | `core.party_bank.verify` |
| GET | `/parties/{id}/block-history`, `/rating-snapshots` | history |  | rows |  | `core.party.view` |

Filters/sorts for `/parties`: `filter[status|role|partyKind|qualificationStatus|governorate|q]`, `sort=nameAr|code|-createdAt`. Block scope and bank data: see BR-COR-38.

### 7.5 Materials (COR-E6)

| method | path | purpose | key request fields | key response fields | errors | permission |
| --- | --- | --- | --- | --- | --- | --- |
| GET, POST, PATCH | `/material-categories`, `/{id}/attributes` | categories, spec schema | `code`, `parentId`, `validityDays`, `defaultTradeId`, attributes | category | `category_in_use` | `core.material.view` / `.manage_catalog` |
| GET | `/materials` | list |  | `id, code, nameAr, categoryId, baseUnitCode, status, validityDays` |  | `core.material.view` |
| POST, PATCH | `/materials`, `/materials/{id}` | create / edit | `nameAr`, `categoryId`, `baseUnitCode`, `specs`, `defaultCostCodeId?` | material, `warnings[]` | `material_spec_invalid`, `material_duplicate_warning` | `core.material.create` / `.manage` |
| POST | `/materials/{id}/approve`, `/deactivate`, `/reactivate`, `/merge` | state actions (5.5) | `targetMaterialId`, `reason` | material | `material_merge_invalid`, `usage_check_failed` | `core.material.approve`, `.merge` |
| POST, DELETE | `/materials/{id}/aliases`, `/alternatives` | aliases and substitutes | `aliasText`, `supplierId?` | alias | `alias_conflict` | `core.material.manage` |
| POST | `/materials/match` | `Match(text, hints)` | `text`, `categoryHint?`, `supplierId?` | `candidates[{materialId, score, reasons[]}]`, `autoMatch`, `matchVersion` |  | `core.material.view` |
| POST | `/materials:import` | import via PLT-E9 framework (dry-run then commit) | file id | job id |  | `core.material.manage` |
| GET | `/boq-catalog-items` \[R2\] | standard BOQ items |  | items |  | `core.boq_catalog.view` |

Filters/sorts for `/materials`: `filter[categoryId|status|baseUnitCode|tradeId]`, `q`, `sort=code|nameAr|-updatedAt`.

### 7.6 Platform-style endpoints owned by Core

| method | path | purpose | permission |
| --- | --- | --- | --- |
| POST | `/seed:run` | run seeder for tenant (`seedName?`) returns `seedRun` | `core.seed.run` (`tenant_admin`) |
| GET | `/lookups` | single call returning enabled units, currencies, trades, tax codes, enabled cost-code roots for client caches (ETag, 5 min) | authenticated |
| GET | `/reference-usage?entityType=&entityId=` | usage count for any entity | `core.company.view` |

### 7.7 Key examples

Create project:

```txt
POST /api/v1/core/projects
Idempotency-Key: 6f1c...
{ "nameAr": "برج النخيل", "projectType": "residential", "contractType": "lump_sum",
  "contractCurrency": "EGP", "clientPartyId": "0192...", "siteTimezone": "Africa/Cairo",
  "plannedStartDate": "2026-11-01", "plannedEndDate": "2028-04-30" }
```

```json
201 { "id": "0192a...", "code": "P-2026-004", "status": "setup", "readinessPercent": 17,
      "setup": [ { "step": "details", "status": "in_progress" }, { "step": "template", "status": "not_started" } ],
      "etag": "W/\"1\"" }
```

Activate with blockers (not overridden):

```json
POST /projects/0192a.../activate   If-Match: W/"9"
422 { "code": "core.readiness_blockers_open", "status": 422,
      "params": { "blockers": ["boq_budget_missing", "no_active_zone"] } }
```

Activate with override (`owner_executive`):

```json
{ "override": true, "reason": "Site mobilisation starts Sunday; budget upload follows." }
200 { "status": "active", "goLiveOverridden": true, "actualStartDate": "2026-11-01" }
```

Apply template:

```json
POST /projects/0192a.../apply-template
{ "templateId": "0192t...", "options": { "skipExisting": true } }
200 { "applyRunId": "0192r...", "created": 14, "skipped": 0,
      "warnings": [ { "code": "core.template_item_no_zone", "params": { "item": "MEP-RISER" } } ] }
```

Material match (golden: BR-COR-37):

```json
POST /materials/match  { "text": "اسمنت 50 كيلو", "categoryHint": "cement" }
200 { "autoMatch": true, "matchVersion": 3,
      "candidates": [ { "materialId": "0192m...", "score": 0.90, "reasons": ["alias_global", "category_hint", "trigram"] },
                      { "materialId": "0192n...", "score": 0.55, "reasons": ["trigram"] } ] }
```

Block party:

```json
POST /parties/0192p.../block  { "scope": "all", "reasonCode": "fraud_suspected", "reason": "Bank letter forged" }
200 { "status": "blocked", "blockScope": "all" }
```

---

## 8. Events

Envelope per 00 §3.9; partition key = aggregate id. Core payloads carry ids and the fields consumers need; consumers re-read through Core contracts for anything else.

### 8.1 Published (registry names fixed)

| Event | Fires when | Payload |
|---|---|---|
| `core.project.created.v1` | project insert commits | \`projectId, code, nameAr, projectType, contractCurrency, clientPartyId, status, origin ('manual' |
| `core.project.activated.v1` | `activate` | `projectId, actualStartDate, goLiveOverridden, overriddenCheckKeys[], tradeTemplateId?` |
| `core.project.closed.v1` | `close` | `projectId, actualEndDate, closedAt, overridden: bool` |
| `core.work_package.created.v1` | create or template apply | `workPackageId, projectId, tradeId, zoneId?, code, nameAr, status, progressMethod, isTimeRelated, costCodeIds[], defaultCostCodeId?` |
| `core.work_package.updated.v1` | any field or status change | `workPackageId, projectId, changedFields[], status, previousStatus?, costCodeIds[]` |
| `core.work_package.archived.v1` | `archive` | `workPackageId, projectId, code` |
| `core.cost_code.created.v1` | insert | `costCodeId, code, nameAr, category, tradeId?, parentId?, isPostable, path` |
| `core.cost_code.updated.v1` | edit, move, activate/deactivate | `costCodeId, changedFields[], isActive, isPostable, path` |
| `core.party.created.v1` | insert | `partyId, code, nameAr, partyKind, roles[], status` |
| `core.party.updated.v1` | edit, approve, deactivate, reactivate, role or qualification change | `partyId, changedFields[], status, roles[], qualificationStatus` |
| `core.party.blocked.v1` | `block` | `partyId, scope, reasonCode, blockedAt` (reason text not included) |
| `core.material.created.v1` | insert | `materialId, code, nameAr, categoryId, baseUnitCode, status, validityDays` |
| `core.material.updated.v1` | edit, approve, (de)activate, alias change | `materialId, changedFields[], status, validityDays` |

### 8.2 Consumed

| Event | Handler | What it does | Idempotency |
|---|---|---|---|
| `estimating.tender.won.v1` | `TenderWonHandler` \[R3\] | Creates a `draft` project with client, name, value, currency; stores `origin_type='tender'`, `origin_id`; notification `COR_PROJECT_DRAFT_FROM_TENDER` | `processed_events` plus unique `(origin_type, origin_id)`; replay returns existing project, publishes nothing (BR-COR-13) |
| `procurement.supplier.rated.v1` | `RatingCacheHandler` | Upserts `party_rating_snapshots` and updates `parties.rating_score` | Upsert key `(party_id, as_of)`; ignore events older than the stored `as_of` |
| `contracts.contract.signed.v1` | `ProjectContractValueHandler` | Marks `contract_value_source = 'contract'`; project field becomes read-only (BR-COR-12) | Last-write by `signedAt`; replay is a no-op |

### 8.3 Proposed registry additions

The following are referenced by section 5 and need registry entries (consumers in brackets):

| Event | Payload | Why |
|---|---|---|
| `core.project.status_changed.v1` (planning, cost, procurement, reporting) | `projectId, from, to, reason?, changedAt` | hold, resume, begin-closing and reopen must be visible to modules enforcing BR-COR-17 without polling |
| `core.party.unblocked.v1` (procurement, sourcing, subcontracts) | `partyId, unblockedAt` | clear cached blocks |
| `core.party.merged.v1` (all `IReferenceUsageProvider` modules) | `fromPartyId, toPartyId, mergeLogId` | re-point references |
| `core.party.bank_account_verified.v1` (procurement) | `partyId, bankAccountId, currency` | release held payment lines |
| `core.material.merged.v1` (sourcing, procurement, estimating) | `fromMaterialId, toMaterialId, mergeLogId` | re-point (BR-COR-36) |
| `core.fiscal_year.created.v1` (cost) | `fiscalYearId, startDate, endDate` | Cost creates its period-close records |

---

## 9. Background jobs and schedules

All jobs run through PLT-E12; timestamps in the tenant time zone; each is per-tenant unless stated; each is idempotent and records a `platform.job_runs` row.

| Job | Schedule | What | Idempotency |
|---|---|---|---|
| `core_consistency_check` | nightly 02:00 | Verifies by-value references from other modules into `core` (work packages, cost codes, parties, materials) exist and are active where required; reports orphans to `platform.data_quality_findings` | Findings keyed `(entity, id, rule)`; reruns update, not duplicate |
| `core_duplicate_scan` | nightly 02:30 | Parties: normalised name equality, trigram >= 0.8, same phone; materials: trigram >= 0.85 within category; writes suggestions | Suggestion unique `(entity_type, a_id, b_id)` |
| `core_fx_stale_check` | daily 07:00 | For each enabled foreign currency in use, if the latest rate is older than `fx_max_age_days` notify `finance_controller` (`COR_FX_STALE`) | One notification per currency per stale episode |
| `core_fiscal_year_reminder` | on 1 November | If no fiscal year covers the next 90 days, notify `finance_controller` (`COR_FISCAL_YEAR_MISSING`) | Dedupe key `(tenant, year)` |
| `core_party_qualification_expiry` | daily 06:00 | Sets `qualification_status = expired` where `qualified_until < today`; publishes `core.party.updated.v1`; notifies 30 days before (`COR_QUALIFICATION_EXPIRING`) | Update only rows not yet expired |
| `core_readiness_refresh` | every 15 min for projects in `setup` | Re-evaluates readiness cache for projects touched in the last day | Overwrites cache row |
| `core_rate_import` \[R2\] | daily 08:00 | Imports central-bank/market rates from the configured feed | Unique `(from,to,rate_date,rate_type)` upsert |
| `core_usage_cache_refresh` | hourly | Fills `reference_usage_cache` for the usage screens | Overwrite |
| `core_seed_apply` | on tenant creation or new seed version | `TenantSeeder` (BR-COR-30) | `seed_runs` marker |
| `core_export_runner` | on demand | Large exports (> 5,000 rows) | Job id returned to caller |

---

## 10. Permissions

Roles per 00 §4.4. `TA` tenant\_admin, `OE` owner\_executive, `PM` project\_manager, `SU` superintendent, `FM` foreman, `PO` procurement\_officer, `FC` finance\_controller, `QS` cost\_qs\_engineer, `CM` contracts\_manager, `DC` document\_controller, `ES` estimator. `QA`, `SO` (safety) have only `view` permissions on projects, work packages and zones.

| Permission | Roles | Notes |
|---|---|---|
| `core.company.view` | all internal roles | Settings without bank or tax-registration details for `SU`, `FM` |
| `core.company.manage` | TA, OE |  |
| `core.unit.view` / `core.tax_code.view` / `core.calendar.view` / `core.fiscal_calendar.view` | all internal |  |
| `core.unit.manage`, `core.tax_code.manage`, `core.calendar.manage` | TA, QS (units) , FC (tax codes) |  |
| `core.fiscal_calendar.manage` | TA, FC |  |
| `core.exchange_rate.view` / `.manage` | OE, FC, QS, PO view; FC, TA manage | Needs `core.cost_visibility.view`; omitted for `SU`, `FM` (BR-COR-38) |
| `core.cost_visibility.view` | OE, PM, FC, QS, PO, CM, ES, TA | The master switch for cost fields (00 BR-X-06) |
| `core.project.view` | all internal roles, **project-scoped** by `platform.project_members` (OE, FC see all) | `contractValueAmount` needs `core.cost_visibility.view` |
| `core.project.create` | PM, OE, TA |  |
| `core.project.manage` | PM (own projects), OE, TA |  |
| `core.project.activate_override` | OE |  |
| `core.project.close`, `core.project.reopen` | close: OE, PM; reopen: OE |  |
| `core.zone.view` / `.manage` | all internal / PM, SU (create only), QS |  |
| `core.trade.view` / `.manage` | all / TA, PM, QS |  |
| `core.cost_code.view` | all internal |  |
| `core.cost_code.manage` | TA, QS, FC |  |
| `core.work_package.view` | all internal, project-scoped |  |
| `core.work_package.manage` | PM, QS, TA | `SU` may create when tenant flag `site_creates_wp` is on \[R2\] |
| `core.template.view` / `.manage` / `.apply` | all / TA, QS / PM, QS |  |
| `core.party.view` | all internal roles; `supplier_user` and others only their own party via portals | Hidden for non-cost roles: `paymentTermsDays`, `creditLimitAmount`, `ratingScore` |
| `core.party.create` | PO, CM, TA |  |
| `core.party.manage` | PO, CM, TA | Role and identifier edits |
| `core.party.approve` | TA, FC | Not the creator when review flag is on |
| `core.party.block` | FC, TA, OE | PO and PM have only `request-block` through `core.party.request_block` |
| `core.party.merge` | TA |  |
| `core.party_bank.view` | FC, TA, OE; PO view masked (last 4) | Bank data omitted for everyone else |
| `core.party_bank.manage` | PO, FC |  |
| `core.party_bank.verify` | FC | Never the creator; consider limit by amount \[open question 4\] |
| `core.material.view` | all internal |  |
| `core.material.create` / `.manage` | PO, QS, TA / QS, TA |  |
| `core.material.approve`, `core.material.merge` | TA, QS / TA |  |
| `core.material.manage_catalog` | TA, QS | Categories and attributes |
| `core.boq_catalog.view` | ES, QS, CM, PM | `referenceRateAmount` needs cost visibility \[R2\] |
| `core.seed.run` | TA |  |

Amount limits: none in Core (no approvals on amounts). Project scope: `projects`, `zones`, `work_packages`, `project_members` rows are filtered by the user's visible projects; parties, materials, cost codes, units are tenant-wide. External users never see Core endpoints; portals use their own facade endpoints (PRT module).

---

## 11. Notifications

Delivered through PLT-E7; channels `in_app` (always), `push`, `whatsapp`, `email` per user preference. Templates exist in `ar` and `en`; Arabic shown.

| Code | Recipients | Trigger | Channels | Arabic sample |
| --- | --- | --- | --- | --- |
| `COR_PROJECT_DRAFT_FROM_TENDER` | `owner_executive`, `project_manager` | tender won handler creates a draft | in\_app, whatsapp | تم إنشاء مشروع مبدئي «\{project\}» من المناقصة التي فزتم بها. أكملوا الإعداد. |
| `COR_PROJECT_ACTIVATED` | project members | `activate` | in\_app, push | تم تفعيل المشروع \{project\}. |
| `COR_PROJECT_OVERRIDE` | `owner_executive`, `finance_controller` | activation with override | in\_app, email | تم تفعيل المشروع \{project\} مع تجاوز بنود ناقصة: \{checks\}. |
| `COR_PROJECT_ON_HOLD` / `COR_PROJECT_RESUMED` | project members | hold, resume | in\_app, push | تم إيقاف المشروع \{project\} مؤقتاً: \{reason\}. |
| `COR_PARTY_PENDING_REVIEW` | `tenant_admin`, `finance_controller` | party created in `pending_review` | in\_app | طرف جديد «\{party\}» بانتظار المراجعة. |
| `COR_PARTY_BLOCKED` | `procurement_officer`, `finance_controller`, project managers with open orders | `block` | in\_app, whatsapp | تم إيقاف التعامل مع «\{party\}» (\{scope\}). |
| `COR_PARTY_BLOCK_REQUESTED` | `finance_controller` | `request-block` | in\_app | طلب \{user\} إيقاف التعامل مع «\{party\}»: \{reason\}. |
| `COR_BANK_VERIFY_REQUESTED` | `finance_controller` (not creator) | bank account created | in\_app | حساب بنكي جديد للمورد «\{party\}» بانتظار التحقق. |
| `COR_BANK_REJECTED` | account creator | `reject` | in\_app | تم رفض الحساب البنكي للمورد «\{party\}»: \{reason\}. |
| `COR_QUALIFICATION_EXPIRING` | `procurement_officer` | 30 days before `qualified_until` | in\_app, email | تأهيل المورد «\{party\}» ينتهي في \{date\}. |
| `COR_FX_STALE` | `finance_controller` | `core_fx_stale_check` | in\_app | سعر صرف \{currency\} لم يُحدَّث منذ \{days\} يوماً. |
| `COR_FISCAL_YEAR_MISSING` | `finance_controller` | job | in\_app, email | لا توجد سنة مالية تغطي الفترة القادمة. |
| `COR_DUPLICATE_SUGGESTIONS` | `tenant_admin` | weekly digest of scan | in\_app | يوجد \{n\} اقتراحات لدمج أطراف أو مواد مكررة. |
| `COR_MATERIAL_PENDING_REVIEW` | `tenant_admin`, `cost_qs_engineer` | material in `pending_review` | in\_app | مادة جديدة «\{material\}» بانتظار الاعتماد. |

---

## 12. Error codes

All are `core.<code>`; shown without prefix. Messages come from the catalogue (00 §3.7).

| code | HTTP | when | Arabic | English |
| --- | --- | --- | --- | --- |
| `base_currency_locked` | 422 | change base currency with projects present | لا يمكن تغيير العملة الأساسية بعد إنشاء مشروع. | Base currency cannot change once a project exists. |
| `fiscal_start_invalid` | 422 | start month outside 1-12 | شهر بداية السنة المالية غير صحيح. | Invalid fiscal start month. |
| `currency_unknown` / `currency_not_enabled` | 422 | not ISO or not enabled | العملة غير مفعلة. | Currency is not enabled. |
| `unit_system_immutable` | 422 | edit protected fields of system unit | لا يمكن تعديل وحدة النظام. | System units cannot be changed. |
| `unit_in_use` | 409 | delete used unit | الوحدة مستخدمة. | Unit is in use. |
| `unit_conversion_unavailable` | 422 | no path | لا يوجد تحويل بين الوحدتين. | No conversion between these units. |
| `unit_conversion_duplicate` | 409 | duplicate pair | تحويل مكرر. | Duplicate conversion. |
| `tax_rate_overlap` | 422 | ranges overlap | تداخل في فترات النسبة الضريبية. | Tax rate ranges overlap. |
| `tax_rate_missing` | 422 | no rate on date | لا توجد نسبة ضريبية في هذا التاريخ. | No tax rate in force on that date. |
| `tax_code_in_use` | 409 | delete or change used code | الكود الضريبي مستخدم. | Tax code is in use. |
| `fiscal_year_gap` / `fiscal_year_overlap` | 422 | non-contiguous or overlapping year | السنوات المالية يجب أن تكون متتالية. | Fiscal years must be contiguous. |
| `fiscal_period_missing` | 422 | date outside defined periods | لا توجد فترة مالية لهذا التاريخ. | No fiscal period for this date. |
| `fx_rate_invalid` | 400 | rate \<= 0 | سعر الصرف غير صحيح. | Invalid exchange rate. |
| `fx_rate_missing` | 422 | no rate found | لا يوجد سعر صرف. | No exchange rate found. |
| `fx_rate_stale` | 422 / warning | rate older than `fx_max_age_days` | سعر الصرف قديم (\{days\} يوماً). | Exchange rate is stale (\{days\} days). |
| `holiday_duplicate` | 409 | same date and scope | الإجازة مسجلة. | Holiday already exists. |
| `project_name_required` | 400 | no Arabic name | اسم المشروع مطلوب. | Project name is required. |
| `project_closed` | 422 | mutation on closed or cancelled project | المشروع مغلق. | The project is closed. |
| `project_on_hold` | 422 | new commitment on held project | المشروع متوقف مؤقتاً. | The project is on hold. |
| `project_invalid_transition` | 409 | action not allowed from status | الإجراء غير متاح في حالة المشروع الحالية. | Action not allowed in current project status. |
| `project_contract_value_locked` | 422 | edit after contract signed | قيمة العقد تُدار من العقود. | Contract value is managed by Contracts. |
| `project_in_use` | 409 | cancel with usage | لا يمكن إلغاء مشروع له حركات. | Project has transactions. |
| `project_close_blocked` | 422 | close providers report blockers | يوجد بنود تمنع إغلاق المشروع. | Items block project closure. |
| `readiness_blockers_open` | 422 | activate with failing blockers | بنود إلزامية غير مكتملة. | Mandatory readiness items are incomplete. |
| `activation_override_reason_required` | 422 | override without reason (>= 10 chars) | سبب التجاوز مطلوب. | Override reason is required. |
| `readiness_provider_unavailable` | 503 | provider did not answer | تعذر تقييم الجاهزية. | Readiness could not be evaluated. |
| `setup_step_not_skippable` / `setup_step_blocked` | 422 | BR-COR-14 | لا يمكن تخطي هذه الخطوة. | This step cannot be skipped. |
| `zone_depth_exceeded` / `zone_cycle` / `zone_code_duplicate` | 422 / 422 / 409 | BR-COR-18 | شجرة المناطق غير صالحة. | Invalid zone tree. |
| `zone_has_active_children` / `zone_in_use` / `zone_inactive` | 422 | archive guards, inactive zone selected | المنطقة لا يمكن أرشفتها. | Zone cannot be archived. |
| `trade_in_use` / `trade_disabled` | 409 / 422 | trade guards | التخصص مستخدم أو غير مفعل. | Trade is in use or disabled. |
| `work_package_duplicate` | 409 | BR-COR-19 | يوجد بند عمل لنفس التخصص والمنطقة. | A work package exists for this trade and zone. |
| `work_package_open_items` | 422 | archive/complete with open items | توجد معاملات مفتوحة على بند العمل. | Work package has open items. |
| `work_package_invalid_transition` | 409 | wrong status | الإجراء غير متاح لحالة بند العمل. | Action not allowed in current status. |
| `progress_method_locked` | 422 | BR-COR-20 | لا يمكن تغيير طريقة قياس الإنجاز. | Progress method is locked. |
| `cost_code_not_allowed_for_wp` | 422 | BR-COR-21 | كود التكلفة غير مسموح لبند العمل. | Cost code not allowed for this work package. |
| `cost_code_duplicate` / `cost_code_cycle` | 409 / 422 | tree integrity | كود التكلفة مكرر أو يسبب حلقة. | Duplicate or cyclic cost code. |
| `cost_code_has_children` / `cost_code_is_postable` | 422 | BR-COR-23 | لا يمكن الترحيل على كود له فروع. | Parent codes are not postable. |
| `cost_code_inactive` / `cost_code_in_use` | 422 / 409 | BR-COR-31 | كود التكلفة غير نشط أو مستخدم. | Cost code inactive or in use. |
| `template_empty` / `template_in_use` | 422 / 409 | template guards | القالب فارغ أو مستخدم. | Template empty or in use. |
| `party_duplicate_hard` | 409 | same tax/commercial register | الطرف مسجل مسبقاً (\{party\}). | Party already registered (\{party\}). |
| `party_duplicate_soft` | 200 warning | similar name | يوجد طرف مشابه. | A similar party exists. |
| `party_role_invalid` / `party_identifier_invalid` | 422 | bad role or identifier format | بيانات الطرف غير صحيحة. | Invalid party data. |
| `party_review_same_user` | 422 | approver equals creator | لا يجوز اعتماد طرف أنشأته. | You cannot approve a party you created. |
| `party_block_reason_required` | 422 | block without reason | سبب الإيقاف مطلوب. | Block reason is required. |
| `party_unblock_forbidden` | 403 | fraud block by non-owner | رفع الإيقاف يتطلب المدير التنفيذي. | Only the executive owner can unblock. |
| `party_blocked` | 422 | callers using blocked party | الطرف موقوف. | Party is blocked. |
| `party_merge_invalid` | 422 | BR-COR-28 | لا يمكن دمج هذين الطرفين. | These parties cannot be merged. |
| `iban_invalid` | 422 | mod-97 fails | رقم الآيبان غير صحيح. | Invalid IBAN. |
| `bank_verifier_same_as_creator` | 422 | BR-COR-26 | لا يجوز التحقق من حساب أنشأته. | You cannot verify an account you created. |
| `bank_verification_method_required` | 422 | no method | طريقة التحقق مطلوبة. | Verification method required. |
| `bank_account_name_mismatch` | 200 warning | BR-COR-27 | اسم الحساب لا يطابق اسم الطرف. | Account name differs from party name. |
| `material_spec_invalid` | 422 | BR-COR-34 | مواصفات المادة غير صحيحة. | Invalid material specifications. |
| `material_duplicate_warning` | 200 warning | similar material | توجد مادة مشابهة. | A similar material exists. |
| `material_merge_invalid` | 422 | BR-COR-36 | لا يمكن دمج المادتين. | Materials cannot be merged. |
| `alias_conflict` | 409 | BR-COR-35 | الاسم البديل مستخدم لمادة أخرى. | Alias belongs to another material. |
| `category_in_use` | 409 | delete used category | التصنيف مستخدم. | Category is in use. |
| `usage_check_failed` | 503 | BR-COR-39 | تعذر التحقق من الاستخدام. | Usage check failed. |
| `seed_version_unknown` | 422 | bad seed name | حزمة بيانات غير معروفة. | Unknown seed. |
| `template_item_no_zone` | 200 warning | template item needs zone but none exist | لا توجد منطقة مناسبة للبند. | No matching zone for the item. |

---

## 13. Reports and read models

Core is small enough that most reads hit base tables; three read models support lists and dashboards. Refresh uses the 00 §3.15 mechanism (event-driven upsert), with a nightly rebuild.

| table | columns | refresh |
|---|---|---|
| `core.rm_project_overview` | `project_id, code, name_ar, status, readiness_percent, blockers_open int, wp_total int, wp_active int, zone_count int, member_count int, contract_value_amount (hidden), as_of` | on project, WP, zone and readiness events; rebuild nightly |
| `core.rm_party_directory` | `party_id, code, name_ar, name_en, status, roles text[], qualification_status, is_blocked bool, block_scope, rating_score, last_used_at, search_text` (trigram index) | on `core.party.*` events plus rating cache |
| `core.rm_work_package_tree` | `work_package_id, project_id, zone_path text, trade_code, code, name_ar, status, cost_code_codes text[], planned_start, planned_end` | on `core.work_package.*` and zone events |

Reports (list + export, columns): **Project register** (code, name, client, type, status, planned and actual dates, contract value\* , readiness %); **Setup readiness** (project, check key, severity, result, message); **Work package register** (project, zone path, trade, code, name, status, progress method, cost codes); **Cost code list** (code, path, category, trade, postable, active, usage count); **Party register** (code, name, roles, status, qualification, blocked, payment terms\*, rating\*); **Bank accounts pending verification** (party, bank, IBAN masked, created by, age days); **Material catalogue** (code, name, category, unit, status, validity days, alias count); **Duplicate suggestions**. Fields marked \* obey BR-COR-38. Exports follow 00 §3.14 and are audited.

---

---

## 15. Test scenarios, non-functional notes, open questions

### 15.1 Named scenarios

| # | Scenario | Input | Expected |
|---|---|---|---|
| T1 | Golden project setup | Create "Tower A" (residential, EGP), apply residential template with 1 building zone | project `P-<year>-001`, 19 trades enabled, package `B1-PLUMB` exists with default cost codes; readiness shows boq\_budget as the only open blocker until Cost uploads the budget |
| T2 | Activation blocked and overridden | Blocker `boq_budget_missing` failing; PM then owner activate | PM 422 `readiness_blockers_open`; owner with reason 200, `goLiveOverridden = true`, audit lists the key |
| T3 | Closed project rejects writes | Close project; `IProjectDirectory.Get` | `status = closed`; callers refuse new records; `reopen` by owner returns to `closing` |
| T4 | Work package uniqueness | Two non-archived packages for (Tower A, PLUMB, B1) | second 409; after archiving the first the second creation succeeds |
| T5 | Archive guard | Package with one open PO | 422 `work_package_open_items`; with provider down 503 `usage_check_failed` |
| T6 | Plumbing golden numbers carried | Package `B1-PLUMB`, BAC 1,200,000 (Cost), AC 540,000, 40% complete | Core only supplies ids; Cost computes CPI 0.89, SPI 0.80, EAC 1,350,000; Core's `Validate` returns valid for the package and its cost codes |
| T7 | Cement unit conversion | 60 t cement, offers priced per bag (50 kg) | `Convert(60, t, bag, cement)` = 1,200 bag via material pack conversion; no path for m to kg gives `unit_conversion_unavailable` |
| T8 | Material auto-match | `اسمنت 50 كيلو`, hint cement | score 0.90, gap 0.35, `autoMatch = true`; text `اسمنت` alone with similarity 0.60: 0.30 + 0.30 + 0.10 = 0.70, `autoMatch = false` |
| T9 | Supplier duplicates | Create suppliers A (tax no. 123-456-789) and a second with the same number | second 409 `party_duplicate_hard`; names differing by alef variant produce a soft warning |
| T10 | Block scopes | Block supplier C with `new_orders`, then `all` | `IsBlocked(C,'new_order')` true; payment check false until scope `all`; excluded C in the cement sourcing example is a Sourcing rule, not Core |
| T11 | Bank maker-checker | PO creates account; PO verifies; FC verifies | PO 422 `bank_verifier_same_as_creator`; FC success, event published once, previous account `inactive` |
| T12 | Tax rate change | VAT 14% until 2026-11-30, 15% after | resolve(2026-11-30)=14, resolve(2026-12-01)=15; payment application 4 net 2,063,675 gives VAT 288,914.50 at 14% (rounded 288,915 per Contracts rule) and payable 2,352,590 |
| T13 | Fiscal calendar | Start month 1, generate 2027 | 12 periods; `PeriodOf(2027-02-28)` = Feb; gap year rejected |
| T14 | FX staleness | USD rate 9 days old, max age 7 | warning for spot; error for contract type; one `COR_FX_STALE` |
| T15 | Field visibility | Foreman reads project, party, exchange rates | contract value, payment terms, credit limit, rating, bank data, rates omitted (not null); flag `site_roles_see_costs` reveals contract value only |
| T16 | Tender replay | `estimating.tender.won.v1` delivered 3 times | one draft project |
| T17 | Seed idempotency | Seed, rename a seeded trade, re-seed new version | counts stable; rename kept; `skipped_customized = 1` |
| T18 | Tenant isolation | Tenant A token against B ids on every endpoint, and raw SQL with wrong `app.tenant_id` | 404 / zero rows |
| T19 | Idempotent create | Same `Idempotency-Key` and body twice; same key different body | replay of first response; 422 `idempotency_key_reuse` |
| T20 | Stale write | PATCH project with old `If-Match` | 412 with current version |

### 15.2 Non-functional notes

| Area | Expectation |
|---|---|
| Volumes per tenant | projects 50-500; zones 100 per project; work packages 50-300 per project; cost codes 500-3,000; parties 500-20,000; materials 2,000-100,000; exchange rates 10 per day maximum |
| Latency (p95, warm) | `GET /projects` 150 ms; party and material list 300 ms; `materials/match` 80 ms; `work-packages/validate` 20 ms; activate with 4 providers 1.5 s; template apply of 60 packages 3 s |
| Indexes | `projects(tenant_id, status, code)`; `work_packages(tenant_id, project_id, status)` and the partial unique on `(project_id, trade_id, coalesce(zone_id), ...) where status <> 'archived'`; `cost_codes(tenant_id, parent_id)`, `(tenant_id, code)`; GIN trigram on `parties.search_text`, `materials.search_text`, `material_aliases.alias_normalized`; `exchange_rates(tenant_id, from_currency, to_currency, rate_type, rate_date desc)` |
| Caching | Contract queries per-request cache plus 5-minute ETag on `/lookups`; invalidated by `core.*` events within 5 s |
| Concurrency | Optimistic via `row_version`; series numbering inside the create transaction; unique constraints are the final arbiter for duplicates |
| Event volume | Low (hundreds per day per tenant); bulk import emits per record, throttled to 200 events per second per tenant |

### 15.3 Security notes

Bank account numbers and IBANs are encrypted at column level and masked in logs and audit diffs (last 4 only). Party block reasons of type `fraud_suspected` are visible only to `finance_controller`, `owner_executive`, `tenant_admin`. All role, identifier and bank changes are audited with before and after values. Exports containing hidden fields are filtered server-side and audited. Seeds contain no personal data. Project scope is enforced in the repository layer and again by RLS on `project_id`-bearing tables where applicable. Rate limits per 00 §3.6; `/materials/match` limited to 20 req/s per tenant.

### 15.4 Open questions for the business

1. Do contractors want a party to be both supplier and subcontractor under one record with a single credit and payment history, or separate histories per role?
2. Is the work package unique per (project, trade, zone) acceptable, or do some trades need several parallel packages in the same zone (for example structure phase 1 and phase 2)?
3. Which readiness items must be blockers for every tenant, and which should each tenant be able to downgrade to warnings?
4. Should bank account verification require a second approver by amount (for example above 500,000 EGP of expected payments), or is one finance controller enough?
5. Does a supplier unblocked after `fraud_suspected` always need the executive owner, or may the finance controller with a documented reason?
6. How long must an inactive party or material remain visible in pickers for historical documents, and should archived work packages stay visible in reports by default?
7. For Saudi expansion: separate tax codes (VAT 15%), Hijri dates, SAR as base currency and Saudi identifiers (CR number, VAT number): which are needed in the first Saudi tenant?
8. Is a 7-day default staleness window for exchange rates right, and who is responsible for entering rates when the feed is not yet available (R2)?
9. Should the material master be shared across all tenants as a reviewed central catalogue (with tenant overrides), or stay private per tenant?
10. Do clients and consultants appear as parties with portal access in R1, or only as names on projects until portals (R2)?
11. What is the maximum zone depth contractors actually use (the spec assumes 5), and do they need drawings or GPS attached to zones?
12. Who may cancel a project in `setup` that already has work packages created by a template: project manager alone, or the owner executive?
