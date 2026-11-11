# Several day passes per customer (customer-ux-1, 6 Nov 2026): API checks against the LOCAL
# Dev stack only (API http://127.0.0.1:81, DB localhost\SQLEXPRESS2025 / RegalisWorkSpace_Dev).
# Buys two pay-later day passes for different dates, checks each keeps its own details, checks
# the Passes.OnePassAtATime switch, then cancels the test passes and restores the setting.
#   set REGALIS_TEST_EMAIL / REGALIS_TEST_PASSWORD / REGALIS_TEST_USERID, then run with python.
import datetime, json, os, subprocess, time, urllib.error, urllib.request

API = "http://127.0.0.1:81/api"
SQLCMD = "sqlcmd"
EMAIL, PW, UID = os.environ["REGALIS_TEST_EMAIL"], os.environ["REGALIS_TEST_PASSWORD"], int(os.environ["REGALIS_TEST_USERID"])
results, created = [], []


def sql(q):
    r = subprocess.run([SQLCMD, "-S", r"localhost\SQLEXPRESS2025", "-d", "RegalisWorkSpace_Dev", "-E", "-C", "-I", "-b", "-W", "-h", "-1",
                        "-Q", "SET NOCOUNT ON; " + q], capture_output=True, text=True)
    if r.returncode:
        raise RuntimeError(r.stdout + r.stderr)
    return r.stdout.strip()


def call(method, path, body=None, token=None):
    req = urllib.request.Request(API + path, method=method, data=None if body is None else json.dumps(body).encode(),
                                 headers={"Content-Type": "application/json", **({"Authorization": "Bearer " + token} if token else {})})
    try:
        with urllib.request.urlopen(req, timeout=90) as r:
            return r.status, json.loads(r.read() or b"null")
    except urllib.error.HTTPError as e:
        raw = e.read()
        try:
            return e.code, json.loads(raw)
        except Exception:
            return e.code, raw.decode(errors="replace")


def data(b):
    return b.get("data") if isinstance(b, dict) and "data" in b else b


def check(name, ok, detail=""):
    results.append((name, bool(ok)))
    print(("PASS " if ok else "FAIL ") + name + ("" if ok else "  -> " + str(detail)[:500]))


ist_today = (datetime.datetime.now(datetime.timezone.utc) + datetime.timedelta(hours=5, minutes=30)).date()
d1, d2 = (ist_today + datetime.timedelta(days=3)).isoformat(), (ist_today + datetime.timedelta(days=10)).isoformat()
orig_setting = sql("SELECT SettingValue FROM MST_AppSetting WHERE SettingKey='Passes.OnePassAtATime'")
try:
    s, b = call("POST", "/auth/login", {"email": EMAIL, "password": PW, "portal": "customer"})
    check("customer signs in", s == 200, (s, b))
    tok = data(b)["accessToken"]

    s, b = call("GET", "/passes/purchase-options?type=1", token=tok)
    o = data(b)
    check("options before: no 'current pass', every pack allowed", s == 200 and o["current"] is None and all(p["allowed"] for p in o["plans"]), o)

    s, b = call("POST", "/passes/purchase", {"passPlanId": 1, "validFrom": d1, "paymentMethod": "paylater"}, tok)
    p1 = (data(b) or {}).get("pass") if s == 200 else None
    check(f"buy 1-day pass for {d1} (pay later)", p1 is not None, (s, b))
    p1 and created.append(p1["customerPassId"])

    s, b = call("GET", "/passes/purchase-options?type=1", token=tok)
    o = data(b)
    check("options with a live unpaid pass: still no 'current', every pack allowed, none an upgrade",
          s == 200 and o["current"] is None and all(p["allowed"] and not p["isUpgrade"] for p in o["plans"]), o)

    s, b = call("POST", "/passes/purchase", {"passPlanId": 1, "validFrom": d2, "paymentMethod": "paylater"}, tok)
    p2 = (data(b) or {}).get("pass") if s == 200 else None
    check(f"buy the SAME plan again for {d2} while the first runs (was refused before)", p2 is not None, (s, b))
    p2 and created.append(p2["customerPassId"])

    s, b = call("GET", "/passes/my?page=1&pageSize=100", token=tok)
    items = {x["customerPassId"]: x for x in (data(b) or {}).get("items", [])}
    if p1 and p2:
        a, c = items.get(p1["customerPassId"]), items.get(p2["customerPassId"])
        check("both passes listed separately", a and c and a["passNumber"] != c["passNumber"], items.keys())
        check("each keeps its own dates", a["validFrom"].startswith(d1) and c["validFrom"].startswith(d2) and a["validTo"].startswith(d1) and c["validTo"].startswith(d2), (a, c))
        check("each keeps its own purchase time, amount and payment state",
              a["purchaseUtc"] and c["purchaseUtc"] and a["isPayLater"] and c["isPayLater"] and a["amountPaid"] == 0 and c["amountPaid"] == 0
              and a["totalAmount"] == c["totalAmount"] > 0 and a["status"] == 1 and c["status"] == 1, (a, c))
        check("first pass was not replaced by the second", a["status"] == 1, a)
        inv = sql(f"SELECT COUNT(DISTINCT InvoiceId) FROM TRN_CustomerPass WHERE CustomerPassId IN ({p1['customerPassId']},{p2['customerPassId']})")
        check("each pass has its own invoice", inv == "2", inv)

    # The old rule is still there behind the switch.
    sql("UPDATE MST_AppSetting SET SettingValue='true' WHERE SettingKey='Passes.OnePassAtATime'")
    o = None
    for _ in range(14):  # settings may be cached briefly
        s, b = call("GET", "/passes/purchase-options?type=1", token=tok)
        o = data(b)
        if o and o.get("current"):
            break
        time.sleep(5)
    check("Passes.OnePassAtATime=true brings back 'current pass' + upgrade-only", o and o.get("current") and any(not p["allowed"] for p in o["plans"]), o)
finally:
    sql(f"UPDATE MST_AppSetting SET SettingValue='{orig_setting}' WHERE SettingKey='Passes.OnePassAtATime'")
    # Customers may lack PASS cancel permission (the page then says "ask the front desk");
    # fall back to an admin sign-in (REGALIS_ADMIN_EMAIL / REGALIS_ADMIN_PASSWORD).
    admin_tok = None
    for pid in created:
        s, b = call("POST", f"/passes/{pid}/cancel", token=tok)
        if s == 403 and os.environ.get("REGALIS_ADMIN_EMAIL"):
            if admin_tok is None:
                _, ab = call("POST", "/auth/login", {"email": os.environ["REGALIS_ADMIN_EMAIL"], "password": os.environ["REGALIS_ADMIN_PASSWORD"], "portal": "admin"})
                admin_tok = (data(ab) or {}).get("accessToken")
            s, b = call("POST", f"/passes/{pid}/cancel", token=admin_tok)
        check(f"cleanup: cancel test pass {pid}", s == 200, (s, b))

print(f"\n{sum(ok for _, ok in results)}/{len(results)} passed")
