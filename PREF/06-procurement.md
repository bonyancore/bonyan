---
title: "06 Procurement"
doc_no: "06"
section: "Supply Chain"
status: "Proposal"
source: https://app.notion.com/p/a674cdd21296833199f081380c802500?pvs=204
exported: 2026-10-29 (Notion MCP)
---

# 06 — Procurement (Module 4): Backend Specification

**Key:** `PRC`  **Schema:** `procurement`  **Release:** R1 (stock/site stores, framework agreements and advanced supplier analytics are R2, marked in place) <br> **Reads with:** `00` (conventions; cited as "per 00 §x"), v3 business spec Module 4 (lines 549-589), flow F2 (lines 192-201), permission matrix (lines 920-946). Automated sourcing (4.4A) is file `07`; this file defines only the **hand-off contract** between the two (section 4.9 of this file and tickets PRC-E7). <br> **Audience:** backend team. Everything needed to build the module is in this file. Arabic terms appear in parentheses.

---

## 1. Purpose and business context

### 1.1 The business problem in plain language

A contractor does not manufacture anything; it **buys** most of what ends up in the building: cement, steel, pipes, cables, tiles, paint, equipment hire. In an Egyptian contractor today this buying is run from WhatsApp messages, paper delivery notes (إذن استلام) and Excel sheets. The consequences are the ones the owner complains about:

- The site needs 60 tonnes of cement in two days and nobody knows whether the trade still has budget for it.
- Three suppliers are phoned, prices are written on paper, the cheapest on paper is chosen, and the payment terms that make a slightly dearer offer cheaper in real cash terms are ignored.
- The supplier delivers 300 of 800 pipes on Monday, 250 on Thursday. Nobody can say, without phoning the store, how many are still due, so the foreman re-orders or the activity stops.
- The supplier's invoice arrives for 800 pipes although only 550 were received, or at a price 4% above the order, and the accountant pays it because it "looks right".
- At month end the cost report cannot say how much has been **ordered but not delivered**, how much has been **delivered but not invoiced**, and how much is **invoiced but unpaid**.

Module 4 turns this into one controlled chain: **purchase request → (RFQ and quotes, or automated sourcing) → purchase order → goods receipt → supplier invoice with three-way match → payment**. Each step is a record that carries the project, the trade (work package, أعمال) and the cost code. At each step the cost ledger (module `cost`, file 08) is told what happened, so the trade's committed, accrued, invoiced and paid figures move at the right moment (00 §4.3). Those four figures are **never added together** (BR-X-02).

### 1.2 Terms used in this file

| Term | Meaning |
|---|---|
| Site request (طلب موقع) | A foreman's need ("200 bags of cement for slab 3, needed Sunday"). Owned by module `planning` (file 05). Procurement receives it when the superintendent confirms it. |
| Purchase request, PR (طلب شراء) | The procurement-side record of a need, split into **purchase lines** the officer can source and order. Created from a confirmed site request or manually by the officer. |
| RFQ (طلب عرض أسعار) | Request for quotation sent to chosen suppliers with a deadline. In this file: the **manual path** for materials not covered by automated sourcing. |
| Quote (عرض سعر) | A supplier's priced reply to an RFQ line. |
| Purchase order, PO (أمر شراء) | The binding order to a supplier: lines, price, terms, delivery schedule. **Frozen once approved**; any change is a new **version** (amendment) with a reason. |
| Goods receipt, GRN (إذن استلام) | Record that goods arrived: quantity delivered, quantity accepted, quantity rejected, condition, photo, delivery note number. A PO is usually delivered in several lots (**partial deliveries**). |
| Supplier invoice (فاتورة مورد) | The supplier's bill. Matched to PO and receipts (**three-way match**) before it may be paid. |
| Three-way match | Check that what is invoiced (quantity and price) agrees with what was ordered (PO) and what was actually received (receipt). Small differences inside a **tolerance** pass; larger ones become exceptions. |
| VAT (ضريبة القيمة المضافة) | 14% by default, configurable tax code. Recoverable, so it is kept apart from cost: cost ledger receives amounts **excluding VAT** (00 §4.3). |
| Withholding (ضريبة الخصم من المنبع) | A percentage the buyer deducts from the supplier's payment and pays to the tax authority. Modelled as a tax code of type `withholding`. Egyptian rules must be confirmed with an accountant (00 §3.5, v3 open question 5). |
| Committed / accrued / invoiced / paid | Cost states: PO issued / goods received / invoice matched / payment executed. |
| Tolerance | Allowed difference (percentage or amount) inside which a delivery or an invoice is accepted without exception. |
| Segregation of duties | A user cannot approve a record they submitted (BR-X-07). |

### 1.3 How it fits the master lifecycle and flows

- **Upstream:** `planning` raises site requests (F2 steps 1-2). `cost` holds the budget by work package and cost code; Procurement reads it to show remaining budget and to run the **budget check** (F2 step 2/4). `core` holds suppliers (parties), materials, work packages, cost codes, tax codes and units. `sourcing` (file 07) produces recommendations for materials with automated sourcing (F2 step 3).
- **This module** owns F2 steps 4-7 in detail (review/PO, receipt, invoice match, payment scheduling and recording) and the manual alternative to step 3.
- **Downstream:** `cost` receives ledger-relevant events (committed, accrued, invoiced, paid). `cash` receives payables (invoice due dates, scheduled payments). `planning` receives goods receipts (material readiness for activities) and late-delivery information. `sourcing` receives issued POs, receipts and invoices as **price observations**. `reporting` reads the procurement register and trade position read models.
- **Done when (v3 line 588):** within a week an officer takes a foreman's request through RFQ, comparison, approved PO, partial delivery and invoice match, and the trade's committed and accrued cost change at each step. The golden scenario in section 15 is exactly that.

### 1.4 What the officer sees: the procurement register

The main web screen is the **procurement register** (سجل المشتريات). One row per **purchase line** (a PO line when there was no request). Each row sits in one of six **pipeline stages**, shown as chips with counts at the top of the screen:

`Request → Sourcing → Awaiting review → PO → Delivery → Invoiced`

| Stage | Meaning (precise rules in BR-PRC-60 and section 13) |
|---|---|
| Request | Line approved or submitted, nothing started yet (no sourcing event, no RFQ, no PO). |
| Sourcing | Automated sourcing is collecting offers, or a manual RFQ is open for quotes. |
| Awaiting review | An offer recommendation or a set of quotes is ready for the officer to approve, reject or adjust. |
| PO | A PO draft exists, is pending approval, or approved but not yet issued/acknowledged. |
| Delivery | PO issued; goods not fully received (includes partly received). |
| Invoiced | Fully received (or short-closed) and at least one matched invoice exists. Fully paid lines move to **Closed** and leave the pipeline counts. |

The officer clicks a chip to filter the list. Cost columns are visible only to roles with the price visibility (section 10).

### 1.5 Scope by release

| Capability | Release |
|---|---|
| Purchase requests (site-request driven and manual), budget check, duplicate detection | R1 |
| RFQ (manual path), quotes capture, manual comparison, award to PO | R1 |
| PO with versions, terms, delivery schedule, approval by amount, PDF ar/en, supplier confirmation | R1 |
| Goods receipt with partial deliveries, photos, tolerance, reversal, return notes | R1 |
| Supplier invoice, three-way match, VAT, withholding, disputes, credit notes | R1 |
| Payment batches and execution records (no bank integration; bank file export for manual upload) | R1 |
| Supplier master extension: status, documents, bank accounts, rating events and scorecard basics | R1 (rating event capture and basic scorecard); detailed analytics R2 |
| Supplier portal (RFQ reply, PO confirm, delivery note and invoice upload) | R1 basic, R2 full (file 15) |
| Stock and site stores, issue to work package, transfers | R2 (out of scope here) |
| Framework agreements and call-offs | R2 (hooks noted, not built) |
| Price benchmark, delivery-risk prediction | R3 |

---

## 2. Users and roles

Built-in roles per 00 §4.4. Matrix reference: v3 lines 920-946 (C create/edit, V view, A approve).

| Role | What they do in this module | Matrix |
|---|---|---|
| `foreman` | Records goods receipts on site (mobile, offline queue), sees POs and expected deliveries **without prices**, photographs delivered and rejected goods. Raises site requests (planning module, not here). | PO: V; Goods receipt: C; RFQ/quote/supplier: no access |
| `superintendent` | Confirms site requests (planning), records goods receipts, sees RFQs and suppliers (view), quantities only. | PO: V; Goods receipt: C; RFQ/quotes/supplier: V |
| `procurement_officer` | Primary user. Reviews requests, runs RFQs and manual comparisons, reviews sourcing recommendations, creates and issues POs within limit, follows up deliveries, registers supplier invoices (data entry), maintains supplier records. | RFQ/quotes/supplier: C; PO: C (issues within limit); Goods receipt: V; Invoice/payment: V |
| `project_manager` | Approves POs and amendments above the officer limit and over-budget POs for own projects. Sees register, trade view and receipts. | PO: A (limit) |
| `finance_controller` | Matches invoices, accepts variances, approves invoices for payment, builds and approves payment batches, records execution, verifies supplier bank accounts. Approves POs by limit. | PO: A (limit); Invoice and payment: C, A |
| `cost_qs_engineer` | Views everything for cost reconciliation; no create/approve rights in this module. | V |
| `owner_executive` | Approves large POs, large payments, hard-budget overrides. | PO: A (large); Invoice/payment: A (large) |
| `tenant_admin` | Configures procurement settings, terms templates, tolerances. | settings |
| `contracts_manager`, `document_controller`, `qa_qc_engineer`, `safety_officer`, `estimator` | No procurement rights by default (QA/QC may be granted `goods_receipt.view` for inspection evidence). | - |
| `supplier_user` (external, aud=portal) | Receives RFQs, submits quotes, confirms or declines POs, proposes delivery dates, uploads delivery notes and invoices, sees payment status of own invoices. Never sees budgets, other suppliers' quotes, or internal margins. | portal |

**Amount limits** are carried by `platform.project_members.limits` and the platform `approval_policy` (record type `procurement.purchase_order`, `procurement.purchase_order_amendment`, `procurement.supplier_invoice`, `procurement.payment_batch`); this file never hard-codes limits.

---

## 3. Services and components

Names are .NET types inside `Procurement.Core` unless stated. **Aggregate** = rich root with invariants and state machine; **CRUD** = plain handlers.

| Component | Kind | Responsibility |
|---|---|---|
| `PurchaseRequest` | Aggregate | Header + lines; origin, routing per line, status roll-up from lines; duplicate flag. |
| `SiteRequestConfirmedHandler` | Consumer | Consumes `planning.site_request.confirmed.v1`; creates a PR idempotently. |
| `BudgetCheckService` | Domain service | Computes available budget per (work package, cost code); returns result `ok / soft_overrun / hard_block / no_budget`; reads `rm_budget_cache` and the `cost` contract query. |
| `DuplicateRequestDetector` | Domain service | Same item/work package/quantity within N days (BR-PRC-03). |
| `Rfq` | Aggregate | RFQ header, lines, invitations, deadline, status. |
| `Quote` | Aggregate | Quote header/lines, normalised totals, revision chain. |
| `QuoteComparisonCalculator` | Calculator | Manual path: landed cost, credit value, hard filters, score, ranking (BR-PRC-14..19). Deterministic subset of the sourcing `ComparisonEngine`; shared formula spec in file 07. |
| `PurchaseOrder` | Aggregate | Header identity + versions + lines + delivery schedule; totals; state machine; amendment; short-close. |
| `PoTotalsCalculator` | Calculator | Line, VAT and total rounding (BR-PRC-22). |
| `PoApprovalCoordinator` | Application service | Builds approval request through `IApprovalService` (amount, hints), handles `platform.approval.*` for record types owned here. |
| `PoDocumentRenderer` | Service | Arabic/English/bilingual PO PDF (RTL) via platform PDF service; stores to `IFileService`. |
| `PoLineBalanceProjector` | Domain service | Maintains `purchase_order_line_balances` (received, rejected, invoiced, short-closed) in the same transaction as the cause. |
| `GoodsReceipt` | Aggregate | Receipt header/lines, tolerance enforcement, posting, reversal. |
| `ReceiptToleranceEvaluator` | Calculator | Over/under delivery decisions (BR-PRC-33..36). |
| `ReturnNote` | Aggregate (light) | Rejection/return to supplier, expected credit. |
| `SupplierInvoice` | Aggregate | Registration, tax maths, status, hold, approval for payment. |
| `InvoiceTaxCalculator` | Calculator | VAT, withholding, stamp, net payable (BR-PRC-41..44). |
| `ThreeWayMatcher` | Domain service | Match lines to PO and accepted receipts; tolerances; writes `invoice_match_lines` (BR-PRC-45..48). |
| `InvoiceDispute` | Aggregate (light) | Dispute lifecycle and credit-note resolution. |
| `PaymentBatch` | Aggregate | Batch with payments and allocations; approval; execution recording. |
| `PaymentAllocator` | Domain service | Allocates payments to invoices, updates outstanding amounts, PO payment status. |
| `SupplierProfile` | Aggregate (light) | Supplier status, documents, bank accounts, category links. Party identity stays in `core`. |
| `SupplierRatingRecorder` | Domain service | Turns receipts/invoices/schedules into `supplier_rating_events` and publishes `procurement.supplier.rated.v1`. |
| `DeliveryFollowUpService` | CRUD + service | Follow-up log, promised dates. |
| `LateDeliveryScanner` | Job | Finds overdue and at-risk deliveries; writes alerts and notifications. |
| `ProcurementAlertService` | Service | Writes/resolves `procurement_alerts` (duplicate, split order, price above recent, expired documents, etc.). |
| `SourcingHandoffGateway` | Adapter | Publishes request events for sourcing; consumes `sourcing.recommendation.ready/decided`; creates PO drafts (section 4.9). |
| `RegisterProjector` | Projector | Maintains `rm_procurement_register`, `rm_trade_position`, `rm_supplier_scorecard`, `rm_spend` (section 13). |
| `ProcurementSettingsService` | CRUD | Tolerances, modes, thresholds, terms templates. |
| `ProcurementQueries` (public in `Procurement.Contracts`) | Contract | `IProcurementQuery`: `GetOrderedReceivedInvoiced(projectId, workPackageId)`, `GetPurchaseLinesForRequest(requestId)`, `GetPoLineStatus(poId)`; used by planning, cost, sourcing. |
| Consumers | Handlers | `cost.budget.*`, `core.party.*`, `core.work_package.*`, `core.cost_code.*`, `core.material.*`, `platform.approval.*`, `planning.site_request.confirmed`, `planning.constraint.*`, `sourcing.recommendation.*` (section 8). |

Aggregates: `PurchaseRequest`, `Rfq`, `Quote`, `PurchaseOrder`, `GoodsReceipt`, `SupplierInvoice`, `PaymentBatch`. CRUD: supplier documents, bank accounts, terms templates, settings, follow-ups, alerts.

Note on bank accounts: supplier bank accounts and their verification are owned by `core` (file 02, `Party` aggregate, `IPartyDirectory.GetPrimaryBankAccount`, verified accounts only). Procurement never stores a bank account master; a payment stores `payee_bank_account_id` by value plus a masked snapshot (`payee_iban_masked`, `payee_bank_name`) taken at scheduling time. Where this file says "supplier bank accounts" for the finance role, it means the verification action in `core`.

---

## 4. Domain model

All tables live in schema `procurement`, carry the standard columns of 00 §3.2 (`id`, `tenant_id`, `created_at/by`, `updated_at/by`, `row_version`, `is_deleted`, `source`) and are protected by RLS on `tenant_id`. Only exceptions are stated. **Transactions are never deleted** (BR-X-04): cancel or reverse. `is_deleted` is used only on reference tables (`po_term_templates`, `supplier_documents`, `procurement_settings` rows). FKs inside the schema are real constraints; FKs to `core.projects`, `core.work_packages`, `core.parties`, `platform.users` are real; other cross-module ids are by value (00 §5.4). Money is `numeric(18,4)` with `currency` (default tenant base, `EGP`); quantities are `numeric(18,4)` with `unit_code`. Foreign-currency documents carry `fx_rate numeric(18,8)` and `fx_date`; every `*_base_amount` is `amount * fx_rate` rounded to 4 decimals and is what events and read models report (R1 cost ledger is base-currency only, file 08).

### 4.1 Enumerations

| Enumeration | Values and meaning |
|---|---|
| `pr.status` | `draft` (manual, being typed), `submitted` (waiting officer review), `approved` (reviewed, lines routed), `partly_ordered`, `ordered` (all lines on POs), `partly_delivered`, `delivered`, `closed`, `rejected`, `cancelled` |
| `pr_line.status` | `open`, `in_sourcing` (auto sourcing event or RFQ open), `awaiting_review` (recommendation or quotes ready), `po_draft`, `ordered`, `partly_delivered`, `delivered`, `closed`, `cancelled` |
| `pr_line.route` | `auto_sourcing` (sourcing module, file 07), `manual_rfq` (this module), `direct_po` (officer already knows supplier/price), `framework_call_off` (R2, reserved) |
| `pr.origin` | `site_request` (from planning event), `manual` (officer), `reorder` (R2 reserved) |
| `budget_check.result` | `ok`, `soft_overrun` (over budget, mode `warn`, reason mandatory), `hard_block` (over budget, mode `block`), `no_budget` (no budget line found), `not_checked` (mode `off` or cost unreachable) |
| `rfq.status` | `draft`, `sent`, `closed` (deadline passed or all replied), `awarded`, `cancelled` |
| `rfq_invitation.status` | `invited`, `viewed`, `quoted`, `declined`, `no_response` |
| `quote.status` | `received` (captured), `revised` (superseded by a later revision), `shortlisted`, `selected`, `rejected`, `expired`, `withdrawn` |
| `quote.channel` | `portal`, `email`, `whatsapp`, `phone`, `paper`, `upload` |
| `po.status` | `draft`, `pending_approval`, `approved` (approved, not yet sent), `issued` (sent to supplier; commitment posted), `acknowledged` (supplier confirmed), `partly_received`, `received`, `invoiced`, `paid`, `closed`, `cancelled` |
| `po_version.status` | `draft`, `pending_approval`, `approved`, `rejected`, `superseded`, `withdrawn` |
| `po_version.kind` | `original`, `amendment`, `short_close` |
| `po_line.close_reason` | `short_closed`, `cancelled`, `fully_received` |
| `po_ack.response` | `confirmed`, `confirmed_with_changes` (supplier proposes new dates or quantities), `declined` |
| `grn.status` | `draft` (saved on mobile, not posted), `posted`, `reversed`, `cancelled` (draft discarded) |
| `grn_line.condition` | `good`, `damaged`, `wrong_item`, `short_shelf_life`, `excess`, `quality_failed` |
| `return_note.status` | `draft`, `issued`, `collected`, `credit_received`, `closed`, `cancelled` |
| `invoice.kind` | `invoice`, `credit_note`, `debit_note` |
| `invoice.status` | `registered`, `matching` (matcher running or awaiting receipts), `exception` (needs finance action), `matched` (accepted by match or by variance acceptance), `on_hold`, `disputed`, `approved_for_payment`, `scheduled`, `partly_paid`, `paid`, `rejected`, `cancelled` |
| `invoice_match.line_result` | `matched`, `within_tolerance`, `price_variance`, `qty_over_received`, `qty_over_ordered`, `no_receipt`, `unordered_item`, `receipt_not_accepted` |
| `dispute.status` | `open`, `supplier_replied`, `resolved_credit_note`, `resolved_accepted`, `resolved_rejected`, `closed` |
| `payment_batch.status` | `draft`, `pending_approval`, `approved`, `released` (bank file exported or handed to bank), `partly_executed`, `executed`, `cancelled` |
| `payment.status` | `scheduled`, `released`, `executed`, `failed`, `cancelled`, `reversed` |
| `payment.method` | `bank_transfer`, `cheque`, `cash`, `other` |
| `supplier_profile.status` | `candidate`, `approved`, `on_hold`, `blocked`. Blocked/on-hold are mirrored from `core.party` where the party status governs (BR-PRC-70). |
| `supplier_document.type` | `commercial_register`, `tax_card`, `vat_certificate`, `insurance`, `iso_certificate`, `price_agreement`, `other`; each has `expires_on date null` |
| `rating_event.kind` | `on_time_delivery`, `late_delivery`, `quantity_accuracy`, `quality_rejection`, `price_variance_invoice`, `documents_complete`, `response_time`, `manual` |
| `alert.type` | `duplicate_request`, `duplicate_po`, `split_order_near_limit`, `price_above_recent`, `late_delivery`, `delivery_at_risk`, `receipt_over_ordered`, `invoice_not_matching`, `supplier_document_expired`, `supplier_document_expiring`, `over_budget`, `no_supplier_response`, `sourcing_stalled` |
| `alert.severity` / `alert.status` | `info`/`warning`/`critical`; `open`/`acknowledged`/`resolved`/`suppressed` |
| `stage` (register) | `request`, `sourcing`, `awaiting_review`, `po`, `delivery`, `invoiced`, `closed` |

### 4.2 Number series (per 00 §3.1)

| Series | Default format | Per | Used by |
|---|---|---|---|
| `PRC_PR` | `PR-0412` | tenant | `purchase_requests.pr_number` |
| `PRC_RFQ` | `RFQ-0088` | tenant | `rfqs.rfq_number` |
| `PRC_QT` | `QT-0301` | tenant | `quotes.quote_number` (internal; supplier's own reference in `supplier_quote_ref`) |
| `PRC_PO` | `PO-1066` | tenant | `purchase_orders.po_number` (version suffix is shown as `PO-1066 v2`, never a new number) |
| `PRC_GRN` | `GRN-2209` | tenant | `goods_receipts.grn_number` |
| `PRC_RTN` | `RTN-0031` | tenant | `return_notes.return_number` |
| `PRC_SI` | `SI-1520` | tenant | `supplier_invoices.registration_number` (internal; supplier's number in `supplier_invoice_no`) |
| `PRC_PB` | `PB-0045` | tenant | `payment_batches.batch_number` |
| `PRC_PAY` | `PAY-3310` | tenant | `payments.payment_number` |

### 4.3 Tables: purchase requests and budget check

#### `purchase_requests` (aggregate root `PurchaseRequest`)

| Column | Type | Null | Notes |
|---|---|---|---|
| `pr_number` | text | no | series `PRC_PR`; unique `(tenant_id, pr_number)` |
| `project_id` | uuid | no | FK `core.projects` |
| `origin` | text | no | `pr.origin` |
| `site_request_id` | uuid | yes | by value (planning); unique `(tenant_id, site_request_id)` where not null: idempotency of the consumer |
| `status` | text | no | `pr.status`; CHECK list |
| `requested_by` | uuid | no | foreman for site-origin; officer for manual |
| `confirmed_by` | uuid | yes | superintendent who confirmed the site request |
| `assigned_officer_id` | uuid | yes | responsible officer (routing rule: BR-PRC-02) |
| `needed_by_date` | date | yes | earliest line need date (derived, cached) |
| `priority` | text | no | `normal`, `urgent`; default `normal` |
| `notes` | text | yes |  |
| `duplicate_flag` | bool | no | default false; set by `DuplicateRequestDetector` |
| `submitted_at`, `approved_at`, `approved_by` | timestamptz/uuid | yes |  |
| `reject_reason` | text | yes | required when `rejected` |

Indexes: `(tenant_id, project_id, status)`; partial `(tenant_id, assigned_officer_id) where status in ('submitted','approved','partly_ordered')`; keyset `(tenant_id, created_at desc, id desc)`.

#### `purchase_request_lines`

| Column | Type | Null | Notes |
|---|---|---|---|
| `pr_id` | uuid | no | FK; `line_no int` unique per PR |
| `project_id`, `work_package_id`, `cost_code_id` | uuid | no | BR-X-01; work package must belong to the project (BR-PRC-01) |
| `zone_id` | uuid | yes | delivery zone on site |
| `site_request_line_id` | uuid | yes | by value |
| `material_id` | uuid | yes | `core.materials` by value; null for free-text items (then `item_description` mandatory and sourcing route is not `auto_sourcing`) |
| `item_description` | text | no | Arabic or English; copied from material at creation |
| `specification` | text | yes |  |
| `quantity` | numeric(18,4) | no | CHECK `> 0` |
| `unit_code` | text | no |  |
| `needed_by_date` | date | no |  |
| `estimated_unit_price` | numeric(18,4) | yes | budget rate or last price; visibility `price` |
| `estimated_amount` | numeric(18,4) | yes | `quantity * estimated_unit_price` |
| `route` | text | no | `pr_line.route`, set at approval (BR-PRC-04) |
| `status` | text | no | `pr_line.status` |
| `ordered_qty` | numeric(18,4) | no | default 0; sum of PO line quantities (current versions, not cancelled); CHECK `>= 0` |
| `delivered_qty` | numeric(18,4) | no | default 0; accepted receipts |
| `budget_status` | text | yes | last `budget_check.result` |
| `sourcing_event_ref` | uuid | yes | `sourcing` event id by value |
| `rfq_id` | uuid | yes | FK `rfqs` when `manual_rfq` |
| `duplicate_of_line_id` | uuid | yes | self FK, set by detector |
| `photo_file_ids` | uuid\[\] | yes | copied from the site request |

Index: `(tenant_id, project_id, work_package_id, status)`, `(tenant_id, material_id, project_id)`, partial `(tenant_id, status) where status in ('open','in_sourcing','awaiting_review')`.

#### `budget_checks` (append-only log; no `updated_*`)

One row per check execution, so the decision and reason are reproducible (BR-X-10).

| Column | Type | Null | Notes |
|---|---|---|---|
| `subject_type` | text | no | `pr_line`, `po_version`, `po_amendment` |
| `subject_id` | uuid | no |  |
| `project_id`, `work_package_id`, `cost_code_id` | uuid | no |  |
| `level` | text | no | `work_package` or `cost_code`, copied from `cost.project_settings.budget_check_level` |
| `mode` | text | no | `off`, `warn`, `block` (from cost settings at check time) |
| `budget_amount` | numeric(18,4) | yes | approved budget at the level checked |
| `exposure_amount` | numeric(18,4) | yes | what the cost contract counts against the budget (formula owned by `IBudgetCheck`, file 08 section 6.4) |
| `proposed_amount` | numeric(18,4) | no | value of the subject (delta for amendments), ex VAT, base currency |
| `remaining_amount` | numeric(18,4) | yes | `budget_amount - exposure_amount` |
| `overrun_amount` | numeric(18,4) | no | `max(0, proposed - remaining - tolerance)`; default 0 |
| `tolerance_pct` | numeric(7,4) | no | from cost settings |
| `result` | text | no | `budget_check.result` |
| `basis` | text | no | `live` (cost contract answered) or `cache` (fallback to `rm_budget_cache`) |
| `reason` | text | yes | officer's reason (soft overrun) or override reason (hard block) |
| `override_by` | uuid | yes | user with `procurement.budget.override` |
| `checked_at` | timestamptz | no |  |

Index: `(tenant_id, subject_type, subject_id, checked_at desc)`.

### 4.4 Tables: RFQ and quotes (manual path)

#### `rfqs` (aggregate root `Rfq`)

| Column | Type | Null | Notes |
|---|---|---|---|
| `rfq_number` | text | no | unique per tenant |
| `project_id` | uuid | no |  |
| `title` | text | no |  |
| `status` | text | no | `rfq.status` |
| `deadline_at` | timestamptz | no | CHECK later than `sent_at`; supplier sees it in local time |
| `need_by_date` | date | yes | latest acceptable delivery date (hard filter, BR-PRC-16) |
| `delivery_site_id` / `delivery_address` | uuid / text | yes | project site or free address |
| `payment_terms_requested` | text | yes |  |
| `instructions` | text | yes | visible to suppliers |
| `attachment_file_ids` | uuid\[\] | yes | specs, drawings |
| `sent_at`, `sent_by` | timestamptz/uuid | yes |  |
| `awarded_at`, `awarded_by` | timestamptz/uuid | yes |  |
| `cancel_reason` | text | yes |  |

#### `rfq_lines`

| Column | Type | Null | Notes |
| --- | --- | --- | --- |
| `rfq_id` | uuid | no | FK; `line_no` unique per RFQ |
| `pr_line_id` | uuid | yes | FK; one PR line can be in one open RFQ line at a time (partial unique index on `pr_line_id` where RFQ status in `draft`,`sent`) |
| `material_id`, `item_description`, `specification` |  |  | copied |
| `quantity`, `unit_code` | numeric(18,4)/text | no |  |
| `work_package_id`, `cost_code_id` | uuid | no |  |
| `needed_by_date` | date | no |  |

#### `rfq_invitations`

| Column | Type | Null | Notes |
| --- | --- | --- | --- |
| `rfq_id` | uuid | no | FK |
| `supplier_id` | uuid | no | FK `core.parties`; unique `(rfq_id, supplier_id)`; must pass BR-PRC-05 (eligible supplier) |
| `status` | text | no | `rfq_invitation.status` |
| `channel` | text | no | `portal`, `email`, `link`, `whatsapp` (R2) |
| `access_token_hash` | text | yes | link access without account (hash only; token shown once) |
| `sent_at`, `viewed_at`, `responded_at` | timestamptz | yes |  |
| `decline_reason` | text | yes |  |

#### `quotes` (aggregate root `Quote`)

| Column | Type | Null | Notes |
|---|---|---|---|
| `quote_number` | text | no | unique per tenant |
| `rfq_id` | uuid | yes | FK; null for a stand-alone quote captured for a direct PO |
| `supplier_id` | uuid | no | FK `core.parties` |
| `supplier_quote_ref` | text | yes | supplier's own number |
| `revision_no` | int | no | default 1; `supersedes_id` self FK; `is_current` bool (00 §3.3) |
| `status` | text | no | `quote.status` |
| `channel` | text | no | `quote.channel` |
| `currency` | text | no |  |
| `fx_rate` | numeric(18,8) | no | default 1 |
| `valid_until` | date | yes | CHECK not before `received_on` |
| `received_on` | date | no |  |
| `payment_terms_code` | text | yes | `cash`, `net_30`, `net_60`, or tenant code |
| `credit_days` | int | no | default 0; CHECK `>= 0` |
| `advance_pct` | numeric(7,4) | no | default 0; CHECK 0..100 |
| `delivery_terms` | text | yes | `delivered_site`, `ex_works`, `supplier_transport_extra` |
| `lead_time_days` | int | yes | header default; lines may override |
| `vat_included` | bool | no | default false |
| `subtotal_amount`, `transport_amount`, `other_charges_amount`, `discount_amount` | numeric(18,4) | no | header charges, ex VAT; CHECK `>= 0` |
| `file_id` | uuid | yes | the supplier's PDF or photo, kept as evidence |
| `captured_by` | uuid | yes | null when portal |
| `rejection_reason` | text | yes |  |

Unique: `(tenant_id, rfq_id, supplier_id, revision_no)`; partial unique `(tenant_id, rfq_id, supplier_id) where is_current` (one current quote per supplier per RFQ).

#### `quote_lines`

| Column | Type | Null | Notes |
|---|---|---|---|
| `quote_id` | uuid | no | FK |
| `rfq_line_id` | uuid | yes | FK; unique `(quote_id, rfq_line_id)` |
| `offered_description` | text | yes | when supplier offers an alternative |
| `spec_match` | text | no | `exact`, `equivalent`, `partial`, `no_match`; set by officer; `no_match` is a hard filter |
| `quantity` | numeric(18,4) | no | quantity the supplier can supply; CHECK `> 0` |
| `unit_code` | text | no |  |
| `unit_price` | numeric(18,4) | no | ex VAT; CHECK `>= 0`; visibility `price` |
| `line_discount_pct` | numeric(7,4) | no | default 0 |
| `min_order_qty` | numeric(18,4) | yes |  |
| `lead_time_days` | int | yes | overrides header |
| `line_amount` | numeric(18,4) | no | `round(quantity * unit_price * (1 - line_discount_pct/100), 4)` |

#### `quote_comparisons` and `quote_comparison_lines` (manual comparison sheet, frozen at award)

`quote_comparisons`: `rfq_id`, `financing_rate_monthly_pct numeric(7,4)`, `weights jsonb` (`{"cost":70,"lead_time":15,"reliability":15}`), `formula_version text` (`manual-1`), `need_by_date date`, `computed_at`, `computed_by`, `is_frozen bool` (true at award), `frozen_inputs jsonb` (copies of each quote's prices, supplier reliability scores and settings at that time, BR-X-10), `award_reason text`, `award_override_reason text` (mandatory when the selected quote is not rank 1, BR-PRC-19). <br> `quote_comparison_lines`: one row per `(comparison, quote)`: `landed_amount`, `credit_value_amount`, `effective_amount`, `lead_time_days`, `excluded bool`, `exclusion_reason text` (`too_late`, `spec_no_match`, `supplier_blocked`, `expired`, `partial_quantity`, `below_min_order`), `cost_score`, `lead_score`, `reliability_score`, `total_score numeric(9,4)`, `rank int null`, `budget_delta_amount` (effective vs budget of the request lines). Unique `(comparison_id, quote_id)`.

### 4.5 Tables: purchase orders

#### `purchase_orders` (aggregate root `PurchaseOrder`; one row per PO identity)

| Column | Type | Null | Notes |
|---|---|---|---|
| `po_number` | text | no | unique per tenant; never reused |
| `project_id` | uuid | no | FK `core.projects` |
| `supplier_id` | uuid | no | FK `core.parties`; fixed after first issue |
| `status` | text | no | `po.status` |
| `current_version_id` | uuid | no | FK `purchase_order_versions`; the **approved/issued** version once approved, otherwise the draft |
| `pending_version_id` | uuid | yes | an amendment under edit or approval; at most one (BR-PRC-26) |
| `origin` | text | no | `manual`, `rfq_award`, `sourcing`, `site_request_direct` |
| `source_rfq_id`, `source_quote_id` | uuid | yes |  |
| `sourcing_recommendation_id`, `sourcing_offer_id` | uuid | yes | by value; unique `(tenant_id, sourcing_recommendation_id, sourcing_offer_id)` where not null: idempotency of the decision handler (section 4.9) |
| `issued_at`, `issued_by` | timestamptz/uuid | yes |  |
| `acknowledged_at` | timestamptz | yes |  |
| `closed_at` | timestamptz | yes |  |
| `delivery_status` | text | no | `none`, `partial`, `complete` (derived by projector) |
| `invoice_status` | text | no | `none`, `partial`, `complete` |
| `payment_status` | text | no | `unpaid`, `partial`, `paid` |
| `total_ordered_amount` | numeric(18,4) | no | ex VAT, current version, cached |
| `total_received_amount`, `total_invoiced_amount`, `total_paid_amount` | numeric(18,4) | no | all ex VAT, default 0; **separate figures, never summed** (BR-X-02) |
| `currency` | text | no |  |

Indexes: `(tenant_id, project_id, status)`, `(tenant_id, supplier_id, status)`, partial open `(tenant_id, project_id) where status in ('issued','acknowledged','partly_received')`.

#### `purchase_order_versions`

| Column | Type | Null | Notes |
|---|---|---|---|
| `po_id` | uuid | no | FK |
| `version_no` | int | no | 1 for original; unique `(po_id, version_no)` |
| `kind` | text | no | `po_version.kind` |
| `status` | text | no | `po_version.status` |
| `supersedes_id` | uuid | yes | previous approved version |
| `change_reason` | text | yes | mandatory when `version_no > 1` (BR-X-03) |
| `change_summary` | jsonb | yes | machine diff: `{"lines":[{"lineNo":1,"qty":[800,900],"unitPrice":[85,85]}]}` |
| `order_date` | date | no |  |
| `currency`, `fx_rate`, `fx_date` |  |  |  |
| `payment_terms_code` | text | no |  |
| `credit_days` | int | no | CHECK `>= 0` |
| `advance_pct` | numeric(7,4) | no | R1 stores the term only; advance payments are R2 (open question 4) |
| `delivery_terms` | text | no | `delivered_site`, `ex_works` |
| `delivery_site_id` / `delivery_address` | uuid/text | yes |  |
| `late_penalty_pct_per_day` | numeric(7,4) | yes | contractual term printed on the PO; R1 informational only (no automatic deduction) |
| `warranty_months` | int | yes |  |
| `terms_text_ar`, `terms_text_en` | text | yes | resolved from `po_term_templates` and frozen here |
| `subtotal_amount`, `discount_amount`, `transport_amount`, `vat_amount`, `total_amount` | numeric(18,4) | no | computed by `PoTotalsCalculator`; CHECK `total = subtotal - discount + transport + vat` |
| `base_total_amount` | numeric(18,4) | no | ex VAT in base currency, used for approval routing and commitments |
| `budget_check_id` | uuid | yes | FK `budget_checks` (latest binding check) |
| `approval_id` | uuid | yes | `platform.approvals` by value |
| `submitted_by`, `submitted_at` | uuid/timestamptz | yes | segregation of duties uses `submitted_by` |
| `approved_at`, `approved_by` | timestamptz/uuid | yes |  |
| `document_file_id` | uuid | yes | rendered PDF of this version |
| `source_comparison_id` | uuid | yes | FK `quote_comparisons` |

Partial unique: `(po_id) where status in ('draft','pending_approval') and version_no > 1` (one open amendment).

#### `purchase_order_lines`

| Column | Type | Null | Notes |
|---|---|---|---|
| `po_version_id` | uuid | no | FK; `po_id` repeated for joins |
| `line_no` | int | no | **stable across versions**; unique `(po_version_id, line_no)` |
| `pr_line_id` | uuid | yes | FK `purchase_request_lines`; a PO line may be fed by several PR lines through `po_line_pr_links` when consolidated (R2); R1 is one-to-one |
| `material_id` | uuid | yes |  |
| `item_description`, `specification` | text | no / yes | frozen copy |
| `quantity` | numeric(18,4) | no | CHECK `> 0` |
| `unit_code` | text | no |  |
| `unit_price` | numeric(18,4) | no | frozen; CHECK `>= 0`; visibility `price` |
| `discount_pct` | numeric(7,4) | no | default 0 |
| `tax_code_id` | uuid | no | `core` tax code of type VAT; rate copied in `vat_rate_pct numeric(7,4)` |
| `line_amount` | numeric(18,4) | no | `round(quantity * unit_price * (1 - discount_pct/100), 4)`, ex VAT |
| `vat_amount` | numeric(18,4) | no |  |
| `work_package_id`, `cost_code_id` | uuid | no | BR-X-01 |
| `receipt_required` | bool | no | default true; false for services, hire (two-way match) |
| `over_tolerance_pct`, `under_tolerance_pct` | numeric(7,4) | no | copied from settings or the material class at creation; editable by officer while draft |
| `needed_by_date` | date | no |  |
| `close_reason` | text | yes | `po_line.close_reason` |
| `quote_line_id` | uuid | yes | provenance |

Index: `(tenant_id, po_id, line_no)`, `(tenant_id, work_package_id, cost_code_id)`.

#### `po_delivery_schedule`

| Column | Type | Null | Notes |
| --- | --- | --- | --- |
| `po_version_id`, `line_no` | uuid/int | no | schedule rows belong to a version and a line |
| `slot_no` | int | no | unique per line |
| `planned_date` | date | no |  |
| `planned_qty` | numeric(18,4) | no | CHECK `> 0`; sum over slots must equal line quantity (BR-PRC-23) |
| `supplier_proposed_date` | date | yes | from supplier acknowledgement |
| `zone_id` | uuid | yes |  |
| `note` | text | yes |  |

#### `purchase_order_line_balances` (maintained by `PoLineBalanceProjector` in the same transaction as its cause)

PK `(tenant_id, po_id, line_no)`. Not a read model: it enforces rules (tolerance, invoice limits). Rebuildable from receipts, invoices, versions.

| Column | Type | Notes |
|---|---|---|
| `po_line_id` | uuid | current-version line row |
| `ordered_qty` | numeric(18,4) | quantity of the current approved version (after short-close = received) |
| `delivered_qty` | numeric(18,4) | sum of `delivered_qty` on posted receipt lines (what arrived) |
| `accepted_qty` | numeric(18,4) | sum of accepted (net of reversals); this is what is accrued |
| `rejected_qty` | numeric(18,4) |  |
| `invoiced_qty` | numeric(18,4) | quantity on matched invoice lines (net of credit notes) |
| `ordered_amount`, `accepted_amount`, `invoiced_amount`, `paid_amount` | numeric(18,4) | ex VAT, separate measures |
| `short_closed_qty` | numeric(18,4) | released commitment |
| `last_receipt_date` | date |  |
| `version_no` | int | the version the balance reflects |

CHECK: `accepted_qty >= 0`, `accepted_qty <= ordered_qty * (1 + over_tolerance)` is enforced in the domain (needs the tolerance), plus a DB CHECK `accepted_qty <= ordered_qty * 1.5` as a safety net.

#### `po_acknowledgements` (supplier confirmation)

`po_id`, `po_version_id`, `response` (`po_ack.response`), `responded_by_user_id` (external or internal user entering it), `channel` (`portal`, `email`, `phone`), `responded_at`, `proposed_changes jsonb` (per line new date/quantity), `comment`. An internal user can record a phone confirmation (`source='ui'`, flagged `entered_on_behalf=true`). A `declined` or `confirmed_with_changes` response opens a task for the officer (BR-PRC-28).

#### `po_term_templates` (CRUD reference)

`code`, `name_ar`, `name_en`, `payment_terms_code`, `credit_days`, `delivery_terms`, `terms_text_ar`, `terms_text_en`, `is_default`, `is_active`. Unique `(tenant_id, code)`.

### 4.6 Tables: goods receipt and returns

#### `goods_receipts` (aggregate root `GoodsReceipt`)

| Column | Type | Null | Notes |
|---|---|---|---|
| `grn_number` | text | no | unique per tenant; assigned at post (drafts show `client_ref` until then) |
| `client_ref` | uuid | no | generated on the device; unique `(tenant_id, client_ref)` (offline idempotency, file 15) |
| `project_id` | uuid | no |  |
| `po_id` | uuid | no | FK |
| `supplier_id` | uuid | no | copied |
| `status` | text | no | `grn.status` |
| `receipt_date` | date | no | date goods arrived; CHECK not in the future; period guard BR-X-05 |
| `delivery_note_no` | text | yes | supplier's delivery note number; unique per `(tenant_id, supplier_id, delivery_note_no)` where status = posted (duplicate delivery note rejected, BR-PRC-37) |
| `vehicle_ref` | text | yes | plate or driver name |
| `received_by` | uuid | no | foreman or superintendent |
| `site_zone_id` | uuid | yes |  |
| `posted_at`, `posted_by` | timestamptz/uuid | yes |  |
| `reversed_at`, `reversed_by`, `reversal_reason` |  | yes | reversal is a state change plus a mirror, never an edit |
| `reverses_grn_id` | uuid | yes | not used in R1 (reversal flips status and publishes `reversed`); kept for returns that are posted as negative receipts in R2 |
| `device_captured_at` | timestamptz | yes | when captured offline |
| `comment` | text | yes |  |

Indexes: `(tenant_id, po_id, status)`, `(tenant_id, project_id, receipt_date desc)`.

#### `goods_receipt_lines`

| Column | Type | Null | Notes |
|---|---|---|---|
| `grn_id` | uuid | no | FK; `line_no` unique |
| `po_id`, `po_line_no`, `po_line_id` | uuid/int/uuid | no | the version line current at posting time |
| `delivered_qty` | numeric(18,4) | no | what the truck brought; CHECK `>= 0` |
| `accepted_qty` | numeric(18,4) | no | CHECK `>= 0` |
| `rejected_qty` | numeric(18,4) | no | CHECK `accepted_qty + rejected_qty = delivered_qty` |
| `unit_code` | text | no | must equal PO line unit, or be convertible through `IUnitConverter` (then both stored) |
| `condition` | text | no | `grn_line.condition` |
| `rejection_reason` | text | yes | mandatory when `rejected_qty > 0` |
| `tolerance_decision` | text | no | `within`, `over_accepted_with_permission`, `under_noted`; default `within` |
| `accepted_unit_price` | numeric(18,4) | yes | price at posting (PO line price), hidden from site roles; used for the accrual value |
| `accepted_amount` | numeric(18,4) | yes | `round(accepted_qty * accepted_unit_price * (1 - discount), 4)` |
| `batch_ref`, `expiry_date` | text/date | yes | for cement, paint and similar |
| `note` | text | yes |  |

#### `goods_receipt_attachments`

`grn_id`, `grn_line_id null`, `file_id` (platform file), `kind` (`delivered_goods`, `delivery_note`, `rejected_goods`, `other`), `taken_at`, `gps jsonb null`. At least one `delivery_note` or `delivered_goods` attachment is required to post when `procurement_settings.require_receipt_evidence = true` (default true).

#### `return_notes` (light aggregate `ReturnNote`) and `return_note_lines`

`return_notes`: `return_number`, `po_id`, `grn_id`, `supplier_id`, `status`, `return_date`, `reason`, `expected_credit_amount` (ex VAT), `credit_note_invoice_id` (FK `supplier_invoices` when the supplier's credit note is registered), `collected_by_ref`. `return_note_lines`: `grn_line_id`, `quantity`, `unit_code`, `unit_price`, `amount`, `reason`, `photo_file_ids uuid[]`. A rejected quantity on a posted receipt line can generate a return note in one action.

### 4.7 Tables: supplier invoices and matching

#### `supplier_invoices` (aggregate root `SupplierInvoice`)

| Column | Type | Null | Notes |
|---|---|---|---|
| `registration_number` | text | no | series `PRC_SI`; unique per tenant |
| `kind` | text | no | `invoice.kind`; credit and debit notes reference `original_invoice_id` |
| `original_invoice_id` | uuid | yes | self FK; mandatory for `credit_note`/`debit_note` |
| `project_id` | uuid | no | single project per invoice in R1 (BR-PRC-40) |
| `supplier_id` | uuid | no | FK `core.parties` |
| `po_id` | uuid | yes | FK; an invoice normally references one PO; null only for invoices without PO (`no_po_reason` mandatory, routed to exception) |
| `supplier_invoice_no` | text | no | as printed; unique `(tenant_id, supplier_id, supplier_invoice_no)` where status not in (`rejected`,`cancelled`) (duplicate invoice guard, BR-PRC-41) |
| `invoice_date` | date | no |  |
| `received_date` | date | no | date the company received it (starts the payment term when the supplier terms say "from receipt") |
| `due_date` | date | no | computed by BR-PRC-43, editable with reason |
| `currency`, `fx_rate`, `fx_date` |  |  |  |
| `status` | text | no | `invoice.status` |
| `subtotal_amount` | numeric(18,4) | no | sum of line amounts ex VAT, before header discount |
| `discount_amount` | numeric(18,4) | no | header discount |
| `net_amount` | numeric(18,4) | no | `subtotal - discount`; **this is the figure sent to the cost ledger** |
| `vat_amount` | numeric(18,4) | no |  |
| `gross_amount` | numeric(18,4) | no | `net + vat` (must equal the printed total within BR-PRC-42 rounding) |
| `printed_total_amount` | numeric(18,4) | no | total printed on the invoice, entered by the clerk, used for the arithmetic check |
| `withholding_tax_code_id` | uuid | yes | `core` tax code of type `withholding` |
| `withholding_rate_pct`, `withholding_amount` | numeric | yes | amount deducted from payment, computed on `net_amount` by default (base configurable on the tax code) |
| `stamp_amount` | numeric(18,4) | no | default 0; stamp duty if a `stamp` tax code applies |
| `other_deduction_amount`, `other_deduction_reason` | numeric/text | no / yes | e.g. penalty agreed with supplier; reason mandatory when > 0 |
| `net_payable_amount` | numeric(18,4) | no | `gross - withholding - stamp - other_deduction` |
| `paid_amount` | numeric(18,4) | no | cash paid so far, default 0 |
| `outstanding_amount` | numeric(18,4) | no | `net_payable - paid_amount`; CHECK `>= 0` for kind `invoice` |
| `match_status` | text | no | `not_run`, `matched`, `within_tolerance`, `exception` |
| `match_run_at` | timestamptz | yes |  |
| `variance_accepted_by`, `variance_accepted_reason` | uuid/text | yes | finance acceptance (BR-PRC-47) |
| `matched_at`, `matched_by` | timestamptz/uuid | yes | `matched_by` is the system or the accepting user |
| `approved_for_payment_at`, `approved_by` | timestamptz/uuid | yes |  |
| `hold_reason` | text | yes |  |
| `file_id` | uuid | yes | scanned invoice |
| `registered_by` | uuid | no | officer or supplier portal user |
| `no_po_reason` | text | yes |  |

Indexes: `(tenant_id, project_id, status)`, `(tenant_id, supplier_id, status)`, partial `(tenant_id, due_date) where status in ('approved_for_payment','scheduled','partly_paid')` (payables), `(tenant_id, po_id)`.

#### `supplier_invoice_lines`

| Column | Type | Null | Notes |
|---|---|---|---|
| `invoice_id` | uuid | no | FK; `line_no` unique |
| `po_id`, `po_line_no` | uuid/int | yes | the PO line this invoice line claims; null = unordered item |
| `description` | text | no |  |
| `quantity` | numeric(18,4) | no | negative allowed only for credit notes (CHECK by kind) |
| `unit_code` | text | no |  |
| `unit_price` | numeric(18,4) | no |  |
| `discount_pct` | numeric(7,4) | no | default 0 |
| `line_amount` | numeric(18,4) | no | ex VAT |
| `tax_code_id`, `vat_rate_pct`, `vat_amount` |  | no |  |
| `work_package_id`, `cost_code_id` | uuid | yes | copied from the PO line when matched; entered by finance for unordered lines (BR-PRC-46) |

#### `invoice_match_lines` (written by `ThreeWayMatcher`; append per run, latest run flagged)

| Column | Type | Null | Notes |
|---|---|---|---|
| `invoice_id`, `invoice_line_id` | uuid | no | `run_no int` groups a run; `is_latest bool` |
| `po_line_no` | int | yes |  |
| `match_mode` | text | no | `three_way` or `two_way` (PO line `receipt_required=false`) |
| `ordered_qty`, `accepted_qty_to_date`, `invoiced_qty_before` | numeric(18,4) | no | snapshot for reproducibility (BR-X-10) |
| `matchable_qty` | numeric(18,4) | no | `accepted_qty_to_date - invoiced_qty_before` (three-way) or `ordered_qty - invoiced_qty_before` (two-way) |
| `invoice_qty` | numeric(18,4) | no |  |
| `po_unit_price`, `invoice_unit_price` | numeric(18,4) | no |  |
| `qty_variance`, `price_variance_pct`, `amount_variance` | numeric | no | `amount_variance = (invoice_unit_price - po_unit_price) * invoice_qty` |
| `result` | text | no | `invoice_match.line_result` |
| `tolerance_applied` | jsonb | no | `{"qtyPct":1,"pricePct":2,"amount":100}` as at run time |
| `accrued_amount_matched` | numeric(18,4) | no | accrual value of the quantity being invoiced (qty at PO price); sent to cost for the accrual true-up |

#### `invoice_disputes` (light aggregate `InvoiceDispute`)

`invoice_id`, `opened_by`, `opened_at`, `status` (`dispute.status`), `reason_code` (`price`, `quantity`, `quality`, `duplicate`, `wrong_project`, `other`), `description`, `disputed_amount numeric(18,4)`, `supplier_reply text`, `supplier_replied_at`, `resolution text`, `credit_note_invoice_id uuid null`, `resolved_by`, `resolved_at`. At most one open dispute per invoice (partial unique).

### 4.8 Tables: payments

#### `payment_batches` (aggregate root `PaymentBatch`)

| Column | Type | Null | Notes |
|---|---|---|---|
| `batch_number` | text | no | unique per tenant |
| `title` | text | no | e.g. "Payment run Sunday 12 Oct" |
| `status` | text | no | `payment_batch.status` |
| `planned_payment_date` | date | no |  |
| `paid_from_account_ref` | text | yes | company bank account reference (text label, R1; account master is in `cash`) |
| `method` | text | no | `payment.method` default for payments |
| `currency` | text | no | one currency per batch |
| `total_amount` | numeric(18,4) | no | sum of payment amounts (cash); CHECK `>= 0` |
| `approval_id` | uuid | yes |  |
| `submitted_by`, `approved_by`, `approved_at` |  | yes |  |
| `released_at`, `released_by` |  | yes | set when the bank file is exported |
| `export_file_id` | uuid | yes | the exported bank file (CSV in a generic layout; bank-specific layouts are R2) |

#### `payments`

| Column | Type | Null | Notes |
| --- | --- | --- | --- |
| `payment_number` | text | no | unique per tenant |
| `batch_id` | uuid | yes | FK; null for a single ad-hoc payment (still approved through the policy) |
| `supplier_id` | uuid | no |  |
| `project_id` | uuid | yes | null when one payment covers invoices of several projects (allocations carry the project) |
| `status` | text | no | `payment.status` |
| `method` | text | no |  |
| `currency`, `fx_rate`, `fx_date` |  |  |  |
| `amount` | numeric(18,4) | no | cash amount; CHECK `> 0`; equals sum of allocations |
| `scheduled_date` | date | no | feeds the payables schedule in `cash` |
| `executed_date` | date | yes | CHECK not before `scheduled_date - 30 days` (typo guard, warning) |
| `bank_reference` | text | yes | transfer or cheque number, mandatory at execution for `bank_transfer` and `cheque` |
| `payee_bank_account_id` | uuid | yes | `core` bank account by value; verified accounts only (BR-PRC-55) |
| `payee_iban_masked`, `payee_bank_name` | text | yes | snapshot |
| `executed_by` | uuid | yes |  |
| `failure_reason` | text | yes | for `failed` |
| `reversed_at`, `reversal_reason` |  | yes | bank returned the transfer |

#### `payment_allocations`

| Column | Type | Null | Notes |
| --- | --- | --- | --- |
| `payment_id`, `invoice_id` | uuid | no | unique `(payment_id, invoice_id)` |
| `po_id` | uuid | yes |  |
| `project_id`, `work_package_id`, `cost_code_id` |  |  | work package and cost code are **per invoice line** in the event, not here |
| `cash_amount` | numeric(18,4) | no | CHECK `> 0`; part of the invoice's `net_payable_amount` settled |
| `settled_fraction` | numeric(18,12) | no | `cash_amount / net_payable_amount` of the invoice; used to split the ledger amount |
| `paid_ex_vat_amount` | numeric(18,4) | no | ex-VAT cost portion reported to cost (BR-PRC-56) |

### 4.9 Hand-off contract with Automated Sourcing (file 07)

This section is the **only** coupling between this module and `sourcing`. Both modules code against it. Everything crosses as integration events (names fixed by 00 §7) or through contract queries; no module reads the other's tables.

**Direction 1: Procurement to Sourcing.** Event `procurement.purchase_request.approved.v1` fires when the officer approves a PR (or the system auto-approves a confirmed site request, BR-PRC-02). Lines with `route = auto_sourcing` are listed in the payload (section 8). Sourcing treats each line as one sourcing request. Procurement never calls sourcing synchronously. If sourcing does not answer within `sourcing_stall_hours` (default 4; urgent 1) the `SourcingStallScanner` raises alert `sourcing_stalled` and offers the officer **Switch to manual RFQ** (`POST /purchase-request-lines/{id}/route`), which sets `route=manual_rfq` and publishes nothing; sourcing ignores a later offer set for a line no longer routed to it (it receives the line status through `IProcurementQuery.GetPurchaseLinesForRequest`).

**Direction 2: Sourcing to Procurement.**

1. `sourcing.recommendation.ready.v1` -> line status `awaiting_review`, `register` stage becomes **Awaiting review**, task for the assigned officer (consumer `RecommendationReadyHandler`). Nothing else is stored; the recommendation itself stays in `sourcing`. The register row stores `recommendation_id` and deep-links to the sourcing review screen.
2. `sourcing.recommendation.decided.v1` with `decision` in `approved`, `adjusted`, `rejected`:
   - `approved` or `adjusted`: handler `RecommendationDecidedHandler` creates **one PO draft per supplier** (usually one) with `origin='sourcing'`. Prices, quantities, lead times, payment terms and transport are **copied from the decided offer snapshot in the event** (frozen evidence, v3 4.4A.5); later price-list changes in sourcing do not touch the PO. `adjusted` carries the officer's changed quantity or supplier and a mandatory `reason`. The PO draft's `submitted_by` is the deciding officer (`decidedBy`), so segregation of duties (BR-X-07) treats the officer as submitter. If `procurement_settings.auto_submit_sourced_po = true` (default) the PO is submitted at once: budget check, then approval routing by amount; if approval is not required it is approved and, when `auto_issue_sourced_po = true` (default), issued ("PO is created and sent automatically", F2 step 9). Over-limit and over-budget POs wait in `pending_approval`.
   - `rejected`: the line returns to `open` with `route=manual_rfq`, the reason is stored on the line (`sourcing_reject_reason`) and an alert is created if the officer chose "no supplier available".
   - Idempotency: unique `(tenant_id, sourcing_recommendation_id, sourcing_offer_id)` on `purchase_orders`; a duplicate event finds the PO and does nothing.
3. Price observations (layer 4 of the price data, v3 4.4A.1): `procurement.purchase_order.issued.v1` and `.changed.v1` carry unit prices, landed unit price, lead time, payment terms, material id and supplier id per line; sourcing stores them as observations of type `po`. Receipt and invoice prices are observations of type `receipt` and `invoice`; because the registry sends receipts only to cost, planning, reporting, the **invoice price observation** is supplied through `procurement.invoice.matched.v1` (proposed consumer addition, section 8.3).
4. Contract queries (`Procurement.Contracts`): `GetPurchaseLinesForRequest(requestId)`, `GetPoLineStatus(poId)`, `GetRecentPurchasePrice(materialId, projectId?, days)` (used by the sourcing recommendation to show "price vs the company's recent purchases"; returns last PO unit price, date, supplier; price visibility enforced by the caller's permission). Sourcing exposes nothing that procurement reads synchronously.

The manual path in this module (RFQ, quotes, `QuoteComparisonCalculator`) uses the same landed-cost and credit-value arithmetic as `ComparisonEngine` (file 07 owns the formula; BR-PRC-14..19 restate it so the golden example is testable here). Quotes captured manually are also published to sourcing as price observations through the PO that results from them (no separate event in R1).

### 4.10 Tables: suppliers, follow-up, alerts, settings

#### `supplier_profiles` (one row per supplier party; CRUD plus status rules)

`party_id` (FK `core.parties`, unique per tenant), `status` (`supplier_profile.status`), `status_reason`, `payment_terms_code`, `credit_days`, `credit_limit_amount numeric(18,4) null`, `delivers_to_sites bool`, `lead_time_days_typical int`, `tax_registration_checked_on date`, `blocked_at`, `blocked_by`, `notes`, `reliability_score numeric(7,4) null` (0..100, recomputed by `SupplierRatingRecorder`, null until 3 rated events, BR-PRC-72), `reliability_as_of timestamptz`.

`supplier_category_links`: `supplier_party_id`, `category_code` (material category from `core`), unique pair. Used by RFQ supplier selection and by sourcing eligibility (read through contract in R1 by sourcing: `IProcurementQuery.GetEligibleSuppliers(categoryCode, projectId)`).

#### `supplier_documents`

`party_id`, `type` (`supplier_document.type`), `file_id`, `document_no`, `issued_on`, `expires_on date null`, `verified_by`, `verified_at`, `status` (`valid`, `expiring` (within `document_expiry_warning_days`, default 30), `expired`, `rejected`). Index `(tenant_id, expires_on) where expires_on is not null`.

#### `supplier_rating_events` (append-only)

| Column | Type | Null | Notes |
| --- | --- | --- | --- |
| `supplier_id` | uuid | no |  |
| `kind` | text | no | `rating_event.kind` |
| `score` | numeric(7,4) | no | 0..100 normalised score of this event (BR-PRC-71) |
| `weight` | numeric(7,4) | no | default 1 |
| `po_id`, `po_line_no`, `grn_id`, `invoice_id` |  | yes | evidence links |
| `project_id` | uuid | yes |  |
| `detail` | jsonb | no | e.g. `{"plannedDate":"2026-10-12","actualDate":"2026-10-14","daysLate":2}` |
| `occurred_at` | timestamptz | no |  |
| `recorded_by` | uuid | yes | null for system events, user for `manual` |

Unique `(tenant_id, kind, grn_id, po_line_no)` where `grn_id is not null` and unique `(tenant_id, kind, invoice_id)` where `invoice_id is not null` (idempotent generation). Index `(tenant_id, supplier_id, occurred_at desc)`.

#### `delivery_follow_ups`

`po_id`, `po_line_no null`, `contacted_at`, `contacted_by`, `contact_channel` (`phone`, `whatsapp`, `email`, `visit`), `outcome` (`confirmed_date`, `delayed`, `no_answer`, `partial_shipped`, `cancelled_by_supplier`), `promised_date date null`, `note`. A `promised_date` updates `po_delivery_schedule.supplier_proposed_date` for the next open slot (no new PO version: promised dates are not contractual).

#### `procurement_alerts`

`alert_type`, `severity`, `status`, `project_id`, `subject_type`, `subject_id`, `supplier_id null`, `message_code`, `params jsonb`, `dedupe_key text` (unique per tenant while status in open/acknowledged), `raised_at`, `acknowledged_by/at`, `resolved_at`, `resolved_reason`. Index `(tenant_id, status, severity, raised_at desc)`.

#### `procurement_settings` (one row per tenant; project overrides in `procurement_project_settings` with the same columns where noted P)

| Column | Type | Default | Meaning |
|---|---|---|---|
| `duplicate_window_days` (P) | int | 7 | N in BR-PRC-03 |
| `default_over_tolerance_pct` (P) | numeric(7,4) | 5 | over-delivery tolerance copied to new PO lines |
| `default_under_tolerance_pct` (P) | numeric(7,4) | 5 | under-delivery tolerance (short-close without approval) |
| `invoice_qty_tolerance_pct` | numeric(7,4) | 1 | BR-PRC-45 |
| `invoice_price_tolerance_pct` | numeric(7,4) | 2 | BR-PRC-45 |
| `invoice_total_tolerance_amount` | numeric(18,4) | 100 | BR-PRC-45, base currency |
| `invoice_rounding_tolerance_amount` | numeric(18,4) | 1 | printed total vs computed (BR-PRC-42) |
| `withholding_base` | text | `net_ex_vat` | or `gross` (accountant decision) |
| `financing_rate_monthly_pct` | numeric(7,4) | 2 | credit-value rate for manual comparison; sourcing keeps its own copy in its policy, both are shown to the admin, open question 6 |
| `comparison_weights` | jsonb | `{"cost":70,"lead_time":15,"reliability":15}` |  |
| `reliability_default_score` | numeric(7,4) | 70 | used when a supplier has fewer than 3 rated events |
| `price_above_recent_pct` | numeric(7,4) | 5 | BR-PRC-18 alert threshold |
| `split_order_window_days` / `split_order_margin_pct` | int / numeric | 3 / 10 | BR-PRC-24 |
| `late_warning_days` | int | 2 | days before planned date to flag `delivery_at_risk` |
| `activity_start_alert_days` | int | 7 | v3: delivery late for an activity starting within 7 days |
| `require_receipt_evidence` | bool | true | BR-PRC-31 |
| `auto_submit_sourced_po`, `auto_issue_sourced_po` | bool | true, true | section 4.9 |
| `amendment_reduction_needs_approval` | bool | false | BR-PRC-27 |
| `sourcing_stall_hours` | int | 4 |  |
| `document_expiry_warning_days` | int | 30 |  |
| `block_po_for_expired_documents` | bool | false | warn by default, BR-PRC-70 |
| `vat_default_tax_code_id` | uuid | tenant VAT 14% |  |
| `invoice_without_po_allowed` | bool | false |  |
| `quote_validity_default_days` | int | 7 |  |

#### Read-model tables

Defined in section 13: `rm_procurement_register`, `rm_trade_position`, `rm_supplier_scorecard`, `rm_spend`, `rm_payables_due`, `rm_budget_cache`.

### 4.11 ER summary (text)

```txt
core.projects 1---* purchase_requests 1---* purchase_request_lines *---0..1 rfq_lines *---1 rfqs 1---* rfq_invitations
                                              |                                          rfqs 1---* quotes 1---* quote_lines
                                              |                                          rfqs 1---1 quote_comparisons 1---* quote_comparison_lines *---1 quotes
                                              +--(pr_line_id)--> purchase_order_lines *---1 purchase_order_versions *---1 purchase_orders
purchase_orders 1---* purchase_order_versions 1---* purchase_order_lines 1---* po_delivery_schedule
purchase_orders 1---* purchase_order_line_balances (po_id, line_no)      purchase_orders 1---* po_acknowledgements
purchase_orders 1---* goods_receipts 1---* goods_receipt_lines (po_id, po_line_no) ; goods_receipts 1---* goods_receipt_attachments
goods_receipts 1---* return_notes 1---* return_note_lines
purchase_orders 1---* supplier_invoices 1---* supplier_invoice_lines ; supplier_invoices 1---* invoice_match_lines ; supplier_invoices 0..1---* invoice_disputes
payment_batches 1---* payments 1---* payment_allocations *---1 supplier_invoices
core.parties 1---1 supplier_profiles 1---* supplier_documents ; supplier_profiles 1---* supplier_rating_events
budget_checks (log) <-- pr lines, PO versions
```

Cost-bearing keys on every transaction line: `project_id, work_package_id, cost_code_id, supplier_id` (BR-X-01).

---

## 5. State machines

Every transition is written to `platform.status_history` (00 §3.4) with actor and reason. Events named here are from the registry in 00 §7. "Ledger" means a cost entry created by `cost` when it consumes the event (00 §4.3); procurement never writes ledger rows. Permission codes are in section 10.

### 5.1 Purchase request (`pr.status`)

| From | Action | To | Guard | Who | Side effects |
| --- | --- | --- | --- | --- | --- |
| (none) | `create` (manual) | `draft` | project active; lines valid (BR-PRC-01) | officer | number `PRC_PR` |
| (none) | consumer of `planning.site_request.confirmed` | `submitted` | idempotent on `site_request_id` | system | event `purchase_request.created`; task to assigned officer; budget check per line (BR-PRC-06); duplicate scan (BR-PRC-03) |
| `draft` | `submit` | `submitted` | at least one line | officer | `purchase_request.created` |
| `submitted` | `approve` | `approved` | every line has `route`; soft overrun has reason | officer; or system when auto-approval applies (BR-PRC-02) | event `purchase_request.approved` (lines with `auto_sourcing` listed); lines move per route (5.2) |
| `submitted` | `reject` | `rejected` | `reject_reason` | officer | notify requester (foreman and superintendent) |
| `approved`/`partly_ordered`/`partly_delivered` | derived | `partly_ordered`, `ordered`, `partly_delivered`, `delivered` | roll-up of line statuses | system | none |
| `delivered`/`ordered` | derived | `closed` | all lines `closed` or `cancelled` | system | none |
| `draft`/`submitted`/`approved` | `cancel` | `cancelled` | no line on a PO that is issued | officer | open RFQ lines released; sourcing ignores the line (4.9) |

### 5.2 Purchase request line (`pr_line.status`)

| From | Action | To | Guard / trigger | Side effects |
| --- | --- | --- | --- | --- |
| `open` | PR approved with `route=auto_sourcing` or RFQ sent | `in_sourcing` | route set | RFQ line locked (partial unique index) |
| `open` | PR approved with `direct_po` | `po_draft` | officer starts PO | link `pr_line_id` |
| `in_sourcing` | `sourcing.recommendation.ready` or RFQ closed with at least one quote | `awaiting_review` | route-specific | task to officer |
| `in_sourcing`/`awaiting_review` | `POST .../route` to `manual_rfq` | `open` | no PO draft exists | alert `sourcing_stalled` resolved |
| `awaiting_review` | recommendation decided `rejected` | `open` | consumer | route forced to `manual_rfq` |
| `awaiting_review`/`open` | PO draft created | `po_draft` | PO line links line | none |
| `po_draft` | PO issued | `ordered` | `ordered_qty` = PO quantity | none |
| `ordered` | receipt posted | `partly_delivered` / `delivered` | `delivered_qty` vs `quantity` (accepted) | none |
| `partly_delivered` | PO line short-closed | `delivered` | BR-PRC-30 | none |
| any non-final | `cancel` | `cancelled` | not on issued PO | none |
| `delivered` | invoice matched and paid in full | `closed` | system | leaves pipeline counts |

### 5.3 RFQ (`rfq.status`) and invitation

| From | Action | To | Guard | Who | Side effects |
| --- | --- | --- | --- | --- | --- |
| `draft` | `send` | `sent` | at least 1 line and 1 invitation (BR-PRC-20); deadline in the future; invitees eligible (BR-PRC-05) | officer | invitations `invited`; portal and email notice; PR lines `in_sourcing` |
| `sent` | `close` / deadline job | `closed` | deadline passed or all invitations answered | officer / system | no-reply invitations `no_response` |
| `sent` | `extend_deadline` | `sent` | new deadline later | officer | notice to invitees |
| `closed` | `reopen` | `sent` | new deadline; no PO created | officer | audit |
| `closed` | `award` | `awarded` | comparison computed; override reason if not rank 1 (BR-PRC-19) | officer | comparison frozen; PO draft created per awarded supplier; losing quotes `rejected`; winner `selected` |
| `draft`/`sent`/`closed` | `cancel` | `cancelled` | not awarded; `cancel_reason` | officer | PR lines back to `open` |

Invitation: `invited -> viewed` (first open of link or portal view), `invited/viewed -> quoted` (quote saved), `-> declined` (supplier action, reason), `-> no_response` (RFQ closed).

### 5.4 Purchase order header (`po.status`) and version (`po_version.status`)

| From | Action | To | Guard | Who | Side effects |
|---|---|---|---|---|---|
| (none) | `create` | `draft` (version 1 `draft`) | supplier approved (BR-PRC-70); lines valid | officer; or system (sourcing) | number `PRC_PO` |
| `draft` | `submit` | `pending_approval` | schedule sums equal quantities (BR-PRC-23); budget check run (BR-PRC-25); documents warning; split-order scan (BR-PRC-24) | officer | approval request type `procurement.purchase_order`; `submitted_by` stored |
| `pending_approval` | `platform.approval.approved` | `approved` | approver is not `submitted_by` (BR-X-07) | approver by limit (PM, finance\_controller, owner\_executive) | version `approved`, `approved_by`; PDF rendered |
| `pending_approval` | `platform.approval.rejected` | `draft` | reason | approver | version back to `draft`; officer notified |
| `pending_approval` | `withdraw` | `draft` | submitter only | officer | approval cancelled |
| `approved` | `issue` | `issued` | supplier not blocked; period open for `order_date` | officer within limit | **`purchase_order.issued`** (ledger COMMITTED); `issued_at`; PDF sent through portal and email; balances created |
| `issued` | `record_acknowledgement` (`confirmed`) | `acknowledged` | version is current | supplier or officer on behalf | none; `confirmed_with_changes` / `declined` create a task (BR-PRC-28), status unchanged |
| `issued`/`acknowledged` | receipt posted | `partly_received` | accepted qty > 0, below total | system | none |
| `partly_received`/`issued`/`acknowledged` | receipt posted covering all lines | `received` | every line `accepted_qty >= ordered_qty * (1 - under_tolerance)` or short-closed | system | none |
| `received` | invoice matched covering all received quantity | `invoiced` | system | system | none |
| `invoiced` | payments cover all invoices | `paid` | `outstanding_amount = 0` for all invoices | system | none |
| `paid`/`received`/`invoiced` | `close` or automatic | `closed` | nothing outstanding or remainder short-closed | officer / system | **`purchase_order.closed`** |
| `draft`/`pending_approval`/`approved` | `cancel` | `cancelled` | not issued | officer | `purchase_order.cancelled` only if it had been issued |
| `issued`/`acknowledged` | `cancel` | `cancelled` | no receipts posted (accepted qty = 0), no invoices; reason; approval when value above officer limit | officer + approval | **`purchase_order.cancelled`** (ledger reverses COMMITTED) |

Amendment: from `issued`/`acknowledged`/`partly_received`, `POST /purchase-orders/{id}/amend` creates version `n+1` (`kind=amendment`, `draft`) copied from the current one and sets `pending_version_id`. `submit` -> `pending_approval` -> `approved` (version `approved`, previous `superseded`, `current_version_id` switches, `pending_version_id` cleared). The PO header status does not change during an amendment. On approval the PO is re-sent and **`purchase_order.changed`** fires with the line deltas (ledger COMMITTED delta). Rejection returns the amendment to `draft`; `withdraw` marks it `withdrawn`. <br> Short-close (`kind=short_close`, usually no approval within `default_under_tolerance_pct`, otherwise approval): releases the undelivered remainder; `purchase_order.changed` with negative delta.

### 5.5 Goods receipt (`grn.status`)

| From | Action | To | Guard | Who | Side effects |
| --- | --- | --- | --- | --- | --- |
| (none) | `create` (offline-safe via `client_ref`) | `draft` | PO issued or later; PO not cancelled | foreman, superintendent | none |
| `draft` | `post` | `posted` | evidence rule (BR-PRC-31); tolerance (BR-PRC-33..36); period open (BR-X-05); duplicate delivery note (BR-PRC-37) | foreman, superintendent | number `PRC_GRN`; balances updated; **`goods_receipt.posted`** (ledger ACCRUED + COMMITTED consumed); PO/line statuses; rating events; invoices waiting in `matching` re-queued; alert `receipt_over_ordered` when over |
| `draft` | `discard` | `cancelled` | author | creator | none |
| `posted` | `reverse` | `reversed` | no matched invoice consumes the quantity (BR-PRC-38); reason; period open | superintendent or procurement officer with permission | balances reduced; **`goods_receipt.reversed`**; rating events voided by compensating event |

### 5.6 Return note (`return_note.status`)

`draft -> issued` (officer, sends to supplier) `-> collected` (supplier collected goods) `-> credit_received` (credit note invoice linked, BR-PRC-39) `-> closed`. `cancel` allowed before `collected`.

### 5.7 Supplier invoice (`invoice.status`)

| From | Action | To | Guard | Who | Side effects |
| --- | --- | --- | --- | --- | --- |
| (none) | `register` | `registered` | duplicate guard (BR-PRC-41); arithmetic (BR-PRC-42); project/PO consistency (BR-PRC-40) | officer, finance\_controller, or supplier portal | number `PRC_SI`; due date (BR-PRC-43); **`invoice.registered`** (cash payable forecast) |
| `registered` | `match` (auto after register and on receipt events) | `matching` | PO known | system | match run written |
| `matching`/`registered` | match outcome all lines `matched`/`within_tolerance` | `matched` | BR-PRC-45 | system | **`invoice.matched`** (ledger INVOICED, accrual true-up) |
| `matching` | outcome has exceptions | `exception` |  | system | alert `invoice_not_matching`; task finance |
| `exception` | `accept_variance` | `matched` | reason; approver by limit (BR-PRC-47) | finance\_controller | **`invoice.matched`** with variance flag |
| `exception` | `rematch` | `matching` | new receipt or corrected data | finance/officer | new run |
| `exception`/`matching`/`matched` | `hold` | `on_hold` | reason | finance\_controller | removed from payables schedule |
| `on_hold` | `release_hold` | previous status |  | finance\_controller |  |
| `exception`/`matched`/`approved_for_payment` | `open_dispute` | `disputed` | reason code | finance, officer | **`invoice.disputed`**; payment blocked |
| `disputed` | `resolve` | `matched`/`rejected`/`exception` | per resolution (5.8) | finance |  |
| `matched` | `approve_for_payment` | `approved_for_payment` | approval by amount (type `procurement.supplier_invoice`); approver not registrant (BR-X-07); supplier not blocked | finance\_controller, owner\_executive (large) | invoice enters payable list |
| `approved_for_payment` | batch approved / payment created | `scheduled` | payment `scheduled` | system | **`payment.scheduled`** |
| `scheduled` | payment executed partially | `partly_paid` | allocation \< outstanding | system |  |
| `scheduled`/`partly_paid` | payment executed in full | `paid` | outstanding = 0 | system |  |
| `registered`/`exception`/`on_hold`/`disputed` | `reject` | `rejected` | reason | finance\_controller | releases quantity; duplicate key freed |
| any before `scheduled` | `cancel` | `cancelled` | no payment; **if previously matched: reversal of INVOICED** by corrective event `invoice.matched` with `reversal=true` | finance\_controller |  |

### 5.8 Dispute (`dispute.status`)

`open -> supplier_replied` (portal reply or recorded) `-> resolved_credit_note` (credit note registered and linked; invoice net reduced through the credit note, never edited) \| `resolved_accepted` (finance accepts invoice as is, variance accepted) \| `resolved_rejected` (invoice rejected). `closed` after follow-up. One open dispute per invoice.

### 5.9 Payment batch (`payment_batch.status`) and payment (`payment.status`)

| From | Action | To | Guard | Who | Side effects |
| --- | --- | --- | --- | --- | --- |
| (none) | `create` | `draft` | currency single; payments from `approved_for_payment` invoices (BR-PRC-52) | finance\_controller | payments `scheduled` after submit |
| `draft` | `submit` | `pending_approval` | bank accounts verified (BR-PRC-55); no overpayment (BR-PRC-58) | finance\_controller | approval request `procurement.payment_batch`; **`payment.scheduled`** per payment |
| `pending_approval` | approval decision | `approved` / `draft` | approver not submitter | finance\_controller (limit), owner\_executive (large) |  |
| `approved` | `release` | `released` | export file generated | finance\_controller | payments `released`; CSV export stored |
| `released`/`approved` | `record_execution` (per payment) | `partly_executed` / `executed` | `bank_reference` mandatory (BR-PRC-54) | finance\_controller | payment `executed`; **`payment.executed`** (ledger PAID); allocations update invoices, POs |
| `released` | `mark_failed` (per payment) | batch unchanged | reason | finance | payment `failed`; invoice returns to `approved_for_payment` |
| `executed` payment | `reverse` | payment `reversed` | bank returned funds; reason | finance | **`payment.executed`** with `reversal=true` (negative PAID) |
| `draft`/`pending_approval`/`approved` | `cancel` | `cancelled` | no executed payment | finance | invoices back to `approved_for_payment` |

---

## 6. Business rules

All amounts ex VAT in base currency unless stated; `round` = round half away from zero to 4 decimals (banker's rounding is not used). "Settings" are `procurement_settings` (section 4.10). Every calculation stores its inputs and formula version (BR-X-10).

### 6.1 Requests and budget check

| ID | Rule |
|---|---|
| BR-PRC-01 | A purchase line's `work_package_id` must belong to the line's `project_id`, the cost code must be active, and the project must be `active` (not closed). Violation: `procurement.work_package_project_mismatch`. |
| BR-PRC-02 | A PR created from `planning.site_request.confirmed` is `submitted` and assigned to the officer mapped to the project (or work package trade) in `project_members`; if none, to the tenant's `procurement_officer` with the fewest open PRs. Auto-approval: when every line has a material with automated sourcing enabled and the budget check is `ok` and no duplicate flag, the PR is approved by the system (`approved_by` null, `source='system'`) so the F2 step 3 starts without manual work; otherwise the officer approves. |
| BR-PRC-03 | Duplicate detection: another line (any PR or PO line not `cancelled`) with the same `material_id` (or identical normalised `item_description` when free text), the same `work_package_id` and a quantity within +/-10% created within `duplicate_window_days` sets `duplicate_flag`, `duplicate_of_line_id` and alert `duplicate_request`. It never blocks; the officer clears it with a note. The same check on PO creation raises `duplicate_po`. |
| BR-PRC-04 | On approval each line gets exactly one `route`: `auto_sourcing` if `material_id` is set and the material has `auto_sourcing_enabled` in `core`; `manual_rfq` if the material has none or is free text; `direct_po` only if the officer selects it. The officer may override the default; a route can be changed until a PO draft or open RFQ line exists. |
| BR-PRC-05 | Eligible supplier = `core.party` of type supplier, `supplier_profile.status = approved`, not `blocked`, and not `on_hold` (an `on_hold` supplier can be invited only by a user with `procurement.supplier.override_status` and a reason). A `candidate` supplier is eligible for RFQ invitation only; it cannot receive a PO until approved. |
| BR-PRC-06 | Budget check (`BudgetCheckService` calls `IBudgetCheck`, file 08 section 6.4) runs on PR line creation (informational), on PO `submit`, and on amendment `submit` with the delta. `remaining = budget - exposure`. Result: `ok` if `proposed <= remaining + tolerance_pct% of budget`; else `soft_overrun` when mode `warn` (reason mandatory, response carries warning `procurement.po_over_budget_soft`) or `hard_block` when mode `block` (error `procurement.po_over_budget`, passes only with `procurement.budget.override` and a reason, which creates `override_by`). `no_budget` is treated as `soft_overrun` for mode `warn` and `hard_block` for `block`. If the cost contract does not answer within 2 s the check falls back to `rm_budget_cache` and records `basis='cache'`. |
| BR-PRC-07 | Over-ordering guard: the sum of non-cancelled PO line quantities linked to a PR line may not exceed the PR line quantity by more than `default_over_tolerance_pct` unless the officer states a reason (`procurement.po_exceeds_request`, warning). A PO line quantity below the request is allowed (balance stays open on the PR line). |

### 6.2 RFQ, quotes and manual comparison

Formula version `manual-1`; identical arithmetic to `ComparisonEngine` in file 07. Inputs per quote: line amounts, header charges, `credit_days`, `advance_pct`, lead time, supplier reliability, settings.

| ID | Rule |
|---|---|
| BR-PRC-14 | Landed amount of a quote = `sum(line_amount) + transport_amount + other_charges_amount - discount_amount`, ex VAT. If `vat_included`, each `unit_price` is first divided by `1 + vat_rate`. Landed unit price = landed / total quantity. Quotes in foreign currency are converted with the quote's `fx_rate` before comparison. |
| BR-PRC-15 | Credit value = `landed * financing_rate_monthly_pct/100 * credit_days/30`. Advance cost = `landed * advance_pct/100 * financing_rate_monthly_pct/100 * lead_time_days/30`. **Effective amount = landed - credit\_value + advance\_cost.** |
| BR-PRC-16 | Hard filters (exclusion reasons): `too_late` if `lead_time_days` is later than the RFQ `need_by_date` (today + lead time > need date); `spec_no_match` if any line `spec_match = no_match`; `supplier_blocked`; `expired` if `valid_until < today`; `partial_quantity` if offered quantity \< requested on any line (shown, not ranked, unless the officer includes it manually); `below_min_order`. Excluded quotes get `rank = null`. |
| BR-PRC-17 | Score among non-excluded quotes: `cost_score = 100 * min_effective / effective`; `lead_score = 100 * max(min_lead,1) / max(lead,1)`; `reliability_score` = supplier `reliability_score`, or `reliability_default_score` (70) when null; `total = (w_cost*cost + w_lead*lead + w_rel*reliability) / 100` with weights from `comparison_weights` (70/15/15), rounded to 4 decimals. Rank 1 = highest total; ties are broken by lower effective amount, then earlier `received_on`. |
| BR-PRC-18 | Price vs recent purchases: for each quote and PO line the system compares the unit price with `GetRecentPurchasePrice(materialId, projectId, 90 days)`; if above by more than `price_above_recent_pct` (5%) alert `price_above_recent` is raised and shown on the sheet. Warning only. |
| BR-PRC-19 | Award: the officer selects any non-excluded quote. If it is not rank 1, `award_override_reason` is mandatory and stored; the comparison is frozen (`is_frozen`, `frozen_inputs`) so later changes of settings or supplier scores do not alter it. Award creates a PO draft with prices copied from the quote lines (frozen). A quote past `valid_until` cannot be awarded. |
| BR-PRC-20 | An RFQ needs at least one line, one invitation, a deadline at least 1 hour in the future and a `need_by_date` not before today. A PR line can be in only one open RFQ. After the deadline suppliers cannot submit through the portal; the officer may still capture a late quote with `channel` = the actual channel and flag `late=true` in `detail`, which requires `procurement.quote.capture_late`. |
| BR-PRC-21 | A revised quote creates `revision_no + 1`, sets the old row `revised`, `is_current=false`; only one current quote per supplier per RFQ. Revisions after comparison freeze are not allowed. |

**Worked example (golden cement RFQ).** Request 60 t, budget rate 3,000 (180,000), need in 2 days, rate 2%/month, weights 70/15/15, reliability default 70.

| Quote | Landed | Credit value | Effective | Lead | Filter | cost / lead / reliab. | Total | Rank |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| A (3,050 x 60, transport incl., 30 days credit) | 183,000 | 3,660 (183,000 x 2% x 30/30) | **179,340** | 1 | pass | 100 / 100 / 70 | 95.5000 | 1 |
| B (2,950 x 60 + 4,200, cash) | 181,200 | 0 | **181,200** | 2 | pass | 98.9735 / 50 / 70 | 87.2815 | 2 |
| C (2,900 x 60 + 3,000, 60 days) | 177,000 | not computed | not computed | 5 | `too_late` | - | - | null |

`budget_delta_amount` on the line is `landed - budget` (not effective): A = 183,000 - 180,000 = +3,000 (1.67%), flagged as over budget. Awarding A creates PO draft with subtotal 183,000, VAT 14% = 25,620, total 208,620.

### 6.3 Purchase orders

| ID | Rule |
|---|---|
| BR-PRC-22 | Totals: `line_amount = round(qty * unit_price * (1 - discount_pct/100), 4)`; `vat_amount(line) = round(line_amount * vat_rate_pct/100, 4)`; `subtotal = sum(line_amount)`; header `discount_amount` is deducted from the subtotal and reduces VAT pro rata (`vat = round(sum(line vat) * (1 - discount/subtotal), 4)`); `transport_amount` is taxed at `vat_default_tax_code`; `total = subtotal - discount + transport + vat`. `base_total_amount = (subtotal - discount + transport) * fx_rate`. The DB CHECK enforces the total identity. |
| BR-PRC-23 | For each line the delivery schedule must contain at least one slot and `sum(planned_qty) = quantity`; `planned_date >= order_date`. Services (`receipt_required=false`) have a single slot. |
| BR-PRC-24 | Split-order warning: on `submit`, if the officer's approval limit is L and the sum of POs to the same supplier, project and work package issued or submitted in the last `split_order_window_days` (3) is within `split_order_margin_pct` (10%) below L or above L when counted together while each is below L, alert `split_order_near_limit` (warning, goes into the approval request context, never blocks). |
| BR-PRC-25 | Before `submit`, `BudgetCheckService` runs on the **PO value per work package/cost code** (sum of lines at the check level). `hard_block` rejects the submit unless overridden (BR-PRC-06). The latest binding check is stored in `budget_check_id`. |
| BR-PRC-26 | At most one open amendment (`draft` or `pending_approval`, `version_no > 1`) per PO. A cancelled or closed PO cannot be amended. |
| BR-PRC-27 | An amendment may change quantity, price, discount, schedule, terms and add lines; it may not change supplier, project or delete a line that has accepted receipts (it may reduce quantity down to `accepted_qty`). `change_reason` is mandatory. Approval routing uses the **absolute delta** of `base_total_amount` plus any increase of a single line; if the delta is `<= 0` and `amendment_reduction_needs_approval = false` the amendment is approved without approval. Budget check runs on positive delta only. |
| BR-PRC-28 | Supplier acknowledgement `confirmed_with_changes` or `declined` creates a task for the officer and sets alert `no_supplier_response`-style follow-up; it never edits the PO. An issued PO without any response after `po_ack_reminder_hours` (72) raises reminder notification `PRC_PO_NO_ACK`. |
| BR-PRC-29 | On `issue`, prices, quantities, terms and tax are frozen (BR-X-03). Commitment posted by cost equals `base_total_amount` split per PO line (`line_amount * fx_rate`), by work package and cost code, with `occurred on = issued_at date`. The period guard BR-X-05 applies to `order_date`. |
| BR-PRC-30 | Short-close releases the undelivered remainder of a line: `short_closed_qty = ordered_qty - accepted_qty`, line `close_reason = short_closed`. Allowed without approval if `accepted_qty >= ordered_qty * (1 - under_tolerance_pct/100)`; otherwise requires approval (`procurement.purchase_order_amendment`). Released commitment = `short_closed_qty * unit_price * (1 - discount)` (event `changed` with negative delta). Invoicing the short-closed quantity is then impossible (BR-PRC-45). |

### 6.4 Goods receipt

| ID | Rule |
|---|---|
| BR-PRC-31 | To post: at least one line with `delivered_qty > 0`; every line has `accepted_qty + rejected_qty = delivered_qty`; rejected lines have a reason and a photo; when `require_receipt_evidence` at least one `delivery_note` or `delivered_goods` attachment. Receipts can be posted only against POs `issued` or later and not `cancelled`. |
| BR-PRC-33 | Remaining quantity of a line = `ordered_qty - accepted_qty_to_date`. Over-delivery limit = `ordered_qty * (1 + over_tolerance_pct/100)`. If `accepted_qty_to_date + new accepted <= ordered_qty` decision `within`. |
| BR-PRC-34 | If the new total exceeds `ordered_qty` but not the over-delivery limit: `within` accepted, alert `receipt_over_ordered` (info) and the PO officer is notified. |
| BR-PRC-35 | If the new total exceeds the over-delivery limit the poster must either reject the excess (condition `excess`, rejected quantity) or hold `procurement.goods_receipt.accept_over` (superintendent, officer) and give a reason: decision `over_accepted_with_permission`, alert `receipt_over_ordered` (warning). Otherwise error `procurement.receipt_exceeds_tolerance`. Accepted quantity above the order is accrued at the PO price and needs an amendment before it can be invoiced (BR-PRC-45). |
| BR-PRC-36 | When a PO line reaches `accepted_qty >= ordered_qty * (1 - under_tolerance_pct/100)` and the supplier confirms no more is coming (or the officer short-closes), the line is complete; `under_noted` is set on the last receipt line that fell short. Units: receipt unit must equal the PO unit or be convertible; stored quantity is converted at posting with the factor frozen on the line. |
| BR-PRC-37 | A posted receipt with the same `(supplier_id, delivery_note_no)` as another posted receipt is rejected (`procurement.duplicate_delivery_note`) unless the earlier one is `reversed`. |
| BR-PRC-38 | Reversal needs a reason and is refused if reversing would make `accepted_qty < invoiced_qty` for any line (`procurement.receipt_already_invoiced`); finance must first reject or credit the invoice. Reversal of a receipt does not delete evidence. |
| BR-PRC-39 | A return note can be created from rejected quantity of a posted receipt line (`quantity <= rejected_qty - already returned`). Expected credit = `quantity * po unit price * (1 - discount)`. Rejected quantity is never accrued and never matchable. A registered credit note linked to the return note closes the expected credit. |

**Worked example (partial deliveries).** PO line: 800 pipes at 85 = 68,000. Receipt 1: delivered 300, accepted 300 (accrued 25,500). Receipt 2: delivered 260, accepted 250, rejected 10 (accrued 21,250; total accrued 46,750); balance due = 800 - 550 = 250; PO `partly_received`. Over limit is 840 (5%).

### 6.5 Supplier invoices, tax and matching

| ID | Rule |
|---|---|
| BR-PRC-40 | An invoice belongs to one project and normally to one PO of the same supplier and project; lines of one invoice may refer only to lines of that PO. Invoices without PO need `invoice_without_po_allowed = true` and `no_po_reason`; they always go to `exception`. |
| BR-PRC-41 | Duplicate guard: `(supplier_id, supplier_invoice_no)` unique among non-rejected/non-cancelled invoices (`procurement.duplicate_invoice`, 409). Soft duplicate: same supplier, same `gross_amount` and `invoice_date` with a different number raises alert `invoice_not_matching` (info) for the finance review. |
| BR-PRC-42 | Arithmetic: `line_amount = round(qty * unit_price * (1 - disc/100), 4)`; `net = subtotal - discount`; `gross = net + vat`. `abs(gross - printed_total_amount) <= invoice_rounding_tolerance_amount` (1.0000) else error `procurement.invoice_arithmetic_mismatch`. A credit note carries negative quantities and reduces matched quantity. |
| BR-PRC-43 | Due date = basis date + `credit_days` of the PO version (or supplier profile when no PO). Basis = `received_date` when the terms code ends with `_receipt`, else `invoice_date`. A manual change needs a reason and is audited. |
| BR-PRC-44 | `withholding_amount = round(base * withholding_rate_pct/100, 4)` where base = `net_amount` (`withholding_base = net_ex_vat`) or `gross_amount`; `net_payable = gross - withholding - stamp - other_deduction`. Example (illustrative rate, accountant to confirm): net 100,000, VAT 14% = 14,000, gross 114,000, withholding 1% of net = 1,000, no stamp, other deduction 0 -> net payable 113,000. The ledger INVOICED amount is `net_amount` (100,000), never gross or net payable. |
| BR-PRC-45 | Matching per invoice line (three-way): `matchable_qty = accepted_qty_to_date - invoiced_qty_before` (two-way when `receipt_required=false`: `ordered_qty - invoiced_qty_before`). Quantity check: `invoice_qty <= matchable_qty` is `matched`; if `invoice_qty <= matchable_qty * (1 + invoice_qty_tolerance_pct/100)` it is `within_tolerance`; otherwise `qty_over_received` (if `invoiced_before + invoice_qty <= ordered_qty * (1+over_tol)`) or `qty_over_ordered`; no accepted receipt at all: `no_receipt`; all received quantity rejected: `receipt_not_accepted`. Price check: `price_variance_pct = (invoice_unit_price - po_unit_price) / po_unit_price * 100`; passes when `abs(pct) <= invoice_price_tolerance_pct` **or** `abs(amount_variance) <= invoice_total_tolerance_amount`; a price below the PO is always `within_tolerance`; otherwise `price_variance`. Invoice `match_status = matched` if all lines `matched`, `within_tolerance` if some are within tolerance and none exceptional, `exception` otherwise. |
| BR-PRC-46 | An invoice line with no PO line (`unordered_item`) is always an exception. Finance may accept it only by assigning `work_package_id` and `cost_code_id` and a reason; it then posts INVOICED without a prior commitment and raises alert `invoice_not_matching`. |
| BR-PRC-47 | Variance acceptance (`accept_variance`) needs a reason and the amount-limit approval for `procurement.supplier_invoice`, measured on `sum(abs(amount_variance))`. Accepting `price_variance` does not change the PO. Accepting `qty_over_ordered` is refused unless the PO is amended first. The accepted variance is stored with the invoice. |
| BR-PRC-48 | Receipts are consumed by invoices in the order of invoice `registered_at` (FIFO). A newer invoice cannot take quantity already claimed by an older matched invoice (`invoiced_qty_before` includes it). Receipt events re-queue invoices in `matching`/`exception`. |
| BR-PRC-49 | Credit note (`kind=credit_note`) references the original invoice; amounts are positive in the header and quantities negative per line; on match it reduces `invoiced_qty` and sends `invoice.matched` with negative amounts. A credit note linked to a return note or dispute closes them. |
| BR-PRC-50 | An invoice in `disputed` cannot be approved for payment; at most one open dispute per invoice; resolving with `resolved_credit_note` requires the credit note to be registered first. |

**Worked example (pipes invoice).** Receipts accepted 550 of 800 at PO price 85. Invoice for 800 at 88.40: line 1 qty 800 > matchable 550 (tolerance 1% = 555.5) -> `qty_over_received` (800 \<= 840); price variance (88.40-85)/85 = 4.0% > 2% and amount variance 3.40 x 800 = 2,720 > 100 -> exception on both. Corrected invoice for 550 at 85: `matched`, accrual true-up 0, INVOICED +46,750. A later invoice for 250 after receipt 3 matches against the remaining 250.

### 6.6 Payments

| ID | Rule |
|---|---|
| BR-PRC-51 | Only `matched` invoices of non-blocked suppliers can be approved for payment; approval routes by `net_payable_amount` under `procurement.supplier_invoice`; the approver is not the registrant nor the matcher who accepted variance (BR-X-07). |
| BR-PRC-52 | A batch holds payments in one currency for `approved_for_payment` invoices; the payment `amount` = sum of allocations; `scheduled_date >= today`. An invoice can be in only one open payment. Partial payment is allowed: allocation \<= `outstanding_amount`. |
| BR-PRC-53 | Batch approval routes by `total_amount` under `procurement.payment_batch`; the approver is not the submitter and not the creator of any payment in the batch (BR-X-07, never overridable for payments). |
| BR-PRC-54 | Execution records `executed_date`, `bank_reference` (mandatory for transfer and cheque) and executor. Execution date may not be in a closed period (BR-X-05). Execution is recorded by a user, there is no bank integration in R1. |
| BR-PRC-55 | The payee account must be a **verified** account of the supplier party in `core`; an account changed within the last 7 days needs a second confirmation (`procurement.payment.confirm_new_bank_account`). A snapshot of masked IBAN and bank name is stored with the payment. |
| BR-PRC-56 | Per allocation: `settled_fraction = cash_amount / net_payable_amount` (12 decimals); `paid_ex_vat_amount = round(settled_fraction * net_amount, 4)`; on the allocation that brings `outstanding_amount` to 0 the amount is `net_amount - sum(previous paid_ex_vat_amount)` so the total paid ex VAT equals `net_amount` exactly. Withholding is part of the settled amount even though it is remitted to the tax authority. Example: invoice net 100,000, net payable 113,000; pay 56,500 -> fraction 0.5, paid ex VAT 50,000; pay 56,500 -> 50,000. |
| BR-PRC-57 | A failed payment (`failed`) releases the invoice back to `approved_for_payment`; a reversed payment (bank returned) restores `outstanding_amount` and sends a negative `payment.executed`. |
| BR-PRC-58 | Overpayment is impossible: `sum(executed + scheduled allocations) <= net_payable_amount` per invoice (DB trigger plus domain check, `procurement.payment_exceeds_outstanding`). |

### 6.7 Pipeline, trade position, alerts, suppliers

| ID | Rule |
|---|---|
| BR-PRC-60 | Register stage per purchase line (first rule that matches wins): `closed` if line status `closed`/`cancelled` or PO line fully paid; `invoiced` if received (accepted >= ordered\*(1-under\_tol) or short-closed) and invoiced\_qty > 0; `delivery` if PO issued/acknowledged/partly\_received; `po` if a PO draft/pending/approved exists; `awaiting_review` if line `awaiting_review`; `sourcing` if `in_sourcing`; else `request`. |
| BR-PRC-61 | Trade position per (project, work package) is computed **per PO line** then summed so over- and under-delivery do not offset: `ordered = sum(ordered_amount)`; `received = sum(accepted_amount)`; `invoiced = sum(invoiced_amount)`; `paid = sum(paid_amount)`; `ordered_not_delivered = sum(max(0, ordered - received))`; `delivered_not_invoiced = sum(max(0, received - invoiced))`; `invoiced_unpaid = sum(max(0, invoiced - paid))`. These are separate figures (BR-X-02). Golden plumbing trade: ordered 900,000; received 540,000; invoiced 450,000; paid 300,000 -> ordered not delivered 360,000; delivered not invoiced 90,000; invoiced unpaid 150,000; payable on goods received 240,000 (received - paid). |
| BR-PRC-62 | Late delivery: for each open schedule slot, `delivery_at_risk` when `planned_date - today <= late_warning_days` and not fully received; `late_delivery` when `planned_date < today` and open quantity remains. Severity `critical` when a linked activity (from `planning` constraint data) starts within `activity_start_alert_days` (7). Alerts auto-resolve on receipt, short-close or new planned date by amendment. |
| BR-PRC-63 | Buying list (R1 basic): open PR lines plus blocked material constraints (`planning.constraint.blocked`) grouped by trade and needed-by week; full look-ahead integration follows the planning contract (R2). |
| BR-PRC-70 | Supplier status: `blocked` (also set when `core.party.blocked`) forbids new RFQ invitations, POs, payments; existing POs stay and need an officer decision; `on_hold` forbids new POs but allows receipts and payments; `candidate` may quote only. If a required document is `expired` and `block_po_for_expired_documents` is true the PO `submit` fails (`procurement.supplier_documents_expired`), else a warning. |
| BR-PRC-71 | Rating events (score 0..100): `on_time_delivery` 100 when receipt date \<= slot `planned_date`; `late_delivery` = `max(0, 100 - 10 * days_late)` (planned 12 Oct, received 14 Oct -> 80); `quantity_accuracy` = `max(0, 100 * (1 - abs(delivered - planned_qty) / planned_qty))`; `quality_rejection` = `100 * (1 - rejected/delivered)`; `price_variance_invoice` = 100 if invoice price \<= PO price else `max(0, 100 - 10 * variance_pct)`; `documents_complete` 100 or 0 (manual); `response_time` 100 if RFQ answered by deadline else 0. One event per receipt line and kind, one per invoice (unique indexes). |
| BR-PRC-72 | `reliability_score` = weighted average of events from the last 12 months (`sum(score*weight)/sum(weight)`), `null` when fewer than 3 events. Recomputed after each new event; `procurement.supplier.rated.v1` is published when the score changes by at least 1 point. |
| BR-PRC-73 | Document expiry: status `expiring` when `expires_on - today <= document_expiry_warning_days`, `expired` when past; daily job raises `supplier_document_expiring` / `supplier_document_expired` once per document (dedupe key). |
| BR-PRC-74 | Period guard (BR-X-05) applies to receipt date, invoice registration (`invoice_date` for ledger), and payment execution date; the operation fails with `procurement.period_closed`. |
| BR-PRC-75 | Foreign currency: a PO, invoice or payment in a non-base currency needs `fx_rate` and `fx_date`; invoice currency must equal PO currency; events carry both document amounts and `*_base_amount` (R1 ledger uses base). |
| BR-PRC-76 | Visibility (BR-X-06): the following are omitted for roles without `procurement.price.view`: `unit_price`, `line_amount`, `discount_*`, all `*_amount` on PR lines, quotes, POs, receipts (`accepted_unit_price`, `accepted_amount`), invoices, payments, budget fields, `trade position` money columns and all exports of them. Quantities, dates, statuses and supplier names remain visible to `foreman` and `superintendent` for POs and receipts. Foreman has no supplier or quote access. |
| BR-PRC-77 | Portal isolation: `supplier_user` sees only records where `supplier_id` equals own party; never other suppliers' quotes, budgets or internal notes; an RFQ link token gives access to one invitation only. |

---

## 7. API endpoints

Base `/api/v1/procurement`. Conventions per 00 §3.6 (keyset `limit`/`cursor`, `If-Match` on every mutating call except create, `Idempotency-Key` required on all `POST` actions marked **I**, errors per 00 §3.7). Fields in the "key response" column marked (P) are omitted without `procurement.price.view` (BR-PRC-76). Permission codes are listed in section 10; `ps` = project scope check (BR-X-09). Error codes are in section 12; `412` and `422 idempotency_key_reuse` apply everywhere and are not repeated.

### 7.1 Purchase requests

| Method | Path | Purpose | Key request | Key response | Errors | Permission |
|---|---|---|---|---|---|---|
| GET | `/purchase-requests` | List | filters below | `id, prNumber, projectId, status, origin, neededByDate, priority, duplicateFlag, lineCount, assignedOfficerId` |  | `purchase_request.view` ps |
| POST | `/purchase-requests` | Create manual PR (draft) | `projectId, priority, notes, lines[{workPackageId, costCodeId, materialId?, itemDescription, quantity, unitCode, neededByDate, zoneId?}]` | PR with lines, `budgetStatus` per line | `work_package_project_mismatch`, `validation_failed` | `purchase_request.create` |
| GET | `/purchase-requests/{id}?include=lines` | Read |  | header, lines, `estimatedAmount` (P), budget check result, duplicates | 404 | view |
| PATCH | `/purchase-requests/{id}` | Edit draft/submitted header or lines (`lines` merge by `lineNo`) | fields | PR | `pr_not_editable` 409 | create |
| POST | `/purchase-requests/{id}/submit` I | Draft to submitted |  | PR | `pr_empty` | create |
| POST | `/purchase-requests/{id}/approve` I | Approve and route | `lines[{lineId, route}]`, `budgetReasons[{lineId, reason}]` | PR; published event | `route_required`, `budget_reason_required` | `purchase_request.approve` |
| POST | `/purchase-requests/{id}/reject` | Reject | `reason` | PR |  | approve |
| POST | `/purchase-requests/{id}/cancel` | Cancel | `reason` | PR | `pr_has_issued_po` | `purchase_request.cancel` |
| POST | `/purchase-request-lines/{id}/route` | Change route (switch to manual RFQ) | `route, reason` | line | `route_locked` | approve |
| POST | `/purchase-request-lines/{id}/budget-check` | Run check now |  | `budget_checks` row |  | view |
| POST | `/purchase-request-lines/{id}/clear-duplicate` | Clear flag | `note` | line |  | approve |

List `/purchase-requests`: `filter[projectId]`, `filter[status]`, `filter[origin]`, `filter[assignedOfficerId]`, `filter[priority]`, `filter[duplicateFlag]`, `filter[neededBy][lte]`, `filter[workPackageId]` (via lines), `filter[createdAt][gte|lte]`, `q` (number, description); `sort`: `createdAt`, `neededByDate`, `prNumber`, `priority`.

### 7.2 RFQ, quotes, comparison

| Method | Path | Purpose | Key request | Key response | Errors | Permission |
| --- | --- | --- | --- | --- | --- | --- |
| GET/POST | `/rfqs` | List / create from PR lines | `projectId, title, deadlineAt, needByDate, prLineIds[], supplierIds[], instructions, attachmentFileIds[]` | RFQ | `pr_line_in_open_rfq` 409, `supplier_not_eligible` | `rfq.view` / `rfq.create` |
| GET/PATCH | `/rfqs/{id}` | Read / edit draft |  | RFQ with lines, invitations | `rfq_not_editable` | same |
| POST | `/rfqs/{id}/invitations` | Add supplier | `supplierId, channel` | invitation (link token shown once) | `supplier_not_eligible` | create |
| POST | `/rfqs/{id}/send` I | Send |  | RFQ `sent` | `rfq_invalid`, `deadline_in_past` | `rfq.send` |
| POST | `/rfqs/{id}/extend-deadline`, `/close`, `/reopen`, `/cancel` | Lifecycle | `deadlineAt` / `reason` | RFQ | `rfq_not_open` | send |
| GET | `/rfqs/{id}/quotes` | Quotes (current by default) | `?include=lines` | quotes, `unitPrice` (P) |  | `quote.view` |
| POST | `/rfqs/{id}/quotes` | Capture a quote for a supplier (paper, phone, email) | header terms, `lines[{rfqLineId, quantity, unitPrice, specMatch, leadTimeDays}]`, `fileId` | quote | `quote_exists_use_revise`, `rfq_closed_late_capture_denied` | `quote.capture` |
| POST | `/quotes/{id}/revise` | New revision | as capture | quote `revisionNo+1` | `comparison_frozen` | capture |
| POST | `/quotes/{id}/reject`, `/shortlist` | Mark | `reason` | quote |  | capture |
| POST | `/rfqs/{id}/comparison:compute` | Compute/recompute sheet | `financingRateMonthlyPct?, weights?, includeQuoteIds[]?` | comparison + lines | `no_quotes`, `comparison_frozen` | `quote.capture` |
| GET | `/rfqs/{id}/comparison` | Read sheet with calculation |  | per quote landed, creditValue, effective, scores, rank, exclusion (P) |  | `quote.view` |
| POST | `/rfqs/{id}/award` I | Award, freeze, create PO draft(s) | `awards[{quoteId, rfqLineIds[]}], awardReason, awardOverrideReason?` | `poIds[]` | `award_override_reason_required`, `quote_expired`, `quote_excluded` | `rfq.award` |

List `/rfqs`: `filter[projectId|status|supplierId]`, `filter[deadlineAt][lte]`, `q`; `sort`: `createdAt`, `deadlineAt`, `rfqNumber`.

### 7.3 Purchase orders

| Method | Path | Purpose | Key request | Key response | Errors | Permission |
| --- | --- | --- | --- | --- | --- | --- |
| GET | `/purchase-orders` | List | filters below | `id, poNumber, versionNo, projectId, supplierId, status, deliveryStatus, invoiceStatus, paymentStatus, totalOrderedAmount (P), neededByDate` |  | `purchase_order.view` ps |
| POST | `/purchase-orders` | Create draft (manual, from PR lines, or from quote) | `projectId, supplierId, origin, prLineIds[]?, quoteId?, terms{...}, lines[{prLineId?, materialId?, itemDescription, quantity, unitCode, unitPrice, discountPct, taxCodeId, workPackageId, costCodeId, neededByDate, schedule[{plannedDate, plannedQty}]}]` | PO v1 with totals | `supplier_not_approved`, `unit_mismatch`, `po_exceeds_request` (warning) | `purchase_order.create` |
| GET | `/purchase-orders/{id}?include=lines,schedule,balances,versions` | Read (`?version=n` for history) |  | PO; balances; ack; budget check |  | view |
| PATCH | `/purchase-orders/{id}` | Edit current draft version | merge | PO | `po_version_frozen` 409 | create |
| POST | `/purchase-orders/{id}/submit` I | Submit for approval (budget check, scan) | `budgetReason?` | PO `pending_approval`, `approvalId`, `warnings[]` | `po_over_budget` 422, `po_schedule_mismatch`, `supplier_documents_expired`, `supplier_blocked` | `purchase_order.submit` |
| POST | `/purchase-orders/{id}/withdraw` | Withdraw |  | PO |  | submit |
| POST | `/purchase-orders/{id}/issue` I | Issue to supplier (limit check) | `sendVia[portal,email]` | PO `issued` | `po_not_approved`, `period_closed`, `limit_exceeded` | `purchase_order.issue` |
| POST | `/purchase-orders/{id}/acknowledgements` | Record supplier response (on behalf) | `response, channel, proposedChanges[], comment` | ack | `po_not_issued` | `purchase_order.acknowledge_on_behalf` |
| POST | `/purchase-orders/{id}/amend` I | Create amendment draft (copy) | `changeReason` | PO with `pendingVersionId` | `amendment_exists`, `po_closed` | `purchase_order.amend` |
| POST | `/purchase-orders/{id}/short-close` I | Short-close lines | `lines[{lineNo, reason}]` | PO | `short_close_below_received` | amend |
| POST | `/purchase-orders/{id}/cancel` I | Cancel | `reason` | PO | `po_has_receipts`, `po_has_invoices` | `purchase_order.cancel` |
| POST | `/purchase-orders/{id}/close` | Close | `reason?` | PO | `po_has_open_amounts` | `purchase_order.close` |
| GET | `/purchase-orders/{id}/versions` | Version history with `changeSummary` |  | versions |  | view |
| GET | \`/purchase-orders/\{id\}/document?lang=ar | en | bi\` | PDF (renders if missing) |  | file URL |
| GET | `/purchase-orders/{id}/expected-deliveries` | Slots and balance due (site view, no prices) |  | slots, `orderedQty, acceptedQty, balanceQty` |  | `goods_receipt.view` |
| POST | `/purchase-orders/{id}/follow-ups` | Log follow-up call | `channel, outcome, promisedDate, note` | follow-up |  | `purchase_order.create` |

List `/purchase-orders`: `filter[projectId|supplierId|status|deliveryStatus|invoiceStatus|paymentStatus|origin|workPackageId]`, `filter[issuedAt][gte|lte]`, `filter[neededBy][lte]`, `filter[overdue]=true`, `q` (number, supplier name); `sort`: `createdAt`, `issuedAt`, `poNumber`, `totalOrderedAmount` (only with price visibility).

### 7.4 Goods receipt and returns

| Method | Path | Purpose | Key request | Key response | Errors | Permission |
| --- | --- | --- | --- | --- | --- | --- |
| GET | `/goods-receipts` | List | filters below | `grnNumber, clientRef, poId, supplierId, status, receiptDate, lineCount` |  | `goods_receipt.view` |
| POST | `/goods-receipts` | Create draft (idempotent by `clientRef`) | `clientRef, poId, receiptDate, deliveryNoteNo, vehicleRef, lines[{poLineNo, deliveredQty, acceptedQty, rejectedQty, condition, rejectionReason, batchRef, expiryDate}], attachments[{fileId, kind}]` | GRN draft | `po_not_receivable`, `quantity_mismatch` | `goods_receipt.create` |
| POST | `/goods-receipts:bulk` | Offline queue sync (up to 500) | array | per item results |  | create |
| PATCH | `/goods-receipts/{id}` | Edit draft |  | GRN | `grn_not_draft` | create |
| POST | `/goods-receipts/{id}/post` I | Post | `acceptOverReason?` | GRN `posted`, `poBalance`, warnings | `receipt_exceeds_tolerance`, `receipt_evidence_required`, `duplicate_delivery_note`, `period_closed` | `goods_receipt.post` |
| POST | `/goods-receipts/{id}/reverse` | Reverse | `reason` | GRN `reversed` | `receipt_already_invoiced` | `goods_receipt.reverse` |
| POST | `/goods-receipts/{id}/discard` | Discard draft |  |  |  | create |
| POST | `/goods-receipts/{id}/return-notes` | Create return note from rejected qty | `lines[{grnLineId, quantity}], reason` | return note | `return_exceeds_rejected` | `return_note.manage` |
| POST | \`/return-notes/\{id\}/issue | collect | close | cancel\` | Lifecycle |  |

List `/goods-receipts`: `filter[projectId|poId|supplierId|status|receivedBy]`, `filter[receiptDate][gte|lte]`, `filter[hasRejection]`; `sort`: `receiptDate`, `createdAt`, `grnNumber`. Site roles never receive `acceptedUnitPrice`, `acceptedAmount`.

### 7.5 Supplier invoices

| Method | Path | Purpose | Key request | Key response | Errors | Permission |
| --- | --- | --- | --- | --- | --- | --- |
| GET | `/supplier-invoices` | List | filters below | `registrationNumber, supplierInvoiceNo, status, matchStatus, netAmount, grossAmount, netPayableAmount, outstandingAmount, dueDate` |  | `invoice.view` |
| POST | `/supplier-invoices` | Register (+ auto match) | `kind, projectId, supplierId, poId, supplierInvoiceNo, invoiceDate, receivedDate, printedTotalAmount, withholdingTaxCodeId?, stampAmount, otherDeduction{amount, reason}, fileId, lines[{poLineNo?, description, quantity, unitCode, unitPrice, discountPct, taxCodeId, workPackageId?, costCodeId?}]` | invoice with computed amounts and match result | `duplicate_invoice` 409, `invoice_arithmetic_mismatch`, `invoice_po_mismatch`, `supplier_blocked` | `invoice.register` |
| PATCH | `/supplier-invoices/{id}` | Edit while `registered`/`exception` |  | invoice | `invoice_not_editable` | register |
| GET | `/supplier-invoices/{id}/match-result` | Latest run per line with tolerance applied |  | `invoice_match_lines` |  | view |
| POST | `/supplier-invoices/{id}/rematch` | Re-run |  | run |  | `invoice.match` |
| POST | `/supplier-invoices/{id}/accept-variance` I | Accept exceptions | `reason, lines[]` | invoice `matched` | `variance_not_acceptable`, `limit_exceeded` | `invoice.accept_variance` |
| POST | `/supplier-invoices/{id}/hold`, `/release-hold` | Hold | `reason` |  |  | `invoice.hold` |
| POST | `/supplier-invoices/{id}/disputes` | Open dispute | `reasonCode, description, disputedAmount` | dispute | `dispute_exists` | `invoice.dispute` |
| POST | \`/invoice-disputes/\{id\}/reply | resolve\` | Supplier reply (portal) / resolve | `resolution, creditNoteInvoiceId?` | dispute | `credit_note_required` |
| POST | `/supplier-invoices/{id}/approve-for-payment` I | Approve |  | invoice | `invoice_not_matched`, `invoice_disputed` | `invoice.approve` |
| POST | `/supplier-invoices/{id}/reject`, `/cancel` |  | `reason` |  |  | `invoice.reject` |

List `/supplier-invoices`: `filter[projectId|supplierId|poId|status|matchStatus|kind]`, `filter[dueDate][lte]`, `filter[invoiceDate][gte|lte]`, `filter[overdue]`, `q` (numbers); `sort`: `dueDate`, `invoiceDate`, `createdAt`, `registrationNumber`. Money fields are hidden from roles without `procurement.price.view` (foreman and superintendent have no invoice access at all).

### 7.6 Payments and payables

| Method | Path | Purpose | Key request | Key response | Errors | Permission |
| --- | --- | --- | --- | --- | --- | --- |
| GET | `/payables` | Approved invoices due, by due date | `filter[dueDate][lte]`, `filter[supplierId]` | invoices with `outstandingAmount`, bank account status |  | `payment.view` |
| GET/POST | `/payment-batches` | List / create draft | `title, plannedPaymentDate, method, currency, payments[{supplierId, allocations[{invoiceId, cashAmount}], payeeBankAccountId}]` | batch with totals | `invoice_not_payable`, `payment_exceeds_outstanding`, `currency_mismatch` | `payment.view` / `payment.create` |
| PATCH | `/payment-batches/{id}` | Edit draft |  |  |  | create |
| POST | `/payment-batches/{id}/submit` I | Submit |  | `approvalId` | `bank_account_unverified` | create |
| POST | `/payment-batches/{id}/release` I | Generate bank file (CSV) |  | `exportFileId` | `batch_not_approved` | `payment.release` |
| POST | `/payments/{id}/execute` I | Record execution | `executedDate, bankReference` | payment, invoice outstanding | `bank_reference_required`, `period_closed` | `payment.execute` |
| POST | `/payments/{id}/fail`, `/reverse` |  | `reason` |  |  | execute |
| POST | `/payment-batches/{id}/cancel` |  | `reason` |  | `payment_already_executed` | create |

List `/payment-batches`: `filter[status|plannedPaymentDate|supplierId]`; `sort`: `plannedPaymentDate`, `createdAt`. List `/payments`: `filter[status|supplierId|projectId|invoiceId]`, `filter[executedDate][gte|lte]`.

### 7.7 Suppliers, register, alerts, settings

| Method | Path | Purpose | Permission |
| --- | --- | --- | --- |
| GET | `/suppliers` | Suppliers with profile status, reliability, open POs (filters `status`, `categoryCode`, `q`, `documentStatus`; sort `name`, `reliabilityScore`) | `supplier.view` |
| GET/PATCH | `/suppliers/{partyId}/profile` | Profile (terms, credit limit) | view / `supplier.manage` |
| POST | `/suppliers/{partyId}/status` | Approve, hold, block, unblock (reason) | `supplier.manage`; hold override `supplier.override_status` |
| GET/POST/PATCH/DELETE | `/suppliers/{partyId}/documents` | Documents with expiry | view / manage |
| POST | `/suppliers/{partyId}/rating-events` | Manual rating | `supplier.rate` |
| GET | `/suppliers/{partyId}/scorecard` | Reliability, event breakdown, on-time %, rejections, price variance | `supplier.view` |
| POST | `/suppliers:import` | Excel import (platform import framework: upload, preview, commit) | `supplier.manage` |
| GET | `/register` | Procurement register rows (filters `stage`, `projectId`, `workPackageId`, `supplierId`, `neededBy[lte]`, `materialId`, `q`; sort `neededByDate`, `stage`, `createdAt`) | `register.view` |
| GET | `/register/summary` | Stage chip counts for a project | `register.view` |
| GET | `/trade-position` | Per work package measures (BR-PRC-61); `filter[projectId]` required | `register.view` (money needs price) |
| GET | `/buying-list` | By trade and week | `register.view` |
| GET | `/alerts`; POST `/alerts/{id}/acknowledge`, `/resolve` | Alerts (\`filter\[type | severity |
| GET/PATCH | `/settings`, `/projects/{projectId}/settings` | Settings (4.10) | `settings.manage` |
| GET/POST/PATCH/DELETE | `/po-term-templates` | Terms templates | `settings.manage` |
| GET | \`/reports/\{code\}/export?format=xlsx | csv | pdf\` |

**Portal endpoints** (audience `portal`, base `/api/v1/portal/procurement`, rate-limited per 00 §3.6; detailed UI contract in file 15): `GET /rfqs`, `GET /rfqs/{id}`, `POST /rfqs/{id}/quotes`, `POST /rfqs/{id}/decline`, `GET /purchase-orders`, `POST /purchase-orders/{id}/acknowledgements`, `POST /purchase-orders/{id}/delivery-notes`, `POST /invoices`, `GET /invoices` (own payment status). All reuse the same domain handlers with `source='portal'` and filter by the user's `party_id`.

### 7.8 JSON examples

**Create PO from the cement award (request)**

```json
POST /api/v1/procurement/purchase-orders
Idempotency-Key: 3d5a0f0e-8b3f-4b0e-9c61-5d1d7f4f2a10
{
  "projectId": "0192a1c4-0000-7000-8000-000000000001",
  "supplierId": "0192a1c4-0000-7000-8000-0000000000a1",
  "origin": "rfq_award",
  "quoteId": "0192a1c4-0000-7000-8000-0000000000b1",
  "terms": { "paymentTermsCode": "net_30", "creditDays": 30, "deliveryTerms": "delivered_site", "orderDate": "2026-10-11" },
  "lines": [{
    "prLineId": "0192a1c4-0000-7000-8000-0000000000c1",
    "materialId": "0192a1c4-0000-7000-8000-0000000000d1",
    "itemDescription": "Portland cement CEM I 42.5N, bulk",
    "quantity": 60, "unitCode": "t", "unitPrice": 3050, "discountPct": 0,
    "taxCodeId": "0192a1c4-0000-7000-8000-0000000000e1",
    "workPackageId": "0192a1c4-0000-7000-8000-0000000000f1",
    "costCodeId": "0192a1c4-0000-7000-8000-0000000000f2",
    "neededByDate": "2026-10-13",
    "schedule": [{ "plannedDate": "2026-10-12", "plannedQty": 60 }]
  }]
}
```

Response `201` (price fields shown to a user with `procurement.price.view`):

```json
{
  "id": "0192a1c4-0000-7000-8000-000000000101", "poNumber": "PO-1066", "versionNo": 1,
  "status": "draft", "currency": "EGP",
  "totals": { "subtotalAmount": 183000.0000, "discountAmount": 0, "transportAmount": 0, "vatAmount": 25620.0000, "totalAmount": 208620.0000, "baseTotalAmount": 183000.0000 },
  "warnings": [{ "code": "procurement.po_over_budget_soft", "params": { "remaining": 180000.0000, "requested": 183000.0000, "currency": "EGP" } }],
  "etag": "W/\"7\""
}
```

**Post a goods receipt (partial delivery, site user: no prices in response)**

```json
POST /api/v1/procurement/goods-receipts/0192.../post     If-Match: W/"3"
{ "acceptOverReason": null }
-> 200
{
  "id": "0192...", "grnNumber": "GRN-2209", "status": "posted", "receiptDate": "2026-10-12",
  "lines": [{ "poLineNo": 1, "deliveredQty": 260, "acceptedQty": 250, "rejectedQty": 10, "toleranceDecision": "within" }],
  "poBalance": [{ "poLineNo": 1, "orderedQty": 800, "acceptedQty": 550, "balanceQty": 250, "unitCode": "pcs" }],
  "warnings": []
}
```

**Register an invoice that fails the match (finance view)**

```json
POST /api/v1/procurement/supplier-invoices
{ "kind": "invoice", "projectId": "0192...", "supplierId": "0192...", "poId": "0192...",
  "supplierInvoiceNo": "INV-7781", "invoiceDate": "2026-10-20", "receivedDate": "2026-10-21",
  "printedTotalAmount": 77520.0000,
  "lines": [{ "poLineNo": 1, "description": "uPVC pipe 110 mm", "quantity": 800, "unitCode": "pcs", "unitPrice": 88.40, "discountPct": 0, "taxCodeId": "0192..." }] }
-> 201
{ "registrationNumber": "SI-1520", "status": "exception", "matchStatus": "exception",
  "netAmount": 70720.0000, "vatAmount": 9900.8000, "grossAmount": 80620.8000,
  "matchLines": [{ "poLineNo": 1, "matchMode": "three_way", "matchableQty": 550, "invoiceQty": 800,
     "result": "qty_over_received", "priceVariancePct": 4.0000, "amountVariance": 2720.0000 }] }
```

(Printed total in this example must equal 80,620.80 within 1.00 or the call fails with `invoice_arithmetic_mismatch`; the printed value above is replaced by 80620.8000 in the test fixture.)

**Compute the comparison sheet (response excerpt)**

```json
POST /api/v1/procurement/rfqs/0192.../comparison:compute  -> 200
{ "formulaVersion": "manual-1", "financingRateMonthlyPct": 2.0, "weights": { "cost": 70, "lead_time": 15, "reliability": 15 },
  "lines": [
   { "quoteNumber": "QT-0301", "landedAmount": 183000.0000, "creditValueAmount": 3660.0000, "effectiveAmount": 179340.0000, "leadTimeDays": 1, "totalScore": 95.5000, "rank": 1, "budgetDeltaAmount": 3000.0000 },
   { "quoteNumber": "QT-0302", "landedAmount": 181200.0000, "creditValueAmount": 0, "effectiveAmount": 181200.0000, "leadTimeDays": 2, "totalScore": 87.2815, "rank": 2, "budgetDeltaAmount": 1200.0000 },
   { "quoteNumber": "QT-0303", "landedAmount": 177000.0000, "excluded": true, "exclusionReason": "too_late", "rank": null } ] }
```

**Execute a payment**

```json
POST /api/v1/procurement/payments/0192.../execute    Idempotency-Key: ...   If-Match: W/"2"
{ "executedDate": "2026-10-26", "bankReference": "TRF-20261026-0087" }
-> 200 { "paymentNumber": "PAY-3310", "status": "executed", "amount": 56500.0000,
         "allocations": [{ "invoiceId": "0192...", "cashAmount": 56500.0000, "settledFraction": 0.500000000000, "paidExVatAmount": 50000.0000, "invoiceOutstandingAfter": 56500.0000 }] }
```

---

## 8. Events

Envelope per 00 §3.9; below only `payload`. All amounts carry `currency` and, when not base, `fxRate` plus `*BaseAmount`. Partition key = aggregate id. Quantities carry `unitCode`. Payload amounts are present for all consumers (events are internal; price visibility is applied at read time, BR-PRC-76).

### 8.1 Published

| Event | Fires when | Payload |
|---|---|---|
| `procurement.purchase_request.created.v1` | PR leaves `draft` for the first time (`submitted`) or is created `submitted` from a site request | `prId, prNumber, projectId, origin, siteRequestId?, priority, neededByDate, assignedOfficerId, lines[{lineId, lineNo, workPackageId, costCodeId, materialId?, quantity, unitCode, neededByDate}]` |
| `procurement.purchase_request.approved.v1` | Officer approves (or auto-approval BR-PRC-02) | as above plus `approvedBy, approvedAt, lines[].route, lines[].budgetStatus, lines[].zoneId, lines[].urgent`. Sourcing reads only `route=auto_sourcing` lines (section 4.9). |
| `procurement.purchase_order.issued.v1` | `issue` succeeds (commitment point, BR-PRC-29) | `poId, poNumber, versionNo, projectId, supplierId, issuedAt, orderDate, paymentTermsCode, creditDays, totalAmount, baseTotalAmount, currency, lines[{lineNo, prLineId?, materialId?, workPackageId, costCodeId, quantity, unitCode, unitPrice, discountPct, lineAmount, baseLineAmount, landedUnitPrice, leadTimeDays, neededByDate}]` |
| `procurement.purchase_order.changed.v1` | An amendment or short-close version is approved and becomes current | `poId, versionNo, previousVersionNo, kind, changeReason, deltaBaseAmount, lines[]` (full new line set with `lineDeltaBaseAmount`) so cost can post `commitment_change` |
| `procurement.purchase_order.cancelled.v1` | PO cancelled (only without receipts/invoices) | `poId, versionNo, reason, releasedBaseAmount, lines[{lineNo, workPackageId, costCodeId, releasedBaseAmount}]` |
| `procurement.purchase_order.closed.v1` | PO closed (all lines complete and no open amounts, or manual close) | `poId, closedAt, reason, releasedBaseAmount` (uncommitted remainder after short-close; 0 when fully received) |
| `procurement.goods_receipt.posted.v1` | Receipt posted | `grnId, grnNumber, poId, poVersionNo, projectId, supplierId, receiptDate, lines[{poLineNo, workPackageId, costCodeId, materialId?, acceptedQty, rejectedQty, unitCode, acceptedUnitPrice, acceptedAmount, acceptedBaseAmount, toleranceDecision}]`. Cost posts accrual (`accrual`, consumes commitment); planning updates material availability. |
| `procurement.goods_receipt.reversed.v1` | Receipt reversed | same shape with `reason`; amounts positive, consumers negate |
| `procurement.invoice.registered.v1` | Invoice saved (any kind) after arithmetic check | `invoiceId, registrationNumber, kind, supplierId, projectId, poId?, supplierInvoiceNo, invoiceDate, dueDate, netAmount, vatAmount, grossAmount, netPayableAmount, currency, status` (cash uses it for payable forecast pending match) |
| `procurement.invoice.matched.v1` | Status becomes `matched` (match or variance accepted); credit notes negative | `invoiceId, kind, supplierId, projectId, poId?, dueDate, netAmount, vatAmount, netPayableAmount, withholdingAmount, lines[{poLineNo?, workPackageId, costCodeId, materialId?, invoicedQty, unitCode, unitPrice, netAmount, vatAmount, varianceAmount, accepted}]` |
| `procurement.invoice.disputed.v1` | Dispute opened | `invoiceId, disputeId, reasonCode, disputedAmount, supplierId, projectId` (cash freezes the payable line) |
| `procurement.payment.scheduled.v1` | Batch approved (payments `scheduled`) | `batchId, paymentId, paymentNumber, supplierId, projectId?, scheduledDate, amount, currency, allocations[{invoiceId, cashAmount}]` |
| `procurement.payment.executed.v1` | Execution recorded; reversal publishes negative amount with `reversal=true` | `paymentId, paymentNumber, executedDate, bankReference, amount, currency, allocations[{invoiceId, projectId, workPackageId?, cashAmount, settledFraction, paidExVatAmount}]` |
| `procurement.supplier.rated.v1` | `reliability_score` changes by >= 1 point (BR-PRC-72) | `supplierId, reliabilityScore, previousScore, eventCount, lastEventKind` |

### 8.2 Consumed

| Event | Handler | Action | Idempotency |
| --- | --- | --- | --- |
| `planning.site_request.confirmed.v1` | `SiteRequestConfirmedHandler` | Creates PR `origin=site_request` (BR-PRC-02), runs duplicate detector and budget check | Unique `(tenant_id, site_request_id)` on `purchase_requests`; duplicate event returns existing PR |
| `planning.constraint.blocked.v1` / `.cleared.v1` | `ConstraintHandler` | Marks register rows and buying list with the blocking constraint; raises/clears `delivery_at_risk` (BR-PRC-62) | Upsert keyed by `constraint_id`; processed-events table |
| `sourcing.recommendation.ready.v1` | `RecommendationReadyHandler` | Line to `awaiting_review`; task for the officer | Status guard (no-op if already beyond) |
| `sourcing.recommendation.decided.v1` | `RecommendationDecidedHandler` | PO draft per supplier, or return line to `manual_rfq` (4.9) | Unique `(tenant_id, sourcing_recommendation_id, sourcing_offer_id)` |
| `cost.budget.approved.v1` / `.revised.v1` | `BudgetCacheHandler` | Upserts `rm_budget_cache` per (work package, cost code) | Upsert with `budget_version` guard: ignore older version |
| `platform.approval.approved/rejected/cancelled/escalated.v1` | `ApprovalOutcomeHandler` by `recordType` (`procurement.purchase_order`, `.purchase_order_amendment`, `.supplier_invoice`, `.payment_batch`) | Moves record to `approved`/`draft`(rejected)/previous state; escalated sends notification | Record state guard plus `approval_id` match |
| `core.party.created/updated/blocked.v1` | `PartyHandler` | Creates/updates `supplier_profiles` shell (status `candidate`); blocked sets `blocked` (BR-PRC-70) | Upsert by `party_id` |
| `core.work_package.updated/archived.v1`, `core.cost_code.updated.v1` | `MasterDataHandler` | Refreshes denormalised names in `rm_*`; archived work package blocks new lines | Idempotent upsert |
| `core.material.created/updated.v1` | `MaterialHandler` | Refreshes `auto_sourcing_enabled` flag cache and default unit | Upsert |
| `core.project.closed.v1` | `ProjectClosedHandler` | Cancels open drafts, raises alert for open POs | State guard |
| `cost.period.closed.v1` | `PeriodCloseHandler` | Updates local `closed_periods` cache for BR-PRC-74 (authoritative check is the synchronous `ICostPeriodGuard`) | Upsert by period |

All handlers store `(consumer, event_id)` per 00 §3.9; failures follow the 5-step retry and dead-letter.

### 8.3 Proposed registry additions

1. Add `procurement` as consumer of `cost.period.closed.v1` and `core.project.closed.v1` (the registry lists reporting/cash and "all modules" respectively; make procurement explicit).
2. Add `sourcing` as consumer of `procurement.invoice.matched.v1` (invoice price observation, section 4.9) and of `procurement.goods_receipt.posted.v1` (receipt price observation).
3. Add `planning` as consumer of `procurement.purchase_order.changed.v1` (delivery dates moved; constraint recalculation).

---

## 9. Background jobs and schedules

Hangfire queues per 00 §3.10; every job has tenant context, a timeout, a `job_runs` record and is idempotent. "PT" = per tenant (enable flag in tenant settings); time zone Africa/Cairo.

| Job | Schedule / trigger | What | Idempotency | PT |
| --- | --- | --- | --- | --- |
| `RfqDeadlineCloser` | every 5 min, `default` | Sets RFQ `closed` when deadline passed or all invitations replied; invitations without reply `no_response`; notifies officer | Status guard | yes |
| `QuoteExpiryJob` | hourly | Quotes with `valid_until < today` and not selected become `expired` | Status guard | yes |
| `SourcingStallScanner` | every 15 min | Lines `in_sourcing` longer than `sourcing_stall_hours` raise `sourcing_stalled` | Alert `dedupe_key = stalled:{lineId}` | yes |
| `LateDeliveryScanner` | every 2 h from 06:00 to 18:00 | BR-PRC-62: `delivery_at_risk`, `late_delivery`; notifications; rating events for late slots | Alert dedupe `late:{poId}:{slotId}:{status}` | yes |
| `PoAckReminderJob` | hourly | Issued POs without response after `po_ack_reminder_hours` send `PRC_PO_NO_ACK` once per 24 h | `last_reminder_at` | yes |
| `InvoiceRematchSweep` | every 15 min plus triggered by `goods_receipt.posted` | Re-runs matcher for invoices in `matching`/`exception` for the PO; FIFO (BR-PRC-48) | Run record per (invoice, trigger event id) | yes |
| `InvoiceDueReminderJob` | daily 07:00 | Approved invoices due in 3 days or overdue to finance; builds `rm_payables_due` | Notification dedupe per invoice and day | yes |
| `SupplierDocumentExpiryJob` | daily 02:00 | BR-PRC-73 statuses and alerts | Dedupe per document and status | yes |
| `SupplierReliabilityRecalc` | nightly 03:00 | Recomputes scores from the 12-month window (drops events leaving the window); publishes `supplier.rated` on change >= 1 | Deterministic from events | yes |
| `RegisterReconciliation` | nightly 03:30 | Recomputes `rm_procurement_register`, `rm_trade_position`, `rm_spend` for projects touched in the last 48 h and compares with the incremental rows; mismatch logged and corrected | Rebuild is a pure function of facts | yes |
| `StaleDraftReminderJob` | daily 08:00 | GRN drafts older than 3 days, PR `draft` older than 7 days, PO `draft` older than 14 days: reminder to owner | Dedupe per record and week | yes |
| `ExportReportJob` | on demand (`reports` queue) | Builds xlsx/csv/pdf for > 5,000 rows; stores through `IFileService` | Job id = idempotency key | no |
| `OutboxRelay` | platform (PLT-E4) | Publishes events | platform | no |

---

## 10. Permissions

Codes are `procurement.<resource>.<action>`; matrix of built-in roles (00 §4.4). Legend: X = granted by default; L = granted within the user's amount limit from `project_members.limits` or approval policy; P = project scope (BR-X-09); (v) view only.

| Permission | PO | PM | FIN | OWN | SUP | FOR | CQS | TA | Notes |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| `purchase_request.view` | X P | X P | X | X | X P | X P (own, no prices) | X |  | Foreman sees PRs of own site requests, quantities only |
| `purchase_request.create` | X | X |  |  |  |  |  |  | Site users create through planning, not here |
| `purchase_request.approve` | X | X |  |  |  |  |  |  | Routing and duplicate clearing |
| `purchase_request.cancel` | X | X |  |  |  |  |  |  |  |
| `rfq.view` | X | X | X | X | X (v) |  | X |  | Superintendent view only |
| `rfq.create`, `rfq.send` | X |  |  |  |  |  |  |  |  |
| `rfq.award` | L | L |  | X |  |  |  |  | Award itself has no amount limit; the PO that follows does |
| `quote.view` | X | X | X | X |  |  | X |  | Prices need `price.view` |
| `quote.capture` | X |  |  |  |  |  |  |  |  |
| `quote.capture_late` | X | X |  |  |  |  |  |  | Capture after the deadline |
| `purchase_order.view` | X P | X P | X | X | X P (no prices) | X P (no prices) | X |  |  |
| `purchase_order.create`, `.submit`, `.withdraw` | X | X |  |  |  |  |  |  |  |
| `purchase_order.approve` |  | L | L | L (large) |  |  |  |  | Through approval engine, not a direct endpoint; BR-X-07 |
| `purchase_order.issue` | L | L |  | X |  |  |  |  | Officer limit from `project_members.limits.po_issue` |
| `purchase_order.acknowledge_on_behalf` | X |  |  |  |  |  |  |  |  |
| `purchase_order.amend`, `.cancel`, `.close` | X | X |  |  |  |  |  |  | Amend approval by delta |
| `goods_receipt.view` | X | X | X |  | X | X | X |  | QA/QC may be granted |
| `goods_receipt.create`, `.post` |  |  |  |  | X | X |  |  | Officer may post when granted |
| `goods_receipt.accept_over` | X |  |  |  | X |  |  |  | Reason mandatory |
| `goods_receipt.reverse` | X | X |  |  |  |  |  |  |  |
| `return_note.manage` | X |  |  |  |  |  |  |  |  |
| `invoice.view` | X | X | X | X |  |  | X |  |  |
| `invoice.register` | X |  | X |  |  |  |  |  | Data entry |
| `invoice.match`, `.hold`, `.dispute`, `.reject` |  |  | X |  |  |  |  |  | Officer may `dispute` |
| `invoice.accept_variance` |  |  | L | L |  |  |  |  | Limit on sum of absolute variance (BR-PRC-47) |
| `invoice.approve` |  |  | L | L |  |  |  |  | By `net_payable_amount`; not the registrant |
| `payment.view` | X | X | X | X |  |  | X |  | Officer sees status only |
| `payment.create`, `.release`, `.execute` |  |  | X |  |  |  |  |  | Four-eyes with approval (BR-PRC-53) |
| `payment.confirm_new_bank_account` |  |  | X | X |  |  |  |  | Second person, not the batch creator |
| `supplier.view` | X | X | X | X | X (v) |  | X |  |  |
| `supplier.manage`, `supplier.rate` | X |  | X (manage) |  |  |  |  |  |  |
| `supplier.override_status` |  | X |  | X |  |  |  |  | Reason mandatory |
| `register.view`, `alert.view` | X | X | X | X | X (qty) |  | X |  |  |
| `alert.manage` | X | X | X |  |  |  |  |  |  |
| `settings.manage` |  |  |  |  |  |  |  | X |  |
| `report.export` | X | X | X | X |  |  | X |  | Exports obey `price.view` |
| `price.view` | X | X | X | X |  |  | X | X | **Field visibility** permission |
| `budget.override` |  | X |  | X |  |  |  |  | Soft/hard override with reason |

Roles: PO = `procurement_officer`, PM = `project_manager`, FIN = `finance_controller`, OWN = `owner_executive`, SUP = `superintendent`, FOR = `foreman`, CQS = `cost_qs_engineer`, TA = `tenant_admin`. `supplier_user` has no internal permissions; portal endpoints check audience `portal` and `party_id` (BR-PRC-77).

**Field visibility (BR-X-06, BR-PRC-76).** Without `procurement.price.view` (foreman, superintendent, and any custom role without it) these fields are omitted, not nulled: `unitPrice`, `lineAmount`, `discountPct`, `discountAmount`, `transportAmount`, `vatAmount`, `totalAmount`, `baseTotalAmount`, `estimatedAmount`, `acceptedUnitPrice`, `acceptedAmount`, all invoice and payment amounts, `budget*` and `budgetDeltaAmount`, `landedAmount`, `effectiveAmount`, all `rm_*` money columns, and the same columns in exports. Quantity, date, status and supplier name stay. Foreman and superintendent never see RFQ comparison, quotes (foreman: no endpoint access at all), invoices or payments. Sorting or filtering by a hidden field returns `403 procurement.field_not_permitted`.

**Project scope.** Every list query joins `project_members` (BR-X-09). Supplier-level data (profiles, scorecards) is tenant-wide for holders of `supplier.view`.

**Segregation of duties (BR-X-07).** Enforced in the domain, not only in the approval engine: PO approver != submitter; invoice approver != registrant and != variance acceptor; payment batch approver != batch creator and != any payment creator (not overridable); new bank account confirmer != account editor.

---

## 11. Notifications

Channels per 00 §3.12: `in_app` (always), `push` (mobile), `email`, `whatsapp` (R1 for supplier RFQ/PO links only, template-approved). Recipient = role within the project unless stated. Messages are Arabic first; `{...}` are parameters.

| Template code | Recipients | Trigger | Channels | Arabic sample |
| --- | --- | --- | --- | --- |
| `PRC_PR_NEW` | assigned officer | PR created from site request | in\_app, push | طلب شراء جديد \{prNumber\} للمشروع \{project\}: \{lineCount\} بنود مطلوبة بتاريخ \{neededBy\}. |
| `PRC_PR_DUPLICATE` | officer | duplicate flag set | in\_app | تنبيه: البند \{item\} مطلوب سابقاً في \{otherNumber\} خلال \{days\} أيام. |
| `PRC_SOURCING_STALLED` | officer | `sourcing_stalled` alert | in\_app, push | لم يصل عرض تلقائي للبند \{item\} منذ \{hours\} ساعات. يمكنك التحويل إلى طلب عروض يدوي. |
| `PRC_RECO_READY` | officer | recommendation ready | in\_app, push | توصية شراء جاهزة للمراجعة: \{item\}، الأفضل \{supplier\} بسعر فعلي \{amount\} \{currency\}. |
| `PRC_RFQ_INVITE` | supplier contacts | RFQ sent | email, whatsapp | دعوة لتقديم عرض سعر \{rfqNumber\}. آخر موعد \{deadline\}. الرابط: \{link\} |
| `PRC_RFQ_CLOSED` | officer | deadline passed | in\_app | أُغلق طلب العروض \{rfqNumber\}: وصل \{quoted\} من \{invited\} عروض. |
| `PRC_QUOTE_RECEIVED` | officer | portal quote submitted | in\_app | وصل عرض جديد من \{supplier\} لطلب العروض \{rfqNumber\}. |
| `PRC_PO_APPROVAL` | approver | PO/amendment submitted | in\_app, push, email | أمر الشراء \{poNumber\} بقيمة \{amount\} \{currency\} بانتظار اعتمادك. |
| `PRC_PO_APPROVED` / `PRC_PO_REJECTED` | submitter | approval outcome | in\_app, push | تم اعتماد أمر الشراء \{poNumber\}. / تم رفض أمر الشراء \{poNumber\}: \{reason\}. |
| `PRC_PO_ISSUED` | supplier contacts, officer | PO issued | email, whatsapp, portal | أمر شراء جديد \{poNumber\} من \{tenant\}. يرجى التأكيد: \{link\} |
| `PRC_PO_NO_ACK` | officer | no response in 72 h | in\_app | لم يؤكد \{supplier\} أمر الشراء \{poNumber\} منذ \{hours\} ساعة. |
| `PRC_PO_ACK_CHANGES` | officer | supplier confirms with changes or declines | in\_app, push | \{supplier\} طلب تعديل أمر الشراء \{poNumber\}: \{summary\}. |
| `PRC_DELIVERY_AT_RISK` | officer, superintendent | at-risk slot | in\_app, push | توريد \{item\} (\{poNumber\}) مقرر \{plannedDate\} ولم يصل بعد. |
| `PRC_DELIVERY_LATE` | officer, PM, superintendent | late slot (critical when activity starts \<= 7 days) | in\_app, push | تأخر توريد \{item\} \{days\} يوم، ويؤثر على نشاط \{activity\} الذي يبدأ \{start\}. |
| `PRC_GRN_POSTED` | officer | receipt posted | in\_app | استلام \{grnNumber\} على \{poNumber\}: مقبول \{accepted\} \{unit\}، مرفوض \{rejected\}. |
| `PRC_GRN_OVER` | officer | over-delivery accepted/flagged | in\_app | الكمية المستلمة من \{item\} تجاوزت المطلوب بنسبة \{pct\}%. |
| `PRC_INVOICE_EXCEPTION` | finance | invoice in `exception` | in\_app, email | فاتورة \{registrationNumber\} من \{supplier\} بها فروقات تحتاج قرارك: \{summary\}. |
| `PRC_INVOICE_APPROVAL` | approver | submitted for payment approval | in\_app, push | فاتورة \{registrationNumber\} بصافي \{amount\} \{currency\} بانتظار اعتمادك للدفع. |
| `PRC_INVOICE_DUE` | finance | due in 3 days / overdue | in\_app, email | \{count\} فواتير تستحق خلال 3 أيام بإجمالي \{amount\} \{currency\}. |
| `PRC_DISPUTE_OPENED` / `PRC_DISPUTE_REPLY` | supplier / officer, finance | dispute opened / supplier replied | email, portal, in\_app | تم فتح اعتراض على فاتورة \{supplierInvoiceNo\}: \{reason\}. |
| `PRC_PAYMENT_APPROVAL` | approver | batch submitted | in\_app, push | دفعة \{batchNumber\} بإجمالي \{amount\} \{currency\} بانتظار اعتمادك. |
| `PRC_PAYMENT_EXECUTED` | supplier contacts, officer | payment executed | email, portal | تم تحويل \{amount\} \{currency\} لفاتورتكم \{supplierInvoiceNo\}. المرجع \{bankReference\}. |
| `PRC_SUPPLIER_DOC_EXPIRY` | officer | document expiring/expired | in\_app, email | مستند \{docType\} للمورد \{supplier\} ينتهي في \{date\}. |
| `PRC_BUDGET_WARNING` | PM | soft overrun approved with reason | in\_app | تجاوز ميزانية \{workPackage\} بقيمة \{amount\} بسبب: \{reason\}. |

Rules: notifications are generated by handlers after commit through `INotificationService` (idempotent by `(template, recordId, version)`); recipients are trimmed by project membership; money parameters are omitted from messages sent to roles without `price.view`; supplier messages contain no internal fields; user preferences may switch off `push`/`email` but not `in_app` for approvals.

---

## 12. Error codes

Format per 00 §3.7 (`code` = `procurement.<name>`; table lists `<name>`). Platform codes (`validation_failed`, `unauthorized`, `forbidden`, `not_found`, `precondition_failed` 412, `idempotency_key_reuse` 422, `too_many_requests`) are not repeated.

| Code | HTTP | When | Arabic message | English message |
| --- | --- | --- | --- | --- |
| `work_package_project_mismatch` | 422 | BR-PRC-01 | حزمة العمل لا تنتمي لهذا المشروع. | The work package does not belong to this project. |
| `pr_not_editable` | 409 | PR beyond `submitted` edited | لا يمكن تعديل طلب الشراء في حالته الحالية. | The purchase request can no longer be edited. |
| `pr_empty` | 422 | submit without lines | طلب الشراء بلا بنود. | The purchase request has no lines. |
| `route_required` | 422 | approve with line without route | حدد مسار الشراء لكل بند. | Choose a route for every line. |
| `budget_reason_required` | 422 | soft overrun without reason | سبب تجاوز الميزانية مطلوب. | A reason is required for exceeding the budget. |
| `pr_has_issued_po` | 409 | cancel PR with issued PO | يوجد أمر شراء صادر على هذا الطلب. | An issued purchase order exists for this request. |
| `route_locked` | 409 | route change after PO draft | لا يمكن تغيير المسار بعد إنشاء أمر الشراء. | The route cannot be changed once an order exists. |
| `po_exceeds_request` | 200 warning | BR-PRC-07 | كمية الأمر تتجاوز كمية الطلب. | Order quantity exceeds requested quantity. |
| `supplier_not_eligible` | 422 | BR-PRC-05 | المورد غير مؤهل لهذه العملية. | The supplier is not eligible for this action. |
| `supplier_blocked` | 422 | PO/payment for blocked supplier | المورد محظور. | The supplier is blocked. |
| `supplier_documents_expired` | 422 / warning | expired documents, setting on | مستندات المورد منتهية. | Supplier documents have expired. |
| `pr_line_in_open_rfq` | 409 | BR-PRC-20 | البند مدرج في طلب عروض مفتوح. | The line is already in an open RFQ. |
| `rfq_not_editable` | 409 | edit after send | لا يمكن تعديل طلب العروض بعد الإرسال. | The RFQ cannot be edited after sending. |
| `rfq_invalid` | 422 | no lines/invitations | طلب العروض ناقص. | The RFQ is incomplete. |
| `deadline_in_past` | 422 | deadline \< now + 1 h | الموعد النهائي غير صالح. | The deadline is not valid. |
| `rfq_not_open` | 409 | action on closed RFQ | طلب العروض غير مفتوح. | The RFQ is not open. |
| `quote_exists_use_revise` | 409 | second quote same supplier | يوجد عرض لهذا المورد، استخدم تعديل العرض. | A quote exists for this supplier; revise it instead. |
| `rfq_closed_late_capture_denied` | 403 | late capture without permission | انتهى موعد العروض ولا تملك صلاحية الإدخال المتأخر. | The deadline passed and you may not capture late quotes. |
| `comparison_frozen` | 409 | change after award | المقارنة مجمدة بعد الترسية. | The comparison is frozen after award. |
| `no_quotes` | 422 | compute without quotes | لا توجد عروض للمقارنة. | There are no quotes to compare. |
| `award_override_reason_required` | 422 | BR-PRC-19 | سبب اختيار عرض غير الأفضل مطلوب. | A reason is required when not awarding the top-ranked quote. |
| `quote_expired` / `quote_excluded` | 422 | award expired or excluded quote | العرض منتهي / مستبعد. | The quote is expired / excluded. |
| `po_version_frozen` | 409 | edit non-draft version | النسخة معتمدة ولا يمكن تعديلها. | This version is approved and cannot be edited. |
| `po_over_budget` | 422 | hard block | قيمة الأمر تتجاوز الميزانية المتبقية. | The order exceeds the remaining budget. |
| `po_over_budget_soft` | 200 warning | soft overrun | تنبيه: الأمر يتجاوز الميزانية المتبقية. | Warning: the order exceeds the remaining budget. |
| `po_schedule_mismatch` | 422 | BR-PRC-23 | جدول التوريد لا يطابق الكمية. | The delivery schedule does not match the quantity. |
| `po_not_approved` | 409 | issue before approval | أمر الشراء غير معتمد. | The order is not approved. |
| `po_not_issued` | 409 | acknowledge before issue | أمر الشراء لم يصدر بعد. | The order has not been issued. |
| `limit_exceeded` | 403 | amount above user limit | القيمة تتجاوز حد صلاحيتك. | The amount exceeds your authority limit. |
| `period_closed` | 422 | BR-PRC-74 | الفترة المحاسبية مقفلة. | The accounting period is closed. |
| `amendment_exists` | 409 | BR-PRC-26 | يوجد تعديل مفتوح على هذا الأمر. | An amendment is already open. |
| `po_closed` | 409 | amend closed/cancelled PO | أمر الشراء مغلق. | The order is closed. |
| `short_close_below_received` | 422 | BR-PRC-30 | لا يمكن الإغلاق دون الكمية المستلمة. | Cannot short-close below received quantity. |
| `po_has_receipts` / `po_has_invoices` | 409 | cancel with receipts/invoices | يوجد استلام / فواتير على الأمر. | Receipts / invoices exist on the order. |
| `po_has_open_amounts` | 409 | close with open balance | توجد كميات أو مبالغ مفتوحة. | Open quantities or amounts remain. |
| `grn_not_draft` | 409 | edit posted receipt | الاستلام مرحّل. | The receipt is already posted. |
| `receipt_exceeds_tolerance` | 422 | BR-PRC-35 | الكمية تتجاوز حد السماح. | Quantity exceeds the delivery tolerance. |
| `receipt_evidence_required` | 422 | BR-PRC-31 | أرفق صورة إذن التسليم أو البضاعة. | Attach delivery note or goods photo. |
| `duplicate_delivery_note` | 409 | BR-PRC-37 | رقم إذن التسليم مسجل من قبل. | This delivery note number is already recorded. |
| `receipt_already_invoiced` | 409 | BR-PRC-38 | الكمية مفوترة بالفعل. | The quantity has already been invoiced. |
| `return_exceeds_rejected` | 422 | BR-PRC-39 | كمية المرتجع أكبر من المرفوض. | Return exceeds rejected quantity. |
| `invoice_not_editable` | 409 | edit after matched | لا يمكن تعديل الفاتورة. | The invoice cannot be edited. |
| `invoice_arithmetic_mismatch` | 422 | BR-PRC-42 | إجمالي الفاتورة لا يطابق الحساب. | Invoice total does not match the calculation. |
| `duplicate_invoice` | 409 | BR-PRC-41 | رقم الفاتورة مسجل لنفس المورد. | This invoice number exists for the supplier. |
| `invoice_without_po_not_allowed` | 422 | BR-PRC-40 | الفاتورة بدون أمر شراء غير مسموحة. | Invoices without an order are not allowed. |
| `variance_not_acceptable` | 422 | BR-PRC-47 | لا يمكن قبول هذا الفرق. | This variance cannot be accepted. |
| `dispute_exists` | 409 | BR-PRC-50 | يوجد اعتراض مفتوح. | A dispute is already open. |
| `credit_note_required` | 422 | resolve with credit but none | سجل الإشعار الدائن أولاً. | Register the credit note first. |
| `invoice_not_matched` | 409 | approve unmatched | الفاتورة غير مطابقة. | The invoice is not matched. |
| `invoice_disputed` | 409 | approve disputed | الفاتورة محل اعتراض. | The invoice is disputed. |
| `invoice_not_payable` | 422 | batch with unapproved invoice | الفاتورة غير جاهزة للدفع. | The invoice is not ready for payment. |
| `payment_exceeds_outstanding` | 422 | BR-PRC-58 | المبلغ أكبر من المتبقي. | Amount exceeds the outstanding balance. |
| `bank_account_unverified` | 422 | BR-PRC-55 | حساب المورد البنكي غير موثق. | The supplier bank account is not verified. |
| `new_bank_account_confirmation_required` | 422 | account changed \< 7 days | يلزم تأكيد ثان للحساب الجديد. | A second confirmation is required for the new account. |
| `batch_not_approved` | 409 | release unapproved | الدفعة غير معتمدة. | The batch is not approved. |
| `bank_reference_required` | 422 | BR-PRC-54 | مرجع التحويل مطلوب. | Bank reference is required. |
| `payment_already_executed` | 409 | cancel executed | الدفعة نُفذت بالفعل. | The payment has already been executed. |
| `self_approval_not_allowed` | 403 | BR-X-07 | لا يمكنك اعتماد عملية قمت بها. | You cannot approve your own work. |
| `fx_rate_required` | 422 | BR-PRC-75 | سعر الصرف مطلوب. | An exchange rate is required. |
| `field_not_permitted` | 403 | sort/filter on hidden field | الحقل غير متاح لصلاحيتك. | This field is not available to your role. |
| `portal_rfq_closed` | 409 | portal quote after deadline | انتهى موعد تقديم العروض. | The quotation deadline has passed. |

---

## 13. Reports and read models

Read models are rebuilt from facts by `RegisterProjector` (incremental, same-transaction for `rm_procurement_register`; event-driven for the rest) and reconciled nightly (section 9). Standard columns apply; every table has `tenant_id`, `project_id` (indexed first after tenant).

### 13.1 Tables

| Table | Grain | Columns (besides keys) | Refresh |
| --- | --- | --- | --- |
| `rm_procurement_register` | one row per PR line (and per PO line once ordered) | `pr_line_id, po_id null, po_line_no null, pr_id, pr_number, po_number, work_package_id, cost_code_id, material_id, item_description, supplier_id, quantity, unit_code, needed_by_date, planned_delivery_date, stage, route, recommendation_id, rfq_id, ordered_qty, accepted_qty, invoiced_qty, paid_amount (P), ordered_amount (P), is_late, is_blocked_by_constraint, assigned_officer_id, updated_at`. Indexes `(tenant_id, project_id, stage)`, `(tenant_id, needed_by_date)`, `(tenant_id, supplier_id)`. | Same transaction as cause |
| `rm_trade_position` | (project, work\_package) | `ordered_amount, received_amount, invoiced_amount, paid_amount, ordered_not_delivered_amount, received_not_invoiced_amount, invoiced_not_paid_amount, budget_amount, remaining_budget_amount, as_of` (BR-PRC-61; all P). Golden check: 900,000 / 540,000 / 450,000 / 300,000 | Event-driven, 5 s lag |
| `rm_supplier_scorecard` | supplier | `reliability_score, event_count, on_time_pct, avg_days_late, rejection_pct, price_variance_avg_pct, open_po_count, open_po_base_amount (P), last_po_date, document_status` | After each rating event; nightly full |
| `rm_spend` | (project, supplier, material category, month) | `ordered_base_amount, received_base_amount, invoiced_base_amount, paid_base_amount (all P), po_count, grn_count` | Event-driven; months bucketed in Gregorian with Hijri label not stored (00 §3.5) |
| `rm_payables_due` | invoice | `invoice_id, supplier_id, project_id, due_date, net_payable_amount, outstanding_amount (P), status, days_overdue, aging_bucket (current, 1-30, 31-60, 61-90, 90+), bank_account_ok` | Daily plus on invoice/payment events |
| `rm_budget_cache` | (work package, cost code) | `budget_amount, committed_amount, budget_version, source_event_id, updated_at` — cache for `BudgetCheckService`; authoritative answer comes from `IBudgetCheck` | `cost.budget.*` events |

### 13.2 Reports (list and export; permission `report.export`; money columns need `price.view`)

| Code | Report | Columns | Filters |
| --- | --- | --- | --- |
| `procurement_register` | Procurement register | PR no, PO no, work package, item, quantity, unit, stage, supplier, needed by, planned delivery, late flag, ordered, received, invoiced, paid | project, stage, work package, supplier, late |
| `po_list` | Purchase order list | PO no, version, date, supplier, project, status, delivery/invoice/payment status, total, balance not delivered | project, supplier, status, date range |
| `open_po_balance` | Open orders and balances | PO, line, item, ordered, accepted, balance qty, balance amount, promised date | project, supplier |
| `late_deliveries` | Late and at-risk deliveries | PO, item, supplier, planned date, days late, linked activity, officer, last follow-up | project, severity |
| `receipts_log` | Goods receipts | GRN, date, PO, supplier, item, delivered, accepted, rejected, reason, receiver | project, date range |
| `invoice_matching` | Invoice matching exceptions | Invoice, supplier, PO, result per line, variance %, variance amount, age | status, supplier |
| `payables_aging` | Payables ageing | Supplier, invoices, outstanding, aging buckets | as-of date, project |
| `payment_register` | Payments | Payment no, batch, supplier, executed date, amount, bank reference, invoices | date range, supplier |
| `supplier_scorecard` | Supplier performance | Supplier, reliability, on-time %, rejection %, price variance, open POs | category |
| `spend_by_supplier` | Spend analysis | Supplier, category, ordered, invoiced, paid, share % | project, period |
| `trade_position` | Trade commitments | Work package, ordered, received, invoiced, paid, open commitment, accrued not paid | project |
| `price_history` | Material price history | Material, date, supplier, PO, unit price, quantity | material, date range |

Exports: xlsx/csv/pdf; Arabic headers when `lang=ar` (RTL sheet); numbers as numbers; row limit 100,000; above 5,000 rows asynchronous through `ExportReportJob` (`202` + job id, polled; download through `IFileService` signed URL). Exports repeat permission trimming and project scope; the export event is audited with filters.

---

---

## 15. Test scenarios, non-functional notes, security, open questions

### 15.1 Named scenarios (golden project parts that touch procurement)

| ID | Scenario | Inputs | Expected |
|---|---|---|---|
| S1 | Cement comparison | Need 60 t in 2 days, budget 3,000/t; A 3,050 x 60 transport included, 30 days credit; B 2,950 x 60 + 4,200 transport, cash; C 2,900 x 60 + 3,000, 60 days, lead 5 days; rate 2%/month, weights 70/15/15, reliability 70 | A landed 183,000, credit value 3,660, effective 179,340, score 95.5000 rank 1; B landed 181,200, effective 181,200, score 87.2815 rank 2; C excluded `too_late`; A budget delta +3,000; awarding A yields PO total 208,620 (VAT 25,620) |
| S2 | Award override | Choose B | 422 without reason; with reason PO created and `award_override_reason` stored; sheet frozen |
| S3 | Partial deliveries | PO 800 pipes at 85 (68,000); GRN-1 300/300; GRN-2 delivered 260, accepted 250, rejected 10 | Accrued 25,500 then 21,250 (total 46,750); accepted to date 550; balance 250; `partly_received`; over limit 840 |
| S4 | Over-delivery | Same PO, accepted to date 835 then +10 | Total 845 > 840: 422 without `accept_over`; with it `over_accepted_with_permission` and warning alert; 838 accepted `within` + info alert |
| S5 | Invoice exception | Invoice 800 at 88.40 against 550 accepted | `qty_over_received`, price variance 4.0000% (88.40 vs 85), amount variance 2,720, gross 80,620.80, status `exception`; corrected invoice 550 at 85 = net 46,750, VAT 6,545, gross 53,295 matches |
| S6 | Partial payment | Net 100,000, net payable 113,000, pay 56,500 then 56,500 | First: settled 0.5, paid ex VAT 50,000, outstanding 56,500; second: paid ex VAT total exactly 100,000, outstanding 0, invoice `paid` |
| S7 | Five spend states | Plumbing trade: committed 900,000, accrued 540,000, invoiced 450,000, paid 300,000 | Trade position: ordered-not-delivered 360,000, received-not-invoiced 90,000, invoiced-not-paid 150,000; cost shows AC 540,000 (feeds CPI 0.89 with BAC 1,200,000 at 40% and EAC 1,350,000 in the cost module) |
| S8 | Supplier lateness | Planned 12 Oct, received 14 Oct | `late_delivery` event score 80; with `on_time` events of 100 x2 the 12-month average = 93.3333 |
| S9 | Budget guard | Remaining budget 180,000, PO 183,000 | `warn`: warning and reason; `block`: 422 `po_over_budget`; override with `budget.override` records user and reason |
| S10 | Segregation | Same user submits and approves PO / invoice / batch | 403 `self_approval_not_allowed` each |
| S11 | Offline sync | Bulk of 500 receipts with 20 repeated `clientRef` | 480 created, 20 `existing`; no duplicate accrual events |
| S12 | Tenant isolation | Tenant B requests tenant A ids on every endpoint | 404; zero rows in lists and exports |
| S13 | Visibility | Foreman and superintendent call PO, receipt, register | No price, amount or budget fields; hidden sort 403 |
| S14 | Closed period | September closed, receipt dated 28 Sept | 422 `period_closed`; after reopen with reason the post succeeds and is audited |

### 15.2 Non-functional notes

- **Volumes per tenant per year** (00 §3.17): 10,000 POs (about 40,000 lines), 25,000 receipts, 12,000 invoices, 8,000 payments, 500,000 audit rows, 200,000 files; plan partitioning only after 5 years of data.
- **Indexes** (beyond section 4): `purchase_orders (tenant_id, project_id, status, created_at desc)`, `(tenant_id, supplier_id, status)`, `(tenant_id, needed_by_date) where status in ('issued','acknowledged','partly_received')`; `purchase_order_lines (po_id, line_no)`; `goods_receipt_lines (tenant_id, po_id, po_line_no)`; `supplier_invoices (tenant_id, supplier_id, supplier_invoice_no) where status not in ('rejected','cancelled')`, `(tenant_id, status, due_date)`; `payment_allocations (invoice_id)`; `rm_procurement_register (tenant_id, project_id, stage)`; keyset pagination uses `(sort_field, id)` composite indexes for each whitelisted sort.
- **Query patterns**: project register and PO lists (most frequent, read model, p95 \< 800 ms for 50 rows); receipt posting touches one PO line balance row with `SELECT ... FOR UPDATE` (p95 \< 300 ms); matching runs asynchronously within 5 s of a receipt; dashboards read `rm_*` (\< 2 s).
- **Concurrency**: balance rows are the lock point for receipts, invoices and payments; `If-Match` ETag on aggregates; all posting handlers are retry-safe through Idempotency-Key and unique business keys.
- **Outbox lag** p95 \< 5 s; event contract tests in CI; reconciliation job asserts that `sum(ordered_amount) in rm_trade_position` equals commitments reported by `cost` for golden data.
- **Files**: delivery note photos up to 10 MB each, 5 per receipt, thumbnails generated; offline sync must tolerate retry of the upload after the receipt.

### 15.3 Security notes

- Prices, rates, bank details and supplier quotes are the sensitive data; response trimming is central (PRC-047), not per endpoint; exports and PDFs use the same filter.
- Bank account changes are audited, require second confirmation within 7 days, and full IBAN appears only in the bank file; API responses show masked IBAN.
- Supplier portal tokens are random 256-bit values stored hashed, single-invitation, expire at RFQ deadline + 7 days, rate-limited; portal queries always filter by the user's `party_id`.
- Segregation of duties is enforced in the domain (section 10); override flags are tenant settings and are logged.
- All state changes write `status_history` and audit rows (PLT-E5); deletion of posted records is impossible (BR-X-04); file uploads are virus-scanned by the platform before linking.
- Idempotency keys are scoped by tenant, user and endpoint for 24 hours; reuse with a different body returns 422 `idempotency_key_reuse`.

### 15.4 Open questions (for the business)

1. Withholding tax: which rates apply to which supplier categories, and is the base net ex VAT or gross for each? (BR-PRC-44 uses net ex VAT by default.)
2. Stamp duty and other statutory deductions on supplier invoices: fixed percentages or case-by-case amounts?
3. Does the company pay VAT to suppliers in the same payment as the net amount, or may VAT be held until the supplier's tax invoice is filed?
4. Should the financing rate for credit value (default 2% per month) be one company-wide number or per project and per period? Who owns it, procurement or finance?
5. Which approval limits per role apply to POs, invoices and payments, and are they per project or per tenant? Is the owner always required above a fixed amount?
6. Is over-delivery tolerance (default 5%) the same for all materials, or should bulk materials (sand, cement by weight) have wider tolerance than fixtures?
7. When a supplier delivers less than ordered and says nothing more will come, who may short-close without approval: the officer, or always the project manager?
8. Do you require a second confirmation for new supplier bank accounts, and how many days should the cooling period be (default 7)?
9. Are cheques and cash payments needed in R1, or only bank transfers? Which bank file format do your banks accept?
10. May invoices be registered without a purchase order (petty and urgent purchases), and if so up to what amount?
11. Should a receipt be allowed against a PO that is approved but not yet issued (supplier delivered early on a phone order)?
12. How are returns and credit notes handled today: does the supplier send a credit note, or do you deduct from the next payment?
13. Should the reliability score count lateness in calendar days or working days, and does a delay caused by the site (access, not ready) excuse the supplier?
14. Must a PO to a supplier with expired documents (tax card, commercial register) be blocked, or only warned? Which documents are mandatory?
15. Which currencies other than EGP occur (USD, SAR, EUR), who sets the exchange rate, and on what date: order, invoice or payment?
