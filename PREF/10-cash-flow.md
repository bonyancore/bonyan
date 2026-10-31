---
title: "10 Cash flow"
doc_no: "10"
section: "Commercial and Finance"
status: "Proposal"
source: https://app.notion.com/p/63d4cdd2129683908b67012f0309884e?pvs=204
exported: 2026-10-29 (Notion MCP)
---

# Construction Connect — Backend Specification 10: Cash Flow (Module 7)

**Module key:** `CSH` (tickets `CSH-001…`) · **Schema:** `cash` (00 §5 fixes the schema name as `cash`; the assignment text said `cashflow`, 00 wins until an ADR changes it) · **v3 source:** section 4.7, lines 754-791 · **Reads with:** `00` (conventions, event registry), `06` Procurement (invoices, payments), `09` Contracts (payment applications, receipts, retention), `08` Cost Control (budget, forecast), `11` Subcontracts (R2). <br> Conventions are not repeated here: standard columns per 00 §3.2, API rules per 00 §3.6, errors per 00 §3.7, events per 00 §3.9, jobs per 00 §3.10, tickets per 00 §10.

---

## 1. Purpose and business context

### 1.1 The business problem in plain language

A contractor does not go bankrupt because a project loses margin; it goes bankrupt because **cash runs out in a particular week**. Construction is a business of long gaps: the company pays labour weekly or monthly, pays suppliers 30-60 days after delivery, and pays subcontractors after their monthly valuation, while the client pays only after the company submits a **payment application** (مستخلص — the monthly claim for work done), the client's engineer **certifies** it (reduces or approves the amount), and the client's payment term (for example 30 days) passes. A profitable project can therefore still be short of cash for a month.

This module answers one question for the finance controller: **"For the next 13 weeks, week by week, how much money will come in, how much will go out, and what will the bank balance be — and in which week is the first problem?"** It also warns early (before the week arrives), explains the problem (which receipts are late, which payments are large), and lets the controller test "what if" changes.

### 1.2 Terms used in this file

| Term | Meaning |
|---|---|
| Receivable (مستحقات / ذمم مدينة) | Money the client (or another party) owes us: certified payment applications, client invoices, retention releases, settled claims. |
| Payable (ذمم دائنة) | Money we owe: matched supplier invoices, subcontractor certificates, credit notes (negative), back-charges (negative). |
| Commitment (التزام) | A purchase order or subcontract already issued but not yet fully invoiced. It is a **future payable**: the money is not owed yet but will be. |
| Retention (محتجزات) | A percentage of each certificate (for example 5%) held by the client until handover; collected later as a retention release. |
| Cheque, post-dated (شيك آجل) | In Egypt payments and receipts are often by cheques dated in the future. A post-dated cheque we issued will leave the bank on its date; one we received can be deposited on or after its date. |
| Opening balance | Cash available at the start of week 1 (bank balance for the company, or the cash allocated to a project). |
| Floor (الحد الأدنى للرصيد) | The minimum balance the company wants to keep. A forecast closing balance below the floor is a **shortfall**, even when the balance is still positive. |
| Week 1 | The week (Saturday start by default, `locale.week_start`, 01 BR-PLT-153) that contains the forecast `as_of_date`. |
| Scenario | A saved set of what-if adjustments on top of the live forecast (R3). |
| Confidence | `high` (certified/contractual), `medium` (submitted, expected), `low` (assumed, commitment-based). An expected date is **never** presented as guaranteed cash (v3 line 782). |

### 1.3 How the module fits the master lifecycle (flows F1-F7)

Cash Flow is **downstream of everything and upstream of nothing except reporting**. It owns no source documents; it owns **schedules** (a cash-timed view of documents owned by other modules), **manual plans** (payroll, overheads, loans, taxes, cheques), **forecasts**, and **scenarios**.

| Flow / step | What Cash Flow does |
|---|---|
| F4 procure (PO issued, goods received, invoice matched, payment) | `procurement.purchase_order.*` create/adjust **commitments**; `invoice.registered/matched/disputed` create **payables**; `payment.scheduled/executed` move payables to scheduled/paid and record **actuals**. |
| F5 subcontract (R2) | Subcontract signed/revised become commitments; certified valuations and back-charges become payables. |
| F7/F8 bill and collect | `payment_application.submitted` creates an **expected receivable** (medium confidence); `certified` replaces it with the certified receivable (high confidence); `invoice_to_client.issued` and `receipt.recorded` update it; `retention.released` creates retention receivables. |
| F6 control and close | `cost.forecast.updated`, `cost.budget.*`, `cost.period.closed` give the uncommitted-spend estimate (R2) and the period snapshot; `planning.schedule.*` give dates for spreading. |
| Command Center (Module 1) | `cash.forecast.refreshed` and `cash.shortfall.detected` feed the portfolio and exceptions views (03). |

### 1.4 What is in each release

| Capability | Release |
|---|---|
| Receivables, payables, commitments, cheque register, bank accounts (simple), payroll plans, planned items, aging, payment proposals, **project** 13-week and 12-month forecast, floor alerts, trade view, overdue alerts | **R1** |
| **Company** forecast (all projects plus company overheads), shortfall explanation, bank statement reconciliation, accounting export and ETA-ready invoice data, subcontract commitments/payables, uncommitted-budget spend in forecast | **R2** |
| Scenarios (delay collection, defer or bring forward a payment, follow up an overdue certificate), collection-date prediction from client history | **R3** |

### 1.5 The golden example (used throughout)

Project forecast as of Saturday 2026-10-10: opening balance **2,400,000** EGP, floor **600,000**. Payment application 4 (certified payable **2,352,590**, see 09 and `_BRIEF` golden numbers) is due **2026-11-10 (week 5)**. Payment application 5 (**2,600,000**, submitted, expected) is due about **2026-12-08 (week 9)**. Outflows (payroll, suppliers, subcontractors, overheads) make the balance fall to **562,590 at the end of week 8**, which is **37,410 below the floor**, before the second collection arrives in week 9. The full 13-week table is in section 15.1 and drives the golden test.

---

## 2. Users and roles

Roles per 00 §4.4. External users (supplier, subcontractor, client portals) have **no** access to this module in any release; supplier payment status shown in the supplier portal comes from Procurement, not from Cash.

| Role | What they do here | Scope |
|---|---|---|
| `finance_controller` | Primary user. Maintains plans (payroll, planned items, cheques, bank accounts), sets floor and assumptions, reviews and overrides expected dates, prioritises payables, generates and accepts payment proposals, acknowledges shortfalls, runs scenarios, reconciles statements, runs exports. | All projects |
| `owner_executive` | Reads company and project forecasts and shortfalls; approves large write-offs and large payment batches (the batch itself is in Procurement). | All projects |
| `project_manager` | Reads the forecast, receivables and payables of **own** projects; sees payroll only as a weekly category total (no plan detail); can follow up overdue certificates (add follow-up notes). | Project membership |
| `contracts_manager` | Reads receivables and the client side of the forecast; adds follow-up notes on overdue certificates; reads exposure (pending variations/claims). | Project membership |
| `procurement_officer` | Reads payables (not receivables, not forecast totals); sets **payment priority** and proposes planned pay dates for supplier payables so payment priorities reflect delivery needs. | Project membership; payables only |
| `cost_qs_engineer` | No access in R1 (v3 matrix shows no entry); gets the trade view in reports via Reporting only. | n/a |
| `tenant_admin` | Configures number series, notification recipients and company-level settings; no financial data access unless also a finance role. | Tenant |
| `foreman`, `superintendent`, `document_controller`, `qa_qc_engineer`, `safety_officer`, `estimator` | No access (all endpoints return `403`). | n/a |

Hidden-field summary (details in section 10): payroll plan lines and bank account numbers are visible only to `finance_controller` and `owner_executive`; `project_manager` sees payroll as category totals. Nothing in this module exposes unit rates or margins, but BR-X-06 still applies: if a role lacks `cash.forecast.view`, the field or endpoint is **omitted/403**, not nulled.

---

## 3. Services and components

Module projects per 00 §2.2: `Cash.Contracts` (events, `ICashQuery` for Reporting), `Cash.Core`, `Cash.Infrastructure`, `Cash.Api`.

### 3.1 Aggregates (rich) and CRUD

| Component | Kind | Responsibility |
|---|---|---|
| `Receivable` | aggregate | Lifecycle of expected/certified/invoiced/received money; expected-date override; follow-ups; write-off request. |
| `Payable` | aggregate | Lifecycle matched/scheduled/paid; priority, planned pay date; dispute freeze. |
| `Commitment` (+ lines) | aggregate | Open PO/subcontract value not yet invoiced; maintains `remaining` for forecast. |
| `Cheque` | aggregate | Issued/received cheque lifecycle with allocations to payables/receivables. |
| `CashForecast` | aggregate (00 §2.3) | One live forecast per scope (project or company), snapshots, scenario results; owns periods and items; evaluates floor. |
| `PaymentProposal` | aggregate | A proposed payment run with lines; accept hands over to Procurement/Subcontracts. |
| `Scenario` | aggregate | Adjustments plus computed result (R3). |
| `CashAlert` | aggregate | Alert lifecycle and dedupe. |
| Bank accounts, assumptions, forecast settings, payroll plans, planned items, bank statement imports (R2), export batches (R2) | CRUD | Plain reference/plan data with optimistic concurrency. |

### 3.2 Handlers, calculators and jobs

| Name | Type | One-line responsibility |
|---|---|---|
| `ContractTermsHandler` | consumer | Stores payment terms, retention, VAT, completion date per contract (`contract_refs`) from `contracts.contract.signed/amended`. |
| `PaymentApplicationHandler` | consumer | Creates/updates the receivable from `submitted` (expected) and `certified` (certified). |
| `ClientInvoiceHandler` | consumer | Links the client invoice to the certificate receivable or creates an invoice-only receivable. |
| `ReceiptHandler` | consumer | Reduces receivable outstanding; handles reversals; writes actuals. |
| `RetentionAndClaimHandler` | consumer | Retention release and settled claims become receivables; submitted/approved/rejected variations and submitted claims maintain `exposures`. |
| `SupplierInvoiceHandler` | consumer | Registered/matched/disputed invoices become payables; maintains commitment invoicing. |
| `PaymentStatusHandler` | consumer | `payment.scheduled/executed` (procurement and subcontracts) update payables; write actuals; trade paid-to-date. |
| `CommitmentHandler` | consumer | PO issued/changed/cancelled/closed (and subcontract signed/revised, R2) maintain commitments. |
| `SubcontractPayableHandler` (R2) | consumer | Valuation certified and back-charge raised become payables. |
| `ProjectRefHandler` | consumer | Baseline dates (planning), budget and EAC (cost), project lifecycle into `project_refs`; period close triggers a snapshot. |
| `ForecastItemGenerator` | calculator | Turns schedules, commitments, plans, cheques into dated, confidence-tagged **forecast items** for a scope (formula `cashfc-1`). |
| `ForecastCalculator` | calculator (pure) | Items + settings → weekly and monthly periods, closing balances, conservative balances. No I/O; property-tested. |
| `ShortfallEvaluator` | calculator | Finds first week below floor within the alert horizon; amount; severity; raises/clears alerts; publishes events. |
| `ShortfallExplainer` (R2) | calculator | Identifies the receipts not arriving and the payments leaving between the last healthy peak and the shortfall week. |
| `RecurrenceExpander` | calculator | Expands payroll plans and recurring planned items into dated occurrences. |
| `AgingCalculator` | query | Buckets receivables/payables by days past due and counterparty. |
| `PaymentProposalBuilder` | calculator | Selects payables for a pay date by priority, due date and floor protection. |
| `ScenarioEngine` (R3) | calculator | Applies adjustments to a copy of the live item set and recomputes. |
| `CollectionDateEstimator` (R3) | calculator | Average days-late per client from receipt history; proposes `expected_date`. |
| `StatementMatcher` (R2) | calculator | Suggests matches between bank statement lines and actuals/cheques. |
| `ForecastRefreshJob`, `ForecastNightlyRebuildJob`, `WeeklySnapshotJob`, `OverdueScanJob`, `ChequeDueJob`, `ProposalExpiryJob`, `SnapshotPurgeJob`, `AccuracyJob`, `NameRefreshJob`, `CommitmentConsistencyJob`, `RmRebuildJob` | jobs | Section 9. |
| `ICashQuery` (`Cash.Contracts`) | public contract | `GetProjectCashSummary`, `GetCompanyCashSummary`, `GetNextShortfall`, `GetTradeCash`; used by Reporting (03) so Reporting never reads `cash.*` tables. |
| `ICoreLookup` (`Core.Contracts`, consumed) | contract query | Resolves party and work package names (cached 5 min); names are copied onto rows at event time and refreshed nightly. |

### 3.3 Dependency rules specific to this module

1. Cash **never calls** Procurement, Contracts or Subcontracts synchronously. All inputs arrive as events; all outputs leave as events (00 §2.1).
2. Cash **never changes** a source document (no payment is "paid" by Cash). A payment becomes paid only when Procurement/Subcontracts publish `payment.executed`.
3. The forecast is **derived data**: every number can be traced to `forecast_items` rows, each pointing to its source record (BR-X-10).
4. Money in forecast items is in the tenant base currency; foreign-currency documents are converted at the document's own `fx_rate` (no revaluation in R1).

---

## 4. Domain model

All tables are in schema `cash`. Every table has the standard columns of 00 §3.2 (`id`, `tenant_id`, `created_at/by`, `updated_at/by`, `row_version`, `is_deleted`, `source`) unless marked **append-only** (no `updated_*`, no deletes). Only module-specific columns are listed. `tenant_id` is the first column of every composite index and the RLS key. FKs inside the module are real; references to other modules (`contract_id`, `po_id`, `invoice_id`, `application_id`, `work_package_id`, `cost_code_id`) are **by value, no FK constraint** (00 §5.4). Real FKs to `core.projects`, `core.parties`, `platform.users` are allowed and used as stated. Money is `numeric(18,4)` with `currency`; `fx_rate numeric(18,8)` (1 for base). Business numbers come from `platform.number_series`: `CSH_RCV` (`RCV-0001`), `CSH_PYB` (`PYB-0001`), `CSH_CHQ` (`CHQ-0001`), `CSH_PRP` (`PRP-0001`), `CSH_SCN` (`SCN-0001`).

### 4.1 ER summary

```txt
bank_accounts 1---* cheques *---* (payables | receivables) via cheque_allocations
contract_refs 1---* receivables 1---* receivable_followups
payables ---(by value)--- invoices/valuations of procurement/subcontracts
commitments 1---* commitment_lines ;  commitments 1---* commitment_invoice_links
payroll_plans, planned_items, cheques, receivables, payables, commitment_lines
        \__ ForecastItemGenerator __/
                     |
forecast_settings + assumptions --> forecasts 1---* forecast_periods
                                    forecasts 1---* forecast_items (drill-down)
forecasts (kind=live|snapshot|scenario) *---1 scenarios 1---* scenario_adjustments
forecasts 1---* cash_alerts (by dedupe_key)        payment_proposals 1---* payment_proposal_lines --- payables
cash_actuals (append-only)  bank_statement_imports 1---* bank_statement_lines --- cash_actuals/cheques (R2)
exposures, project_refs, rm_cash_project_summary, rm_trade_cash, rm_forecast_accuracy, rm_company_cash_weekly (R2)
```

### 4.2 Enumerations

| Enum | Values and meaning |
|---|---|
| `receivable.kind` | `certificate` (from a payment application), `invoice_only` (client invoice with no matched certificate), `retention` (retention release), `advance` (advance payment), `claim` (settled claim, R2), `other` (manual) |
| `receivable.status` | `expected` (application submitted, not certified), `certified`, `invoiced`, `partly_received`, `received`, `written_off`, `cancelled` (application rejected or superseded) |
| `payable.kind` | `supplier_invoice`, `supplier_credit_note` (negative), `subcontract_certificate` (R2), `backcharge` (negative, R2), `other_payable` |
| `payable.status` | `registered` (invoice saved, not matched), `matched`, `approved` (approved for payment in Procurement), `scheduled` (in an approved payment batch), `partly_paid`, `paid`, `disputed`, `cancelled` |
| `payable.priority` | `1` critical (site stops, statutory, payroll-linked), `2` high, `3` normal (default), `4` low (can slip) |
| `confidence` | `high`, `medium`, `low` (section 1.2) |
| `date_basis` | `contractual` (contract terms/due date), `override` (user-set), `assumed` (assumption/lag/commitment rule), `predicted` (R3) |
| `cheque.direction` | `issued`, `received` |
| `cheque.status` | `issued`, `received`, `deposited`, `cleared`, `bounced`, `cancelled`, `replaced` |
| `payroll_plan.frequency` | `weekly`, `biweekly`, `monthly` |
| `payroll_plan.status` | `active`, `paused`, `ended` |
| `planned_item.kind` | inflow: `loan_drawdown`, `equity_injection`, `other_inflow`; outflow: `equipment`, `overhead`, `loan_repayment`, `tax`, `insurance`, `rent`, `other_outflow` |
| `planned_item.recurrence` | `once`, `weekly`, `monthly`, `quarterly`, `yearly` |
| `planned_item.status` | `planned`, `realised` (marked done, excluded from forecast), `cancelled` |
| `forecast.kind` | `live` (one per scope, recomputed), `snapshot` (frozen copy), `scenario` (result of a scenario) |
| `forecast.scope` | `project`, `company` (R2) |
| `forecast_item.direction` | `in`, `out` |
| `forecast_item.category` | in: `collection`, `retention`, `advance`, `claim`, `planned_inflow`, `cheque_in`; out: `supplier`, `subcontractor`, `commitment_po`, `commitment_subcontract`, `uncommitted_spend`, `payroll`, `overhead`, `loan`, `tax`, `equipment`, `cheque_out`, `other_outflow` |
| `scenario.status` | `draft`, `computed`, `archived` |
| `scenario_adjustment.type` | `delay_receivable`, `advance_receivable`, `shift_client_collections`, `follow_up_receivable`, `defer_payable`, `bring_forward_payable`, `delay_commitment`, `remove_item`, `add_inflow`, `add_outflow` |
| `payment_proposal.status` | `proposed`, `accepted`, `partly_executed`, `executed`, `discarded`, `expired` |
| `proposal_line.execution_status` | `pending`, `scheduled`, `executed`, `dropped` |
| `alert.rule_code` | `balance_below_floor`, `forecast_at_risk` (conservative case), `payment_due_unapproved`, `receivable_overdue`, `cheque_due_no_funds`, `payment_exceeds_commitment`, `payment_to_blocked_party`, `cheque_bounced`, `receipt_exceeds_receivable`, `guarantee_expiring` |
| `alert.status` | `open`, `acknowledged`, `resolved` |
| `alert.severity` | `info`, `warning`, `high`, `critical` |
| `actual.direction` / `actual.category` | `in`/`out`; `client_receipt`, `retention_receipt`, `advance_receipt`, `supplier_payment`, `subcontract_payment`, `payroll`, `other` |
| `exposure.kind` | `variation_submitted`, `variation_rejected`, `claim_submitted`, `guarantee_commission` (info) |
| `statement_line.match_status` (R2) | `unmatched`, `suggested`, `matched`, `ignored` |

### 4.3 Reference and settings tables

#### `bank_accounts`

| column | type | null | notes |
|---|---|---|---|
| `account_code` | text | no | unique `(tenant_id, account_code)` |
| `name_ar`, `name_en` | text | no/yes | at least one required |
| `bank_party_id` | uuid | yes | FK `core.parties` (kind bank) |
| `account_no_masked` | text | yes | last 4 digits only (hidden from non-finance) |
| `currency` | text | no | CHECK in tenant allowed currencies |
| `account_type` | text | no | `current`, `savings`, `project` (dedicated to a project), `escrow` |
| `project_id` | uuid | yes | FK `core.projects`; set when `account_type='project'` |
| `current_balance_amount` | numeric(18,4) | no | last recorded balance, default 0 |
| `balance_as_of_date` | date | yes |  |
| `include_in_forecast` | bool | no | default true; only these feed company opening balance |
| `is_active` | bool | no | default true |
| Indexes: unique `(tenant_id, account_code)`; `(tenant_id, project_id) where project_id is not null`. |  |  |  |

#### `forecast_settings` (one row per scope; project row overrides company row field by field when not null)

| column | type | null | notes |
|---|---|---|---|
| `scope` | text | no | `project`, `company` |
| `project_id` | uuid | yes | FK `core.projects`; null for company; unique `(tenant_id, scope, project_id)` (company row has null project, enforced with partial unique index) |
| `opening_balance_amount` | numeric(18,4) | yes | project scope: cash allocated to the project at `opening_balance_date`; company scope: manual adjustment added to the sum of included bank accounts (default 0) |
| `opening_balance_date` | date | yes |  |
| `min_balance_amount` | numeric(18,4) | yes | the floor; CHECK `>= 0`; null = inherit; company default 0 |
| `alert_horizon_weeks` | int | yes | default 13; CHECK between 1 and 13 |
| `shortfall_refire_pct` | numeric(7,4) | yes | default 10; a new `shortfall.detected` is published if the shortfall grows by this percentage |
| `shortfall_refire_amount` | numeric(18,4) | yes | default 50,000; or by this amount |
| `vat_rate_pct` | numeric(7,4) | yes | default 14; used for commitment-based outflows (ex-VAT amounts) |
| `default_collection_lag_days` | int | yes | default 0; days added to client due dates |
| `default_payment_lag_days` | int | yes | default 0; days added to supplier due dates (company pays late by habit) |
| `commitment_invoice_lag_days` | int | yes | default 0; days from needed-by date to supplier invoice |
| `cheque_clearing_days` | int | yes | default 2; days from deposit/due date to funds for received cheques |
| `uncertified_haircut_pct` | numeric(7,4) | yes | default 0; reduces submitted (uncertified) applications |
| `conservative_delay_days` | int | yes | default 14; delay applied to medium-confidence inflows in the conservative case |
| `include_uncommitted_spend` | bool | yes | default false (R2): adds budget-remaining-minus-committed spend as low-confidence outflow |
| `overdue_receivable_days` | int | yes | default 7 (BR-CSH-13) |
| `unapproved_due_days` | int | yes | default 3 (BR-CSH-32) |
| `cheque_alert_horizon_days` | int | yes | default 14 (BR-CSH-31) |
| `adjust_to_working_day` | bool | yes | default true (BR-CSH-28) |
| `allow_proposal_unapproved` | bool | yes | default false (BR-CSH-37) |
| `shortfall_notify_roles` | text\[\] | yes | default `{finance_controller, owner_executive}` |
| `payroll_visible_to_pm` | bool | yes | default false: when true PM sees payroll plan names (not amounts per employee; none are stored) |

#### `assumptions`

Typed overrides with validity and a human note, shown next to the forecast (v3: forecasts show assumptions).

| column | type | null | notes |
|---|---|---|---|
| `scope` | text | no | `company`, `project`, `party` |
| `project_id` | uuid | yes | when scope is project |
| `party_id` | uuid | yes | FK `core.parties`; when scope is party (client or supplier) |
| `kind` | text | no | `collection_lag_days`, `payment_lag_days`, `commitment_invoice_lag_days`, `cheque_clearing_days`, `uncertified_haircut_pct`, `custom_note` |
| `value_numeric` | numeric(18,4) | yes |  |
| `valid_from`, `valid_to` | date | no/yes | CHECK `valid_to >= valid_from` |
| `confidence_range_days` | int | yes | plus/minus days shown as the date window of items using this assumption |
| `note` | text | yes |  |
| Resolution order for any parameter: party assumption (valid on the item date) → project assumption → project settings → company assumption → company settings → built-in default. |  |  |  |

#### `contract_refs` (replica fed by `contracts.contract.signed/amended`)

`contract_id uuid` (unique per tenant with `version`), `project_id` (FK `core.projects`), `client_party_id` (FK `core.parties`), `contract_number text`, `contract_value numeric(18,4)`, `currency`, `retention_pct numeric(7,4)`, `advance_pct`, `payment_terms_days int`, `vat_rate_pct`, `completion_date date`, `version int`, `retention_release_eligible_stage text null` (hint set by `closeout.handover.completed`), `last_event_id uuid`.

#### `project_refs` (replica; R1 for status, R2 for budget fields)

`project_id uuid` (PK business key, FK `core.projects`), `project_code`, `status` (core status), `planned_start_date`, `planned_finish_date`, `baseline_version int`, `budget_total_amount`, `budget_version int`, `eac_amount numeric(18,4) null`, `ac_amount numeric(18,4) null`, `cost_as_of date null`, `last_closed_period text null`, `last_event_id`.

### 4.4 Schedules

#### `receivables` (aggregate root `Receivable`)

| column | type | null | notes |
|---|---|---|---|
| `receivable_number` | text | no | `RCV-0001`, unique `(tenant_id, receivable_number)` |
| `project_id` | uuid | no | FK `core.projects` |
| `contract_id` | uuid | yes | by value; null for `other` |
| `client_party_id` | uuid | no | FK `core.parties` |
| `party_name_ar`, `party_name_en` | text | yes | snapshot, refreshed by `NameRefreshJob` |
| `kind` | text | no | enum |
| `status` | text | no | enum, default `expected` |
| `application_id` | uuid | yes | source payment application; unique `(tenant_id, application_id)` where not null |
| `application_number`, `revision_no` | text, int | yes | e.g. `PA-04`, 1 |
| `certificate_id` | uuid | yes | set on certification |
| `invoice_id`, `invoice_number` | uuid, text | yes | set when client invoice issued |
| `release_id` | uuid | yes | retention release or claim id (kind retention/claim); unique where not null |
| `document_number` | text | yes | the number the client recognises (certificate/invoice) |
| `currency`, `fx_rate` | text, numeric | no | document currency and rate |
| `gross_amount` | numeric(18,4) | no | amount owed at creation (payable incl VAT for certificates); CHECK `>= 0` |
| `vat_amount` | numeric(18,4) | no | default 0 |
| `received_amount` | numeric(18,4) | no | default 0; CHECK `received_amount <= gross_amount` unless `has_excess_receipt` |
| `outstanding_amount` | numeric(18,4) | no | `gross_amount - received_amount` (maintained by aggregate, CHECK equals) |
| `issue_date`, `due_date` | date | yes/no | `due_date` = contractual due date |
| `expected_date` | date | no | date the forecast uses (BR-CSH-10) |
| `expected_date_basis` | text | no | `date_basis` enum |
| `expected_date_override_reason` | text | yes | required when basis is `override` |
| `confidence` | text | no | enum, derived from status (BR-CSH-11) |
| `date_window_days` | int | yes | plus/minus window from assumptions |
| `has_excess_receipt` | bool | no | default false |
| `last_follow_up_at` | timestamptz | yes |  |
| `written_off_at`, `written_off_by`, `write_off_reason`, `write_off_approval_id` |  | yes |  |
| `cancelled_reason` | text | yes |  |
| Indexes: `(tenant_id, project_id, status)`; partial `(tenant_id, expected_date) where status in ('expected','certified','invoiced','partly_received')` (open receivables); `(tenant_id, client_party_id, status)`; `(tenant_id, invoice_id)`; keyset `(tenant_id, created_at desc, id desc)`. |  |  |  |
| Checks: `gross_amount >= 0`; `outstanding_amount = gross_amount - received_amount`; `status in (...)`; `fx_rate > 0`. |  |  |  |

#### `receivable_followups`

`receivable_id` (FK), `followed_up_by` (FK `platform.users`), `followed_up_at timestamptz`, `channel` (`call`, `meeting`, `email`, `letter`, `portal`), `contact_name text`, `note text`, `promised_date date null`, `next_follow_up_date date null`. When `promised_date` is set the user may apply it as the override date in the same call (BR-CSH-12).

#### `payables` (aggregate root `Payable`)

| column | type | null | notes |
|---|---|---|---|
| `payable_number` | text | no | `PYB-0001` |
| `project_id` | uuid | yes | FK `core.projects`; null for company overhead invoices |
| `party_id` | uuid | no | FK `core.parties` (supplier or subcontractor) |
| `party_name_ar`, `party_name_en` | text | yes | snapshot |
| `kind`, `status` | text | no | enums; `status` default `registered` |
| `previous_status` | text | yes | stored when entering `disputed` to restore |
| `source_type` | text | no | `procurement_invoice`, `subcontract_valuation`, `subcontract_backcharge`, `manual` |
| `source_id` | uuid | no | invoice/valuation/backcharge id; unique `(tenant_id, source_type, source_id)` |
| `source_number` | text | yes | supplier invoice no. or valuation no. |
| `po_id`, `subcontract_id` | uuid | yes | by value |
| `work_package_id` | uuid | yes | when the whole payable belongs to one work package (invoice lines may carry several: `wp_split jsonb`) |
| `wp_split` | jsonb | yes | `[{workPackageId, netAmount}]` from matched lines, used for the trade view |
| `currency`, `fx_rate` |  | no |  |
| `net_amount`, `vat_amount`, `withholding_amount` | numeric(18,4) | no | withholding default 0; credit notes negative |
| `payable_amount` | numeric(18,4) | no | cash owed = net + VAT - withholding (`netPayableAmount` from the event) |
| `paid_amount` | numeric(18,4) | no | default 0 |
| `outstanding_amount` | numeric(18,4) | no | `payable_amount - paid_amount` |
| `invoice_date`, `due_date` | date | yes/no |  |
| `planned_pay_date` | date | yes | set by finance/procurement via plan (BR-CSH-18) |
| `planned_pay_reason` | text | yes | required when `planned_pay_date > due_date` |
| `priority` | smallint | no | default 3 |
| `is_party_blocked` | bool | no | default false; set by `core.party.blocked` consumer (see 8.4) |
| `dispute_id` | uuid | yes |  |
| `approved_for_payment_at` | timestamptz | yes | from the proposed `procurement.invoice.approved_for_payment.v1` |
| `scheduled_payment_id`, `scheduled_date` | uuid, date | yes | from `payment.scheduled` |
| `exceeds_commitment` | bool | no | default false (BR-CSH-26) |
| `proposed_pay_date` | date | yes | date proposed by procurement, ignored by the forecast until confirmed (BR-CSH-19) |
| `last_event_at` | timestamptz | no | envelope `occurredAt` of the last applied event (BR-CSH-27) |
| Indexes: `(tenant_id, project_id, status)`; partial `(tenant_id, due_date) where status in ('registered','matched','approved','scheduled','partly_paid')`; `(tenant_id, party_id, status)`; `(tenant_id, po_id)`; unique `(tenant_id, source_type, source_id)`. |  |  |  |
| Checks: `outstanding_amount = payable_amount - paid_amount`; `priority between 1 and 4`; `status in (...)`. |  |  |  |

#### `commitments` (aggregate root `Commitment`)

| column | type | null | notes |
| --- | --- | --- | --- |
| `commitment_kind` | text | no | `purchase_order`, `subcontract` |
| `source_id` | uuid | no | po id or subcontract id; unique `(tenant_id, commitment_kind, source_id)` |
| `source_number` | text | no | `PO-1066` |
| `project_id` | uuid | no | FK `core.projects` |
| `party_id` | uuid | no | supplier or subcontractor |
| `party_name_ar`, `party_name_en` | text | yes | snapshot |
| `version_no` | int | no | current PO/subcontract version applied (older events ignored) |
| `status` | text | no | `open`, `closed`, `cancelled` |
| `currency`, `fx_rate` |  | no |  |
| `credit_days` | int | no | from PO payment terms |
| `committed_amount` | numeric(18,4) | no | ex VAT, current version, base-currency amount as in event `baseTotalAmount` (document currency in `committed_doc_amount`) |
| `invoiced_amount` | numeric(18,4) | no | ex VAT: matched line net + unmatched registered net (BR-CSH-24) |
| `paid_ex_vat_amount` | numeric(18,4) | no | from payment allocations |
| `remaining_amount` | numeric(18,4) | no | `max(0, committed - invoiced)`; never negative |
| `spread_method` | text | yes | for subcontracts: `even_monthly_to_completion` |
| `planned_finish_date` | date | yes | subcontracts |
| Separate figures (committed, invoiced, paid) are never summed (BR-X-02); `remaining_amount` is a derived planning figure. |  |  |  |

#### `commitment_lines`

`commitment_id` (FK), `line_no int`, `work_package_id uuid`, `cost_code_id uuid`, `line_amount numeric(18,4)` (ex VAT, base), `invoiced_amount` (matched, ex VAT), `needed_by_date date null`, `remaining_amount` (after FIFO assignment of unmatched amounts, BR-CSH-24). Unique `(commitment_id, line_no)`.

#### `commitment_invoice_links`

Tracks each invoice's contribution so registered → matched does not double count: `commitment_id` (FK), `invoice_id uuid`, `registered_net_amount`, `matched_net_amount`, `state` (`registered`, `matched`, `reversed`). Unique `(commitment_id, invoice_id)`.

#### `exposures`

`project_id`, `contract_id`, `kind` (enum), `source_id uuid`, `source_number`, `amount numeric(18,4)`, `currency`, `status` (`open`, `closed`), `occurred_on date`, `closed_on date null`. Unique `(tenant_id, kind, source_id)`. Used only for the "not forecast" exposure figure (v3 line 221); **never** added to forecast balances.

### 4.5 Plans and cheques

#### `payroll_plans`

| column | type | null | notes |
| --- | --- | --- | --- |
| `name_ar`, `name_en` | text | no/yes |  |
| `project_id` | uuid | yes | null = company payroll |
| `frequency` | text | no | enum |
| `amount` | numeric(18,4) | no | per payment run, CHECK `> 0`, currency in `currency` |
| `currency` | text | no |  |
| `weekday` | smallint | yes | 0-6 (Saturday = 0 under `locale.week_start=sat`) for weekly/biweekly |
| `day_of_month` | smallint | yes | 1-31 for monthly; clamped to month end |
| `anchor_date` | date | yes | first pay date for biweekly |
| `start_date`, `end_date` | date | no/yes | CHECK `end_date >= start_date` |
| `status` | text | no | enum, default `active` |
| `confidence` | text | no | default `high` |
| `note` | text | yes |  |
| Cross-check: exactly one of `weekday`, `day_of_month` populated according to `frequency` (CHECK). |  |  |  |

#### `planned_items`

`direction` (`in`/`out`), `kind` (enum), `project_id uuid null`, `party_id uuid null`, `description_ar/en`, `amount numeric(18,4)` (> 0), `currency`, `fx_rate`, `planned_date date`, `recurrence text` (default `once`), `recurrence_end_date date null`, `confidence` (default `medium`), `status` (enum), `realised_on date null`, `linked_payable_id uuid null` (when the one-off outflow is later invoiced). Indexes `(tenant_id, status, planned_date)`.

#### `cheques` (aggregate root `Cheque`)

| column | type | null | notes |
| --- | --- | --- | --- |
| `register_number` | text | no | `CHQ-0001` |
| `direction` | text | no | `issued`, `received` |
| `cheque_no` | text | no | printed number; unique `(tenant_id, direction, bank_account_id, cheque_no)` for issued; `(tenant_id, drawer_bank_party_id, cheque_no)` for received |
| `bank_account_id` | uuid | yes | FK `bank_accounts`; issuing account (issued) or depositing account (received) |
| `drawer_bank_party_id` | uuid | yes | FK `core.parties` (bank of the drawer, received cheques) |
| `counterparty_id` | uuid | no | FK `core.parties`: payee (issued) or drawer (received) |
| `project_id` | uuid | yes |  |
| `amount`, `currency` |  | no | amount > 0 |
| `issue_date`, `due_date` | date | no | post-dated when `due_date > issue_date`; CHECK `due_date >= issue_date` |
| `status` | text | no | enum |
| `deposited_on`, `cleared_on`, `bounced_on` | date | yes |  |
| `bounce_reason_code` | text | yes | `insufficient_funds`, `signature`, `stopped`, `account_closed`, `other` |
| `replaced_by_id` | uuid | yes | FK `cheques` |
| `note` | text | yes |  |
| Indexes: partial `(tenant_id, due_date) where status in ('issued','received','deposited')`; `(tenant_id, counterparty_id)`. |  |  |  |

#### `cheque_allocations`

`cheque_id` (FK), `payable_id uuid null` (FK `payables`), `receivable_id uuid null` (FK `receivables`), `amount numeric(18,4)`; CHECK exactly one of the two ids; CHECK direction consistency (issued → payable). Sum of allocations ≤ cheque amount; a payable's open-cheque allocations ≤ its outstanding (BR-CSH-35).

### 4.6 Forecast, scenarios, alerts, proposals, actuals

#### `forecasts` (aggregate root `CashForecast`)

| column | type | null | notes |
| --- | --- | --- | --- |
| `kind` | text | no | `live`, `snapshot`, `scenario` |
| `scope` | text | no | `project`, `company` |
| `project_id` | uuid | yes | FK `core.projects`; null for company |
| `scenario_id` | uuid | yes | FK `scenarios`; kind `scenario` only |
| `snapshot_reason` | text | yes | `weekly`, `period_close`, `manual`, `proposal` |
| `source_forecast_id` | uuid | yes | live forecast a snapshot/scenario was copied from |
| `as_of_date` | date | no | forecast date |
| `week1_start_date` | date | no | BR-CSH-01 |
| `horizon_weeks` | int | no | 13 |
| `opening_balance_amount` | numeric(18,4) | no |  |
| `floor_amount` | numeric(18,4) | no |  |
| `currency` | text | no | base currency |
| `min_balance_amount` | numeric(18,4) | no | minimum weekly closing balance in the alert horizon |
| `min_balance_week_no` | int | no |  |
| `closing_balance_week13_amount` | numeric(18,4) | no |  |
| `has_shortfall` | bool | no |  |
| `first_shortfall_week_no` | int | yes |  |
| `shortfall_amount` | numeric(18,4) | no | default 0 (BR-CSH-04) |
| `conservative_min_balance_amount` | numeric(18,4) | no |  |
| `conservative_min_week_no` | int | yes |  |
| `formula_version` | text | no | `cashfc-1` (BR-X-10) |
| `inputs_snapshot` | jsonb | no | effective settings, assumptions used, source counts |
| `input_hash` | text | no | sha256 over items + inputs; unchanged hash means no new `refreshed` event |
| `is_dirty` | bool | no | live only; set by handlers |
| `dirty_since` | timestamptz | yes |  |
| `computed_at` | timestamptz | no |  |
| `computed_by` | uuid | yes | null for job |
| Unique partial index: one live row per scope: `(tenant_id, scope, coalesce(project_id,'00000000-0000-0000-0000-000000000000')) where kind='live'`. Index `(tenant_id, scope, project_id, kind, as_of_date desc)`. |  |  |  |

#### `forecast_periods`

`forecast_id` (FK, cascade on purge), `period_type` (`week`, `month`), `period_no int` (1..13 for weeks, 1..12 for months), `start_date`, `end_date`, `opening_amount`, `inflow_amount`, `outflow_amount`, `closing_amount`, `floor_amount`, `is_below_floor bool`, `conservative_inflow_amount`, `conservative_closing_amount`, `conservative_below_floor bool`, `inflow_by_category jsonb`, `outflow_by_category jsonb`, `item_count int`. Unique `(forecast_id, period_type, period_no)`. CHECK `closing_amount = opening_amount + inflow_amount - outflow_amount`.

#### `forecast_items` (the drill-down: every receipt and payment behind a number)

| column | type | null | notes |
|---|---|---|---|
| `forecast_id` | uuid | no | FK `forecasts` |
| `direction` | text | no | `in`, `out` |
| `category` | text | no | enum |
| `source_type` | text | no | `receivable`, `payable`, `commitment_line`, `payroll_plan`, `planned_item`, `cheque`, `uncommitted_budget`, `scenario_adjustment` |
| `source_id` | uuid | no | row in the module's table |
| `source_ref` | text | yes | human reference (`PA-04`, `INV-8841`, `PO-1066`) |
| `project_id` | uuid | yes |  |
| `party_id` | uuid | yes |  |
| `party_name_ar`, `party_name_en` | text | yes | snapshot |
| `work_package_id` | uuid | yes | trade attribution |
| `planned_date` | date | no | date used (after rules/scenario) |
| `base_date` | date | no | date before scenario adjustment (equals `planned_date` outside scenarios) |
| `window_from`, `window_to` | date | yes | confidence window |
| `period_no` | int | yes | week number 1..13, null beyond |
| `month_no` | int | yes | 1..12, null beyond |
| `amount_base` | numeric(18,4) | no | > 0 always; sign is the direction (credit notes reduce `out` items as `in`? no: credit notes are netted into the same payable list as negative outflow, see BR-CSH-20) |
| `original_amount`, `currency`, `fx_rate` |  | no |  |
| `confidence` | text | no | enum |
| `date_basis` | text | no | enum |
| `is_overdue_rolled` | bool | no | planned date before week 1, shown in week 1 |
| `is_adjusted` | bool | no | changed by a scenario adjustment |
| `reason_code` | text | no | why this item exists: `certified_due`, `submitted_expected`, `invoice_due`, `unmatched_invoice_due`, `po_remaining`, `payroll_run`, `planned_item`, `cheque_due`, `uncommitted_budget` |
| Indexes: `(tenant_id, forecast_id, period_no, direction)`; `(tenant_id, forecast_id, source_type, source_id)`; `(tenant_id, forecast_id, planned_date)`. |  |  |  |
| Amount rule: `amount_base` is positive; a negative payable (credit note, back-charge) appears as an `in`-direction item with category `supplier`/`subcontractor` only when it is **unapplied** (no payable to net against); otherwise it is netted inside the party's payable amount (BR-CSH-20). |  |  |  |

#### `scenarios`

`scenario_number` (`SCN-0001`), `scope`, `project_id uuid null`, `name_ar`, `name_en`, `description text`, `source_forecast_id uuid` (live forecast at compute time), `status`, `result_forecast_id uuid null` (FK `forecasts`, kind `scenario`), `computed_at`, `base_min_balance_amount`, `scenario_min_balance_amount`, `delta_min_balance_amount`, `base_first_shortfall_week_no int null`, `scenario_first_shortfall_week_no int null`, `archived_at null`.

#### `scenario_adjustments`

| column | type | null | notes |
|---|---|---|---|
| `scenario_id` | uuid | no | FK |
| `line_no` | int | no | unique with scenario |
| `type` | text | no | enum |
| `target_type` | text | yes | `receivable`, `payable`, `commitment`, `commitment_line`, `planned_item`, `payroll_plan`, `cheque`, `client` (all open receivables of one client), `forecast_item` |
| `target_id` | uuid | yes |  |
| `shift_days` | int | yes | positive = later, negative = earlier |
| `new_date` | date | yes | alternative to `shift_days` |
| `amount_override` | numeric(18,4) | yes | used for partial amount (defer part of a payable) |
| `moved_amount` | numeric(18,4) | yes | for partial defer/bring-forward: portion moved |
| `one_off_date`, `one_off_amount` | date, numeric | yes | for `add_inflow/add_outflow` |
| `note` | text | yes |  |
| CHECK: `shift_days` xor `new_date` for move types; `moved_amount <= target outstanding`. |  |  |  |

#### `payment_proposals` (aggregate root `PaymentProposal`)

`proposal_number` (`PRP-0001`), `scope` (`company`, `project`), `project_id null`, `pay_date date`, `status`, `strategy` (`priority_then_due`, `oldest_first`), `available_cash_amount numeric(18,4)` (result of floor protection), `cash_cap_amount numeric(18,4) null` (user input), `lookahead_days int` (default 7), `allow_partial bool`, `min_partial_amount numeric(18,4)`, `total_proposed_amount`, `total_deferred_amount`, `forecast_id` (live forecast used), `floor_protection_applied bool`, `accepted_at/by`, `handover_event_id uuid null`, `expires_at timestamptz`. <br> `payment_proposal_lines`: `proposal_id` (FK), `line_no`, `payable_id` (FK `payables`), `party_id`, `project_id`, `due_date`, `priority`, `outstanding_amount`, `proposed_amount`, `decision` (`include`, `defer`), `defer_reason` (`cash_cap`, `floor_protection`, `blocked_party`, `disputed`, `not_approved`, `user`), `user_override bool`, `execution_status`, `payment_id uuid null`, `executed_date null`. Unique `(proposal_id, payable_id)`.

#### `cash_alerts` (aggregate root `CashAlert`)

`rule_code`, `severity`, `status`, `scope`, `project_id null`, `dedupe_key text` (unique `(tenant_id, rule_code, dedupe_key)` while status in open/acknowledged: partial unique), `subject_type`, `subject_id uuid null`, `message_code`, `message_params jsonb`, `first_raised_at`, `last_raised_at`, `acknowledged_by/at/note`, `resolved_at`, `resolved_reason` (`condition_cleared`, `manual`, `superseded`), `forecast_id uuid null`, `notify_roles text[]`.

#### `cash_actuals` (append-only)

`direction`, `category`, `project_id null`, `party_id null`, `work_package_id null`, `amount numeric(18,4)` (positive; reversal rows set `is_reversal` and reference `reverses_actual_id`), `currency`, `fx_rate`, `value_date date`, `source_type` (`receipt`, `payment`, `subcontract_payment`, `manual`), `source_id uuid`, `source_line_key text` (e.g. allocation index), `reference text`, `bank_account_id uuid null`, `is_reversal bool`, `reverses_actual_id uuid null`, `event_id uuid`. Unique `(tenant_id, source_type, source_id, source_line_key, is_reversal)`. Used only for reports (receipts and payments, forecast accuracy, trade paid-to-date); never for the opening balance.

#### Reconciliation and export (R2)

`bank_statement_imports`: `bank_account_id`, `file_id` (platform file), `period_from`, `period_to`, `opening_balance_amount`, `closing_balance_amount`, `line_count`, `matched_count`, `status` (`uploaded`, `parsed`, `reviewing`, `confirmed`, `failed`), `import_batch_id`. `bank_statement_lines`: `import_id` (FK), `line_no`, `value_date`, `description`, `reference`, `debit_amount`, `credit_amount`, `balance_amount`, `match_status`, `matched_actual_id uuid null`, `matched_cheque_id uuid null`, `match_score numeric(5,2) null`, `matched_by`, `matched_at`. Unique `(import_id, line_no)`; duplicate-line guard unique `(tenant_id, bank_account_id, value_date, reference, debit_amount, credit_amount)` where reference not null. <br> `accounting_export_batches`: `export_type` (`accounting_journal`, `eta_invoice_data`), `period_from`, `period_to`, `format` (`xlsx`, `csv`), `status` (`queued`, `running`, `done`, `failed`), `file_id`, `row_count`, `parameters jsonb`, `requested_by`.

### 4.7 Read-model tables (`rm_*`, section 13 has refresh rules)

`rm_cash_project_summary`, `rm_trade_cash`, `rm_forecast_accuracy`, `rm_company_cash_weekly` (R2). Columns in 13.1.

---

## 5. State machines

Illegal transitions return `409 cash.invalid_state_transition` (00 §2.3). Status changes are written to `platform.status_history`.

### 5.1 Receivable

| from | action (trigger) | to | guard | who | side effects |
| --- | --- | --- | --- | --- | --- |
| (none) | `payment_application.submitted` (event) | `expected` | no receivable for `application_id`, or revision higher than stored | system | create; confidence `medium`; mark live forecast dirty |
| `expected` | `payment_application.submitted` (higher `revisionNo`) | `expected` | revision higher | system | replace amounts and date; `revision_no` updated |
| `expected` | `payment_application.certified` (event) | `certified` | amounts present | system | amount := `certifiedPayable`; `due_date` := event `dueDate`; confidence `high`; recompute expected date |
| (none) | `payment_application.certified` (no prior expected row) | `certified` |  | system | create directly |
| `certified` | `invoice_to_client.issued` (event, matched by BR-CSH-08) | `invoiced` | amount equals outstanding within 0.01, or applicationId present | system | set `invoice_id`, `invoice_number`; due date from invoice if different (BR-CSH-09) |
| `certified`, `invoiced`, `partly_received` | `receipt.recorded` (allocation) | `partly_received` / `received` | outstanding after > 0 / = 0 | system | actual written; `alert receivable_overdue` resolved when received |
| `received` / `partly_received` | `receipt.recorded` reversal | back to `partly_received`/`invoiced` |  | system | outstanding restored; actual reversed |
| `expected` | `payment_application` withdrawn/rejected (status change surfaced by supersede or `cancel` via event `payment_application.submitted` with lower amount cannot cancel; cancellation arrives with `platform.approval.rejected` consumed by Contracts and a new revision) | `cancelled` | stale > 90 days after expected date and no certificate | finance\_controller (manual `cancel`) | removed from forecast |
| `certified`, `invoiced`, `partly_received` | `write-off` | `written_off` | outstanding > 0, reason, approval granted (`cash.receivable_write_off`) | finance\_controller requests; approver by amount | removed from forecast; audit; notification |
| any open | `override-date` / `reset-date` | same | reason required for override | finance\_controller | confidence unchanged; basis `override` |

### 5.2 Payable

| from | action (trigger) | to | guard | who | side effects |
| --- | --- | --- | --- | --- | --- |
| (none) | `invoice.registered` | `registered` |  | system | create; commitment link `registered` |
| `registered` | `invoice.matched` | `matched` |  | system | payable fields from matched event; commitment link `matched`; `wp_split` stored |
| (none) | `invoice.matched` (no prior registered) | `matched` |  | system | create directly |
| `matched` | `approved_for_payment` (proposed event) | `approved` |  | system | `approved_for_payment_at` |
| `matched`, `approved` | `payment.scheduled` | `scheduled` | allocation includes this payable | system | `scheduled_payment_id`, `scheduled_date`; proposal line `scheduled` |
| `scheduled`, `approved`, `matched` | `payment.executed` | `partly_paid` / `paid` | `paid_amount + cashAmount` vs payable | system | actual written; commitment `paid_ex_vat_amount`; reversal restores |
| `registered`, `matched`, `approved`, `scheduled` | `invoice.disputed` | `disputed` |  | system | `previous_status` stored; excluded from forecast and proposals |
| `disputed` | `invoice.matched` (resolution) | `matched` |  | system | restore |
| any non-paid | invoice cancelled/rejected (credit note full) | `cancelled` |  | system | removed from forecast |
| any open | `plan` (priority, planned pay date) | same | reason when pushing beyond due date | finance\_controller; procurement\_officer (priority and proposed date only) | audit; dirty |

### 5.3 Cheque

Issued cheques: `issued -> cleared | bounced | cancelled | replaced`. Received cheques: `received -> deposited -> cleared | bounced`; `received -> cancelled`.

| from | action | to | guard | who | side effects |
| --- | --- | --- | --- | --- | --- |
| (none) | `register` | `issued` / `received` |  | finance\_controller | forecast item at `due_date` (issued) or `due_date + clearing days` (received, not deposited) |
| `received` | `deposit` | `deposited` | `today >= due_date` unless `allow_early_deposit` flag with reason | finance\_controller | `deposited_on`; forecast date := `deposit date + cheque_clearing_days` |
| `issued`, `deposited` | `clear` | `cleared` |  | finance\_controller (or R2 statement match) | `cleared_on`; removed from forecast; bank balance not changed automatically |
| `issued`, `deposited` | `bounce` | `bounced` | reason code | finance\_controller | issued: payable outstanding restored (allocation dropped); received: receivable outstanding restored; alert `cheque_bounced` (critical) |
| `issued` | `cancel` | `cancelled` | not cleared | finance\_controller | removed from forecast; allocation dropped |
| `issued`, `bounced` | `replace` | `replaced` | new cheque id | finance\_controller | new cheque created with allocations copied |

### 5.4 Forecast (`CashForecast`)

`live` rows are recomputed in place (no states). Snapshots and scenario results are immutable. Lifecycle of the dirty flag: `clean -> dirty` (handler or plan change) `-> computing -> clean` (job). A refresh that produces an identical `input_hash` only updates `computed_at`.

### 5.5 Payment proposal

| from | action | to | guard | who | side effects |
| --- | --- | --- | --- | --- | --- |
| (none) | `generate` | `proposed` | live forecast exists for scope | finance\_controller | lines built by `PaymentProposalBuilder`; `expires_at = now + 7 days` |
| `proposed` | edit lines (include/defer) | `proposed` | line belongs to proposal | finance\_controller | totals and floor check recomputed |
| `proposed` | `accept` | `accepted` | at least one `include` line; floor check passes or `acknowledge_floor_breach` with reason | finance\_controller | publishes proposed `cash.payment_proposal.accepted.v1`; snapshot forecast `proposal` |
| `accepted` | payments scheduled/executed (events) | `partly_executed` / `executed` | all `include` lines executed -> `executed` | system | line `execution_status` updated |
| `proposed` | `discard` | `discarded` |  | finance\_controller |  |
| `proposed`, `accepted` (no scheduled lines) | `ProposalExpiryJob` | `expired` | `expires_at` passed | system |  |

Approval of the actual payment authority is the Procurement `payment_batch` (06 section 5.9) or the Subcontracts payment approval, **not** this proposal.

### 5.6 Scenario (R3), alert, payroll plan, planned item, bank statement import (R2)

| entity | transitions |
|---|---|
| Scenario | `draft -> computed` (`compute`); `computed -> draft` (edit adjustments, discards result); `draft/computed -> archived` |
| Alert | `open -> acknowledged` (user, note) ; `open/acknowledged -> resolved` (system when condition clears, or manual); `resolved` never reopens: a recurrence creates a new row |
| Payroll plan | `active <-> paused`; `active/paused -> ended` |
| Planned item | `planned -> realised`; `planned -> cancelled` |
| Statement import | `uploaded -> parsed -> reviewing -> confirmed`; any -> `failed` |

---

## 6. Business rules

Rule ids are `BR-CSH-NN`. "Item" means a row of `cash.forecast_items`. All amounts are base currency (EGP) unless stated. Cross-module rules BR-X-01 to BR-X-10 (00 §11) also apply.

### 6.1 Forecast mechanics

| ID | Rule |
|---|---|
| BR-CSH-01 | **Week boundaries.** Week 1 starts on the most recent `locale.week_start` day (default Saturday) on or before `as_of_date`; week n covers `week1_start + 7(n-1)` to `week1_start + 7n - 1`. Horizon is 13 weeks (91 days). The monthly view has 12 calendar months starting with the month of `as_of_date`; month 1 is the full calendar month, not a 30-day block. Example: `as_of_date` Saturday 2026-10-10 gives week 1 = 2026-10-10..16, week 5 = 2026-11-07..13, week 9 = 2026-12-05..11, week 13 = 2027-01-02..08. |
| BR-CSH-02 | **What enters the forecast.** Only open items: receivables in `expected`, `certified`, `invoiced`, `partly_received` (amount = `outstanding_amount`); payables in `registered`, `matched`, `approved`, `scheduled`, `partly_paid` (amount = `outstanding_amount`), never `disputed` or `cancelled`; commitment lines with `remaining_amount > 0` on `open` commitments; active payroll plan occurrences; `planned` planned items; cheques in `issued`, `received`, `deposited` that are not already represented by an allocation (BR-CSH-29). Realised, cleared, received, paid, cancelled and written-off records never appear. |
| BR-CSH-03 | **Balance formula.** For each week: `closing = opening + inflow - outflow`, `opening(week 1) = opening balance (BR-CSH-06)`, `opening(week n) = closing(week n-1)`. Inflow and outflow are sums of item `amount_base` by `direction` for items whose `planned_date` falls in the week (plus overdue items rolled into week 1, BR-CSH-13). The same identity holds for months. `forecast_periods` has a CHECK on it. |
| BR-CSH-04 | **Floor and shortfall.** Let `floor` be the effective `min_balance_amount` (project row, else company row, else 0). Within the first `alert_horizon_weeks` weeks (default 13) a week is **below floor** when `closing < floor` (strictly). `min_balance = min(closing)` over the horizon, `min_balance_week_no` the earliest week reaching it. `has_shortfall = min_balance < floor`; `first_shortfall_week_no` = the earliest week below floor (not necessarily the minimum week); `shortfall_amount = floor - min_balance` when `has_shortfall`, else 0. The balance may still be positive during a shortfall. |
| BR-CSH-05 | **Conservative case.** Computed in the same pass: every `medium` and `low` confidence inflow is moved later by `conservative_delay_days` (default 14, assumption-aware); outflows and `high` inflows keep their dates. `conservative_min_balance_amount` and week are stored. If the base case is healthy but the conservative case is below floor, alert `forecast_at_risk` (severity `warning`) is raised; this never publishes `cash.shortfall.detected`. |
| BR-CSH-06 | **Opening balance.** Project scope: `forecast_settings.opening_balance_amount` of the project row (cash allocated to the project); if null, the balance of active `bank_accounts` with `project_id` = project and `include_in_forecast`; else 0 with a `warnings[]` entry `cash.opening_balance_missing`. Company scope (R2): sum of `current_balance_amount` of active accounts with `include_in_forecast` (converted at the latest rate in `core` currency table) plus the company row adjustment. A bank balance is a manual entry (`PATCH /bank-accounts/{id}/balance`) or, in R2, the closing balance of a confirmed statement; the module never reads a bank. |
| BR-CSH-07 | **Currency.** Items are stored in base currency using the source document's `fx_rate` (no revaluation in R1). Documents in USD/SAR/EUR keep `original_amount`, `currency`, `fx_rate`; `amount_base = round(original_amount x fx_rate, 4)`. Bank accounts in foreign currency are converted for the company opening balance only. |
| BR-CSH-13 | **Overdue items.** An open item with `planned_date < week1_start_date` is not lost: it is placed in week 1 with `is_overdue_rolled = true` and `planned_date` kept in `base_date`. Overdue **receivables** roll to week 1 only if `expected_date >= as_of_date`; a receivable whose expected date has passed is moved to `as_of_date + overdue_assumption_days` (`overdue_receivable_days`, default 7, in settings) with basis `assumed`, so an overdue client never produces a phantom receipt in the past. Overdue payables roll to week 1 unchanged (they are owed now). |
| BR-CSH-34 | **Refresh triggers and debounce.** Any change to a source schedule, plan, cheque, setting, assumption or `project_refs` sets `is_dirty` on the affected live forecasts (project forecast for the project; company forecast too, R2). `ForecastRefreshJob` recomputes dirty forecasts at most every 60 seconds per forecast (debounce). A user can force `POST /forecasts/{id}/refresh` (synchronous, max once per 10 seconds per forecast, else `429`). The recomputation is a pure function of stored inputs (BR-X-10); if `input_hash` is unchanged only `computed_at` moves and no event is published. |

### 6.2 Receivables

| ID | Rule |
|---|---|
| BR-CSH-08 | **Client invoice linking.** `contracts.invoice_to_client.issued` is matched to a receivable in this order: (1) event carries `applicationId` (proposed payload addition, section 8.4) and the receivable has that `application_id`; (2) same `contract_id`, status `certified`, and `abs(totalAmount - outstanding_amount) <= 0.01`; (3) otherwise create a receivable of kind `invoice_only` with the invoice amount. Two receivables are never linked to one invoice (unique `(tenant_id, invoice_id)` where not null). |
| BR-CSH-09 | **Due date.** `due_date` of a certificate receivable is the `dueDate` of `payment_application.certified` (certification date + `payment_terms_days` from the contract, computed by Contracts). When the client invoice arrives with a different `dueDate`, the invoice date wins and the change is logged in `status_history` with reason `due_date_from_invoice`. For an expected (uncertified) receivable the due date is `expectedCertificationDate + payment_terms_days` from `contract_refs`. |
| BR-CSH-10 | **Expected date (the date the forecast uses).** Resolution, first match wins: (1) `override` set by a user (`expected_date_basis='override'`, reason required); (2) predicted date (R3, `CollectionDateEstimator`, only when accepted by the user or the tenant enabled auto-use) with basis `predicted`; (3) `due_date + collection_lag_days`, where lag comes from the assumption resolution order of section 4.3 (party, project, settings), basis `assumed` when lag is non-zero else `contractual`. The override survives later recalculation, but is cleared (`reset-date`) automatically when the status moves from `expected` to `certified` (a certificate replaces the guess) unless the user ticked `keep_on_certify`. |
| BR-CSH-11 | **Confidence.** `expected` -> `medium`; `certified`, `invoiced`, `partly_received` -> `high` when the date basis is `contractual`, `medium` when `assumed`, `override` or `predicted`; retention receivables -> `low` until `contracts.retention.released.v1` has `status='approved'`, then `medium`; claims -> `low`. Cheques received -> `high` once `deposited`, else `medium`. Payables from matched invoices -> `high`; unmatched registered invoices -> `medium`; PO commitments -> `low`; payroll plans default `high`. Confidence is shown in every API response with the date window, never hidden. |
| BR-CSH-12 | **Follow-up and promised date.** A follow-up note may carry `promisedDate`. If the request sets `applyAsExpectedDate=true`, the receivable gets `expected_date = promisedDate`, basis `override`, reason `client_promise` in the same transaction. The promised date is never applied silently. `last_follow_up_at` is updated. A follow-up on a `received`, `written_off` or `cancelled` receivable is rejected (`cash.receivable_not_open`). |
| BR-CSH-14 | **Receivable amount.** A certificate receivable `gross_amount = certifiedPayable` (net of retention, advance recovery and deductions, plus VAT) from the event, never recomputed here. `expected` receivables use `payableAmount` of the submitted application reduced by `uncertified_haircut_pct`: `gross = round(payableAmount x (1 - haircut/100), 4)`. **Golden:** payment application 4: certified payable **2,352,590** (09 section 6: 2,445,500 gross, less 244,550 advance recovery, 122,275 retention, 15,000 deductions = 2,063,675 net; VAT 14% = 288,915; payable 2,352,590). The receivable shows that exact figure and contract number `PA-04`. |
| BR-CSH-15 | **Retention and claims.** `contracts.retention.released.v1` creates a receivable of kind `retention` with `gross_amount = approvedAmount`, `expected_date = expectedReceiptDate`, keyed by `release_id`; a second event for the same `releaseId` with `status='collected'` is ignored here (the receipt arrives separately). `contracts.claim.settled.v1` creates a `claim` receivable with `settledAmount` (R2). Retention is never forecast before an approved release event: held retention sits in Contracts, not in cash. |
| BR-CSH-16 | **Receipts.** `contracts.receipt.recorded.v1` allocations reduce `outstanding_amount` of the receivable whose `invoice_id` matches; a receipt without matching allocation reduces the oldest open `certified`/`invoiced` receivable of the same `contract_id` (FIFO by due date). Receipt larger than outstanding sets `has_excess_receipt` and raises alert `receipt_exceeds_receivable` (high); the excess is not carried forward. A reversal (`isReversal=true`) restores outstanding and reverses the actual (BR-X-04). Every receipt writes a `cash_actuals` row (never an update to opening balance). |
| BR-CSH-17 | **Write-off.** Write-off needs `reason`, goes through the platform approval (`record_type='cash.receivable_write_off'`, routed by amount, BR-X-07). On approval: status `written_off`, item removed, audit entry, `owner_executive` notified if above the tenant threshold. A written-off receivable that is later paid is not reopened: the receipt is recorded as `invoice_only`/`other` receivable with a warning. |

### 6.3 Payables, commitments and payments

| ID | Rule |
|---|---|
| BR-CSH-18 | **Payable date.** `pay_date_used = planned_pay_date` if set, else `due_date + payment_lag_days` (assumption resolution order, party first). `planned_pay_date > due_date` requires `planned_pay_reason`; `planned_pay_date < today` is rejected. Confidence follows BR-CSH-11. A `scheduled` payable uses `scheduled_date` from the payment event (a scheduled payment overrides the plan). |
| BR-CSH-19 | **Priority.** `priority` 1..4, default 3. Priority 1 payables are never proposed for deferral by the proposal builder (BR-CSH-37) and never moved by scenario `defer_payable` without an explicit flag `include_critical=true`. A `procurement_officer` may set priority and propose a pay date but the date becomes effective only after a finance user confirms (`plan` action with `confirm=true`); until then it is stored as `proposed_pay_date` and ignored by the forecast. |
| BR-CSH-20 | **Credit notes and back-charges.** A negative `invoice.matched` (credit note) or `backcharge.raised` is stored as a payable of kind `supplier_credit_note`/`backcharge` with negative `payable_amount`. In the forecast: if the party has an open payable on the same project with outstanding >= credit, the credit is **netted** into that payable item (the item amount reduces; a `reason_code` of `credit_netted` is noted in `inputs_snapshot`); otherwise it appears as an `in` item with category `supplier`/`subcontractor`, confidence `low`, dated at `due_date`. A forecast item amount is always positive. |
| BR-CSH-21 | **Disputes.** `procurement.invoice.disputed.v1` sets status `disputed`, stores `previous_status`, and **removes the whole payable** from the forecast and from proposals (the disputed part is not separable in R1; `disputedAmount` is shown on the payable). A later `matched` event for the same invoice restores it. The undisputed remainder is a manual planned item if the finance controller wants to pay it. |
| BR-CSH-22 | **Blocked party.** A `core.party.blocked.v1` event sets `is_party_blocked` on all open payables of the party (scope `all` or `procurement`/`subcontracts` matching the payable source). Blocked payables stay in the forecast (the liability exists) with a flag, are excluded from proposals (`defer_reason='blocked_party'`), and alert `payment_to_blocked_party` is raised when a `payment.scheduled` event names a blocked party. |
| BR-CSH-23 | **Payment events.** `procurement.payment.scheduled.v1` sets `scheduled` on the allocated payables with `scheduled_date`; `payment.executed.v1` adds `cashAmount` per allocation to `paid_amount` (`paid` when `outstanding = 0`, else `partly_paid`), writes one `cash_actuals` row per allocation (`source_line_key = invoiceId`), and updates `commitments.paid_ex_vat_amount` with `paidExVatAmount`. A reversal (`reversal=true`, negative amount) restores the payable and reverses the actual. Subcontract payments do the same on subcontract payables (R2). |
| BR-CSH-24 | **Commitment remaining and invoicing.** `remaining_amount(commitment) = max(0, committed_amount - invoiced_amount)` ex VAT. `invoiced_amount` = sum over `commitment_invoice_links` of `matched_net_amount` for state `matched` plus `registered_net_amount` for state `registered`, so an invoice registered and then matched counts once. Line allocation: matched invoice lines carry `poLineNo` and reduce that line; registered (unmatched) invoices and lines without `poLineNo` reduce lines FIFO by `line_no`. Over-invoicing never makes remaining negative. Cancelled and closed commitments have remaining 0 (a closed PO releases `releasedBaseAmount`). |
| BR-CSH-25 | **Commitment date and amount.** For each line with `remaining_amount > 0`: `item.amount = round(remaining x (1 + vat_rate_pct/100), 4)` (cash includes VAT), `item.planned_date = max(needed_by_date or commitment issue date, as_of_date) + commitment_invoice_lag_days + credit_days`. Category `commitment_po`, confidence `low`, `reason_code='po_remaining'`. **Worked example:** PO-1066 cement, committed 179,340 ex VAT, nothing invoiced, needed by 2026-10-20, credit days 30, lag 0, VAT 14%: item 204,447.60 on 2026-11-19 (week 6). After an invoice of 100,000 ex VAT is registered the item becomes 79,340 x 1.14 = 90,447.60 and the new payable (114,000 payable) is dated by its own due date. Subcontract commitments (R2) spread `remaining` evenly per month from today to `planned_finish_date` (`even_monthly_to_completion`) with `credit_days` of the subcontract payment term. |
| BR-CSH-26 | **Payment above commitment.** When a payable's cumulative `net_amount` for a PO exceeds `committed_amount` (using the latest applied version), `exceeds_commitment = true`, alert `payment_exceeds_commitment` (warning) is raised, and the payable still enters the forecast (money is owed). Procurement owns the blocking decision; Cash only warns. |
| BR-CSH-27 | **Versions and out-of-order events.** Commitments apply an event only if its `versionNo` is higher than `version_no`; payables and receivables apply an event only if its envelope `occurredAt` is not older than the row's `last_event_at` (receivables carry the same column as payables). Older events are acknowledged and ignored. A `purchase_order.changed` for an unknown PO creates the commitment from the event's full line set (the payload carries it). |

### 6.4 Plans and cheques

| ID | Rule |
|---|---|
| BR-CSH-28 | **Payroll and recurring items.** `RecurrenceExpander` produces dated occurrences inside `[as_of_date, as_of_date + 91 days]` (and 12 months for the monthly view): weekly on `weekday`; biweekly from `anchor_date` every 14 days; monthly on `day_of_month`, clamped to the last day of short months (31 becomes 28/30), moved **earlier** to the previous Thursday if it falls on a Friday or Saturday (`locale` weekend) when `adjust_to_working_day` is on. Items outside `start_date..end_date` are skipped; `paused` and `ended` plans produce nothing. Planned items of recurrence `once` appear once on `planned_date` (rolled to week 1 if overdue and still `planned`). **Golden:** weekly payroll 150,000 every Saturday-week gives 13 items; overhead 100,000 on the 25th of each month falls in weeks 3, 7 and 11. |
| BR-CSH-29 | **Cheques in the forecast and no double counting.** An **issued** cheque is an outflow on `due_date` (it leaves the bank when presented). If the cheque has allocations to payables, the payable item is **replaced** by the cheque item for the allocated amount (the cheque date wins over the due date) and the unallocated remainder stays on the payable. A **received** cheque is an inflow on `due_date + cheque_clearing_days` while `received`, and on `deposited_on + cheque_clearing_days` once deposited; allocations to receivables replace the receivable item for that amount the same way. A cheque with no allocation is a stand-alone item (category `cheque_out`/`cheque_in`). A `bounced` cheque disappears from the forecast and restores the payable/receivable outstanding (it comes back as an ordinary item). |
| BR-CSH-30 | **Post-dated cheque rules.** `due_date >= issue_date`. `deposit` before `due_date` is rejected (`cash.cheque_not_yet_due`) unless the user supplies `allowEarly=true` and a reason (audited). A received cheque still `received` 7 days after its due date creates a task `cheque_deposit_due` for `finance_controller` (task inbox, no alert). |
| BR-CSH-31 | **Cheque due without funds.** For each issued cheque due within `alert_horizon_days` (default 14), if the forecast **closing balance of the week containing the due date is below the cheque amount**, or below `floor` after the cheque, raise `cheque_due_no_funds` (critical when the week closing balance is negative, high otherwise). Dedupe key `chq:{chequeId}`. Bounce of an issued cheque raises `cheque_bounced` (critical) and reduces the supplier reliability view only in Procurement (no cross-write). |
| BR-CSH-35 | **Allocation limits.** Sum of a cheque's allocations \<= cheque amount; for one payable the sum of allocations from non-terminal cheques (`issued`, `deposited`) \<= its `outstanding_amount`; for one receivable likewise. Allocations on an `issued` cheque can only target payables, on a `received` cheque only receivables (CHECK). When a payment is executed for an allocated payable the matching allocation is closed (the cheque must be `cleared`); an executed payment larger than the cheque's allocation is allowed (partly by cheque, partly by transfer). |

### 6.5 Alerts

| ID | Rule |
|---|---|
| BR-CSH-32 | **Payment due without approval.** Daily scan: payable with `due_date <= today + 3` (setting `unapproved_due_days`, default 3), status in `registered`, `matched` (not `approved`/`scheduled`/`disputed`), outstanding > 0 -> alert `payment_due_unapproved` (warning; high when overdue), dedupe `pyb:{payableId}`, recipients `finance_controller` and, for supplier payables, the PO owner (`procurement_officer`). Resolved when status leaves `registered/matched` or the payable is paid/cancelled. |
| BR-CSH-33 | **Receivable overdue.** Daily scan: `due_date < today` and outstanding > 0 and status in `certified`, `invoiced`, `partly_received` -> alert `receivable_overdue` with `days_overdue = today - due_date`; severity `warning` for 1-30 days, `high` for 31-60, `critical` > 60. Recipients `finance_controller`, `contracts_manager`, project manager. Dedupe `rcv:{receivableId}`; severity upgrades update the same alert and re-notify; resolved on full receipt or write-off. Overdue age buckets in aging use the same days. |
| BR-CSH-36 | **Alert dedupe and re-fire.** One open or acknowledged alert per `(rule_code, dedupe_key)`. For `balance_below_floor` the key is `{scope}:{projectId or company}`; the alert is updated, not duplicated, on each recompute. `cash.shortfall.detected.v1` is published when the alert is first raised, when `first_shortfall_week_no` moves **earlier**, or when `shortfall_amount` grows by at least `shortfall_refire_pct` percent **or** `shortfall_refire_amount` since the last publication. Clearing (no shortfall in the horizon) resolves the alert with reason `condition_cleared` and does not publish an event. An acknowledged alert keeps its acknowledgement while the shortfall week is unchanged; if the shortfall week moves earlier it returns to `open`. |

### 6.6 Payment proposals, trade view, aging

| ID | Rule |
|---|---|
| BR-CSH-37 | **Payment proposal builder.** Input: `pay_date`, `strategy`, optional `cash_cap_amount`, `lookahead_days` (default 7), `allow_partial`. Candidate payables: status `matched` or `approved`, not disputed, not blocked, outstanding > 0, with `due_date <= pay_date + lookahead_days` (or overdue). Sort by `priority asc, due_date asc, outstanding desc` (`priority_then_due`) or `due_date asc` (`oldest_first`). `available_cash = min(cash_cap_amount if given, projected balance at pay_date - floor)` where projected balance = opening balance of the week containing `pay_date` from the live forecast **excluding** the candidate payables of this proposal; if negative, 0. Walk the sorted list adding `outstanding` while `cumulative <= available_cash` (decision `include`); a payable that does not fit is `defer` (`cash_cap` or `floor_protection`), except with `allow_partial` a line may be included for `available - cumulative` when that is >= `min_partial_amount`. Priority 1 lines are always `include`; if they exceed available cash the proposal carries `floor_protection_applied=false` and warning `cash.proposal_critical_exceeds_cash`. `not_approved` lines (status `matched` not yet `approved`) are included only when the tenant setting `allow_proposal_unapproved` is true and are labelled. |
| BR-CSH-38 | **Floor protection and accept.** Accepting recomputes the floor check on the forecast with the included lines moved to `pay_date`: if any week of the horizon falls below floor **because of** the proposal (i.e. the base forecast was healthy there), accept needs `acknowledgeFloorBreach=true` and a `reason`, else `422 cash.proposal_breaches_floor`. Accept hands over to Procurement/Subcontracts by publishing `cash.payment_proposal.accepted.v1` (proposed, section 8.4); those modules create the payment batch. Cash never schedules or pays. |
| BR-CSH-39 | **Trade view.** Per project and work package (trade): `paid_to_date` = sum of `cash_actuals` out (supplier + subcontract) with that `work_package_id` (allocations are pro-rated by `settledFraction` over `wp_split` when a payment covers several work packages); `unpaid_committed` = (a) open payables' outstanding split by `wp_split` + (b) commitment `remaining_amount x (1 + vat)` by line work package; `next_30_days_out` = items of those two kinds dated within 30 days. The three figures are shown side by side and never summed into one (BR-X-02); payroll and overhead have no trade. |
| BR-CSH-40 | **Aging.** Receivable and payable aging use days past due on `as_of` date: buckets `current` (not yet due), `1-30`, `31-60`, `61-90`, `over_90`; grouped by counterparty and project; amount = outstanding. Retention receivables are shown in their own column (not aged). Credit notes reduce the bucket they fall in. Payable aging uses `due_date`, not `planned_pay_date`. **Example:** PA-04 due 2026-11-10 outstanding 2,352,590 is `current` on 2026-10-10 and `1-30` on 2026-11-20 (10 days late). |

### 6.7 R2/R3 intelligence and exposures

| ID | Rule |
|---|---|
| BR-CSH-41 | **Company forecast (R2).** The company live forecast contains the union of items of all non-closed projects (their own items, by reference, not copies of totals), items with `project_id is null` (overhead, loans, taxes, company payroll), and the company opening balance (BR-CSH-06). A bank account marked `account_type='project'` is excluded from the company opening balance when the project forecast uses it as its opening balance, so a pound is never counted twice. Company floor and alerts are independent of project floors. |
| BR-CSH-42 | **Uncommitted spend (R2).** When `include_uncommitted_spend` is on, for each work package the cost forecast gives `uncommitted = max(0, effective EAC - (accrued + committed remaining))` (from `project_refs` and `cost.forecast.updated.v1`); it is spread evenly across the schedule weeks of the work package that fall inside the horizon (from `planning.schedule.baselined/revised` dates), category `uncommitted_spend`, confidence `low`, `date_basis='assumed'`. Absent EAC or dates, no item is created and a `warnings[]` entry says why. |
| BR-CSH-43 | **Shortfall explanation (R2).** For a forecast with a shortfall at week S, the explainer finds the last week P before S with the highest closing balance (the peak), then returns (a) top items by amount of direction `out` dated in weeks P+1..S, (b) the inflows dated in P+1..S, (c) **missing receipts**: open receivables whose original due date is \<= end of S but whose `planned_date` is later (late) or whose confidence is `medium/low`. Each entry links to its `forecast_items` row. The explanation is a computed view, recomputed on request from the stored items, and is reproducible. |
| BR-CSH-44 | **Scenarios (R3).** A scenario is a list of adjustments applied to a **copy** of the live item set; source records are never changed. Move types: `delay_receivable`/`advance_receivable` shift one receivable by `shift_days` or set `new_date`; `shift_client_collections` shifts all open receivables of a client (e.g. "client pays 30 days late" = +30); `defer_payable`/`bring_forward_payable` shift a payable item, optionally only `moved_amount` (the remainder keeps its date); `delay_commitment` shifts a commitment line; `follow_up_receivable` moves the receivable to its due date (or `new_date`) as an expected recovery of lateness; `remove_item`, `add_inflow`, `add_outflow` for manual one-offs. Result = full forecast of kind `scenario` with `is_adjusted` items and `base_date`; delta metrics (`delta_min_balance_amount`, first shortfall week before and after) are stored. Defer of a payable beyond its `due_date + 90 days` returns warning `cash.defer_beyond_terms`; defer of priority 1 requires `include_critical`. |
| BR-CSH-45 | **Collection-date prediction (R3).** For a client with at least 3 received receivables in the last 24 months: `avg_days_late = mean(received_date - due_date)` clipped at 0..120; proposed `expected_date = due_date + round(avg_days_late)`, `date_window_days = round(stddev)`; stored as suggestion only; the user (or tenant policy) applies it, source `ai_suggestion`-like marker `date_basis='predicted'` (BR-X-08 analogue). With fewer than 3 samples no prediction is made. |
| BR-CSH-46 | **Exposure, not cash.** Pending variations and submitted claims (`exposures`) are shown beside the forecast as "not in forecast" with amounts; they never enter balances or alerts. A variation is cash only after it is billed in a payment application. |
| BR-CSH-47 | **Never presented as guaranteed.** Every API response that returns a future inflow includes `confidence`, `dateBasis` and `windowFrom/windowTo`; UI/report templates must label expected receipts as "expected". Reports that print forecast balances print the `as_of_date`, `computed_at`, formula version and assumptions list. |
| BR-CSH-48 | **Snapshots and accuracy.** `WeeklySnapshotJob` freezes the live forecast each Saturday 02:00 (project and, R2, company); `period_close` snapshots are taken when `cost.period.closed.v1` arrives. Accuracy: for each past snapshot week, `abs_error = abs(forecast closing - actual closing)` where actual closing comes from the bank balance entered on the week's last day; stored in `rm_forecast_accuracy`; informational only. Snapshots are kept 3 years (`SnapshotPurgeJob` deletes older rows and their items). |
| BR-CSH-49 | **Consumer idempotency.** Every consumer stores `(consumer, event_id)` (00 §3.9) and additionally upserts by natural key (`application_id`, `source_type+source_id`, `release_id`, `source_id` of commitments), so a replayed or duplicate event changes nothing. A handler failure never partially applies: the row change, `is_dirty` and the outbox write are one transaction. |
| BR-CSH-50 | **Visibility and project scope.** All list/read endpoints are trimmed to the user's projects (BR-X-09). Roles without `cash.forecast.view` get `403`; `project_manager` receives payroll only as category totals (no plan rows); `procurement_officer` receives payables only. Bank `account_no_masked` is omitted unless `cash.bank_account.view_number`. Exports follow the same rules (BR-X-06). |

---

## 7. API endpoints

Base path `/api/v1/cash`. Conventions per 00 §3.6: JSON camelCase, keyset pagination (`limit`, `cursor`, optional `withTotal=true`), actions are `POST`, mutating calls need `If-Match` (ETag from the read) and creating `POST`s accept `Idempotency-Key`. Money fields are `{ "amount": 2352590.0000, "currency": "EGP" }`; in the tables below `money` means that shape. All endpoints require authentication and project scope (BR-X-09). External users get `403` everywhere. Release column: R1 unless marked.

### 7.1 Forecast

| method | path | purpose | key request fields | key response fields | errors | permission |
| --- | --- | --- | --- | --- | --- | --- |
| GET | `/forecasts/live` | The live forecast of a scope with headline numbers | `scope` (`project`/`company`), `projectId` (required for project) | `id, scope, projectId, asOfDate, week1StartDate, openingBalance, floor, minBalance, minBalanceWeekNo, firstShortfallWeekNo, shortfallAmount, conservativeMinBalance, conservativeMinWeekNo, closingWeek13, isDirty, computedAt, formulaVersion, assumptions[]` | 404 `cash.forecast_not_found`, 403 | `cash.forecast.view` (company scope R2) |
| GET | `/forecasts` | List live/snapshot/scenario forecasts | filters below | summary rows | 400 `cash.filter_not_allowed` | `cash.forecast.view` |
| GET | `/forecasts/{id}` | One forecast (headline + settings used) | `include=periods` | as live + `inputsSnapshot` | 404 | `cash.forecast.view` |
| GET | `/forecasts/{id}/periods` | Weekly or monthly table | `periodType` (`week` default, `month`), `view` (`base`, `conservative`, `both`) | `periodNo, startDate, endDate, opening, inflow, outflow, closing, floor, isBelowFloor, inflowByCategory, outflowByCategory, conservativeClosing, itemCount` | 404 | `cash.forecast.view` |
| GET | `/forecasts/{id}/items` | **Drill-down**: the receipts and payments behind a period | `periodNo` or `from/to`, `direction`, `category`, `projectId`, `partyId`, `confidence`, `workPackageId` | `id, direction, category, sourceType, sourceId, sourceRef, partyName, plannedDate, baseDate, windowFrom, windowTo, amount, confidence, dateBasis, isOverdueRolled, reasonCode, link` | 404 | `cash.forecast.view` |
| GET | `/forecasts/{id}/shortfall` | First shortfall week and amount; R2: explanation | `explain=true` (R2) | `hasShortfall, firstShortfallWeekNo, minBalance, minBalanceWeekNo, shortfallAmount, floor, weeks[{periodNo, closing, isBelowFloor}]`, R2 `peakWeekNo, topOutflows[], inflowsInWindow[], missingReceipts[]` | 404; 422 `cash.explanation_not_available` (R1) | `cash.forecast.view` |
| GET | `/forecasts/{id}/compare` | Compare two forecasts (snapshot vs live, scenario vs live) | `withId` | per week `closingA, closingB, delta`, item adds/removes counts | 404; 422 `cash.forecast_scope_mismatch` | `cash.forecast.view` |
| POST | `/forecasts/{id}/refresh` | Force recomputation of a live forecast | none | updated headline | 409 `cash.forecast_not_live`; 429 | `cash.forecast.refresh` |
| POST | `/forecasts/snapshots` | Take a manual snapshot | `scope, projectId, reason` | snapshot id | 404 | `cash.forecast.snapshot` |
| GET | `/forecasts/{id}/trade-cash` | Trade view: paid to date, unpaid committed, next 30 days per work package | `projectId` | `rows[{workPackageId, tradeName, paidToDate, unpaidPayables, unpaidCommitments, next30DaysOut}]` (three figures kept apart) | 404 | `cash.forecast.view` |
| GET | `/forecast-settings` | Effective settings (project row merged over company row) | `scope, projectId` | all settings + `sources` per field | 404 | `cash.forecast.view` |
| PUT | `/forecast-settings` | Upsert a settings row | `scope, projectId, openingBalanceAmount, openingBalanceDate, minBalanceAmount, alertHorizonWeeks, ...` | row | 422 `cash.invalid_setting` | `cash.forecast.settings.manage` |
| GET/POST | `/assumptions` | List/create assumptions | `scope, projectId, partyId, kind, valueNumeric, validFrom, validTo, confidenceRangeDays, note` | row | 422 `cash.assumption_overlap` | view: `cash.forecast.view`; write: `cash.assumption.manage` |
| PATCH/DELETE | `/assumptions/{id}` | Edit / end assumption (soft delete) | merge patch | row | 412 | `cash.assumption.manage` |

List filters for `/forecasts`: `filter[scope]`, `filter[projectId]`, `filter[kind]`, `filter[snapshotReason]`, `filter[asOfDate][gte|lte]`, `filter[hasShortfall]`; sort `asOfDate`, `-asOfDate` (default), `computedAt`, `minBalance`. `/forecasts/{id}/items` filters: `filter[periodNo]`, `filter[direction]`, `filter[category]`, `filter[confidence]`, `filter[projectId]`, `filter[partyId]`, `filter[workPackageId]`, `filter[plannedDate][gte|lte]`, `filter[isOverdueRolled]`; sort `plannedDate` (default), `-amount`, `partyName`.

### 7.2 Receivables

| method | path | purpose | key request fields | key response fields | errors | permission |
| --- | --- | --- | --- | --- | --- | --- |
| GET | `/receivables` | List receivables | filters below; `include=followUps` | `id, receivableNumber, projectId, clientName, kind, status, applicationNumber, documentNumber, grossAmount, vatAmount, receivedAmount, outstandingAmount, issueDate, dueDate, expectedDate, expectedDateBasis, confidence, daysOverdue, lastFollowUpAt` | 400 | `cash.receivable.view` |
| GET | `/receivables/{id}` | One receivable with source refs and history | `include=followUps,history` | as list + `applicationId, certificateId, invoiceId, overrideReason, windowDays` | 404 | `cash.receivable.view` |
| POST | `/receivables` | Manual receivable (kind `other` or `advance`) | `projectId, clientPartyId, kind, grossAmount, currency, dueDate, description` | row | 422 `cash.manual_kind_not_allowed` | `cash.receivable.manage` |
| POST | `/receivables/{id}/override-date` | Set the expected date used by the forecast | `expectedDate, reason, keepOnCertify` | row + forecast dirty | 422 `cash.override_reason_required`, `cash.receivable_not_open` | `cash.receivable.manage` |
| POST | `/receivables/{id}/reset-date` | Back to contractual/assumed date | none | row | 409 `cash.receivable_not_open` | `cash.receivable.manage` |
| GET/POST | `/receivables/{id}/follow-ups` | Follow-up notes (call, meeting, email) | `channel, contactName, note, promisedDate, nextFollowUpDate, applyAsExpectedDate` | follow-up row | 422 `cash.receivable_not_open` | view `cash.receivable.view`; add `cash.receivable.follow_up` |
| POST | `/receivables/{id}/cancel` | Cancel a stale expected receivable | `reason` | row | 422 `cash.cancel_not_allowed` (not stale/expected) | `cash.receivable.manage` |
| POST | `/receivables/{id}/request-write-off` | Start write-off approval | `amount, reason` | approval id | 422 `cash.write_off_reason_required` | `cash.receivable.request_write_off` |
| GET | `/receivables/aging` | Aging (see 7.7) | `asOfDate, groupBy` | buckets | 400 | `cash.aging.view` |

Filters for `/receivables`: `filter[projectId]`, `filter[clientPartyId]`, `filter[status]` (multi), `filter[kind]`, `filter[confidence]`, `filter[dueDate][gte|lte]`, `filter[expectedDate][gte|lte]`, `filter[overdue]=true`, `filter[applicationNumber]`, `q` (number/client text). Sort: `dueDate`, `expectedDate`, `-outstandingAmount`, `-createdAt` (default; keyset).

### 7.3 Payables and commitments

| method | path | purpose | key request fields | key response fields | errors | permission |
| --- | --- | --- | --- | --- | --- | --- |
| GET | `/payables` | List payables | filters below | `id, payableNumber, projectId, partyName, kind, status, sourceNumber, payableAmount, paidAmount, outstandingAmount, dueDate, plannedPayDate, proposedPayDate, priority, isPartyBlocked, exceedsCommitment, scheduledDate` | 400 | `cash.payable.view` |
| GET | `/payables/{id}` | One payable | `include=wpSplit,allocations` | as list + `netAmount, vatAmount, withholdingAmount, poId, subcontractId, wpSplit[], disputeId` | 404 | `cash.payable.view` |
| POST | `/payables/{id}/plan` | Set priority and/or planned pay date | `priority, plannedPayDate, plannedPayReason, confirm` | row | 422 `cash.planned_date_in_past`, `cash.planned_reason_required`, `cash.payable_not_open`; procurement role: date stored as `proposedPayDate` | finance: `cash.payable.plan`; procurement: `cash.payable.propose` |
| POST | `/payables:bulk-plan` | Bulk priority/date (max 500) | `items[{payableId, priority, plannedPayDate}]` | per-item results | 207-style in 200 | `cash.payable.plan` |
| GET | `/commitments` | Open commitments (PO/subcontract) | filters below | `id, commitmentKind, sourceNumber, projectId, partyName, status, committedAmount, invoicedAmount, paidExVatAmount, remainingAmount, creditDays, versionNo` | 400 | `cash.commitment.view` |
| GET | `/commitments/{id}` | Commitment with lines | `include=lines,invoices` | + lines `workPackageId, lineAmount, invoicedAmount, remainingAmount, neededByDate` | 404 | `cash.commitment.view` |
| GET | `/exposures` | Pending variations/claims not in forecast | `projectId` | `kind, sourceNumber, amount, status, occurredOn` | 404 | `cash.forecast.view` |

Filters `/payables`: `filter[projectId]`, `filter[partyId]`, `filter[status]` (multi), `filter[kind]`, `filter[priority]`, `filter[dueDate][gte|lte]`, `filter[isPartyBlocked]`, `filter[exceedsCommitment]`, `filter[unapprovedDue]=true`, `q`. Sort: `dueDate` (default asc), `priority`, `-outstandingAmount`, `-createdAt`. Filters `/commitments`: `filter[projectId]`, `filter[partyId]`, `filter[commitmentKind]`, `filter[status]`, `filter[hasRemaining]=true`; sort `-remainingAmount`, `sourceNumber`, `-createdAt`.

### 7.4 Cheques, bank accounts, payroll and planned items

| method | path | purpose | key request fields | key response fields | errors | permission |
| --- | --- | --- | --- | --- | --- | --- |
| GET | `/cheques` | Cheque register | filters below | `id, registerNumber, direction, chequeNo, bankAccountName, counterpartyName, projectId, amount, issueDate, dueDate, status, isPostDated, daysToDue, allocatedAmount` | 400 | `cash.cheque.view` |
| POST | `/cheques` | Register an issued or received cheque | `direction, chequeNo, bankAccountId, drawerBankPartyId, counterpartyId, projectId, amount, currency, issueDate, dueDate, allocations[{payableId or receivableId, amount}]` | row | 409 `cash.cheque_duplicate`; 422 `cash.allocation_exceeds_outstanding`, `cash.allocation_direction_mismatch`, `cash.cheque_dates_invalid` | `cash.cheque.manage` |
| GET | `/cheques/{id}` | One cheque with allocations |  | + allocations, status history | 404 | `cash.cheque.view` |
| POST | `/cheques/{id}/deposit` | Deposit a received cheque | `depositedOn, bankAccountId, allowEarly, reason` | row | 422 `cash.cheque_not_yet_due`; 409 invalid state | `cash.cheque.manage` |
| POST | `/cheques/{id}/clear` | Mark cleared | `clearedOn` | row | 409 | `cash.cheque.manage` |
| POST | `/cheques/{id}/bounce` | Mark bounced | `bouncedOn, reasonCode, note` | row; alert raised | 409 | `cash.cheque.manage` |
| POST | `/cheques/{id}/cancel` | Cancel an issued cheque | `reason` | row | 409 `cash.cheque_already_cleared` | `cash.cheque.manage` |
| POST | `/cheques/{id}/replace` | Replace with a new cheque | new cheque fields | new row (copy of allocations) | 409 | `cash.cheque.manage` |
| GET/POST | `/bank-accounts` | List/create bank accounts | `accountCode, name, bankPartyId, accountNoMasked, currency, accountType, projectId, includeInForecast` | row (number omitted without `view_number`) | 409 `cash.bank_account_code_duplicate` | view `cash.bank_account.view`; write `cash.bank_account.manage` |
| PATCH | `/bank-accounts/{id}` | Edit | merge patch | row | 412 | `cash.bank_account.manage` |
| PATCH | `/bank-accounts/{id}/balance` | Record current balance (manual) | `balanceAmount, asOfDate` | row; forecasts dirty | 422 `cash.balance_date_in_future` | `cash.bank_account.manage` |
| GET/POST | `/payroll-plans` | List/create payroll plans | `nameAr, projectId, frequency, amount, currency, weekday/dayOfMonth, anchorDate, startDate, endDate` | row (PM: totals only) | 422 `cash.payroll_schedule_invalid` | view `cash.payroll_plan.view`; write `cash.payroll_plan.manage` |
| PATCH | `/payroll-plans/{id}` ; POST `/payroll-plans/{id}/pause` `/resume` `/end` | Edit and lifecycle |  | row | 409 | `cash.payroll_plan.manage` |
| GET | `/payroll-plans/occurrences` | Expanded dates for the next 13 weeks | `projectId` | `[{date, amount, planId}]` |  | `cash.payroll_plan.view` or `cash.forecast.view` (totals) |
| GET/POST | `/planned-items` | List/create other in/outflows | `direction, kind, projectId, partyId, description, amount, currency, plannedDate, recurrence, recurrenceEndDate, confidence` | row | 422 `cash.planned_item_invalid` | view `cash.planned_item.view`; write `cash.planned_item.manage` |
| PATCH | `/planned-items/{id}` ; POST `/planned-items/{id}/realise` `/cancel` | Edit, mark done or cancel | `realisedOn` | row | 409 | `cash.planned_item.manage` |

Filters `/cheques`: `filter[direction]`, `filter[status]`, `filter[bankAccountId]`, `filter[counterpartyId]`, `filter[projectId]`, `filter[dueDate][gte|lte]`, `filter[postDated]=true`; sort `dueDate` (default), `-amount`, `-createdAt`. `/planned-items`: `filter[direction]`, `filter[kind]`, `filter[status]`, `filter[projectId]`, `filter[plannedDate][gte|lte]`; sort `plannedDate`, `-createdAt`.

### 7.5 Payment proposals

| method | path | purpose | key request fields | key response fields | errors | permission |
| --- | --- | --- | --- | --- | --- | --- |
| POST | `/payment-proposals` | Generate a proposal for a pay date | `scope, projectId, payDate, strategy, cashCapAmount, lookaheadDays, allowPartial, minPartialAmount` | proposal with lines, `availableCash, totalProposed, totalDeferred, floorProtectionApplied, warnings[]` | 422 `cash.no_live_forecast`, `cash.pay_date_in_past` | `cash.proposal.generate` |
| GET | `/payment-proposals` | List | filters | summary |  | `cash.proposal.view` |
| GET | `/payment-proposals/{id}` | One proposal with lines and floor effect (`weeklyClosingWithProposal[]`) | `include=lines` |  | 404 | `cash.proposal.view` |
| PATCH | `/payment-proposals/{id}/lines/{lineId}` | Include/defer a line, change proposed amount | `decision, proposedAmount, deferReason` | line + new totals | 422 `cash.proposal_line_amount_invalid`; 409 proposal not `proposed` | `cash.proposal.generate` |
| POST | `/payment-proposals/{id}/accept` | Accept and hand over | `acknowledgeFloorBreach, reason` | proposal `accepted`, `handoverEventId` | 422 `cash.proposal_breaches_floor`, `cash.proposal_empty`; 409 `cash.proposal_expired` | `cash.proposal.accept` |
| POST | `/payment-proposals/{id}/discard` | Discard | `reason` |  | 409 | `cash.proposal.generate` |

Filters: `filter[status]`, `filter[scope]`, `filter[projectId]`, `filter[payDate][gte|lte]`; sort `-createdAt` (default), `payDate`.

### 7.6 Alerts

| method | path | purpose | key request fields | key response fields | errors | permission |
| --- | --- | --- | --- | --- | --- | --- |
| GET | `/alerts` | List alerts | filters | `id, ruleCode, severity, status, scope, projectId, subjectType, subjectId, messageCode, messageParams, firstRaisedAt, lastRaisedAt` | 400 | `cash.alert.view` |
| POST | `/alerts/{id}/acknowledge` | Acknowledge with note | `note` | row | 409 `cash.alert_already_resolved` | `cash.alert.acknowledge` |
| POST | `/alerts/{id}/resolve` | Manual resolve (non-floor rules only) | `reason` | row | 422 `cash.alert_not_manually_resolvable` | `cash.alert.acknowledge` |

Filters: `filter[ruleCode]`, `filter[severity]`, `filter[status]`, `filter[projectId]`, `filter[scope]`; sort `-lastRaisedAt` (default), `severity`.

### 7.7 Aging, reports, exports

| method | path | purpose | key request fields | key response fields | errors | permission |
| --- | --- | --- | --- | --- | --- | --- |
| GET | `/aging/receivables` | Receivable aging | `asOfDate, groupBy` (`client`, `project`), `projectId` | `rows[{groupId, name, current, d1_30, d31_60, d61_90, over90, retention, total}]`, totals | 400 | `cash.aging.view` |
| GET | `/aging/payables` | Payable aging | same | same (no retention column) | 400 | `cash.aging.view` (procurement: supplier payables only) |
| GET | `/reports/receipts-and-payments` | Actual receipts and payments for a date range | `from, to, projectId, direction, category` | `rows[{valueDate, direction, category, partyName, projectId, reference, amount}]`, totals | 400 | `cash.report.view` |
| GET | `/reports/payment-schedule` | Payables by planned pay date and priority | `from, to, projectId` | `rows[{payDate, payableNumber, party, priority, outstanding, status}]` | 400 | `cash.report.view` |
| GET | `/reports/cash-by-project` | One row per project: opening, min balance, shortfall flag, next 30 days in/out |  | rows |  | `cash.report.view` |
| GET | `/reports/forecast-accuracy` | Snapshot accuracy | `from, to` | weekly abs error |  | `cash.report.view` |
| GET | `{report}/export` | \`format=xlsx | csv | pdf`; `202\` + job when > 5,000 rows | same filters | file or job url |
| POST | `/exports` (R2) | Accounting export or ETA-ready invoice data | `exportType, periodFrom, periodTo, format` | batch id, `202` | 422 `cash.export_period_invalid` | `cash.export.run` |
| POST | `/bank-statements` (R2) | Upload statement (file id from platform files) | `bankAccountId, fileId` | import row | 422 `cash.statement_unreadable` | `cash.statement.manage` |
| GET | `/bank-statements/{id}/lines` (R2) | Lines with match suggestions | `filter[matchStatus]` | lines |  | `cash.statement.manage` |
| POST | `/bank-statements/{id}/lines/{lineId}/match` (R2) | Confirm a match | `actualId` or `chequeId` | line | 409 `cash.statement_line_already_matched` | `cash.statement.manage` |
| POST | `/bank-statements/{id}/confirm` (R2) | Finish reconciliation and set the account balance |  | import `confirmed` | 422 `cash.statement_unmatched_lines` (unless `allowUnmatched`) | `cash.statement.manage` |

### 7.8 Scenarios (R3)

| method | path | purpose | key request fields | key response fields | errors | permission |
| --- | --- | --- | --- | --- | --- | --- |
| POST | `/scenarios` | Create draft | `scope, projectId, nameAr, nameEn, description, adjustments[]` | scenario | 422 `cash.scenario_adjustment_invalid` | `cash.scenario.manage` |
| GET | `/scenarios` ; `/scenarios/{id}` | List/read | filters `filter[status]`, `filter[projectId]`, sort `-createdAt` | scenario + result headline | 404 | `cash.scenario.view` |
| PUT | `/scenarios/{id}/adjustments` | Replace the adjustment list (discards result) | `adjustments[{type, targetType, targetId, shiftDays, newDate, movedAmount, oneOffDate, oneOffAmount, note}]` | scenario | 409 archived; 422 | `cash.scenario.manage` |
| POST | `/scenarios/{id}/compute` | Compute against the current live forecast | none | `resultForecastId`, delta metrics | 422 `cash.scenario_empty`; 409 | `cash.scenario.manage` |
| POST | `/scenarios/{id}/archive` | Archive |  |  |  | `cash.scenario.manage` |
| GET | `/receivables/{id}/predicted-date` | Collection-date prediction for one receivable |  | `predictedDate, windowDays, sampleSize, avgDaysLate` | 422 `cash.prediction_insufficient_history` | `cash.scenario.view` |

### 7.9 Examples

**Live forecast, golden project (GET ****`/forecasts/live?scope=project&projectId=0192...a1`****)**

```json
{
  "id": "0192f3c1-8e44-7a10-b6d2-5d1f0a9c7001",
  "scope": "project", "projectId": "0192f2aa-0001-7000-8000-0000000000a1",
  "kind": "live", "asOfDate": "2026-10-10", "week1StartDate": "2026-10-10", "horizonWeeks": 13,
  "openingBalance": { "amount": 2400000.0000, "currency": "EGP" },
  "floor": { "amount": 600000.0000, "currency": "EGP" },
  "minBalance": { "amount": 562590.0000, "currency": "EGP" }, "minBalanceWeekNo": 8,
  "hasShortfall": true, "firstShortfallWeekNo": 8,
  "shortfallAmount": { "amount": 37410.0000, "currency": "EGP" },
  "conservativeMinBalance": { "amount": -387410.0000, "currency": "EGP" }, "conservativeMinWeekNo": 10,
  "closingWeek13": { "amount": 862590.0000, "currency": "EGP" },
  "isDirty": false, "computedAt": "2026-10-10T05:12:03Z", "formulaVersion": "cashfc-1",
  "assumptions": [{ "kind": "collection_lag_days", "scope": "company", "valueNumeric": 0, "note": "client pays on due date" }]
}
```

**Drill-down of the shortfall week (GET ****`/forecasts/{id}/items?filter[periodNo]=8&filter[direction]=out&sort=-amount&limit=5`****)**

```json
{ "items": [
  { "id": "0192f3c1-...-c001", "direction": "out", "category": "supplier", "sourceType": "payable",
    "sourceId": "0192f2de-...-b007", "sourceRef": "INV-8841", "partyName": "Al-Nasr Steel",
    "plannedDate": "2026-12-02", "baseDate": "2026-12-02", "amount": { "amount": 310000.0000, "currency": "EGP" },
    "confidence": "high", "dateBasis": "contractual", "reasonCode": "invoice_due", "isOverdueRolled": false },
  { "id": "0192f3c1-...-c002", "direction": "out", "category": "payroll", "sourceType": "payroll_plan",
    "sourceRef": "Site labour weekly", "plannedDate": "2026-11-28", "amount": { "amount": 150000.0000, "currency": "EGP" },
    "confidence": "high", "dateBasis": "contractual", "reasonCode": "payroll_run" }
], "nextCursor": null }
```

**Override an expected date (POST ****`/receivables/{id}/override-date`****, headers ****`If-Match: "17"`****)**

```json
{ "expectedDate": "2026-12-22", "reason": "client_promise: finance director said payment after board meeting", "keepOnCertify": false }
```

Response `200`: the receivable with `expectedDate: "2026-12-22"`, `expectedDateBasis: "override"`, `confidence: "medium"`, `isDirty` forecast marked. Without `reason`: `422 cash.override_reason_required`.

**Generate a payment proposal (POST ****`/payment-proposals`****, ****`Idempotency-Key`**** header)**

```json
{ "scope": "project", "projectId": "0192f2aa-0001-7000-8000-0000000000a1", "payDate": "2026-11-28",
  "strategy": "priority_then_due", "lookaheadDays": 7, "allowPartial": false }
```

Response (excerpt): `availableCash` is illustrative (projected balance at the pay date excluding the proposal's own candidate payables, minus the floor); lines are sorted by priority then due date; the lines that do not fit are `decision: defer`, `deferReason: floor_protection`; `floorProtectionApplied: true`; `warnings: []`.

**Scenario compute, delay collection 30 days (R3) (POST ****`/scenarios/{id}/compute`****)** returns `{ "resultForecastId": "...", "baseMinBalance": 562590.00, "scenarioMinBalance": -1790000.00, "deltaMinBalance": -2352590.00, "baseFirstShortfallWeekNo": 8, "scenarioFirstShortfallWeekNo": 5 }` for the golden project when PA-04 moves from week 5 to week 9 (2026-12-10); the numbers are derived in section 15.3.

---

## 8. Events

Envelope per 00 §3.9 (`eventId, type, occurredAt, tenantId, projectId, actorId, correlationId, causationId, payload`). Payloads below show only `payload`. Money is `{amount, currency}` shown as `x`. Partition key = aggregate id (forecast id). JSON schemas live in `Cash.Contracts/events/*.schema.json`.

### 8.1 Published (names fixed by 00 §7)

| Event | Fires when | Payload |
|---|---|---|
| `cash.forecast.refreshed.v1` | A live forecast is recomputed **and** `input_hash` changed (at most once per 60 s per forecast, BR-CSH-34); also after a manual refresh that changed numbers; snapshots do not publish | `forecastId, scope, projectId?, asOfDate, week1StartDate, horizonWeeks, openingBalance x, floor x, minBalance x, minBalanceWeekNo, hasShortfall, firstShortfallWeekNo?, shortfallAmount x, conservativeMinBalance x, conservativeMinWeekNo?, closingWeek13 x, next30DaysIn x, next30DaysOut x, openReceivablesTotal x, openPayablesTotal x, overdueReceivablesTotal x, computedAt, formulaVersion` |
| `cash.shortfall.detected.v1` | Floor alert `balance_below_floor` raised for the first time, or re-fired by BR-CSH-36 (earlier week, or growth by `shortfall_refire_pct`/`shortfall_refire_amount`). Never for the conservative-only case. | `alertId, scope, projectId?, forecastId, firstShortfallWeekNo, weekStartDate, weekEndDate, minBalanceWeekNo, minBalance x, floor x, shortfallAmount x, previousShortfallAmount x?, severity, isRefire, conservativeMinBalance x, topOutflows[{sourceRef, partyName, category, plannedDate, amount x}] (up to 5, week <= first shortfall week), expectedInflows[{sourceRef, partyName, plannedDate, amount x, confidence}] (R2: explanation fields; empty in R1)` |

Consumers of the two events: reporting (Command Center cash tiles and exceptions, file 03) and notifications (shortfall notice, section 11). No other module consumes cash events; there is no feedback into Procurement or Contracts in R1.

### 8.2 Consumed

All handlers record `(consumer, event_id)` in `platform.processed_events`, upsert by a natural key (BR-CSH-49), run in one transaction with the row change, the `is_dirty` flag and any outbox write, and ignore events older than the stored version (BR-CSH-27). After a successful apply they mark the affected live forecasts dirty. Failed events follow the 5-step retry and dead-letter (00 §3.9).

| Event | Handler | Action | Idempotency key |
|---|---|---|---|
| `contracts.contract.signed.v1` | `ContractTermsHandler` | Upsert `contract_refs` (value, terms, retention, advance, VAT, completion date); no schedule yet | `contract_id` + `version` |
| `contracts.contract.amended.v1` (proposed 8.4) | `ContractTermsHandler` | Update `contract_refs` version, completion date, value; recompute expected dates of `expected` receivables | `contract_id` + `version` |
| `contracts.payment_application.submitted.v1` | `PaymentApplicationHandler` | Create/replace receivable `expected`: `application_id`, `application_number` (`applicationNumber`), `revision_no`, `gross_amount = payableAmount x (1 - haircut)`, `due_date = expectedCertificationDate + payment_terms_days`, confidence `medium`. Ignores if a `certified` row exists. | unique `(tenant_id, application_id)`; ignore revision \<= stored |
| `contracts.payment_application.certified.v1` | `PaymentApplicationHandler` | Move to / create `certified`: `gross_amount = certifiedPayable`, `vat_amount = certifiedVat`, `certificate_id`, `due_date = dueDate`, confidence `high`, recompute `expected_date` (override cleared unless `keep_on_certify`). Raises alert `receivable_overdue` check at next scan. | same row; status guard |
| `contracts.invoice_to_client.issued.v1` | `ClientInvoiceHandler` | Link to the certificate receivable (BR-CSH-08) or create `invoice_only`; take `dueDate` of the invoice (BR-CSH-09) | unique `(tenant_id, invoice_id)` |
| `contracts.receipt.recorded.v1` | `ReceiptHandler` | Allocate to receivables (BR-CSH-16); write `cash_actuals` (`client_receipt`/`retention_receipt`/`advance_receipt` from `receiptType`); reversal restores outstanding and reverses actual | `cash_actuals` unique `(source_type, source_id, source_line_key, is_reversal)` |
| `contracts.retention.released.v1` | `RetentionAndClaimHandler` | Create/update `retention` receivable keyed by `releaseId` (BR-CSH-15); status `collected` closes nothing here | unique `release_id` |
| `contracts.claim.submitted.v1`, `.settled.v1` (R2) | `RetentionAndClaimHandler` | submitted: upsert `exposures` (`claim_submitted`); settled: close exposure, create `claim` receivable | unique `(kind, source_id)` / `release_id` |
| `contracts.variation.submitted.v1`, `.approved.v1`, `.rejected.v1` | `RetentionAndClaimHandler` | submitted: exposure `variation_submitted` open; approved: exposure closed (becomes cash only through the application); rejected: exposure closed with kind `variation_rejected` | unique `(kind, source_id)` |
| `contracts.guarantee.expiring.v1` | `GuaranteeExpiryHandler` | Raise alert `guarantee_expiring` (info, 30 days; warning, 7 days); informational for cash (commission or cash margin is a manual planned item) | dedupe `gtee:{guaranteeId}:{daysLeft bucket}` |
| `procurement.purchase_order.issued.v1` | `CommitmentHandler` | Create `commitments` (`purchase_order`) and lines from `baseLineAmount`, `neededByDate`, `creditDays` | unique `(commitment_kind, source_id)`; `versionNo` guard |
| `procurement.purchase_order.changed.v1` | `CommitmentHandler` | Replace line set with the new version; keep invoiced links; recompute remaining (BR-CSH-24) | `versionNo` strictly greater |
| `procurement.purchase_order.cancelled.v1`, `.closed.v1` | `CommitmentHandler` | Commitment `cancelled`/`closed`, remaining 0 | status guard |
| `procurement.invoice.registered.v1` | `SupplierInvoiceHandler` | Create payable `registered` (confidence `medium`), amounts from `netPayableAmount`, `dueDate`; add `commitment_invoice_links` state `registered` | unique `(source_type, source_id)` |
| `procurement.invoice.matched.v1` | `SupplierInvoiceHandler` | Payable `matched` (create if missing), refresh amounts, `withholdingAmount`, `wp_split` from `lines[]`; link state `matched`; credit notes negative (BR-CSH-20) | same key; `occurredAt` guard |
| `procurement.invoice.disputed.v1` | `SupplierInvoiceHandler` | Payable `disputed`, `dispute_id` (BR-CSH-21) | status guard |
| `procurement.invoice.approved_for_payment.v1`, `.cancelled.v1` (proposed 8.4) | `SupplierInvoiceHandler` | `approved` + `approved_for_payment_at`; or `cancelled` (rejected/cancelled invoice) | status guard |
| `procurement.payment.scheduled.v1` | `PaymentStatusHandler` | Payables `scheduled` with `scheduledDate`; proposal lines `scheduled`; alert `payment_to_blocked_party` if blocked | `paymentId` + allocation |
| `procurement.payment.executed.v1` | `PaymentStatusHandler` | BR-CSH-23: paid amounts, actuals, commitment `paid_ex_vat_amount`; reversal when `reversal=true` | actual key `(payment, invoiceId)` |
| `subcontracts.subcontract.signed.v1`, `.revised.v1` (R2) | `CommitmentHandler` | Subcontract commitment with lines by work package; revised applies `deltaLines` | `versionNo` guard |
| `subcontracts.valuation.certified.v1`, `.valuation.reversed.v1` (proposed by 11) (R2) | `SubcontractPayableHandler` | Create payable `subcontract_certificate` (`payableAmount`, `dueDate`, retention not payable); reduces commitment remaining by `grossAmount`; reversal cancels | unique `(source_type, source_id)` |
| `subcontracts.payment.scheduled.v1`, `.executed.v1` (R2) | `PaymentStatusHandler` | as procurement payments, on subcontract payables; `paymentType=retention` payments close retention payables | `paymentId`, `version` |
| `subcontracts.backcharge.raised.v1` (R2) | `SubcontractPayableHandler` | Negative payable `backcharge` (netted per BR-CSH-20); `deltaAmount` handles revisions | `backchargeId` + `version` |
| `subcontracts.subcontract.closed.v1` (proposed by 11) (R2) | `CommitmentHandler` | Commitment `closed`, remaining 0 | status guard |
| `cost.budget.approved.v1`, `.revised.v1` | `ProjectRefHandler` | Update `project_refs.budget_total_amount`, `budget_version` (R2: spread base for uncommitted spend) | `budget_version` guard |
| `cost.forecast.updated.v1` | `ProjectRefHandler` | Update `eac_amount`, `ac_amount`, `cost_as_of`; marks forecast dirty only when `include_uncommitted_spend` (R2) | latest `asOfDate` wins |
| `cost.period.closed.v1` | `ProjectRefHandler` | Update `last_closed_period`; take `period_close` snapshot (BR-CSH-48) | `periodId` |
| `planning.schedule.baselined.v1`, `.revised.v1` | `ProjectRefHandler` | Update planned dates; used only for uncommitted-spend spreading (R2) and subcontract completion | baseline version guard |
| `core.project.created.v1`, `.activated.v1`, `.closed.v1` | `ProjectRefHandler` | Upsert `project_refs`; create the live project forecast on activation (empty settings); closed: live forecast archived (excluded from company forecast), open alerts resolved `superseded` | `project_id` |
| `core.party.updated.v1`, `core.party.blocked.v1` | `PartyHandler` | Refresh name snapshots; blocked: set `is_party_blocked` on open payables (BR-CSH-22) | upsert by `party_id` |
| `closeout.handover.completed.v1` | `ProjectRefHandler` | Sets `contract_refs.retention_release_eligible_stage` hint, shown on the retention view; no receivable is created until Contracts publishes the release | `(project_id, handover_type)` |
| `core.calendar.changed.v1` (proposed by 05) | `CalendarHandler` | Recompute payroll/overhead occurrences (working-day adjustment) | `(scope, effectiveFrom)` |
| `platform.approval.approved.v1`, `.rejected.v1`, `.cancelled.v1` (`recordType='cash.receivable_write_off'`) | `ApprovalOutcomeHandler` | Approved: receivable `written_off`; rejected/cancelled: stays open | approval id + status guard |

### 8.3 Notes on ordering and replay

1. Events for one aggregate arrive in order; events of different aggregates may interleave. Example: `invoice.matched` before `invoice.registered` is legal (handled by create-if-missing); a `payment.executed` for an invoice Cash has never seen is **not** applied: the handler fails with `cash.payable_not_found`, is retried per 00 §3.9 (up to 2 h), then dead-lettered.
2. A nightly `CommitmentConsistencyJob` and `RmRebuildJob` repair drift (section 9). A full replay of consumed events from the outbox rebuilds schedules deterministically.

### 8.4 Proposed registry additions

1. `procurement.invoice.approved_for_payment.v1` (publisher procurement; consumers cash, reporting): payload `invoiceId, supplierId, projectId, approvedBy, approvedAt, netPayableAmount, currency`. Needed because the v3 payable workflow (Matched -> Approved) is visible in cash and alert `payment_due_unapproved` depends on it; 06 has the status but no event.
2. `procurement.invoice.cancelled.v1` (publisher procurement; consumers cash, cost, reporting): payload `invoiceId, supplierId, projectId, reason (rejected|cancelled|fully_credited), netPayableAmount`. Needed so cash removes the payable (registry has only registered/matched/disputed).
3. `cash.payment_proposal.accepted.v1` (publisher cash; consumers procurement, subcontracts, notifications): payload `proposalId, proposalNumber, scope, projectId?, payDate, floorBreachAcknowledged, lines[{payableId, sourceType (procurement_invoice|subcontract_valuation), sourceId, partyId, projectId, proposedAmount x, priority, dueDate}]`. Lets Procurement prefill a `payment_batch` draft; without it the proposal would be a dead end.
4. Add `cash` as consumer of `contracts.contract.amended.v1` (proposed by 09), of `subcontracts.valuation.reversed.v1`, `subcontracts.subcontract.closed.v1` (proposed by 11) and `core.calendar.changed.v1` (proposed by 05); add `cash` as consumer of `core.project.*`, `core.party.updated/blocked` (registry lists procurement/sourcing/subcontracts/contracts for party events), `closeout.handover.completed.v1` (already listed), `contracts.variation.*` (listed), `contracts.claim.*` (listed) and `planning.schedule.*` (listed).
5. Extend `contracts.invoice_to_client.issued.v1` payload with `applicationId?` and `receivableKind?` (additive, allowed within v1 per 00 §3.9 rule 1) so BR-CSH-08 step 1 works.
6. No change is needed to `procurement.payment.executed.v1`, but its fields `settledFraction` and `paidExVatAmount` are contractually required by BR-CSH-23 and BR-CSH-39 (contract test).

---

## 9. Background jobs and schedules

Per 00 §3.10: Hangfire, tenant context, timeout, `platform.job_runs` record, idempotent. PT = per-tenant enable flag. Time zone `Africa/Cairo`. Queues: `critical`, `default`, `reports`.

| Job | Schedule / trigger | What | Idempotency | PT |
|---|---|---|---|---|
| `ForecastRefreshJob` | every 60 s, `critical` | Selects live forecasts with `is_dirty` and `dirty_since <= now - 60 s` (debounce), runs `ForecastItemGenerator` + `ForecastCalculator` + `ShortfallEvaluator`; writes forecast, periods, items; publishes `cash.forecast.refreshed.v1` when `input_hash` changed; per-forecast advisory lock | Pure function of inputs; same hash = no event; lock `pg_advisory_xact_lock(hash(forecast_id))` | yes |
| `ForecastNightlyRebuildJob` | 01:30 daily, `default` | Recomputes every live forecast regardless of dirty flag (moves `as_of_date` to today and shifts week 1 when a new week starts) | Same function; no event if hash same | yes |
| `WeeklySnapshotJob` | Saturday 02:00 (week start) | Freezes live project (and R2 company) forecasts as `snapshot` reason `weekly` after a fresh recompute | Unique `(scope, project_id, snapshot_reason, as_of_date)` for weekly | yes |
| `OverdueScanJob` | daily 06:00 | Rules `receivable_overdue` (BR-CSH-33), `payment_due_unapproved` (BR-CSH-32), `receipt_exceeds_receivable` leftovers; severity upgrades; resolves cleared alerts; marks receivables with expired `expected_date` for BR-CSH-13 (dirty) | Alert dedupe key | yes |
| `ChequeDueJob` | daily 06:15 | Rule `cheque_due_no_funds` (BR-CSH-31), task `cheque_deposit_due` for received cheques still undeposited 7 days after due date; lists cheques due in the next 3 days in the finance morning digest | Dedupe `chq:{id}`; task unique per cheque and day | yes |
| `ProposalExpiryJob` | hourly | `payment_proposals` past `expires_at` become `expired` (state table 5.5) | Status guard | yes |
| `SnapshotPurgeJob` | monthly, 1st 03:00, `default` | Deletes snapshot/scenario forecasts and items older than 3 years (BR-CSH-48) and archived scenario results older than 12 months | Delete by id list; resumable | yes |
| `AccuracyJob` | Saturday 03:00 | For snapshots whose week ended, compute absolute error vs entered bank balance; upserts `rm_forecast_accuracy` | Upsert `(forecast_id, week_no)` | yes |
| `NameRefreshJob` | nightly 02:30 | Refreshes `party_name_*` snapshots on open schedules and items from `core.parties` (contract query `ICoreLookup`) | Idempotent update | yes |
| `CommitmentConsistencyJob` | nightly 03:30 | Recomputes `invoiced_amount`/`remaining_amount` from `commitment_invoice_links`; compares open PO count with `procurement` contract query `IPurchaseOrderQuery.GetOpenPoIds`; logs and repairs mismatches; raises `ops` alert when > 0.5% mismatch | Recomputation, not delta | yes |
| `RmRebuildJob` | nightly 04:00, `reports` | Rebuilds `rm_cash_project_summary`, `rm_trade_cash`, `rm_company_cash_weekly` from tables (parameterised `rebuild_rm_cash_*`) | Truncate-and-insert in one transaction per project | yes |
| `ExportJob` (R2) | on request | Builds accounting export / ETA-ready file, stores in platform files, sets batch `done` | `batch_id` status guard | yes |
| `StatementParseJob` (R2) | on upload | Parses statement file, builds lines, runs `StatementMatcher` suggestions | Import status guard; line unique key | yes |
| `CollectionHistoryJob` (R3) | Sunday 03:00 | Refreshes per-client collection statistics (`avg_days_late`, `stddev`, sample size) | Upsert by client | yes |

Job timeouts: refresh 60 s per forecast; nightly rebuild 30 min per tenant; exports 15 min. The refresh job is cheap for typical tenants (see section 15.4).

---

## 10. Permissions

Codes follow `module.resource.action`. Roles use 00 §4.4. "Scope" means project membership (BR-X-09). Amount limits are not hard-coded here: the platform `approval_policy` governs write-off approvals (`cash.receivable_write_off`) and the Procurement batch approval governs payments.

| Permission | finance\_controller | owner\_executive | project\_manager | contracts\_manager | procurement\_officer | tenant\_admin | Notes |
| --- | --- | --- | --- | --- | --- | --- | --- |
| `cash.forecast.view` | all projects + company | all | own projects (payroll as category total) | own projects (no payroll, no bank balances) | no | no | Company scope (R2) only finance and executive |
| `cash.forecast.refresh` | yes | yes | no | no | no | no |  |
| `cash.forecast.snapshot` | yes | yes | no | no | no | no |  |
| `cash.forecast.settings.manage` | yes | no | no | no | no | no | floor, opening balance, lags |
| `cash.assumption.manage` | yes | no | no | no | no | no |  |
| `cash.receivable.view` | yes | yes | own | own | no | no |  |
| `cash.receivable.manage` | yes | no | no | no | no | no | override date, manual receivable, cancel |
| `cash.receivable.follow_up` | yes | no | own | own | no | no | follow-up notes only |
| `cash.receivable.request_write_off` | yes | no | no | no | no | no | approval by amount via platform |
| `cash.receivable.write_off` (approval right) | no | by `approval_policy` | no | no | no | no | not an endpoint: used by the approval engine to authorise the approver |
| `cash.payable.view` | yes | yes | own | no | own, supplier payables only | no |  |
| `cash.payable.plan` | yes | no | no | no | no | no | priority, planned pay date (effective immediately) |
| `cash.payable.propose` | yes | no | no | no | own | no | priority and **proposed** date (needs finance confirmation, BR-CSH-19) |
| `cash.commitment.view` | yes | yes | own | no | own | no |  |
| `cash.cheque.view` | yes | yes | no | no | no | no |  |
| `cash.cheque.manage` | yes | no | no | no | no | no |  |
| `cash.bank_account.view` | yes | yes | no | no | no | no |  |
| `cash.bank_account.view_number` | yes | yes | no | no | no | no | masked number otherwise omitted |
| `cash.bank_account.manage` | yes | no | no | no | no | no |  |
| `cash.payroll_plan.view` | yes | yes | no | no | no | no | PM receives category totals through `cash.forecast.view` |
| `cash.payroll_plan.manage` | yes | no | no | no | no | no |  |
| `cash.planned_item.view` / `.manage` | yes / yes | yes / no | no | no | no | no |  |
| `cash.proposal.view` | yes | yes | no | no | own (read-only, supplier lines) | no |  |
| `cash.proposal.generate` | yes | no | no | no | no | no |  |
| `cash.proposal.accept` | yes | yes (large amounts) | no | no | no | no | segregation: the accepter cannot be the generator on tenants with `approval.segregation` on |
| `cash.alert.view` | yes | yes | own | own | own (payable alerts) | no |  |
| `cash.alert.acknowledge` | yes | yes | no | no | no | no |  |
| `cash.aging.view` | yes | yes | own (receivables + payables) | own (receivables) | payables only | no |  |
| `cash.report.view` | yes | yes | own | own (receivable reports) | payment schedule only | no |  |
| `cash.report.export` | yes | yes | own | no | no | no | exports obey field rules (BR-X-06) |
| `cash.scenario.view` / `.manage` (R3) | yes / yes | yes / no | no | no | no | no |  |
| `cash.statement.manage` (R2) | yes | no | no | no | no | no |  |
| `cash.export.run` (R2) | yes | yes | no | no | no | no |  |

Notes:

1. **Hidden fields** (omitted, not nulled; BR-X-06): bank `account_no_masked` without `view_number`; payroll plan rows and any item with `category='payroll'` at row level for roles without `cash.payroll_plan.view` (drill-down returns the category total row `payroll` with `sourceRef` omitted); `withholdingAmount` is visible to finance, executive and procurement only. `foreman` and `superintendent` have no cash permission, so no cost or cash field reaches them.
2. **Project scope.** Every list adds `project_id in visible projects`. Company-level items (`project_id is null`: overhead, loans, taxes, company payroll) are visible only with company scope (finance, executive).
3. The `procurement_officer` sees a payable only for projects they belong to and only kinds `supplier_invoice`, `supplier_credit_note`.
4. The built-in role-to-permission seed is a platform migration owned by file 01; this table is the source for it (ticket CSH-030).

---

## 11. Notifications

Producers publish notification intents (`template_code`, recipients, params, link) per 00 §3.12; text is resolved on the server (`ar` default). In-app is always on; push/email per user preference; critical alerts bypass quiet hours only for `cash_shortfall` and `cash_cheque_bounced`.

| Template code | Recipients | Trigger | Channel | Arabic sample |
|---|---|---|---|---|
| `cash_shortfall` | `finance_controller`, `owner_executive` (`shortfall_notify_roles`) | `cash.shortfall.detected.v1` raised/re-fired | in-app, push, email | "تحذير سيولة: رصيد مشروع \{project\} المتوقع في الأسبوع \{week\} هو \{minBalance\} جنيه، أقل من الحد الأدنى \{floor\} بمقدار \{shortfall\}." |
| `cash_forecast_at_risk` | `finance_controller` | Conservative case below floor, base healthy (BR-CSH-05) | in-app, email | "في حال تأخر التحصيلات غير المؤكدة \{days\} يومًا سينخفض رصيد \{project\} إلى \{minBalance\} في الأسبوع \{week\}." |
| `cash_receivable_overdue` | `finance_controller`, `contracts_manager`, project manager | Receivable becomes overdue; severity upgrade at 31 and 61 days | in-app, push (high+), email | "المستخلص \{applicationNumber\} للمشروع \{project\} متأخر \{days\} يومًا بقيمة \{amount\} جنيه. أضف متابعة." |
| `cash_payment_due_unapproved` | `finance_controller`, PO owner | BR-CSH-32 | in-app, email | "الفاتورة \{invoiceNo\} للمورد \{supplier\} تستحق في \{dueDate\} ولم يتم اعتمادها للدفع (القيمة \{amount\})." |
| `cash_cheque_due_no_funds` | `finance_controller`, `owner_executive` (critical) | BR-CSH-31 | in-app, push, email | "الشيك رقم \{chequeNo\} بقيمة \{amount\} يستحق في \{dueDate\} والرصيد المتوقع لا يغطيه." |
| `cash_cheque_bounced` | `finance_controller`, `owner_executive`, project manager | Cheque marked bounced | in-app, push | "ارتد الشيك رقم \{chequeNo\} (\{counterparty\}) بقيمة \{amount\}: \{reason\}." |
| `cash_cheque_deposit_due` | `finance_controller` | Received cheque not deposited 7 days after due date (task) | in-app task | "شيك مستلم رقم \{chequeNo\} من \{drawer\} حان موعد إيداعه." |
| `cash_payment_blocked_party` | `finance_controller`, `procurement_officer` | Payment scheduled for a blocked party (BR-CSH-22) | in-app, email | "تمت جدولة دفعة للمورد المحظور \{supplier\}. راجع الدفعة \{paymentNumber\}." |
| `cash_exceeds_commitment` | `finance_controller`, `procurement_officer` | BR-CSH-26 | in-app | "فواتير أمر الشراء \{po\} تجاوزت قيمة الالتزام بمقدار \{excess\}." |
| `cash_receipt_exceeds` | `finance_controller` | BR-CSH-16 | in-app | "المبلغ المستلم لـ \{document\} أكبر من المستحق بمقدار \{excess\}." |
| `cash_proposal_accepted` | `finance_controller`, `owner_executive`, procurement payment owners | Proposal accepted | in-app, email | "تم اعتماد مقترح الدفعات \{proposalNumber\} بقيمة \{total\} لتاريخ \{payDate\}." |
| `cash_write_off_requested` | approver from policy | Write-off submitted | in-app task, email | "طلب إعدام الذمة المدينة \{number\} بقيمة \{amount\}. السبب: \{reason\}." |
| `cash_weekly_digest` | `finance_controller`, `owner_executive` | Saturday 07:00 (platform digest job reading `ICashQuery`) | email | "ملخص السيولة: الأسابيع الـ13 القادمة، أدنى رصيد \{minBalance\} في الأسبوع \{week\}؛ \{n\} مستخلصات متأخرة." |
| `cash_guarantee_expiring` | `finance_controller`, `contracts_manager` | `contracts.guarantee.expiring.v1` | in-app | "ضمان \{guarantee\} ينتهي في \{expiry\}." |

Notification rules: shortfall notice repeats only on re-fire (BR-CSH-36), never daily for the same unchanged shortfall; digest includes unchanged shortfalls once a week.

---

## 12. Error codes

Format per 00 §3.7: `code = cash.<snake_case>`. Every code has Arabic and English text in the catalogue.

| code | HTTP | when | Arabic message | English message |
|---|---|---|---|---|
| `cash.forecast_not_found` | 404 | No live forecast for scope/project (not activated, not visible) | لا توجد توقعات نقدية لهذا النطاق | No cash forecast exists for this scope |
| `cash.forecast_not_live` | 409 | Refresh requested on a snapshot/scenario forecast | لا يمكن تحديث نسخة مجمدة | Only a live forecast can be refreshed |
| `cash.forecast_scope_mismatch` | 422 | Compare two forecasts of different scope/project | لا يمكن مقارنة توقعات من نطاقين مختلفين | Forecasts have different scopes |
| `cash.refresh_rate_limited` | 429 | Manual refresh within 10 s | تم التحديث للتو، حاول بعد قليل | Refreshed just now, retry shortly |
| `cash.opening_balance_missing` (warning) | 200 | No opening balance or bank account for scope | الرصيد الافتتاحي غير محدد، تم اعتباره صفرًا | Opening balance not set, assumed zero |
| `cash.invalid_setting` | 422 | Setting outside range (horizon 1-13, floor \< 0, etc.) | قيمة الإعداد غير صحيحة | Invalid setting value |
| `cash.assumption_overlap` | 422 | Same kind/scope/party with overlapping validity | يوجد افتراض آخر في نفس الفترة | An assumption already covers this period |
| `cash.filter_not_allowed` | 400 | Filter or sort not whitelisted | عامل التصفية غير مسموح | Filter not allowed |
| `cash.invalid_state_transition` | 409 | Action not allowed in current status | الإجراء غير مسموح في الحالة الحالية | Action not allowed in the current status |
| `cash.receivable_not_found` / `cash.payable_not_found` | 404 | Unknown id or not visible; consumer sees payment for unknown invoice | السجل غير موجود | Record not found |
| `cash.receivable_not_open` | 422 | Override, follow-up or write-off on a closed receivable | الذمة المدينة مغلقة | The receivable is not open |
| `cash.override_reason_required` | 422 | Override without reason | سبب تعديل التاريخ مطلوب | A reason is required for the date override |
| `cash.manual_kind_not_allowed` | 422 | Manual receivable of kind `certificate`/`retention` | لا يمكن إنشاء هذا النوع يدويًا | This kind cannot be created manually |
| `cash.cancel_not_allowed` | 422 | Cancel on a receivable that is certified or less than 90 days stale | لا يمكن إلغاء هذه الذمة | Receivable cannot be cancelled |
| `cash.write_off_reason_required` | 422 | Write-off without reason or amount > outstanding | سبب الإعدام مطلوب | Write-off reason required |
| `cash.planned_date_in_past` | 422 | `plannedPayDate < today` | تاريخ الدفع المخطط في الماضي | Planned pay date is in the past |
| `cash.planned_reason_required` | 422 | Planned date after due date without reason | يلزم سبب لتأجيل الدفع بعد الاستحقاق | Reason required to pay after the due date |
| `cash.payable_not_open` | 422 | Plan on paid/cancelled/disputed payable | الدفعة غير مفتوحة | The payable is not open |
| `cash.cheque_duplicate` | 409 | Same bank account and cheque number already registered | رقم الشيك مسجل مسبقًا | Cheque number already registered |
| `cash.cheque_dates_invalid` | 422 | `dueDate < issueDate` | تاريخ الاستحقاق قبل تاريخ الإصدار | Due date before issue date |
| `cash.cheque_not_yet_due` | 422 | Deposit before due date without `allowEarly` | لم يحن موعد استحقاق الشيك | Cheque is not yet due |
| `cash.cheque_already_cleared` | 409 | Cancel/bounce a cleared cheque | الشيك تم صرفه | Cheque already cleared |
| `cash.allocation_exceeds_outstanding` | 422 | Allocations exceed payable/receivable outstanding or cheque amount | التخصيص يتجاوز المبلغ المستحق | Allocation exceeds the outstanding amount |
| `cash.allocation_direction_mismatch` | 422 | Issued cheque allocated to a receivable or the reverse | اتجاه التخصيص غير متوافق | Allocation direction mismatch |
| `cash.bank_account_code_duplicate` | 409 | Duplicate account code | رمز الحساب مستخدم | Account code already used |
| `cash.balance_date_in_future` | 422 | Balance as-of date after today | تاريخ الرصيد في المستقبل | Balance date is in the future |
| `cash.payroll_schedule_invalid` | 422 | Weekday/day-of-month inconsistent with frequency | جدول الرواتب غير صحيح | Invalid payroll schedule |
| `cash.planned_item_invalid` | 422 | Amount \<= 0, recurrence end before date | بند التدفق المخطط غير صحيح | Invalid planned item |
| `cash.no_live_forecast` | 422 | Proposal generated without forecast | لا توجد توقعات نقدية | No live forecast |
| `cash.pay_date_in_past` | 422 | Proposal pay date before today | تاريخ الدفع في الماضي | Pay date in the past |
| `cash.proposal_line_amount_invalid` | 422 | Proposed amount > outstanding or \<= 0 | مبلغ السطر غير صحيح | Invalid line amount |
| `cash.proposal_breaches_floor` | 422 | Accept would push a week below floor without acknowledgement | المقترح يخفض الرصيد تحت الحد الأدنى | Proposal breaches the minimum balance |
| `cash.proposal_empty` | 422 | Accept with no `include` line | لا توجد سطور مشمولة | No included lines |
| `cash.proposal_expired` | 409 | Accept after `expires_at` | انتهت صلاحية المقترح | Proposal expired |
| `cash.alert_already_resolved` | 409 | Acknowledge a resolved alert | التنبيه تم حله | Alert already resolved |
| `cash.alert_not_manually_resolvable` | 422 | Manual resolve of floor alert | هذا التنبيه يُغلق تلقائيًا | This alert closes automatically |
| `cash.explanation_not_available` | 422 | Explain requested in R1 | الشرح غير متاح في هذا الإصدار | Explanation not available in this release |
| `cash.export_too_large` | 422 | Export with synchronous flag over 5,000 rows | الملف كبير، سيتم إعداده في الخلفية | Export too large for direct download |
| `cash.export_period_invalid` | 422 | R2 export period overlaps a closed export or is > 12 months | فترة التصدير غير صحيحة | Invalid export period |
| `cash.statement_unreadable` | 422 | R2 statement parse failure | تعذر قراءة كشف الحساب | Statement cannot be read |
| `cash.statement_line_already_matched` | 409 | R2 | البند مطابق مسبقًا | Line already matched |
| `cash.statement_unmatched_lines` | 422 | R2 confirm with unmatched lines | توجد بنود غير مطابقة | Unmatched lines remain |
| `cash.scenario_empty` | 422 | R3 compute with no adjustments | لا توجد تعديلات في السيناريو | Scenario has no adjustments |
| `cash.scenario_adjustment_invalid` | 422 | R3 invalid target, both shift and date, moved amount > outstanding | تعديل السيناريو غير صحيح | Invalid scenario adjustment |
| `cash.defer_beyond_terms` (warning) | 200 | R3 deferral beyond terms + 90 days | التأجيل يتجاوز شروط الدفع | Deferral exceeds payment terms |
| `cash.prediction_insufficient_history` | 422 | R3 fewer than 3 samples | لا يوجد تاريخ كافٍ للتنبؤ | Not enough history to predict |
| `cash.proposal_critical_exceeds_cash` (warning) | 200 | Generated proposal cannot respect floor because priority-1 lines exceed available cash | الدفعات الحرجة تتجاوز السيولة المتاحة | Critical payments exceed available cash |
| `cash.precondition_failed` | 412 | Stale `If-Match` | تم تعديل السجل، أعد التحميل | Record changed, reload |
| `cash.idempotency_key_reuse` | 422 | Same key, different body (platform code `idempotency_key_reuse` shown with module prefix) | مفتاح التكرار مستخدم بطلب مختلف | Idempotency key reused with a different request |

---

## 13. Reports and read models

Read models are refreshed by event handlers in the same transaction (00 §3.15), rebuilt nightly by `RmRebuildJob`, and carry `as_of`. Reporting (03) reads them only through `ICashQuery`.

### 13.1 `rm_*` tables

| Table | Columns | Refresh |
|---|---|---|
| `rm_cash_project_summary` | `tenant_id, project_id (PK with tenant), as_of timestamptz, forecast_id, as_of_date, opening_balance_amount, floor_amount, min_balance_amount, min_balance_week_no, has_shortfall bool, first_shortfall_week_no, shortfall_amount, conservative_min_balance_amount, next30_in_amount, next30_out_amount, open_receivables_amount, overdue_receivables_amount, overdue_receivables_count, open_payables_amount, overdue_payables_amount, unapproved_due_count, open_alert_count, cheques_due_14d_count, currency` | Updated at the end of every forecast refresh; nightly rebuild |
| `rm_trade_cash` | `tenant_id, project_id, work_package_id, trade_name_ar, trade_name_en, paid_to_date_amount, unpaid_payables_amount, unpaid_commitments_amount, next30_out_amount, as_of` (three measures separate) | On payment/payable/commitment change (handler); nightly rebuild |
| `rm_forecast_accuracy` | `tenant_id, scope, project_id, week_start_date, snapshot_forecast_id, forecast_closing_amount, actual_closing_amount, abs_error_amount, pct_error, as_of` | `AccuracyJob` |
| `rm_company_cash_weekly` (R2) | `tenant_id, forecast_id, week_no, start_date, opening_amount, inflow_amount, outflow_amount, closing_amount, below_floor bool, project_count, as_of` | Company forecast refresh |
| `rm_receivable_aging` | `tenant_id, as_of_date, project_id, client_party_id, current_amount, d1_30_amount, d31_60_amount, d61_90_amount, over90_amount, retention_amount` | Nightly rebuild (aging for arbitrary dates is computed live from `receivables`) |
| `rm_payable_aging` | same without retention, by `party_id` | Nightly rebuild |

Indexes: PK `(tenant_id, project_id)` or the natural key shown; `rm_trade_cash (tenant_id, project_id, work_package_id)`.

### 13.2 `ICashQuery` contract (for Reporting)

`GetProjectCashSummary(projectId)` (from `rm_cash_project_summary`), `GetCompanyCashSummary()` (R2), `GetNextShortfall(scope, projectId?)`, `GetOpenItems(direction, projectId?, limit)`, `GetTradeCash(projectId)`. These carry the names Reporting (03 section 8) expects (`GetProjectCash`, `GetPortfolioCash`, `GetOpenItems`); alignment is open question 9.

### 13.3 Reports (list and export; CSV/XLSX/PDF, Arabic headers, RTL PDF)

| Report | Columns | Filters |
|---|---|---|
| Cash forecast (13 weeks) | week no, start, end, opening, inflow, outflow, closing, floor, below floor flag, conservative closing, plus drill-down sheet of items (date, direction, category, reference, party, amount, confidence, date basis) | scope, project, view base/conservative, as-of (snapshot) |
| Cash forecast (12 months) | month, opening, inflow, outflow, closing | scope, project |
| Aging (receivables, payables) | counterparty, project, current, 1-30, 31-60, 61-90, over 90, retention (receivables), total | as-of date, group by, project |
| Payment schedule | pay date, payable no, supplier, project, priority, due date, planned date, outstanding, status | date range, project, priority |
| Receipts and payments | value date, direction, category, party, project, reference, amount, bank account | date range, project, direction |
| Cash by project | project, opening, min balance, min week, shortfall, next 30 days in/out, overdue receivables | active projects |
| Cash by trade (paid and unpaid committed) | trade, paid to date, unpaid payables, unpaid commitments, next 30 days | project |
| Cheque register | register no, direction, cheque no, bank, counterparty, amount, issue date, due date, status, days to due | status, bank, due range |
| Forecast accuracy | week, forecast closing, actual closing, abs error, % error | date range |
| Receivable follow-up log | receivable, client, channel, note, promised date, next follow-up | project, client |

Every printed forecast carries as-of date, `computedAt`, formula version, assumptions list and the sentence "expected receipts are not guaranteed cash" (BR-CSH-47).

---

---

## 15. Test scenarios, non-functional notes, open questions

### 15.1 Golden scenario G1: the week-8 shortfall (drives CSH-016, CSH-017, CSH-019, CSH-021, CSH-045)

**Inputs.** Project forecast, `as_of_date` Saturday 2026-10-10, week 1 starts 2026-10-10, base currency EGP, no collection lag, no haircut, floor 600,000, opening balance 2,400,000.

Sources:

- Receivable `PA-04`: `certified`, payable **2,352,590** (09 golden: gross 2,445,500; advance recovery 244,550; retention 122,275; deductions 15,000; net 2,063,675; VAT 14% 288,915), due 2026-11-10 -> week 5, confidence high.
- Receivable `PA-05`: `expected` (submitted), payable **2,600,000**, expected date 2026-12-08 -> week 9, confidence medium.
- Payroll plan: weekly 150,000 (13 occurrences).
- Planned item (overhead): 100,000 monthly on the 25th -> 2026-10-25 (week 3), 2026-11-25 (week 7), 2026-12-25 (week 11).
- Supplier outflows (matched payables by due date plus PO commitments by BR-CSH-25), by week: 250,000; 300,000; 200,000; 300,000; 410,000; 450,000; 370,000; 510,000; 350,000; 300,000; 200,000; 300,000; 300,000.
- No other inflows. Subcontract payables are R2 and absent from this R1 fixture.

**Expected weekly result**

| Week | Dates (Sat-Fri) | Opening | Inflow | Payroll | Overhead | Suppliers | Outflow | Closing | Below floor 600,000 |
|---|---|---|---|---|---|---|---|---|---|
| 1 | 2026-10-10..16 | 2,400,000 | 0 | 150,000 | 0 | 250,000 | 400,000 | 2,000,000 | no |
| 2 | 10-17..23 | 2,000,000 | 0 | 150,000 | 0 | 300,000 | 450,000 | 1,550,000 | no |
| 3 | 10-24..30 | 1,550,000 | 0 | 150,000 | 100,000 | 200,000 | 450,000 | 1,100,000 | no |
| 4 | 10-31..11-06 | 1,100,000 | 0 | 150,000 | 0 | 300,000 | 450,000 | 650,000 | no |
| 5 | 11-07..13 | 650,000 | **2,352,590** (PA-04) | 150,000 | 0 | 410,000 | 560,000 | 2,442,590 | no |
| 6 | 11-14..20 | 2,442,590 | 0 | 150,000 | 0 | 450,000 | 600,000 | 1,842,590 | no |
| 7 | 11-21..27 | 1,842,590 | 0 | 150,000 | 100,000 | 370,000 | 620,000 | 1,222,590 | no |
| 8 | 11-28..12-04 | 1,222,590 | 0 | 150,000 | 0 | 510,000 | 660,000 | **562,590** | **yes (short 37,410)** |
| 9 | 12-05..11 | 562,590 | **2,600,000** (PA-05) | 150,000 | 0 | 350,000 | 500,000 | 2,662,590 | no |
| 10 | 12-12..18 | 2,662,590 | 0 | 150,000 | 0 | 300,000 | 450,000 | 2,212,590 | no |
| 11 | 12-19..25 | 2,212,590 | 0 | 150,000 | 100,000 | 200,000 | 450,000 | 1,762,590 | no |
| 12 | 12-26..01-01 | 1,762,590 | 0 | 150,000 | 0 | 300,000 | 450,000 | 1,312,590 | no |
| 13 | 2027-01-02..08 | 1,312,590 | 0 | 150,000 | 0 | 300,000 | 450,000 | 862,590 | no |

Checks (assert in CSH-045): outflows weeks 1-8 total 4,190,000 = 2,400,000 + 2,352,590 - 562,590; `minBalance = 562,590` in week 8; `firstShortfallWeekNo = 8` (week 4 closes at 650,000, above floor); `shortfallAmount = 600,000 - 562,590 = 37,410`; week-13 closing 862,590; total inflow 4,952,590; total outflow 6,490,000 (2,400,000 + 4,952,590 - 862,590 = 6,490,000: 4,190,000 + 2,300,000).

**Conservative case** (`conservative_delay_days` 14; PA-05 is medium confidence so it moves from 2026-12-08 to 2026-12-22, week 11; PA-04 is high so it stays): closings weeks 1-8 unchanged; week 9 62,590; week 10 -387,410; week 11 1,762,590; week 12 1,312,590; week 13 862,590; `conservativeMinBalance -387,410` in week 10. Only a base-case shortfall publishes the event; the conservative result is shown in the response and as `forecast_at_risk` information.

**Event assertion.** `cash.shortfall.detected.v1` payload contains `firstShortfallWeekNo 8`, `weekStartDate 2026-11-28`, `minBalance 562,590`, `floor 600,000`, `shortfallAmount 37,410`.

**Drill-down assertion.** `items?filter[periodNo]=8&filter[direction]=out` returns payroll 150,000 plus supplier items totalling 510,000 (for example one matched invoice payable 310,000 due 2026-12-02 and others), sum 660,000.

**What the controller learns from it.** The shortfall is 37,410, small compared with the 2,352,590 collected in week 5, and it ends only because PA-05 (medium confidence) is collected in week 9. Remedies the tool must make visible: defer 37,410 of week-8 supplier payments by one week (R3 scenario), or collect PA-05 earlier, or confirm a loan drawdown planned item.

### 15.2 Named test scenarios

| # | Scenario | Inputs | Expected |
|---|---|---|---|
| G2 | PA-04 lifecycle | submitted (payable 2,352,590), then certified (2,352,590, due 2026-11-10), invoice 2,352,590, receipt 2,352,590 on 2026-11-12 | statuses `expected` -> `certified` -> `invoiced` -> `received`; confidence `medium` -> `high`; item in week 5 until received then gone; one `cash_actuals` row `client_receipt` 2,352,590 |
| G3 | Partial receipts | PA-04 receipts 1,000,000 then 1,352,590 | `partly_received` outstanding 1,352,590, forecast item 1,352,590; then `received`, no item |
| G4 | Late client | PA-04 override expected date to 2026-12-10 | week-5 inflow disappears; week 9 inflow 2,352,590 + 2,600,000; first shortfall week 5 (week 5 closing 90,000, weeks 6-8: -510,000, -1,130,000, -1,790,000) |
| G5 | PO commitment to payable | PO-1066 179,340 ex VAT, needed 2026-10-20, credit 30 days; invoice 100,000 ex VAT registered then matched, payable 114,000 due 2026-12-02 | commitment item 204,447.60 (2026-11-19) then 90,447.60; payable item 114,000 on 2026-12-02; commitment invoiced counted once |
| G6 | Credit note netting | supplier payable 114,000 and credit note -20,000 same project | one item 94,000 (credit netted); no `in` item |
| G7 | Dispute | invoice 114,000 disputed then resolved | item removed while disputed, restored on `matched` |
| G8 | Cheque no double count | issued cheque 100,000 allocated to payable 114,000 | payable item 14,000 + cheque item 100,000 on cheque due date |
| G9 | Received cheque | 500,000 due 2026-11-15, deposited 2026-11-16, clearing 2 days | inflow 2026-11-18 (week 5), after bounce item vanishes and receivable outstanding returns |
| G10 | Floor equality | closing exactly 600,000 | not a shortfall (strict) |
| G11 | Overdue roll | payable due 2026-10-01 outstanding 80,000, as of 2026-10-10 | item in week 1, `isOverdueRolled`; overdue receivable with expected date past moves to as-of + 7 days (basis `assumed`) |
| G12 | Alert re-fire | shortfall 37,410 -> 100,000 -> earlier week | events: first raise, re-fire (>= 50,000 increase), re-fire (earlier week); none when unchanged |
| G13 | Debounce | 100 payable events in 30 s | one recompute, one `refreshed` event |
| G14 | Aging boundaries | outstanding due 30, 31, 60, 61, 90, 91 days ago | buckets `1-30`, `31-60`, `31-60`, `61-90`, `61-90`, `over_90` |
| G15 | Payment proposal | candidates P1 80,000; P3 200,000; P3 300,000; available 450,000 | included 80,000 + 200,000 = 280,000; 300,000 deferred `floor_protection` |
| G16 | Write-off | 50,000 write-off approved | removed from forecast; audit; no reopen |
| G17 | Retention | release 340,000 approved, expected 2026-12-15 | `retention` receivable confidence `medium`, item week 10 |
| G18 | Exposure | VO-05 85,000 submitted | exposure shown, forecast unchanged; after billing in PA-05 the amount is inside PA-05 only |
| G19 | Scenario (R3) | delay PA-04 30 days | closings per 15.3 |
| G20 | Idempotent replay | replay all events of G1 | identical forecast and `input_hash` |
| G21 | Visibility | PM views drill-down | payroll shown as one category row; no bank account numbers; procurement officer `403` on forecast |
| G22 | Tenant isolation | tenants A and B with golden data | no cross-read or write |

### 15.3 Scenario derivation (R3, CSH-042 AC1)

Scenario "client pays PA-04 30 days late": PA-04 moves from 2026-11-10 to 2026-12-10 (week 9). Closings: week 4 650,000; week 5 650,000 - 560,000 = 90,000; week 6 -510,000; week 7 -1,130,000; week 8 -1,790,000; week 9 -1,790,000 + 2,352,590 + 2,600,000 - 500,000 = 2,662,590 (identical to the base case from week 9 on, because both receipts are in by then). `scenarioMinBalance -1,790,000` (week 8), `deltaMinBalance = -1,790,000 - 562,590 = -2,352,590`, `scenarioFirstShortfallWeekNo 5` against base 8. The cash flow module therefore reports that a 30-day delay on a single certificate moves the first shortfall from week 8 to week 5 and deepens it from 37,410 to 2,390,000 below the floor (600,000 + 1,790,000).

Scenario "defer week-8 supplier payments by one week": move 37,410 of the 510,000 week-8 supplier items to week 9 (`moved_amount 37,410`, `shift_days 7`): week 8 closing 600,000 (not below floor, equality is not a shortfall), week 9 closing 2,662,590 (same as base, total outflow by week 9 is unchanged); no shortfall remains.

### 15.4 Non-functional notes

| Topic | Note |
|---|---|
| Volumes (per tenant per year) | 50 projects; 12 applications each (600 receivables); 12,000 supplier payables; 10,000 POs; 100,000 `cash_actuals`; live forecasts 51 (50 projects + company) with about 13 + 12 period rows and up to 3,000 items each; weekly snapshots 52 x 51 forecasts x 3,000 items = about 8 million item rows per year at the upper bound, hence the 3-year purge and the `(tenant_id, forecast_id, period_no, direction)` index. Store snapshots' items compressed only if row count passes 50 million (partition by `created_at`, plan not v1). |
| Query patterns | Live headline by `(tenant, scope, project, kind)`; items by `(forecast, period_no, direction)` (drill-down); open receivables/payables by partial indexes on `expected_date`/`due_date`; aging by grouping on open rows; trade view from `rm_trade_cash`. |
| Latency targets | Headline and periods p95 \< 300 ms; drill-down page (50 rows) p95 \< 500 ms; list endpoints p95 \< 800 ms; project forecast recompute p95 \< 2 s; company forecast recompute \< 20 s; refresh debounce 60 s; outbox lag \< 5 s. |
| Concurrency | Forecast recomputation holds an advisory lock per forecast; handlers update schedule rows with `row_version` optimistic checks and retry on conflict (3 times). |
| Correctness | `ForecastCalculator` is pure and property-tested; `input_hash` makes recomputation reproducible (BR-X-10); the nightly rebuild recomputes from tables, so a missed event is repaired by the consistency job, not hidden. |
| Money | All amounts `numeric(18,4)`; rounding `MidpointRounding.AwayFromZero` at the 4th decimal when applying FX, VAT and haircut; the golden VAT figures are exact. Totals are sums of rounded items, never rounded sums. |

### 15.5 Security notes

1. Cash data is among the most sensitive in the product: all endpoints require authentication, MFA is mandatory for `finance_controller` and `owner_executive` (00 §3.8 **\[decide\]**), and no external user can reach the module.
2. Payroll data is category-level only (a weekly lump sum per plan): no employee names, ids or salaries are ever stored here, which keeps the module out of personal-data scope.
3. Bank account numbers are stored masked (last 4 digits); full numbers are never stored; `view_number` is a separate permission.
4. Audit: all writes (overrides, plans, cheques, settings, proposals, write-offs, balance edits) are in `platform.audit_log` with before/after; exports are audited (who, what, row count).
5. Cash never initiates a payment, so a compromised cash account cannot move money; it can only change forecast inputs, which are audited and versioned in snapshots.
6. Rate limit manual refresh and export endpoints per user; exports to PDF/XLSX escape formula injection (cells starting with `=`, `+`, , `@` are prefixed).
7. Event consumers validate `tenantId` of the envelope against the schedule row's tenant before applying (CSH-044 AC3).

### 15.6 Open questions (for the business)

1. **Which balance is "opening"?** Should the project forecast start from cash allocated to the project (a manual figure) or should the company hold one bank pool and allocate nothing to projects? The answer decides whether project forecasts are comparable to the company forecast.
2. **Floor policy.** Is the minimum balance one company number, one per project, or a number of weeks of payroll and overheads? Should a breach of the floor alert the owner or only the finance controller?
3. **Payment terms in practice.** Egyptian clients often pay long after certification. Should the default `collection_lag_days` be derived from each client's history from the start, or entered manually until the R3 prediction exists?
4. **Uncertified applications.** Should submitted but uncertified payment applications appear in the forecast at all in the base case, at a discount (haircut), or only in an "optimistic" view? The golden example includes PA-05 at full value with confidence "medium".
5. **Cheque practice.** Do you routinely deposit received post-dated cheques on the due date, and how many days until funds are available? Do suppliers present issued cheques on the due date or later? This drives `cheque_clearing_days` and the issued-cheque date.
6. **Accounting export.** Which accounting system and chart of accounts do your accountants use, and is a journal export enough or do they need sub-ledger detail? Who confirms ETA invoice data fields?
7. **Bank statements.** Which banks and statement formats must be supported first (CSV, Excel, MT940, PDF only)?
8. **Retention timing.** When retention is released, is the cash expected on the release date or after a further payment term? Should held retention appear in the 12-month view as low-confidence income at the expected handover date?
9. **Reporting contract names.** File 03 expects `GetProjectCash` and `GetPortfolioCash`; this file offers `GetProjectCashSummary` and `GetCompanyCashSummary`. Product decision needed only if portfolio terminology differs from "company".
10. **Write-off authority.** Who approves write-offs and at what amounts (platform approval policy needs the thresholds)?
11. **Overdue assumption.** For a receivable whose expected date has passed, is "7 days from today" the right default, or should the system keep it in the current week until a user updates it?
12. **VAT on commitments.** Should commitment-based outflows include VAT (as modelled, because cash leaves with VAT) even when the contractor recovers input VAT through a tax return later? Does the company want a separate VAT settlement planned item?
13. **Foreign-currency payables.** Should open USD/EUR payables be revalued at today's rate in the forecast (not in R1) or kept at the invoice rate?
14. **Payment proposals.** Should the proposal be allowed to include invoices that are matched but not yet approved (setting `allow_proposal_unapproved`)?

<p />
