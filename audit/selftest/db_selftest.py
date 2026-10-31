"""Builds a healthy PostgreSQL schema and variants with ONE injected defect each; the DB audit must pass the first and catch every defect.
Needs a PostgreSQL server: set SELFTEST_PG_DSN (a superuser DSN to the 'postgres' database), default host=/tmp port=54329 user=postgres."""
import os, subprocess, sys, xml.etree.ElementTree as ET, psycopg

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
BASE = os.environ.get("SELFTEST_PG_DSN", "host=/tmp port=54329 user=postgres")
DEFECTS = {  # flag -> test that must fail
    "norls": "test_tenant_tables_have_forced_rls", "nopolicycheck": "test_tenant_tables_have_tenant_policy",
    "policytrue": "test_rls_behaviour_as_app_role", "nulltenant": "test_tenant_id_is_not_null",
    "bypass": "test_app_role_is_unprivileged", "owner": "test_app_role_is_unprivileged", "createpriv": "test_app_role_is_unprivileged",
    "float": "test_no_float_money_columns", "naivets": "test_timestamps_have_timezone",
    "notrigger": "test_append_only_tables", "grantupdate": "test_append_only_tables",
    "nouniq": "test_expected_unique_indexes", "crossfk": "test_no_cross_schema_foreign_keys"}


def policy(table, flags):
    using = "true" if "policytrue" in flags and table == "cash.receivables" else "tenant_id = current_setting('app.tenant_id', true)::uuid"
    chk = "" if ("nopolicycheck" in flags and table == "cash.receivables") else f" WITH CHECK ({using})"
    return f"CREATE POLICY tenant_isolation ON {table} USING ({using}){chk};"


def build(flags):
    f = set(flags)
    with psycopg.connect(BASE + " dbname=postgres", autocommit=True) as c:
        c.execute("DROP DATABASE IF EXISTS audit_db WITH (FORCE)")
        c.execute("CREATE DATABASE audit_db")
        try: c.execute("CREATE ROLE app_role LOGIN")
        except psycopg.errors.DuplicateObject: pass
        c.execute("ALTER ROLE app_role NOSUPERUSER " + ("BYPASSRLS" if "bypass" in f else "NOBYPASSRLS"))
    tenant_tables = ["platform.users", "platform.audit_log", "platform.idempotency_keys", "platform.number_series", "core.projects",
                     "cost.cost_entries", "cost.cost_periods", "cost.budgets", "cash.receivables"]
    ddl = """
    CREATE SCHEMA platform; CREATE SCHEMA cost; CREATE SCHEMA cash; CREATE SCHEMA core;
    CREATE TABLE core.projects (id uuid PRIMARY KEY, tenant_id uuid NOT NULL);
    CREATE TABLE platform.users (id uuid PRIMARY KEY, tenant_id uuid NOT NULL);
    CREATE TABLE platform.audit_log (id uuid PRIMARY KEY, tenant_id uuid NOT NULL, created_at timestamptz NOT NULL DEFAULT now());
    CREATE TABLE platform.processed_events (consumer text, event_id uuid, PRIMARY KEY (consumer, event_id));
    CREATE TABLE platform.idempotency_keys (tenant_id uuid NOT NULL, user_id uuid NOT NULL, key uuid NOT NULL, PRIMARY KEY (tenant_id, user_id, key));
    CREATE TABLE platform.number_series (tenant_id uuid NOT NULL, series_code text, scope_key text, next_value bigint, PRIMARY KEY (tenant_id, series_code, scope_key));
    CREATE TABLE cost.cost_entries (id uuid PRIMARY KEY, tenant_id uuid NOT NULL, project_id uuid, posting_key text NOT NULL, reverses_entry_id uuid,
        amount numeric(18,4) NOT NULL, created_at timestamptz NOT NULL DEFAULT now());
    CREATE TABLE cost.cost_periods (id uuid PRIMARY KEY, tenant_id uuid NOT NULL, project_id uuid, year int, month int);
    CREATE TABLE cost.budgets (id uuid PRIMARY KEY, tenant_id uuid NOT NULL, project_id uuid, is_current boolean, total_amount numeric(18,4), created_at timestamptz DEFAULT now());
    CREATE TABLE cash.receivables (id uuid PRIMARY KEY, tenant_id uuid NOT NULL, budget_id uuid, amount numeric(18,4), created_at timestamptz DEFAULT now());
    """
    if "nulltenant" in f: ddl += "ALTER TABLE cash.receivables ALTER COLUMN tenant_id DROP NOT NULL;"
    if "float" in f: ddl += "ALTER TABLE cost.budgets ADD COLUMN total_amount_float double precision;"
    if "naivets" in f: ddl += "ALTER TABLE cash.receivables ADD COLUMN due_at timestamp;"
    if "crossfk" in f: ddl += "ALTER TABLE cash.receivables ADD CONSTRAINT fk_x FOREIGN KEY (budget_id) REFERENCES cost.budgets(id);"
    if "nouniq" not in f: ddl += "CREATE UNIQUE INDEX ux_posting ON cost.cost_entries (tenant_id, posting_key);"
    ddl += """CREATE UNIQUE INDEX ux_reversal ON cost.cost_entries (reverses_entry_id) WHERE reverses_entry_id IS NOT NULL;
    CREATE UNIQUE INDEX ux_period ON cost.cost_periods (tenant_id, project_id, year, month);
    CREATE UNIQUE INDEX ux_current ON cost.budgets (tenant_id, project_id) WHERE is_current;"""
    if "nouniq" in f: ddl += "CREATE UNIQUE INDEX ux_reversal2 ON cost.cost_entries (reverses_entry_id) WHERE reverses_entry_id IS NOT NULL;"
    ddl += """CREATE FUNCTION platform.forbid_mutation() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RAISE EXCEPTION 'append-only'; END $$;"""
    for t in ("cost.cost_entries", "platform.audit_log"):
        if "notrigger" in f and t == "cost.cost_entries": continue
        ddl += (f"CREATE TRIGGER trg_row BEFORE UPDATE OR DELETE ON {t} FOR EACH ROW EXECUTE FUNCTION platform.forbid_mutation();"
                f"CREATE TRIGGER trg_trunc BEFORE TRUNCATE ON {t} FOR EACH STATEMENT EXECUTE FUNCTION platform.forbid_mutation();")
    for t in tenant_tables:
        ddl += f"ALTER TABLE {t} ENABLE ROW LEVEL SECURITY;"
        if not ("norls" in f and t == "cost.budgets"): ddl += f"ALTER TABLE {t} FORCE ROW LEVEL SECURITY;"
        ddl += policy(t, f)
    ddl += "GRANT USAGE ON SCHEMA platform, cost, cash, core TO app_role; GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA platform, cost, cash, core TO app_role;"
    ddl += "REVOKE UPDATE, DELETE, TRUNCATE ON cost.cost_entries, platform.audit_log FROM app_role;"
    if "grantupdate" in f: ddl += "GRANT UPDATE ON cost.cost_entries TO app_role;"
    if "createpriv" in f: ddl += "GRANT CREATE ON SCHEMA cost TO app_role;"
    if "owner" in f: ddl += "ALTER TABLE cash.receivables OWNER TO app_role;"
    seed = "".join(f"INSERT INTO {t.split('|')[0]} (id, tenant_id) VALUES (gen_random_uuid(), '00000000-0000-0000-0000-0000000000aa');"
                   for t in ["core.projects", "platform.users", "platform.audit_log", "cost.cost_periods", "cost.budgets", "cash.receivables"])
    seed += ("INSERT INTO cost.cost_entries (id, tenant_id, posting_key, amount) VALUES (gen_random_uuid(),'00000000-0000-0000-0000-0000000000aa','k1',1);"
             "INSERT INTO platform.idempotency_keys VALUES ('00000000-0000-0000-0000-0000000000aa', gen_random_uuid(), gen_random_uuid());"
             "INSERT INTO platform.number_series VALUES ('00000000-0000-0000-0000-0000000000aa','PO','',1);")
    with psycopg.connect(BASE + " dbname=audit_db", autocommit=True) as c:
        c.execute(ddl); c.execute(seed)


def run(flags, tag):
    env = dict(os.environ, AUDIT_DB_DSN=BASE + " dbname=audit_db", AUDIT_DB_DSN_APP=BASE.replace("user=postgres", "user=app_role") + " dbname=audit_db",
               AUDIT_DB_APP_ROLE="app_role", AUDIT_STRICT="1")
    j = f"/tmp/junit_db_{tag}.xml"
    subprocess.run([sys.executable, "-m", "pytest", "db", "-q", "-p", "no:cacheprovider", f"--junitxml={j}"], cwd=ROOT, env=env,
                   stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL, timeout=300)
    r = ET.parse(j).getroot()
    failed = [f"{c.get('classname')}::{c.get('name')}" for c in r.iter("testcase") if c.find("failure") is not None or c.find("error") is not None]
    return sum(1 for _ in r.iter("testcase")), failed


def db_selftest():
    ok = True
    build([]); total, failed = run([], "good")
    good = not failed
    print(f"[db] healthy schema: {total} tests, {len(failed)} failed -> {'PASS (expected)' if good else 'FAIL (UNEXPECTED)'}")
    for x in failed[:10]: print("      ", x)
    ok &= good
    for flag, expect in DEFECTS.items():
        build([flag]); total, failed = run([flag], flag)
        caught = any(expect in x for x in failed)
        print(f"[db] defect '{flag}': {len(failed)} failing tests, caught by '{expect}': {'YES' if caught else 'NO  <-- audit missed it'}")
        ok &= caught
    return ok


if __name__ == "__main__":
    sys.path.insert(0, ROOT); r = db_selftest(); print("DB SELFTEST", "PASSED" if r else "FAILED"); sys.exit(0 if r else 1)
