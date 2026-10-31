---
title: "04 Estimating and tendering"
doc_no: "04"
section: "Tender and Estimate"
status: "Proposal"
source: https://app.notion.com/p/f324cdd2129683dc9ed981ec0044933b?pvs=204
exported: 2026-10-29 (Notion MCP)
---

# 04 — Estimating and Tendering (Module 2) — Backend Specification

**Key:** `EST`  **Schema:** `estimating`  **Release:** R3 for the full module; **R1** carries only the *simple estimate import* (Excel estimate or budget imported into a draft estimate, finalised against an existing project; epic EST-E9). <br> **Business source:** v3 spec section 4.2 (lines 470-507) and flow F1 (lines 185-191). **Conventions:** this file obeys `00-Backend-Architecture-and-Conventions.md` ("00"); it does not restate them. Section references such as "per 00 §3.6" point there. <br> **Related files:** `02` Core (projects, trades, cost codes, parties, materials), `07` Sourcing (price book events), `08` Cost (draft budget), `09` Contracts (draft revenue BOQ), `13` Closeout (rate observations and lessons feed the rate library), `14` Intelligence (extraction suggestions).

---

## 1. Purpose and business context

### 1.1 The business problem in plain language

A contractor earns money only if the price it quotes to the client is higher than what the job really costs, and only if it wins enough jobs at that price. Before there is a project there is a **tender** (مناقصة): the client (a developer, an authority, a hospital owner) sends documents and asks several contractors to quote a price by a deadline. The documents contain:

- a **bill of quantities** (BOQ, جدول الكميات / المقايسة): a numbered list of work items with a unit (m2, m3, ton, item, lump sum) and a quantity, for example "Reinforced concrete C35 for slabs, m3, 4,200". The contractor writes a **unit rate** (سعر الوحدة) against each item; quantity x rate = item price; the sum is the bid;
- **drawings** (رسومات), **specifications** (المواصفات) and **conditions of contract** (شروط التعاقد) that say how and when the contractor is paid, how much is held back as retention (محتجز ضمان), what penalties (غرامات تأخير) apply, how long the warranty (فترة الضمان) lasts, and so on.

Today an estimator re-types the BOQ into Excel, hunts for rates in old files, phones suppliers, adds percentages for overheads and profit, and later the winner's numbers are typed again into the project budget. Three losses follow: prices without a traceable source, risky clauses missed, and a budget that does not match what was priced. This module fixes that: the **same record chain** runs from tender to priced estimate to bid price to project budget, with every number traceable to its source.

### 1.2 What the module does (the terms engineers must know)

| Term | Meaning |
|---|---|
| **Tender** | A bidding opportunity: client, deadline, documents, status, decision, outcome. Number series `TND`. |
| **Tender BOQ** | The client's item list as imported (quantities are the client's; they are verified, never silently changed). |
| **Estimate** | The contractor's priced version of the tender BOQ, in **versions** (v1, v2 ...). Number series `EST`. Estimates also exist without a tender (R1 simple import for an existing project). |
| **Estimate line** | One priced item; its **direct cost** is built from **components**: material, labour, equipment, subcontract, other. |
| **Direct cost** (التكلفة المباشرة) | Sum of line direct costs. The cost of the physical work only. |
| **Site overheads** (مصاريف الموقع / التجهيزات) | Costs of running the site that are not tied to one item: site staff, temporary facilities, insurance, utilities, security. Either a percentage of direct cost or a detailed list of **preliminaries** (time-related and fixed). |
| **Head office overheads** (المصاريف العمومية / الإدارة العليا) | A share of the company's permanent office cost charged to the job. |
| **Risk contingency** (احتياطي المخاطر) | Money held for things that may go wrong, either a percentage or the expected value of a **risk register**. |
| **Profit** (الربح) | What the company wants to earn. Expressed as markup on cost or as margin on price. |
| **Bid price** (سعر العطاء) | Direct + site overheads + head office + risk contingency + profit, excluding VAT. |
| **Margin** | `profit / bid price` (margin on price). **Markup** is `profit / cost`. The backend always reports margin on price. |
| **Bid scenario** | A what-if copy of the assumptions (materials +8 %, price fixed, risk 4 %) with recomputed cost, price and margin. |
| **Bid / no-bid decision** | A structured, approved record: the executive decides to bid, bid with conditions, or not to bid, with the margin and risk flags visible at decision time. |
| **Outcome** | Won, lost or withdrawn, with reasons and, when known, the winner's price. |
| **Award handoff** | When a tender is won, the winning estimate becomes a project draft, a **cost budget draft** by trade and a **revenue BOQ draft** without re-typing. |
| **Rate library** | The company's own rates (per unit of work or per material), each with source, date, region, specification, quantity band and payment terms. It is fed from the sourcing **price book** and from **past projects** (actual rates from closeout). |

### 1.3 Worked example that runs through this file (the hospital)

A private hospital tender. After pricing all BOQ items the estimator has a **direct cost of 50,100,000 EGP** (material 22,000,000; labour 9,000,000; equipment 4,100,000; subcontract 15,000,000). The company policy applies:

| Step | Basis | Rate | Amount (EGP) |
|---|---|---|---|
| Direct cost | sum of lines |  | 50,100,000.0000 |
| Site overheads | direct | 8.00 % | 4,008,000.0000 |
| Head office | direct + site overheads (54,108,000) | 4.50 % | 2,434,860.0000 |
| Risk contingency | cumulative so far (56,542,860) | 2.50 % | 1,413,571.5000 |
| **Cost before profit** |  |  | **57,956,431.5000** |
| Profit | markup on cost | 7.00 % | 4,056,950.2050 |
| **Bid price (ex VAT)** |  |  | **62,013,381.7050** (about 62.0 M) |
| Margin on price | profit / price |  | **6.5421 %** (about 6.5 %) |

With the price rounded up to the nearest 1,000 EGP the price is 62,014,000 and the rounding difference 618.2950 goes to profit (profit 4,057,568.5; margin 6.5430 %). If materials rise 8 % (+1,760,000 on direct cost) and the price is held, cost becomes 59,992,425.9 and margin falls to **3.2589 %**, below the 5 % margin floor, so the scenario is flagged. If the tender is won, the project budget draft is: direct cost by trade (50,100,000) + general conditions from site overheads (4,008,000) + risk reserve (1,413,571.5) = **55,521,571.5**; head office (2,434,860) and profit (4,057,568.5) are not budget; the revenue BOQ totals 62,014,000.

The percentages are illustrative policy values chosen to reproduce the v3 totals (direct 50.1 M, bid about 62.0 M, margin about 6.5 %); real values come from each tenant's `markup_policies` (see Open question 1).

### 1.4 Where it sits in the master lifecycle and flows

| Lifecycle stage / flow | Role of this module |
|---|---|
| Stage 1, Tender and estimate (F1 steps 1-2) | Owns it: tender register, estimate, bid price, bid decision \[A\]. |
| Stage 2, Award and contract (F1 step 3) | Records the outcome. On **won** publishes `estimating.tender.won.v1`; `contracts` records the contract and saves the revenue BOQ. |
| Stage 3, Project setup (F1 step 4) | Publishes `estimating.estimate.finalized.v1`; `cost` creates the draft budget by trade (PM adjusts \[A\], locks baseline), `contracts` creates the draft revenue BOQ. |
| Stage 9, Hand over and close (F7 step 5) | Consumes `closeout.lesson.published.v1`: actual rates and productivity enter the rate library; estimation-error report. |
| Pricing intelligence | Consumes `sourcing.price.updated.v1` (price book), `core.material.*`, `core.cost_code.*`. |

### 1.5 Release scope

| Capability | Release |
|---|---|
| Simple estimate import: Excel estimate/budget to draft estimate lines by trade; finalise to an existing project; publishes `estimate.finalized` (epic EST-E9) | **R1** |
| Tender register, documents, tender BOQ import, estimate builder, rate library, quotes, build-up, scenarios, checks, bid decision, outcome, award handoff, win/loss reports | **R3** |
| Quantity and clause extraction (AI suggestions with source highlight, human verification) | R3 (consumes `intelligence.suggestion.created.v1`, owned by file 14) |

---

## 2. Users and roles

| Role (00 §4.4) | What they do in this module |
|---|---|
| `estimator` | Primary user. Registers tenders, imports the BOQ, builds estimates, captures quotes, runs scenarios, submits for review, records outcome. |
| `cost_qs_engineer` | Same as estimator for pricing (edit estimates, rates); cannot see margin/profit fields. |
| `project_manager` | Member of the bid team; reviews estimates \[A\] up to their limit; after award adjusts the draft budget in `cost` (not here). Sees margin only for tenders where they are a member. |
| `contracts_manager` | Reviews flagged clauses, records client contract terms after win, can mark clause risk as accepted. |
| `owner_executive` | Bid/no-bid decision \[A\], approves price above PM limit, sees win/loss and margin. |
| `finance_controller` | Read access to estimates and margin; confirms head-office allocation rate in `markup_policies`. |
| `tenant_admin` | Manages settings, loss reasons, markup policies, escalation indices. |
| `procurement_officer` | Read-only on rate library and tender quotes when asked to help price (no margin, no profit). |
| `foreman`, `superintendent` | **No access** to this module. No permission code is granted; endpoints return 403; global search does not index estimating entities for them (BR-X-06, v3 section 5.2: costs, rates and margins hidden from site users). |
| `supplier_user` | No access in this module. Supplier quotes for a tender are captured by the estimator (uploaded file plus typed lines); supplier portal quoting for tenders is out of scope. |
| `client_user` | No access. |

Segregation of duties (00 BR-X-07): the user who submitted the estimate for review cannot approve it, and the user who recorded a bid decision cannot approve that decision, unless the tenant enables self-approval (audited flag on the approval).

---

## 3. Services and components

Aggregates are rich (rules, money, versions); CRUD parts are plain.

| Component | Kind | Responsibility |
|---|---|---|
| `Tender` aggregate | Aggregate (state machine) | Tender status, deadline, team, decision links, outcome. |
| `TenderBoqImporter` | Handler on `IImportService` | Three-step import (upload, preview, commit) of client BOQ Excel/CSV into `tender_boq_items`; hierarchy detection, unit normalisation, duplicate item numbers, 5,000 rows in \< 10 s preview (00 §3.17). |
| `TenderDocumentService` | CRUD + file linking | Documents per tender (BOQ, drawings, specifications, conditions, addenda) through `IFileService`; addendum handling raises "BOQ outdated" flag. |
| `ExtractionSuggestionHandler` | Consumer handler | Receives `intelligence.suggestion.created.v1` for tender documents; stores suggested BOQ quantities and clauses as `suggested`; human verifies (BR-X-08). |
| `Estimate` aggregate | Aggregate (versions, state machine) | Lines, components, preliminaries, risks, build-up items; owns calculation inputs; freezes on submission; revision creates version n+1. |
| `EstimateCalculator` | Pure domain service | Computes line direct cost, trade totals, build-up, bid price, margin, line price allocation. Deterministic, versioned (`calc_version`), stores inputs hash (BR-X-10). |
| `BidScenarioEngine` | Domain service | Applies scenario overrides to a copy of inputs, calls `EstimateCalculator`, stores results; supports "solve for price" and "solve for margin". |
| `RateLibraryService` | Aggregate-light + queries | Rates, supersession, adjustment of a rate to the estimate's date/region/spec/quantity/terms, stale detection, outlier statistics. |
| `RateAdjuster` | Pure domain service | `adjusted_rate = base_rate x escalation x region x spec x quantity x terms` with every factor returned for display. |
| `RateSuggestionProvider` | Query | For an estimate line: ranked rate candidates with source projects and sample size (no benchmark if sample below threshold, BR-EST-26). |
| `PriceBookRateSync` | Consumer handler | On `sourcing.price.updated.v1` upserts rates of source `price_book`. |
| `ActualRateIngestor` | Consumer handler | On `closeout.lesson.published.v1` stores `rate_observations` and creates/updates rates of source `past_project` after verification flag. |
| `TenderQuoteService` | CRUD | Supplier/subcontractor quotes received for the tender; lines can be attached to estimate components. |
| `EstimateCheckEngine` | Rule runner | Scope and risk checks (`estimate_check_findings`); run on demand and before review. |
| `ClauseRiskClassifier` | Rule-based | Flags clauses (payment term, retention, penalty, warranty, liability) using tenant thresholds; no AI decision. |
| `BidDecisionService` | Aggregate-light | Go/no-go and price-approval decisions; links to `IApprovalService`; snapshots margin and risk flags. |
| `ApprovalOutcomeHandler` | Consumer handler | Handles `platform.approval.approved/rejected` for record types `estimating.estimate_review` and `estimating.bid_decision`. |
| `TenderOutcomeService` | Aggregate-light | Won/lost/withdrawn with reasons; triggers award handoff. |
| `AwardHandoffOrchestrator` | Saga/process manager | Publishes `estimating.tender.won.v1`; tracks project creation (consumes `core.project.created.v1`); freezes the winning estimate and publishes `estimating.estimate.finalized.v1`. Table `award_handoffs`. |
| `EstimateHandoffQuery` | Contract query (`Estimating.Contracts`) | `GetHandoff(estimateId)` returns trade budget lines and revenue BOQ lines for `cost` and `contracts` (payload too large for the event, 00 §3.9 rule 4). |
| `SimpleEstimateImporter` | Handler on `IImportService` (R1) | Imports an Excel estimate/budget (trade, description, unit, quantity, unit cost or total) into a draft estimate. |
| `TenderDeadlineJob`, `RateStalenessJob`, `EstimateRebuildJob` | Jobs | Section 9. |
| `WinLossReadModelProjector` | Projection | `rm_win_loss`, `rm_bid_pipeline`, `rm_estimate_accuracy` (section 13). |
| `EstimatingExportService` | Export | Bid summary, estimate by trade, scenario comparison, win/loss (xlsx/pdf/csv, 00 §3.6). |

CRUD only: `tender_members`, `loss_reasons`, `markup_policies`, `escalation_indices`, `rate_adjustment_factors`, `estimating.settings`, `tender_documents`, `tender_quotes`.

---

## 4. Domain model

All tables live in schema `estimating`, carry `tenant_id` with RLS, and the standard columns of 00 §3.2 (`id`, `tenant_id`, `created_at/by`, `updated_at/by`, `row_version`, `is_deleted`, `source`) unless marked **(append-only)** or **(no soft delete)**. Transaction-like records (estimates, decisions, outcomes) are never deleted: a draft estimate may be deleted only while `status='draft'` and never submitted; otherwise cancelled/superseded. FKs inside the schema are real constraints; references to other modules are by value (uuid) per 00 §5.4 except `core.projects`, `core.parties`, `platform.users` which may use real FKs (marked FK\*). Percentages are stored as percent numbers (`8.0000` = 8 %) in `numeric(9,4)`. Money is `numeric(18,4)` with `currency` (ISO, default tenant base `EGP`). Quantities are `numeric(18,4)` with `unit_code`. All amounts exclude VAT unless named `*_incl_vat`.

### 4.1 ER summary

```txt
tenders 1--* tender_members
tenders 1--* tender_documents 1--* tender_clauses
tenders 1--* tender_boq_items (self-parent hierarchy)
tenders 1--* tender_quotes 1--* tender_quote_lines
tenders 1--* estimates (versions; one is_current)
estimates 1--* estimate_lines 1--* estimate_line_components
estimate_lines *--0..1 tender_boq_items
estimates 1--* estimate_preliminary_lines
estimates 1--* estimate_buildup_items
estimates 1--* estimate_trade_totals
estimates 1--* estimate_assumptions | estimate_risks | estimate_check_findings
estimates 1--* bid_scenarios
tenders 1--* bid_decisions (go_no_go, price_approval)
tenders 1--0..1 tender_outcomes 1--* tender_outcome_reasons *--1 loss_reasons
tenders 1--0..1 award_handoffs
markup_policies --> copied into estimate_buildup_items at estimate creation
rates 1--* rate_observations ; rates self-supersession ; escalation_indices, rate_adjustment_factors
estimate_line_components --> rates / tender_quote_lines (source link)
```

### 4.2 Enumerations

| Enum | Values and meaning |
|---|---|
| `tender.status` | `new` registered; `pricing` estimate in progress; `review` under approval; `approved` approved for submission; `submitted` bid sent to client; `won`; `lost`; `withdrawn` (we withdrew after submitting or before); `no_bid` (decided not to bid). |
| `tender.contract_type` | `lump_sum`, `remeasured` (re-measurement / unit price), `cost_plus`, `design_build`. |
| `tender.source_channel` | `public_portal`, `direct_invitation`, `repeat_client`, `consultant`, `other`. |
| `document.doc_type` | `boq`, `drawing`, `specification`, `conditions`, `addendum`, `clarification`, `site_visit_note`, `other`. |
| `document.extraction_status` | `none`, `queued`, `suggested`, `verified`, `failed`. |
| `clause.category` | `payment_term`, `retention`, `advance_payment`, `penalty`, `warranty`, `liability`, `duration`, `price_adjustment`, `guarantee`, `insurance`, `termination`, `other`. |
| `clause.verification_status` | `suggested` (from AI/rules), `verified` (human confirmed), `rejected`, `manual` (typed by a person). |
| `clause.risk_level` | `none`, `low`, `medium`, `high`, `critical`. |
| `estimate.kind` | `tender` (belongs to a tender), `imported` (R1 simple import, no tender). |
| `estimate.status` | `draft`, `in_review`, `approved`, `submitted`, `finalized`, `superseded`, `cancelled`. |
| `line.line_kind` | `boq_item`, `added_item` (not in client BOQ), `provisional_sum`, `daywork`, `alternate` (priced option, excluded from total unless `include_in_total`). |
| `component.component_type` | `material`, `labour`, `equipment`, `subcontract`, `other`. |
| `component.rate_source_type` | `rate_library`, `price_book`, `past_project`, `supplier_quote`, `subcontract_quote`, `manual`, `imported`. |
| `buildup.item_type` | `site_overheads`, `head_office`, `risk_contingency`, `financing_cost`, `insurance_bonds`, `other_cost`, `profit` (exactly one, always last). |
| `buildup.calc_mode` | `percent`, `lump_sum`, `detailed` (site overheads from `estimate_preliminary_lines`), `register` (risk contingency from `estimate_risks`). |
| `buildup.basis` | `direct` (direct cost only) or `cumulative` (direct cost plus all earlier build-up items). |
| `profit.mode` | `markup_on_cost`, `margin_on_price`. |
| `risk.category` | `design`, `price_escalation`, `subcontractor`, `site_conditions`, `schedule`, `commercial_terms`, `regulatory`, `fx`. |
| `check.severity` | `info`, `warning`, `blocker`. |
| `check.status` | `open`, `acknowledged`, `resolved`, `waived` (needs reason). |
| `decision.stage` | `go_no_go` (early), `price_approval` (before submission). |
| `decision.result` | `bid`, `bid_with_conditions`, `no_bid`, `pending`. |
| `outcome.result` | `won`, `lost`, `withdrawn`. |
| `loss_reason.category` | `price`, `technical`, `relationship`, `capacity`, `commercial_terms`, `timing`, `cancelled_by_client`, `other`. |
| `rate.source_type` | `price_book`, `past_project`, `supplier_quote`, `manual`, `benchmark`. |
| `rate.status` | `pending_review`, `active`, `stale`, `retired`. |
| `handoff.status` | `pending`, `project_requested`, `project_linked`, `finalized`, `failed`. |

### 4.3 Tables

#### 4.3.1 `estimating.settings` (one row per tenant)

| column | type | null | notes |
|---|---|---|---|
| `tenant_id` | uuid | no | PK (single row) |
| `stale_rate_months` | int | no | default 6; rate older than this triggers check E02 |
| `deadline_alert_days` | int | no | default 3 |
| `margin_floor_pct` | numeric(9,4) | no | default 5.0000; check E03 blocker below it |
| `outlier_pct` | numeric(9,4) | no | default 30.0000; check E10 |
| `outlier_min_sample` | int | no | default 5 |
| `benchmark_min_sample` | int | no | default 5; below this no benchmark is shown (BR-EST-26) |
| `default_markup_policy_id` | uuid | yes | FK `markup_policies` |
| `price_rounding_step` | numeric(18,4) | no | default 1000; 0 = no rounding |
| `price_rounding_mode` | text | no | `up`, `nearest`, `down`; default `up` |
| `self_approval_allowed` | bool | no | default false |
| `region_codes` | text\[\] | no | tenant regions, e.g. `CAIRO`, `ALEX`, `UPPER_EGYPT`, `RED_SEA`, `KSA_RIYADH` |
| `require_go_no_go_above` | numeric(18,4) | yes | estimated value above which an early go/no-go decision is mandatory |

#### 4.3.2 `estimating.tenders`

| column | type | null | notes |
|---|---|---|---|
| `number` | text | no | `TND-0001`; unique `(tenant_id, number)` |
| `name_ar` / `name_en` | text | yes | at least one required (check) |
| `client_party_id` | uuid | no | FK\* `core.parties`, party type client |
| `consultant_party_id` | uuid | yes | FK\* `core.parties` |
| `project_type` | text | no | free code from tenant list, e.g. `hospital`, `residential_tower`, `road`; used by benchmarks |
| `location_text` | text | yes |  |
| `region_code` | text | no | must be in `settings.region_codes` |
| `contract_type` | text | no | enum above |
| `source_channel` | text | no | enum above |
| `status` | text | no | CHECK enum; default `new` |
| `received_date` | date | no |  |
| `clarification_deadline` | timestamptz | yes |  |
| `submission_deadline` | timestamptz | no |  |
| `site_visit_date` | date | yes |  |
| `expected_duration_days` | int | yes | from conditions |
| `estimated_value_amount` | numeric(18,4) | yes | rough value at registration (used for go/no-go threshold) |
| `currency` | text | no | default tenant base |
| `bid_bond_required` | bool | no | default false |
| `bid_bond_pct` | numeric(9,4) | yes |  |
| `bid_bond_amount` | numeric(18,4) | yes |  |
| `owner_estimator_id` | uuid | yes | FK\* `platform.users` |
| `current_estimate_id` | uuid | yes | the `is_current` estimate |
| `boq_outdated` | bool | no | true when an addendum arrived after the last BOQ import |
| `submitted_at` | timestamptz | yes | set by `submit` |
| `submitted_price_amount` | numeric(18,4) | yes | frozen price at submission |
| `submitted_estimate_id` | uuid | yes | estimate version submitted |
| `withdrawn_reason` / `no_bid_reason` | text | yes |  |
| `won_project_id` | uuid | yes | FK\* `core.projects`, set by handoff |

Indexes: `(tenant_id, status, submission_deadline)`; `(tenant_id, client_party_id, received_date desc)`; `(tenant_id, created_at desc, id desc)`; partial `(tenant_id, submission_deadline) where status in ('new','pricing','review','approved')`; trigram on `name_ar`,`name_en`. Checks: `submission_deadline >= received_date`; `bid_bond_pct between 0 and 100`; `status in (...)`.

#### 4.3.3 `estimating.tender_members`

| column | type | null | notes |
| --- | --- | --- | --- |
| `tender_id` | uuid | no | FK `tenders` |
| `user_id` | uuid | no | FK\* `platform.users` |
| `member_role` | text | no | `estimator`, `reviewer`, `coordinator`, `pm_designate` |
| `can_view_margin` | bool | no | default false, true for executive/estimator by role rule |

Unique `(tender_id, user_id, member_role)`. A tender's visibility (BR-X-09) = tenant users with `estimating.tender.read` AND (role in `owner_executive`, `estimator`, `tenant_admin`, `finance_controller` OR membership row).

#### 4.3.4 `estimating.tender_documents`

| column | type | null | notes |
| --- | --- | --- | --- |
| `tender_id` | uuid | no | FK |
| `doc_type` | text | no | enum |
| `title` | text | no |  |
| `file_id` | uuid | no | by value `platform.files` |
| `version_label` | text | yes | e.g. `Rev B` |
| `received_date` | date | no |  |
| `is_addendum` | bool | no | when true on a `boq`/`addendum` document, sets `tenders.boq_outdated = true` |
| `extraction_status` | text | no | default `none` |
| `intelligence_job_ref` | text | yes | id returned to the intelligence service |
| `notes` | text | yes |  |

Index `(tenant_id, tender_id, doc_type)`.

#### 4.3.5 `estimating.tender_clauses`

| column | type | null | notes |
|---|---|---|---|
| `tender_id` | uuid | no | FK |
| `document_id` | uuid | yes | FK `tender_documents` |
| `clause_ref` | text | yes | e.g. `Sub-clause 14.7` |
| `category` | text | no | enum |
| `clause_text` | text | no | verbatim extract |
| `extracted_value` | jsonb | yes | structured, e.g. `{"retentionPct":10,"releaseMonths":12}` or `{"delayPenaltyPctPerWeek":0.5,"capPct":10}` |
| `source_page` | int | yes |  |
| `source_region` | jsonb | yes | highlight box `{page,x,y,w,h}` for UI |
| `verification_status` | text | no | enum |
| `risk_level` | text | no | enum; set by `ClauseRiskClassifier`, overridable with reason |
| `risk_reason_code` | text | yes | e.g. `retention_above_10`, `unlimited_liability`, `payment_over_90_days` |
| `accepted_by` / `accepted_at` / `accepted_reason` | uuid/timestamptz/text | yes | contracts manager accepting a high/critical clause |
| `suggestion_id` | uuid | yes | intelligence suggestion reference (BR-X-08) |

Index `(tenant_id, tender_id, risk_level)`.

#### 4.3.6 `estimating.tender_boq_items`

| column | type | null | notes |
|---|---|---|---|
| `tender_id` | uuid | no | FK |
| `import_batch_id` | uuid | yes | `platform.import_batches` |
| `parent_id` | uuid | yes | FK self (section/heading) |
| `item_no` | text | no | client's item number, e.g. `03.02.015`; unique per `(tender_id, item_no)` among non-deleted rows |
| `level` | int | no | depth, 0 = top section |
| `is_section` | bool | no | headings carry no quantity |
| `description_ar` / `description_en` | text | yes | at least one for non-section |
| `unit_code` | text | yes | `core.units` code; required when not section |
| `quantity` | numeric(18,4) | yes | required and >= 0 when not section |
| `trade_code` | text | yes | core trade code (COR-E7); suggested by keyword mapping, confirmed by estimator |
| `source_document_id` | uuid | yes |  |
| `source_ref` | text | yes | sheet and cell, e.g. `BOQ!C214`, or `p.37` |
| `extraction_confidence` | numeric(5,4) | yes | 0..1 for AI-extracted rows |
| `verification_status` | text | no | `imported` (from Excel, exact), `suggested`, `verified`, `rejected` |
| `quantity_revised_from` | numeric(18,4) | yes | previous client quantity after an addendum |
| `addendum_document_id` | uuid | yes |  |
| `sort_order` | int | no |  |

Indexes: `(tenant_id, tender_id, sort_order)`; `(tenant_id, tender_id, trade_code)`. Check: `is_section or (unit_code is not null and quantity is not null)`.

#### 4.3.7 `estimating.tender_quotes` and `estimating.tender_quote_lines`

`tender_quotes`: `tender_id` FK; `supplier_party_id` uuid FK\* `core.parties`; `quote_ref` text; `quote_date` date; `valid_until` date null; `currency`; `fx_rate numeric(18,8)` null; `payment_terms_days int` null; `delivery_terms` text null; `quote_kind` text (`material`,`subcontract`,`equipment`); `file_id` uuid null; `total_amount numeric(18,4)` null; `status` text (`received`,`under_review`,`used`,`rejected`,`expired`). Unique `(tenant_id, tender_id, supplier_party_id, quote_ref)`.

`tender_quote_lines`: `quote_id` FK; `line_no int`; `description` text; `material_id` uuid null (by value, `core.materials`); `unit_code`; `quantity numeric(18,4)` null; `unit_price_amount numeric(18,4)`; `price_includes_delivery bool`; `lead_time_days int` null; `notes`. Check `unit_price_amount >= 0`.

#### 4.3.8 `estimating.estimates`

| column | type | null | notes |
|---|---|---|---|
| `number` | text | no | `EST-0001`; versions share the number with `-v2` suffix in the display, unique `(tenant_id, number, version)` |
| `kind` | text | no | `tender` or `imported` |
| `tender_id` | uuid | yes | FK; required when kind=`tender` |
| `project_id` | uuid | yes | FK\* `core.projects`; required to finalise `imported` estimates; set on tender estimates by handoff |
| `name` | text | no |  |
| `version` | int | no | starts at 1; unique `(tenant_id, tender_id, version)` |
| `supersedes_id` | uuid | yes | previous version |
| `is_current` | bool | no | exactly one current per tender (partial unique index) |
| `change_reason` | text | yes | required when version > 1 |
| `status` | text | no | enum |
| `currency` | text | no |  |
| `price_date` | date | no | date the rates are priced at (escalation reference) |
| `region_code` | text | no |  |
| `markup_policy_id` | uuid | yes | policy the build-up was initialised from |
| `profit_mode` | text | no | `markup_on_cost` or `margin_on_price` |
| `price_rounding_step` | numeric(18,4) | no | copied from settings |
| `price_rounding_mode` | text | no |  |
| `target_price_amount` | numeric(18,4) | yes | optional manager override: the calculator solves profit to hit it |
| `direct_cost_amount` | numeric(18,4) | no | calculated (stored result) |
| `site_overheads_amount` | numeric(18,4) | no |  |
| `head_office_amount` | numeric(18,4) | no |  |
| `risk_contingency_amount` | numeric(18,4) | no |  |
| `other_costs_amount` | numeric(18,4) | no | financing, insurance, other lumps |
| `cost_before_profit_amount` | numeric(18,4) | no |  |
| `profit_amount` | numeric(18,4) | no | after rounding adjustment |
| `rounding_adjustment_amount` | numeric(18,4) | no | price rounding difference included in profit |
| `bid_price_amount` | numeric(18,4) | no | ex VAT |
| `vat_amount` | numeric(18,4) | no | informational: price x tax rate |
| `tax_code_id` | uuid | yes | `core.tax_codes` |
| `margin_pct` | numeric(9,4) | no | profit / bid price x 100 |
| `markup_pct` | numeric(9,4) | no | profit / cost before profit x 100 |
| `calc_version` | text | no | calculator formula version, e.g. `est-calc-1` |
| `calc_inputs_hash` | text | no | sha256 of the normalised inputs |
| `calculated_at` | timestamptz | no |  |
| `is_stale_calc` | bool | no | set true by any input change; cleared by recalculation |
| `submitted_for_review_by` / `_at` | uuid/timestamptz | yes | for segregation of duties |
| `approval_id` | uuid | yes | `platform` approval instance |
| `approved_at` | timestamptz | yes |  |
| `finalized_at` / `finalized_by` | timestamptz/uuid | yes | handoff freeze |
| `handoff_summary` | jsonb | yes | counts and totals published in the event |

Checks: `kind='tender' => tender_id is not null`; `bid_price_amount >= 0`; `version >= 1`; `version = 1 or change_reason is not null`. Indexes: `(tenant_id, tender_id, version desc)`; unique partial `(tenant_id, tender_id) where is_current`; `(tenant_id, status)`; `(tenant_id, project_id)`.

#### 4.3.9 `estimating.estimate_lines`

| column | type | null | notes |
|---|---|---|---|
| `estimate_id` | uuid | no | FK |
| `line_no` | int | no |  |
| `line_kind` | text | no | enum |
| `tender_boq_item_id` | uuid | yes | FK; required for `boq_item`; unique per `(estimate_id, tender_boq_item_id)` |
| `item_no` | text | yes | copied for display/export |
| `description_ar` / `description_en` | text | yes |  |
| `unit_code` | text | no |  |
| `quantity` | numeric(18,4) | no | copied from BOQ item; override needs `quantity_override_reason` |
| `quantity_override_reason` | text | yes |  |
| `trade_code` | text | no | core trade code |
| `work_package_code` | text | yes | optional work-package template code from COR-E7 |
| `cost_code_id` | uuid | yes | by value `core.cost_codes`; required before finalise |
| `unit_direct_cost_amount` | numeric(18,4) | no | sum of component amounts per unit |
| `direct_cost_amount` | numeric(18,4) | no | `quantity x unit_direct_cost_amount`, rounded 4 dp |
| `bid_unit_rate_amount` | numeric(18,4) | yes | calculated after build-up allocation, 2-decimal precision |
| `bid_amount` | numeric(18,4) | yes | `quantity x bid_unit_rate_amount` rounded 2 dp |
| `include_in_total` | bool | no | default true; false for unselected `alternate` |
| `has_source` | bool | no | true when every component has a source (stored for fast checks) |
| `productivity_note` | text | yes |  |
| `remarks` | text | yes |  |

Indexes: `(tenant_id, estimate_id, line_no)`; `(tenant_id, estimate_id, trade_code)`. Checks: `quantity >= 0`; `direct_cost_amount >= 0`.

#### 4.3.10 `estimating.estimate_line_components`

| column | type | null | notes |
|---|---|---|---|
| `estimate_line_id` | uuid | no | FK |
| `component_type` | text | no | enum |
| `description` | text | no | e.g. `Ready-mix concrete C35` |
| `material_id` | uuid | yes | by value `core.materials` |
| `unit_code` | text | no | unit of the resource |
| `consumption_qty` | numeric(18,4) | no | resource quantity per one unit of the line (e.g. 1.0500 m3 concrete per m3 slab including waste) |
| `waste_pct` | numeric(9,4) | no | default 0; effective consumption = `consumption_qty x (1 + waste_pct/100)` |
| `unit_rate_amount` | numeric(18,4) | no | rate of the resource in `currency` after adjustment |
| `currency` | text | no |  |
| `fx_rate` | numeric(18,8) | yes | to estimate currency |
| `amount_per_line_unit` | numeric(18,4) | no | `effective consumption x unit_rate x fx_rate` |
| `rate_source_type` | text | no | enum |
| `rate_id` | uuid | yes | FK `rates` |
| `quote_line_id` | uuid | yes | FK `tender_quote_lines` |
| `rate_base_amount` | numeric(18,4) | yes | rate before adjustment |
| `rate_adjustment` | jsonb | yes | factors applied `{"escalation":1.0420,"region":1.0000,"spec":1.0000,"quantity":0.9800,"terms":1.0100,"index_code":"MAT-STEEL"}` |
| `rate_date` | date | yes | date of the underlying rate/quote |
| `source_note` | text | yes | free text when `manual` (mandatory then, BR-EST-08) |

Checks: `consumption_qty >= 0`; `unit_rate_amount >= 0`; `rate_source_type='manual' => source_note is not null`; `rate_source_type='rate_library' => rate_id is not null`; `rate_source_type in ('supplier_quote','subcontract_quote') => quote_line_id is not null`. Index `(tenant_id, estimate_line_id)`; `(tenant_id, rate_id)`.

#### 4.3.11 `estimating.estimate_preliminary_lines` (detailed site overheads)

| column | type | null | notes |
|---|---|---|---|
| `estimate_id` | uuid | no | FK |
| `group_code` | text | no | `staff`, `facilities`, `utilities`, `insurance`, `security`, `testing`, `transport`, `other` |
| `description` | text | no |  |
| `is_time_related` | bool | no | time-related lines use duration |
| `unit_code` | text | no | `month`, `item`, `ls` |
| `quantity` | numeric(18,4) | no | for time-related: number of units per month |
| `duration_months` | numeric(9,4) | yes | required when time-related |
| `unit_rate_amount` | numeric(18,4) | no |  |
| `amount` | numeric(18,4) | no | `quantity x (duration_months or 1) x unit_rate` |
| `cost_code_id` | uuid | yes | target cost code for the general conditions budget |

#### 4.3.12 `estimating.estimate_buildup_items`

| column | type | null | notes |
|---|---|---|---|
| `estimate_id` | uuid | no | FK |
| `item_type` | text | no | enum; unique `(estimate_id, item_type)` except `other_cost` (several) |
| `label` | text | no | display text (Arabic/English via code, free text for `other_cost`) |
| `sort_order` | int | no | build-up order; profit last |
| `calc_mode` | text | no | enum |
| `basis` | text | no | `direct` or `cumulative` |
| `pct` | numeric(9,4) | yes | for `percent` mode |
| `lump_amount` | numeric(18,4) | yes | for `lump_sum` mode |
| `calculated_amount` | numeric(18,4) | no | result |
| `basis_amount` | numeric(18,4) | no | result: amount the percentage was applied to |
| `to_budget` | text | no | where the handoff sends it: `trade_budget` (direct, n/a), `general_conditions`, `risk_reserve`, `none` (head office and profit) |

Check: exactly one `profit` row and it has the highest `sort_order` (validated by aggregate, plus trigger-free CHECK on `item_type='profit' => calc_mode='percent'`).

#### 4.3.13 `estimating.estimate_trade_totals` (calculated)

`estimate_id`, `trade_code`, `material_amount`, `labour_amount`, `equipment_amount`, `subcontract_amount`, `other_amount`, `direct_cost_amount` (sum), `bid_amount` (allocated price), `line_count`. Unique `(estimate_id, trade_code)`. Rebuilt on every calculation. This is the trade view and the source of the budget handoff.

#### 4.3.14 `estimating.estimate_assumptions`

`estimate_id`; `kind` (`assumption`, `exclusion`, `clarification`, `qualification`); `text`; `sort_order`; `linked_line_id` uuid null; `include_in_bid_letter bool`.

#### 4.3.15 `estimating.estimate_risks`

`estimate_id`; `category`; `description`; `probability_pct numeric(9,4)` (0-100); `impact_amount numeric(18,4)`; `expected_value_amount numeric(18,4)` = probability x impact / 100 (stored); `included_in_contingency bool` (default true); `mitigation text`; `owner_user_id uuid null`; `clause_id uuid null`. Check probability between 0 and 100.

#### 4.3.16 `estimating.estimate_check_findings`

`estimate_id`; `run_id uuid`; `rule_code` (E01-E12, section 6); `severity`; `estimate_line_id` null; `clause_id` null; `message_params jsonb`; `status`; `waived_reason text null`; `waived_by uuid null`; `detected_at`; `resolved_at null`. Index `(tenant_id, estimate_id, status, severity)`.

#### 4.3.17 `estimating.bid_scenarios`

| column | type | null | notes |
|---|---|---|---|
| `estimate_id` | uuid | no | FK |
| `name` | text | no | e.g. `Materials +8 %, price held` |
| `is_base` | bool | no | the base scenario mirrors the estimate; one per estimate |
| `overrides` | jsonb | no | e.g. `{"materialPct":8,"labourPct":0,"equipmentPct":0,"subcontractPct":0,"riskPct":null,"profitPct":null,"siteOverheadPct":null,"priceMode":"hold_price","priceAmount":62013381.7050,"durationMonths":null}` |
| `price_mode` | text | no | `hold_price` (price fixed, margin result), `hold_margin` (solve price), `recompute` (build-up as configured) |
| `direct_cost_amount` | numeric(18,4) | no | result |
| `cost_before_profit_amount` | numeric(18,4) | no |  |
| `bid_price_amount` | numeric(18,4) | no |  |
| `profit_amount` | numeric(18,4) | no |  |
| `margin_pct` | numeric(9,4) | no |  |
| `below_floor` | bool | no | margin below `margin_floor_pct` |
| `calc_version` / `calc_inputs_hash` | text | no |  |
| `selected` | bool | no | at most one selected; applying it copies overrides into the estimate (action `apply-scenario`) |

#### 4.3.18 `estimating.bid_decisions`

| column | type | null | notes |
|---|---|---|---|
| `tender_id` | uuid | no | FK |
| `stage` | text | no | enum |
| `estimate_id` | uuid | yes | required for `price_approval` |
| `scenario_id` | uuid | yes | scenario chosen |
| `result` | text | no | enum, `pending` until the approval completes |
| `rationale` | text | yes | required for `no_bid` and `bid_with_conditions` |
| `conditions` | text | yes |  |
| `price_amount` / `margin_pct` / `cost_before_profit_amount` | numeric | yes | snapshot at request time |
| `risk_snapshot` | jsonb | no | open check findings and high-risk clauses at request time |
| `requested_by` | uuid | no |  |
| `approval_id` | uuid | yes |  |
| `decided_by` / `decided_at` | uuid/timestamptz | yes |  |

Index `(tenant_id, tender_id, stage, created_at desc)`.

#### 4.3.19 `estimating.tender_outcomes`, `loss_reasons`, `tender_outcome_reasons`

`tender_outcomes`: `tender_id` unique FK; `result`; `outcome_date`; `our_price_amount` (copied from submission); `winning_bidder_name text`; `winning_party_id uuid null`; `winning_price_amount numeric(18,4) null`; `our_rank int null`; `bidders_count int null`; `contract_value_amount numeric(18,4) null` (won); `award_date date null`; `contract_start_date date null`; `client_feedback text null`; `lessons_note text null`; `recorded_by uuid`. <br> `loss_reasons`: `code` (unique per tenant), `name_ar`, `name_en`, `category`, `is_active`, seeded with `price_too_high`, `price_too_low_suspected`, `technical_score`, `relationship`, `capacity_conflict`, `terms_unacceptable`, `late_submission`, `client_cancelled`, `incomplete_bid`. <br> `tender_outcome_reasons`: `outcome_id`, `reason_code`, `is_primary bool`, `comment`. At least one reason is mandatory for `lost`/`withdrawn`/`no_bid`; exactly one primary.

#### 4.3.20 Rate library: `rates`, `rate_observations`, `escalation_indices`, `rate_adjustment_factors`

`rates`:

| column | type | null | notes |
|---|---|---|---|
| `code` | text | no | unique `(tenant_id, code)` for non-superseded; e.g. `RC-C35-SLAB-CAIRO` |
| `description_ar` / `description_en` | text | yes |  |
| `rate_class` | text | no | `resource` (per material/labour/equipment unit) or `composite` (per unit of work, with components jsonb) |
| `component_type` | text | no | enum |
| `trade_code` | text | yes |  |
| `material_id` | uuid | yes | by value |
| `unit_code` | text | no |  |
| `base_rate_amount` | numeric(18,4) | no |  |
| `currency` | text | no |  |
| `rate_date` | date | no | date the price applies to |
| `region_code` | text | no |  |
| `spec_tag` | text | yes | e.g. `C35`, `grade-60`, `class-A` |
| `qty_band_min` / `qty_band_max` | numeric(18,4) | yes | quantity band for which the rate is valid |
| `payment_terms_days` | int | yes |  |
| `includes_delivery` | bool | no |  |
| `source_type` | text | no | enum |
| `source_ref_type` / `source_ref_id` | text/uuid | yes | `project`, `quote`, `price_book_entry`, `lesson` |
| `source_project_id` | uuid | yes | FK\* `core.projects` for `past_project` |
| `sample_size` | int | no | observations behind the rate (1 for single quote) |
| `rate_min_amount` / `rate_max_amount` | numeric(18,4) | yes | range across observations |
| `status` | text | no | enum |
| `is_verified` | bool | no | past-project rates need human verification before `active` |
| `supersedes_id` | uuid | yes |  |
| `is_current` | bool | no |  |
| `components` | jsonb | yes | for composite rates: list of `{componentType,description,unitCode,consumptionQty,wastePct,rateId}` |
| `valid_to` | date | yes |  |

Indexes: `(tenant_id, trade_code, status)`; `(tenant_id, material_id, region_code) where is_current`; trigram on description; `(tenant_id, source_project_id)`. Check `base_rate_amount >= 0`; `qty_band_max is null or qty_band_max >= qty_band_min`.

`rate_observations` **(append-only)**: `rate_id` null, `project_id` uuid, `trade_code`, `cost_code_id` null, `item_description`, `unit_code`, `quantity`, `component_type`, `actual_rate_amount`, `currency`, `observed_date`, `region_code`, `productivity_value numeric(18,4)` null (e.g. m2 per man-day), `productivity_unit text` null, `estimated_rate_amount numeric(18,4)` null (what the estimate said), `lesson_id uuid`, `is_verified bool`, `event_id uuid` (unique per tenant for idempotency).

`escalation_indices`: `index_code` (e.g. `MAT-STEEL`, `MAT-CEMENT`, `LAB-GENERAL`, `EQP-RENT`), `index_month date` (first of month), `index_value numeric(18,6)`, `source_note`; unique `(tenant_id, index_code, index_month)`.

`rate_adjustment_factors`: `kind` (`region`,`spec`,`quantity`,`terms`), `key` (e.g. region code, `spec_tag`, band code, `payment_days:60`), `component_type` null, `factor numeric(9,6)`, `effective_from date`, `effective_to date null`. Unique `(tenant_id, kind, key, component_type, effective_from)`.

#### 4.3.21 `estimating.markup_policies`

`code` unique; `name_ar/name_en`; `is_default`; `site_overheads_mode` (`percent`/`detailed`), `site_overheads_pct`; `head_office_pct`; `head_office_basis`; `risk_mode` (`percent`/`register`); `risk_pct`; `profit_mode`; `profit_pct`; `margin_floor_pct` null (overrides settings); `applies_to_project_type` text null; `items jsonb` (extra lumps). Creating an estimate copies the policy into `estimate_buildup_items`; later policy edits never change existing estimates (00 §3.3 principle).

#### 4.3.22 `estimating.award_handoffs`

`tender_id` unique; `estimate_id` (winning version); `status` (enum); `contract_value_amount`; `project_id` null; `requested_event_id`; `finalized_event_id` null; `project_linked_at`; `finalized_at`; `failure_reason text null`; `attempts int`. A row exists from the moment `award` is executed.

### 4.4 Design notes

1. Estimate versions are rows; `create-revision` deep-copies lines, components, preliminaries, buildup, risks, assumptions (5,000 lines in one transaction under 2 s).
2. Calculated columns (totals, `bid_unit_rate_amount`) are stored results, written only by `EstimateCalculator`. `is_stale_calc=true` makes list endpoints show a warning and blocks review/submit/finalize until recalculated.
3. Search index entries: tender (number, names, client), estimate (number, name), rate (code, descriptions). Estimating entities are excluded from search for roles without `estimating.tender.read`.

---

## 5. State machines

### 5.1 Tender

| from | action | to | guard | who | side effects |
| --- | --- | --- | --- | --- | --- |
| (none) | `register` | `new` | client, region, deadline, type valid | `estimating.tender.create` | number `TND-n`; audit; notification `EST_TENDER_ASSIGNED` to owner estimator |
| `new` | `start-pricing` | `pricing` | owner estimator set; if `estimated_value >= settings.require_go_no_go_above`, an approved `go_no_go` decision with result `bid` or `bid_with_conditions` exists | `estimating.tender.update` | creates estimate v1 from default markup policy if none |
| `new` | `no-bid` | `no_bid` | reason (loss reason code) | `estimating.decision.record` (approval required when estimated value above threshold) | outcome row with result `withdrawn` is NOT created; `no_bid_reason` stored; rm updated |
| `pricing` | `submit-for-review` | `review` | current estimate `draft`, calc not stale, no open `blocker` findings, all BOQ items priced or explicitly `not_priced` documented, no unverified BOQ items | `estimating.estimate.update`; not the approver | estimate to `in_review`; bid decision `price_approval` created `pending`; approval requested via `IApprovalService` (record type `estimating.estimate_review`, amount = bid price); event `platform.approval.requested.v1` is platform's |
| `review` | approval approved | `approved` | all steps approved; margin >= floor or waiver approved by executive step | system (on `platform.approval.approved.v1`) | estimate `approved`; decision result set (`bid` / `bid_with_conditions`); notification `EST_ESTIMATE_APPROVED` |
| `review` | approval rejected | `pricing` | rejection reason | system | estimate back to `draft`; notification `EST_ESTIMATE_REJECTED` |
| `review` | `recall` | `pricing` | requester only; approval cancelled | `estimating.estimate.update` | approval cancelled |
| `approved` | `submit` | `submitted` | `submission_deadline` not passed (override needs `estimating.tender.submit_late` + reason); submission file attached (`bid_letter` document optional) | `estimating.tender.submit` | estimate `submitted` frozen (read-only); `submitted_at`, `submitted_price_amount`, `submitted_estimate_id`; audit |
| `submitted` | `revise` | `pricing` | client asked for revision/BAFO; reason | `estimating.estimate.update` | new estimate version `draft` (`supersedes_id`), prior stays `submitted`->`superseded` on new submit; decisions must be repeated |
| `new`,`pricing`,`review`,`approved`,`submitted` | `withdraw` | `withdrawn` | reason | `estimating.tender.update` | estimate `cancelled` if not submitted else stays `submitted`; outcome row `withdrawn` with reasons |
| `submitted` | `record-lost` | `lost` | at least one reason, primary reason | `estimating.outcome.record` | outcome row; rm win/loss; notification `EST_TENDER_LOST` to executive; estimate remains `submitted` (history) |
| `submitted` | `record-won` | `won` | contract value >= 0; award date | `estimating.outcome.record` (+ approval when tenant policy `award_confirmation` is on) | outcome row; `award_handoffs` row `pending`; publishes `estimating.tender.won.v1`; saga continues (5.3) |

Terminal: `won` (continues through handoff), `lost`, `withdrawn`, `no_bid`. Any other action: `409 estimating.invalid_state_transition`.

### 5.2 Estimate (version)

| from | action | to | guard | who | side effects |
| --- | --- | --- | --- | --- | --- |
| (none) | `create` / `import` | `draft` | tender not terminal; for `imported` kind, project exists | `estimating.estimate.create` | number; buildup rows from policy; calc run |
| `draft` | `recalculate` | `draft` |  | `estimating.estimate.update` | results stored; `is_stale_calc=false` |
| `draft` | `submit-for-review` | `in_review` | as tender guard above (tender estimates) | `estimating.estimate.update` | approval requested |
| `in_review` | approval approved | `approved` |  | system |  |
| `in_review` | approval rejected / recall | `draft` |  | system / requester |  |
| `approved` | `reopen` | `draft` | margin or inputs must change before submission; reason; approval cancelled; decision invalidated | `estimating.estimate.update` | audit |
| `approved` | tender `submit` | `submitted` |  | system | frozen |
| `submitted` | `create-revision` | new row `draft` v+1 | reason required | `estimating.estimate.update` | old row stays `submitted` until new one is submitted, then `superseded`; `is_current` moves to new row |
| `submitted` | handoff finalize | `finalized` | tender `won`, project linked | system (saga) | publishes `estimating.estimate.finalized.v1`; `finalized_at` |
| `approved` (kind `imported`) | `finalize` | `finalized` | project exists and is not closed; cost codes assigned; no blocker findings | `estimating.estimate.finalize` | publishes `estimating.estimate.finalized.v1` (R1 path) |
| `draft`,`approved` | `cancel` | `cancelled` | not submitted | `estimating.estimate.update` |  |

For R1 `imported` estimates the flow is `draft -> approved -> finalized` where `approved` is reached by the action `approve-import` (a single approval step by `project_manager` or `cost_qs_engineer` with `estimating.estimate.approve`) so the same event contract is used.

### 5.3 Award handoff saga

| from | step | to | detail |
|---|---|---|---|
| (none) | `record-won` | `pending` | row created in the same transaction as `estimating.tender.won.v1` outbox row |
| `pending` | consumer in `core` creates project draft | `project_requested` | marker set when the outbox row is dispatched (status via `core.project.created.v1`) |
| `project_requested` | consume `core.project.created.v1` with `origin.type='tender'`, `origin.id = tenderId` | `project_linked` | sets `tenders.won_project_id`, `estimates.project_id`; handler runs in the same transaction as the next step |
| `project_linked` | finalize estimate | `finalized` | publishes `estimating.estimate.finalized.v1` with `projectId`; consumers `cost` and `contracts` build drafts |
| any | no project event within 24 h (job `AwardHandoffWatchdog`) | `failed` | notification `EST_HANDOFF_FAILED` to estimator and tenant\_admin; retry action `POST /tenders/{id}/handoff/retry` republishes `tender.won` with `retry=true` (consumers idempotent on `tenderId`) |

---

## 6. Business rules

Numbering `BR-EST-nn`. "Calc" rules are implemented only in `EstimateCalculator` and unit-tested with the golden numbers (section 15).

### 6.1 Tender and documents

- **BR-EST-01** Tender number is generated by `platform.number_series` series `TND` (default `TND-0001`); never reused.
- **BR-EST-02** `submission_deadline >= received_date`; `region_code` must belong to `settings.region_codes`; a tender needs `client_party_id` of party type client.
- **BR-EST-03** An addendum document (`is_addendum=true`, type `boq` or `addendum`) sets `tenders.boq_outdated=true`; while true, `submit-for-review` and `submit` are rejected with `estimating.boq_outdated` until the estimator re-imports the BOQ or confirms "no BOQ change" (action `clear-boq-outdated`, reason). A re-import creates new `tender_boq_items` revisions: changed quantities set `quantity_revised_from`; estimate lines whose BOQ quantity changed are flagged (finding E06) and their quantity updates only on the estimator's confirmation (the contractor never silently changes the client's quantity).
- **BR-EST-04** Document file limits per 00 §3.11 (25 MB; 100 MB for `drawing`); allowed types whitelist; a BOQ document must be `.xlsx`, `.xls`, `.csv` or `.pdf`.
- **BR-EST-05** No more than one estimate per tender is `is_current`; creating a revision moves the flag in the same transaction.

### 6.2 Tender BOQ import

- **BR-EST-06** Import column mapping (preview step): item number, description, unit, quantity, optional trade, optional remarks. Row errors in Arabic per row/column: missing unit, negative quantity, unknown unit (suggest nearest `core.units` match, accepting Arabic names `م2`, `متر مربع`, `م3`, `طن`, `عدد`, `مقطوعية`), duplicate item number, quantity not numeric (Arabic-Indic digits are normalised to Western before parsing).
- **BR-EST-07** Section rows are detected from the item-number hierarchy (`03`, `03.02`, `03.02.015`) or from rows with a description and no unit/quantity; their quantity must be empty. Commit is all-or-nothing by default, option "skip error rows" allowed with a returned list.
- **BR-EST-07a** A parent section's total never carries a quantity; quantity consistency (sum of children where the BOQ shows subtotals) is checked as finding E06.

### 6.3 Estimate lines and components

- **BR-EST-08** Every component has a source: `rate_id`, `quote_line_id`, or `source_note` (manual). A line is `has_source=true` only if all its components have one (check E01).
- **BR-EST-09** `amount_per_line_unit = consumption_qty x (1 + waste_pct/100) x unit_rate_amount x coalesce(fx_rate,1)`, rounded to 4 dp at component level.
- **BR-EST-10** `unit_direct_cost_amount = sum(component.amount_per_line_unit)`; `direct_cost_amount = round(quantity x unit_direct_cost_amount, 4)`.
- **BR-EST-11** `D (direct cost) = sum(direct_cost_amount)` over lines with `include_in_total=true`. Alternates are priced but excluded unless selected.
- **BR-EST-12** Trade totals: `estimate_trade_totals` groups lines by `trade_code`; the sum of trade `direct_cost_amount` equals D exactly (invariant tested on every calculation).
- **BR-EST-13** A line's `trade_code` must exist in the tenant's trade templates (COR-E7). A BOQ item without trade is mapped by keyword suggestion; the estimator must confirm before review (check E07).

### 6.4 Rate library and adjustment

- **BR-EST-14** Rate adjustment: `adjusted_rate = base_rate x escalation x region x spec x quantity x terms`. `escalation = index(index_code, price_date month) / index(index_code, rate_date month)`; if either index is missing the factor is 1 and the rate carries a warning `estimating.escalation_index_missing`. Region, spec, quantity and terms factors come from `rate_adjustment_factors` (default 1.000000). All factors are stored in `rate_adjustment` on the component.
- **BR-EST-15** Candidate selection for a line: same `material_id` or description match, `is_current`, status `active`, region equal to the estimate region first then others (factor applied), spec tag equal if given, quantity inside band if bands exist. Ranking: exact region and spec first, then newest `rate_date`, then larger `sample_size`.
- **BR-EST-16** A rate is **stale** when `rate_date < price_date - stale_rate_months`; stale rates are usable with a warning (finding E02); status moves to `stale` by `RateStalenessJob` only when no newer current rate exists for the same key.
- **BR-EST-17** A rate from another region than the estimate's is flagged "out of region" (E02) unless a region factor exists.
- **BR-EST-18** Rates from `price_book` are written/updated only by `PriceBookRateSync`. The sync creates a new `rates` row (`supersedes_id` previous, `is_current` moves) whenever the price changes by more than 0.5 % or the month changes; unchanged prices only touch `as_of`. Price book rates carry `sample_size` = number of fresh offers behind the price (from the event) and are `active` immediately.
- **BR-EST-19** Rates from `past_project` are created `pending_review`; a user with `estimating.rate.manage` verifies (sets `is_verified`, `active`). Unverified rates never appear in suggestions (BR-EST-26) and are not used by `RateSuggestionProvider`.
- **BR-EST-20** Rates are never edited in place: a change creates a new row superseding the old (00 §3.3). Estimates copy the rate value, so later library changes never alter an existing estimate.
- **BR-EST-21** Outlier check: for a component with `rate_id` whose library group (same material/rate key) has `sample_size >= outlier_min_sample`, a rate deviating more than `outlier_pct` from the group median raises E10 (warning).

### 6.5 Build-up (the money rules)

- **BR-EST-22** Build-up order is `sort_order`. Let `D` be direct cost. For each non-profit item `k` in order: `basis_k = D` if `basis='direct'`, else `D + sum(amount_j for j<k)`. `amount_k = round(basis_k x pct_k / 100, 4)` for `percent`; `lump_amount` for `lump_sum`; sum of preliminary lines for `detailed`; sum of `expected_value_amount` of risks with `included_in_contingency` for `register`.
- **BR-EST-23** Cost before profit `C = D + sum(amount_k)` over non-profit items. Profit: `markup_on_cost`: `profit = round(C x pct / 100, 4)`, `price = C + profit`. `margin_on_price`: `price = round(C / (1 - pct/100), 4)`, `profit = price - C` (pct must be \< 100).
- **BR-EST-24** Price rounding: if `price_rounding_step > 0`, `price_rounded = ceil/round/floor(price / step) x step` by mode; `rounding_adjustment = price_rounded - price` is added to profit; `bid_price = price_rounded`. If `target_price_amount` is set it replaces rounding: profit = target - C (target must be >= C, else `estimating.target_below_cost`).
- **BR-EST-25** `margin_pct = profit / bid_price x 100`; `markup_pct = profit / C x 100`; both rounded to 4 dp. VAT is informational: `vat_amount = round(bid_price x tax_rate, 4)`; the price handed over is ex VAT (VAT 14 % on 62,014,000 = 8,681,960). Golden: section 1.3 and section 15.
- **BR-EST-25a** Price allocation to lines: `raw_i = direct_cost_i x bid_price / D`; `bid_unit_rate_i = round(raw_i / quantity_i, 2)`; `bid_amount_i = round(quantity_i x bid_unit_rate_i, 2)`; `rounding_adjustment_lines = bid_price - sum(bid_amount_i)` is stored on the estimate (`handoff_summary.lineRoundingAmount`) and exported as a lump line `ROUNDING`. Lines with quantity 0 get rate 0 and amount 0 (their cost is still in D and carried by the allocation of others, flagged E11). Allocation is pro rata to direct cost; unbalanced (front-loaded) rate shifting is not supported (Open question 4). Example: a line with direct cost 1,000,000 at price/direct ratio 62,014,000/50,100,000 = 1.2377645 gets bid amount 1,237,764.47.
- **BR-EST-25b** Hand-off budget mapping: direct cost by trade becomes trade budget; `site_overheads` becomes the budget of trade `general_conditions` (cost codes from `estimate_preliminary_lines.cost_code_id` or default code); `risk_contingency` becomes budget line `risk_reserve` (project-level, unallocated to trades); `head_office`, `profit`, `financing_cost` have `to_budget='none'`; `insurance_bonds` and `other_cost` go to `general_conditions`. Hospital: budget = 50,100,000 + 4,008,000 + 1,413,571.5 = 55,521,571.5. The sum `budget + head_office + profit = bid_price` (invariant, test).
- **BR-EST-25c** A calculation is reproducible (BR-X-10): inputs hash covers lines, components, preliminaries, risks, build-up items, profit mode, rounding parameters and `calc_version`; recomputing from stored inputs yields identical results; `is_stale_calc` is set by any write that changes the inputs.

### 6.6 Margin, scenarios, risk

- **BR-EST-26** No benchmark or rate suggestion is displayed when `sample_size < benchmark_min_sample`; the API returns `benchmark: null` with `reason: "insufficient_sample"` and the sample size. Benchmarks use only the tenant's own verified data.
- **BR-EST-27** Margin floor: effective floor = policy `margin_floor_pct` else `settings.margin_floor_pct`. If `margin_pct < floor`, finding E03 (blocker) is raised; the estimate can go to review only with an explicit waiver note, and the approval policy must include the executive step (approval payload carries `belowFloor=true`).
- **BR-EST-28** Scenario with `hold_price`: price = base `bid_price`; cost recomputed with overrides (percent adjustments apply to the resource component types of every line; build-up percentages re-applied); `profit = price - C'`, `margin = profit/price`. Golden: materials +8 % (+1,760,000 on D) gives D' = 51,860,000, C' = 59,992,425.9, profit 2,020,955.805, margin 3.2589 %, below floor 5 %. Scenario `hold_margin` solves price for a target margin on price (`price = C'/(1 - m)`), then applies rounding. Scenario results are stored with inputs hash and calc version.
- **BR-EST-29** Only one scenario per estimate may be `selected`; applying it copies the overrides into line components/build-up as new values (not as a hidden multiplier) and recalculates; the base scenario is recorded in the audit trail.
- **BR-EST-30** Risk register contingency: `expected_value = probability_pct x impact_amount / 100`; if risk mode is `register` and the register total differs from the policy percentage by more than 50 % relative, finding E12 (info) shows both numbers.

### 6.7 Checks (EstimateCheckEngine rule codes)

| Code | Severity | Condition |
|---|---|---|
| E01 | blocker | A priced line has a component without a source (`has_source=false`). |
| E02 | warning | A rate older than `stale_rate_months`, or from another region without region factor. |
| E03 | blocker | Margin below floor. |
| E04 | warning | Tender deadline within `deadline_alert_days` and tender not submitted (raised by job, not the engine). |
| E05 | blocker (critical) / warning (high) | Clause with `risk_level` `critical` (e.g. unlimited liability, `liability` with no cap) not accepted by a contracts manager; `high` ones are warnings until accepted. |
| E06 | warning | Quantity inconsistency: BOQ child sum differs from subtotal; unit mismatch between estimate line and BOQ item; client quantity revised by addendum. |
| E07 | blocker | BOQ item (non-section) with no estimate line, or line with unconfirmed trade, or mandatory trade of the trade template missing for the project type. |
| E08 | warning | Duplicate item numbers or identical descriptions with different rates in the same trade. |
| E09 | warning | Line with zero direct cost (not a provisional/alternate). |
| E10 | warning | Rate deviates more than `outlier_pct` from library median. |
| E11 | info | Line quantity 0 (cost in D, no price allocation). |
| E12 | info | Register vs percentage contingency disagree. |

- **BR-EST-31** Clause classification thresholds (tenant-configurable defaults): retention above 10 % `high`; retention release later than 12 months after handover `medium`; payment term over 60 days `medium`, over 90 days `high`; delay penalty above 0.5 % per week or total cap above 10 % `high`; no cap on liability or consequential loss `critical`; warranty over 24 months `medium`; price adjustment excluded on contracts above 12 months duration `medium`; advance payment below 10 % `low`.
- **BR-EST-32** Every blocker finding must be `resolved` or `waived` (with `waived_reason`, permission `estimating.check.waive`) before `submit-for-review`. A waived E03 requires the approver chain to contain the executive step.

### 6.8 Decision, outcome, handoff

- **BR-EST-33** Go/no-go and price-approval decisions are approval-routed (`IApprovalService`, record types `estimating.bid_decision` and `estimating.estimate_review`, amount = estimated value or bid price). The decision stores a snapshot of price, margin, cost and risk flags at request time (immutable, BR-X-03). Approval of a price that has since changed is impossible: if the estimate is `reopen`ed the approval is cancelled.
- **BR-EST-34** Lost outcome requires at least one loss reason and exactly one primary reason; winning bidder name and price are optional but, when given, `winning_price_amount > 0`. `our_rank` is between 1 and `bidders_count` when both are given.
- **BR-EST-35** Recording `won` requires `contract_value_amount` (what the client contracts; may differ from our bid after negotiation) and `award_date`; if contract value differs from `submitted_price_amount` by more than 0.5 %, warning `estimating.contract_value_differs` and the handoff scales the revenue BOQ: each line `bid_amount` is multiplied by `contract_value / bid_price` (rates rounded as BR-EST-25a) while the **cost budget is unchanged** (so margin changes visibly); original and scaled values are both in the handoff query.
- **BR-EST-36** Handoff is idempotent per `tenderId` (consumers dedupe on `eventId`; republish with `retry=true` keeps the same `handoffId`).
- **BR-EST-37** Finalising an estimate requires: every line has `cost_code_id` (defaulted from the trade template's default cost code, else the estimator assigns), every trade exists in the target project's trade list (or is created from the trade template), no blocker findings, `is_stale_calc=false`. After finalising the estimate and its tender documents are read-only.
- **BR-EST-38** (R1) Simple estimate import: columns `trade_code` (or trade name), `description`, `unit`, `quantity`, `unit_cost` or `total_cost`, optional `cost_code`, `notes`; each row becomes a line with a single component `other` with `rate_source_type='imported'` (no source checks E01/E02/E10 for imported estimates); no build-up beyond an optional contingency; the estimate `bid_price_amount` equals `cost_before_profit_amount` (no revenue). Handoff creates cost budget draft only (no revenue BOQ because `contracts` BOQ import covers revenue in R1).
- **BR-EST-39** `closeout.lesson.published.v1` observations: a rate observation is stored exactly once per `(tenant, event_id, observation index)`; observations create or update `past_project` rates per `(trade, normalised description key, unit, region)` as a weighted mean by quantity; the library `rate_min/max` and `sample_size` are updated; the new rate row is `pending_review` until verified (BR-EST-19).
- **BR-EST-40** Estimation error: when a `past_project` observation carries `estimated_rate_amount`, `rm_estimate_accuracy` stores `(actual - estimated) / estimated` by trade and project type; shown only with sample size (BR-EST-26).
- **BR-EST-41** Field visibility (BR-X-06): the whole module is invisible to `foreman` and `superintendent`. Within the module, margin fields (`profit_amount`, `margin_pct`, `markup_pct`, `rounding_adjustment_amount`, buildup rows of types `profit`, `risk_contingency`, `head_office`, scenarios' margin) are omitted unless the caller has `estimating.margin.view`; `procurement_officer` sees rates and quotes only. Exports obey the same rules.

---

## 7. API endpoints

Base `/api/v1/estimating`. All conventions per 00 §3.6: keyset pagination, actions as `POST`, `If-Match` on `PATCH` and mutating actions, `Idempotency-Key` on all creating or action `POST`s, filters whitelisted. Every response from this module omits margin fields unless the caller holds `estimating.margin.view` (BR-EST-41). Foreman and superintendent receive `403` on every path.

### 7.1 Tenders, documents, clauses, BOQ

| method | path | purpose | key request fields | key response fields | errors | permission |
| --- | --- | --- | --- | --- | --- | --- |
| GET | `/tenders` | list | filters below | `id, number, nameAr, clientName, status, submissionDeadline, estimatedValueAmount, ownerEstimator, bidPriceAmount*` | 403 | `estimating.tender.read` |
| POST | `/tenders` | register | `clientPartyId, regionCode, contractType, sourceChannel, projectType, receivedDate, submissionDeadline, nameAr/nameEn, estimatedValueAmount, bidBondRequired` | tender + `ETag` | `estimating.region_invalid`, `estimating.deadline_before_received`, 422 | `estimating.tender.create` |
| GET | `/tenders/{id}` | read (`?include=members,documents,clauses,currentEstimate`) |  | tender, `boqOutdated`, counts | 404 | `estimating.tender.read` |
| PATCH | `/tenders/{id}` | edit header | merge-patch of editable fields | tender | 412, `estimating.tender_locked` | `estimating.tender.update` |
| POST | `/tenders/{id}/start-pricing` | new to pricing | `ownerEstimatorId?` | tender, estimate v1 id | `estimating.go_no_go_required`, `estimating.invalid_state_transition` | `estimating.tender.update` |
| POST | `/tenders/{id}/no-bid` | decide not to bid | `reasonCode, rationale` | tender | `estimating.invalid_state_transition` | `estimating.decision.record` |
| POST | `/tenders/{id}/submit` | send bid | `submittedAt?, lateReason?, bidLetterDocumentId?` | tender (frozen price) | `estimating.deadline_passed`, `estimating.boq_outdated` | `estimating.tender.submit` |
| POST | `/tenders/{id}/revise` | client asked revision | `reason` | new estimate id | 409 | `estimating.estimate.update` |
| POST | `/tenders/{id}/withdraw` | withdraw | `reason` | tender | 409 | `estimating.tender.update` |
| POST | `/tenders/{id}/clear-boq-outdated` | confirm no BOQ change | `reason` | tender | 409 | `estimating.tender.update` |
| POST | `/tenders/{id}/handoff/retry` | republish won | none | handoff | `estimating.handoff_not_failed` | `estimating.outcome.record` |
| GET/POST/DELETE | `/tenders/{id}/members` | team | `userId, memberRole` | members | 409 duplicate | read: `estimating.tender.read`; write `estimating.tender.update` |
| GET/POST | `/tenders/{id}/documents` | list / attach | `docType, fileId, isAddendum, title` | document, `extractionStatus` | `estimating.file_type_not_allowed` | `estimating.document.read` / `.manage` |
| POST | `/tenders/{id}/documents/{docId}/extract` | queue AI extraction (R3) | none | `extractionStatus=queued` | 409 | `estimating.document.manage` |
| GET | `/tenders/{id}/clauses` | clauses | filters | clauses with `riskLevel, verificationStatus` |  | `estimating.tender.read` |
| POST | `/tenders/{id}/clauses` | add manual clause | `category, textAr, valueNumber, unit` | clause (classified) | 422 | `estimating.clause.manage` |
| POST | `/tenders/{id}/clauses/{cid}/verify` / `reject` / `accept-risk` | human review | `note` | clause | 403 | `estimating.clause.manage`; `accept-risk`: `estimating.clause.accept_risk` |
| POST | `/tenders/{id}/boq/import-preview` | step 1 and 2 of import | `fileId, mapping{}` | `previewId, rows[], errors[], counts` | `estimating.import_row_errors` | `estimating.boq.import` |
| POST | `/tenders/{id}/boq/import-commit` | step 3 | `previewId, skipErrorRows` | `importedCount, revision` | `estimating.preview_expired` | `estimating.boq.import` |
| GET | `/tenders/{id}/boq-items` | BOQ tree | filters | items, `parentId`, `unitCode`, `quantity`, `revision`, `verified` |  | `estimating.tender.read` |
| PATCH | `/tenders/{id}/boq-items/{itemId}` | verify / fix suggested item | `quantity, unitCode, verified` | item | 412 | `estimating.boq.import` |
| GET/POST | `/tenders/{id}/quotes` | supplier or subcontract quotes | `partyId, quoteDate, validUntil, lines[]` | quote | 422 | `estimating.quote.read` / `.manage` |
| POST | `/tenders/{id}/record-won` | outcome won | `contractValueAmount, awardDate, note` | outcome, handoff | `estimating.contract_value_differs` (warning) | `estimating.outcome.record` |
| POST | `/tenders/{id}/record-lost` | outcome lost | `outcomeDate, reasons[{code,isPrimary,comment}], winningBidderName?, winningPriceAmount?, ourRank?, biddersCount?` | outcome | `estimating.primary_reason_required` | `estimating.outcome.record` |
| GET | `/tenders/{id}/handoff` | saga status |  | `status, projectId, estimateId` |  | `estimating.tender.read` |

Tender list filters: `filter[status]`, `filter[clientPartyId]`, `filter[regionCode]`, `filter[projectType]`, `filter[ownerEstimatorId]`, `filter[contractType]`, `filter[sourceChannel]`, `filter[submissionDeadlineFrom|To]`, `filter[q]` (number or name, trigram). Sort: `submissionDeadline`, `-createdAt`, `number`, `estimatedValueAmount`. Default sort `submissionDeadline`.

### 7.2 Estimates

| method | path | purpose | key request fields | key response fields | errors | permission |
| --- | --- | --- | --- | --- | --- | --- |
| GET | `/estimates` | list | filters below | header totals, `status`, `version`, `isCurrent` |  | `estimating.estimate.read` |
| POST | `/estimates` | create tender estimate or empty imported one | `kind, tenderId?, name, priceDate, regionCode, markupPolicyId` | estimate | `estimating.tender_terminal` | `estimating.estimate.create` |
| POST | `/estimates/import` | R1 simple import (two-step like BOQ) | `projectId, fileId, mapping` | preview, then estimate | row errors | `estimating.estimate.create` |
| GET | `/estimates/{id}` | read with totals | `?include=buildup,tradeTotals,findings` | header, build-up, trade totals |  | `estimating.estimate.read` |
| PATCH | `/estimates/{id}` | edit header and build-up parameters | `profitMode, profitPct, targetPriceAmount, roundingStep` | estimate, `isStaleCalc` | `estimating.estimate_locked` | `estimating.estimate.update` |
| GET/POST | `/estimates/{id}/lines` | list / add line (`:bulk` supported, 500) | `boqItemId?, lineKind, description, unitCode, quantity, tradeCode` | line | `estimating.trade_unknown` | `estimating.estimate.update` |
| PATCH/DELETE | `/estimates/{id}/lines/{lineId}` | edit / delete |  | line | 412 | `estimating.estimate.update` |
| POST | `/estimates/{id}/lines/{lineId}/components` | add component | `componentType, consumptionQty, wastePct, unitRateAmount, rateId? \| quoteLineId? \| sourceNote` | component, line unit cost | `estimating.component_source_required` | `estimating.estimate.update` |
| GET | `/estimates/{id}/lines/{lineId}/rate-suggestions` | ranked candidates | `?componentType=` | candidates with `adjustment{escalation,region,spec,quantity,terms}`, `sampleSize`, `benchmark` |  | `estimating.rate.read` |
| GET/PUT | `/estimates/{id}/preliminaries` | detailed site overheads | lines array | lines, total |  | `estimating.estimate.update` |
| GET/PUT | `/estimates/{id}/buildup` | build-up items | items `{itemType,calcMode,basis,pct,lumpAmount}` | items, amounts\* | `estimating.profit_item_invalid` | `estimating.estimate.update`; profit rows `estimating.margin.view` |
| GET/POST/PATCH | `/estimates/{id}/risks`, `/assumptions` | register | `category, probabilityPct, impactAmount, includedInContingency` | risk, `expectedValueAmount` |  | `estimating.estimate.update` |
| POST | `/estimates/{id}/recalculate` | run calculator | none | totals, `calcInputsHash` |  | `estimating.estimate.update` |
| POST | `/estimates/{id}/run-checks` | run EstimateCheckEngine | none | findings |  | `estimating.estimate.update` |
| GET | `/estimates/{id}/findings` | list findings | \`filter\[severity | status | code\]\` | findings |
| POST | `/estimates/{id}/findings/{fid}/acknowledge` / `waive` / `resolve` | disposition | `note`; `waive` needs `reason` | finding | `estimating.waive_reason_required` | `estimating.estimate.update`; `waive`: `estimating.check.waive` |
| GET/POST | `/estimates/{id}/scenarios` | list / create+calculate | `name, overrides, priceMode` | scenario results\* | `estimating.scenario_invalid` | `estimating.scenario.manage` |
| POST | `/estimates/{id}/scenarios/{sid}/apply` | copy overrides into estimate | none | estimate | `estimating.estimate_locked` | `estimating.scenario.manage` |
| POST | `/estimates/{id}/submit-for-review` | request approval | `waiverNote?` | estimate `in_review`, `approvalId` | `estimating.open_blockers`, `estimating.calc_stale`, `estimating.unpriced_items`, `estimating.boq_outdated` | `estimating.estimate.update` |
| POST | `/estimates/{id}/recall` / `reopen` / `cancel` | state actions | `reason` | estimate | 409 | `estimating.estimate.update` |
| POST | `/estimates/{id}/approve-import` | R1 single-step approval | none | estimate `approved` | `estimating.self_approval_blocked` | `estimating.estimate.approve` |
| POST | `/estimates/{id}/finalize` | finalize imported estimate | none | estimate `finalized` | `estimating.cost_code_missing`, `estimating.project_closed` | `estimating.estimate.finalize` |
| GET | `/estimates/{id}/handoff-preview` | budget and revenue mapping preview |  | trade budget lines, revenue lines |  | `estimating.estimate.read` |
| GET | `/estimates/{id}/export` | xlsx / pdf / csv | `format, view=bid_summary\|by_trade\|detailed` | file or 202 job |  | `estimating.estimate.export` |

Estimate list filters: `filter[status]`, `filter[kind]`, `filter[tenderId]`, `filter[projectId]`, `filter[isCurrent]`, `filter[createdBy]`. Sort: `-createdAt`, `number`, `bidPriceAmount` (only with margin-visible callers for price-derived; price itself is not hidden from estimators). Lines filters: `filter[tradeCode]`, `filter[hasSource]`, `filter[lineKind]`, `filter[q]`; sort `sortOrder`, `-directCostAmount`.

### 7.3 Rate library, indices, policies, settings, decisions, reports

| method | path | purpose | permission |
|---|---|---|---|
| GET | `/rates` | list; filters `tradeCode, status, sourceType, regionCode, componentType, materialId, isCurrent, q`; sort `code, -rateDate, tradeCode` | `estimating.rate.read` |
| POST / GET / PATCH | `/rates`, `/rates/{id}` | create manual rate; supersede by PATCH creates new row (BR-EST-20) | `estimating.rate.manage` |
| POST | `/rates/{id}/verify`, `/retire` | verify past-project rate, retire | `estimating.rate.manage` |
| POST | `/rates:bulk` / `/rates/import` | import rates from Excel (PLT-E9) | `estimating.rate.manage` |
| GET | `/rates/{id}/history` | superseded chain and observations | `estimating.rate.read` |
| GET/POST/PATCH | `/escalation-indices` | monthly indices | `estimating.settings.manage` |
| GET/PUT | `/rate-adjustment-factors` | region/spec/quantity/terms factors | `estimating.settings.manage` |
| GET/POST/PATCH | `/markup-policies` | policies | read `estimating.estimate.read`; write `estimating.settings.manage` |
| GET/PATCH | `/settings` | tenant settings | `estimating.settings.manage` |
| GET/POST/PATCH | `/loss-reasons` | reference data | write `estimating.settings.manage` |
| GET | `/tenders/{id}/decisions` | decision history | `estimating.decision.read` |
| POST | `/tenders/{id}/decisions` | request go/no-go or price approval | `estimating.decision.record` |
| GET | `/reports/win-loss`, `/reports/pipeline`, `/reports/estimate-accuracy`, `/reports/rate-usage` | read models (section 13) | `estimating.report.view` |
| GET | `/benchmarks` | trade/project-type cost per unit; `benchmark:null` below sample | `estimating.margin.view` or `estimating.rate.read` (cost only) |

### 7.4 JSON examples

**Create scenario (hospital, materials +8 %, price held)**

```json
POST /api/v1/estimating/estimates/6f1c.../scenarios
Idempotency-Key: 0d6b8f6e-...
{ "name": "Materials +8 %, price held",
  "overrides": { "materialPct": 8, "labourPct": 0, "equipmentPct": 0, "subcontractPct": 0 },
  "priceMode": "hold_price" }

201 Created
{ "id": "a4d2...", "isBase": false, "priceMode": "hold_price",
  "directCostAmount": 51860000.0000, "costBeforeProfitAmount": 59992425.9000,
  "bidPriceAmount": 62014000.0000, "profitAmount": 2021574.1000,
  "marginPct": 3.2599, "belowFloor": true, "calcVersion": "est-calc-1" }
```

(The 3.2589 % in section 1.3 is on the unrounded price 62,013,381.705; with the rounded price 62,014,000 the golden test asserts 3.2599 %. Both are asserted in section 15.)

**Submit for review blocked**

```json
POST /api/v1/estimating/estimates/6f1c.../submit-for-review   ->  422
{ "code": "estimating.open_blockers", "status": 422,
  "detail": "Open blocker findings must be resolved or waived.",
  "params": { "blockers": [ { "code": "E03", "findingId": "..." }, { "code": "E01", "count": 4 } ] } }
```

**Record won**

```json
POST /api/v1/estimating/tenders/9b3e.../record-won
{ "contractValueAmount": 62014000.0000, "awardDate": "2026-11-02", "note": "Letter of award received" }

200 OK
{ "tenderStatus": "won",
  "handoff": { "id": "...", "status": "pending", "estimateId": "6f1c..." },
  "warnings": [] }
```

**Rate suggestions**

```json
GET /estimates/6f1c.../lines/3a9e.../rate-suggestions?componentType=material
200 { "items": [ { "rateId": "...", "code": "RC-C35-CAIRO", "baseRateAmount": 1850.0000, "unitCode": "m3",
      "adjusted": { "rateAmount": 1933.6200, "factors": { "escalation": 1.0400, "region": 1.0000, "spec": 1.0050, "quantity": 1.0000, "terms": 1.0000 } },
      "sourceType": "price_book", "rateDate": "2026-09-01", "sampleSize": 6, "warnings": [] } ],
  "benchmark": null, "benchmarkReason": "insufficient_sample", "sampleSize": 3 }
```

---

## 8. Events

Envelope per 00 §3.9. Partition key is the aggregate id (tender id or estimate id). Payload amounts are `numeric` strings with `currency`.

### 8.1 Published

| Event | Fires when | Payload (beyond envelope) |
|---|---|---|
| `estimating.tender.won.v1` | `record-won` commits (and on `handoff/retry` with `retry=true`) | `tenderId, tenderNumber, handoffId, estimateId, clientPartyId, nameAr, nameEn, projectType, regionCode, contractType, contractValueAmount, currency, awardDate, expectedDurationDays, submittedPriceAmount, retry` |
| `estimating.estimate.finalized.v1` | Saga finalize step (tender estimates, after `project_linked`) or `finalize` of an imported estimate | `estimateId, kind, tenderId?, projectId, version, currency, directCostAmount, costBeforeProfitAmount, bidPriceAmount, scaleFactor, handoffUrl` (contract query `GetHandoff(estimateId)`), `tradeCount, lineCount, hasRevenue` (false for imported) |

`projectId` on the envelope is null for `tender.won` and set for `estimate.finalized`. Margin fields are not published (privacy: consumers `cost` and `contracts` do not need profit). Contract tests: JSON schemas `estimating.tender.won.v1.schema.json` and `estimating.estimate.finalized.v1.schema.json`.

### 8.2 Consumed

| Event | Handler | Action | Idempotency |
| --- | --- | --- | --- |
| `platform.approval.approved.v1` / `.rejected.v1` / `.cancelled.v1` | `ApprovalOutcomeHandler` (recordType `estimating.estimate_review`, `estimating.bid_decision`, `estimating.award_confirmation`) | Section 5.1/5.2 transitions; set decision result; notifications | `processed_events`; state guard (ignore if already moved); approval id match |
| `platform.approval.step_approved.v1`, `.escalated.v1` | same handler | notifications only | `processed_events` |
| `core.project.created.v1` | `AwardHandoffOrchestrator` | If `origin.type='tender'`: link project, finalize estimate (5.3) | Handoff unique per tender; skip when status already `project_linked` or later |
| `core.cost_code.created.v1` / `.updated.v1` | cache refresh | refresh cost-code lookup used by handoff validation | upsert by id |
| `core.material.created.v1` / `.updated.v1` | `MaterialRefHandler` | refresh material lookup; flag `rates` whose material was deactivated | upsert by id |
| `sourcing.price.updated.v1` | `PriceBookRateSync` | BR-EST-18 | `processed_events` + unique `(source_ref_id, price_month)` |
| `closeout.lesson.published.v1` | `ActualRateIngestor` | BR-EST-39, BR-EST-40 | unique `(tenant, event_id, observation_index)` |
| `intelligence.suggestion.created.v1` (target `estimating.tender_document`) | `ExtractionSuggestionHandler` | store suggested BOQ items and clauses as `suggested` | `processed_events`; suggestion id stored |

### 8.3 Proposed registry additions

1. `estimating.estimate.submitted.v1` (fires on tender submission; consumers: reporting, contracts for pre-award visibility) is not required for R1/R2 and is NOT proposed now.
2. `platform.approval` record types `estimating.estimate_review`, `estimating.bid_decision`, `estimating.award_confirmation` must be registered in the approval type catalogue (PLT-E3), not in the event registry. No new event names are needed.

---

## 9. Background jobs and schedules

| Job | Schedule | What | Idempotency | Per tenant |
| --- | --- | --- | --- | --- |
| `TenderDeadlineJob` | daily 07:00 tenant time (Africa/Cairo) | Tenders in `new/pricing/review/approved` with `submission_deadline` within `deadline_alert_days` raise finding E04 on the estimate and notification `EST_DEADLINE_NEAR`; also 24 h and 2 h reminders | one notification per `(tender, threshold)` stored in `tender_alerts_sent` | yes |
| `RateStalenessJob` | weekly Sunday 03:00 | BR-EST-16: mark rates `stale` where no newer current rate | updates are conditional on status; re-run safe | yes |
| `EstimateRebuildJob` | trigger: `escalation_indices`, `rate_adjustment_factors` or settings changed | Marks open draft estimates `is_stale_calc=true`; does not change values (BR-EST-20) | idempotent flag set | yes |
| `AwardHandoffWatchdog` | every 15 min | Handoffs in `pending`/`project_requested` older than 24 h become `failed` and notify (5.3) | status guard | all tenants, per-tenant loop |
| `ImportPreviewCleanupJob` | hourly | Deletes import previews older than 24 h | delete by timestamp | all |
| `WinLossProjectionRebuild` | nightly 02:30 plus on demand | Rebuilds `rm_*` from source tables | truncate-and-load per tenant in a transaction | yes |
| `DocumentExtractionDispatcher` (R3) | on `extract` action | Sends document to intelligence; times out after 30 min to `failed` | document id | yes |
| `ExportJob` | trigger | Large exports (> 5,000 rows) | job id | yes |

---

## 10. Permissions

Codes `estimating.<resource>.<action>`. Legend: E estimator, Q cost\_qs\_engineer, PM project\_manager, CM contracts\_manager, X owner\_executive, F finance\_controller, T tenant\_admin, P procurement\_officer. `foreman` and `superintendent` have none.

| Code | Roles | Notes |
|---|---|---|
| `estimating.tender.read` | E, Q, PM\*, CM, X, F, T | PM only for tenders where member (BR-X-09); X, E, T, F all tenders |
| `estimating.tender.create` / `.update` | E, X, T |  |
| `estimating.tender.submit` | E, X | `estimating.tender.submit_late` additionally X only |
| `estimating.document.read` | E, Q, PM\*, CM, X, T |  |
| `estimating.document.manage` | E, CM, T |  |
| `estimating.boq.import` | E, Q |  |
| `estimating.clause.manage` | E, CM |  |
| `estimating.clause.accept_risk` | CM, X | critical clauses |
| `estimating.quote.read` | E, Q, P | P sees prices only, no margin |
| `estimating.quote.manage` | E, Q |  |
| `estimating.estimate.read` | E, Q, PM\*, CM, X, F, P (rates only) |  |
| `estimating.estimate.create` / `.update` | E, Q | amount limit none; approval governs release |
| `estimating.estimate.approve` | PM, X (R1 import path: PM, Q) | amount limit from approval policy; segregation BR-X-07 |
| `estimating.estimate.finalize` | PM, Q, X | imported path only; tender path is system |
| `estimating.estimate.export` | E, Q, X, F | exports honour margin visibility |
| `estimating.check.waive` | E, X | E3 waiver chain includes X |
| `estimating.scenario.manage` | E, X | scenario results contain margin: requires margin view to read |
| `estimating.margin.view` | E, X, F, T, PM (tender member with `can_view_margin`) | field-level: omits `profitAmount, marginPct, markupPct, roundingAdjustmentAmount`, profit/head-office/contingency build-up rows, scenario margin, decision margin snapshot |
| `estimating.decision.read` / `.record` | read: E, PM\*, X, F; record: E, PM, X | approval by X (limit above PM) |
| `estimating.outcome.record` | E, X | award confirmation approval if tenant policy on |
| `estimating.rate.read` | E, Q, PM, X, F, P | P sees rates and sample size, never margins |
| `estimating.rate.manage` | E, Q, T | verification of past-project rates |
| `estimating.settings.manage` | T, F (head-office rate only) | markup policies, indices, factors, loss reasons |
| `estimating.report.view` | E, X, F, T | win/loss with margins needs margin view |

Amount limits live in the approval policy (PLT-E3): default PM approves bid price up to 20,000,000 EGP, X above (tenant-configurable, Open question 3). Project scope: estimates of imported kind follow `platform.project_members`; tender estimates follow tender membership. Search (PLT-E10) indexes tender number, name and client only for callers with `estimating.tender.read`.

---

## 11. Notifications

Via `INotificationService` (00 §3.12); in-app plus email by default, push for `EST_DEADLINE_NEAR`; WhatsApp not used in this module. Margin figures never appear in notification text.

| Code | Recipients | Trigger | Channel | Arabic sample |
| --- | --- | --- | --- | --- |
| `EST_TENDER_ASSIGNED` | owner estimator | tender registered or owner changed | in-app, email | تم إسناد المناقصة \{number\} إليك، آخر موعد للتقديم \{deadline\}. |
| `EST_DEADLINE_NEAR` | owner estimator, tender members | `TenderDeadlineJob` | in-app, email, push | تبقّى \{hours\} ساعة على تقديم المناقصة \{number\}. |
| `EST_ADDENDUM_RECEIVED` | owner estimator | addendum document attached | in-app, email | وصل ملحق جديد للمناقصة \{number\}؛ جدول الكميات يحتاج مراجعة. |
| `EST_REVIEW_REQUESTED` | approvers (from approval) | submit-for-review | in-app, email | طلب اعتماد سعر المناقصة \{number\} بقيمة \{price\}. |
| `EST_ESTIMATE_APPROVED` | requester, owner estimator | approval approved | in-app, email | تم اعتماد تقدير المناقصة \{number\}، يمكن التقديم. |
| `EST_ESTIMATE_REJECTED` | requester | approval rejected | in-app, email | تم رفض تقدير المناقصة \{number\}: \{reason\}. |
| `EST_BLOCKER_FOUND` | owner estimator | run-checks raises new blocker | in-app | توجد \{count\} ملاحظات مانعة في تقدير \{number\}. |
| `EST_CLAUSE_CRITICAL` | contracts\_manager, owner\_executive | clause classified `critical` | in-app, email | بند عالي الخطورة في شروط المناقصة \{number\}: \{category\}. |
| `EST_TENDER_SUBMITTED` | executive, members | submit | in-app | تم تقديم عطاء المناقصة \{number\}. |
| `EST_TENDER_WON` | executive, PM designate, finance\_controller | record-won | in-app, email | تم ترسية المناقصة \{number\}، جاري إنشاء المشروع. |
| `EST_TENDER_LOST` | executive, owner estimator | record-lost | in-app, email | لم تُرسَ المناقصة \{number\}، السبب الرئيسي: \{reason\}. |
| `EST_HANDOFF_FAILED` | estimator, tenant\_admin | watchdog | in-app, email | تعذّر إنشاء المشروع من المناقصة \{number\}، يرجى إعادة المحاولة. |
| `EST_RATE_REVIEW_PENDING` | users with `estimating.rate.manage` | new `pending_review` rates from lessons (daily digest) | in-app | \{count\} أسعار من مشاريع سابقة بانتظار المراجعة. |

Deduplication key is `(code, entityId, recipient, threshold)`; user preferences per PLT-E7.

---

## 12. Error codes

All prefixed `estimating.`; format per 00 §3.7.

| code | HTTP | when | Arabic | English |
| --- | --- | --- | --- | --- |
| `invalid_state_transition` | 409 | action not allowed from current status | الإجراء غير مسموح في الحالة الحالية | Action not allowed in the current status |
| `tender_locked` | 409 | edit of submitted or terminal tender fields | لا يمكن تعديل مناقصة بعد التقديم | Tender can no longer be edited |
| `tender_terminal` | 422 | estimate created on a won/lost/withdrawn/no\_bid tender | المناقصة منتهية | Tender is closed |
| `region_invalid` | 422 | region not in settings | المنطقة غير معرفة | Region not configured |
| `deadline_before_received` | 422 | BR-EST-02 | موعد التقديم قبل تاريخ الاستلام | Deadline is before received date |
| `go_no_go_required` | 422 | start-pricing above threshold without approved decision | يلزم قرار المشاركة قبل بدء التسعير | A go/no-go decision is required first |
| `deadline_passed` | 422 | submit after deadline without override | انتهى موعد التقديم | Submission deadline has passed |
| `boq_outdated` | 422 | BR-EST-03 | جدول الكميات قديم بعد ملحق | BOQ is outdated after an addendum |
| `file_type_not_allowed` | 422 | BR-EST-04 | نوع الملف غير مسموح | File type not allowed |
| `import_row_errors` | 422 | preview contains errors and commit is all-or-nothing | توجد أخطاء في صفوف الملف | Import file has row errors |
| `preview_expired` | 410 | preview older than 24 h | انتهت صلاحية المعاينة | Import preview expired |
| `estimate_locked` | 409 | edit of in-review/submitted/finalized estimate | التقدير مقفل | Estimate is locked |
| `trade_unknown` | 422 | BR-EST-13 | بند الأعمال غير معرّف | Unknown trade |
| `component_source_required` | 422 | component without rate, quote or note | يلزم مصدر للسعر | A price source is required |
| `profit_item_invalid` | 422 | build-up without exactly one last profit item, pct >= 100 for margin mode | بند الربح غير صحيح | Invalid profit item |
| `target_below_cost` | 422 | BR-EST-24 | السعر المستهدف أقل من التكلفة | Target price is below cost |
| `scenario_invalid` | 422 | override out of range or unknown key | بيانات السيناريو غير صحيحة | Invalid scenario |
| `open_blockers` | 422 | BR-EST-32 | توجد ملاحظات مانعة مفتوحة | Open blocking findings |
| `calc_stale` | 422 | `is_stale_calc` true | يلزم إعادة الحساب | Recalculation required |
| `unpriced_items` | 422 | BOQ items without estimate line | توجد بنود بدون تسعير | Unpriced BOQ items |
| `waive_reason_required` | 422 | waive without reason | يلزم سبب الاستثناء | Waiver reason required |
| `self_approval_blocked` | 403 | BR-X-07 | لا يمكنك اعتماد ما أرسلته | You cannot approve your own submission |
| `primary_reason_required` | 422 | BR-EST-34 | يلزم سبب رئيسي واحد | Exactly one primary reason is required |
| `contract_value_differs` | 200 warning | BR-EST-35 | قيمة العقد تختلف عن السعر المقدم | Contract value differs from bid |
| `handoff_not_failed` | 409 | retry when not failed | لا حاجة لإعادة المحاولة | Handoff is not in failed state |
| `cost_code_missing` | 422 | BR-EST-37 | بنود بدون كود تكلفة | Lines without cost code |
| `project_closed` | 422 | finalize into closed project | المشروع مغلق | Project is closed |
| `rate_superseded` | 409 | edit of non-current rate | السعر تم استبداله | Rate was superseded |
| `escalation_index_missing` | 200 warning | BR-EST-14 | مؤشر التضخم غير متوفر | Escalation index missing |
| `insufficient_sample` | 200 (field reason) | BR-EST-26 | العينة غير كافية للمقارنة | Sample too small for benchmark |
| `margin_below_floor` | 200 warning | BR-EST-27 on recalculation | الهامش أقل من الحد الأدنى | Margin below floor |

Plus platform codes `precondition_failed` (412), `idempotency_key_reuse` (422), `forbidden` (403).

---

## 13. Reports and read models

Projections update from domain events inside the module (same transaction for header facts) and by `WinLossProjectionRebuild` nightly (reconciliation). Margin columns are omitted for callers without `estimating.margin.view`.

### 13.1 `estimating.rm_bid_pipeline` (one row per open tender)

| column | type | notes |
| --- | --- | --- |
| `tender_id` | uuid | PK with `tenant_id` |
| `status`, `region_code`, `project_type`, `client_party_id` | text/uuid |  |
| `submission_deadline` | timestamptz |  |
| `estimated_value_amount`, `bid_price_amount` | numeric(18,4) |  |
| `margin_pct` | numeric(9,4) | margin-gated |
| `open_blockers`, `open_warnings` | int |  |
| `days_to_deadline` | int | computed at refresh |
| `owner_estimator_id` | uuid |  |

Refresh: on tender/estimate/finding change. Index `(tenant_id, status, submission_deadline)`.

### 13.2 `estimating.rm_win_loss` (one row per decided tender)

`tender_id, result (won/lost/withdrawn/no_bid), decided_on, client_party_id, region_code, project_type, contract_type, source_channel, our_price_amount, winning_price_amount, price_gap_pct ((our - winning)/winning x 100), our_margin_pct*, our_rank, bidders_count, primary_reason_code, primary_reason_category, estimator_id, estimate_cost_amount`. Refresh: on outcome recorded and nightly. Win rate = won / (won + lost) by count and by value; no-bid and withdrawn excluded from the denominator but reported. Slices shown only with at least `benchmark_min_sample` tenders (BR-EST-26).

### 13.3 `estimating.rm_estimate_accuracy`

`trade_code, project_type, region_code, observations int, mean_error_pct, median_error_pct, stddev_pct, last_updated`. Built from BR-EST-40; rows with `observations < benchmark_min_sample` are stored but returned with `value: null`.

### 13.4 `estimating.rm_rate_usage`

`rate_id, estimates_count, last_used_at, avg_adjustment_factor`. Refresh nightly. Used to find unused or heavily used rates.

### 13.5 Reports and exports

| Report | Columns | Formats |
|---|---|---|
| Bid summary (per estimate) | trade, direct cost, build-up lines, cost before profit, profit\*, bid price, VAT, margin\* | xlsx, pdf |
| Estimate by trade | trade, lines, direct cost, % of D, bid amount | xlsx, csv |
| Priced BOQ (client format) | item no., description, unit, quantity, unit rate, amount, `ROUNDING` line | xlsx, pdf (no cost, no margin) |
| Scenario comparison | scenario, overrides, D, C, price, margin\*, below floor | xlsx, pdf |
| Win/loss | per 13.2 | xlsx, csv |
| Pipeline | per 13.1 | xlsx, csv |
| Rate library | per rates columns | xlsx, csv |

Priced BOQ export is the only document safe to hand to a client; it is generated from the allocation in BR-EST-25a and has no cost columns, so its permission is `estimating.estimate.export` without margin view.

---

---

## 15. Test scenarios, non-functional notes, open questions

### 15.1 Named scenarios

| Scenario | Input | Expected |
|---|---|---|
| S1 Hospital build-up | D 50,100,000 (material 22,000,000; labour 9,000,000; equipment 4,100,000; subcontract 15,000,000); site 8 % direct; head office 4.5 % cumulative; risk 2.5 % cumulative; markup 7 % | site 4,008,000.0000; head office 2,434,860.0000; risk 1,413,571.5000; C 57,956,431.5000; profit 4,056,950.2050; price 62,013,381.7050; margin 6.5421 %; markup 7.0000 % |
| S2 Rounding | S1 with step 1,000 up | price 62,014,000.0000; adjustment 618.2950; profit 4,057,568.5000; margin 6.5430 %; VAT 14 % = 8,681,960.0000 |
| S3 Margin-on-price mode | C 57,956,431.5, margin 6.5421 % | price within 0.01 of 62,013,381.7; `pct >= 100` rejected |
| S4 Materials +8 %, hold price | S1, scenario materialPct 8 | D 51,860,000; C 59,992,425.9; profit 2,020,955.805 (unrounded price); margin 3.2589 %; with rounded price margin 3.2599 %; `belowFloor` true; E03 raised on apply |
| S5 Hold margin | S4 solve price at 6.5421 % | price = C' / (1 - 0.065421) within rounding |
| S6 Target price below cost | target 57,000,000 | 422 `target_below_cost` |
| S7 Price allocation | lines 10 x 100 and 5 x 200, D 2,000, price 2,300 | rates 115 and 230; amounts 1,150 and 1,150; rounding 0 |
| S8 Handoff mapping | S1 won at 62,014,000 | trade budgets 50,100,000; general conditions 4,008,000; risk reserve 1,413,571.5; head office, profit, financing not budgeted |
| S9 Contract value differs | won contract 62,500,000 vs bid 62,014,000 (+0.78 %) | warning; scale factor 1.007837; revenue lines sum 62,500,000 |
| S10 Rate adjustment | base 1,850; escalation 104/100; spec 1.005 | 1,933.6200 with factors listed |
| S11 Stale rate | rate\_date 8 months old, window 6 | E02 warning; job sets `stale` only if no newer current rate |
| S12 Outlier | group median 100, sample 6, outlier 30 %, rate 140 | E10; sample 4 gives no E10 |
| S13 Addendum | addendum type boq after import | `boqOutdated`; submit-for-review 422 until re-import or clear |
| S14 Segregation | requester approves own estimate | 403 `self_approval_blocked`; allowed with tenant flag and audit |
| S15 Lost with reasons | two reasons, one primary | outcome stored; `rm_win_loss.price_gap_pct` = (our - winning)/winning x 100 |
| S16 Saga idempotency | `core.project.created` delivered twice, then `tender.won` retried | one finalize event; same `handoffId` |
| S17 Margin visibility | cost\_qs\_engineer reads estimate, scenarios, decisions, exports | no profit, margin, markup, head-office, contingency rows anywhere |
| S18 Simple import | 3 rows: concrete 1,000 m3 at 1,800; steel 50 t at 32,000; block total 450,000 | D 3,850,000; one `other` component per line; finalize publishes event with `hasRevenue=false` |
| S19 Tenant isolation | tenant A token, tenant B ids on all endpoints and exports | 404 everywhere; RLS check with application filter disabled |
| S20 Reproducibility | recalculate twice, change nothing | identical `calcInputsHash` and amounts; changing one rate changes the hash |

### 15.2 Non-functional notes

| Topic | Target and approach |
|---|---|
| Volumes (large tenant) | 150 tenders/year, 40 estimate versions/year, 5,000 lines and 15,000 components per estimate, 100,000 rates, 2,000,000 observations. |
| Recalculate | p95 under 2 s for 5,000 lines (in-memory calculator, set-based writes); results stored, not computed on read. |
| BOQ preview | 5,000 rows under 10 s (00 §3.17). |
| Lists | Keyset on `(tenant_id, created_at desc, id desc)`; p95 under 300 ms. |
| Rate search | trigram on description, composite `(tenant_id, material_id, region_code) where is_current`; p95 under 300 ms. |
| Concurrency | `If-Match` on estimate; recalculation takes an advisory lock per estimate id so two users cannot race. |
| Retention | Submitted estimates and decisions are never deleted; drafts of lost tenders archived after 24 months by tenant policy (Open question 6). |

### 15.3 Security notes

- Margin and profit are the most sensitive data of the company: omitted (not nulled) for unauthorised callers in JSON, exports, search index, notifications and event payloads.
- Tender documents may be confidential to the client: file access goes through signed URLs from `IFileService`, and access is audited.
- All state changes and waivers audited with reason (PLT-E5); approval snapshots immutable.
- Imports validated for formula injection in cells (`=`, `+`, `@`) when re-exported to Excel.
- No AI output changes a number without human verification (BR-X-08).

### 15.4 Open questions (for the business)

1. What are your standard percentages for site overheads, head office, risk contingency and profit? Do they differ by project type or client? (Section 1.3 values are illustrative.)
2. Is profit set as markup on cost or margin on price in your company, and who may change it per tender?
3. What are the approval limits: up to what bid price can a project manager approve, and when must the owner approve?
4. Which tenders need a go/no-go decision before pricing (value threshold, new clients, certain regions)?
5. Do you round bid prices (to 1,000, 10,000)? Up, down or nearest?
6. How long must lost-tender estimates and documents be kept?
7. After award, is the contract value usually equal to the bid, or negotiated? Should the revenue BOQ be scaled or re-entered?
8. Do you need to price in USD/SAR for Saudi tenders, and which exchange rate source and date applies?
9. Which escalation indices do you track (steel, cement, labour, fuel) and who maintains them monthly?
10. Should past-project rates be verified by the estimator or by the cost engineer of that project?
11. Do clients give BOQs mostly in Excel or PDF? Is PDF extraction needed in the first tendering release?
12. Is the bid bond (guarantee) tracked here, or in contracts/cash?
