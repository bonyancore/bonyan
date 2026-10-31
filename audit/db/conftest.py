"""Database audit: inspects the PostgreSQL catalog (and, if given an app-role connection, behaviour)."""
import os, re, uuid, yaml, pytest, psycopg

DSN = os.environ.get("AUDIT_DB_DSN")
DSN_APP = os.environ.get("AUDIT_DB_DSN_APP")
APP_ROLE = os.environ.get("AUDIT_DB_APP_ROLE", "app_role")
SCHEMAS = [s.strip() for s in os.environ.get("AUDIT_DB_SCHEMAS", "platform,cost,cash").split(",") if s.strip()]
SETTING = os.environ.get("AUDIT_TENANT_SETTING", "app.tenant_id")
STRICT = os.environ.get("AUDIT_STRICT", "0") == "1"
EXPECT = yaml.safe_load(open(os.environ.get("AUDIT_DB_EXPECT", os.path.join(os.path.dirname(__file__), "expectations.yaml"))))


def skip_or_fail(msg):
    (pytest.fail if STRICT else pytest.skip)(msg)


def _connect(dsn):
    if not dsn:
        pytest.fail("AUDIT_DB_DSN is not set (see audit/README.md).")
    return psycopg.connect(dsn, autocommit=True)


@pytest.fixture(scope="session")
def db():
    c = _connect(DSN); yield c; c.close()


def q(db, sql, *args):
    return db.execute(sql, args).fetchall()


def tenant_tables():
    if not DSN:
        return []
    with psycopg.connect(DSN, autocommit=True) as c:
        return [f"{r[0]}.{r[1]}" for r in c.execute(
            """SELECT n.nspname, c.relname FROM pg_class c JOIN pg_namespace n ON n.oid=c.relnamespace
               JOIN pg_attribute a ON a.attrelid=c.oid AND a.attname='tenant_id' AND NOT a.attisdropped
               WHERE c.relkind IN ('r','p') AND n.nspname = ANY(%s) ORDER BY 1,2""", (SCHEMAS,)).fetchall()]


def table_exists(db, qualified):
    return bool(q(db, "SELECT to_regclass(%s) IS NOT NULL", qualified)[0][0])


def pytest_generate_tests(metafunc):
    if "tenant_table" in metafunc.fixturenames:
        tt = tenant_tables(); metafunc.parametrize("tenant_table", tt or [None], ids=tt or ["no-tenant-tables"])
    if "append_table" in metafunc.fixturenames:
        metafunc.parametrize("append_table", EXPECT.get("append_only", []))
    if "unique_expect" in metafunc.fixturenames:
        u = EXPECT.get("unique_indexes", []); metafunc.parametrize("unique_expect", u, ids=[f"{x['table']}({','.join(x['columns'])})" for x in u])
