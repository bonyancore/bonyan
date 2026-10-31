"""Security and response-convention checks that run against EVERY operation found in the OpenAPI document."""
import re, pytest
from conftest import (http, body_kw, assert_problem, LEAK_RE, BODY_METHODS)


def test_requires_authentication(op):
    """S3: no token => 401, for every endpoint (never 200/403/404/500)."""
    r = http(op.method, op.url, **body_kw(op.method))
    assert r.status_code == 401, f"{op.id}: expected 401 without a token, got {r.status_code}"


def test_forbidden_without_permission(op, noperm_a):
    """S3: authenticated user without the permission => 403 (permission is checked before the record is looked up)."""
    r = http(op.method, op.url, token=noperm_a, **body_kw(op.method))
    assert r.status_code == 403, f"{op.id}: expected 403 for a user without permissions, got {r.status_code}"
    assert_problem(r)


def test_unknown_id_is_404(id_get_op, admin_a):
    """S4: a record that does not exist (or is not visible) is 404, never 200/403/500."""
    r = http("GET", id_get_op.url, token=admin_a)
    assert r.status_code == 404, f"{id_get_op.id}: expected 404 for a random id, got {r.status_code}"
    assert_problem(r)


@pytest.mark.parametrize("name,kw", [
    ("malformed-json", dict(data=b'{"a":', headers={"Content-Type": "application/json"})),
    ("null-body", dict(data=b"null", headers={"Content-Type": "application/json"})),
    ("array-body", dict(data=b"[1,2,3]", headers={"Content-Type": "application/json"})),
    ("deep-nesting", dict(data=b"[" * 4000 + b"]" * 4000, headers={"Content-Type": "application/json"})),
    ("wrong-content-type", dict(data=b"x=1", headers={"Content-Type": "text/plain"})),
    ("sql-meta-strings", dict(json={"name": "'; DROP TABLE x;--", "note": "\" OR 1=1 --"})),
    ("rtl-and-nul", dict(json={"name": "\u202eمرحبا\u0000"})),
    ("huge-string", dict(json={"name": "A" * 300_000})),
])
def test_hostile_body_is_rejected_cleanly(op, admin_a, name, kw):
    """S6/S8: hostile bodies never cause a 5xx, and errors are problem+json without internals."""
    if op.method not in BODY_METHODS:
        pytest.skip("no body")
    r = http(op.method, op.url, token=admin_a, **kw)
    assert r.status_code < 500, f"{op.id} [{name}] -> {r.status_code}: {r.text[:200]}"
    if r.status_code >= 400:
        assert_problem(r)


@pytest.mark.parametrize("name,params", [
    ("negative-limit", {"limit": "-5"}), ("text-limit", {"limit": "abc"}),
    ("sql-cursor", {"cursor": "' OR 1=1--"}), ("huge-limit", {"limit": "100000"}),
    ("unicode-filter", {"q": "\u202e\u0000"}),
])
def test_hostile_query_never_500(get_op, admin_a, name, params):
    r = http("GET", get_op.url, token=admin_a, params=params)
    assert r.status_code < 500, f"{get_op.id} [{name}] -> {r.status_code}: {r.text[:200]}"
    if r.status_code >= 400:
        assert_problem(r)


@pytest.mark.parametrize("value", ["not-a-uuid", "' OR 1=1--", "..%2F..%2Fetc%2Fpasswd", "0" * 400])
def test_hostile_path_param_never_500_or_200(param_op, admin_a, value):
    r = http(param_op.method, param_op.with_param(value), token=admin_a, **body_kw(param_op.method))
    assert r.status_code < 500, f"{param_op.id} [{value}] -> {r.status_code}: {r.text[:200]}"
    assert r.status_code != 200, f"{param_op.id} accepted a garbage id with 200"
    if r.status_code in (404, 405):      # may come from routing (framework) rather than the domain: only require no leak
        assert not LEAK_RE.search(r.text), f"{param_op.id}: leaks internals: {r.text[:300]}"
    elif r.status_code >= 400:
        assert_problem(r)


def test_error_bodies_do_not_leak_internals(op, admin_a):
    """S8: probe with an empty body and a random id; any error body must be clean."""
    r = http(op.method, op.url, token=admin_a, **body_kw(op.method))
    if r.status_code >= 400:
        assert not LEAK_RE.search(r.text), f"{op.id}: leaks internals: {r.text[:300]}"


def test_security_headers(get_op, admin_a):
    r = http("GET", get_op.url, token=admin_a)
    assert r.headers.get("X-Content-Type-Options", "").lower() == "nosniff", f"{get_op.id}: missing X-Content-Type-Options: nosniff"
    assert "X-Powered-By" not in r.headers, f"{get_op.id}: X-Powered-By leaks the stack"


def test_cors_is_not_open(get_op, admin_a):
    r = http("GET", get_op.url, token=admin_a, headers={"Origin": "https://evil.example"})
    acao = r.headers.get("Access-Control-Allow-Origin")
    assert acao not in ("*", "https://evil.example"), f"{get_op.id}: CORS allows an arbitrary origin ({acao})"


def test_trace_method_is_not_allowed(list_op, admin_a):
    r = http("TRACE", list_op.url, token=admin_a)
    assert r.status_code in (400, 404, 405, 501), f"TRACE returned {r.status_code}"


def test_list_endpoints_validate_paging(list_op, admin_a):
    """Paging inputs are validated (negative or non-numeric limit is a 4xx, not clamped silently into a 200 with garbage)."""
    r = http("GET", list_op.url, token=admin_a, params={"limit": "-5"})
    assert 400 <= r.status_code < 500, f"{list_op.id}: limit=-5 returned {r.status_code}"
