"""A tiny in-memory API used ONLY to prove the audit works. MOCK_BAD=flag1,flag2 injects specific defects."""
import asyncio, hashlib, os, threading, time, uuid
from fastapi import FastAPI, Request
from fastapi.exceptions import RequestValidationError
from fastapi.responses import JSONResponse, Response

BAD = set(filter(None, os.environ.get("MOCK_BAD", "").split(",")))
TOKENS = {"admin-a": ("A", True), "noperm-a": ("A", False), "admin-b": ("B", True)}
app = FastAPI()
LOCK = threading.Lock()
BUDGETS, COUNTER, IDEM = {}, {"A": 0, "B": 0}, {}


class Problem(Exception):
    def __init__(self, status, code, detail=""):
        self.status, self.code, self.detail = status, code, detail


def problem(status, code, detail=""):
    ct = "application/json" if "shape" in BAD else "application/problem+json"
    return JSONResponse({"type": "about:blank", "title": "error", "status": status, "code": code, "detail": detail,
                         "traceId": uuid.uuid4().hex}, status_code=status, media_type=ct)


@app.exception_handler(Problem)
async def _p(_, e): return problem(e.status, e.code, e.detail)


@app.exception_handler(RequestValidationError)
async def _v(_, e):
    if "leak" in BAD:
        return problem(500, "platform.internal", "System.Text.Json.JsonException at Cost/Budgets.cs:line 42 Npgsql.PostgresException SELECT * FROM cost.budgets")
    return problem(400, "platform.validation_failed", "invalid request")


@app.exception_handler(Exception)
async def _e(_, e): return problem(500, "platform.internal", "unexpected error")


@app.middleware("http")
async def headers(request: Request, call_next):
    resp = await call_next(request)
    if "nonosniff" not in BAD:
        resp.headers["X-Content-Type-Options"] = "nosniff"
    if "cors" in BAD and request.headers.get("origin"):
        resp.headers["Access-Control-Allow-Origin"] = "*"
    return resp


def auth(request: Request, perm=True):
    h = request.headers.get("authorization", "")
    tok = h[7:] if h.lower().startswith("bearer ") else None
    if tok not in TOKENS and "noauth" not in BAD:
        raise Problem(401, "platform.unauthenticated")
    tenant, ok = TOKENS.get(tok, ("A", True))
    if not ok and "noperm" not in BAD:
        raise Problem(403, "platform.forbidden")
    return tenant


@app.get("/health")
def health(): return {"ok": True}


@app.get("/api/v1/cost/budgets")
def list_budgets(request: Request, limit: int = 50, cursor: str = ""):
    t = auth(request)
    if limit < 1:
        raise Problem(400, "platform.invalid_limit")
    if "nolimit" not in BAD:
        limit = min(limit, 200)
    items = [b for b in BUDGETS.values() if b["tenant"] == t][:limit]
    return {"items": [{k: v for k, v in b.items() if k != "tenant"} for b in items]}


@app.post("/api/v1/cost/budgets")
async def create_budget(request: Request):
    t = auth(request)
    try:
        body = await request.json()
    except Exception:
        raise RequestValidationError([])
    if not isinstance(body, dict) or not isinstance(body.get("name", ""), str) or len(body.get("name", "")) > 200:
        raise Problem(400, "platform.validation_failed")
    key = request.headers.get("idempotency-key")
    h = hashlib.sha256(repr(sorted(body.items())).encode()).hexdigest()
    if key and "idem" not in BAD:
        with LOCK:
            prev = IDEM.get((t, key))
            if prev:
                if prev[0] != h:
                    raise Problem(422, "platform.idempotency_key_reuse")
                if prev[1] is None:
                    raise Problem(409, "platform.idempotency_in_progress")
                return JSONResponse(prev[1], status_code=201)
            IDEM[(t, key)] = (h, None)
    if "race" in BAD:
        n = COUNTER[t]; await asyncio.sleep(0.02); COUNTER[t] = n + 1; n += 1
    else:
        with LOCK:
            COUNTER[t] += 1; n = COUNTER[t]
    b = {"id": str(uuid.uuid4()), "tenant": t, "number": f"BUD-{n:04d}", "name": body.get("name", ""), "status": "draft", "version": 1}
    if "massassign" in BAD:
        b["status"] = body.get("status", b["status"]); b["id"] = body.get("id", b["id"])
    with LOCK:
        BUDGETS[b["id"]] = b
    out = {k: v for k, v in b.items() if k != "tenant"}
    if key and "idem" not in BAD:
        IDEM[(t, key)] = (h, out)
    return JSONResponse(out, status_code=201)


def _get(t, bid):
    b = BUDGETS.get(str(bid))
    if not b or (b["tenant"] != t and "xtenant" not in BAD):
        raise Problem(404, "cost.budget_not_found")
    return b


@app.get("/api/v1/cost/budgets/{bid}")
def get_budget(bid: uuid.UUID, request: Request):
    b = _get(auth(request), bid)
    return JSONResponse({k: v for k, v in b.items() if k != "tenant"}, headers={"ETag": f'"{b["version"]}"'})


@app.patch("/api/v1/cost/budgets/{bid}")
async def patch_budget(bid: uuid.UUID, request: Request):
    t = auth(request); b = _get(t, bid)
    if b["tenant"] != t:
        raise Problem(404, "cost.budget_not_found")
    inm = request.headers.get("if-match")
    if "noetag" not in BAD and inm not in (f'"{b["version"]}"',):
        raise Problem(412, "platform.precondition_failed")
    body = await request.json()
    with LOCK:
        b["name"] = body.get("name", b["name"]); b["version"] += 1
    return JSONResponse({k: v for k, v in b.items() if k != "tenant"}, headers={"ETag": f'"{b["version"]}"'})


@app.post("/api/v1/cost/budgets/{bid}/approve")
def approve(bid: uuid.UUID, request: Request):
    t = auth(request); b = _get(t, bid)
    if "winner" in BAD:
        if b["status"] != "draft":
            raise Problem(409, "platform.invalid_state_transition")
        time.sleep(0.05); b["status"] = "approved"; return {"status": "approved"}
    with LOCK:
        if b["status"] != "draft":
            raise Problem(409, "platform.invalid_state_transition")
        b["status"] = "approved"
    return {"status": "approved"}
