"""Scenario-driven checks (tenant isolation, idempotency, mass assignment, ETag, races, paging cap).
Scenarios are described in a YAML file (AUDIT_SCENARIOS, default scenarios.yaml). Without scenarios these areas are NOT
verified: the tests skip (or FAIL when AUDIT_STRICT=1) and say so."""
import os, re, uuid, yaml, pytest
from concurrent.futures import ThreadPoolExecutor
from conftest import http, skip_or_fail, assert_problem

PATH = os.environ.get("AUDIT_SCENARIOS", os.path.join(os.path.dirname(__file__), "scenarios.yaml"))


def _load():
    if not os.path.exists(PATH):
        return []
    return yaml.safe_load(open(PATH, encoding="utf-8")).get("scenarios", [])


def pytest_generate_tests(metafunc):
    if "scenario" in metafunc.fixturenames:
        wanted = metafunc.definition.get_closest_marker("stype").args[0]
        sc = [s for s in _load() if s["type"] == wanted]
        if not sc:
            metafunc.parametrize("scenario", [None], ids=[f"no-{wanted}-scenario"])
        else:
            metafunc.parametrize("scenario", sc, ids=[s["name"] for s in sc])


def render(o, i=0):
    if isinstance(o, str):
        return o.replace("{i}", str(i)).replace("{uuid}", str(uuid.uuid4()))
    if isinstance(o, dict):
        return {k: render(v, i) for k, v in o.items()}
    if isinstance(o, list):
        return [render(v, i) for v in o]
    return o


def dig(obj, dotted):
    for part in dotted.split("."):
        obj = obj[part]
    return obj


def create(token, c, i=0, headers=None):
    return http(c.get("method", "POST"), c["path"], token, headers=headers, json=render(c["body"], i))


def need(scenario, wanted):
    if scenario is None:
        skip_or_fail(f"no '{wanted}' scenario in {PATH}: this area is NOT verified")


@pytest.mark.stype("cross_tenant")
def test_cross_tenant_isolation(scenario, admin_a, admin_b):
    """S1/S4: tenant B can neither read nor change tenant A's record, and A's record is unchanged afterwards."""
    need(scenario, "cross_tenant")
    r = create(admin_a, scenario["create"]); assert r.status_code in (200, 201), r.text
    rid = str(dig(r.json(), scenario["create"].get("id_from", "id")))
    read = scenario["read"]["path"].replace("{id}", rid)
    before = http("GET", read, admin_a); assert before.status_code == 200
    rb = http("GET", read, admin_b)
    assert rb.status_code == 404, f"tenant B read tenant A's record: {rb.status_code} {rb.text[:200]}"
    if "mutate" in scenario:
        m = scenario["mutate"]
        rm = http(m["method"], m["path"].replace("{id}", rid), admin_b, headers=m.get("headers"), json=render(m.get("body", {})))
        assert rm.status_code in (403, 404), f"tenant B could call {m['method']} on A's record: {rm.status_code}"
    after = http("GET", read, admin_a); assert after.json() == before.json(), "tenant A's record changed after B's attempts"


@pytest.mark.stype("idempotency")
def test_idempotency_key(scenario, admin_a):
    """PLT-205: same key + same body replays; same key + different body is 422; parallel duplicates execute once."""
    need(scenario, "idempotency")
    c = scenario["create"]; idf = c.get("id_from", "id")
    key = str(uuid.uuid4()); body_i = 7
    r1 = create(admin_a, c, body_i, {"Idempotency-Key": key}); assert r1.status_code in (200, 201), r1.text
    r2 = create(admin_a, c, body_i, {"Idempotency-Key": key})
    assert r2.status_code == r1.status_code and dig(r2.json(), idf) == dig(r1.json(), idf), "replay did not return the original response"
    r3 = create(admin_a, c, 8, {"Idempotency-Key": key})
    assert r3.status_code == 422, f"same key with a different body must be 422, got {r3.status_code}"
    assert str(r3.json().get("code", "")).endswith("idempotency_key_reuse"), r3.text
    k2 = str(uuid.uuid4())
    with ThreadPoolExecutor(10) as ex:
        res = list(ex.map(lambda _: create(admin_a, c, 9, {"Idempotency-Key": k2}), range(10)))
    ok = [x for x in res if x.status_code in (200, 201)]
    assert ok, "no parallel request succeeded"
    assert len({dig(x.json(), idf) for x in ok}) == 1, "parallel duplicates created more than one record"
    assert all(x.status_code in (200, 201, 409) for x in res), [x.status_code for x in res]


@pytest.mark.stype("mass_assignment")
def test_mass_assignment(scenario, admin_a):
    """S5: server-owned fields sent by the client are ignored."""
    need(scenario, "mass_assignment")
    c = dict(scenario["create"]); body = render(c["body"]); body.update(scenario["inject"])
    r = http(c.get("method", "POST"), c["path"], admin_a, json=body)
    assert r.status_code < 500, r.text
    if r.status_code in (200, 201):
        out = r.json()
        for f in scenario["forbid_equal"]:
            assert str(dig(out, f)) != str(scenario["inject"][f]), f"client-supplied '{f}' was accepted"


@pytest.mark.stype("etag")
def test_etag_if_match(scenario, admin_a):
    """Optimistic concurrency: stale If-Match is 412; a correct one succeeds; reusing the old one fails."""
    need(scenario, "etag")
    c = scenario["create"]; r = create(admin_a, c); assert r.status_code in (200, 201), r.text
    rid = str(dig(r.json(), c.get("id_from", "id")))
    path = scenario["patch"]["path"].replace("{id}", rid)
    g = http("GET", scenario["read"]["path"].replace("{id}", rid), admin_a)
    etag = g.headers.get("ETag"); assert etag, "GET returned no ETag"
    bad = http("PATCH", path, admin_a, headers={"If-Match": '"bogus"'}, json=render(scenario["patch"]["body"], 1))
    assert bad.status_code == 412, f"stale If-Match must be 412, got {bad.status_code}"
    ok = http("PATCH", path, admin_a, headers={"If-Match": etag}, json=render(scenario["patch"]["body"], 2))
    assert ok.status_code in (200, 204), ok.text
    again = http("PATCH", path, admin_a, headers={"If-Match": etag}, json=render(scenario["patch"]["body"], 3))
    assert again.status_code == 412, f"re-using an old ETag must be 412, got {again.status_code}"


@pytest.mark.stype("concurrency_create")
def test_parallel_creates_get_unique_numbers(scenario, admin_a):
    """PLT-206 style: N parallel creates yield N distinct numbers (and no gaps when gapless=true)."""
    need(scenario, "concurrency_create")
    c = scenario["create"]; n = scenario.get("count", 30); fld = scenario["unique_field"]
    with ThreadPoolExecutor(n) as ex:
        res = list(ex.map(lambda i: create(admin_a, c, i), range(n)))
    ok = [x for x in res if x.status_code in (200, 201)]
    assert len(ok) == n, f"{n - len(ok)} of {n} parallel creates failed: {[x.status_code for x in res if x not in ok][:5]}"
    nums = [str(dig(x.json(), fld)) for x in ok]
    assert len(set(nums)) == n, f"duplicate numbers under concurrency: {sorted(nums)}"
    if scenario.get("gapless"):
        ints = sorted(int(re.search(r"(\d+)$", v).group(1)) for v in nums)
        assert ints[-1] - ints[0] + 1 == n, f"gaps in numbers: {ints}"


@pytest.mark.stype("single_winner")
def test_only_one_parallel_action_wins(scenario, admin_a):
    """Approvals/state changes: of N simultaneous identical actions exactly one succeeds, the rest are 409/422."""
    need(scenario, "single_winner")
    c = scenario["create"]; r = create(admin_a, c); assert r.status_code in (200, 201), r.text
    rid = str(dig(r.json(), c.get("id_from", "id"))); a = scenario["action"]; p = a["path"].replace("{id}", rid)
    n = scenario.get("parallel", 6)
    with ThreadPoolExecutor(n) as ex:
        res = list(ex.map(lambda _: http(a.get("method", "POST"), p, admin_a, json=render(a.get("body", {}))), range(n)))
    wins = [x for x in res if x.status_code in (200, 201, 204)]
    assert len(wins) == scenario.get("expect_success", 1), f"{len(wins)} actions succeeded: {[x.status_code for x in res]}"
    assert all(x.status_code in (200, 201, 204, 409, 422) for x in res), [x.status_code for x in res]


@pytest.mark.stype("pagination_cap")
def test_page_size_is_capped(scenario, admin_a):
    """S12/scalability: a huge limit never returns an unbounded page."""
    need(scenario, "pagination_cap")
    c = scenario["create"]; seed = scenario.get("seed_count", 210); cap = scenario.get("max_page", 200)
    with ThreadPoolExecutor(8) as ex:
        list(ex.map(lambda i: create(admin_a, c, i), range(seed)))
    r = http("GET", scenario["list"]["path"], admin_a, params={"limit": 100000})
    assert r.status_code in (200, 400, 422), r.status_code
    if r.status_code == 200:
        body = r.json(); items = body if isinstance(body, list) else next((v for v in body.values() if isinstance(v, list)), [])
        assert len(items) <= cap, f"page of {len(items)} items returned for limit=100000 (cap {cap})"
