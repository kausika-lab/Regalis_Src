# End-to-end checks for the customer-portal changes against the LOCAL Dev stack only
# (API http://127.0.0.1:81, DB localhost\SQLEXPRESS2025 / RegalisWorkSpace_Dev).
# Restores the customer's password hash and date of birth at the end.
import json, subprocess, sys, urllib.request, urllib.error

API = "http://127.0.0.1:81/api"
SQLCMD = r"C:\Program Files\Microsoft SQL Server\Client SDK\ODBC\180\Tools\Binn\sqlcmd.exe"
# Test customer on the local Dev DB: REGALIS_TEST_EMAIL, REGALIS_TEST_PASSWORD, REGALIS_TEST_USERID.
import os
CUST_EMAIL, CUST_PW, CUST_ID = os.environ["REGALIS_TEST_EMAIL"], os.environ["REGALIS_TEST_PASSWORD"], int(os.environ["REGALIS_TEST_USERID"])
results = []

def sql(q):
    r = subprocess.run([SQLCMD, "-S", r"localhost\SQLEXPRESS2025", "-d", "RegalisWorkSpace_Dev", "-E", "-C", "-I", "-b", "-W", "-h", "-1",
                        "-Q", "SET NOCOUNT ON; " + q], capture_output=True, text=True)
    if r.returncode: raise RuntimeError(r.stdout + r.stderr)
    return r.stdout.strip()

def call(method, path, body=None, token=None):
    req = urllib.request.Request(API + path, method=method, data=None if body is None else json.dumps(body).encode(),
                                 headers={"Content-Type": "application/json", **({"Authorization": "Bearer " + token} if token else {})})
    try:
        with urllib.request.urlopen(req, timeout=60) as r:
            return r.status, json.loads(r.read() or b"null")
    except urllib.error.HTTPError as e:
        raw = e.read()
        try: return e.code, json.loads(raw)
        except Exception: return e.code, raw.decode(errors="replace")

def check(name, ok, detail=""):
    results.append((name, bool(ok)))
    print(("PASS " if ok else "FAIL ") + name + ("" if ok else "  -> " + str(detail)[:400]))

def login(pw):
    s, b = call("POST", "/auth/login", {"email": CUST_EMAIL, "password": pw, "portal": "customer"})
    return s, (b or {}).get("data") if isinstance(b, dict) else None

def errors_of(b):
    return [(e.get("field"), e.get("message")) for e in (b.get("errors") or [])] if isinstance(b, dict) else []

orig_hash = sql(f"SELECT PasswordHash FROM MST_User WHERE UserId={CUST_ID}")
orig_dob = sql(f"SELECT CONVERT(varchar(10), DateOfBirth, 23) FROM MST_User WHERE UserId={CUST_ID}")
try:
    s, d = login(CUST_PW)
    check("customer signs in", s == 200 and d, (s, d))
    tok = d["accessToken"]

    # 8. sidebar
    s, b = call("GET", "/menus/my", token=tok)
    flat = json.dumps(b)
    check("sidebar: no Notifications entry", s == 200 and "CUST_NOTIFICATIONS" not in flat and '"/notifications"' not in flat, flat[:300])
    check("sidebar: other customer entries still there", "/book" in flat and "/bookings" in flat and "/profile" in flat, flat[:300])
    s, b = call("GET", "/notifications/my?page=1&pageSize=5", token=tok)
    check("notifications API still answers (bell)", s == 200, (s, b))

    # 2. passwords
    s, b = call("POST", "/auth/register", {"fullName": "Validation Probe", "email": "probe@example.invalid", "mobileNumber": "12",
                 "password": "plainpassword", "confirmPassword": "plainpassword", "gender": 3})
    errs = errors_of(b)
    check("register: simple password not rejected for composition", s == 400 and not any("uppercase" in (m or "") for _, m in errs), errs)
    check("register: gender 3 (Other) rejected", any((f or "").lower() == "gender" for f, _ in errs), errs)
    s, b = call("POST", "/auth/register", {"fullName": "Validation Probe", "email": "probe@example.invalid", "mobileNumber": "12",
                 "password": "short", "confirmPassword": "short"})
    check("register: password under 8 still rejected", any((f or "").lower() == "password" for f, _ in errors_of(b)), errors_of(b))
    s, b = call("POST", "/auth/reset-password", {"resetToken": "x", "newPassword": "plainpassword", "confirmPassword": "plainpassword"})
    check("reset: simple password passes validation (fails only on the token)", not any("uppercase" in (m or "") for _, m in errors_of(b)), (s, b))

    s, b = call("POST", "/auth/change-password", {"currentPassword": CUST_PW, "newPassword": "short", "confirmPassword": "short"}, tok)
    check("change password: 'short' rejected", s == 400, (s, b))
    s, b = call("POST", "/auth/change-password", {"currentPassword": CUST_PW, "newPassword": "my simple pass", "confirmPassword": "my simple pass"}, tok)
    check("change password: 'my simple pass' accepted", s == 200, (s, b))
    s, d = login("my simple pass")
    check("login with the simple password", s == 200 and d, (s, d))
    tok = d["accessToken"]

    # 5. DOB optional through onboarding
    s, b = call("GET", "/profile/onboarding", token=tok)
    p = b["data"]["profile"]
    body = {k: p.get(k) or "" for k in ["fullName", "mobileNumber", "companyName", "addressLine1", "addressLine2", "city", "state", "country", "postalCode", "gstin"]}
    body.update({"dateOfBirth": None, "gender": 2})
    s, b = call("POST", "/profile/onboarding", body, tok)
    check("onboarding without date of birth accepted", s == 200, (s, b))
    s, b = call("GET", "/profile/onboarding", token=tok)
    check("profile complete without date of birth", s == 200 and b["data"]["isComplete"] is True and "dateOfBirth" not in (b["data"].get("missingFields") or []), b)
    s, b = call("POST", "/profile/onboarding", {**body, "gender": 3}, tok)
    check("onboarding: gender 3 (Other) rejected", s == 400, (s, b))
    s, b = call("GET", "/auth/me", token=tok)
    check("auth/me still reports isProfileComplete", s == 200 and json.dumps(b).find('"isProfileComplete": true') >= 0, json.dumps(b)[:300])
finally:
    sql(f"UPDATE MST_User SET PasswordHash=N'{orig_hash}' WHERE UserId={CUST_ID}")
    if orig_dob and orig_dob != "NULL":
        sql(f"UPDATE MST_User SET DateOfBirth='{orig_dob}' WHERE UserId={CUST_ID}")
    s, d = login(CUST_PW)
    check("restored: original password works again", s == 200, s)
    print("restored DOB:", sql(f"SELECT CONVERT(varchar(10), DateOfBirth, 23) FROM MST_User WHERE UserId={CUST_ID}"))

print(f"\n{sum(ok for _, ok in results)}/{len(results)} passed")
sys.exit(0 if all(ok for _, ok in results) else 1)
