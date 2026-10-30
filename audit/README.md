# Independent audit pack (security + responses + database)

**Purpose:** verify the agent's work from the outside, with checks the agent does not control. It does not read the source code:
it needs a **running API** (with its OpenAPI document) and a **PostgreSQL dev database**.

**Owned by the human.** The agent must not edit anything in `audit/` except `audit/api/scenarios.yaml`, `audit/db/expectations.yaml`
and `audit/config.env` (flagged for review). It must never weaken a check to make it pass.

## 1. Setup (once)
```
pip install -r audit/requirements.txt
cp audit/config.example.env audit/config.env      # fill tokens and DSNs; use a DEV database and throw-away tokens
cp audit/api/scenarios.example.yaml audit/api/scenarios.yaml   # adapt paths/bodies to the real API
```
You need three tokens: tenant A with all permissions, tenant A with no permissions, tenant B with all permissions.
The project must expose a way to mint them for tests (a test auth mode); the agent is asked to provide it in PLT-211.
Start the API and the database, then:
```
bash audit/run_audit.sh            # api + db       (or: api | db)
AUDIT_STRICT=1 bash audit/run_audit.sh   # unverified areas FAIL instead of being skipped (use for the final run)
```

## 2. What is checked

### API (every operation found in the OpenAPI document under the prefix, automatically)
| Check | Maps to |
|---|---|
| no token => 401 | S3 |
| token without permission => 403 (problem+json) | S3 |
| random/unknown id => 404 problem+json | S4 |
| malformed JSON, null/array body, deep nesting, wrong content type, SQL meta strings, RTL/NUL, 300 KB string => never 5xx, clean problem+json | S6, S7, S8 |
| hostile query (`limit=-5`, `limit=abc`, SQL in cursor), hostile path ids => never 5xx/200 | S6, S7 |
| error bodies contain no stack traces, SQL, connection strings, file paths | S8 |
| `X-Content-Type-Options: nosniff`, no `X-Powered-By`, CORS not open, TRACE not allowed | S8, S15 |
| paging input validated (`limit=-5` is 4xx) | S6 |

### API scenarios (from `scenarios.yaml`; without a scenario the area is NOT verified)
| Scenario type | Proves |
|---|---|
| `cross_tenant` | tenant B cannot read or change tenant A's record; A's record unchanged (S1, S4) |
| `idempotency` | same key+body replays; same key+different body = 422 `idempotency_key_reuse`; 10 parallel duplicates create one record (PLT-205, S13) |
| `mass_assignment` | client-supplied `status`/`id`/`tenantId` are ignored (S5) |
| `etag` | stale `If-Match` = 412; correct one works; old ETag fails after update |
| `concurrency_create` | N parallel creates get N distinct numbers (and no gaps if `gapless`) (PLT-206) |
| `single_winner` | of N simultaneous identical actions exactly one succeeds (approvals, state machines) |
| `pagination_cap` | `limit=100000` never returns more than the page cap |

### Database (`expectations.yaml` lists the expected tables and indexes)
| Check | Maps to |
|---|---|
| every table with `tenant_id`: RLS enabled **and forced**, a policy using `current_setting('app.tenant_id')`, `WITH CHECK` on write policies, `tenant_id NOT NULL` | S1 |
| app role: not superuser, no BYPASSRLS, owns nothing, cannot CREATE in the module schemas | S1, S14 |
| behaviour as the app role: a random tenant or no tenant sees 0 rows (needs data in the table) | S1, S2 |
| append-only tables: app role lacks UPDATE/DELETE/TRUNCATE and a BEFORE trigger rejects them | CST-202 |
| unique indexes that enforce idempotency, numbering, one-current-version, one-reversal | PLT-205/206, CST-201/202/205 |
| no cross-module foreign keys (except `core.projects`, `core.work_packages`, `core.parties`, `platform.users`) | architecture rule |
| no float/double/money columns for amounts; no `timestamp` without time zone | conventions |

## 3. What this audit does NOT verify (still the agent's tests and your review)
Business correctness and golden numbers (EV, EAC, true-up, PA-04), event schemas and consumer behaviour, outbox ordering and
exactly-once effects, file upload security (magic bytes, scan gating), import limits (zip bombs, formulas), CI gates,
architecture rules, and code quality. It is a safety net, not a substitute for review.

## 4. Reading results
- **FAILED** = a real defect or a wrong audit configuration. Fix the code. Never edit the audit to pass.
- **SKIPPED** = not verified (no scenario, table not built yet, empty table, token missing). Treat as a gap; `AUDIT_STRICT=1` turns skips into failures.
- A framework-generated 404 for an unmatched route may not be problem+json; the audit only requires "no leak" there.

## 5. Proof that the audit itself works
`python audit/selftest/run_selftest.py api` starts a healthy mock API and 13 defective variants (missing auth, missing permission check,
leaked stack traces, cross-tenant read, mass assignment, broken idempotency, no page cap, missing nosniff, number race, double approval,
ETag ignored, wrong error shape, open CORS): the audit must pass the healthy one and catch every defect.
`python audit/selftest/run_selftest.py db` (needs a PostgreSQL superuser DSN in `SELFTEST_PG_DSN`) does the same for 13 database defects
(RLS not forced, policy without WITH CHECK, USING(true), nullable tenant_id, BYPASSRLS role, owner role, CREATE privilege, float money,
naive timestamps, ledger without trigger, ledger with UPDATE grant, missing unique index, cross-module FK).
