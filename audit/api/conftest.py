"""Black-box API audit. Independent of the codebase: it only needs a running API and its OpenAPI document.
Configuration is through environment variables (see audit/README.md)."""
import json, os, re, uuid
from dataclasses import dataclass
import pytest, requests

BASE = os.environ.get("AUDIT_BASE_URL", "http://127.0.0.1:8080").rstrip("/")
OPENAPI = os.environ.get("AUDIT_OPENAPI", "/openapi/v1.json")        # URL path or a local file path
PREFIX = os.environ.get("AUDIT_PATH_PREFIX", "/api/v1/")
PUBLIC_RE = re.compile(os.environ.get("AUDIT_PUBLIC_REGEX", r"^/(health|healthz|ready|live)"))
SKIP_RE = re.compile(os.environ.get("AUDIT_SKIP_REGEX", r"^\b$"))     # paths to exclude entirely
STRICT = os.environ.get("AUDIT_STRICT", "0") == "1"                  # strict: unverified areas FAIL instead of SKIP
TIMEOUT = float(os.environ.get("AUDIT_TIMEOUT", "30"))
CODE_RE = re.compile(r"^[a-z][a-z0-9]*(\.[a-z0-9_]+)+$")             # module.snake_case error code
LEAK_RE = re.compile(
    r"(\bat [\w.<>$]+\(.*\) in .*:line \d+|System\.[A-Z]\w+Exception|Npgsql|PostgresException|SqlException|"
    r"Traceback \(most recent call last\)|\bSELECT\b.+\bFROM\b|\bINSERT INTO\b|Host=.+;|Password=|"
    r"[A-Za-z]:\\\\[\w\\\\]+\.cs|/(home|app|src|usr)/[\w./-]+\.(cs|py)\b|StackTrace)", re.I | re.S)
BODY_METHODS = {"POST", "PUT", "PATCH"}


def skip_or_fail(msg):
    (pytest.fail if STRICT else pytest.skip)(msg)


@dataclass(frozen=True)
class Op:
    method: str
    path: str

    @property
    def has_params(self):
        return "{" in self.path

    @property
    def url(self):
        return re.sub(r"\{[^}]+\}", lambda _: str(uuid.uuid4()), self.path)

    def with_param(self, value):
        return re.sub(r"\{[^}]+\}", value, self.path)

    @property
    def id(self):
        return f"{self.method} {self.path}"


_OPS = None


def _spec():
    if os.path.exists(OPENAPI):
        return json.load(open(OPENAPI, encoding="utf-8"))
    r = requests.get(BASE + OPENAPI, timeout=TIMEOUT)
    r.raise_for_status()
    return r.json()


def operations():
    global _OPS
    if _OPS is None:
        _OPS = []
        for path, item in _spec().get("paths", {}).items():
            if not path.startswith(PREFIX) or SKIP_RE.search(path):
                continue
            for m in ("get", "post", "put", "patch", "delete"):
                if m in item:
                    _OPS.append(Op(m.upper(), path))
        if not _OPS:
            raise pytest.UsageError(f"No operations under {PREFIX} found in the OpenAPI document ({OPENAPI}).")
    return _OPS


def pytest_generate_tests(metafunc):
    fx = metafunc.fixturenames
    if "op" in fx:
        ops = [o for o in operations() if not PUBLIC_RE.search(o.path)]
        metafunc.parametrize("op", ops, ids=[o.id for o in ops])
    if "get_op" in fx:
        ops = [o for o in operations() if o.method == "GET"]
        metafunc.parametrize("get_op", ops, ids=[o.id for o in ops])
    if "list_op" in fx:
        ops = [o for o in operations() if o.method == "GET" and not o.has_params]
        metafunc.parametrize("list_op", ops, ids=[o.id for o in ops])
    if "id_get_op" in fx:
        ops = [o for o in operations() if o.method == "GET" and o.has_params]
        metafunc.parametrize("id_get_op", ops, ids=[o.id for o in ops])
    if "param_op" in fx:
        ops = [o for o in operations() if o.has_params and not PUBLIC_RE.search(o.path)]
        metafunc.parametrize("param_op", ops, ids=[o.id for o in ops])


def http(method, path, token=None, headers=None, **kw):
    h = {"Accept": "application/json"}
    if token:
        h["Authorization"] = f"Bearer {token}"
    h.update(headers or {})
    return requests.request(method, BASE + path, headers=h, timeout=TIMEOUT, allow_redirects=False, **kw)


def body_kw(method):
    return {"json": {}} if method in BODY_METHODS else {}


def assert_problem(r, allow_empty_401=False):
    if allow_empty_401 and r.status_code == 401 and not r.content:
        return
    ct = r.headers.get("Content-Type", "")
    assert "application/problem+json" in ct, f"{r.request.method} {r.request.url} -> {r.status_code} Content-Type '{ct}' (expected application/problem+json)"
    body = r.json()
    assert isinstance(body.get("status"), int), f"problem body has no integer 'status': {body}"
    assert CODE_RE.match(str(body.get("code", ""))), f"problem body has no stable 'code' like module.snake_case: {body}"
    assert not LEAK_RE.search(r.text), f"error body leaks internals: {r.text[:300]}"


def _token(name):
    t = os.environ.get(f"AUDIT_TOKEN_{name.upper()}")
    if not t:
        pytest.fail(f"Missing env AUDIT_TOKEN_{name.upper()} (see audit/README.md). The audit cannot run without it.")
    return t


@pytest.fixture(scope="session")
def admin_a():
    return _token("admin_a")


@pytest.fixture(scope="session")
def noperm_a():
    return _token("noperm_a")


@pytest.fixture(scope="session")
def admin_b():
    t = os.environ.get("AUDIT_TOKEN_ADMIN_B")
    if not t:
        skip_or_fail("AUDIT_TOKEN_ADMIN_B not set: tenant isolation NOT verified")
    return t
