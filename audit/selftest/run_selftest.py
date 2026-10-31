"""Proves the audit works: it must PASS on a healthy mock API and must FAIL (on the expected test) for every injected defect.
Usage: python selftest/run_selftest.py [api|db|all]"""
import os, subprocess, sys, time, xml.etree.ElementTree as ET, urllib.request

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
sys.path.insert(0, ROOT)
PORT = 18080
ENV_API = {"AUDIT_BASE_URL": f"http://127.0.0.1:{PORT}", "AUDIT_OPENAPI": "/openapi.json", "AUDIT_TOKEN_ADMIN_A": "admin-a",
           "AUDIT_TOKEN_NOPERM_A": "noperm-a", "AUDIT_TOKEN_ADMIN_B": "admin-b",
           "AUDIT_SCENARIOS": os.path.join(ROOT, "api", "scenarios.example.yaml"), "AUDIT_STRICT": "1"}
# defect flag -> substring that MUST appear among failing test ids
API_DEFECTS = {"noauth": "test_requires_authentication", "noperm": "test_forbidden_without_permission", "leak": "test_hostile_body",
               "xtenant": "test_cross_tenant_isolation", "massassign": "test_mass_assignment", "idem": "test_idempotency_key",
               "nolimit": "test_page_size_is_capped", "nonosniff": "test_security_headers", "race": "test_parallel_creates",
               "winner": "test_only_one_parallel_action_wins", "noetag": "test_etag_if_match", "shape": "assert_problem|test_forbidden|test_unknown_id",
               "cors": "test_cors_is_not_open"}


def run_pytest(path, env, junit):
    e = dict(os.environ); e.update(env)
    subprocess.run([sys.executable, "-m", "pytest", path, "-q", "-p", "no:cacheprovider", f"--junitxml={junit}"], cwd=ROOT, env=e,
                   stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL, timeout=600)
    root = ET.parse(junit).getroot()
    failed = [f"{c.get('classname')}::{c.get('name')}" for c in root.iter("testcase") if c.find("failure") is not None or c.find("error") is not None]
    total = sum(1 for _ in root.iter("testcase")); skipped = sum(1 for c in root.iter("testcase") if c.find("skipped") is not None)
    return total, skipped, failed


def start_api(flags):
    e = dict(os.environ); e["MOCK_BAD"] = flags
    p = subprocess.Popen([sys.executable, "-m", "uvicorn", "selftest.mock_api:app", "--port", str(PORT), "--log-level", "warning"],
                         cwd=ROOT, env=e, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
    for _ in range(60):
        try:
            urllib.request.urlopen(f"http://127.0.0.1:{PORT}/health", timeout=1); return p
        except Exception:
            time.sleep(0.25)
    p.kill(); raise RuntimeError("mock API did not start")


def api_selftest():
    ok = True
    p = start_api("")
    try:
        total, skipped, failed = run_pytest("api", ENV_API, "/tmp/junit_good.xml")
    finally:
        p.terminate(); p.wait()
    good = not failed
    print(f"[api] healthy API: {total} tests, {skipped} skipped, {len(failed)} failed -> {'PASS (expected)' if good else 'FAIL (UNEXPECTED)'}")
    for f in failed[:10]: print("      ", f)
    ok &= good
    for flag, expect in API_DEFECTS.items():
        p = start_api(flag)
        try:
            total, skipped, failed = run_pytest("api", ENV_API, f"/tmp/junit_{flag}.xml")
        finally:
            p.terminate(); p.wait()
        caught = any(any(x in f for x in expect.split("|")) for f in failed)
        print(f"[api] defect '{flag}': {len(failed)} failing tests, caught by '{expect}': {'YES' if caught else 'NO  <-- audit missed it'}")
        ok &= caught
    return ok


if __name__ == "__main__":
    what = sys.argv[1] if len(sys.argv) > 1 else "all"
    res = True
    if what in ("api", "all"): res &= api_selftest()
    if what in ("db", "all"):
        from selftest.db_selftest import db_selftest
        res &= db_selftest()
    print("SELFTEST", "PASSED" if res else "FAILED"); sys.exit(0 if res else 1)
