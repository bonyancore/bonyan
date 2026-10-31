---
title: "05 Planning and site execution"
doc_no: "05"
section: "Site and Quality"
status: "Proposal"
source: https://app.notion.com/p/f7e4cdd2129683e090db0112ed2630ca?pvs=204
exported: 2026-10-29 (Notion MCP)
---

# 05 — Planning and Site Execution (Module 3) — Backend Specification

**Key:** `PLN` \| **Schema:** `planning` \| **v3 sources:** Module 3 (lines 508-548), project spine 3.3-3.6 (293-350), flow F2 first part (192-201), F4 steps 1-2 (progress). <br> **Reads with:** `00-Backend-Architecture-and-Conventions.md` (conventions are not restated; references look like "per 00 §3.6"). Release tags: **R1** first, **R2**, **R3** marked on every feature that is not R1.

---

## 1. Purpose and business context

### 1.1 The business problem in plain language

A construction contractor earns money only when work on site is finished, measured and certified. Between the signed contract and the final handover, three questions are asked every day by the site team and every week by the project manager (PM):

1. **What should happen, and when?** (the *schedule*)
2. **What actually happened?** (the *progress* and the *daily report*)
3. **What stops it from happening next week, and what do we need to buy or arrange now?** (the *look-ahead* and *site requests*)

Module 3 answers these questions with records that every other module can use. Progress is the number that turns into **earned value (EV)** in Cost Control (file 08), into **measured quantities** for the client payment application (مستخلص) in Contracts (file 09), and into the subcontractor claim check in Subcontracts (file 11). A wrong or late progress number therefore means wrong cost forecasts, wrong cash forecasts and wrong invoices. That is why this module distinguishes **reported** progress (what the foreman typed) from **verified** progress (what the superintendent confirmed) and lets only verified progress feed money.

### 1.2 Terms (for engineers who do not know construction)

| Term | Meaning |
|---|---|
| **Work package (WP)** (حزمة عمل / بند أعمال) | One trade inside one project, for example "Plumbing - Building 3". Owned by `core` (file 02). It carries budget, spend and progress. Planning attaches activities and progress rules to it. |
| **Zone** (منطقة / دور) | A physical part of the project: building, floor, area. Owned by `core`. |
| **Activity** (نشاط) | A schedulable piece of work inside a WP and a zone: "Install waste pipes, floor 2". Has dates, duration, quantity, dependencies. |
| **Dependency / logic** (علاقة) | "B cannot start until A finishes". Types: **FS** finish-to-start, **SS** start-to-start, **FF** finish-to-finish, **SF** start-to-finish; plus **lag** (waiting days, may be negative). |
| **Calendar** (تقويم العمل) | Which days are working days. Egypt default: Friday is the weekly day off (Saturday to Thursday work), plus public holidays. |
| **Critical path** (المسار الحرج) | The chain of activities with zero **float** (spare time). A day lost on it is a day lost for the whole project. |
| **Float / slack** (الفائض الزمني) | How many working days an activity can slip without delaying the project (**total float**) or its successor (**free float**). |
| **Baseline** (الخطة الأساسية) | The approved, frozen copy of the plan against which progress is judged. A **revision** is a new approved baseline with a reason; the old ones are kept. |
| **Planned progress (PV%)** | The percentage that *should* be done at a date according to the baseline. Cost Control multiplies it by the budget (BAC) to get planned value (PV). |
| **Actual progress** | The percentage that *is* done, from progress entries. |
| **Progress methods** | **Quantity-based** (m² tiled / m² planned), **stage-weighted** (pipes laid 40%, tested 20%, fixtures 40%), **milestone** (0 or 100). One method per work package. |
| **Roll-up** | Combining activity progress into WP %, zone % and project % with weights. |
| **Daily site report** (تقرير الموقع اليومي) | The foreman's record of a day: weather, labour, equipment, quantities, deliveries, delays, photos. |
| **Site request** (طلب موقع) | A foreman's request for material, equipment or labour. Confirmed by the superintendent, then becomes a purchase request in Procurement (flow F2 steps 1-2). |
| **Look-ahead** (خطة الأسابيع الثلاثة) | The next three weeks of activities, each checked for readiness: drawings, materials, labour, equipment, predecessor work, permits, access. |
| **Constraint** (معوّق) | One missing thing that prevents an activity from starting ("tiles not delivered"). Cleared when solved. |
| **Weekly work plan / PPC** | Each week the team commits to a list of activities; at week end **Plan Percent Complete = completed / committed**. Reasons for missed items are recorded so the team can fix causes, not symptoms. |
| **Location board** (لوحة المواقع) | A matrix of zones (rows) by trades (columns) coloured by status. |
| **S-curve** (منحنى S) | Cumulative planned vs actual progress over time. |

### 1.3 Where it sits in the lifecycle (v3 section 2.1 and flows)

| Lifecycle stage | What this module does |
|---|---|
| 3 Project setup | Receives the project and WPs from `core`; builds the schedule (typed or imported from Excel/MS Project-like file); locks the first **baseline**; publishes `planning.schedule.baselined.v1` so Cost and Cash can plan against dates. |
| 4 Plan and mobilise | Look-ahead with constraints and readiness scoring; weekly work plan; material constraints feed Procurement expediting. |
| 5 Buy (F2 steps 1-2) | Foreman raises a **site request** from a phone in under a minute; superintendent confirms; `planning.site_request.confirmed.v1` creates the purchase request in Procurement. |
| 6 Build and record | Daily reports, progress entries with photos, supervisor verification, delay records. |
| 7 Control | Planned vs actual, SPI input, S-curve, location board, PPC; late and not-ready alerts. |
| 8 Bill (F4 steps 1-2) | Verified progress and quantities are the basis for the payment application; roll-up percentages are the basis for Cost EV. |

### 1.4 What this module does NOT own

- Budget, cost, EV, CPI: Cost Control (file 08). Planning supplies **planned %** and **verified actual %**; Cost multiplies by BAC.
- Purchase requests, POs, receipts: Procurement (file 06). Planning stores only references and replicated status.
- Documents, RFIs, inspections, permits: `docs` (file 12). Planning stores **constraints** that point to them and clears them on events.
- Zones, work packages, trade templates, units: `core` (file 02).
- Variations and extension of time: Contracts (file 09). Planning only turns an approved time impact into a **proposed revision** for the PM.

### 1.5 Hidden cost data (BR-X-06)

The module itself is almost cost-free. Fields that contain cost are **omitted** (not nulled) for `foreman` and `superintendent` and for any role without `cost.budget.view_amounts`: `activity.budgetAmount`, `wpProgressConfig.budgetWeightAmount`, and any `*Amount` in the schedule-compare views. Percentages, quantities and dates are visible to all site roles. The v3 text "system shows remaining budget" on confirming a site request is delivered by the **Cost read model** (client calls the cost budget-status endpoint, file 08); `GET` site-request responses never carry budget numbers.

---

## 2. Users and roles

| Role (00 §4.4) | What they do in this module |
|---|---|
| `foreman` | Creates **site requests**; writes the **daily report**; enters **progress** with photos from the phone (Idempotency-Key mandatory, offline queue); sees own look-ahead items; records delays. Cannot verify progress, cannot confirm requests, cannot see cost fields. |
| `superintendent` | Confirms/rejects/returns site requests; **verifies** progress; reviews daily reports; manages look-ahead and constraints; commits the weekly plan; edits the working schedule (dates, logic) but cannot approve a baseline. |
| `project_manager` | Builds and edits the schedule; imports; creates and **approves baselines/revisions**; configures progress methods and weights; applies time impacts from variations; reads S-curve, PPC. |
| `cost_qs_engineer` | Reads progress and quantities; sets activity budget weights and measured quantities; reads S-curve and planned %. Cannot edit the schedule logic. |
| `procurement_officer` | Reads look-ahead material constraints and site requests (view); sees which materials are blocking activities. Cannot edit planning data. |
| `contracts_manager` | Reads delays and time-impact records (view); applies extension-of-time info (R2). |
| `qa_qc_engineer`, `safety_officer`, `document_controller` | View schedule and look-ahead; create/clear constraints of their own type (drawing, inspection, permit). |
| `owner_executive` | Reads status, S-curve, PPC, location board. |
| `tenant_admin` | Company calendars, reason codes, planning settings defaults. |
| External (`subcontractor_user`) **R2** | Portal: sees own activities in the look-ahead, submits progress claims (consumed by Subcontracts, file 11), records own delays. Out of scope for the R1 API except the shared endpoints listed with permission `planning.activity.view_own`. |

Project scope: every endpoint is trimmed by `platform.project_members` (BR-X-09). Foremen may additionally be restricted to **zones or work packages** (`limits.zoneIds`, `limits.workPackageIds` in project\_members); list endpoints add that filter.

---

## 3. Services and components

Patterns per 00 §2.3. **Aggregates** (rich, rule-heavy) vs **CRUD** is stated for each.

| Component | Type | Responsibility |
|---|---|---|
| `ScheduleAggregate` (schedule + activities + dependencies of one project) | Aggregate | Edits to logic and dates; cycle detection on every dependency change; keeps `schedule.is_dirty` until recalculated; blocks edits when the project is closed. |
| `ScheduleCalculator` (domain service) | Calculator | CPM forward/backward pass over working calendars, lag, constraints, actuals; outputs early/late dates, total/free float, critical flag, project finish. Pure function: same inputs and `formula_version` give the same output (BR-X-10). |
| `ScheduleCalcRunner` (handler + job) | Handler/Job | Debounced recalculation; stores `schedule_calc_runs`; writes results back to activities; refreshes dependent read models. |
| `WorkCalendarService` | Domain service | `AddWorkdays`, `NextWorkday`, `WorkdaysBetween` using calendar weekdays and exceptions (Friday off default, holidays, Ramadan hours are informational). |
| `BaselineAggregate` | Aggregate | Create draft baseline from current working schedule, refresh, approve (freeze), supersede previous; versioning per 00 §3.3. |
| `PlannedProgressCalculator` | Calculator | Planned % per activity/WP/zone/project at any date from a baseline; writes `baseline_planned_curve` at approval. |
| `ProgressMethodStrategy` (`QuantityStrategy`, `StageStrategy`, `MilestoneStrategy`) | Domain service | Converts an entry into activity %; enforces validations (over 100%, backward). |
| `ProgressRollupService` | Calculator | Activity -> WP -> zone -> project percentages, reported and verified, with weights. |
| `ProgressEntryAggregate` | Aggregate | Entered -> Verified/Rejected; cumulative quantity consistency; auto-sets actual start/finish; emits events. |
| `ProgressSnapshotJob` | Job | Daily per-project snapshot into `rm_progress_daily` (planned, reported, verified). |
| `DailyReportAggregate` | Aggregate | Draft -> Submitted -> Reviewed -> Locked; owns manpower, equipment, quantities, deliveries, visitors, notes; can generate progress entries from quantity lines. |
| `DelayRegister` | CRUD + rules | Delay events with cause category and evidence; R1 minimal (created from daily report), R2 full register. |
| `SiteRequestAggregate` | Aggregate | Submit/confirm/reject/return/cancel; line validation; mobile idempotency; publishes `site_request.confirmed`. |
| `ConstraintAggregate` | Aggregate | Open/clear/waive constraints; blocking vs non-blocking; publishes blocked/cleared events. |
| `ConstraintGenerator` | Domain service | Creates constraints from `activity_requirements`, predecessor status, replicated receipts and document status. |
| `ReadinessScorer` | Calculator | Readiness score and status (ready / at risk / blocked) per activity (BR-PLN-40..43). |
| `WeeklyPlanAggregate` | Aggregate | Draft -> Committed -> Closed; item outcomes; PPC computation. |
| `LocationBoardProjector`, `LookAheadProjector`, `PpcProjector`, `SCurveQuery` | Projections/queries | Read models in section 13. |
| `ScheduleImporter` (via `IImportService`) | Import adapter | Excel/CSV (R1), MS Project XML (MSPDI) and Primavera XER (**R2**) parsers -> activities and dependencies. |
| `PlanningEventHandlers` | Consumers | One handler class per consumed event (section 8), each idempotent via `processed_events`. |
| `PlanningContracts` (`Planning.Contracts`) | Public interfaces | `IPlannedProgressQuery.GetPlannedPercent(projectId, asOf, workPackageId?)`, `IProgressQuery.GetVerifiedProgress(...)`, `IVerifiedQuantityQuery.GetVerifiedQuantities(projectId, from, to, boqItemId?)` used by Cost, Contracts, Subcontracts. |
| CRUD parts | CRUD | Calendars, reason codes, project settings, photos, visitors, notes, stages, requirements. |

---

## 4. Domain model

Schema `planning`. Every table has the standard columns of 00 §3.2 (`id, tenant_id, created_at/by, updated_at/by, row_version, is_deleted, source`) unless it says "append-only". Transactions are never deleted (00 §3.2); `is_deleted` is used only on settings and reference rows. Every table carries `tenant_id` (RLS) and, except settings defaults, `project_id`. FKs to `core.projects`, `core.work_packages`, `core.zones`, `platform.users` are real FKs (00 §5.4); FKs to other modules are by value. Money fields exist only in the places marked *hidden-cost*.

Working-day arithmetic is **not** stored here: the calendar of a project is `core` (BR-COR-09: company weekend days, project overrides, holidays; default weekend Friday and Saturday). Planning calls `ICoreCalendar.IsWorkingDay(projectId, date)` and caches the project calendar per calculation run.

### 4.1 `planning.planning_settings` (CRUD; tenant default row has `project_id null`)

| column | type | null | notes |
|---|---|---|---|
| `project_id` | uuid | yes | null = tenant default; project row overrides field by field |
| `late_alert_days` | int | no | default 3; activity late by more than N days raises alert (BR-PLN-52) |
| `lookahead_weeks` | int | no | default 3, check 1..6 |
| `not_ready_alert_days` | int | no | default 5 (v3: not ready 5 days before start) |
| `daily_report_cutoff_time` | time | no | default 18:00 project local; after it a missing report is "late" |
| `daily_report_lock_hour` | int | no | default 10: reports of day D lock at that hour on D+1 |
| `photo_required_for_progress` | bool | no | default true when progress increase >= `photo_required_min_delta_pct` |
| `photo_required_min_delta_pct` | numeric(7,4) | no | default 5 |
| `verification_required` | bool | no | default true; false = entries auto-verify (small projects, flagged `auto_verified`) |
| `backward_progress_needs_reason` | bool | no | default true (always true in R1, field reserved) |
| `readiness_weights` | jsonb | no | default `{"drawing":25,"material":30,"labour":15,"equipment":10,"predecessor":20,"permit":10,"inspection":10,"access":5}` |
| `ppc_target_pct` | numeric(7,4) | no | default 80 |
| `schedule_formula_version` | int | no | default 1 (stored with results, BR-X-10) |

Keys: unique `(tenant_id, coalesce(project_id, zero-uuid))`.

### 4.2 `planning.schedules` (aggregate root; one **working** schedule per project)

| column | type | null | notes |
|---|---|---|---|
| `project_id` | uuid | no | FK; unique `(tenant_id, project_id)` where `is_deleted=false` |
| `name` | text | no | default project name |
| `data_date` | date | no | "status date": progress is measured up to here; default today, moved by PM or by the weekly job |
| `start_date` | date | no | project start (activities may not start earlier unless a constraint says so) |
| `finish_date` | date | yes | calculated project finish (max early finish) |
| `is_dirty` | bool | no | true when logic/dates changed since the last calc (BR-PLN-07) |
| `last_calc_run_id` | uuid | yes | FK `schedule_calc_runs` |
| `current_baseline_id` | uuid | yes | FK `baselines`, the approved current baseline |
| `source_type` | text | no | `manual`, `trade_template`, `import_excel`, `import_mspdi`, `import_xer` |
| `status` | text | no | `active`, `frozen` (project closed) |

Check: `status in ('active','frozen')`.

### 4.3 `planning.activities` (aggregate member of `ScheduleAggregate`)

| column | type | null | notes |
|---|---|---|---|
| `schedule_id` | uuid | no | FK |
| `project_id`, `work_package_id` | uuid | no | WP required for `task` and `milestone`; null only for `summary` |
| `zone_id` | uuid | yes | FK `core.zones`; null = whole project |
| `parent_id` | uuid | yes | FK self; the parent must be `activity_type='summary'`; depth \<= 4 |
| `wbs_code` | text | no | `1.2.3` maintained by the service; unique `(schedule_id, wbs_code)` |
| `activity_code` | text | no | business code `A-0001` from series `PLN_ACT` per project; unique `(tenant_id, project_id, activity_code)`; `external_ref` for imported id |
| `name_ar`, `name_en` | text | no | at least one |
| `activity_type` | text | no | `task`, `milestone`, `summary` |
| `unit_code` | text | yes | required when the WP method is `quantity` |
| `planned_qty` | numeric(18,4) | yes | quantity to execute; required for `quantity` method; check `>= 0` |
| `duration_days` | int | no | working days; `0` for milestones; check `>= 0` |
| `constraint_type` | text | no | `asap` (default), `snet` start-no-earlier-than, `fnlt` finish-no-later-than, `mso` must-start-on |
| `constraint_date` | date | yes | required unless `asap` |
| `early_start`, `early_finish`, `late_start`, `late_finish` | date | yes | calculator output |
| `total_float_days`, `free_float_days` | int | yes | calculator output (working days, may be negative) |
| `is_critical` | bool | no | `total_float_days <= 0` |
| `start_date`, `finish_date` | date | yes | the **current plan** dates shown on the Gantt = early dates unless the PM pinned dates (`is_pinned`); after actual start they become `actual_start` / forecast |
| `is_pinned` | bool | no | PM-fixed dates, ignored by forward pass except as a constraint |
| `actual_start`, `actual_finish` | date | yes | set by progress entries (BR-PLN-24) |
| `progress_pct_reported` | numeric(7,4) | no | 0..100, from latest `entered` or `verified` entry |
| `progress_pct_verified` | numeric(7,4) | no | 0..100, from latest `verified` entry |
| `executed_qty_reported`, `executed_qty_verified` | numeric(18,4) | no | cumulative |
| `weight` | numeric(18,6) | no | roll-up weight inside its WP (BR-PLN-31); default from `weight_basis` |
| `budget_amount` | numeric(18,4) | yes | *hidden-cost*; set by `cost_qs_engineer`/PM only when `weight_basis='budget'`; currency = base |
| `status` | text | no | `not_started`, `in_progress`, `completed`, `on_hold` |
| `hold_reason` | text | yes | required when `on_hold` |
| `responsible_user_id` | uuid | yes | FK users (foreman) |
| `subcontractor_party_id` | uuid | yes | by value (R2 portal and claims) |
| `template_item_id` | uuid | yes | `core.trade_default_activities` origin for idempotent re-seed |
| `sort_order` | int | no |  |

Indexes: `(tenant_id, schedule_id, sort_order)`; `(tenant_id, project_id, work_package_id)`; `(tenant_id, project_id, zone_id)`; partial `(tenant_id, project_id, start_date) where status <> 'completed'` (look-ahead); partial `(tenant_id, project_id) where is_critical and status <> 'completed'`. Checks: type lists; `progress_pct_* between 0 and 100`; milestone `duration_days=0`; `(constraint_type='asap') = (constraint_date is null)`; `finish_date >= start_date`; `actual_finish >= actual_start`; `status='completed'` implies `progress_pct_reported=100`.

### 4.4 `planning.activity_dependencies`

| column | type | null | notes |
| --- | --- | --- | --- |
| `schedule_id`, `project_id` | uuid | no |  |
| `predecessor_id`, `successor_id` | uuid | no | FK `activities`; both in the same schedule; not equal |
| `dep_type` | text | no | `FS`, `SS`, `FF`, `SF` |
| `lag_days` | int | no | working days; negative = lead; range -365..365 |
| `is_driving` | bool | no | calculator output: this link determines the successor's early date |

Keys: unique `(tenant_id, predecessor_id, successor_id)`; index `(tenant_id, successor_id)`, `(tenant_id, predecessor_id)`. Cycle check in the aggregate (BR-PLN-06), not in SQL.

### 4.5 `planning.schedule_calc_runs` (append-only)

| column | type | null | notes |
|---|---|---|---|
| `schedule_id`, `project_id` | uuid | no |  |
| `trigger` | text | no | `edit`, `progress`, `import`, `data_date`, `baseline`, `manual`, `nightly` |
| `formula_version` | int | no | BR-X-10 |
| `input_hash` | text | no | sha256 of activities+dependencies+calendar+data\_date; same hash = skip |
| `data_date` | date | no |  |
| `activity_count`, `dependency_count` | int | no |  |
| `project_finish` | date | yes |  |
| `critical_count` | int | no |  |
| `negative_float_count` | int | no |  |
| `status` | text | no | `running`, `succeeded`, `failed` |
| `error_code` | text | yes | e.g. `planning.dependency_cycle` |
| `started_at`, `finished_at` | timestamptz | no |  |
| `duration_ms` | int | yes |  |

Index `(tenant_id, schedule_id, started_at desc)`.

### 4.6 Baselines (versioned per 00 §3.3)

`planning.baselines` (aggregate root)

| column | type | null | notes |
|---|---|---|---|
| `schedule_id`, `project_id` | uuid | no |  |
| `baseline_no` | text | no | series `PLN_BL`, `BL-01`; unique per tenant |
| `version` | int | no | 1 = initial; revision = n+1 |
| `supersedes_id` | uuid | yes | previous baseline |
| `is_current` | bool | no | exactly one approved current per project (partial unique index) |
| `status` | text | no | `draft`, `approved`, `superseded`, `cancelled` |
| `kind` | text | no | `initial`, `revision` |
| `change_reason` | text | no | mandatory for revisions; min 10 chars |
| `reason_category` | text | yes | `client_instruction`, `variation_time_impact`, `design_change`, `site_conditions`, `recovery`, `other` |
| `source_ref_type`, `source_ref_id` | text, uuid | yes | e.g. `variation` when from `contracts.variation.approved` |
| `time_impact_days` | int | yes | proposed finish change from a variation |
| `project_start`, `project_finish` | date | yes | frozen at approval |
| `data_date_at_approval` | date | yes |  |
| `approved_by`, `approved_at` | uuid, timestamptz | yes | PM; not the same as `created_by` unless the tenant allows (BR-X-07 does not apply: no amounts) |
| `formula_version` | int | no |  |

Indexes: unique `(tenant_id, project_id)` where `is_current`; `(tenant_id, project_id, version)`. Check: `status='approved'` implies `approved_by` not null; `kind='revision'` implies `change_reason` not null.

`planning.baseline_activities` (frozen copy, append-only after approval): `baseline_id`, `activity_id` (by value, the live row may be deleted/changed), `activity_code`, `work_package_id`, `zone_id`, `wbs_code`, `name_ar`, `name_en`, `activity_type`, `planned_start date`, `planned_finish date`, `duration_days int`, `planned_qty numeric(18,4)`, `unit_code`, `weight numeric(18,6)`, `total_float_days int`, `is_critical bool`. Unique `(baseline_id, activity_id)`; index `(tenant_id, baseline_id, work_package_id)`.

`planning.baseline_planned_curve` (append-only, written at approval by `PlannedProgressCalculator`): `baseline_id`, `project_id`, `work_package_id uuid null` (null = project level), `zone_id uuid null`, `point_date date`, `planned_pct numeric(7,4)`, `cumulative_weight_done numeric(18,6)`. Unique `(baseline_id, coalesce(work_package_id,zero), coalesce(zone_id,zero), point_date)`. Points: one per week (the first working day of each week) plus the exact start and finish dates of each WP; consumers interpolate linearly (CST BR-CST-31).

### 4.7 Progress configuration and stages

`planning.wp_progress_configs` (one per work package, CRUD created lazily)

| column | type | null | notes |
|---|---|---|---|
| `work_package_id` | uuid | no | FK; unique `(tenant_id, work_package_id)` |
| `project_id` | uuid | no |  |
| `method` | text | no | `quantity`, `stage`, `milestone`; replica of `core.work_packages.progress_method` (event `core.work_package.updated`); Planning rejects entries whose kind differs |
| `weight_basis` | text | no | how activity weights inside this WP are derived: `quantity`, `duration`, `budget`, `manual`, `equal` (default `quantity` for quantity method, `duration` otherwise) |
| `bac_amount` | numeric(18,4) | yes | *hidden-cost*; replica of the current budget (event `cost.budget.approved/revised`, proposed addition 8.3); used only as WP weight in project roll-up |
| `wp_weight` | numeric(18,6) | yes | explicit manual weight (when project `wp_weight_basis='manual'`) |
| `allow_over_100` | bool | no | default false |
| `locked_at` | timestamptz | yes | set at first progress entry (method locked, BR-COR-20) |

`planning.wp_stages` (stage-weighted method; seeded from trade templates)

| column | type | null | notes |
| --- | --- | --- | --- |
| `work_package_id` | uuid | no | FK |
| `stage_no` | int | no | order, unique per WP |
| `name_ar`, `name_en` | text | no | e.g. "Pipes laid", "Pressure test", "Fixtures" |
| `weight_pct` | numeric(7,4) | no | the stages of a WP sum to exactly 100 (BR-PLN-33) |
| `requires_inspection` | bool | no | default false; R2: stage 100% needs `docs.inspection.passed` |

Unique `(work_package_id, stage_no)`. Stages are per WP, not per activity; each activity of a stage WP carries its own state per stage in `activity_stage_states`: `activity_id`, `stage_id`, `pct_done numeric(7,4)` (0..100, reported), `pct_done_verified numeric(7,4)`; unique `(activity_id, stage_id)`.

### 4.8 `planning.progress_entries` (aggregate root; the fact that feeds EV and certificates)

| column | type | null | notes |
|---|---|---|---|
| `entry_no` | text | no | series `PLN_PE`, `PE-00123`; unique per tenant |
| `project_id`, `work_package_id`, `activity_id` | uuid | no | activity belongs to the WP |
| `zone_id` | uuid | yes | copied from the activity, can be refined to a sub-zone |
| `entry_date` | date | no | the day the work was done (not the typing day); not after today; not before `activity.actual_start` unless it sets it |
| `method` | text | no | `quantity`, `stage`, `milestone` (= WP method) |
| `quantity_done` | numeric(18,4) | yes | quantity executed **in this entry** (delta); required for `quantity` |
| `unit_code` | text | yes | must equal the activity unit |
| `cumulative_qty` | numeric(18,4) | yes | previous verified+entered cumulative + `quantity_done` (server computed, BR-PLN-21) |
| `progress_pct` | numeric(7,4) | no | activity percent **after** this entry, server computed for quantity, supplied for milestone (0 or 100); stage derived from lines |
| `previous_pct` | numeric(7,4) | no | activity percent before this entry |
| `is_backward` | bool | no | true when `progress_pct < previous_pct` |
| `backward_reason` | text | yes | mandatory when `is_backward` (BR-PLN-22) |
| `status` | text | no | `entered`, `verified`, `rejected`, `superseded`, `cancelled` |
| `entered_by` | uuid | no | foreman |
| `entered_at` | timestamptz | no | server time |
| `client_created_at` | timestamptz | yes | device time (offline) |
| `verified_by`, `verified_at` | uuid, timestamptz | yes | superintendent |
| `verified_qty` | numeric(18,4) | yes | superintendent may correct the quantity downwards or upwards (BR-PLN-25); default = `quantity_done` |
| `reject_reason` | text | yes | mandatory when rejected |
| `daily_report_id` | uuid | yes | FK `daily_reports` when created from a report line |
| `version` | int | no | event key `(progress_record_id = id, version)`; increments on verification correction |
| `notes` | text | yes |  |
| `idempotency_key` | uuid | yes | unique `(tenant_id, idempotency_key)` (mobile replays) |

Indexes: `(tenant_id, activity_id, entry_date desc)`; `(tenant_id, project_id, status, entry_date)`; partial `(tenant_id, project_id) where status='entered'` (verification queue); keyset `(tenant_id, created_at desc, id desc)`. Checks: `quantity_done >= 0`; `progress_pct between 0 and 100` (or up to the allowed excess when `allow_over_100`, BR-PLN-20); status list.

`planning.progress_entry_stage_lines`: `entry_id`, `stage_id`, `pct_done numeric(7,4)` (0..100 after this entry), `previous_pct numeric(7,4)`; unique `(entry_id, stage_id)`.

`planning.site_photos` (CRUD; file bytes live in `platform.files`, 00 §3.11)

| column | type | null | notes |
| --- | --- | --- | --- |
| `file_id` | uuid | no | `platform.files` by value; unique `(tenant_id, file_id)` |
| `project_id` | uuid | no |  |
| `work_package_id`, `zone_id`, `activity_id` | uuid | yes | at least one of zone/activity/WP |
| `progress_entry_id`, `daily_report_id`, `site_request_id`, `delay_id` | uuid | yes | owner link (at most one) |
| `kind` | text | no | `before`, `after`, `progress`, `issue`, `delivery`, `safety`, `general` |
| `taken_at` | timestamptz | no | EXIF or device time |
| `taken_by` | uuid | no |  |
| `latitude`, `longitude` | numeric(9,6) | yes |  |
| `caption` | text | yes |  |

Index `(tenant_id, project_id, taken_at desc)`, `(tenant_id, activity_id)`, `(tenant_id, zone_id, kind)`.

### 4.9 Daily reports

`planning.daily_reports` (aggregate root; unique `(tenant_id, project_id, report_date)` for non-cancelled, one report per project per day; R2 allows one per zone via `zone_id` null-able, field reserved)

| column | type | null | notes |
| --- | --- | --- | --- |
| `report_no` | text | no | series `PLN_DR`, `DR-0145` |
| `report_date` | date | no |  |
| `status` | text | no | `draft`, `submitted`, `reviewed`, `locked`, `returned` |
| `weather` | text | no | `clear`, `hot`, `windy`, `dusty`, `rain`, `other` |
| `temperature_max_c`, `temperature_min_c` | numeric(5,1) | yes |  |
| `work_stopped` | bool | no | whole-site stoppage that day |
| `summary_note` | text | yes |  |
| `submitted_by`, `submitted_at` | uuid, timestamptz | yes |  |
| `reviewed_by`, `reviewed_at`, `review_note` | uuid, timestamptz, text | yes |  |
| `locked_at` | timestamptz | yes | set by `DailyReportLockJob` |
| `is_late` | bool | no | submitted after `daily_report_cutoff_time` of its date |
| `manpower_total` | int | no | derived sum |

Children (all with `daily_report_id` FK, `project_id`, `sort_order`):

| table | columns (besides standard) |
|---|---|
| `daily_report_manpower` | `trade_id` (core, by value) or `party_id` (subcontractor), `work_package_id?`, `headcount int >= 0`, `hours numeric(6,2)`, `role_label text?`, `is_subcontractor bool` |
| `daily_report_equipment` | `equipment_name text`, `equipment_ref text?`, `hours_used numeric(6,2)`, `hours_idle numeric(6,2)`, `is_rented bool`, `supplier_party_id?`, `breakdown_note text?` |
| `daily_report_quantities` | `activity_id`, `quantity_done numeric(18,4)`, `unit_code`, `progress_entry_id uuid?` (created entry), `notes` |
| `daily_report_deliveries` | `material_id? `, `description text`, `quantity numeric(18,4)`, `unit_code`, `supplier_name text?`, `goods_receipt_id uuid?` (by value, if a receipt exists), `is_goods_receipt_posted bool` (info only; receipts are Procurement's) |
| `daily_report_visitors` | `visitor_name`, `organisation`, `purpose`, `time_in`, `time_out`, `visitor_type` (`client`, `consultant`, `authority`, `supplier`, `other`) |
| `daily_report_notes` | `note_type` (`blocker`, `instruction`, `safety`, `quality`, `general`), `text`, `activity_id?`, `delay_id?` |

`planning.delay_records` (R1 minimal: created from a report blocker; R2 full register)

| column | type | null | notes |
|---|---|---|---|
| `delay_no` | text | no | series `PLN_DL` |
| `project_id`, `activity_id?`, `work_package_id?`, `zone_id?` | uuid |  |  |
| `cause_category` | text | no | `client`, `design`, `material`, `weather`, `subcontractor`, `own_resources`, `authority`, `force_majeure`, `other` |
| `start_date` | date | no |  |
| `end_date` | date | yes | open while null |
| `days_lost` | int | yes | working days, computed when closed |
| `description` | text | no |  |
| `status` | text | no | `open`, `closed`, `disputed` (R2) |
| `daily_report_id` | uuid | yes | origin |
| `evidence_refs` | jsonb | no | \`\[\{ "type": "photo |
| `claimable` | bool | no | default false; R2 hand-off to Contracts claims |

Index `(tenant_id, project_id, status)`; rule BR-PLN-54: a delay without `cause_category` cannot be closed.

### 4.10 Site requests

`planning.site_requests` (aggregate root)

| column | type | null | notes |
|---|---|---|---|
| `request_no` | text | no | series `PLN_SR`, `SR-0231`; unique per tenant |
| `project_id`, `work_package_id` | uuid | no | WP in the project and `active` or `planned` |
| `activity_id` | uuid | yes | the activity the need belongs to (v3: "raised from an activity") |
| `zone_id` | uuid | yes | delivery location |
| `request_type` | text | no | `material`, `equipment`, `labour` (R1 only `material` creates a PR; the other types are confirmed and passed as event with `request_type`, handled by Procurement as manual PR lines or R2 labour planning) |
| `status` | text | no | `draft`, `submitted`, `confirmed`, `rejected`, `returned`, `cancelled` |
| `needed_by_date` | date | no | check `>= created date` (BR-PLN-61) |
| `priority` | text | no | `normal`, `urgent` |
| `notes` | text | yes |  |
| `client_request_id` | uuid | no | device-generated; unique `(tenant_id, client_request_id)` (offline duplicate guard) |
| `submitted_at` | timestamptz | yes |  |
| `confirmed_by`, `confirmed_at` | uuid, timestamptz | yes | superintendent |
| `decision_note` | text | yes | mandatory on reject/return |
| `purchase_request_id` | uuid | yes | by value, set by consuming `procurement.purchase_request.created.v1` |
| `pr_status` | text | yes | replicated PR status for display (`approved`, `partly_ordered`, `closed`...) |

`planning.site_request_lines`: `request_id` FK, `line_no int`, `material_id uuid?` (core by value), `item_description text` (required when no material), `specification text?`, `quantity numeric(18,4)` check `> 0`, `unit_code text`, `needed_by_date date?` (default header), `quantity_ordered numeric(18,4)` and `quantity_received numeric(18,4)` default 0 (replicated, info), `photo_file_ids uuid[]`. Unique `(request_id, line_no)`. No price columns exist in this table (nothing to hide).

Indexes: `(tenant_id, project_id, status)`; partial `(tenant_id, project_id) where status='submitted'` (superintendent queue); `(tenant_id, work_package_id)`; keyset `(tenant_id, created_at desc, id desc)`.

### 4.11 Requirements, constraints, look-ahead

`planning.activity_requirements` (CRUD; what an activity needs before it can start; seeded from trade templates and by the PM)

| column | type | null | notes |
| --- | --- | --- | --- |
| `activity_id`, `project_id` | uuid | no |  |
| `requirement_type` | text | no | `drawing`, `material`, `labour`, `equipment`, `permit`, `inspection`, `access` (predecessor readiness is derived from dependencies, never entered) |
| `description` | text | no |  |
| `lead_days` | int | no | needed this many working days before the activity start (default 5 material, 3 drawing) |
| `is_blocking` | bool | no | default true; a non-blocking requirement lowers the score but does not block |
| `material_id` | uuid | yes | for `material` |
| `required_qty`, `unit_code` | numeric(18,4), text | yes | for `material` |
| `document_ref_id` | uuid | yes | `docs` by value for `drawing`, `permit`, `inspection` |
| `crew_size` | int | yes | for `labour` |
| `resource_ref` | text | yes | equipment name/type |
| `status` | text | no | `active`, `not_applicable` |

`planning.constraints` (aggregate root)

| column | type | null | notes |
|---|---|---|---|
| `constraint_no` | text | no | series `PLN_CN` |
| `project_id`, `activity_id` | uuid | no |  |
| `work_package_id`, `zone_id` | uuid | yes | copied |
| `constraint_type` | text | no | `drawing`, `material`, `labour`, `equipment`, `predecessor`, `permit`, `inspection`, `access`, `other` |
| `requirement_id` | uuid | yes | FK `activity_requirements` when auto-generated |
| `origin` | text | no | `auto` (ConstraintGenerator), `manual` |
| `status` | text | no | `open`, `cleared`, `waived`, `cancelled` |
| `is_blocking` | bool | no |  |
| `needed_by_date` | date | no | activity start minus `lead_days` (working days) |
| `owner_user_id` | uuid | yes | person responsible to clear it |
| `description` | text | no |  |
| `source_ref_type`, `source_ref_id` | text, uuid | yes | `purchase_request`, `purchase_order`, `document`, `rfi`, `inspection`, `activity` |
| `cleared_at`, `cleared_by` | timestamptz, uuid | yes |  |
| `clear_method` | text | yes | `event` (system), `manual`, `waived` |
| `waive_reason` | text | yes | mandatory for `waived` (BR-PLN-44) |

Indexes: unique `(tenant_id, activity_id, requirement_id)` where `status='open' and requirement_id is not null` (idempotent generation); `(tenant_id, project_id, status, needed_by_date)`; `(tenant_id, activity_id)`.

`planning.ref_supply_status` (replica, upserted by consumers): `material_id`, `project_id`, `work_package_id?`, `purchase_request_id?`, `purchase_order_id?`, `po_status`, `promised_date date?`, `ordered_qty`, `received_qty`, `last_receipt_date`, `source_event_id`; unique `(tenant_id, project_id, purchase_order_id, material_id)`. `planning.ref_documents` (replica): `document_id`, `project_id`, `work_package_id?`, `kind` (`drawing`, `permit`, `inspection`, `rfi`), `status` (`approved`, `superseded`, `answered`, `passed`, `failed`, `pending`), `revision`, `source_event_id`. `planning.processed_events(consumer, event_id)` unique.

### 4.12 Weekly plan and PPC

`planning.weekly_plans` (aggregate root): `plan_no` (series `PLN_WW`), `project_id`, `week_start_date date` (first working day of the week, unique per project), `week_end_date`, `status` (`draft`, `committed`, `closed`), `committed_by`, `committed_at`, `closed_at`, `items_committed int`, `items_completed int`, `ppc_pct numeric(7,4)` null until closed, `notes`.

`planning.weekly_plan_items`: `weekly_plan_id`, `activity_id`, `work_package_id`, `zone_id`, `committed_by_user_id` (foreman), `target_pct_end_of_week numeric(7,4)` (planned activity percent at week end), `target_qty numeric(18,4)?`, `outcome` (`pending`, `completed`, `partial`, `not_done`), `achieved_pct numeric(7,4)?`, `reason_code_id uuid?`, `reason_note text?`, `readiness_score_at_commit int`, `added_after_commit bool` (default false: an item added after commit does not count in the denominator, BR-PLN-72). Unique `(weekly_plan_id, activity_id)`.

`planning.variance_reason_codes` (CRUD, tenant-seeded): `code`, `name_ar`, `name_en`, `category` (`design`, `material`, `labour`, `equipment`, `weather`, `predecessor`, `client`, `permit`, `planning_error`, `quality`, `safety`, `other`), `is_active`, `sort_order`. Seed 12 codes (one per category). Unique `(tenant_id, code)`.

### 4.13 Import and ER summary

`planning.schedule_import_runs` (append-only): `project_id`, `platform_import_batch_id` (00 §3.14 / PLT-E9), `file_id`, `format` (`xlsx`, `csv`, `mspdi`, `xer`), `mode` (`replace_draft`, `append`, `update_by_external_ref`), `rows_total`, `rows_ok`, `rows_error`, `activities_created`, `activities_updated`, `dependencies_created`, `calc_run_id`, `status` (`validated`, `committed`, `failed`), `mapping jsonb` (column mapping), `errors jsonb`.

```txt
core.projects 1--1 schedules 1--* activities 1--* activity_dependencies (pred, succ)
schedules 1--* baselines 1--* baseline_activities ; baselines 1--* baseline_planned_curve
schedules 1--* schedule_calc_runs
core.work_packages 1--1 wp_progress_configs 1--* wp_stages ; activities 1--* activity_stage_states
activities 1--* progress_entries 1--* progress_entry_stage_lines ; progress_entries 1--* site_photos
activities 1--* activity_requirements 1--* constraints (open/cleared)
projects 1--* daily_reports 1--* {manpower, equipment, quantities, deliveries, visitors, notes} ; daily_reports 1--* delay_records
projects 1--* site_requests 1--* site_request_lines  --event--> procurement.purchase_requests
projects 1--* weekly_plans 1--* weekly_plan_items --> activities, variance_reason_codes
```

### 4.14 Enumerations (summary)

| enum | values and meaning |
|---|---|
| `dep_type` | FS finish-to-start; SS start-to-start; FF finish-to-finish; SF start-to-finish |
| `activity.status` | `not_started`, `in_progress`, `completed`, `on_hold` |
| `progress_entry.status` | `entered` (reported by foreman), `verified` (counts in EV and certificates), `rejected` (superintendent refused, does not count), `superseded` (a later correction replaced it, history), `cancelled` (author withdrew before verification) |
| `daily_report.status` | `draft`, `submitted`, `returned` (back to foreman), `reviewed`, `locked` |
| `site_request.status` | `draft`, `submitted`, `confirmed`, `rejected`, `returned`, `cancelled` |
| `constraint.status` | `open`, `cleared`, `waived`, `cancelled` |
| `readiness_status` | `ready` (score 100, nothing open), `at_risk` (score \< 100 but no open blocking constraint, or a blocking one with `needed_by_date` > today + 5 days), `blocked` (open blocking constraint due now or overdue), `not_applicable` (completed or in progress) |
| `weekly_plan.status` | `draft`, `committed`, `closed` |
| `baseline.status` | `draft`, `approved`, `superseded`, `cancelled` |

### 4.15 Additional columns referenced by the rules

| table | column | type | notes |
|---|---|---|---|
| `planning_settings` | `progress_backdate_days` | int | default 7 (BR-PLN-29); `wp_weight_basis` text `budget`/`manual`/`equal`, default `budget` (BR-PLN-32); `allow_self_verify` bool default false |
| `progress_entries` | `auto_verified`, `self_verified` | bool | BR-PLN-27; `previous_entry_id uuid null` (correction chain); `over_quantity numeric(18,4) null` (BR-PLN-20); `photo_pending bool` (BR-PLN-30); `period_closed bool` (BR-PLN-29) |
| `constraints` | `blocked_event_sent_at` | timestamptz null | one `blocked` event per open period (BR-PLN-46) |
| `site_requests` | `over_budget_acknowledged` bool, `budget_status_at_confirm` text null |  | BR-PLN-64 |
| `daily_reports` | `non_working_day` bool |  | BR-PLN-80 |

`planning.site_request_line_changes` (append-only): `request_id`, `line_id`, `field` (`quantity`, `needed_by_date`), `old_value`, `new_value`, `changed_by`, `changed_at`. `planning.rm_*` read models are in section 13.

---

## 5. State machines

All transitions are explicit action endpoints (00 §3.6) and write `platform.status_history` (00 §3.4). "Who" uses 00 §4.4 role names; PM = `project_manager`, SUP = `superintendent`, FM = `foreman`.

### 5.1 Baseline

| from | action | to | guard | who | side effects |
|---|---|---|---|---|---|
| (none) | `create` | `draft` | schedule has >= 1 activity, no `negative_float_count` blocking (warning only), calc not dirty (BR-PLN-13) | PM | copies activities and dependencies into `baseline_activities`; draft is editable by re-`refresh` |
| `draft` | `refresh` | `draft` |  | PM | re-copy from the working schedule |
| `draft` | `approve` | `approved` | BR-PLN-13/14; every task has a WP, duration and dates; reason mandatory when version > 1 | PM | previous current becomes `superseded` (`is_current=false`); `baseline_planned_curve` written; `schedules.current_baseline_id` set; event `schedule.baselined.v1` (version 1) or `schedule.revised.v1`; audit |
| `draft` | `cancel` | `cancelled` |  | PM | none |
| `approved` | (none) |  | immutable (BR-PLN-15) |  | a correction is a new revision |

### 5.2 Progress entry

| from | action | to | guard | who | side effects |
| --- | --- | --- | --- | --- | --- |
| (none) | `create` | `entered` | BR-PLN-20..23, 30; schedule active; activity not `completed` unless reopening with reason | FM, SUP, PM | updates activity `progress_pct_reported`, `executed_qty_reported`, sets `actual_start` on first entry > 0, `actual_finish` on 100; event `progress.recorded.v1`; schedule marked dirty (`trigger=progress`); verification task to SUP |
| `entered` | `verify` | `verified` | verifier is not the author unless `verification_required=false` or the SUP is also the author with `allow_self_verify` (flagged) (BR-PLN-27) | SUP, PM | updates `progress_pct_verified`, `executed_qty_verified`; rolls up WP/zone/project (BR-PLN-32); event `progress.verified.v1`; refresh `rm_progress_daily` |
| `entered` | `verify` with `verifiedQty` | `verified` | qty change recorded as `version+1` (BR-PLN-25) | SUP | event carries the corrected quantity |
| `entered` | `reject` | `rejected` | reason mandatory | SUP, PM | reported values recomputed from the last non-rejected entry; notification to author |
| `entered` | `cancel` | `cancelled` | author only, before verification | FM | recomputation as for reject; event `progress.recorded.v1` with `cancelled=true` |
| `verified` | `correct` | `superseded` (old) + new `entered`/`verified` | reason; creates a new entry that replaces it (`supersedes` link via `previous_entry_id`) | SUP, PM | events for both; contracts facts versioned |

### 5.3 Daily report

| from | action | to | guard | who | side effects |
| --- | --- | --- | --- | --- | --- |
| (none) | `create` | `draft` | one report per project per date (BR-PLN-80); date not in the future | FM, SUP | prefilled from yesterday's manpower and equipment |
| `draft`, `returned` | `submit` | `submitted` | weather set; manpower or `work_stopped=true`; every blocker note has a delay or is typed `blocker` (BR-PLN-81) | FM | `is_late` computed; quantity lines not yet linked create progress entries (BR-PLN-83); event `daily_report.submitted.v1` |
| `submitted` | `return` | `returned` | note mandatory | SUP | notification to author |
| `submitted` | `review` | `reviewed` |  | SUP, PM | verifies linked entries if SUP chooses `verifyEntries=true` |
| `reviewed` | `lock` (job) | `locked` | next day at `daily_report_lock_hour` | system | frozen; later edits only through `reopen` |
| `locked` | `reopen` | `reviewed`->edit | reason; PM only | PM | audited |
| `draft` | `delete` | (removed) | only draft reports may be deleted | author |  |

### 5.4 Site request

| from | action | to | guard | who | side effects |
| --- | --- | --- | --- | --- | --- |
| (none) | `create` | `draft` | WP active; lines >= 1 (BR-PLN-60) | FM, SUP | draft saved offline-first |
| `draft`, `returned` | `submit` | `submitted` | BR-PLN-60/61; `Idempotency-Key` + `clientRequestId` | FM | task to SUP; notification `site_request_submitted` |
| `submitted` | `confirm` | `confirmed` | SUP is in the project; line edits allowed at confirm (quantity, needed-by) and recorded (BR-PLN-62) | SUP, PM | event `site_request.confirmed.v1` in the same transaction; Procurement creates the PR |
| `submitted` | `reject` | `rejected` | reason | SUP, PM | notification to FM |
| `submitted` | `return` | `returned` | note | SUP | FM edits and resubmits |
| `draft`, `submitted` | `cancel` | `cancelled` | author or SUP |  | task closed |
| `confirmed` | (none) |  | frozen; later quantity changes are PR changes in Procurement |  | `pr_status`, `quantity_ordered`, `quantity_received` are replicated for display only |

### 5.5 Constraint

| from | action | to | guard | who | side effects |
| --- | --- | --- | --- | --- | --- |
| (none) | `open` (system or manual) | `open` | activity not completed | system, SUP, PM, owner of type (docs/qa/safety for drawing/inspection/permit) | when `is_blocking` and `needed_by_date <= today + not_ready_alert_days`: event `constraint.blocked.v1`; look-ahead refresh |
| `open` | `clear` | `cleared` | evidence note for manual clear; system clears on event (BR-PLN-43) | owner, SUP, PM, system | event `constraint.cleared.v1`; readiness recomputed |
| `open` | `waive` | `waived` | reason; SUP or PM |  | audited; event `constraint.cleared.v1` with `waived=true` |
| `open` | `cancel` | `cancelled` | requirement removed or activity removed | system, PM |  |
| `cleared` | `reopen` | `open` | e.g. goods receipt reversed (BR-PLN-45) | system, SUP | event `constraint.blocked.v1` again |

### 5.6 Weekly plan

| from | action | to | guard | who | side effects |
| --- | --- | --- | --- | --- | --- |
| (none) | `create` | `draft` | one plan per project per week | SUP | items proposed from look-ahead (activities starting or running in the week, ready or at risk) |
| `draft` | `commit` | `committed` | >= 1 item; blocked items flagged and need an acknowledgement (BR-PLN-71) | SUP | stores `readiness_score_at_commit`, `items_committed` |
| `committed` | `add-item` | `committed` | item flagged `added_after_commit` | SUP | not counted in PPC |
| `committed` | `close` | `closed` | on or after `week_end_date`; every item has an outcome and, if not `completed`, a reason code (BR-PLN-73) | SUP, PM | computes PPC; `rm_ppc_weekly` upsert |
| `closed` | (none) |  | frozen |  |  |

### 5.7 Activity status (derived, not an action)

`not_started -> in_progress` on first entry with progress > 0 (sets `actual_start` = `entry_date`); `in_progress -> completed` when reported progress reaches 100 and a `verified` entry at 100 exists (reported 100 shows "awaiting verification"; `actual_finish` is set when the 100 entry is created and kept); `completed -> in_progress` only through a `correct` with reason; `on_hold` is a PM/SUP action `hold` / `resume` with reason and freezes constraint alerts and late alerts.

---

## 6. Business rules

### 6.1 Schedule and logic

| ID | Rule |
|---|---|
| BR-PLN-01 | Each project has one working schedule, created by the first of: trade template seeding, import, or manual create. All activities live in it. |
| BR-PLN-02 | An activity of type `task` or `milestone` belongs to a work package of the same project; an activity of type `summary` has none and its dates/percent are always derived from its children (min start, max finish, weighted percent). Summary activities cannot have progress entries or constraints. |
| BR-PLN-03 | `duration_days` is in working days and `>= 1` for tasks, `0` for milestones. A task finishes on `AddWorkdays(start, duration - 1)` (a 1-day task starts and finishes the same day). A milestone's start = finish. |
| BR-PLN-04 | Dependency semantics (successor S, predecessor P, lag L in working days): **FS**: `S.start >= NextWorkday(P.finish) + L`; **SS**: `S.start >= P.start + L`; **FF**: `S.finish >= P.finish + L`; **SF**: `S.finish >= P.start + L`. For FS the successor starts the working day after the predecessor finishes when L = 0. |
| BR-PLN-05 | Lag may be negative (lead) but the resulting early start cannot precede the schedule `start_date`. A dependency with `abs(lag) > 365` is rejected. Duplicate pair (predecessor, successor) is rejected, even with a different type. A milestone may not be a successor of `SF`. |
| BR-PLN-06 | Adding or changing a dependency that creates a cycle is rejected with `planning.dependency_cycle`; the error lists the activity codes of the cycle in order. Check is a DFS over the project's links inside the same transaction (a project has at most 20,000 activities; target 50 ms for 5,000). |
| BR-PLN-07 | Any change to duration, logic, constraint, pinning, actual dates or `data_date` sets `schedules.is_dirty = true`. `ScheduleCalcRunner` recalculates after a debounce of 3 seconds per project (or immediately when the request has `?calculate=true`) and clears the flag. Reads return the last result with `schedule.isDirty` and `lastCalcAt` so the UI can show "recalculating". |
| BR-PLN-08 | CPM (formula version 1): forward pass gives early start/finish (ES/EF) from logic, constraints and `data_date`; backward pass from the project finish (the max EF, or the latest `fnlt` constraint if earlier) gives late start/finish (LS/LF). **Total float** = `WorkdaysBetween(ES, LS)`. **Free float** = minimum over successors of the gap between this activity's earliest completion and the successor's earliest need under the link type. Actualised activities: a started activity uses `actual_start`; the remaining duration `= ceil(duration x (1 - progress_pct_verified/100))` is scheduled from `data_date` (retained-logic mode). Completed activities have float 0 and are not critical. |
| BR-PLN-09 | `is_critical = total_float_days <= 0` and the activity is not completed. The **critical path** is the set of critical activities connected by driving links (`is_driving`). Negative float means the plan cannot meet a `fnlt` or the contractual completion date; the schedule shows `negative_float_count`. |
| BR-PLN-10 | Constraints: `snet` forces ES >= date; `mso` forces ES = date (and produces negative float if logic pushes later); `fnlt` forces LF \<= date. A pinned activity (`is_pinned`) is treated as `mso` with its pinned start. |
| BR-PLN-11 | Working days come from `ICoreCalendar` (BR-COR-09), read once per calculation run. Planning marks all active schedules dirty when a holiday or weekend setting changes (consumer of `core.holiday` changes is not in the registry; see proposed addition 8.3) and in the nightly `ScheduleNightlyRecalcJob`. |
| BR-PLN-12 | A closed project (`core.project.closed.v1`) freezes the schedule (`status='frozen'`): reads allowed, all writes and recalculation rejected with `planning.schedule_frozen`. |

**Worked example (CPM, BR-PLN-04/08).** Calendar Saturday to Thursday; start day 1. A (5 d) -> B (3 d) FS; A -> C (2 d) FS; B -> D (4 d) FS lag 2; C -> D FS lag 0. A: ES 1, EF 5. B: ES 6, EF 8. C: ES 6, EF 7. D: via B needs `NextWorkday(8)+2` = day 11; via C needs day 8; ES(D) = 11, EF(D) = 14; project finish day 14. Backward: LF(D)=14, LS(D)=11; B: LF = LS(D) - 1 - lag = 8, LS=6, float 0; C: LF = 10, LS 9, total float 3 days, free float 3 (D starts at 11, C finishes 7 -> gap 3); A float 0. Critical path A-B-D; `is_driving` is true for A->B, B->D and false for C->D.

### 6.2 Baseline and planned progress

| ID | Rule |
|---|---|
| BR-PLN-13 | The first baseline (`kind=initial`) can be approved only when: the schedule is not dirty, every task has a WP, duration and dates, there is no dependency cycle, and `core.project.status` is `setup` or `active`. A project may be activated without a baseline (warning check `planning.schedule_baselined`), but EV and PV in Cost show "no plan" until one exists. |
| BR-PLN-14 | A revision requires `change_reason` (>= 10 chars) and `reason_category`; it is a new version row (00 §3.3). Revisions are allowed at any time while the project is active; the system warns when the revision moves `project_finish` later than the contractual completion date from `core`/`contracts` (warning code `planning.finish_beyond_contract`). |
| BR-PLN-15 | Approved baselines are immutable (BR-X-03). Their activities and curve rows are append-only. Old baselines stay queryable for comparison (`GET /baselines/{id}/compare/{otherId}`). |
| BR-PLN-16 | **Planned % of an activity at date d** from baseline activity `b`: if `b.activity_type='milestone'`: 100 when `d >= b.planned_start` else 0; else `clamp(WorkdaysBetween(b.planned_start, d) / b.duration_days x 100, 0, 100)` where `WorkdaysBetween` counts working days from planned start through `d` inclusive (0 before the start). Distribution is linear per working day in R1 (curve shapes, e.g. front/back-loaded concrete, are an R3 option). |
| BR-PLN-17 | **Planned % of a WP at d** = `sum(activity_planned_pct x weight) / sum(weight)` over its baseline activities, using the baseline weights. Zone and project planned % use the same weighted formula with the roll-up weights of BR-PLN-32. |
| BR-PLN-18 | At baseline approval `PlannedProgressCalculator` writes `baseline_planned_curve` for each WP, each zone-with-activities and the project: points every working week and at each WP start and finish, so linear interpolation between points is exact for linear activities within 0.01 pt. The event `schedule.baselined/revised.v1` carries the curve for each WP (CST BR-CST-31 interpolates). |
| BR-PLN-19 | A `contracts.variation.approved.v1` event with `timeImpactDays > 0` creates a **draft revision proposal** (a draft baseline `kind=revision`, `reason_category=variation_time_impact`, `source_ref_id=variation id`, `time_impact_days`) and a task for the PM; it never changes the schedule by itself. Idempotent on the variation id. (R1 task only; the automatic shifting of activities is R2.) |

**Worked example (planned %).** Activity "Tile floor 2" planned 1-20 of a month, 20 working days. At working day 10 planned = 10/20 = 50%. The plumbing golden WP at the status date shows weighted planned progress 50% (cost then computes PV = 1,200,000 x 50% = 600,000; Planning does not touch money).

### 6.3 Progress measurement

| ID | Rule |
|---|---|
| BR-PLN-20 | Activity percent is between 0 and 100. For `quantity` activities, cumulative quantity above `planned_qty` is allowed up to `planned_qty x 1.10` when WP `allow_over_100 = true`, in which case percent is capped at 100 and the excess is stored (`cumulative_qty`) and flagged `over_quantity` (feeds Contracts over-BOQ checks); otherwise an entry that would exceed 100% is rejected with `planning.progress_over_100` and the foreman is asked to raise a quantity change (planned quantity edit by PM/QS). Status and percent never exceed 100. |
| BR-PLN-21 | **Quantity method**: entry carries `quantityDone` (this period). Server computes `cumulative_qty = last_non_rejected_cumulative + quantityDone` and `progress_pct = min(100, cumulative_qty / planned_qty x 100)` to 4 decimals. The client may instead send `cumulativeQty` (device shows "total so far"); the server converts to a delta and rejects when it is lower than the last verified cumulative without `backwardReason`. Unit must equal the activity unit. |
| BR-PLN-22 | Backward progress (new percent \< previous) is accepted only with `backwardReason` (min 5 chars) and `status` is routed to verification with a flag; the superintendent sees it first in the queue. An alert `planning.progress_backward` is raised. A zero-quantity entry is rejected. |
| BR-PLN-23 | **Stage method**: the WP has stages with weights summing to 100 (BR-PLN-33). An entry sends the new `pctDone` (0..100) per touched stage. Activity percent `= sum(stage.weight_pct x stage.pct_done / 100)`. A stage cannot be reduced without a reason (BR-PLN-22). |
| BR-PLN-24 | `actual_start` = the earliest `entry_date` of an entry with percent > 0 (verified or entered); `actual_finish` = `entry_date` of the entry that first reaches 100. If an entry later lowers progress below 100, `actual_finish` is cleared. An entry dated before the activity's predecessor `actual_start` (for FS predecessors) raises warning `planning.out_of_sequence` (not blocking). |
| BR-PLN-25 | Only `verified` entries count in verified progress. On `verify` the SUP may supply `verifiedQty`; the stored `version` increments and the event reports the verified quantity, which is the quantity Contracts uses for measurement (v3: "counted in certificates"). A rejection excludes the entry; the reported percent falls back to the last non-rejected entry. |
| BR-PLN-26 | **Milestone method**: percent is 0 or 100. A 100 entry on a milestone activity is `verified` by the SUP like any other; nothing in between. Milestone WPs may carry `activity_type='milestone'` activities only. |
| BR-PLN-27 | Segregation: the verifier cannot be the author unless the project setting `verification_required=false` (entries are then created with `status='verified'`, `auto_verified=true` visible in the response and in the audit) or the user holds `planning.progress.self_verify` and is the SUP (flag `self_verified=true` in the event). |
| BR-PLN-28 | Both events are published: `progress.recorded.v1` at entry creation (reported, feeds early warning and provisional contracts facts) and `progress.verified.v1` at verification. **Only the verified percent** is used by Cost for EV (CST BR-CST-30) and verified quantity for payment applications. |
| BR-PLN-29 | Late entry: entries may be made for dates up to 7 days back (setting `progress_backdate_days`, default 7); older dates need `planning.progress.backdate` permission and a reason. Future dates are rejected. Entries inside a closed cost period are accepted but the event carries `periodClosed=true`; Cost decides (BR-X-05 applies to cost postings, not to progress facts). |
| BR-PLN-30 | Photo rule: when `photo_required_for_progress` and the percent increase >= `photo_required_min_delta_pct` (or any milestone completion) the entry must have at least one photo linked before it is `entered`; the mobile flow uploads photos first (`POST /photos`) and passes `photoIds`. Error `planning.photo_required`. Offline entries may submit with `photoPending=true`; the entry stays `entered` and is excluded from the verification queue until at least one photo arrives (max 72 h, then an alert). |
| BR-PLN-31 | **Activity weight inside its WP** by `weight_basis`: `quantity`: `planned_qty` (activities with different units are invalid for this basis: error `planning.mixed_units_weight`); `duration`: `duration_days`; `budget`: `budget_amount` (hidden-cost; all activities of the WP must have it); `manual`: user value; `equal`: 1. Weights are stored normalised to `numeric(18,6)` as raw values; the service divides by the WP sum at roll-up. |
| BR-PLN-32 | **Roll-up**: WP percent `= sum(a.pct x a.weight) / sum(a.weight)` over its task activities (milestones included with their weight; summaries excluded). Zone percent and project percent combine WP percents with **WP weights** = WP `bac_amount` share when the budget is approved (`bac_amount` replica), else `wp_weight`, else equal weights, and report the basis used (`weightBasis`) so the number is reproducible (BR-X-10). Computed twice: `reported` (all non-rejected entries) and `verified` (verified entries only). Percent is stored to 4 decimals and shown to 1 decimal. |
| BR-PLN-33 | Stage weights of one WP sum to exactly 100.0000; saving stages that do not is rejected `planning.stage_weights_invalid`. Stages cannot be changed after the first progress entry on the WP unless the PM recalculates (`POST /work-packages/{id}/stages:replace` with a reason, which recomputes activity percents and writes an audit). |
| BR-PLN-34 | The progress method of a WP follows `core.work_packages.progress_method` and is locked at the first entry (`locked_at`). Planning rejects entries of another kind with `planning.progress_method_mismatch`. |
| BR-PLN-35 | Progress feeds nothing but percent and quantity: Planning never stores or returns EV, CPI, SPI amounts. It returns the **schedule performance index inputs** `plannedPct`, `verifiedPct`, and `spiPct = verified / planned` (a percentage-based indicator named `progressRatio`, not the cost SPI; Cost owns SPI in money terms). |

**Worked example (quantity).** Tiles: planned 1,000 m². Entry 1 on day 3: 200 m² -> 20.0000%; entry 2 on day 5: 150 m² -> cumulative 350 -> 35.0000%; SUP verifies entry 2 with `verifiedQty` 140 -> cumulative verified 340 m² -> verified 34.0000%, entry version 2, event quantity 140.

**Worked example (stage and roll-up, golden plumbing).** Stages: pipes laid 40, pressure test 20, fixtures 40. Activity P1 "Building 3 plumbing" has pipes 100%, test 0, fixtures 0 -> `40 x 1 + 0 + 0` = 40%. Roll-up with weight\_basis `budget`: A (budget 480,000) at 100%, B (420,000) at 0%, C (300,000) at 0% -> `(480,000 x 100) / 1,200,000` = 40.0000% verified; planned 50% at the status date from the baseline. Planning publishes planned 50%, verified 40%; Cost derives PV 600,000, EV 480,000 (golden numbers: CPI 0.89, SPI 0.80, EAC 1,350,000 are computed in Cost).

### 6.4 Readiness, constraints and look-ahead

| ID | Rule |
|---|---|
| BR-PLN-40 | The **look-ahead window** is the activities not completed with `start_date` (current plan, or `actual_start` if started) from today through today + `lookahead_weeks` x 7 days, plus activities already in progress that finish in the window. Activities on hold are listed but not scored. |
| BR-PLN-41 | **Readiness checks** per activity: one check per active `activity_requirement` and one **predecessor** check per dependency (satisfied when the predecessor is complete for FS/FF logic, or started for SS/SF logic, using verified status; `entered` 100% counts as "awaiting verification" = at risk). A requirement check is satisfied when: `drawing` -> `ref_documents.status='approved'` for the linked document; `material` -> `ref_supply_status.received_qty >= required_qty` (or a posted receipt covers it, accounting for partial deliveries: satisfied fraction `received/required` is shown, the check is satisfied only at >= 100%); `labour` and `equipment` -> manually confirmed by SUP (`POST /constraints/{id}/clear`) or by a daily-report resource line in R2; `permit` -> approved document; `inspection` -> `docs.inspection.passed` event for the hold point; `access` -> manual. |
| BR-PLN-42 | **Readiness score** `= round(100 x sum(w_t for satisfied checks) / sum(w_t for all applicable checks))` with weights `w_t` from `planning_settings.readiness_weights` (default drawing 25, material 30, labour 15, equipment 10, predecessor 20, permit 10, inspection 10, access 5); if an activity has several checks of the same type the weight is split equally among them. An activity with no applicable checks scores 100. Status: `blocked` if an open blocking constraint has `needed_by_date <= today`; `at_risk` if score \< 100 otherwise (or a blocking constraint is open with a future needed-by date); `ready` if score = 100. Score, status and the list of failed checks are stored with the look-ahead row with `formula_version` (BR-X-10). |
| BR-PLN-43 | `ConstraintGenerator` opens one constraint per unsatisfied check when the activity enters the window (`start_date - lead_days` working days \<= today + window) and clears it automatically on the satisfying event (goods receipt covering the required quantity; `docs.document.approved`; `docs.inspection.passed`; predecessor verified at 100). The rule is idempotent per `(activity_id, requirement_id)` (unique index 4.11). |
| BR-PLN-44 | Waiving a constraint needs SUP or PM and a reason; waived constraints count as satisfied in the score but are listed on the look-ahead with a "waived" badge and in the weekly report. |
| BR-PLN-45 | A reversed goods receipt (`procurement.goods_receipt.reversed.v1`) recomputes `received_qty`; if it falls below `required_qty` the constraint is reopened and `constraint.blocked.v1` fires again. |
| BR-PLN-46 | `constraint.blocked.v1` fires when a blocking constraint is open and `needed_by_date <= today + not_ready_alert_days` (default 5), fired once per constraint per open period (guard: `blocked_event_sent_at`). It carries `materialId`, `requiredQty`, `neededByDate` so Procurement can expedite. The daily `ConstraintDueJob` evaluates it. |
| BR-PLN-47 | A material constraint with no purchase request or order linked (`ref_supply_status` has no row for the material and project) is labelled `not_requested`; the look-ahead offers the action "create site request" pre-filled from the requirement (the foreman submits it; nothing is created automatically). |

**Worked example (readiness).** Activity "Install bathroom fixtures, floor 3", requirements: drawing (approved), material 40 sets (received 25), labour (confirmed), equipment (none), predecessor "tiling floor 3" (complete, verified). Applicable weights: drawing 25, material 30, labour 15, predecessor 20 = 90. Satisfied: drawing, labour, predecessor = 60. Score = round(100 x 60/90) = 67. Material blocking constraint `needed_by` = start - 5 working days. If today is past that date: `blocked`, event fires once. When the second delivery brings received to 40, the receipt event clears it: score 100, `ready`.

### 6.5 Alerts and exceptions (v3 "Rules and alerts")

| ID | Rule |
|---|---|
| BR-PLN-50 | An activity with `actual_start` while an open blocking constraint exists raises warning `planning.started_without_requirement` (in progress response `warnings[]` and exception list); it does not block entry. |
| BR-PLN-51 | Progress above 100% or backward without reason is rejected or flagged per BR-PLN-20/22. |
| BR-PLN-52 | **Late activity**: not completed and `today > finish_date + late_alert_days` working days (default 3), or not started and `today > start_date + late_alert_days`. Produces exception `planning.activity_late` once a day via `LateActivityJob`; a **confirmed delay** is such a date that has already been missed, while **risk** flags (blocked/at risk look-ahead items, open constraints, pending approvals) are labelled `risk` in the exception list, never as predicted delay days (no delay date is predicted in R1-R2). |
| BR-PLN-53 | A daily report not submitted by `daily_report_cutoff_time` on a working day when any activity is `in_progress` produces `planning.daily_report_missing` to the foreman and superintendent (once per day). Non-working days are skipped. |
| BR-PLN-54 | A blocker note on a report creates a `delay_record` (R1 minimal); a delay cannot be closed without `cause_category`, `end_date`, and a description (> 10 chars). Open delays older than 3 days without `cause_category` raise `planning.delay_without_cause`. |
| BR-PLN-55 | Look-ahead item not `ready` 5 working days before start (`not_ready_alert_days`): exception `planning.lookahead_not_ready` to SUP and the owner of each open constraint, once per activity per day (changes only). |

### 6.6 Site requests

| ID | Rule |
|---|---|
| BR-PLN-60 | A site request has >= 1 line; each line has quantity `> 0`, `unit_code`, and either `material_id` or `item_description` (>= 3 chars). Lines may carry up to 5 photos. `request_type=material` lines should reference a material from `core.materials` when it exists (picker with Arabic search); free text is allowed. |
| BR-PLN-61 | `needed_by_date >= today`; a line `needed_by_date` earlier than the header's is rejected. Warning `planning.short_notice` (not blocking) when needed within `lead_time_days` of the material (from `core.materials.typical_lead_days`) or within 2 days; the PR inherits the warning. |
| BR-PLN-62 | Confirming may change line quantity and needed-by; each change is stored in `site_request_line_changes` (line id, field, old, new, by) so the foreman sees what was changed. A confirmation with every line quantity set to 0 is a rejection. |
| BR-PLN-63 | Confirmation publishes `planning.site_request.confirmed.v1` in the same transaction (outbox). Procurement is idempotent on `site_request_id` (PRC BR-PRC-02). A confirmed request is never edited; changes are made in the PR. |
| BR-PLN-64 | **Budget visibility**: the response of `confirm` and `GET` never contains budget numbers. The client shows "remaining budget" for the WP by calling Cost (`GET /cost/work-packages/{id}/budget-status`), which returns `ok / warning / over` plus the amount only to roles with `cost.budget.view_amounts` (for SUP: status only). A confirmation of a request whose WP budget status is `over` is allowed but must carry `overBudgetAcknowledged=true` and is flagged in the event (`budgetStatus`), the PR then routes to approval (PRC BR-PRC-06). |
| BR-PLN-65 | A foreman sees only own requests and those of the WPs in his `limits`; a SUP sees all requests in his projects. Confirming one's own request is blocked (SUP who also created it needs another SUP or PM, BR-X-07 spirit) unless the tenant disables the check (`planning.site_request.self_confirm` permission). |
| BR-PLN-66 | The PR status and received quantities replicate back (`procurement.purchase_request.created/approved`, `goods_receipt.posted`) to `site_requests.pr_status` and `site_request_lines.quantity_received` so the foreman sees "ordered / delivered" on the phone. |

### 6.7 Daily report, weekly plan and PPC

| ID | Rule |
|---|---|
| BR-PLN-70 | **PPC (Plan Percent Complete)** of a week `= completed_items / committed_items x 100` over items committed at `commit` time (not `added_after_commit`). An item is `completed` when `achieved_pct >= target_pct_end_of_week` (verified progress at week end vs the committed target), else `partial` when `> start-of-week pct` and `not_done` when no verified progress. Only verified progress counts. |
| BR-PLN-71 | Committing items whose readiness is `blocked` requires `acknowledgeBlocked=true` and a note per item; the commitment is stored but the item shows the constraint. |
| BR-PLN-72 | Items added after commit are tracked (`added_after_commit`) and excluded from PPC but reported in the "unplanned work" count. |
| BR-PLN-73 | Closing needs an outcome for every item and a `reason_code_id` for each item that is not `completed`. Reasons are summarised in the **top reasons** list by category (count, share). |
| BR-PLN-74 | Project PPC trend uses a rolling average of the last 4 closed weeks; compared with `ppc_target_pct` (default 80). A drop below target for 2 consecutive weeks raises `planning.ppc_below_target`. |
| BR-PLN-75 | Worked example: committed 10 items; at close 7 reached target, 2 partial, 1 not done; 2 items added after commit and finished -> PPC = 7/10 = 70.0000%; reasons: 2 partial = material (1) and labour (1), 1 not done = design (1); unplanned work = 2. |
| BR-PLN-80 | One daily report per project per date (unique); a report is dated the day of the work; submitting for a non-working day is allowed (flag `non_working_day`) when work was done. |
| BR-PLN-81 | A submitted report needs weather, manpower (or `work_stopped=true` with a note), and each blocker has a note; `manpower_total = sum(headcount)` by line. A report with quantity lines but no linked entries creates them (BR-PLN-83). |
| BR-PLN-82 | A report is editable by its author until `submitted`; the SUP can add `review_note`; after `locked` nothing changes without PM `reopen` (audited). Report content is a record of the day and does not itself change progress; only quantity lines linked to entries do. |
| BR-PLN-83 | A quantity line on submit creates a progress entry (`entered`, `daily_report_id` set) through the same validators as BR-PLN-20..30 and Idempotency (key = report id + line id); a validation failure rejects the submit with the line index in `errors[]`. Deliveries listed on the report create no receipts: goods receipt is Procurement's (store/foreman there); the line shows `goods_receipt_id` when one exists for the same material and date. |

### 6.8 Location board, S-curve, visibility, import

| ID | Rule |
|---|---|
| BR-PLN-90 | **Location board** cell = (zone, trade): shows the WP of that zone and trade, with status colour: grey `not_started` (planned start in the future), blue `in_progress` and on track, amber `at_risk` (look-ahead at risk, or verified % behind planned by > 10 points), red `late/blocked` (late alert or blocked constraint), green `completed` (verified 100). Empty cell = no WP. Parent zones aggregate children (the worst colour wins; percent weighted by WP weights). |
| BR-PLN-91 | **S-curve** series per project/zone/WP: planned (from the current baseline curve), forecast (planned to date and linear from the verified percent to the current finish), actual verified (daily snapshot); optional second baseline overlay. Points are weekly; `asOf` is shown. Cost curves (money) come from Cost; Planning serves percentages only. |
| BR-PLN-92 | Hidden fields (BR-X-06): `activity.budgetAmount`, `wpProgressConfig.bacAmount` are removed for roles without `cost.budget.view_amounts` (omitted, exports too). All other fields are visible to site roles. |
| BR-PLN-93 | **Schedule import** (Excel/CSV R1, MSPDI/XER R2): columns mapped (activity code, name, WBS, duration or start/finish, predecessors as \`code\[FS |
| BR-PLN-94 | Seeding from trade templates (`core.trade_default_activities`, `core.trade_template_items`) is idempotent by `template_item_id`; re-applying does not duplicate and does not overwrite user edits. |
| BR-PLN-95 | Public contracts (`IPlannedProgressQuery`, `IProgressQuery`, `IVerifiedQuantityQuery`) return the same numbers as the API and never expose amounts. |

---

## 7. API endpoints

Base `/api/v1/planning`. Conventions per 00 §3.6 (keyset pagination, actions as `POST`, `If-Match` on PATCH and actions, `Idempotency-Key` mandatory on every `POST` create/action from `mobile`; recommended elsewhere). All lists are trimmed by project membership (BR-X-09) and, for foremen, by `limits.zoneIds/workPackageIds`. Errors are listed by code in section 12; "permission" column uses codes of section 10. Hidden cost fields: BR-PLN-92.

### 7.1 Schedule, activities, dependencies (PLN-E1)

| method | path | purpose | key request fields | key response fields | errors | permission |
| --- | --- | --- | --- | --- | --- | --- |
| GET | `/projects/{projectId}/schedule` | working schedule header |  | `id, dataDate, startDate, finishDate, isDirty, lastCalcAt, currentBaseline{id,baselineNo,version}, criticalCount, negativeFloatCount` | `schedule_not_found` | `planning.schedule.view` |
| POST | `/projects/{projectId}/schedule` | create the working schedule (once) | `name, startDate, seedFromTemplates?` | schedule | `schedule_exists` | `planning.schedule.edit` |
| PATCH | `/schedules/{id}` | name, `startDate`; `dataDate` moves through the action | `startDate` | schedule | `schedule_frozen` | `planning.schedule.edit` |
| POST | `/schedules/{id}/data-date` | move the status date | `dataDate` | schedule (dirty) | `data_date_invalid` | `planning.schedule.edit` |
| POST | `/schedules/{id}/calculate` | force a CPM run now | `wait?` | calc run | `dependency_cycle`, `schedule_frozen` | `planning.schedule.calculate` |
| GET | `/schedules/{id}/calc-runs` | recent runs |  | run rows |  | `planning.schedule.view` |
| GET | `/schedules/{id}/activities` | Gantt rows | `include=dependencies,baseline` | activities with dates, float, critical, `progressPctReported/Verified`, `baseline{start,finish}`, `readiness` |  | `planning.activity.view` |
| POST | `/schedules/{id}/activities` | create activity | `workPackageId, zoneId?, parentId?, name, type, durationDays, plannedQty?, unitCode?, constraintType?, constraintDate?, weight?` | activity | `wp_not_in_project`, `unit_required`, `summary_cannot_have_wp` | `planning.activity.edit` |
| PATCH | `/activities/{id}` | edit | same fields, `isPinned`, `responsibleUserId` | activity | `activity_has_progress` (unit/qty change after progress needs `planning.activity.edit_planned_qty`), `schedule_frozen` | `planning.activity.edit` |
| DELETE | `/activities/{id}` | delete activity without progress or baseline reference |  | 204 | `activity_has_progress`, `activity_in_baseline` | `planning.activity.edit` |
| POST | `/activities/{id}/hold`, `/resume` | hold with reason | `reason` | activity |  | `planning.activity.edit` |
| POST | `/activities:bulk` | up to 500 create/update (grid paste) | items | per-item results |  | `planning.activity.edit` |
| GET, POST, PATCH, DELETE | `/activities/{id}/dependencies`, `/dependencies/{id}` | links | `predecessorId, depType, lagDays` | dependency | `dependency_cycle`, `dependency_duplicate`, `dependency_invalid_type` | `planning.activity.edit` |
| GET | `/schedules/{id}/critical-path` | critical chain ordered | `workPackageId?` | `activities[], projectFinish, totalFloatMin` |  | `planning.schedule.view` |
| GET, POST, PATCH | `/activities/{id}/requirements` | requirements of an activity | `requirementType, leadDays, materialId?, requiredQty?, isBlocking` | requirement |  | `planning.activity.edit` |

Filters for `activities`: `filter[workPackageId]`, `filter[zoneId]`, `filter[status]`, `filter[isCritical]`, `filter[tradeId]`, `filter[responsibleUserId]`, `filter[startFrom]`, `filter[startTo]`, `filter[readiness]`; sort: `startDate`, `finishDate`, `wbsCode` (default), `totalFloatDays`, `progressPctVerified`. List response is capped at 5,000 rows without cursor when `view=gantt` (single page, ETag).

### 7.2 Baselines (PLN-E1)

| method | path | purpose | key fields | errors | permission |
| --- | --- | --- | --- | --- | --- |
| GET | `/projects/{projectId}/baselines` | versions | `filter[status]`, `sort=-version` |  | `planning.baseline.view` |
| POST | `/projects/{projectId}/baselines` | create draft from working schedule | `name, changeReason?, reasonCategory?, sourceRefType?, sourceRefId?` | `schedule_dirty`, `baseline_draft_exists` | `planning.baseline.create` |
| POST | `/baselines/{id}/refresh` | re-copy |  | `baseline_not_draft` | `planning.baseline.create` |
| POST | `/baselines/{id}/approve` | freeze, curve, event | `changeReason` (v>1) | `baseline_prereq_failed` (with list), `baseline_reason_required` | `planning.baseline.approve` |
| POST | `/baselines/{id}/cancel` |  |  |  | `planning.baseline.create` |
| GET | `/baselines/{id}` | header + activities (`include=activities`) |  |  | `planning.baseline.view` |
| GET | `/baselines/{id}/compare/{otherId}` | start/finish shifts per activity and project | `filter[workPackageId]` |  | `planning.baseline.view` |
| GET | `/projects/{projectId}/planned-progress` | planned % at a date | `asOf, workPackageId?, zoneId?` |  | `planning.progress.view` |

### 7.3 Progress (PLN-E2)

| method | path | purpose | key request fields | key response fields | errors | permission |
| --- | --- | --- | --- | --- | --- | --- |
| GET | `/wp-progress-configs`, `/work-packages/{id}/progress-config` | method, weights |  | config (cost field gated) |  | `planning.progress.view` |
| PATCH | `/work-packages/{id}/progress-config` | weight basis, `allowOver100`, `wpWeight` |  | config | `progress_method_mismatch` | `planning.progress_config.manage` |
| GET, PUT | `/work-packages/{id}/stages` | list/replace stages (sum 100) | `stages[]` | stages | `stage_weights_invalid` | `planning.progress_config.manage` |
| POST | `/work-packages/{id}/stages:replace` | replace after progress started | `stages[], reason` | recomputed percents |  | `planning.progress_config.manage` |
| POST | `/progress-entries` | create entry (mobile primary) | \`activityId, entryDate, quantityDone | cumulativeQty | stages\[\{stageId,pctDone\}\] | milestoneDone, photoIds\[\], notes, backwardReason?\` |
| GET | `/progress-entries`, `/progress-entries/{id}` | list/detail |  | entry + photos |  | `planning.progress.view` |
| POST | `/progress-entries/{id}/verify` | verify | `verifiedQty?, note?` | entry (version) | `self_verify_not_allowed`, `entry_not_verifiable`, `photo_pending` | `planning.progress.verify` |
| POST | `/progress-entries:verify-bulk` | verify many | `ids[]` (\<= 100) | per-item |  | `planning.progress.verify` |
| POST | `/progress-entries/{id}/reject`, `/cancel`, `/correct` |  | `reason`, corrected values |  |  | `planning.progress.verify` / own for cancel |
| GET | `/projects/{projectId}/progress/summary` | roll-up | `asOf, groupBy` (wp, zone, project, trade), `basis` (verified, reported) | `{ plannedPct, reportedPct, verifiedPct, weightBasis, children[] }` |  | `planning.progress.view` |
| GET | `/projects/{projectId}/progress/verification-queue` | entries awaiting verification | `filter[workPackageId]`, `filter[flag]=backward` |  |  | `planning.progress.verify` |
| GET | `/projects/{projectId}/quantities/verified` | verified quantities by BOQ/WP/period for Contracts UI | `from, to, workPackageId` |  |  | `planning.progress.view` |
| POST | `/photos` | register photo after upload to files | `fileId, kind, activityId?, zoneId?, takenAt, geo?` | photo | `photo_owner_required` | `planning.photo.create` |
| GET | `/photos` | photo log | \`filter\[zoneId | activityId | workPackageId | kind |

Filters for `progress-entries`: `filter[projectId]`, `filter[workPackageId]`, `filter[activityId]`, `filter[status]`, `filter[enteredBy]`, `filter[entryDateFrom]`, `filter[entryDateTo]`, `filter[isBackward]`; sort `entryDate`, `createdAt` (default `-entryDate`).

### 7.4 Daily reports, delays (PLN-E4)

| method | path | purpose | key fields | errors | permission |
| --- | --- | --- | --- | --- | --- |
| POST | `/daily-reports` | create draft (with prefill from yesterday if `prefill=true`) | `projectId, reportDate, weather, manpower[], equipment[], quantities[], deliveries[], visitors[], notes[]` | `daily_report_exists` | `planning.daily_report.create` |
| GET | `/daily-reports`, `/daily-reports/{id}` | list/detail (`include=manpower,equipment,quantities,deliveries,visitors,notes,photos`) |  |  | `planning.daily_report.view` |
| PATCH | `/daily-reports/{id}` | edit while draft/returned (children replaced as arrays) |  | `daily_report_locked` | `planning.daily_report.create` |
| POST | `/daily-reports/{id}/submit`, `/return`, `/review`, `/reopen` | actions | `reviewNote`, `verifyEntries?` | `daily_report_invalid`, `daily_report_wrong_state` | create / `.review` / `.reopen` |
| DELETE | `/daily-reports/{id}` | draft only |  |  | create |
| GET | `/projects/{projectId}/daily-reports/missing` | dates without report | `from, to` |  | `.view` |
| GET, POST, PATCH | `/delays`, `/delays/{id}`, `POST /delays/{id}/close` | delay register (R1 minimal) | `causeCategory, startDate, endDate, activityId` | `delay_cause_required` | `planning.delay.view` / `.manage` |

Filters for `daily-reports`: `filter[projectId]`, `filter[status]`, `filter[reportDateFrom]`, `filter[reportDateTo]`, `filter[isLate]`, `filter[weather]`, `filter[submittedBy]`; sort `reportDate` (default `-reportDate`).

### 7.5 Site requests (PLN-E3)

| method | path | purpose | key fields | errors | permission |
| --- | --- | --- | --- | --- | --- |
| POST | `/site-requests` | create draft or direct submit (`submit=true`) | `projectId, workPackageId, activityId?, zoneId?, requestType, neededByDate, priority, lines[], clientRequestId` | `site_request_no_lines`, `site_request_line_invalid`, `needed_by_in_past`, `wp_not_active` | `planning.site_request.create` |
| GET | `/site-requests`, `/site-requests/{id}` | list, detail with `include=lines,changes,photos` |  |  | `planning.site_request.view` |
| PATCH | `/site-requests/{id}` | edit draft/returned |  | `site_request_wrong_state` | create |
| POST | `/site-requests/{id}/submit`, `/confirm`, `/reject`, `/return`, `/cancel` | actions | confirm: `lines[{lineId,quantity,neededByDate}], overBudgetAcknowledged?`; reject/return: `note` | `site_request_self_confirm`, `site_request_wrong_state`, `over_budget_ack_required` | create / `.confirm` |
| GET | `/site-requests/queue` | superintendent's pending list |  |  | `.confirm` |

Filters: `filter[projectId]`, `filter[workPackageId]`, `filter[status]`, `filter[priority]`, `filter[requestedBy]`, `filter[neededByFrom]`, `filter[neededByTo]`, `filter[requestType]`, `filter[prStatus]`; sort `neededByDate`, `createdAt`, `requestNo`.

### 7.6 Look-ahead, constraints, weekly plan, boards, S-curve (PLN-E4)

| method | path | purpose | key fields | errors | permission |
| --- | --- | --- | --- | --- | --- |
| GET | `/projects/{projectId}/look-ahead` | 3-week list with readiness | `weeks=3, filter[workPackageId], filter[zoneId], filter[readiness], filter[constraintType], filter[responsibleUserId], groupBy` (week, zone, trade) |  | `planning.lookahead.view` |
| GET | `/activities/{id}/readiness` | check-by-check detail |  |  | `planning.lookahead.view` |
| GET, POST | `/constraints`, `/constraints/{id}` | list/create manual constraint | `activityId, constraintType, description, neededByDate, ownerUserId, isBlocking` | `activity_completed` | `planning.constraint.view` / `.manage` |
| POST | `/constraints/{id}/clear`, `/waive`, `/reopen` |  | `note`, `reason` | `constraint_wrong_state`, `waive_reason_required` | `.manage` / `.waive` |
| GET, POST, PATCH | `/weekly-plans`, `/weekly-plans/{id}` | list/create/edit draft | `projectId, weekStartDate, items[]` | `weekly_plan_exists` | `planning.weekly_plan.view` / `.manage` |
| POST | `/weekly-plans/{id}/commit`, `/close`, `/items`, `/items/{itemId}/outcome` | actions | commit: `acknowledgeBlocked`; close: items with outcome+reason | `weekly_plan_incomplete`, `blocked_ack_required` | `.manage` |
| GET | `/projects/{projectId}/ppc` | PPC by week, reasons | `from, to, groupBy` (week, trade, reason) |  | `planning.report.view` |
| GET, POST, PATCH | `/variance-reason-codes` | reasons |  |  | `planning.settings.manage` |
| GET | `/projects/{projectId}/location-board` | zones x trades matrix | `asOf, parentZoneId?` |  | `planning.report.view` |
| GET | `/projects/{projectId}/s-curve` | series | `scope` (project, zone, wp), `scopeId, from, to, baselineId?, compareBaselineId?` |  | `planning.report.view` |
| GET | `/projects/{projectId}/exceptions` | late, not ready, report missing, backward, delay without cause | `filter[type]` |  | `planning.report.view` |
| GET, PATCH | `/planning-settings`, `/projects/{projectId}/planning-settings` | settings |  |  | `planning.settings.manage` |

### 7.7 Import and export (PLN-E1)

| method | path | purpose | fields | errors | permission |
| --- | --- | --- | --- | --- | --- |
| POST | `/projects/{projectId}/schedule/imports` | upload and validate (dry run) | `fileId, format, mode, mapping` | `import_format_unsupported` | `planning.schedule.import` |
| GET | `/schedule-imports/{id}` | result with row errors |  |  | `planning.schedule.import` |
| POST | `/schedule-imports/{id}/commit` | atomic commit |  | `import_has_errors`, `import_replace_not_allowed` | `planning.schedule.import` |
| GET | `/projects/{projectId}/schedule/export?format=xlsx` (also csv, pdf) | export (202 when > 5,000 rows) | `baselineId?` |  | `planning.schedule.view` |
| GET | `/{resource}/export` | export for `progress-entries`, `daily-reports`, `site-requests`, `look-ahead`, `photos` | same filters |  | respective `.view` |

### 7.8 Examples

**Create progress entry (quantity method, mobile)**

```txt
POST /api/v1/planning/progress-entries
Idempotency-Key: 0191f0aa-7c1e-7b22-9c0e-5f4d0b7a1a10
{ "activityId": "0191e1...", "entryDate": "2026-10-08", "quantityDone": 150.0000,
  "unitCode": "m2", "photoIds": ["0191f0..."], "notes": "Floor 2 corridor" }

201 Created   ETag: "7"
{ "id": "0191f0b1...", "entryNo": "PE-00123", "status": "entered", "method": "quantity",
  "activityId": "0191e1...", "quantityDone": 150.0000, "cumulativeQty": 350.0000,
  "previousPct": 20.0000, "progressPct": 35.0000, "isBackward": false,
  "wp": { "id": "0191d9...", "pctReported": 38.2000, "pctVerified": 31.0000 },
  "warnings": [] }
```

**Verify with a corrected quantity**

```txt
POST /api/v1/planning/progress-entries/0191f0b1.../verify   If-Match: "7"
{ "verifiedQty": 140.0000, "note": "Remeasured, corridor edge excluded" }

200 OK { "status": "verified", "version": 2, "verifiedQty": 140.0000,
  "activityPctVerified": 34.0000, "wpPctVerified": 30.7000, "verifiedBy": "0191a2..." }
```

**Create and confirm a site request**

```txt
POST /api/v1/planning/site-requests
Idempotency-Key: 0191f0cc-...   {
 "projectId": "0191a1...", "workPackageId": "0191d9...", "activityId": "0191e1...",
 "zoneId": "0191b2...", "requestType": "material", "neededByDate": "2026-10-14", "priority": "normal",
 "clientRequestId": "0191f0cc-aaaa-7000-8000-000000000001", "submit": true,
 "lines": [ { "materialId": "0191c3...", "quantity": 60.0000, "unitCode": "t" } ] }
201 { "id": "0191f0d0...", "requestNo": "SR-0231", "status": "submitted" }

POST /api/v1/planning/site-requests/0191f0d0.../confirm   If-Match: "2"
{ "lines": [ { "lineId": "0191f0d1...", "quantity": 60.0000, "neededByDate": "2026-10-14" } ] }
200 { "status": "confirmed", "confirmedBy": "0191a3...", "confirmedAt": "2026-10-09T08:12:00Z",
      "warnings": [ { "code": "planning.short_notice", "params": { "days": 5 } } ] }
```

**Look-ahead row**

```txt
GET /api/v1/planning/projects/0191a1.../look-ahead?weeks=3&filter[readiness]=blocked
200 { "items": [ { "activityId": "0191e5...", "activityCode": "A-0231", "name": "Install fixtures, floor 3",
    "workPackageId": "0191d9...", "zoneId": "0191b3...", "startDate": "2026-10-18", "finishDate": "2026-10-22",
    "readinessScore": 67, "readinessStatus": "blocked", "formulaVersion": 1,
    "checks": [ { "type": "drawing", "ok": true }, { "type": "material", "ok": false, "required": 40, "received": 25,
        "unitCode": "set", "constraintId": "0191f2...", "neededByDate": "2026-10-11", "supply": "ordered" },
      { "type": "predecessor", "ok": true } ] } ], "nextCursor": null, "asOf": "2026-10-09T05:00:00Z" }
```

**Close a weekly plan**

```txt
POST /api/v1/planning/weekly-plans/0191f3.../close
{ "items": [ { "itemId": "0191f4...", "outcome": "partial", "achievedPct": 60, "reasonCodeId": "0191r1...", "reasonNote": "Second delivery late" } ] }
200 { "status": "closed", "itemsCommitted": 10, "itemsCompleted": 7, "ppcPct": 70.0000,
      "topReasons": [ { "category": "material", "count": 1 }, { "category": "labour", "count": 1 }, { "category": "design", "count": 1 } ] }
```

---

## 8. Events

Envelope per 00 §3.9. Partition key = aggregate id. `projectId` is set in the envelope for all events below.

### 8.1 Published

| event | fires when | payload fields |
|---|---|---|
| `planning.schedule.baselined.v1` | first baseline approved | `scheduleId, baselineId, baselineNo, version (1), projectStart, projectFinish, formulaVersion, curvesInline (bool), workPackages[{ workPackageId, plannedStart, plannedFinish, weight, points[{ date, plannedPct }] }], projectPoints[{ date, plannedPct }]`. If the serialised payload exceeds 200 KB, `curvesInline=false`, `workPackages[]` carries no points and consumers fetch `IPlannedProgressQuery.GetCurve(baselineId)`. |
| `planning.schedule.revised.v1` | revision approved | all of the above + `previousBaselineId, changeReason, reasonCategory, timeImpactDays?, sourceRefType?, sourceRefId?, finishShiftDays` |
| `planning.progress.recorded.v1` | entry created or cancelled | `progressRecordId, version, entryNo, workPackageId, activityId, zoneId, entryDate, method, quantityDone, cumulativeQty, unitCode, progressPct, previousPct, wpPctReported, isBackward, cancelled (bool), periodClosed (bool), photoCount, enteredBy` |
| `planning.progress.verified.v1` | entry verified (also on corrected re-verification, with higher `version`) | `progressRecordId, version, workPackageId, activityId, zoneId, entryDate, verifiedQty, verifiedCumulativeQty, unitCode, activityPctVerified, wpPctVerified, projectPctVerified, weightBasis, asOfDate, verifiedBy, selfVerified, autoVerified, supersedesProgressRecordId?` |
| `planning.site_request.confirmed.v1` | request confirmed | `siteRequestId, requestNo, requestType, workPackageId, activityId?, zoneId?, neededByDate, priority, requestedBy, confirmedBy, budgetStatus (ok, warning, over or unknown), overBudgetAcknowledged, notes, lines[{ lineId, lineNo, materialId?, itemDescription, specification?, quantity, unitCode, neededByDate, photoFileIds[] }]` |
| `planning.constraint.blocked.v1` | BR-PLN-46 (also reopen) | `constraintId, constraintNo, constraintType, activityId, activityCode, activityName, workPackageId, zoneId?, materialId?, requiredQty?, unitCode?, neededByDate, activityStartDate, isBlocking, ownerUserId?, reopened (bool), supply (not_requested, requested, ordered or partly_received)` |
| `planning.constraint.cleared.v1` | constraint cleared/waived/cancelled | `constraintId, constraintType, activityId, workPackageId, clearedAt, clearMethod, waived (bool), cancelled (bool)` |
| `planning.daily_report.submitted.v1` | report submitted | `dailyReportId, reportNo, reportDate, weather, workStopped, manpowerTotal, quantityLinesCount, blockersCount, photoCount, isLate, deliveries[{ materialId?, description, quantity, unitCode, goodsReceiptId? }], submittedBy` |

### 8.2 Consumed

| event | handler | action | idempotency |
|---|---|---|---|
| `core.project.created.v1` / `.activated.v1` / `.closed.v1` | `ProjectLifecycleHandler` | seed settings and ref rows; on close freeze schedule, stop jobs | upsert by project |
| `core.work_package.created/updated/archived.v1` | `WorkPackageReplicator` | create/update `wp_progress_configs` (method, mirror); seed stages and default activities from template when `seedFromTemplates`; archived WP: activities stay, new entries blocked | upsert by id + `row_version` |
| `procurement.purchase_request.created.v1` / `.approved.v1` | `SupplyStatusHandler` | link `site_requests.purchase_request_id`, `pr_status`; update `ref_supply_status` per line; recompute constraint supply label | unique `(consumer, event_id)` + upsert by PR line |
| `procurement.purchase_order.issued/changed/cancelled/closed.v1` | `SupplyStatusHandler` | upsert `ref_supply_status` (`promised_date`, `ordered_qty`, `po_status`); cancelled -> supply label `not_requested` | upsert by `(PO, material)` |
| `procurement.goods_receipt.posted.v1` / `.reversed.v1` | `ReceiptConstraintHandler` | adds/subtracts `received_qty`; clears or reopens material constraints (BR-PLN-43/45); updates `site_request_lines.quantity_received` | `processed_events`; recompute from totals rather than increments |
| `docs.document.approved.v1` / `.superseded.v1` | `DocumentConstraintHandler` | upsert `ref_documents`; clear drawing/permit constraints when approved; reopen when superseded and requirement is a drawing marked `revision_sensitive` (R2) | upsert by document |
| `docs.rfi.raised/answered/overdue.v1` | `RfiConstraintHandler` | **R2**: open `drawing` or `other` constraint linked to the activity when RFI is linked; clear when answered | upsert by RFI |
| `docs.inspection.passed.v1` / `.failed.v1` | `InspectionHandler` | passed: clears inspection constraint; failed: opens one; **R2** stage `requires_inspection` | upsert |
| `docs.ncr.raised.v1` / `.closed.v1` | `NcrHandler` | **R2**: opens/clears `inspection`-type constraint on the affected activity | upsert |
| `contracts.variation.approved.v1` | `VariationTimeImpactHandler` | BR-PLN-19: draft revision proposal + task to PM when `timeImpactDays > 0` | unique `(source_ref_id)` on `baselines` |
| `cost.budget.approved.v1` / `.revised.v1` | `BudgetWeightHandler` | **proposed addition**: replicate `bac_amount` per WP for roll-up weights (BR-PLN-32); recompute roll-up | upsert by WP + budget version |
| `platform.approval.*` | none | Planning does not use the approval engine (no amounts); listed for completeness |  |

### 8.3 Proposed registry additions

1. Add `planning` as consumer of `cost.budget.approved.v1` / `.revised.v1` (BR-PLN-32 weights); payload needs `workPackages[{workPackageId, totalBudgetAmount, currency}]`.
2. New event `core.calendar.changed.v1` (publisher `core`, consumers `planning`, `cash`): fires when weekend days or a company/project holiday changes; payload `scope (company|project)`, `projectId?`, `effectiveFrom`. Without it Planning relies on the nightly recalculation (BR-PLN-11).
3. Optional (R2): `planning.activity.completed.v1` and `planning.delay.recorded.v1` for Contracts claims and Subcontracts valuation.

---

## 9. Background jobs and schedules

Per 00 §3.10; all idempotent, with tenant context and a `platform.job_runs` record; "per-tenant flag" means the tenant can disable it (setting key `planning.jobs.<name>.enabled`). Times are Africa/Cairo (project time zone from `core.projects`).

| job | trigger/cron | what | idempotency | tenant flag |
|---|---|---|---|---|
| `ScheduleCalcRunner` | event-driven debounce 3 s per project; queue `default` | CPM calculation (BR-PLN-07/08), refresh `rm_*`, writes `schedule_calc_runs` | `input_hash` equal to last run = skip | no |
| `ScheduleNightlyRecalcJob` | `30 1 * * *` | recalc all active schedules whose calendar changed or `data_date` older than today (moves `data_date` to today for projects with `auto_data_date=true`) | hash | yes |
| `ProgressSnapshotJob` | `55 23 * * *` per project time zone | writes `rm_progress_daily` (planned, reported, verified: project, zone, WP) for the date | unique `(project, scope, scope_id, snapshot_date)` upsert | no |
| `ConstraintGeneratorJob` | `*/30 * * * *` + after calc | opens constraints for activities entering the window; refreshes `rm_lookahead_items` | unique index 4.11 | no |
| `ConstraintDueJob` | `0 6 * * *` | evaluates BR-PLN-46, fires `constraint.blocked.v1` once; creates notifications | `blocked_event_sent_at` | yes |
| `LateActivityJob` | `0 7 * * *` (skips non-working days of the project calendar) | BR-PLN-52 exceptions and notifications | per `(activity, date)` | yes |
| `LookAheadNotReadyJob` | `15 7 * * *` | BR-PLN-55 alerts | per `(activity, date)` + changed-only | yes |
| `DailyReportReminderJob` | every 15 min between 15:00 and cutoff+60 min | BR-PLN-53 reminder (before cutoff) and missing alert (after) | per `(project, date, kind)` | yes |
| `DailyReportLockJob` | hourly | locks reviewed reports at `daily_report_lock_hour` of the next day; submitted but unreviewed reports are never locked (task to SUP) | state check | no |
| `WeeklyPlanDraftJob` | first working day of the week 06:30 | creates a draft weekly plan with proposed items | unique `(project, week_start)` | yes |
| `WeeklyPlanCloseReminderJob` | last working day of the week 14:00 | reminds SUP to close | per week | yes |
| `PhotoPendingJob` | hourly | entries with `photo_pending` older than 72 h: alert and exclusion stays | per entry | yes |
| `RmRebuildJob` | `0 3 * * *` | rebuild `rm_*` read models (00 §3.15) | idempotent rebuild by project | no |
| `ConsistencyJob` | `30 3 * * *` | nightly cross-schema checks: orphan `work_package_id`, activity percent equals latest entry, baseline curve end = 100, one current baseline | report only | no |
| `OutboxPublisher` | platform |  |  |  |

---

## 10. Permissions

Roles abbreviations: OE owner\_executive, PM project\_manager, SUP superintendent, FM foreman, PO procurement\_officer, QS cost\_qs\_engineer, CM contracts\_manager, DC document\_controller, QA qa\_qc\_engineer, SO safety\_officer, TA tenant\_admin, SUB subcontractor\_user (R2). All scoped by project membership.

| permission | roles | notes |
| --- | --- | --- |
| `planning.schedule.view` | OE, PM, SUP, FM, PO, QS, CM, DC, QA, SO, TA | FM sees activities of own `limits` only |
| `planning.schedule.edit` | PM, SUP | SUP cannot approve baselines |
| `planning.schedule.calculate` | PM, SUP |  |
| `planning.schedule.import` | PM |  |
| `planning.activity.view` | as schedule.view | `budgetAmount` needs `cost.budget.view_amounts` (PM, QS, OE, finance) |
| `planning.activity.edit` | PM, SUP |  |
| `planning.activity.edit_planned_qty` | PM, QS | after progress started |
| `planning.activity.view_own` | SUB (R2) | only own party activities |
| `planning.baseline.view` | PM, SUP, QS, CM, OE |  |
| `planning.baseline.create` | PM |  |
| `planning.baseline.approve` | PM | not the creator when the tenant enables the 4-eyes setting `planning.baseline.four_eyes` (default off) |
| `planning.progress.view` | OE, PM, SUP, FM, QS, CM, PO |  |
| `planning.progress.create` | FM, SUP, PM | FM limited to own `limits` |
| `planning.progress.verify` | SUP, PM |  |
| `planning.progress.self_verify` | SUP (tenant opt-in) | BR-PLN-27 |
| `planning.progress.backdate` | SUP, PM | beyond 7 days |
| `planning.progress_config.manage` | PM, QS | weights, stages; QS cannot change stages (separate code `planning.progress_config.stages`: PM) |
| `planning.photo.view` / `.create` | all internal / FM, SUP, PM, QA, SO, DC |  |
| `planning.daily_report.view` | OE, PM, SUP, FM, QS, CM, DC, QA, SO |  |
| `planning.daily_report.create` | FM, SUP |  |
| `planning.daily_report.review` | SUP, PM |  |
| `planning.daily_report.reopen` | PM |  |
| `planning.delay.view` / `.manage` | OE, PM, SUP, CM / PM, SUP, FM (create only) |  |
| `planning.site_request.view` | OE, PM, SUP, PO, FM (own and own scope), QS |  |
| `planning.site_request.create` | FM, SUP, PM |  |
| `planning.site_request.confirm` | SUP, PM |  |
| `planning.site_request.self_confirm` | none by default | tenant opt-in |
| `planning.lookahead.view` | OE, PM, SUP, FM, PO, QS, QA, DC, SO |  |
| `planning.constraint.view` | as lookahead.view |  |
| `planning.constraint.manage` | PM, SUP; DC (drawing, permit), QA (inspection), SO (permit) for own types; PO (material) | `constraintType` check in handler |
| `planning.constraint.waive` | PM, SUP |  |
| `planning.weekly_plan.view` | OE, PM, SUP, FM, PO |  |
| `planning.weekly_plan.manage` | SUP, PM |  |
| `planning.report.view` | OE, PM, SUP, FM (own scope), QS, CM, PO | location board, S-curve, PPC, exceptions |
| `planning.settings.manage` | PM (project), TA (tenant defaults, reason codes) |  |

Field visibility: for FM and SUP (and any role without `cost.budget.view_amounts`) the responses of `activities`, `progress-config`, exports and the schedule-compare views omit `budgetAmount`, `bacAmount` and `wpWeightBasis=budget` detail amounts (the weight **basis** label remains). Site request and daily report payloads contain no cost fields. Amount limits: none in this module (no money approvals).

---

## 11. Notifications

Channels per 00 §3.12: `in_app` (always), `push` (mobile), `email`, `whatsapp` (not used by this module in R1 **\[channel to confirm\]**). Recipients are roles within the project unless stated. Arabic first; `{...}` are parameters. Daily digests aggregate the `*_reminder` and alert codes per role.

| Template code | Recipients | Trigger | Channels | Arabic sample |
| --- | --- | --- | --- | --- |
| `PLN_SR_SUBMITTED` | project superintendent(s) | site request submitted | in\_app, push | طلب موقع جديد \{requestNo\} من \{foreman\} لبند \{wp\}: \{lineCount\} بنود مطلوبة بتاريخ \{neededBy\}. |
| `PLN_SR_CONFIRMED` | requester | confirmed | in\_app, push | تم اعتماد طلبك \{requestNo\} وتحويله إلى المشتريات. |
| `PLN_SR_REJECTED` / `PLN_SR_RETURNED` | requester | rejected / returned | in\_app, push | تم رفض الطلب \{requestNo\}: \{reason\}. / أُعيد الطلب \{requestNo\} للتعديل: \{note\}. |
| `PLN_PE_AWAITING` | superintendent | entries waiting > 4 working hours (digest) | in\_app, push | يوجد \{count\} تسجيلات إنجاز بانتظار التحقق في \{project\}. |
| `PLN_PE_REJECTED` | author | rejected | in\_app, push | رُفض تسجيل الإنجاز \{entryNo\} للنشاط \{activity\}: \{reason\}. |
| `PLN_PE_BACKWARD` | superintendent | backward entry | in\_app | تسجيل تراجع في نسبة الإنجاز للنشاط \{activity\} من \{from\}% إلى \{to\}%. السبب: \{reason\}. |
| `PLN_PE_PHOTO_PENDING` | author, superintendent | photo pending > 72 h | in\_app | تسجيل الإنجاز \{entryNo\} ما زال بدون صورة منذ \{hours\} ساعة. |
| `PLN_DR_REMINDER` | foreman | before cutoff, no report | push | تذكير: لم يُرفع تقرير الموقع اليومي لتاريخ \{date\}. آخر موعد \{cutoff\}. |
| `PLN_DR_MISSING` | foreman, superintendent | after cutoff | in\_app, push | لم يُسلَّم تقرير الموقع اليومي ليوم \{date\} في \{project\}. |
| `PLN_DR_RETURNED` | author | returned | in\_app, push | أعاد المشرف تقرير \{date\}: \{note\}. |
| `PLN_CN_BLOCKED` | constraint owner, superintendent, procurement officer (material) | constraint blocked | in\_app, push | معوّق: \{item\} مطلوب لنشاط \{activity\} ويبدأ \{start\}. المتاح \{received\} من \{required\} \{unit\}. |
| `PLN_CN_CLEARED` | superintendent | cleared | in\_app | تم رفع المعوّق عن نشاط \{activity\}: \{constraint\}. |
| `PLN_LA_NOT_READY` | superintendent, owners | not ready 5 days before start | in\_app, push | النشاط \{activity\} يبدأ خلال \{days\} أيام وجاهزيته \{score\}%: ناقص \{missingList\}. |
| `PLN_ACT_LATE` | superintendent, PM | late activity | in\_app, email (digest) | النشاط \{activity\} متأخر \{days\} أيام عن الموعد المخطط. |
| `PLN_BL_PROPOSAL` | project manager | variation time impact | in\_app | الأمر التغييري \{voNumber\} يضيف \{days\} يوم. تمت إضافة مسودة مراجعة للخطة الأساسية. |
| `PLN_BL_APPROVED` | PM, SUP, QS | baseline approved | in\_app | تم اعتماد الخطة الأساسية \{baselineNo\} (النسخة \{version\}). نهاية المشروع \{finish\}. |
| `PLN_WP_CLOSE_REMINDER` | superintendent | last working day | in\_app, push | أغلق خطة الأسبوع \{week\} وسجّل أسباب عدم التنفيذ. |
| `PLN_PPC_LOW` | PM, SUP | below target two weeks | in\_app, email | نسبة الالتزام الأسبوعي \{ppc\}% أقل من الهدف \{target\}% لأسبوعين متتاليين. |
| `PLN_DELAY_NO_CAUSE` | superintendent | open delay > 3 days with no cause | in\_app | تأخير مسجل منذ \{days\} أيام بدون تحديد السبب. |

---

## 12. Error codes

Format per 00 §3.7; code = `planning.<snake_case>`.

| code | HTTP | when | Arabic message | English message |
| --- | --- | --- | --- | --- |
| `planning.schedule_not_found` | 404 | project has no schedule | لا توجد خطة زمنية لهذا المشروع | The project has no schedule |
| `planning.schedule_exists` | 409 | create twice | الخطة الزمنية موجودة مسبقاً | A schedule already exists |
| `planning.schedule_frozen` | 422 | write to a closed project | المشروع مغلق ولا يمكن تعديل الخطة | The project is closed; the schedule is read-only |
| `planning.schedule_dirty` | 422 | baseline from an un-calculated schedule | أعد حساب الخطة قبل إنشاء الخطة الأساسية | Recalculate the schedule first |
| `planning.dependency_cycle` | 422 | circular logic | العلاقة تُكوّن حلقة مغلقة: \{activities\} | The link creates a loop: \{activities\} |
| `planning.dependency_duplicate` | 409 | same pair twice | العلاقة موجودة بين النشاطين | A link between these activities already exists |
| `planning.dependency_invalid_type` | 422 | SF into milestone, lag out of range | نوع العلاقة أو التأخير غير صالح | Invalid link type or lag |
| `planning.wp_not_in_project` | 422 | WP of another project | حزمة العمل لا تنتمي للمشروع | The work package is not in this project |
| `planning.summary_cannot_have_wp` | 422 | BR-PLN-02 | النشاط الملخص لا يرتبط بحزمة عمل | Summary activities cannot have a work package |
| `planning.unit_required` | 422 | quantity method without unit/qty | الوحدة والكمية مطلوبتان | Unit and planned quantity are required |
| `planning.activity_has_progress` | 409 | delete/unit change after progress | يوجد إنجاز مسجل على النشاط | The activity has recorded progress |
| `planning.activity_in_baseline` | 409 | delete activity in a baseline | النشاط مدرج في خطة أساسية | The activity belongs to a baseline |
| `planning.baseline_prereq_failed` | 422 | BR-PLN-13 | شروط اعتماد الخطة الأساسية غير مستوفاة | Baseline prerequisites are not met |
| `planning.baseline_reason_required` | 422 | revision without reason | سبب المراجعة مطلوب | A revision reason is required |
| `planning.baseline_not_draft` / `planning.baseline_draft_exists` | 409 | state | الخطة الأساسية ليست مسودة / توجد مسودة | Baseline is not a draft / a draft exists |
| `planning.progress_over_100` | 422 | BR-PLN-20 | الإنجاز يتجاوز 100% | Progress would exceed 100% |
| `planning.progress_method_mismatch` | 422 | BR-PLN-34 | طريقة القياس لا تطابق حزمة العمل | Entry does not match the work package method |
| `planning.progress_backward_reason_required` | 422 | BR-PLN-22 | سبب التراجع مطلوب | A reason is required for backward progress |
| `planning.photo_required` | 422 | BR-PLN-30 | صورة مطلوبة لهذا التسجيل | A photo is required |
| `planning.entry_date_invalid` | 422 | future or too old date | تاريخ التسجيل غير صالح | Invalid entry date |
| `planning.unit_mismatch` | 422 | unit differs | الوحدة لا تطابق وحدة النشاط | Unit differs from the activity unit |
| `planning.activity_frozen` | 422 | on hold/completed without reopen reason | النشاط متوقف أو مكتمل | The activity is on hold or completed |
| `planning.stage_weights_invalid` | 422 | BR-PLN-33 | مجموع أوزان المراحل يجب أن يساوي 100 | Stage weights must total 100 |
| `planning.mixed_units_weight` | 422 | BR-PLN-31 | وحدات مختلفة لا تصلح كأساس للوزن | Mixed units cannot be used as weights |
| `planning.self_verify_not_allowed` | 403 | BR-PLN-27 | لا يمكنك التحقق من تسجيلك | You cannot verify your own entry |
| `planning.entry_not_verifiable` | 409 | status not `entered` | حالة التسجيل لا تسمح بالتحقق | Entry cannot be verified in its state |
| `planning.photo_pending` | 422 | verify with pending photo | بانتظار الصور | Photos are still pending |
| `planning.daily_report_exists` | 409 | BR-PLN-80 | يوجد تقرير لهذا اليوم | A report exists for this date |
| `planning.daily_report_invalid` | 422 | BR-PLN-81/83 | بيانات التقرير ناقصة | The report is incomplete |
| `planning.daily_report_locked` / `planning.daily_report_wrong_state` | 409 | edit after lock / wrong transition | التقرير مغلق / إجراء غير مسموح | Report is locked / invalid transition |
| `planning.delay_cause_required` | 422 | BR-PLN-54 | سبب التأخير مطلوب | A delay cause is required |
| `planning.site_request_no_lines` | 422 | BR-PLN-60 | الطلب بدون بنود | The request has no lines |
| `planning.site_request_line_invalid` | 422 | quantity/unit/item | بند الطلب غير صالح | Invalid request line |
| `planning.needed_by_in_past` | 422 | BR-PLN-61 | تاريخ الاحتياج في الماضي | Needed-by date is in the past |
| `planning.wp_not_active` | 422 | WP archived or completed | حزمة العمل غير نشطة | The work package is not active |
| `planning.site_request_wrong_state` | 409 | transition | إجراء غير مسموح لحالة الطلب | Invalid transition for the request |
| `planning.site_request_self_confirm` | 403 | BR-PLN-65 | لا يمكنك اعتماد طلبك | You cannot confirm your own request |
| `planning.over_budget_ack_required` | 422 | BR-PLN-64 | يلزم تأكيد تجاوز الموازنة | Over-budget acknowledgement required |
| `planning.constraint_wrong_state` | 409 | transition | إجراء غير مسموح للمعوّق | Invalid transition for the constraint |
| `planning.waive_reason_required` | 422 | BR-PLN-44 | سبب التجاوز مطلوب | A waive reason is required |
| `planning.activity_completed` | 422 | constraint on completed activity | النشاط مكتمل | The activity is completed |
| `planning.weekly_plan_exists` | 409 | one per week | خطة الأسبوع موجودة | A weekly plan exists for this week |
| `planning.weekly_plan_incomplete` | 422 | BR-PLN-73 | أكمل نتائج البنود وأسبابها | Complete all outcomes and reasons |
| `planning.blocked_ack_required` | 422 | BR-PLN-71 | بنود معوّقة تحتاج إقراراً | Acknowledge blocked items |
| `planning.import_format_unsupported` | 422 | R1 formats | صيغة الملف غير مدعومة | File format not supported |
| `planning.import_has_errors` | 422 | commit with errors | يوجد أخطاء في الاستيراد | The import has row errors |
| `planning.import_wp_not_found` | 422 | BR-PLN-93 | حزمة العمل \{code\} غير موجودة | Work package \{code\} not found |
| `planning.import_replace_not_allowed` | 422 | BR-PLN-93 | الاستبدال غير مسموح بعد وجود إنجاز | Replace is not allowed once progress exists |
| `planning.weight_missing` | 422 | BR-PLN-31 basis needs a value on every activity | وزن النشاط غير محدد | An activity has no weight value for this basis |
| `planning.reason_code_in_use` | 409 | delete used reason code | سبب الانحراف مستخدم | The reason code is in use |
| `planning.data_date_invalid` | 422 | data date before start or > today + 7 | تاريخ الحالة غير صالح | Invalid status date |

Warnings (HTTP 200 `warnings[]`): `planning.short_notice`, `planning.out_of_sequence`, `planning.started_without_requirement`, `planning.finish_beyond_contract`, `planning.negative_float`.

---

## 13. Reports and read models

Read models per 00 §3.15: updated by handlers in the transaction of the event, rebuilt nightly by `RmRebuildJob`; every row has `as_of timestamptz`. All have `tenant_id`, `project_id`.

| table | columns (besides keys) | refresh |
|---|---|---|
| `planning.rm_progress_daily` | `scope` (`project`,`zone`,`wp`), `scope_id`, `snapshot_date`, `planned_pct`, `reported_pct`, `verified_pct`, `weight_basis`, `baseline_id`, `activity_count`, `completed_count`; unique `(project_id, scope, scope_id, snapshot_date)` | `ProgressSnapshotJob` nightly; today's row updated at every verification |
| `planning.rm_wp_timeline` (trade view, v3 section 4.3) | `work_package_id`, `trade_id`, `zone_id`, `baseline_start`, `baseline_finish`, `current_start`, `current_finish`, `actual_start`, `actual_finish`, `planned_pct`, `verified_pct`, `reported_pct`, `is_critical`, `float_days_min`, `open_constraints`, `lookahead_items`, `status_color` | after each calc run and progress verification |
| `planning.rm_lookahead_items` | `activity_id`, `work_package_id`, `zone_id`, `start_date`, `finish_date`, `week_offset`, `readiness_score`, `readiness_status`, `failed_checks` jsonb, `open_constraint_count`, `earliest_needed_by`, `responsible_user_id`, `formula_version` | calc run, constraint and supply events, `ConstraintGeneratorJob` |
| `planning.rm_location_board` | `zone_id`, `trade_id`, `work_package_id`, `status_color` (`grey`,`blue`,`amber`,`red`,`green`), `planned_pct`, `verified_pct`, `late_flag`, `blocked_flag`, `open_constraints`; unique `(project_id, zone_id, trade_id)` | as above |
| `planning.rm_ppc_weekly` | `week_start_date`, `scope` (`project`,`trade`), `trade_id?`, `committed`, `completed`, `partial`, `not_done`, `added_after_commit`, `ppc_pct`, `rolling4_ppc_pct` | on weekly plan close |
| `planning.rm_ppc_reasons` | `week_start_date`, `reason_category`, `reason_code_id`, `item_count` | on weekly plan close |
| `planning.rm_site_exceptions` | `exception_type` (`activity_late`,`lookahead_not_ready`,`daily_report_missing`,`progress_backward`,`delay_without_cause`,`photo_pending`,`started_without_requirement`), `ref_id`, `activity_id?`, `severity` (`confirmed`,`risk`), `first_seen_at`, `resolved_at`, `message_params` jsonb | jobs and handlers; feeds Reporting exception list |
| `planning.rm_verified_quantities` | `work_package_id`, `activity_id`, `unit_code`, `period_month`, `verified_qty`, `cumulative_verified_qty`, `last_entry_id` | on `progress.verified`; exposed through `IVerifiedQuantityQuery` |

**S-curve query** (`SCurveQuery`): planned from `baseline_planned_curve` (weekly), actual from `rm_progress_daily` (verified, taken at each week boundary), forecast per BR-PLN-91; response `{ asOf, series: [{ name, points: [{ date, pct }] }] }`.

**Reports** (list/export `xlsx`, `pdf`, `csv`; exports obey BR-X-06; Arabic headers by default):

| report | columns |
|---|---|
| Daily site report (PDF per day) | date, project, weather, temperatures, manpower by trade (headcount, hours), equipment (hours used, idle), quantities (activity, quantity, unit), deliveries, visitors, blockers/notes, photos (thumbnails), submitted/reviewed by |
| Weekly report | week, progress by trade (planned, verified, change since last week), PPC and reasons, look-ahead blocked items, open constraints, delays, manpower trend |
| Progress by trade | trade, WP, zone, planned %, reported %, verified %, variance (points), critical flag, last entry date |
| Planned vs actual | activity code, name, baseline start/finish, current start/finish, actual start/finish, start variance days, finish variance days, float, planned %, verified % |
| 3-week look-ahead | activity, WP, zone, start, finish, readiness score and status, missing items by type, constraint owner, needed-by, supply status |
| Delay log | no., cause, activity, start, end, days lost, status, claimable, evidence count |
| Photo log | date, zone, activity, kind, taken by, caption, link |
| Site request log | no., date, requester, WP, items, needed-by, status, PR status, delivered % |
| Schedule export | WBS, code, name, WP, zone, duration, start, finish, predecessors (`code type lag`), planned qty, unit, weight (budget amounts only with permission) |

---

---

## 15. Test scenarios, non-functional notes, security, open questions

### 15.1 Named test scenarios

| id | scenario | input | expected |
|---|---|---|---|
| T01 | Golden plumbing progress | WP plumbing Building 3, BAC 1,200,000 (Cost); stages 40/20/40; activities A (budget 480,000) pipes 100%, B (420,000) 0%, C (300,000) 0%; status date at 50% of baseline | planned 50.0000, verified 40.0000; events carry both; Cost then derives PV 600,000, EV 480,000, with AC 540,000: CPI 0.89, SPI 0.80, EAC 1,350,000 (asserted in Cost golden test, Planning supplies the percentages) |
| T02 | CPM worked example | network of section 6.1 | finish day 14; C total float 3; critical path A-B-D |
| T03 | Planned % activity | 20 working days, query at day 10; milestone at D | 50%; milestone 0 then 100 |
| T04 | Quantity cumulative | 1,000 m2; entries 200, 150; verify second as 140 | 20% then 35%; verified 34.0000%; event v2 qty 140 |
| T05 | Backward progress | 35% then entry to 30% without reason; then with reason | 422 `progress_backward_reason_required`; then `entered` with `isBackward=true` and an alert |
| T06 | Over 100 | cumulative 950, entry 80, `allowOver100=false` | 422 `progress_over_100`; with `allowOver100=true` pct 100, `overQuantity=30` |
| T07 | Self verification | foreman enters, same user verifies | 403 `self_verify_not_allowed` |
| T08 | Baseline revision | approve v1; variation +14 days creates draft; PM approves v2 with reason | v1 superseded, immutable; `schedule.revised.v1` with `finishShiftDays=14`; Cost curve replaced |
| T09 | Site request cement | foreman requests 60 t, needed in 5 days; superintendent confirms | `site_request.confirmed.v1` with one line 60 t; PR created once in Procurement; replay event creates no second PR (the sourcing numbers: offers A 183,000 effective 179,340, B 181,200, C excluded, are asserted in Sourcing/Procurement tests) |
| T10 | Readiness | requirements per the BR-PLN-42 worked example | score 67, `blocked` after needed-by; receipt 40/40 clears; score 100 |
| T11 | PPC | BR-PLN-75 example | 70.0000; reasons material, labour, design; unplanned 2 |
| T12 | Daily report to progress | report with quantity line 150 m2 submitted | entry `entered` linked; `daily_report.submitted.v1`; verifying entry updates WP % the same day |
| T13 | Lock | reviewed report at 10:00 next day | `locked`; edit rejected |
| T14 | Import | Excel 200 rows with FS+2 text predecessors and one unknown WP | validate shows the row error; commit blocked; fixed file commits and calc finish matches the file's total |
| T15 | Field visibility | foreman and PM get the same activity | foreman response has no `budgetAmount`; PM has it; xlsx export same |
| T16 | Offline replay | 20 queued progress entries replayed twice | 20 rows |
| T17 | Tenant isolation | tenant A token reading tenant B ids | 404 on every endpoint; RLS test with policy attribute removed returns zero rows |
| T18 | Frozen project | project closed event then PATCH activity | 422 `schedule_frozen` |
| T19 | Location board | zone B3-F2 plumbing 40% vs planned 52%, no constraints | amber; parent B3 takes worst colour |
| T20 | Calendar | weekend Fri+Sat, holiday Thursday | 3 working days from Wednesday = Tuesday |

### 15.2 Non-functional notes

| topic | note |
|---|---|
| Volumes (per large tenant, 10 active projects) | 5,000 activities and 8,000 links per project; 200 WPs; progress entries \~ 40/day/project (about 15,000/year/project); daily reports 1/day/project with \~50 child rows; photos \~ 100/day/project (metadata only); constraints \~ 3,000 per project; baselines 3-10 per project, each with 5,000 baseline activities and \~12,000 curve rows |
| Hot queries | activities by schedule (Gantt), look-ahead window (`start_date` index), verification queue (partial index on `entered`), progress entries by activity (`(tenant, activity, entry_date desc)`), site request queue (partial index), exceptions |
| Latency targets (p95) | progress entry create 300 ms; verify 300 ms; site request create/confirm 300 ms; Gantt 5,000 rows 600 ms; look-ahead 400 ms; location board 300 ms; S-curve 300 ms; CPM 5,000 activities 2 s; baseline approval 5 s (job-assisted, returns 200 when finished, otherwise 202) |
| Concurrency | progress entries per activity serialise with a row lock on the activity (`select ... for update`); calc runs use a per-project advisory lock; `If-Match` on edits; verification race: second verifier gets `entry_not_verifiable` |
| Read models | updated in the consumer transaction; nightly rebuild compares with live to detect drift; drift > 0.01 pt raises an ops alert |
| Offline | mobile sends `client_created_at`, `client_request_id`; server time is authoritative for ordering; entry date is a business date entered by the user |
| Retention | progress, reports, photos metadata kept for the project life plus the tenant retention policy; photo files in `platform.files` follow PLT-E6 |

### 15.3 Security notes

- Every endpoint is authorised by permission, project membership and, for foremen, `limits`; handlers re-check (00 §3.8). RLS is the last line of defence (T17).
- Cost fields are omitted for site roles in JSON, in exports and in events (no Planning event carries an amount; the BAC replica is read-only and fed only by `cost.budget.*`).
- Photos: signed URLs with short expiry; geo coordinates visible to PM and SUP only (FM sees own). Photo metadata is stripped of device identifiers.
- Mobile `Idempotency-Key` and request-hash storage prevents replay duplicates; rate limits per 00 §3.6 (an offline sync of 200 entries counts as 200 requests; there is deliberately no `:bulk` for progress entries so that validation stays per entry).
- Audit: baseline approvals, verification and corrections, waived constraints, report reopen, import commit, settings changes, exports.
- Segregation: BR-PLN-27 and BR-PLN-65. No amounts means no approval-engine limits; the two checks are role-based.
- Free text (notes, captions, descriptions) is stored as text and rendered escaped; file uploads go through the platform scanner.

### 15.4 Open questions (for the business)

1. Weekly day off: v3 says Fridays and holidays; Core's default is Friday and Saturday (BR-COR-09). Which should be the tenant default weekend for new companies, and do site crews often work Saturdays (project-level override)?
2. Should an activity at 100% reported but not yet verified count as "completed" for the look-ahead predecessor check, or only after the superintendent verifies (BR-PLN-41 chooses verified)?
3. Does the company want one daily report per project or one per zone/building per day (matters for large multi-building sites; R2 reserves the field)?
4. May a foreman verify another foreman's entry on a small project, or must verification always be by a superintendent (BR-PLN-27)?
5. What is the over-measurement tolerance (BR-PLN-20 uses 10%) before a quantity change request must go through the variation process?
6. Should the weights of work packages in the project roll-up be budget-based (current rule) or contract-value-based (BOQ revenue)? The two differ when margins differ by trade.
7. Which photo rule is acceptable on poor-coverage sites: mandatory before entry (current) or allowed to follow within 72 hours (offline flag)?
8. Do site requests for labour and equipment need their own approval or procurement path in R1, or are they only notes to the superintendent until R2?
9. Who is allowed to waive a constraint (SUP or PM only today); should waiving a material constraint also notify Procurement?
10. Is the weekly plan owned by the superintendent only, or should foremen commit their own items and the superintendent consolidate (affects who is counted in PPC by trade)?
11. Are Egyptian public-holiday dates entered yearly by the admin (Core open question) or may we import an official list?
12. How long must locked daily reports and photos be retained after handover (legal retention)?

### 15.5 Inconsistencies noticed

- v3 and section 1.2 of this file state Friday as the weekly day off; `02-Core` BR-COR-09 defaults to Friday and Saturday. This file follows Core (the calendar is owned there).
- Event registry (00 §7) lists `planning` neither as consumer of `cost.budget.*` nor of any calendar-change event; proposals in 8.3.
