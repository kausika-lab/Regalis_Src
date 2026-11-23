"""End-to-end API checks for the offers kit (23 Nov 2026), against the LOCAL Dev stack.

  API  http://127.0.0.1:81  (local-dev\\api with the offers kit DLLs)
  DB   localhost\\SQLEXPRESS2025 / RegalisWorkSpace_Dev, migration 20261123120000_Offers applied
  Mail the API must send to a local SMTP sink: test purchases also trigger the Wi-Fi kit's emails.

Creates customers offers-e2e-*@example.test and a Sales user, test offers named "OFR-E2E <run>",
bookings on dedicated desks / cabins about ten days ahead, passes and a Virtual Office membership.
At the end: bookings are cancelled through the API, the test offers and their redemptions are
deleted with SQL, the Wi-Fi rows of the test customers are removed, and the users are soft-deleted.
Passes and invoices of the test customers stay (the app cannot delete them); they belong to
soft-deleted users. Nothing else is written with SQL except one test of a deactivated offer.
Env: REGALIS_ADMIN_EMAIL / REGALIS_ADMIN_PASSWORD.

usage: python -I e2e_offers.py
"""
import datetime as dt, json, os, random, subprocess, sys, time, urllib.request, urllib.error
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


def call(method, path, token=None, body=None, params=None, raw=False, files=None):
    while token in RENEWED:
        token = RENEWED[token]
    for _ in range(8):
        s, b = _call(method, path, token, body, params, raw, files)
        if s == 401 and token in CREDS:
            fresh = login(*CREDS[token])
            RENEWED[token] = fresh
            token = fresh
            continue
        if s != 429:
            return s, b
        time.sleep(15)
    return s, b


def _call(method, path, token, body, params, raw, files):
    url = API + path
    if params:
        url += "?" + "&".join(f"{k}={urllib.request.quote(str(v))}" for k, v in params.items() if v is not None)
    if files:
        boundary = "----offerse2e" + RUN
        name, content, ctype = files
        data = (f"--{boundary}\r\nContent-Disposition: form-data; name=\"file\"; filename=\"{name}\"\r\nContent-Type: {ctype}\r\n\r\n").encode() + content + f"\r\n--{boundary}--\r\n".encode()
        req = urllib.request.Request(url, data=data, method=method)
        req.add_header("Content-Type", "multipart/form-data; boundary=" + boundary)
    else:
        data = json.dumps(body).encode() if body is not None else None
        req = urllib.request.Request(url, data=data, method=method)
        req.add_header("Content-Type", "application/json")
    req.add_header("User-Agent", "Mozilla/5.0 (Linux; Android 14) Chrome/130.0 Mobile")
    if token:
        req.add_header("Authorization", "Bearer " + token)
    try:
        with urllib.request.urlopen(req, timeout=90) as r:
            content = r.read()
            if raw:
                return r.status, {"_bytes": content, "_type": r.headers.get("Content-Type", "")}
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
        s, b = call("POST", "/auth/login", body={"email": email_, "password": password, "portal": portal})
        if s == 200:
            CREDS[b["data"]["accessToken"]] = (email_, password, portal)
            return b["data"]["accessToken"]
        if s == 429:
            time.sleep(20)
            continue
        raise SystemExit(f"login {email_} failed: {s} {b}")
    raise SystemExit("login rate limited")


def r2(x):
    return D(str(x)).quantize(D("0.01"), rounding=ROUND_HALF_UP)


def gst(taxable):
    c = r2(D(str(taxable)) * 9 / 100)
    return c, c, D(str(taxable)) + c + c


def dec(v):
    return D(str(v)).quantize(D("0.01"))


IST = dt.timezone(dt.timedelta(hours=5, minutes=30))
now = dt.datetime.now(dt.timezone.utc)
today = now.astimezone(IST).date()


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


TEST_USERS = "Email LIKE 'offers-e2e-%@example.test'"
OFFER_NAMES = "OfferName LIKE 'OFR-E2E %'"


def purge():
    sql(f"""DECLARE @u TABLE (id bigint); INSERT INTO @u SELECT UserId FROM MST_User WHERE {TEST_USERS};
        DECLARE @o TABLE (id int); INSERT INTO @o SELECT OfferId FROM MST_Offer WHERE {OFFER_NAMES};
        DELETE FROM TRN_OfferRedemption WHERE OfferId IN (SELECT id FROM @o) OR CustomerId IN (SELECT id FROM @u);
        DELETE FROM TRN_OfferImage WHERE OfferId IN (SELECT id FROM @o);
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
        UPDATE TRN_Booking SET Status = 5, CancelledUtc = SYSUTCDATETIME(), CancellationReason = 'offers e2e cleanup' WHERE CustomerId IN (SELECT id FROM @u) AND Status IN (1, 2, 3);
        UPDATE MST_User SET IsDeleted = 1, IsActive = 0, Status = 2 WHERE {TEST_USERS} AND IsDeleted = 0;""")


purge()
admin = login(os.environ["REGALIS_ADMIN_EMAIL"], os.environ["REGALIS_ADMIN_PASSWORD"])


def make_user(tag, name, utype, role_code):
    pw = "Offers#E2e" + RUN + "x"
    role = one(f"SELECT RoleId FROM MST_Role WHERE RoleCode='{role_code}'")
    em = f"offers-e2e-{tag}{RUN}@example.test"
    s, b = call("POST", "/users", admin, {"fullName": name, "email": em, "mobileNumber": f"+9191{RUN}{random.randint(100, 999)}", "password": pw, "userType": utype,
                                          "roleIds": [int(role)], "defaultLocationId": 2})
    if s not in (200, 201):
        raise SystemExit(f"create user {tag}: {s} {b}")
    uid = b["data"]["userId"]
    sql(f"UPDATE MST_User SET MustChangePassword=0, Status=1, IsEmailVerified=1, IsMobileVerified=1, AddressLine1='1 Test Road', AddressLine2='Block A', City='Bengaluru', State='Karnataka',"
        f" Country='India', PostalCode='560098', Gender=1 WHERE UserId={uid}")
    return uid, em, pw


c1, c1_email, c1_pw = make_user("c1", "Offers E2E Meera", 1, "CUSTOMER")
c2, c2_email, c2_pw = make_user("c2", "Offers E2E Ravi", 1, "CUSTOMER")
sl, sl_email, sl_pw = make_user("sales", "Offers E2E Sales", 3, "SALES")
cust1 = login(c1_email, c1_pw, "customer")
cust2 = login(c2_email, c2_pw, "customer")
sales = login(sl_email, sl_pw)
for c in (c1, c2):
    s, b = call("POST", f"/wallet/{c}/adjust", admin, {"direction": "credit", "amount": 50000, "reason": "offers e2e"})
    if s != 200:
        raise SystemExit(f"wallet credit: {s} {b}")


def wallet(tok):
    return dec(call("GET", "/wallet/me", tok)[1]["data"]["balance"])


# ---- 1. menu, permissions ----------------------------------------------------------------------
s, b = call("GET", "/menus/my", admin)
check("admin sidebar: Sales > Offers (/admin/offers, BadgePercent)", "ADM_OFFERS" in json.dumps(b) and "/admin/offers" in json.dumps(b) and "BadgePercent" in json.dumps(b), s)
s, b = call("GET", "/menus/my", sales)
check("sales sidebar shows Offers", "ADM_OFFERS" in json.dumps(b), s)
s, _ = call("GET", "/admin/offers", sales)
check("sales (OFFER.VIEW): list opens", s == 200, s)
s, _ = call("POST", "/admin/offers", sales, {})
check("sales: creating refused (403, needs OFFER.MANAGE)", s == 403, s)
s, _ = call("GET", "/admin/offers", cust1)
check("customer: admin offers refused (403)", s == 403, s)
s, b = call("GET", "/menus/my", cust1)
check("customer sidebar unchanged (no admin Offers row)", "ADM_OFFERS" not in json.dumps(b), s)
s, b = call("POST", "/offers/preview", admin, {"module": "pass", "passPlanId": 1})
check("staff cannot preview / apply customer offers (403)", s == 403, s)

s, b = call("GET", "/admin/offers", admin)
base_summary = b["data"]["summary"]

# ---- 2. create, validate ------------------------------------------------------------------------
def offer(tag, **kw):
    body = {"offerName": f"OFR-E2E {RUN} {tag}", "title": kw.pop("title", f"E2E {tag}"), "description": kw.pop("description", None), "terms": kw.pop("terms", None),
            "code": kw.pop("code", None), "showInPortal": kw.pop("showInPortal", True), "discountType": kw.pop("type", 1), "discountValue": kw.pop("value"),
            "maxDiscountAmount": kw.pop("max", None), "minAmount": kw.pop("min", None), "modules": kw.pop("modules"), "workspaceTypeIds": kw.pop("types", []),
            "passPlanIds": kw.pop("passPlans", []), "membershipPlanIds": kw.pop("memberPlans", []), "startDate": kw.pop("start", today.isoformat()),
            "endDate": kw.pop("end", (today + dt.timedelta(days=30)).isoformat()), "isActive": kw.pop("active", True), "canCombine": kw.pop("combine", False),
            "sortOrder": kw.pop("sort", 100), "usesPerCustomer": kw.pop("per", None), "totalUses": kw.pop("total", None)}
    assert not kw, kw
    return call("POST", "/admin/offers", admin, body)


s, b = offer("bad", title="", value=150, modules=[], showInPortal=False, end=(today - dt.timedelta(days=1)).isoformat())
fields = {e.get("field") for e in b.get("errors", [])}
check("validation: title, percentage > 100, no module, hidden without code, past end date -> 400 with fields",
      s == 400 and {"title", "discountValue", "modules", "code", "endDate"} <= fields, (s, fields))
s, b = offer("bad2", value=10, modules=[2], passPlans=[21])
check("validation: a membership plan under Passes is refused", s == 400 and "passPlanIds" in json.dumps(b), (s, b))

CODE1, CODE4 = f"BK10E{RUN}", f"MEM500E{RUN}"
s, b = offer("O1 booking 10pc", title="10% off workspace bookings", description="Book any workspace and save.", terms="One use per customer.\nNot valid with cash bookings at the desk.",
             code=CODE1, value=10, max=50, modules=[1], combine=True, per=1, sort=10)
check("create: 10% booking offer, cap 50, one use per customer, code", s == 201 and b["data"]["status"] == "Active" and b["data"]["code"] == CODE1, (s, b))
O1 = b["data"]["offerId"]
s, b = offer("dup", code=CODE1.lower(), value=5, modules=[1])
check("validation: the same code twice (any case) refused", s == 400 and "code" in json.dumps(b), (s, b))
s, b = offer("O2 cabins flat 100", title="Rs 100 off cabins", type=2, value=100, min=500, modules=[1], types=[2], combine=True, sort=20)
O2 = b["data"]["offerId"]
check("create: fixed 100 off, cabins only, min 500", s == 201 and b["data"]["workspaceTypeIds"] == [2], (s, b))
s, b = offer("O3 single day pass 20pc", title="20% off the Single Day pass", value=20, modules=[2], passPlans=[1], sort=30)
O3 = b["data"]["offerId"]
s, b = offer("O4 membership hidden", title="Rs 500 off membership", type=2, value=500, modules=[3], code=CODE4, showInPortal=False, sort=40)
O4 = b["data"]["offerId"]
check("create: code-only membership offer (hidden from the portal)", s == 201 and b["data"]["showInPortal"] is False, (s, b))
s, b = offer("O5 scheduled", value=15, modules=[1], start=(today + dt.timedelta(days=3)).isoformat())
O5 = b["data"]["offerId"]
check("an offer starting later is Scheduled", b["data"]["status"] == "Scheduled", b)
s, b = offer("O6 inactive", value=12, modules=[1, 2, 3], active=False, code=f"OFFE6{RUN}")
O6 = b["data"]["offerId"]
check("an offer saved switched off is Inactive", b["data"]["status"] == "Inactive", b)
s, b = offer("O7 everything 5pc", title="5% off everything", value=5, modules=[1, 2, 3], sort=50)
O7 = b["data"]["offerId"]
s, b = offer("O8 flat 75", title="Rs 75 off", type=2, value=75, modules=[1], sort=60)
O8 = b["data"]["offerId"]

s, b = call("GET", "/admin/offers", admin)
sm = b["data"]["summary"]
check("overview: total +8, active +6, inactive +1, scheduled +1",
      sm["total"] - base_summary["total"] == 8 and sm["active"] - base_summary["active"] == 6 and sm["inactive"] - base_summary["inactive"] == 1 and sm["scheduled"] - base_summary["scheduled"] == 1, (sm, base_summary))
s, b = call("GET", "/admin/offers", admin, params={"search": CODE1})
check("search by code", s == 200 and [r["offerId"] for r in b["data"]["page"]["items"]] == [O1], b)
s, b = call("GET", "/admin/offers", admin, params={"search": f"OFR-E2E {RUN}", "module": 2, "status": "active"})
ids = {r["offerId"] for r in b["data"]["page"]["items"]}
check("filter: module Passes + active", ids == {O3, O7}, ids)
s, b = call("GET", "/admin/offers", admin, params={"search": f"OFR-E2E {RUN}", "status": "inactive"})
check("filter: inactive", [r["offerId"] for r in b["data"]["page"]["items"]] == [O6], b)
s, b = call("GET", "/admin/offers", admin, params={"search": f"OFR-E2E {RUN}", "sort": "priority", "pageSize": 3, "page": 2})
pg = b["data"]["page"]
check("sort by priority + pagination (page 2 of 3)", pg["totalCount"] == 8 and pg["totalPages"] == 3 and [r["offerId"] for r in pg["items"]][0] == O4, pg)

# ---- 3. what customers see ------------------------------------------------------------------------
def listed(tok, module):
    s, b = call("GET", "/offers", tok, params={"module": module})
    return [o["offerId"] for o in b["data"] if o["title"] and o["offerId"] in (O1, O2, O3, O4, O5, O6, O7, O8)], b


ids, b = listed(cust1, "booking")
check("My Bookings shows active booking offers in priority order (not scheduled / inactive)", ids == [O1, O2, O7, O8], ids)
o1 = next(o for o in b["data"] if o["offerId"] == O1)
check("customer offer card: title, description, terms, code, cap, validity, uses left",
      o1["code"] == CODE1 and o1["maxDiscountAmount"] == 50 and o1["terms"].startswith("One use") and o1["usesLeft"] == 1 and o1["endDate"], o1)
o2 = next(o for o in b["data"] if o["offerId"] == O2)
check("narrowed offer names what it applies to (Cabin)", o2["appliesTo"] == ["Cabin"], o2)
ids, _ = listed(cust1, "pass")
check("Passes shows only pass offers", ids == [O3, O7], ids)
ids, _ = listed(cust1, "membership")
check("Membership shows O7 only (the code-only offer is hidden)", ids == [O7], ids)

# ---- 4. preview: the same arithmetic as checkout -----------------------------------------------------
def preview(tok, **kw):
    return call("POST", "/offers/preview", tok, kw)


DESK, DESK2, DESK3, MDESK, CABIN = 9, 10, 11, 12, 13
s, b = preview(cust1, module="booking", workspaceId=DESK, baseAmounts=[400], offerIds=[O1])
p = b["data"]
check("preview 10% of 400 = 40 (cap 50): 360 + 32.40 + 32.40 = 424.80, was 472.00",
      s == 200 and dec(p["discountAmount"]) == D("40.00") and dec(p["taxableAmount"]) == D("360.00") and dec(p["cgstAmount"]) == D("32.40") and dec(p["totalAmount"]) == D("424.80") and dec(p["originalTotal"]) == D("472.00"), p)
s, b = preview(cust1, module="booking", workspaceId=DESK, baseAmounts=[1000], offerIds=[O1])
check("percentage capped at the maximum (10% of 1000 -> 50)", dec(b["data"]["discountAmount"]) == D("50.00"), b)
rows = {r["offer"]["offerId"]: r for r in p["offers"]}
check("cabin-only offer is not eligible on a desk, with the reason", rows[O2]["eligible"] is False and "type of workspace" in rows[O2]["reason"], rows[O2])
s, b = preview(cust1, module="booking", workspaceId=CABIN, baseAmounts=[400], offerIds=[O2])
check("minimum purchase: 400 < 500 -> refused with the minimum named", b["data"]["error"] and "500.00" in b["data"]["error"] and b["data"]["applied"] == [], b)
s, b = preview(cust1, module="booking", workspaceId=DESK, baseAmounts=[50], offerIds=[O8])
check("fixed 75 on a 50 purchase -> discount 50, never below zero", dec(b["data"]["discountAmount"]) == D("50.00") and dec(b["data"]["totalAmount"]) == D("0.00"), b)
s, b = preview(cust1, module="booking", workspaceId=DESK, baseAmounts=[400], offerIds=[O1, O7])
check("combining with an offer that does not allow it is refused", b["data"]["error"] and "cannot be combined" in b["data"]["error"], b)
s, b = preview(cust1, module="booking", workspaceId=DESK, baseAmounts=[400], offerIds=[O1, O1])
check("preview counts an offer picked twice once (40, not 80)", s == 200 and len(b["data"]["applied"]) == 1 and dec(b["data"]["discountAmount"]) == D("40.00"), (s, b))
s, b = preview(cust1, module="booking", workspaceId=DESK, baseAmounts=[400], code=CODE1.lower())
check("typed code (any case) finds the offer", b["data"]["codeOfferId"] == O1 and dec(b["data"]["discountAmount"]) == D("40.00"), b)
s, b = preview(cust1, module="booking", workspaceId=DESK, baseAmounts=[400], code="NOPE" + RUN)
check("unknown code -> clear message, price unchanged", "No offer matches" in b["data"]["error"] and dec(b["data"]["totalAmount"]) == D("472.00"), b)
s, b = preview(cust1, module="booking", workspaceId=DESK, baseAmounts=[400], offerIds=[O5])
check("scheduled offer cannot be applied (even by id)", bool(b["data"]["error"]), b)
s, b = preview(cust1, module="booking", workspaceId=DESK, baseAmounts=[400], offerIds=[O6])
check("inactive offer cannot be applied", bool(b["data"]["error"]), b)
s, b = preview(cust1, module="pass", passPlanId=1, offerIds=[O1])
check("a booking offer cannot be used on a pass", "cannot be used on passes" in (b["data"]["error"] or ""), b)

# ---- 5. hourly booking, wallet -----------------------------------------------------------------------
def hold(tok, ws, day, hh, minutes=120):
    for shift in range(0, 8):
        s, b = call("POST", "/bookings", tok, {"workspaceId": ws, "startUtc": iso(at(day, hh + shift)), "durationMinutes": minutes})
        if s in (200, 201):
            return b["data"]
        if s != 409:
            raise SystemExit(f"hold failed {s} {b}")
    raise SystemExit("no free slot")


def invoice_for_booking(bid):
    r = sql(f"SELECT TOP 1 i.SubTotal, i.DiscountAmount, i.TaxableAmount, i.CgstAmount, i.SgstAmount, i.TotalAmount, l.Description FROM TRN_Invoice i JOIN TRN_InvoiceLine l ON l.InvoiceId=i.InvoiceId WHERE i.BookingId={bid} ORDER BY i.InvoiceId")
    return r[0] if r else None


w0 = wallet(cust1)
h1 = hold(cust1, DESK, DAY1, 9)
s, b = call("POST", "/offers/bookings/apply", cust1, {"bookingIds": [h1["bookingId"]], "offerIds": [O1, O1]})
check("applying the same offer twice is refused", s == 422 and "twice" in json.dumps(b), (s, b))
s, b = call("POST", "/offers/bookings/apply", cust1, {"bookingIds": [h1["bookingId"]], "offerIds": [O1]})
check("apply 10% to the held booking -> total 424.80", s == 200 and dec(b["data"]["totalAmount"]) == D("424.80"), (s, b))
s, b = call("GET", f"/bookings/{h1['bookingId']}", cust1)
check("held booking now carries the discounted total", dec(b["data"]["totalAmount"]) == D("424.80") and dec(b["data"]["baseAmount"]) == D("400.00"), b)
s, b = call("POST", f"/bookings/{h1['bookingId']}/confirm", cust1, {"paymentMethod": "wallet"})
check("pay from the wallet", s == 200, (s, b))
check("wallet charged the discounted 424.80", w0 - wallet(cust1) == D("424.80"), w0 - wallet(cust1))
inv = invoice_for_booking(h1["bookingId"])
check("invoice: subtotal 400, discount 40, taxable 360, CGST 32.40, SGST 32.40, total 424.80",
      inv and [dec(x) for x in inv[:6]] == [D("400.00"), D("40.00"), D("360.00"), D("32.40"), D("32.40"), D("424.80")], inv)
check("invoice line names the offer", inv and CODE1 in inv[6], inv)
red = sql(f"SELECT OfferId, Module, BaseAmount, DiscountAmount, OfferLabel FROM TRN_OfferRedemption WHERE BookingId={h1['bookingId']}")
check("redemption recorded (booking, 400, 40)", red and red[0][0] == str(O1) and red[0][1] == "1" and dec(red[0][3]) == D("40.00"), red)
hist = one(f"SELECT COUNT(*) FROM TRN_BookingStatusHistory WHERE BookingId={h1['bookingId']} AND Reason LIKE 'Offer applied%'")
check("booking history shows the offer", hist == "1", hist)
s, b = preview(cust1, module="booking", workspaceId=DESK, baseAmounts=[400], offerIds=[O1])
check("one use per customer: second use refused", "already used" in (b["data"]["error"] or ""), b)
ids, lb = listed(cust1, "booking")
check("card shows 0 uses left for this customer", next(o for o in lb["data"] if o["offerId"] == O1)["usesLeft"] == 0, lb)
s, b = call("GET", f"/admin/offers/{O1}", admin)
check("admin: uses 1, discount given 40, cannot be deleted", b["data"]["uses"] == 1 and dec(b["data"]["discountGiven"]) == D("40.00") and b["data"]["canDelete"] is False, b["data"])
s, b = call("DELETE", f"/admin/offers/{O1}", admin)
check("deleting a used offer refused (OFFER_IN_USE)", s == 422 and "Deactivate" in json.dumps(b), (s, b))

# ---- 6. cabin, two combinable offers, online payment ----------------------------------------------------
h2 = hold(cust2, CABIN, DAY1, 9)
s, b = call("POST", "/offers/bookings/apply", cust2, {"bookingIds": [h2["bookingId"]], "offerIds": [O1, O2]})
# 10% of 800 = 80 -> cap 50; + 100 = 150; 650 + 58.50 + 58.50 = 767.00
check("two combinable offers on a cabin: 50 + 100 off 800 -> 767.00", s == 200 and dec(b["data"]["discountAmount"]) == D("150.00") and dec(b["data"]["totalAmount"]) == D("767.00"), (s, b))
s, b = call("POST", "/payments/order", cust2, {"bookingId": h2["bookingId"]})
check("online order is for the discounted 767.00", s == 200 and dec(b["data"]["amount"]) == D("767.00"), (s, b))
order = b["data"]["gatewayOrderId"]
s, b2 = call("POST", "/offers/bookings/apply", cust2, {"bookingIds": [h2["bookingId"]], "offerIds": [O1]})
check("offers cannot change once payment has started", s == 422 and "already started" in json.dumps(b2), (s, b2))
s, pay = call("POST", "/payments/sandbox-complete", cust2, {"gatewayOrderId": order})
s, b = call("POST", f"/bookings/{h2['bookingId']}/confirm", cust2, {"paymentMethod": "gateway", "gatewayOrderId": order, "gatewayPaymentId": pay["data"]["gatewayPaymentId"], "gatewaySignature": pay["data"]["gatewaySignature"]})
check("online payment confirms the booking at 767.00", s == 200 and dec(b["data"]["booking"]["totalAmount"]) == D("767.00"), (s, b))
inv = invoice_for_booking(h2["bookingId"])
check("invoice: discount 150, taxable 650, GST 58.50 + 58.50", inv and [dec(x) for x in inv[1:6]] == [D("150.00"), D("650.00"), D("58.50"), D("58.50"), D("767.00")], inv)
red = sql(f"SELECT OfferId, DiscountAmount FROM TRN_OfferRedemption WHERE BookingId={h2['bookingId']} ORDER BY OfferId")
check("one redemption per offer (50 and 100)", sorted((int(r[0]), dec(r[1])) for r in red) == sorted([(O1, D("50.00")), (O2, D("100.00"))]), red)

# ---- 7. re-check before money moves --------------------------------------------------------------------
h3 = hold(cust2, DESK2, DAY1, 9, 60)
s, b = call("POST", "/offers/bookings/apply", cust2, {"bookingIds": [h3["bookingId"]], "offerIds": [O7]})
check("5% on 200 -> 236.00 - 11.80 = 224.20", s == 200 and dec(b["data"]["totalAmount"]) == D("224.20"), b)
s, _ = call("POST", f"/admin/offers/{O7}/deactivate", admin)
check("admin deactivates the offer", s == 200, s)
s, b = call("POST", f"/bookings/{h3['bookingId']}/confirm", cust2, {"paymentMethod": "wallet"})
check("wallet payment refused once the offer is off (OFFER_NOT_AVAILABLE)", s == 422 and b.get("errors", [{}])[0].get("code") == "OFFER_NOT_AVAILABLE", (s, b))
s, b = call("POST", "/payments/order", cust2, {"bookingId": h3["bookingId"]})
check("online order refused too", s == 422 and "no longer available" in json.dumps(b), (s, b))
ids, _ = listed(cust2, "booking")
check("deactivated offer disappears from the portal", O7 not in ids, ids)
s, b = call("POST", f"/admin/offers/{O7}/activate", admin)
check("admin re-activates it", s == 200 and b["data"]["status"] == "Active", b)
s, b = call("POST", "/offers/bookings/apply", cust2, {"bookingIds": [h3["bookingId"]], "offerIds": []})
check("removing offers puts the original price back (236.00)", s == 200 and dec(b["data"]["totalAmount"]) == D("236.00") and one(f"SELECT COUNT(*) FROM TRN_OfferRedemption WHERE BookingId={h3['bookingId']}") == "0", b)
s, b = call("POST", f"/bookings/{h3['bookingId']}/confirm", cust2, {"paymentMethod": "wallet"})
check("booking without an offer pays the normal 236.00", s == 200 and dec(b["data"]["booking"]["totalAmount"]) == D("236.00"), (s, b))
inv = invoice_for_booking(h3["bookingId"])
check("no-offer invoice unchanged (discount 0, GST on 200)", inv and [dec(x) for x in inv[:6]] == [D("200.00"), D("0.00"), D("200.00"), D("18.00"), D("18.00"), D("236.00")] and "offer" not in inv[6], inv)
s, b = call("POST", "/offers/bookings/apply", cust1, {"bookingIds": [h3["bookingId"]], "offerIds": [O7]})
check("a customer cannot touch someone else's booking", s == 404, (s, b))

# ---- 8. multi-day: one purchase, discount spread over the days --------------------------------------------
m1 = hold(cust2, DESK3, DAY1, 10, 60)
m2 = hold(cust2, DESK3, DAY2, 10, 60)
s, b = call("POST", "/offers/bookings/apply", cust2, {"bookingIds": [m1["bookingId"], m2["bookingId"]], "offerIds": [O8]})
items = {i["bookingId"]: i for i in b["data"]["bookings"]} if s == 200 else {}
check("fixed 75 over two days -> 37.50 each", s == 200 and all(dec(i["discountAmount"]) == D("37.50") for i in items.values()), (s, b))
for m in (m1, m2):
    call("POST", f"/bookings/{m['bookingId']}/confirm", cust2, {"paymentMethod": "wallet"})
s, b = call("GET", f"/admin/offers/{O8}", admin)
check("a multi-day booking counts as one use", b["data"]["uses"] == 1 and dec(b["data"]["discountGiven"]) == D("75.00"), b["data"])

# ---- 9. monthly booking, pay later -------------------------------------------------------------------------
s, b = call("POST", "/bookings/monthly", cust1, {"workspaceId": MDESK, "startDate": open_day(20).isoformat(), "months": 1})
check("monthly hold on a dedicated desk", s in (200, 201), (s, b))
if s in (200, 201):
    mh = b["data"]
    s, b = call("POST", "/offers/bookings/apply", cust1, {"bookingIds": [mh["bookingId"]], "offerIds": [O7]})
    check("5% off a 2000 month -> 1900 + 171 + 171 = 2242.00", s == 200 and dec(b["data"]["totalAmount"]) == D("2242.00"), (s, b))
    s, b = call("POST", f"/bookings/{mh['bookingId']}/confirm", cust1, {"paymentMethod": "paylater"})
    check("pay later: confirmed, 2242.00 due at the desk", s == 200 and b["data"]["isPayLater"] and dec(b["data"]["booking"]["totalAmount"]) == D("2242.00"), (s, b))
    inv = invoice_for_booking(mh["bookingId"])
    check("monthly invoice shows the discount (pending)", inv and dec(inv[1]) == D("100.00") and dec(inv[5]) == D("2242.00"), inv)

# ---- 10. passes ---------------------------------------------------------------------------------------------
s, b = preview(cust1, module="pass", passPlanId=1, offerIds=[O3])
check("pass preview: 20% of 800 = 160 -> 640 + 57.60 + 57.60 = 755.20", dec(b["data"]["totalAmount"]) == D("755.20") and dec(b["data"]["originalTotal"]) == D("944.00"), b)
s, b = preview(cust1, module="pass", passPlanId=2, offerIds=[O3])
check("pass offer narrowed to the Single Day plan: not on the 5-day pack", "does not apply to this plan" in (b["data"]["error"] or ""), b)
w0 = wallet(cust1)
s, b = call("POST", "/passes/purchase", cust1, {"passPlanId": 1, "validFrom": DAY2.isoformat(), "paymentMethod": "wallet", "offerIds": [O3]})
check("buy a day pass from the wallet with the offer", s == 200 and dec(b["data"]["pass"]["totalAmount"]) == D("755.20") and dec(b["data"]["pass"]["price"]) == D("640.00"), (s, b))
pass1 = b["data"]["pass"]["customerPassId"] if s == 200 else 0
check("wallet charged 755.20", w0 - wallet(cust1) == D("755.20"), w0 - wallet(cust1))
r = sql(f"SELECT SubTotal, DiscountAmount, TaxableAmount, CgstAmount, SgstAmount, TotalAmount FROM TRN_Invoice WHERE CustomerPassId={pass1}")
check("pass invoice: 800 - 160 = 640, GST 57.60 x2, 755.20", r and [dec(x) for x in r[0]] == [D("800.00"), D("160.00"), D("640.00"), D("57.60"), D("57.60"), D("755.20")], r)
check("pass redemption tied to the pass", one(f"SELECT COUNT(*) FROM TRN_OfferRedemption WHERE CustomerPassId={pass1} AND OfferId={O3}") == "1")
s, b = call("POST", "/passes/purchase", cust1, {"passPlanId": 2, "validFrom": DAY2.isoformat(), "paymentMethod": "wallet", "offerIds": [O3]})
check("server refuses the offer on an excluded plan", s == 422 and "does not apply" in json.dumps(b), (s, b))
s, b = call("POST", "/passes/order", cust2, {"passPlanId": 1, "offerIds": [O3]})
check("online pass order priced with the offer (755.20)", s == 200 and dec(b["data"]["amount"]) == D("755.20"), (s, b))
order = b["data"]["gatewayOrderId"]
s, pay = call("POST", "/payments/sandbox-complete", cust2, {"gatewayOrderId": order})
s, b = call("POST", "/passes/purchase", cust2, {"passPlanId": 1, "validFrom": DAY2.isoformat(), "paymentMethod": "gateway", "gatewayOrderId": order,
                                                "gatewayPaymentId": pay["data"]["gatewayPaymentId"], "gatewaySignature": pay["data"]["gatewaySignature"]})
check("online purchase completes at the ordered price", s == 200 and dec(b["data"]["pass"]["totalAmount"]) == D("755.20"), (s, b))
pass2 = b["data"]["pass"]["customerPassId"] if s == 200 else 0
check("order-time redemption now points at the pass", one(f"SELECT COUNT(*) FROM TRN_OfferRedemption WHERE CustomerPassId={pass2} AND PaymentId IS NOT NULL") == "1")
s, b = call("POST", "/passes/purchase", cust2, {"passPlanId": 1, "validFrom": DAY2.isoformat(), "paymentMethod": "wallet"})
check("a pass without an offer costs the normal 944.00", s == 200 and dec(b["data"]["pass"]["totalAmount"]) == D("944.00"), (s, b))
r = sql(f"SELECT SubTotal, DiscountAmount, TaxableAmount, TotalAmount FROM TRN_Invoice WHERE CustomerPassId={b['data']['pass']['customerPassId']}")
check("no-offer pass invoice unchanged", r and [dec(x) for x in r[0]] == [D("800.00"), D("0.00"), D("800.00"), D("944.00")], r)

# ---- 11. membership, code-only offer -------------------------------------------------------------------------
s, b = preview(cust1, module="membership", passPlanId=21, code=CODE4)
check("hidden membership offer found by its code: 2500 - 500 -> 2360.00", b["data"]["codeOfferId"] == O4 and dec(b["data"]["totalAmount"]) == D("2360.00"), b)
s, b = preview(cust1, module="pass", passPlanId=1, code=CODE4)
check("membership code does not work on a pass", "cannot be used on passes" in (b["data"]["error"] or ""), b)
s, b = call("POST", "/passes/purchase", cust1, {"passPlanId": 21, "validFrom": today.isoformat(), "paymentMethod": "paylater", "offerIds": [O4]})
check("Virtual Office membership, pay later, with the code offer: 2360.00 awaiting payment", s == 200 and dec(b["data"]["pass"]["totalAmount"]) == D("2360.00") and b["data"]["pass"]["status"] == 5, (s, b))
vo = b["data"]["pass"]["customerPassId"] if s == 200 else 0

# ---- 12. admin monitoring, delete, uses given back ------------------------------------------------------------
s, b = call("GET", f"/admin/offers/{O1}/redemptions", admin)
st = sorted(r["state"] for r in b["data"])
check("uses list: O1 used twice (two customers), with references", s == 200 and st == ["Used", "Used"] and all(r["reference"].startswith("REG") or r["reference"] for r in b["data"]), b)
s, b = call("DELETE", f"/admin/offers/{O5}", admin)
check("an unused offer can be deleted", s == 200, (s, b))
s, b = offer("O6 again", value=8, modules=[1], code=f"OFFE6{RUN}", active=False)
check("an active code stays unique (O6 still holds it)", s == 400, (s, b))
call("DELETE", f"/admin/offers/{O6}", admin)
s, b = offer("O6 again", value=8, modules=[1], code=f"OFFE6{RUN}", active=False)
check("a deleted offer's code can be reused", s == 201, (s, b))

s, b = call("POST", f"/bookings/{h1['bookingId']}/cancel", cust1, {"reason": "offers e2e"})
check("customer cancels the discounted booking", s == 200, (s, b))
s, b = preview(cust1, module="booking", workspaceId=DESK, baseAmounts=[400], offerIds=[O1])
check("cancelled booking gives the one-per-customer use back", not b["data"]["error"], b)
s, b = call("GET", f"/admin/offers/{O1}/redemptions", admin)
check("uses list shows the cancelled one as Released", sorted(r["state"] for r in b["data"]) == ["Released", "Used"], b)

# ---- 13. picture ----------------------------------------------------------------------------------------------
PNG = bytes.fromhex("89504E470D0A1A0A0000000D49484452000000010000000108060000001F15C4890000000D4944415478DA63F8CFC0F01F0005000201E2E2A1F20000000049454E44AE426082")
s, b = call("POST", f"/admin/offers/{O3}/image", admin, files=("banner.png", PNG, "image/png"))
check("upload a PNG picture", s == 200 and b["data"]["hasImage"] and b["data"]["imageVersion"] == 1, (s, b))
s, b = call("GET", f"/offers/{O3}/image", None, raw=True)
check("picture served without sign-in, as image/png", s == 200 and b["_type"].startswith("image/png") and b["_bytes"] == PNG, (s, b.get("_type")))
s, b = call("POST", f"/admin/offers/{O3}/image", admin, files=("evil.png", b"MZ" + b"\x00" * 64, "image/png"))
check("a renamed non-image is refused", s == 422, (s, b))
ids, b = listed(cust1, "pass")
check("portal card carries the picture URL", next(o for o in b["data"] if o["offerId"] == O3)["imageUrl"] == f"/api/offers/{O3}/image?v=1", b)

# ---- 14. existing endpoints still answer ---------------------------------------------------------------------
for path, prm, tok in [("/bookings", None, admin), ("/passes", None, admin), ("/bookings/my", {"tab": "upcoming"}, cust1), ("/passes/my", None, cust1),
                       ("/pass-plans/active", {"type": 2}, cust1), ("/passes/purchase-options", {"type": 1}, cust1)]:
    s, _ = call("GET", path, tok, params=prm)
    check(f"existing GET {path} still 200", s == 200, s)
s, b = call("POST", "/bookings/quote", cust1, {"workspaceId": DESK, "startUtc": iso(at(DAY2, 15)), "durationMinutes": 120})
check("quote unchanged (400 + 36 + 36 = 472)", s == 200 and dec(b["data"]["totalAmount"]) == D("472.00"), b)
s, b = call("POST", "/passes/order", cust1, {"passPlanId": 1})
check("pass order without offers: normal 944.00", s == 200 and dec(b["data"]["amount"]) == D("944.00"), b)

# ---- cleanup ----------------------------------------------------------------------------------------------------
for tok, cid in ((cust1, c1), (cust2, c2)):
    for bid in [r[0] for r in sql(f"SELECT BookingId FROM TRN_Booking WHERE CustomerId={cid} AND Status IN (1,2)")]:
        call("POST", f"/bookings/{bid}/cancel", tok, {"reason": "offers e2e cleanup"})
for pid in [r[0] for r in sql(f"SELECT CustomerPassId FROM TRN_CustomerPass WHERE CustomerId IN ({c1},{c2}) AND Status IN (1,5)")]:
    call("POST", f"/passes/{pid}/cancel", admin)
purge()
left = one(f"SELECT COUNT(*) FROM MST_Offer WHERE {OFFER_NAMES}")
check("cleanup: test offers removed", left == "0", left)

passed = sum(1 for _, ok in results if ok)
print(f"\n{passed}/{len(results)} passed")
sys.exit(0 if passed == len(results) else 1)
