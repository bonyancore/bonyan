import re, uuid, pytest, psycopg
from conftest import (q, table_exists, skip_or_fail, EXPECT, SCHEMAS, SETTING, APP_ROLE, DSN_APP, DSN)


def _need_tables(t):
    if t is None:
        skip_or_fail(f"no tables with a tenant_id column found in schemas {SCHEMAS}: nothing to check yet")


def test_tenant_tables_have_forced_rls(db, tenant_table):
    """S1: ENABLE and FORCE row level security on every tenant table (the table owner must not bypass RLS)."""
    _need_tables(tenant_table)
    en, force = q(db, "SELECT relrowsecurity, relforcerowsecurity FROM pg_class WHERE oid=%s::regclass", tenant_table)[0]
    assert en, f"{tenant_table}: row level security is NOT enabled"
    assert force, f"{tenant_table}: row level security is enabled but NOT forced (owner bypasses it)"


def test_tenant_tables_have_tenant_policy(db, tenant_table):
    _need_tables(tenant_table)
    sch, name = tenant_table.split(".")
    pols = q(db, "SELECT cmd, qual, with_check FROM pg_policies WHERE schemaname=%s AND tablename=%s", sch, name)
    assert pols, f"{tenant_table}: no RLS policy"
    assert any(SETTING in (p[1] or "") or SETTING in (p[2] or "") for p in pols), f"{tenant_table}: no policy uses current_setting('{SETTING}')"
    for cmd, qual, chk in pols:
        if cmd in ("ALL", "INSERT", "UPDATE"):
            assert chk, f"{tenant_table}: {cmd} policy has no WITH CHECK (a tenant could write rows for another tenant)"


def test_tenant_id_is_not_null(db, tenant_table):
    _need_tables(tenant_table)
    nn = q(db, "SELECT attnotnull FROM pg_attribute WHERE attrelid=%s::regclass AND attname='tenant_id'", tenant_table)[0][0]
    assert nn, f"{tenant_table}.tenant_id must be NOT NULL"


def test_app_role_is_unprivileged(db):
    """S1/S14: the runtime role is not superuser, has no BYPASSRLS, owns nothing, and cannot run DDL."""
    r = q(db, "SELECT rolsuper, rolbypassrls FROM pg_roles WHERE rolname=%s", APP_ROLE)
    assert r, f"role '{APP_ROLE}' does not exist (set AUDIT_DB_APP_ROLE)"
    assert not r[0][0], f"{APP_ROLE} is a superuser"
    assert not r[0][1], f"{APP_ROLE} has BYPASSRLS"
    owned = q(db, """SELECT n.nspname||'.'||c.relname FROM pg_class c JOIN pg_namespace n ON n.oid=c.relnamespace
                     WHERE pg_get_userbyid(c.relowner)=%s AND n.nspname = ANY(%s) AND c.relkind IN ('r','p','v','m','S')""", APP_ROLE, SCHEMAS)
    assert not owned, f"{APP_ROLE} owns objects (owners bypass RLS unless forced; migrations must use a separate owner): {[o[0] for o in owned][:5]}"
    for s in SCHEMAS:
        if q(db, "SELECT to_regnamespace(%s) IS NOT NULL", s)[0][0]:
            assert not q(db, "SELECT has_schema_privilege(%s,%s,'CREATE')", APP_ROLE, s)[0][0], f"{APP_ROLE} can CREATE in schema {s}"


def test_no_float_money_columns(db):
    """Money and quantities are numeric, never float/double/money."""
    rx = EXPECT.get("money_name_regex", "(amount|total|price|cost)")
    rows = q(db, """SELECT table_schema||'.'||table_name||'.'||column_name, data_type FROM information_schema.columns
                    WHERE table_schema = ANY(%s) AND data_type IN ('real','double precision','money')""", SCHEMAS)
    bad = [r for r in rows if re.search(rx, r[0].split(".")[-1], re.I)]
    assert not bad, f"floating-point money/quantity columns: {bad[:8]}"


def test_timestamps_have_timezone(db):
    rows = q(db, """SELECT table_schema||'.'||table_name||'.'||column_name FROM information_schema.columns
                    WHERE table_schema = ANY(%s) AND data_type='timestamp without time zone'""", SCHEMAS)
    assert not rows, f"timestamp WITHOUT time zone columns (use timestamptz, UTC): {[r[0] for r in rows][:8]}"


def test_append_only_tables(db, append_table):
    """Ledger-style tables: no UPDATE/DELETE/TRUNCATE for the app role AND a BEFORE trigger that rejects them."""
    if not table_exists(db, append_table):
        return skip_or_fail(f"{append_table}: not built yet")
    for priv in ("UPDATE", "DELETE", "TRUNCATE"):
        assert not q(db, "SELECT has_table_privilege(%s,%s,%s)", APP_ROLE, append_table, priv)[0][0], f"{APP_ROLE} has {priv} on {append_table}"
    bits = 0
    for (tgtype,) in q(db, "SELECT tgtype FROM pg_trigger WHERE tgrelid=%s::regclass AND NOT tgisinternal", append_table):
        if tgtype & 2:   # BEFORE
            bits |= tgtype
    assert bits & 16, f"{append_table}: no BEFORE UPDATE trigger"
    assert bits & 8, f"{append_table}: no BEFORE DELETE trigger"
    assert bits & 32, f"{append_table}: no BEFORE TRUNCATE trigger"


def test_expected_unique_indexes(db, unique_expect):
    """Idempotency, numbering and one-current-version guards must be enforced by the database."""
    t, cols = unique_expect["table"], unique_expect["columns"]
    if not table_exists(db, t):
        return skip_or_fail(f"{t}: not built yet")
    idx = q(db, """SELECT array_agg(a.attname ORDER BY k.ord) FROM pg_index i
                   CROSS JOIN LATERAL unnest(i.indkey) WITH ORDINALITY k(attnum, ord)
                   JOIN pg_attribute a ON a.attrelid=i.indrelid AND a.attnum=k.attnum
                   WHERE i.indrelid=%s::regclass AND i.indisunique GROUP BY i.indexrelid""", t)
    have = [list(r[0]) for r in idx]
    assert cols in have, f"{t}: no UNIQUE index/constraint on ({', '.join(cols)}); found {have}"


def test_no_cross_schema_foreign_keys(db):
    """Modules never reference each other's tables with a real FK (exceptions are listed in expectations.yaml)."""
    allowed = set(EXPECT.get("allowed_cross_schema_fk_targets", []))
    rows = q(db, """SELECT con.conname, ns1.nspname||'.'||c1.relname, ns2.nspname||'.'||c2.relname
                    FROM pg_constraint con JOIN pg_class c1 ON c1.oid=con.conrelid JOIN pg_namespace ns1 ON ns1.oid=c1.relnamespace
                    JOIN pg_class c2 ON c2.oid=con.confrelid JOIN pg_namespace ns2 ON ns2.oid=c2.relnamespace
                    WHERE con.contype='f' AND ns1.nspname = ANY(%s) AND ns2.nspname <> ns1.nspname""", SCHEMAS)
    bad = [r for r in rows if r[2] not in allowed]
    assert not bad, f"cross-module foreign keys: {bad[:6]}"


def test_rls_behaviour_as_app_role(db, tenant_table):
    """Behaviour: as the app role, a random tenant or no tenant sees NO rows (proof needs data in the table)."""
    _need_tables(tenant_table)
    if not DSN_APP:
        return skip_or_fail("AUDIT_DB_DSN_APP not set: RLS behaviour NOT verified")
    total = q(db, f"SELECT count(*) FROM {tenant_table}")[0][0]
    if total == 0:
        return skip_or_fail(f"{tenant_table}: empty, RLS behaviour unproven (seed data first)")
    with psycopg.connect(DSN_APP, autocommit=False) as c:
        c.execute("SELECT set_config(%s,%s,true)", (SETTING, str(uuid.uuid4())))
        assert c.execute(f"SELECT count(*) FROM {tenant_table}").fetchone()[0] == 0, f"{tenant_table}: random tenant can see rows"
        c.rollback()
        try:
            n = c.execute(f"SELECT count(*) FROM {tenant_table}").fetchone()[0]
        except psycopg.Error:
            c.rollback(); n = 0
        assert n == 0, f"{tenant_table}: rows visible when NO tenant is set (must fail closed)"
