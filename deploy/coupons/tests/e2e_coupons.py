"""End-to-end API checks for the coupons kit (25 Nov 2026), against the LOCAL Dev stack.

  API  http://127.0.0.1:81  (local-dev\\api with the coupons kit DLLs)
  DB   localhost\\SQLEXPRESS2025 / RegalisWorkSpace_Dev, migrations ..._Offers and ..._Coupons applied
  Mail the API must send to a local SMTP sink: test purchases also trigger the Wi-Fi kit's emails.

Creates customers coupons-e2e-*@example.test and a Sales user, test coupons with codes CPE<run>*,
one test offer "CPN-E2E <run> ...", bookings on desks DDK-D-10 / DDK-D-11 about ten days ahead,
day passes and a Virtual Office membership. At the end: bookings and passes are cancelled through
the API, the test coupons, offers and their redemptions are deleted with SQL, the Wi-Fi rows of the
test customers are removed, and the users are soft-deleted. Invoices of the test customers stay (the
app cannot delete them); they belong to soft-deleted users. Besides clean-up, SQL is used only to
move a coupon's dates into the past (an expired coupon cannot be saved through the API).
Env: REGALIS_ADMIN_EMAIL / REGALIS_ADMIN_PASSWORD.

usage: python -I e2e_coupons.py
"""
import datetime as dt, json, os, random, subprocess, sys, threading, time, urllib.request, urllib.error
from decimal import Decimal as D, ROUND_HALF_UP

API = os.environ.get("REGALIS_API", "http://127.0.0.1:81/api")
SQL = ["sqlcmd", "-S", r"localhost\SQLEXPRESS2025", "-E", "-C", "-d", "RegalisWorkSpace_Dev", "-I", "-h", "-1", "-W", "-s", "|"]
RUN = str(random.randint(10000, 99999))
results = []


def check(name, ok, detail=""):
    results.append((name, bool(ok)))
    print(("PASS " if ok else "FAIL ") + name + (f"  -- {str(detail)[:500]}" if detail and not ok else ""), flush=True)


def sql(q):
    out = subprocess.run(SQL + ["-b", "-Q", "SET NOCOUNT ON;" + q], capture_output=True, text=True)
    if out.returncode != 0:
        raise SystemExit("SQL failed: " + q[:200] + " :: " + out.stdout + out.stderr)
    return [l.split("|") for l in out.stdout.strip().splitlines() if l.strip()]


def one(q):
    r = sql(q)
    return r[0][0] if r else None


CREDS, RENEWED = {}, {}
LOCK = threading.Lock()


def call(method, path, token=None, body=None, params=None):
    with LOCK:
        while token in RENEWED:
            token = RENEWED[token]
    for _ in range(8):
        s, b = _call(method, path, token, body, params)
        if s == 401 and token in CREDS:
            fresh = login(*CREDS[token])
            with LOCK:
                RENEWED[token] = fresh
            token = fresh
            continue
        if s != 429:
            return s, b
        time.sleep(15)
    return s, b


def _call(method, path, token, body, params):
    url = API + path
    if params:
        url += "?" + "&".join(f"{k}={urllib.request.quote(str(v))}" for k, v in params.items() if v is not None)
    data = json.dumps(body).encode() if body is not None else None
    req = urllib.request.Request(url, data=data, method=method)
    req.add_header("Content-Type", "application/json")
    req.add_header("User-Agent", "Mozilla/5.0 (Linux; Android 14) Chrome/130.0 Mobile")
    if token:
        req.add_header("Authorization", "Bearer " + token)
    try:
        with urllib.request.urlopen(req, timeout=90) as r:
            content = r.read()
            try:
                return r.status, json.loads(content or b"{}")
            except Exception:
                return r.status, {"_bytes": len(content)}
    except urllib.error.HTTPError as e:
        try:
            return e.code, json.loads(e.read() or b"{}")
        except Exception:
            return e.code, {}


def login(email_, password, portal="admin"):
    for _ in range(6):
        s, b = _call("POST", "/auth/login", None, {"email": email_, "password": password, "portal": portal}, None)
        if s == 200:
            with LOCK:
                CREDS[b["data"]["accessToken"]] = (email_, password, portal)
            return b["data"]["accessToken"]
        if s == 429:
            time.sleep(20)
            continue
        raise SystemExit(f"login {email_} failed: {s} {b}")
    raise SystemExit("login rate limited")


def r2(x):
    return D(str(x)).quantize(D("0.01"), rounding=ROUND_HALF_UP)


def priced(base, discount):
    """What checkout charges: discount off the price before GST, CGST and SGST 9% each."""
    taxable = D(str(base)) - D(str(discount))
    c = r2(taxable * 9 / 100)
    return taxable, c, taxable + c + c


def dec(v):
    return D(str(v)).quantize(D("0.01"))


def err_code(b):
    """The COUPON_* code of a 422 envelope."""
    return json.dumps(b.get("errors") or []) if isinstance(b, dict) else ""


IST = dt.timezone(dt.timedelta(hours=5, minutes=30))
today = dt.datetime.now(dt.timezone.utc).astimezone(IST).date()


def open_day(after):
    d = today + dt.timedelta(days=after)
    while d.weekday() == 6:
        d += dt.timedelta(days=1)
    return d


DAY1 = open_day(10)
DAY2 = open_day((DAY1 - today).days + 1)


def iso(d):
    return d.astimezone(dt.timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ")


def at(day, hh, mm=0):
    return dt.datetime(day.year, day.month, day.day, hh, mm, tzinfo=IST)


TEST_USERS = "Email LIKE 'coupons-e2e-%@example.test'"
TEST_COUPONS = "Code LIKE 'CPE%'"
TEST_OFFERS = "OfferName LIKE 'CPN-E2E %'"


def purge():
    sql(f"""DECLARE @u TABLE (id bigint); INSERT INTO @u SELECT UserId FROM MST_User WHERE {TEST_USERS};
        DECLARE @c TABLE (id int); INSERT INTO @c SELECT CouponId FROM MST_Coupon WHERE {TEST_COUPONS};
        DECLARE @o TABLE (id int); INSERT INTO @o SELECT OfferId FROM MST_Offer WHERE {TEST_OFFERS};
        DELETE FROM TRN_CouponRedemption WHERE CouponId IN (SELECT id FROM @c) OR CustomerId IN (SELECT id FROM @u);
        DELETE FROM MST_CouponModule WHERE CouponId IN (SELECT id FROM @c);
        DELETE FROM MST_Coupon WHERE CouponId IN (SELECT id FROM @c);
        DELETE FROM TRN_OfferRedemption WHERE OfferId IN (SELECT id FROM @o) OR CustomerId IN (SELECT id FROM @u);
        DELETE FROM MST_OfferScope WHERE OfferId IN (SELECT id FROM @o);
        DELETE FROM MST_OfferModule WHERE OfferId IN (SELECT id FROM @o);
        DELETE FROM MST_Offer WHERE OfferId IN (SELECT id FROM @o);
        IF OBJECT_ID('dbo.TRN_WifiAccess','U') IS NOT NULL
        BEGIN
            DECLARE @a TABLE (id bigint); INSERT INTO @a SELECT AccessId FROM TRN_WifiAccess WHERE CustomerId IN (SELECT id FROM @u);
            DELETE k FROM TRN_WifiSimKey k JOIN TRN_WifiAccess a ON a.ControllerRef = k.ControllerRef WHERE a.AccessId IN (SELECT id FROM @a);
            DELETE FROM TRN_WifiSession WHERE AccessId IN (SELECT id FROM @a);
            DELETE FROM TRN_WifiDevice WHERE AccessId IN (SELECT id FROM @a);
            DELETE FROM TRN_WifiEvent WHERE AccessId IN (SELECT id FROM @a);
            DELETE FROM TRN_WifiAccessSource WHERE AccessId IN (SELECT id FROM @a)
                OR (SourceType = 1 AND RefId IN (SELECT BookingId FROM TRN_Booking WHERE CustomerId IN (SELECT id FROM @u)))
                OR (SourceType = 2 AND RefId IN (SELECT CustomerPassId FROM TRN_CustomerPass WHERE CustomerId IN (SELECT id FROM @u)));
            DELETE FROM TRN_WifiAccess WHERE ParentAccessId IN (SELECT id FROM @a);
            DELETE FROM TRN_WifiAccess WHERE AccessId IN (SELECT id FROM @a);
            DELETE FROM TRN_AdminNotification WHERE ReferenceType = 'WifiAccess' AND ReferenceId IN (SELECT id FROM @a);
        END
        UPDATE TRN_Booking SET Status = 5, CancelledUtc = SYSUTCDATETIME(), CancellationReason = 'coupons e2e cleanup' WHERE CustomerId IN (SELECT id FROM @u) AND Status IN (1, 2, 3);
        UPDATE MST_User SET IsDeleted = 1, IsActive = 0, Status = 2 WHERE {TEST_USERS} AND IsDeleted = 0;""")


purge()
admin = login(os.environ["REGALIS_ADMIN_EMAIL"], os.environ["REGALIS_ADMIN_PASSWORD"])


def make_user(tag, name, utype, role_code):
    pw = "Coupons#E2e" + RUN + "x"
    role = one(f"SELECT RoleId FROM MST_Role WHERE RoleCode='{role_code}'")
    em = f"coupons-e2e-{tag}{RUN}@example.test"
    s, b = call("POST", "/users", admin, {"fullName": name, "email": em, "mobileNumber": f"+9192{RUN}{random.randint(100, 999)}", "password": pw, "userType": utype,
                                          "roleIds": [int(role)], "defaultLocationId": 2})
    if s not in (200, 201):
        raise SystemExit(f"create user {tag}: {s} {b}")
    uid = b["data"]["userId"]
    sql(f"UPDATE MST_User SET MustChangePassword=0, Status=1, IsEmailVerified=1, IsMobileVerified=1, AddressLine1='1 Test Road', AddressLine2='Block A', City='Bengaluru', State='Karnataka',"
        f" Country='India', PostalCode='560098', Gender=1 WHERE UserId={uid}")
    return uid, em, pw


c1, c1_email, c1_pw = make_user("c1", "Coupons E2E Asha", 1, "CUSTOMER")
c2, c2_email, c2_pw = make_user("c2", "Coupons E2E Kiran", 1, "CUSTOMER")
c3, c3_email, c3_pw = make_user("c3", "Coupons E2E Nila", 1, "CUSTOMER")
sl, sl_email, sl_pw = make_user("sales", "Coupons E2E Sales", 3, "SALES")
cust1 = login(c1_email, c1_pw, "customer")
cust2 = login(c2_email, c2_pw, "customer")
cust3 = login(c3_email, c3_pw, "customer")
sales = login(sl_email, sl_pw)
for c in (c1, c2, c3):
    s, b = call("POST", f"/wallet/{c}/adjust", admin, {"direction": "credit", "amount": 50000, "reason": "coupons e2e"})
    if s != 200:
        raise SystemExit(f"wallet credit: {s} {b}")


def wallet(tok):
    return dec(call("GET", "/wallet/me", tok)[1]["data"]["balance"])


# ---- 1. permissions ------------------------------------------------------------------------------
s, _ = call("GET", "/admin/coupons", sales)
check("sales (OFFER.VIEW): coupon list opens", s == 200, s)
s, _ = call("POST", "/admin/coupons", sales, {})
check("sales: creating a coupon refused (403, needs OFFER.MANAGE)", s == 403, s)
s, _ = call("GET", "/admin/coupons", cust1)
check("customer: admin coupons refused (403)", s == 403, s)
s, _ = call("POST", "/coupons/preview", admin, {"module": "pass", "passPlanId": 1, "code": "X"})
check("staff cannot preview customer coupons (403)", s == 403, s)
s, _ = call("GET", "/coupons", None, params={"module": "pass"})
check("anonymous: coupon list needs sign-in (401)", s == 401, s)
s, b = call("GET", "/admin/coupons", admin)
base_summary = b["data"]["summary"]


# ---- 2. create, validate --------------------------------------------------------------------------
def coupon(code, **kw):
    body = {"code": code, "description": kw.pop("description", "coupons e2e"), "discountType": kw.pop("type", 1), "discountValue": kw.pop("value"),
            "maxDiscountAmount": kw.pop("max", None), "minAmount": kw.pop("min", None), "modules": kw.pop("modules"),
            "validFrom": kw.pop("start", today.isoformat()), "validUntil": kw.pop("end", (today + dt.timedelta(days=60)).isoformat()),
            "totalUses": kw.pop("total", None), "usesPerCustomer": kw.pop("per", None), "showInPortal": kw.pop("show", True), "isActive": kw.pop("active", True)}
    assert not kw, kw
    return call("POST", "/admin/coupons", admin, body)


K = {t: f"CPE{RUN}{t}" for t in ["A", "B", "C", "D", "E", "F", "G", "H", "J", "L"]}
s, b = coupon("", value=10, modules=[])
fields = {e.get("field") for e in b.get("errors", [])}
check("validation: code, services and value-free fields reported together", s == 400 and {"code", "modules"} <= fields, (s, b))
s, b = coupon("a b", value=10, modules=[1])
check("validation: code with a space refused", s == 400 and "code" in json.dumps(b), (s, b))
s, b = coupon(K["A"], value=120, modules=[1])
check("validation: percentage above 100 refused", s == 400 and "discountValue" in json.dumps(b), (s, b))
s, b = coupon(K["A"], value=10, modules=[1], start=(today + dt.timedelta(days=5)).isoformat(), end=(today + dt.timedelta(days=2)).isoformat())
check("validation: Valid until before Valid from refused", s == 400 and "validUntil" in json.dumps(b), (s, b))
s, b = coupon(K["A"], value=10, modules=[1], start=(today - dt.timedelta(days=9)).isoformat(), end=(today - dt.timedelta(days=1)).isoformat())
check("validation: a coupon cannot be created already expired", s == 400 and "validUntil" in json.dumps(b), (s, b))
s, b = coupon(K["A"], value=10, modules=[1], total=2, per=3)
check("validation: uses per customer above the total refused", s == 400 and "usesPerCustomer" in json.dumps(b), (s, b))
s, b = coupon(K["A"], value=0, modules=[1])
check("validation: zero discount refused", s == 400 and "discountValue" in json.dumps(b), (s, b))

# A: 10% capped at 100, min 300, bookings + passes, 3 in total, once per customer.
s, b = coupon(K["A"].lower(), value=10, max=100, min=300, modules=[1, 2], total=3, per=1)
check("create coupon A (code saved upper case, live)", s == 201 and b["data"]["code"] == K["A"] and b["data"]["status"] == "Active", (s, b))
CA = b["data"]["couponId"]
s, b = coupon(K["A"], value=5, modules=[1])
check("duplicate coupon code refused", s == 400 and "already uses the code" in json.dumps(b), (s, b))
# B: fixed 150 on passes, twice per customer.
s, b = coupon(K["B"], type=2, value=150, modules=[2], per=2)
CB = b["data"]["couponId"]
# C: expired (dates moved into the past with SQL afterwards).
s, b = coupon(K["C"], value=10, modules=[2])
CC = b["data"]["couponId"]
sql(f"UPDATE MST_Coupon SET ValidFrom='{(today - dt.timedelta(days=10)).isoformat()}', ValidUntil='{(today - dt.timedelta(days=1)).isoformat()}' WHERE CouponId={CC}")
# D: inactive.  E: scheduled.  F: hidden membership 500 off.  G: one use in total (passes).
s, b = coupon(K["D"], value=10, modules=[2], active=False)
CD = b["data"]["couponId"]
check("inactive coupon saved as Inactive", b["data"]["status"] == "Inactive", b)
s, b = coupon(K["E"], value=10, modules=[2], start=(today + dt.timedelta(days=3)).isoformat())
CE = b["data"]["couponId"]
check("future coupon saved as Scheduled", b["data"]["status"] == "Scheduled", b)
s, b = coupon(K["F"], type=2, value=500, modules=[3], show=False)
CF = b["data"]["couponId"]
s, b = coupon(K["G"], type=2, value=100, modules=[2], total=1)
CG = b["data"]["couponId"]
# H: bookings only, fixed 100 (switched off later, while on a hold).  J: one use in total, for the race.
s, b = coupon(K["H"], type=2, value=100, modules=[1])
CH = b["data"]["couponId"]
s, b = coupon(K["J"], type=2, value=50, modules=[2], total=1)
CJ = b["data"]["couponId"]

# A test offer with a code: a coupon cannot take an offer's code, and a coupon replaces offers.
s, b = call("POST", "/admin/offers", admin, {"offerName": f"CPN-E2E {RUN} pass 20", "title": "E2E 20% pass offer", "code": f"CPO{RUN}", "showInPortal": True,
                                            "discountType": 1, "discountValue": 20, "modules": [1, 2], "workspaceTypeIds": [], "passPlanIds": [], "membershipPlanIds": [],
                                            "startDate": today.isoformat(), "endDate": (today + dt.timedelta(days=30)).isoformat(), "isActive": True, "canCombine": False,
                                            "sortOrder": 100})
check("test offer created (20% on bookings and passes)", s == 201, (s, b))
OF = b["data"]["offerId"]
s, b = coupon(f"CPO{RUN}", value=5, modules=[1])
check("a coupon cannot take an existing offer's code", s == 400 and "offer code" in json.dumps(b), (s, b))

s, b = call("GET", "/admin/coupons", admin, params={"search": f"CPE{RUN}", "pageSize": 50})
st = {}
for r in b["data"]["page"]["items"]:
    st[r["status"]] = st.get(r["status"], 0) + 1
check("statuses of this run's 9 coupons: 6 Active (A,B,F,G,H,J), 1 Inactive, 1 Expired, 1 Scheduled",
      st == {"Active": 6, "Inactive": 1, "Expired": 1, "Scheduled": 1}, st)
sm = b["data"]["summary"]
check("overview cards count at least this run's coupons", sm["total"] - base_summary["total"] >= 9 and sm["expired"] - base_summary["expired"] >= 1, (sm, base_summary))
s, b = call("GET", "/admin/coupons", admin, params={"status": "expired", "search": f"CPE{RUN}"})
check("filter expired + search finds only C", s == 200 and [r["code"] for r in b["data"]["page"]["items"]] == [K["C"]], b)
s, b = call("GET", "/admin/coupons", admin, params={"module": 3, "search": f"CPE{RUN}"})
check("filter by service Membership finds only F", [r["code"] for r in b["data"]["page"]["items"]] == [K["F"]], b)

# ---- 3. portal list ----------------------------------------------------------------------------------
s, b = call("GET", "/coupons", cust1, params={"module": "pass"})
codes = {c["code"] for c in b["data"]}
check("pass list: A, B, G, J listed", {K["A"], K["B"], K["G"], K["J"]} <= codes, codes)
check("pass list: expired, inactive, scheduled, membership-only and booking-only not listed", not ({K["C"], K["D"], K["E"], K["F"], K["H"]} & codes), codes)
check("pass list: A shows 1 use left for this customer", next(c for c in b["data"] if c["code"] == K["A"])["usesLeft"] == 1, b)
s, b = call("GET", "/coupons", cust1, params={"module": "membership"})
check("hidden membership coupon F is not listed", K["F"] not in {c["code"] for c in b["data"]}, b)


# ---- 4. preview: every error message --------------------------------------------------------------------
def preview(tok, **kw):
    return call("POST", "/coupons/preview", tok, kw)


s, b = preview(cust1, module="pass", passPlanId=1, code="NOPE" + RUN)
d = b["data"]
check("unknown code: not found, COUPON_INVALID, 'is not valid'", s == 200 and not d["found"] and d["errorCode"] == "COUPON_INVALID" and "is not valid" in d["error"], b)
s, b = preview(cust1, module="pass", passPlanId=1, code=K["C"])
check("expired coupon: COUPON_EXPIRED, says when", b["data"]["errorCode"] == "COUPON_EXPIRED" and "expired on" in b["data"]["error"] and b["data"]["found"], b)
s, b = preview(cust1, module="pass", passPlanId=1, code=K["D"])
check("inactive coupon: COUPON_INACTIVE 'is not active'", b["data"]["errorCode"] == "COUPON_INACTIVE" and "is not active" in b["data"]["error"], b)
s, b = preview(cust1, module="pass", passPlanId=1, code=K["E"])
check("scheduled coupon: COUPON_NOT_STARTED 'can be used from'", b["data"]["errorCode"] == "COUPON_NOT_STARTED" and "can be used from" in b["data"]["error"], b)
s, b = preview(cust1, module="pass", passPlanId=1, code=K["F"])
check("membership coupon on a pass: COUPON_NOT_APPLICABLE", b["data"]["errorCode"] == "COUPON_NOT_APPLICABLE" and "cannot be used on passes" in b["data"]["error"], b)
s, b = preview(cust1, module="booking", workspaceId=10, baseAmounts=[200], code=K["A"])
check("below the minimum booking amount: COUPON_MIN_AMOUNT", b["data"]["errorCode"] == "COUPON_MIN_AMOUNT" and "300.00" in b["data"]["error"], b)
check("an invalid coupon leaves the price untouched", dec(b["data"]["totalAmount"]) == dec(b["data"]["originalTotal"]) == D("236.00") and not b["data"]["applied"], b)
s, b = preview(cust1, module="pass", passPlanId=1, code="  " + K["A"].lower() + " ")
tax, g, tot = priced(800, 80)
d = b["data"]
check("valid coupon typed in lower case with spaces: 10% of 800 = 80", d["valid"] and dec(d["discountAmount"]) == D("80.00") and d["coupon"]["code"] == K["A"], b)
check(f"preview totals: 720 + {g} x2 = {tot}, original 944.00", dec(d["taxableAmount"]) == tax and dec(d["cgstAmount"]) == g and dec(d["totalAmount"]) == tot and dec(d["originalTotal"]) == D("944.00"), d)
check("preview carries the coupon as one applied line (offerId 0)", len(d["applied"]) == 1 and d["applied"][0]["offerId"] == 0 and d["applied"][0]["code"] == K["A"], d)
s, b = preview(cust1, module="booking", workspaceId=10, baseAmounts=[2000], code=K["A"])
check("percentage capped at the maximum: 10% of 2000 = 200 -> 100", dec(b["data"]["discountAmount"]) == D("100.00"), b)
s, b = preview(cust1, module="pass", passPlanId=1, code="")
check("empty code: asks for one", s == 200 and not b["data"]["valid"] and "Enter a coupon code" in b["data"]["error"], b)

# ---- 5. day pass from the wallet with coupon A ----------------------------------------------------------
w0 = wallet(cust1)
s, b = call("POST", "/passes/purchase", cust1, {"passPlanId": 1, "validFrom": DAY2.isoformat(), "paymentMethod": "wallet", "couponCode": K["A"]})
check(f"day pass with coupon A, wallet: {tot}", s == 200 and dec(b["data"]["pass"]["totalAmount"]) == tot, (s, b))
p1 = b["data"]["pass"]["customerPassId"] if s == 200 else 0
check(f"wallet charged {tot}", w0 - wallet(cust1) == tot, w0 - wallet(cust1))
r = sql(f"SELECT i.SubTotal, i.DiscountAmount, i.TaxableAmount, i.CgstAmount, i.SgstAmount, i.TotalAmount, l.Description FROM TRN_Invoice i JOIN TRN_InvoiceLine l ON l.InvoiceId=i.InvoiceId WHERE i.CustomerPassId={p1}")
check("pass invoice: 800 - 80 = 720, GST x2, total", r and [dec(x) for x in r[0][:6]] == [D("800.00"), D("80.00"), tax, g, g, tot], r)
check("invoice line names the coupon", r and f"coupon {K['A']}" in r[0][6], r)
check("redemption row tied to the pass", one(f"SELECT COUNT(*) FROM TRN_CouponRedemption WHERE CouponId={CA} AND CustomerPassId={p1} AND CustomerId={c1} AND DiscountAmount=80") == "1")
s, b = preview(cust1, module="pass", passPlanId=1, code=K["A"])
check("same customer again: COUPON_ALREADY_USED 'already used'", b["data"]["errorCode"] == "COUPON_ALREADY_USED" and "already used" in b["data"]["error"], b)
s, b = call("POST", "/passes/purchase", cust1, {"passPlanId": 1, "validFrom": DAY2.isoformat(), "paymentMethod": "wallet", "couponCode": K["A"]})
check("server refuses the used coupon on purchase (422 COUPON_ALREADY_USED)", s == 422 and "COUPON_ALREADY_USED" in json.dumps(b), (s, b))
s, b = call("GET", "/coupons", cust1, params={"module": "pass"})
check("portal list: A now shows 0 uses left for this customer", next(c for c in b["data"] if c["code"] == K["A"])["usesLeft"] == 0, b)

# ---- 6. a coupon replaces the offers ---------------------------------------------------------------------
tB, gB, totB = priced(800, 150)
s, b = call("POST", "/passes/purchase", cust1, {"passPlanId": 1, "validFrom": DAY2.isoformat(), "paymentMethod": "wallet", "offerIds": [OF], "couponCode": K["B"]})
check(f"offer + coupon sent together: only the coupon is charged ({totB})", s == 200 and dec(b["data"]["pass"]["totalAmount"]) == totB, (s, b))
p2 = b["data"]["pass"]["customerPassId"] if s == 200 else 0
check("no offer redemption on that pass", one(f"SELECT COUNT(*) FROM TRN_OfferRedemption WHERE CustomerPassId={p2}") == "0")

# ---- 7. online pass order with a coupon ---------------------------------------------------------------------
s, b = call("POST", "/passes/order", cust2, {"passPlanId": 1, "couponCode": K["B"]})
check(f"online pass order priced with coupon B ({totB})", s == 200 and dec(b["data"]["amount"]) == totB, (s, b))
order = b["data"]["gatewayOrderId"]
check("order-time coupon row written with the payment", one(f"SELECT COUNT(*) FROM TRN_CouponRedemption r JOIN TRN_Payment p ON p.PaymentId=r.PaymentId WHERE p.GatewayOrderId='{order}' AND r.CustomerPassId IS NULL") == "1")
s, pay = call("POST", "/payments/sandbox-complete", cust2, {"gatewayOrderId": order})
s, b = call("POST", "/passes/purchase", cust2, {"passPlanId": 1, "validFrom": DAY2.isoformat(), "paymentMethod": "gateway", "gatewayOrderId": order,
                                                "gatewayPaymentId": pay["data"]["gatewayPaymentId"], "gatewaySignature": pay["data"]["gatewaySignature"]})
check("online purchase completes at the ordered price", s == 200 and dec(b["data"]["pass"]["totalAmount"]) == totB, (s, b))
p3 = b["data"]["pass"]["customerPassId"] if s == 200 else 0
check("order-time row now points at the pass", one(f"SELECT COUNT(*) FROM TRN_CouponRedemption WHERE CustomerPassId={p3} AND PaymentId IS NOT NULL AND CouponId={CB}") == "1")
check("one row for the online purchase (not two)", one(f"SELECT COUNT(*) FROM TRN_CouponRedemption WHERE CustomerId={c2} AND CouponId={CB}") == "1")

# ---- 8. usage limit (total) ----------------------------------------------------------------------------------
s, b = call("POST", "/passes/purchase", cust1, {"passPlanId": 1, "validFrom": DAY2.isoformat(), "paymentMethod": "wallet", "couponCode": K["G"]})
check("coupon G (1 use in total) used by customer 1", s == 200, (s, b))
s, b = preview(cust2, module="pass", passPlanId=1, code=K["G"])
check("customer 2: COUPON_LIMIT_REACHED 'reached its usage limit'", b["data"]["errorCode"] == "COUPON_LIMIT_REACHED" and "usage limit" in b["data"]["error"], b)
s, b = call("POST", "/passes/purchase", cust2, {"passPlanId": 1, "validFrom": DAY2.isoformat(), "paymentMethod": "wallet", "couponCode": K["G"]})
check("server refuses the used-up coupon (422 COUPON_LIMIT_REACHED)", s == 422 and "COUPON_LIMIT_REACHED" in json.dumps(b), (s, b))
s, b = call("GET", "/coupons", cust3, params={"module": "pass"})
check("a used-up coupon is no longer listed", K["G"] not in {c["code"] for c in b["data"]}, b)

# Two customers race for the last use of J: exactly one gets it.
race = {}


def buy(tag, tok):
    race[tag] = call("POST", "/passes/purchase", tok, {"passPlanId": 1, "validFrom": DAY2.isoformat(), "paymentMethod": "wallet", "couponCode": K["J"]})


ts = [threading.Thread(target=buy, args=("c2", cust2)), threading.Thread(target=buy, args=("c3", cust3))]
[t.start() for t in ts]
[t.join() for t in ts]
codes_ = sorted(v[0] for v in race.values())
check("race for the last use: one 200, one 422", codes_ == [200, 422], {k: (v[0], json.dumps(v[1])[:200]) for k, v in race.items()})
check("race: exactly one redemption of J", one(f"SELECT COUNT(*) FROM TRN_CouponRedemption WHERE CouponId={CJ}") == "1")


# ---- 9. booking: hold, coupon, wallet ------------------------------------------------------------------------
def hold(tok, ws, day, hh, minutes=120):
    for shift in range(0, 8):
        s, b = call("POST", "/bookings", tok, {"workspaceId": ws, "startUtc": iso(at(day, hh + shift)), "durationMinutes": minutes})
        if s in (200, 201):
            return b["data"]
        if s != 409:
            raise SystemExit(f"hold failed {s} {b}")
    raise SystemExit("no free slot")


h1 = hold(cust2, 10, DAY1, 9)
base1 = dec(h1["baseAmount"])
disc1 = r2(base1 * D("0.10")).min(D("100.00"))
t1, g1, tot1 = priced(base1, disc1)
s, b = call("POST", "/coupons/bookings/apply", cust2, {"bookingIds": [h1["bookingId"]], "code": "NOPE" + RUN})
check("booking: unknown code refused (422 COUPON_INVALID), nothing changed", s == 422 and "COUPON_INVALID" in json.dumps(b)
      and one(f"SELECT COUNT(*) FROM TRN_CouponRedemption WHERE BookingId={h1['bookingId']}") == "0", (s, b))
s, b = call("POST", "/coupons/bookings/apply", cust1, {"bookingIds": [h1["bookingId"]], "code": K["A"]})
check("another customer cannot touch the hold (404)", s == 404, (s, b))
s, b = call("POST", "/coupons/bookings/apply", cust2, {"bookingIds": [h1["bookingId"]], "code": K["A"]})
check(f"coupon A on the hold: base {base1} - {disc1}, total {tot1}", s == 200 and dec(b["data"]["totalAmount"]) == tot1 and dec(b["data"]["discountAmount"]) == disc1, (s, b))
w0 = wallet(cust2)
s, b = call("POST", f"/bookings/{h1['bookingId']}/confirm", cust2, {"paymentMethod": "wallet"})
check(f"confirm from the wallet at {tot1}", s == 200 and dec(b["data"]["booking"]["totalAmount"]) == tot1, (s, b))
check(f"wallet charged {tot1}", w0 - wallet(cust2) == tot1, w0 - wallet(cust2))
r = sql(f"SELECT TOP 1 i.DiscountAmount, i.TaxableAmount, i.TotalAmount, l.Description FROM TRN_Invoice i JOIN TRN_InvoiceLine l ON l.InvoiceId=i.InvoiceId WHERE i.BookingId={h1['bookingId']} ORDER BY i.InvoiceId")
check("booking invoice: discount, taxable and total match; line names the coupon", r and dec(r[0][0]) == disc1 and dec(r[0][1]) == t1 and dec(r[0][2]) == tot1 and f"coupon {K['A']}" in r[0][3], r)
check("booking history records the coupon", one(f"SELECT COUNT(*) FROM TRN_BookingStatusHistory WHERE BookingId={h1['bookingId']} AND Reason LIKE 'Coupon {K['A']} applied%'") == "1")

# ---- 10. on a hold a coupon replaces the offer, and an offer replaces the coupon --------------------------------
h2 = hold(cust3, 11, DAY1, 9)
s, b = call("POST", "/offers/bookings/apply", cust3, {"bookingIds": [h2["bookingId"]], "offerIds": [OF]})
check("offer on the hold first", s == 200 and one(f"SELECT COUNT(*) FROM TRN_OfferRedemption WHERE BookingId={h2['bookingId']}") == "1", (s, b))
s, b = call("POST", "/coupons/bookings/apply", cust3, {"bookingIds": [h2["bookingId"]], "code": K["H"]})
t2, g2, tot2 = priced(dec(h2["baseAmount"]), 100)
check(f"coupon H replaces it: offer row gone, coupon row there, total {tot2}", s == 200 and dec(b["data"]["totalAmount"]) == tot2
      and one(f"SELECT COUNT(*) FROM TRN_OfferRedemption WHERE BookingId={h2['bookingId']}") == "0"
      and one(f"SELECT COUNT(*) FROM TRN_CouponRedemption WHERE BookingId={h2['bookingId']}") == "1", (s, b))
s, b = call("POST", "/coupons/bookings/apply", cust3, {"bookingIds": [h2["bookingId"]], "code": ""})
t0, g0, tot0 = priced(dec(h2["baseAmount"]), 0)
check(f"empty code removes the coupon: back to {tot0}", s == 200 and dec(b["data"]["totalAmount"]) == tot0
      and one(f"SELECT COUNT(*) FROM TRN_CouponRedemption WHERE BookingId={h2['bookingId']}") == "0", (s, b))
s, b = call("POST", "/coupons/bookings/apply", cust3, {"bookingIds": [h2["bookingId"]], "code": K["H"]})
s, b = call("POST", "/offers/bookings/apply", cust3, {"bookingIds": [h2["bookingId"]], "offerIds": [OF]})
s, b = call("GET", f"/admin/coupons/{CH}/redemptions", admin)
check("an offer put on the hold afterwards: the coupon use shows Released", s == 200 and [r["state"] for r in b["data"]] == ["Released"], b)
s, b = call("GET", f"/admin/coupons/{CH}", admin)
check("... and does not count as a use", b["data"]["uses"] == 0, b)
s, b = call("POST", f"/bookings/{h2['bookingId']}/confirm", cust3, {"paymentMethod": "wallet"})
check("confirm charges the offer price only (no coupon note)", s == 200 and one(f"SELECT COUNT(*) FROM TRN_InvoiceLine l JOIN TRN_Invoice i ON i.InvoiceId=l.InvoiceId WHERE i.BookingId={h2['bookingId']} AND l.Description LIKE '%coupon%'") == "0", (s, b))

# ---- 11. switched off after it was put on a hold -----------------------------------------------------------------
h3 = hold(cust3, 11, DAY2, 9)
s, b = call("POST", "/coupons/bookings/apply", cust3, {"bookingIds": [h3["bookingId"]], "code": K["H"]})
check("coupon H on a second hold", s == 200, (s, b))
call("POST", f"/admin/coupons/{CH}/deactivate", admin)
s, b = call("POST", f"/bookings/{h3['bookingId']}/confirm", cust3, {"paymentMethod": "wallet"})
check("deactivated meanwhile: wallet confirm refused (422 COUPON_NOT_AVAILABLE)", s == 422 and "COUPON_NOT_AVAILABLE" in json.dumps(b), (s, b))
s, b = call("POST", "/payments/order", cust3, {"bookingId": h3["bookingId"]})
check("deactivated meanwhile: online order refused too", s == 422 and "COUPON_NOT_AVAILABLE" in json.dumps(b), (s, b))
call("POST", f"/admin/coupons/{CH}/activate", admin)
s, b = call("POST", f"/bookings/{h3['bookingId']}/cancel", cust3, {"reason": "coupons e2e"})

# ---- 12. membership, hidden coupon by code, pay later -------------------------------------------------------------
tF, gF, totF = priced(2500, 500)
s, b = preview(cust1, module="membership", passPlanId=21, code=K["F"])
check(f"hidden membership coupon works by code: 2500 - 500 -> {totF}", b["data"]["valid"] and dec(b["data"]["totalAmount"]) == totF, b)
s, b = call("POST", "/passes/purchase", cust1, {"passPlanId": 21, "validFrom": today.isoformat(), "paymentMethod": "paylater", "couponCode": K["F"]})
check(f"Virtual Office, pay later, with coupon F: {totF} awaiting payment", s == 200 and dec(b["data"]["pass"]["totalAmount"]) == totF and b["data"]["pass"]["status"] == 5, (s, b))
s, b = call("POST", "/passes/purchase", cust1, {"passPlanId": 1, "validFrom": DAY2.isoformat(), "paymentMethod": "wallet", "couponCode": K["F"]})
check("membership coupon on a day pass refused by the server", s == 422 and "COUPON_NOT_APPLICABLE" in json.dumps(b), (s, b))

# ---- 13. admin: uses, delete, edit ----------------------------------------------------------------------------------
s, b = call("GET", "/admin/coupons", admin, params={"search": K["A"]})
row = b["data"]["page"]["items"][0]
check("list row: A used 2 times (pass + booking), 180.00 given", row["uses"] == 2 and dec(row["discountGiven"]) == disc1 + D("80.00"), row)
s, b = call("GET", f"/admin/coupons/{CA}/redemptions", admin)
check("uses list: two Used rows with references and names", s == 200 and sorted(r["state"] for r in b["data"]) == ["Used", "Used"] and all(r["reference"] and r["customerName"] for r in b["data"]), b)
s, b = call("GET", f"/admin/coupons/{CA}", admin)
check("a used coupon cannot be deleted (canDelete false)", b["data"]["canDelete"] is False, b)
s, b = call("DELETE", f"/admin/coupons/{CA}", admin)
check("delete refused once used (422 COUPON_IN_USE)", s == 422 and "COUPON_IN_USE" in json.dumps(b), (s, b))
upd = {"code": K["A"] + "X", "description": "x", "discountType": 1, "discountValue": 10, "maxDiscountAmount": 100, "minAmount": 300, "modules": [1, 2],
       "validFrom": today.isoformat(), "validUntil": (today + dt.timedelta(days=60)).isoformat(), "totalUses": 3, "usesPerCustomer": 1, "showInPortal": True, "isActive": True}
s, b = call("PUT", f"/admin/coupons/{CA}", admin, upd)
check("the code of a used coupon cannot change", s == 400 and "cannot change" in json.dumps(b), (s, b))
upd["code"] = K["A"]
upd["totalUses"] = 5
s, b = call("PUT", f"/admin/coupons/{CA}", admin, upd)
check("other fields of a used coupon can be edited", s == 200 and b["data"]["totalUses"] == 5, (s, b))
s, b = call("POST", f"/admin/coupons/{CC}/activate", admin)
check("an expired coupon cannot be activated", s == 422 and "expired" in json.dumps(b), (s, b))
s, b = call("DELETE", f"/admin/coupons/{CE}", admin)
check("an unused coupon can be deleted", s == 200, (s, b))
s, b = coupon(K["E"], value=10, modules=[2])
check("a deleted coupon's code can be reused", s == 201, (s, b))
s, b = call("GET", "/admin/coupons", admin, params={"search": K["A"]})

# ---- 14. cancellation gives the use back ---------------------------------------------------------------------------
s, b = call("POST", f"/bookings/{h1['bookingId']}/cancel", cust2, {"reason": "coupons e2e"})
check("customer 2 cancels the booking bought with A", s == 200, (s, b))
s, b = preview(cust2, module="pass", passPlanId=1, code=K["A"])
check("cancelled booking gives customer 2's use of A back", b["data"]["valid"], b)

# ---- 15. offers still work as before -------------------------------------------------------------------------------
s, b = call("POST", "/offers/preview", cust3, {"module": "pass", "passPlanId": 1, "code": f"CPO{RUN}"})
check("typing the offer code still applies the offer (20% -> 755.20)", s == 200 and b["data"]["codeOfferId"] == OF and dec(b["data"]["totalAmount"]) == D("755.20"), b)
s, b = call("POST", "/offers/preview", cust3, {"module": "pass", "passPlanId": 1, "auto": True})
check("automatic offer still picked without a coupon", s == 200 and any(a["offerId"] == OF for a in b["data"]["applied"]), b)
s, b = call("POST", "/passes/purchase", cust3, {"passPlanId": 1, "validFrom": DAY2.isoformat(), "paymentMethod": "wallet", "offerIds": [OF]})
check("pass with the offer only: 755.20, offer row, no coupon row", s == 200 and dec(b["data"]["pass"]["totalAmount"]) == D("755.20")
      and one(f"SELECT COUNT(*) FROM TRN_CouponRedemption WHERE CustomerPassId={b['data']['pass']['customerPassId']}") == "0", (s, b))
r = sql(f"SELECT l.Description FROM TRN_InvoiceLine l JOIN TRN_Invoice i ON i.InvoiceId=l.InvoiceId WHERE i.CustomerPassId={b['data']['pass']['customerPassId']}")
check("offer invoice line text unchanged ('; offer CODE - Title')", r and f"; offer CPO{RUN} - E2E 20% pass offer" in r[0][0], r)
s, b = call("POST", "/passes/purchase", cust3, {"passPlanId": 1, "validFrom": DAY2.isoformat(), "paymentMethod": "wallet"})
check("a pass with neither costs the normal 944.00", s == 200 and dec(b["data"]["pass"]["totalAmount"]) == D("944.00"), (s, b))
s, b = call("POST", "/passes/order", cust3, {"passPlanId": 1})
check("pass order with neither: 944.00", s == 200 and dec(b["data"]["amount"]) == D("944.00"), b)
s, b = call("POST", "/bookings/quote", cust3, {"workspaceId": 10, "startUtc": iso(at(DAY2, 15)), "durationMinutes": 120})
check("booking quote unchanged (400 + 36 + 36 = 472)", s == 200 and dec(b["data"]["totalAmount"]) == D("472.00"), b)

# ---- cleanup ----------------------------------------------------------------------------------------------------------
for tok, cid in ((cust1, c1), (cust2, c2), (cust3, c3)):
    for bid in [r[0] for r in sql(f"SELECT BookingId FROM TRN_Booking WHERE CustomerId={cid} AND Status IN (1,2)")]:
        call("POST", f"/bookings/{bid}/cancel", tok, {"reason": "coupons e2e cleanup"})
for pid in [r[0] for r in sql(f"SELECT CustomerPassId FROM TRN_CustomerPass WHERE CustomerId IN ({c1},{c2},{c3}) AND Status IN (1,5)")]:
    call("POST", f"/passes/{pid}/cancel", admin)
purge()
left = one(f"SELECT COUNT(*) FROM MST_Coupon WHERE {TEST_COUPONS}")
check("cleanup: test coupons removed", left == "0", left)

passed = sum(1 for _, ok in results if ok)
print(f"\n{passed}/{len(results)} passed")
sys.exit(0 if passed == len(results) else 1)
