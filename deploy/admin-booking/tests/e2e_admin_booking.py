"""End-to-end API checks for the admin-booking kit (27 Nov 2026), against the LOCAL Dev stack.

  API  http://127.0.0.1:81  (D:\\Regalis\\local-dev\\api with the admin-booking DLLs)
  DB   localhost\\SQLEXPRESS2025 / RegalisWorkSpace_Dev (no migration: the kit changes no table)
  Mail the API must send to a local SMTP sink: confirmations trigger notification and Wi-Fi emails.

Creates customers adm-bk-a<run>@example.test (PayLaterAllowed off, to prove staff bypass it),
adm-bk-b<run>@example.test (an ordinary customer, for the regression checks) and a Finance staff
user adm-bk-fin<run>@example.test (no BOOKING.CREATE). Books meeting rooms and a desk about two
weeks ahead, a Virtual Office, and a test coupon ADMBK<run>.

Booking.MaxMinutes is raised to 780 through the settings API for the multi-day section only (a
whole working day on Dev is 08:00-21:00 = 780 minutes, above the Dev value 480) and put back
afterwards, whatever happens.

Clean-up (unless --keep): bookings and passes cancelled through the API, the coupon and its
redemptions deleted with SQL, Wi-Fi rows of the test customers removed, users soft-deleted.
Invoices and receipts of the test customers stay (the app cannot delete them).
Env: REGALIS_ADMIN_EMAIL / REGALIS_ADMIN_PASSWORD.

usage: python -I e2e_admin_booking.py [--keep]
"""
import datetime as dt, json, os, random, subprocess, sys, time, urllib.request, urllib.error
from decimal import Decimal as D, ROUND_HALF_UP

API = os.environ.get("REGALIS_API", "http://127.0.0.1:81/api")
SQL = ["sqlcmd", "-S", r"localhost\SQLEXPRESS2025", "-E", "-C", "-d", "RegalisWorkSpace_Dev", "-I", "-h", "-1", "-W", "-s", "|"]
RUN = str(random.randint(10000, 99999))
KEEP = "--keep" in sys.argv
results = []


def check(name, ok, detail=""):
    results.append((name, bool(ok)))
    print(("PASS " if ok else "FAIL ") + name + (f"  -- {str(detail)[:600]}" if detail and not ok else ""), flush=True)


def sql(q):
    out = subprocess.run(SQL + ["-b", "-Q", "SET NOCOUNT ON;" + q], capture_output=True, text=True)
    if out.returncode != 0:
        raise SystemExit("SQL failed: " + q[:200] + " :: " + out.stdout + out.stderr)
    return [l.split("|") for l in out.stdout.strip().splitlines() if l.strip()]


def one(q):
    r = sql(q)
    return r[0][0] if r else None


CREDS = {}


def call(method, path, token=None, body=None, params=None):
    for _ in range(8):
        s, b = _call(method, path, token, body, params)
        if s == 401 and token in CREDS:
            token = login(*CREDS[token])
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
            CREDS[b["data"]["accessToken"]] = (email_, password, portal)
            return b["data"]["accessToken"]
        if s == 429:
            time.sleep(20)
            continue
        raise SystemExit(f"login {email_} failed: {s} {b}")
    raise SystemExit("login rate limited")


def dec(v):
    return D(str(v)).quantize(D("0.01"))


def r2(x):
    return D(str(x)).quantize(D("0.01"), rounding=ROUND_HALF_UP)


def gst(base):
    c = r2(D(str(base)) * 9 / 100)
    return D(str(base)) + c + c


def errs(b):
    return json.dumps(b.get("errors") or []) if isinstance(b, dict) else ""


IST = dt.timezone(dt.timedelta(hours=5, minutes=30))
today = dt.datetime.now(dt.timezone.utc).astimezone(IST).date()


def open_day(after):
    d = today + dt.timedelta(days=after)
    while d.weekday() == 6:
        d += dt.timedelta(days=1)
    return d


DAY1 = open_day(12 + random.randint(0, 6))
DAY2 = open_day((DAY1 - today).days + 1)
DAY3 = open_day((DAY2 - today).days + 1)
SUNDAY = DAY1 + dt.timedelta(days=(6 - DAY1.weekday()) % 7 or 7)


def at(d, hh, mm=0):
    return dt.datetime(d.year, d.month, d.day, hh, mm, tzinfo=IST).astimezone(dt.timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ")


TEST_USERS = "Email LIKE 'adm-bk-%@example.test'"
TEST_COUPONS = "Code LIKE 'ADMBK%'"
CODE = f"ADMBK{RUN}"
MEETING, DESK_TYPE, LOC = 3, 1, 2


def purge_user_rows(where_users):
    sql(f"""DECLARE @u TABLE (id bigint); INSERT INTO @u SELECT UserId FROM MST_User WHERE {where_users};
        DECLARE @c TABLE (id int); INSERT INTO @c SELECT CouponId FROM MST_Coupon WHERE {TEST_COUPONS};
        DELETE FROM TRN_CouponRedemption WHERE CouponId IN (SELECT id FROM @c) OR CustomerId IN (SELECT id FROM @u);
        DELETE FROM MST_CouponModule WHERE CouponId IN (SELECT id FROM @c);
        DELETE FROM MST_Coupon WHERE CouponId IN (SELECT id FROM @c);
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
        UPDATE TRN_Booking SET Status = 5, CancelledUtc = SYSUTCDATETIME(), CancellationReason = 'admin-booking e2e cleanup' WHERE CustomerId IN (SELECT id FROM @u) AND Status IN (1, 2, 3);
        UPDATE TRN_CustomerPass SET Status = 4 WHERE CustomerId IN (SELECT id FROM @u) AND Status IN (1, 5);
        UPDATE MST_User SET IsDeleted = 1, IsActive = 0, Status = 2 WHERE UserId IN (SELECT id FROM @u) AND IsDeleted = 0;""")


# Earlier e2e runs only (the -e2e- tag); the demo accounts kept with --keep are left alone.
purge_user_rows("Email LIKE 'adm-bk-%-e2e-%@example.test'")
admin = login(os.environ["REGALIS_ADMIN_EMAIL"], os.environ["REGALIS_ADMIN_PASSWORD"])
ADMIN_ID = int(one(f"SELECT UserId FROM MST_User WHERE Email = '{os.environ['REGALIS_ADMIN_EMAIL']}'"))
TAG = "demo" if KEEP else "e2e"


def make_user(tag, name, utype, role_code, pay_later=1):
    pw = "AdmBk#E2e" + RUN + "x"
    role = one(f"SELECT RoleId FROM MST_Role WHERE RoleCode='{role_code}'")
    em = f"adm-bk-{tag}-{TAG}-{RUN}@example.test"
    s, b = call("POST", "/users", admin, {"fullName": name, "email": em, "mobileNumber": f"+9193{RUN}{random.randint(100, 999)}", "password": pw, "userType": utype,
                                          "roleIds": [int(role)], "defaultLocationId": LOC})
    if s not in (200, 201):
        raise SystemExit(f"create user {tag}: {s} {b}")
    uid = b["data"]["userId"]
    sql(f"UPDATE MST_User SET MustChangePassword=0, Status=1, IsEmailVerified=1, IsMobileVerified=1, AddressLine1='1 Test Road', AddressLine2='Block A', City='Bengaluru', State='Karnataka',"
        f" Country='India', PostalCode='560098', Gender=1, PayLaterAllowed={pay_later} WHERE UserId={uid}")
    return uid, em, pw


ca, ca_email, ca_pw = make_user("a", f"AdminBooking Asha {RUN}", 1, "CUSTOMER", pay_later=0)
cb, cb_email, cb_pw = make_user("b", f"AdminBooking Kiran {RUN}", 1, "CUSTOMER")
fi, fi_email, fi_pw = make_user("fin", f"AdminBooking Finance {RUN}", 3, "FINANCE")
custA = login(ca_email, ca_pw, "customer")
custB = login(cb_email, cb_pw, "customer")
finance = login(fi_email, fi_pw)
s, b = call("POST", f"/wallet/{cb}/adjust", admin, {"direction": "credit", "amount": 20000, "reason": "admin-booking e2e"})
if s != 200:
    raise SystemExit(f"wallet credit: {s} {b}")


def money_rows(cid):
    """Payment-side rows of a customer: payments, invoice receipts, cash collections, wallet ledger."""
    r = sql(f"""SELECT (SELECT COUNT(*) FROM TRN_Payment WHERE CustomerId={cid}),
                       (SELECT COUNT(*) FROM TRN_InvoicePayment WHERE CustomerId={cid}),
                       (SELECT COUNT(*) FROM TRN_CashCollection WHERE CustomerId={cid}),
                       (SELECT COUNT(*) FROM TRN_WalletTransaction WHERE CustomerId={cid})""")
    return tuple(int(x) for x in r[0])


def free_ws(type_id, start, minutes):
    s, b = call("GET", "/availability/workspaces", admin, params={"locationId": LOC, "workspaceTypeId": type_id, "startUtc": start, "durationMinutes": minutes, "includeBusy": "true"})
    return [w for w in (b.get("data") or []) if w.get("isAvailable") is not False]


def ob(path):
    return f"/admin/on-behalf/{ca}{path}"


made_bookings, made_passes = [], []
A0 = money_rows(ca)

# ---- 1. who may book for a customer ---------------------------------------------------------------
st = at(DAY1, 10)
rooms = free_ws(MEETING, st, 60)
check("a free meeting room on day 1 at 10:00", len(rooms) >= 1, rooms)
ROOM = rooms[0]["workspaceId"]
body = {"workspaceId": ROOM, "startUtc": st, "durationMinutes": 60}
s, b = call("POST", f"/admin/on-behalf/{cb}/bookings", custA, body)
check("a customer cannot book for another customer (403)", s == 403, (s, b))
s, b = call("POST", ob("/bookings"), custA, body)
check("a customer cannot use the admin screen even for themselves (403)", s == 403, (s, b))
s, b = call("POST", ob("/bookings"), finance, body)
check("staff without BOOKING.CREATE refused (403)", s == 403, (s, b))
s, b = call("POST", ob("/virtual-office"), custA, {"passPlanId": 21, "validFrom": DAY1.isoformat()})
check("a customer cannot reserve a Virtual Office here (403)", s == 403, (s, b))
s, b = call("POST", "/admin/on-behalf/999999999/bookings", admin, body)
check("unknown customer refused (422 CUSTOMER_NOT_FOUND)", s == 422 and "CUSTOMER_NOT_FOUND" in errs(b), (s, b))
s, b = call("POST", f"/admin/on-behalf/{ADMIN_ID}/bookings", admin, body)
check("a staff user cannot be booked for (422)", s == 422 and "CUSTOMER_NOT_FOUND" in errs(b), (s, b))
s, b = call("POST", "/bookings", custB, {**body, "customerId": ca, "startUtc": at(DAY1, 18)})
check("customer endpoint ignores a customerId in the body: the booking is the caller's own",
      s == 201 and b["data"]["customerId"] == cb and b["data"]["bookingSource"] == 1, (s, b))
if s == 201:
    call("POST", f"/bookings/{b['data']['bookingId']}/cancel", custB, {"reason": "e2e"})

# ---- 2. single slot: same rules as the customer screen ----------------------------------------------
s, q = call("POST", "/bookings/quote", admin, body)
check("quote for staff (existing endpoint)", s == 200, (s, q))
s, b = call("POST", ob("/bookings"), admin, body)
h1 = b.get("data") or {}
check("hold for the customer: 201, the customer's booking, Pending, source Front desk (2)",
      s == 201 and h1.get("customerId") == ca and h1.get("status") == 1 and h1.get("bookingSource") == 2, (s, b))
check("hold priced exactly as the quote (GST 9+9)", h1 and dec(h1["totalAmount"]) == dec(q["data"]["totalAmount"]) == gst(q["data"]["baseAmount"]), (h1, q))
made_bookings.append(h1.get("bookingId"))
row = sql(f"SELECT CustomerId, CreatedByUserId, CreatedBy, BookingSource FROM TRN_Booking WHERE BookingId={h1['bookingId']}")[0]
check("DB: CustomerId = customer, CreatedByUserId = staff, audit CreatedBy = staff, source 2",
      [int(x) for x in row] == [ca, ADMIN_ID, ADMIN_ID, 2], row)
hist = sql(f"SELECT ChangedByUserId FROM TRN_BookingStatusHistory WHERE BookingId={h1['bookingId']}")
check("status history: hold row written by the staff user", [int(r[0]) for r in hist] == [ADMIN_ID], hist)
audit = one(f"SELECT TOP 1 UserId FROM TRN_AuditLog WHERE EntityName='Booking' AND EntityId='{h1['bookingId']}' AND Action='CREATE'")
check("audit trail: CREATE Booking by the staff user", audit is not None and int(audit) == ADMIN_ID, audit)
s, b = call("POST", ob("/bookings"), admin, body)
check("same slot again: 409 (one calendar, range locks)", s == 409, (s, b))
s, b = call("POST", ob("/bookings"), admin, {**body, "startUtc": at(today - dt.timedelta(days=1), 10)})
check("past start refused like the customer screen (422)", s == 422 and "future" in json.dumps(b), (s, b))
s, b = call("POST", ob("/bookings"), admin, {**body, "startUtc": at(SUNDAY, 10)})
check("closed day (Sunday) refused (422 location closed)", s == 422 and "closed" in json.dumps(b).lower(), (s, b))
s, b = call("POST", ob("/bookings"), admin, {**body, "startUtc": at(DAY1, 21)})
check("outside working hours refused (422)", s == 422 and "working hours" in json.dumps(b), (s, b))
s, b = call("POST", ob("/bookings"), admin, {**body, "durationMinutes": 45, "startUtc": at(DAY1, 15)})
check("duration not a multiple of the slot refused (422)", s == 422, (s, b))
s, b = call("POST", ob("/bookings"), admin, {**body, "durationMinutes": 0})
check("validator runs: duration 0 is a 400 with the field", s == 400 and "durationMinutes" in errs(b), (s, b))

s, b = call("POST", ob(f"/bookings/{h1['bookingId']}/confirm"), admin, {})
c1 = b.get("data") or {}
bk = c1.get("booking") or {}
check("confirm: 200, Confirmed, pay later, nothing paid, no payment id",
      s == 200 and bk.get("status") == 2 and bk.get("isPayLater") is True and dec(bk.get("amountPaid", 1)) == 0 and c1.get("paymentId") == 0 and c1.get("paymentNumber") == "", (s, b))
check("confirm: payment status Pending (1) and money due from the start", bk.get("paymentStatus") == 1 and bk.get("paymentDueUtc"), bk)
check("customer A has PayLaterAllowed off, yet staff confirmed without payment (eligibility not applied to staff)",
      one(f"SELECT CAST(PayLaterAllowed AS int) FROM MST_User WHERE UserId={ca}") == "0" and s == 200)
inv = sql(f"SELECT InvoiceId, Status, CAST(PaidAmount AS decimal(12,2)), ISNULL(PaymentId, 0), CAST(TotalAmount AS decimal(12,2)), CustomerId, CustomerName FROM TRN_Invoice WHERE BookingId={h1['bookingId']}")
check("one GST invoice, Pending (1), nothing paid, no payment linked, total = booking, customer snapshot",
      len(inv) == 1 and inv[0][1] == "1" and dec(inv[0][2]) == 0 and inv[0][3] == "0" and dec(inv[0][4]) == dec(bk.get("totalAmount", 0)) and int(inv[0][5]) == ca and inv[0][6].startswith("AdminBooking Asha"), inv)
INV1 = int(inv[0][0]) if inv else 0
check("no payment, receipt, cash or wallet row for the customer", money_rows(ca) == A0, (money_rows(ca), A0))
hist = sql(f"SELECT ToStatus, ChangedByUserId FROM TRN_BookingStatusHistory WHERE BookingId={h1['bookingId']} ORDER BY BookingStatusHistoryId")
check("status history: confirmation written by the staff user", [(int(a), int(c)) for a, c in hist] == [(1, ADMIN_ID), (2, ADMIN_ID)], hist)
note = sql(f"SELECT Title, Message FROM TRN_Notification WHERE UserId={ca} AND ReferenceType='Booking' AND ReferenceId={h1['bookingId']}")
check("customer notified exactly like a pay-later booking (Booking confirmed + pay at the front desk)",
      len(note) == 1 and note[0][0] == "Booking confirmed" and "front desk" in note[0][1], note)
tok = one(f"SELECT COUNT(*) FROM TRN_AccessToken WHERE BookingId={h1['bookingId']} AND Status=1")
check("QR access token issued for the booking (as for a customer pay-later booking)", tok == "1", tok)
s, b = call("POST", ob(f"/bookings/{h1['bookingId']}/confirm"), admin, {})
check("confirming twice refused (422 not awaiting payment)", s == 422, (s, b))
s, b = call("POST", f"/admin/on-behalf/{cb}/bookings/{h1['bookingId']}/confirm", admin, {})
check("confirm under another customer: 404", s == 404, (s, b))

# The customer's own pay-later gate is unchanged.
st2 = at(DAY1, 12)
s, own = call("POST", "/bookings", custA, {"workspaceId": ROOM, "startUtc": st2, "durationMinutes": 60})
check("customer A holds a slot on the customer screen (source 1, created by A)", s == 201 and own["data"]["bookingSource"] == 1, (s, own))
own = own.get("data") or {}
s, b = call("POST", f"/bookings/{own.get('bookingId')}/confirm", custA, {"paymentMethod": "paylater"})
check("customer A's own pay later still refused (PAY_LATER_NOT_ALLOWED)", s == 422 and "PAY_LATER_NOT_ALLOWED" in errs(b), (s, b))
s, b = call("POST", ob(f"/bookings/{own.get('bookingId')}/confirm"), admin, {})
check("staff cannot confirm a hold the customer made on the customer screen (422)", s == 422 and "admin booking screen" in json.dumps(b), (s, b))
s, b = call("GET", "/bookings/pay-later", admin)
check("GET /bookings/pay-later for staff unchanged (not available)", s == 200 and b["data"]["available"] is False, b)
call("POST", f"/bookings/{own.get('bookingId')}/cancel", custA, {"reason": "e2e"})

# ---- 3. where the booking shows -----------------------------------------------------------------
s, b = call("GET", "/bookings", admin, params={"customerId": ca, "pageSize": 50})
row = next((x for x in (b.get("data") or {}).get("items", []) if x["bookingId"] == h1["bookingId"]), None)
check("Admin Bookings list: Confirmed, pay later, source 2, amount due = total",
      row and row["status"] == 2 and row["isPayLater"] and row["bookingSource"] == 2 and dec(row["totalAmount"]) - dec(row["amountPaid"]) == dec(bk["totalAmount"]), row)
s, b = call("GET", f"/bookings/{h1['bookingId']}", admin)
check("Admin booking detail: source 2 (shown as Front desk)", s == 200 and b["data"]["bookingSource"] == 2, b)
s, b = call("GET", "/bookings/my", custA, params={"tab": "upcoming", "pageSize": 50})
check("customer's My Bookings (upcoming) lists it", s == 200 and any(x["bookingId"] == h1["bookingId"] and x["isPayLater"] for x in b["data"]["items"]), b)
s, b = call("GET", "/calendar", admin, params={"view": "day", "date": DAY1.isoformat(), "locationId": LOC, "workspaceTypeId": MEETING})
items = [i for w in (b.get("data") or {}).get("workspaces", []) for i in w["items"]]
check("Calendar (day view) shows it", s == 200 and any(i["kind"].lower() == "booking" and i["referenceId"] == h1["bookingId"] for i in items), [i for i in items][:5])
s, b = call("GET", "/invoices/my", custA, params={"pageSize": 50})
check("customer's Invoices: the invoice is there and unpaid", s == 200 and any(x["invoiceId"] == INV1 and dec(x["paidAmount"]) == 0 for x in b["data"]["items"]), b)
s, b = call("GET", "/cash/outstanding", admin, params={"search": f"AdminBooking Asha {RUN}"})
check("Cash Collections > outstanding lists it as a pay-later item", s == 200 and str(h1["bookingNumber"]) in json.dumps(b), (s, json.dumps(b)[:400]))

# ---- 4. multi-day (whole working day on each date) ------------------------------------------------
orig_max = one("SELECT SettingValue FROM MST_AppSetting WHERE SettingKey='Booking.MaxMinutes'")
s, sl = call("GET", "/availability/slots", admin, params={"locationId": LOC, "workspaceTypeId": MEETING, "date": DAY2.isoformat()})
first, last = sl["data"][0]["startUtc"], sl["data"][-1]["startUtc"]
p = lambda v: dt.datetime.fromisoformat(v.replace("Z", "+00:00"))
WHOLE = int((p(last) - p(first)).total_seconds() // 60) + 30
s, b = call("POST", ob("/bookings"), admin, {"workspaceId": ROOM, "startUtc": first, "durationMinutes": WHOLE})
s2, b2 = call("POST", "/bookings", custB, {"workspaceId": ROOM, "startUtc": first, "durationMinutes": WHOLE})
check(f"with Booking.MaxMinutes={orig_max}, a whole day ({WHOLE} min) is refused the same way for staff and customer",
      int(orig_max) >= WHOLE or (s == s2 == 422 and b["message"] == b2["message"]), (s, b, s2, b2))
multi = []
try:
    if int(orig_max) < WHOLE:
        s, b = call("PUT", "/settings", admin, {"settings": [{"settingKey": "Booking.MaxMinutes", "settingValue": str(WHOLE)}]})
        check("test set-up: Booking.MaxMinutes raised for the multi-day section", s == 200, (s, b))
    for d in (DAY2, DAY3):
        s, sl = call("GET", "/availability/slots", admin, params={"locationId": LOC, "workspaceTypeId": MEETING, "date": d.isoformat()})
        start = sl["data"][0]["startUtc"]
        s, b = call("POST", ob("/bookings"), admin, {"workspaceId": ROOM, "startUtc": start, "durationMinutes": WHOLE,
                                                     "notes": f"Whole-day booking {DAY2} to {DAY3}"})
        multi.append(b.get("data") or {})
        made_bookings.append((b.get("data") or {}).get("bookingId"))
    check("multi-day: one whole-day hold per date, all for the customer, source 2",
          all(m.get("customerId") == ca and m.get("bookingSource") == 2 and m.get("durationMinutes") == WHOLE for m in multi), multi)
    done = [call("POST", ob(f"/bookings/{m['bookingId']}/confirm"), admin, {}) for m in multi if m.get("bookingId")]
    check("multi-day: every day confirmed without payment", len(done) == 2 and all(s == 200 and b["data"]["booking"]["isPayLater"] and b["data"]["paymentId"] == 0 for s, b in done), done)
    ninv = one(f"SELECT COUNT(*) FROM TRN_Invoice WHERE Status=1 AND PaidAmount=0 AND BookingId IN ({','.join(str(m['bookingId']) for m in multi)})")
    check("multi-day: one unpaid invoice per day", ninv == "2", ninv)
finally:
    if int(orig_max) < WHOLE:
        s, b = call("PUT", "/settings", admin, {"settings": [{"settingKey": "Booking.MaxMinutes", "settingValue": orig_max}]})
        check(f"test tear-down: Booking.MaxMinutes back to {orig_max}", s == 200 and one("SELECT SettingValue FROM MST_AppSetting WHERE SettingKey='Booking.MaxMinutes'") == orig_max, (s, b))

# ---- 5. monthly desk -------------------------------------------------------------------------------
s, b = call("GET", "/availability/monthly", admin, params={"locationId": LOC, "workspaceTypeId": DESK_TYPE, "startDate": DAY1.isoformat(), "months": 1})
desks = [w for w in (b.get("data") or []) if w.get("isAvailable") is not False and w.get("pricePerMonth")]
check("a desk with a monthly price is free for the term", len(desks) >= 1, b)
DESK = desks[0]["workspaceId"]
mreq = {"workspaceId": DESK, "startDate": DAY1.isoformat(), "months": 1}
s, mq = call("POST", "/bookings/monthly/quote", admin, mreq)
s, b = call("POST", ob("/bookings/monthly"), admin, mreq)
mh = b.get("data") or {}
made_bookings.append(mh.get("bookingId"))
check("monthly hold for the customer: 201, 1 month, source 2, priced as the monthly quote",
      s == 201 and mh.get("billingMonths") == 1 and mh.get("bookingSource") == 2 and dec(mh.get("totalAmount", 0)) == dec(mq["data"]["totalAmount"]), (s, b, mq))
s, b = call("POST", ob("/bookings/monthly"), admin, mreq)
check("the same desk for the same term again: 409", s == 409, (s, b))
s, b = call("POST", ob("/bookings/monthly"), admin, {**mreq, "workspaceId": ROOM})
check("a meeting room by the month refused (422 only desks and cabins)", s == 422 and "desks and cabins" in json.dumps(b), (s, b))
s, b = call("POST", ob("/bookings/monthly"), admin, {**mreq, "months": 2})
check("a 2-month term refused by the validator (400)", s == 400, (s, b))
s, b = call("POST", ob(f"/bookings/{mh.get('bookingId')}/confirm"), admin, {})
check("monthly confirmed without payment", s == 200 and b["data"]["booking"]["status"] == 2 and b["data"]["booking"]["isPayLater"] and b["data"]["paymentId"] == 0, (s, b))
minv = sql(f"SELECT Status, CAST(PaidAmount AS decimal(12,2)) FROM TRN_Invoice WHERE BookingId={mh.get('bookingId')}")
check("monthly: one invoice for the term, unpaid", len(minv) == 1 and minv[0][0] == "1" and dec(minv[0][1]) == 0, minv)

# ---- 6. Virtual Office ------------------------------------------------------------------------------
dayplan = one("SELECT TOP 1 PassPlanId FROM MST_PassPlan WHERE PassType=1 AND IsActive=1 AND IsDeleted=0")
s, b = call("POST", ob("/virtual-office"), admin, {"passPlanId": int(dayplan), "validFrom": DAY1.isoformat()})
check("a day pass cannot be bought on this screen (422)", s == 422, (s, b))
vo_plan = int(one("SELECT TOP 1 PassPlanId FROM MST_PassPlan WHERE PlanCode='MEM-VIRTUAL-1M' AND IsActive=1"))
s, b = call("POST", ob("/virtual-office"), admin, {"passPlanId": vo_plan, "validFrom": DAY1.isoformat()})
vo = (b.get("data") or {}).get("pass") or {}
made_passes.append(vo.get("customerPassId"))
check("Virtual Office reserved for the customer: AwaitingPayment (5), pay later, nothing paid",
      s == 200 and vo.get("customerId") == ca and vo.get("status") == 5 and vo.get("isPayLater") and dec(vo.get("amountPaid", 1)) == 0 and b["data"]["paymentNumber"] == "", (s, b))
vinv = sql(f"SELECT InvoiceId, Status, CAST(PaidAmount AS decimal(12,2)), ISNULL(PaymentId,0) FROM TRN_Invoice WHERE CustomerPassId={vo.get('customerPassId', 0)}")
check("Virtual Office invoice raised, unpaid, no payment linked", len(vinv) == 1 and vinv[0][1] == "1" and dec(vinv[0][2]) == 0 and vinv[0][3] == "0", vinv)
VINV = int(vinv[0][0]) if vinv else 0
cp = sql(f"SELECT ISNULL(PaymentId,0), CreatedBy FROM TRN_CustomerPass WHERE CustomerPassId={vo.get('customerPassId', 0)}")
check("pass: no payment linked, audit CreatedBy = staff", cp and cp[0][0] == "0" and int(cp[0][1]) == ADMIN_ID, cp)
note = one(f"SELECT Title FROM TRN_Notification WHERE UserId={ca} AND ReferenceType='CustomerPass' AND ReferenceId={vo.get('customerPassId', 0)}")
check("customer notified as for a pay-later pass ('Pass reserved - pay at the front desk')", note == "Pass reserved - pay at the front desk", note)
check("still no payment, receipt, cash or wallet row for the customer", money_rows(ca) == A0, (money_rows(ca), A0))

# ---- 7. coupons and offers on the customer's behalf --------------------------------------------------
s, b = call("POST", "/admin/coupons", admin, {"code": CODE, "description": "admin-booking e2e", "discountType": 2, "discountValue": 100, "maxDiscountAmount": None,
                                              "minAmount": None, "modules": [1, 3], "validFrom": today.isoformat(), "validUntil": (today + dt.timedelta(days=60)).isoformat(),
                                              "totalUses": None, "usesPerCustomer": 1, "isActive": True})
check("test coupon created (Rs 100 off bookings and memberships, once per customer)", s == 201, (s, b))
st3 = at(DAY1, 15)
s, pv = call("POST", ob("/coupons/preview"), admin, {"module": "booking", "code": CODE, "workspaceId": ROOM, "baseAmounts": [float(q["data"]["baseAmount"])]})
check("coupon preview for the customer: valid, Rs 100 off before GST",
      s == 200 and pv["data"]["valid"] and dec(pv["data"]["discountAmount"]) == 100 and dec(pv["data"]["totalAmount"]) == gst(dec(q["data"]["baseAmount"]) - 100), (s, pv))
s, b = call("POST", "/coupons/preview", admin, {"module": "booking", "code": CODE, "workspaceId": ROOM, "baseAmounts": [1000]})
check("staff still cannot use the customer coupon endpoint (403, unchanged)", s == 403, (s, b))
s, b = call("POST", ob("/offers/preview"), admin, {"module": "booking", "offerIds": [], "auto": True, "workspaceId": ROOM, "baseAmounts": [float(q["data"]["baseAmount"])]})
check("offer preview for the customer answers (automatic offers, as on the customer screen)", s == 200 and "totalAmount" in b.get("data", {}), (s, b))
s, b = call("POST", ob("/bookings"), admin, {"workspaceId": ROOM, "startUtc": st3, "durationMinutes": 60})
h3 = b.get("data") or {}
made_bookings.append(h3.get("bookingId"))
s, b = call("POST", ob("/coupons/bookings/apply"), admin, {"bookingIds": [h3.get("bookingId")], "code": CODE})
check("coupon put on the staff hold", s == 200 and dec(b["data"]["discountAmount"]) == 100, (s, b))
s, b = call("POST", ob(f"/bookings/{h3.get('bookingId')}/confirm"), admin, {})
bk3 = (b.get("data") or {}).get("booking") or {}
check("confirmed at the discounted price, still without payment", s == 200 and dec(bk3.get("totalAmount", 0)) == gst(dec(q["data"]["baseAmount"]) - 100) and b["data"]["paymentId"] == 0, (s, b))
i3 = sql(f"SELECT CAST(DiscountAmount AS decimal(12,2)), Status FROM TRN_Invoice WHERE BookingId={h3.get('bookingId')}")
check("invoice carries the Rs 100 discount, unpaid", i3 == [["100.00", "1"]], i3)
red = one(f"SELECT COUNT(*) FROM TRN_CouponRedemption WHERE CustomerId={ca} AND BookingId={h3.get('bookingId')}")
check("coupon use recorded against the customer", red == "1", red)
s, pv = call("POST", ob("/coupons/preview"), admin, {"module": "booking", "code": CODE, "workspaceId": ROOM, "baseAmounts": [1000]})
check("per-customer limit counts the staff booking: already used for A", s == 200 and pv["data"]["errorCode"] == "COUPON_ALREADY_USED", pv)
s, pv = call("POST", "/coupons/preview", custB, {"module": "booking", "code": CODE, "workspaceId": ROOM, "baseAmounts": [1000]})
check("... and still valid for customer B", s == 200 and pv["data"]["valid"], pv)
s, own2 = call("POST", "/bookings", custA, {"workspaceId": ROOM, "startUtc": at(DAY1, 17), "durationMinutes": 60})
s, b = call("POST", ob("/coupons/bookings/apply"), admin, {"bookingIds": [own2["data"]["bookingId"]], "code": CODE})
check("staff cannot put a coupon on the customer's own hold (422)", s == 422, (s, b))
s, b = call("POST", ob("/offers/bookings/apply"), admin, {"bookingIds": [own2["data"]["bookingId"]], "offerIds": []})
check("staff cannot change offers on the customer's own hold (422)", s == 422, (s, b))
call("POST", f"/bookings/{own2['data']['bookingId']}/cancel", custA, {"reason": "e2e"})

# ---- 8. the Payments screen records the money, once ------------------------------------------------
s, b = call("GET", f"/invoices/{INV1}/payment-context", admin)
check("Payments screen: payment context opens for the booking invoice", s == 200, (s, b))
total1 = dec(bk["totalAmount"])
before = money_rows(ca)
s, b = call("POST", f"/invoices/{INV1}/payments", admin, {"amount": float(total1), "paidOn": today.isoformat(), "mode": 2, "reference": f"UPI-{RUN}", "note": "admin-booking e2e"})
check("payment recorded on the Payments screen (UPI, full amount)", s == 200, (s, b))
after = money_rows(ca)
check("exactly one payment and one invoice receipt were written (none existed before)", after[0] - before[0] == 1 and after[1] - before[1] == 1 and after[2:] == before[2:], (before, after))
bkp = sql(f"SELECT CAST(AmountPaid AS decimal(12,2)), PaymentStatus FROM TRN_Booking WHERE BookingId={h1['bookingId']}")
check("booking now paid in full (AmountPaid = total, payment status Paid)", len(bkp) == 1 and dec(bkp[0][0]) == total1 and bkp[0][1] == "2", bkp)
check("invoice Paid", one(f"SELECT Status FROM TRN_Invoice WHERE InvoiceId={INV1}") == "2")
s, b = call("POST", f"/invoices/{INV1}/payments", admin, {"amount": 1, "paidOn": today.isoformat(), "mode": 2, "reference": f"UPI-{RUN}-2"})
check("a second payment on the paid invoice is refused (no duplicate)", s in (400, 409, 422), (s, b))
vtotal = dec(vo.get("totalAmount", 0))
s, b = call("POST", f"/invoices/{VINV}/payments", admin, {"amount": float(vtotal), "paidOn": today.isoformat(), "mode": 2, "reference": f"UPI-VO-{RUN}"})
check("Virtual Office paid on the Payments screen", s == 200, (s, b))
check("Virtual Office active once paid (status 1)", one(f"SELECT Status FROM TRN_CustomerPass WHERE CustomerPassId={vo.get('customerPassId', 0)}") == "1")

# ---- 9. existing behaviour unchanged --------------------------------------------------------------------
B0 = money_rows(cb)
s, h = call("POST", "/bookings", custB, {"workspaceId": ROOM, "startUtc": at(DAY1, 19), "durationMinutes": 60})
hb = h.get("data") or {}
made_bookings.append(hb.get("bookingId"))
s, b = call("POST", f"/bookings/{hb.get('bookingId')}/confirm", custB, {"paymentMethod": "wallet"})
check("customer B on the customer screen, wallet: Confirmed and Paid, source 1", s == 200 and b["data"]["booking"]["paymentStatus"] == 2 and b["data"]["booking"]["bookingSource"] == 1 and b["data"]["paymentId"] > 0, (s, b))
row = sql(f"SELECT CreatedByUserId, CreatedBy FROM TRN_Booking WHERE BookingId={hb.get('bookingId')}")[0]
check("customer booking: CreatedByUserId and audit CreatedBy are the customer", [int(x) for x in row] == [cb, cb], row)
check("customer wallet booking still writes its payment and wallet debit", money_rows(cb)[0] == B0[0] + 1 and money_rows(cb)[3] == B0[3] + 1, (B0, money_rows(cb)))
s, h = call("POST", "/bookings", custB, {"workspaceId": ROOM, "startUtc": at(DAY1, 8), "durationMinutes": 60})
hb2 = h.get("data") or {}
made_bookings.append(hb2.get("bookingId"))
s, b = call("POST", f"/bookings/{hb2.get('bookingId')}/confirm", custB, {"paymentMethod": "paylater"})
check("customer B's own pay later (allowed) unchanged: Confirmed, source 1", s == 200 and b["data"]["booking"]["isPayLater"] and b["data"]["booking"]["bookingSource"] == 1, (s, b))
hist = one(f"SELECT COUNT(*) FROM TRN_BookingStatusHistory WHERE BookingId={hb2.get('bookingId')} AND ChangedByUserId={cb}")
check("customer pay-later history written by the customer", hist == "2", hist)
free = free_ws(MEETING, at(DAY1, 13), 60)
s, b = call("POST", "/bookings/admin", admin, {"customerId": cb, "workspaceId": free[0]["workspaceId"], "startUtc": at(DAY1, 13), "durationMinutes": 60, "paymentMethod": "cash"})
check("old POST /bookings/admin (cash) still works: paid, payment row, source 2", s == 200 and b["data"]["booking"]["paymentStatus"] == 2 and b["data"]["paymentId"] > 0 and b["data"]["booking"]["bookingSource"] == 2, (s, b))
made_bookings.append(((b.get("data") or {}).get("booking") or {}).get("bookingId"))
s, b = call("POST", "/passes/purchase", custB, {"passPlanId": vo_plan, "validFrom": DAY1.isoformat(), "paymentMethod": "wallet"})
check("customer Virtual Office from the wallet unchanged: Active and paid", s == 200 and b["data"]["pass"]["status"] == 1 and b["data"]["paymentNumber"] != "", (s, b))
made_passes.append(((b.get("data") or {}).get("pass") or {}).get("customerPassId"))

# ---- clean-up ----------------------------------------------------------------------------------------
if not KEEP:
    for bid in [x for x in made_bookings if x]:
        call("POST", f"/bookings/{bid}/cancel", admin, {"reason": "admin-booking e2e clean-up"})
    for pid in [x for x in made_passes if x]:
        call("POST", f"/passes/{pid}/cancel", admin)
    purge_user_rows(f"Email LIKE 'adm-bk-%-e2e-{RUN}@example.test'")
else:
    print(f"KEPT: customers {ca_email} / {cb_email}, staff {fi_email}, password AdmBk#E2e{RUN}x (local Dev only)")

passed = sum(1 for _, ok in results if ok)
print(f"\n{passed}/{len(results)} passed")
sys.exit(0 if passed == len(results) else 1)
