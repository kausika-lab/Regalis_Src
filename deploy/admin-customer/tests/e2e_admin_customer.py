"""End-to-end API checks for the admin-customer kit (13 Nov 2026), against the LOCAL stack.

  API  http://127.0.0.1:81  (local-dev\\api with the kit DLLs, SMTP -> tests sink on :2525)
  DB   localhost\\SQLEXPRESS2025 / RegalisWorkSpace_Dev

It writes test data (bookings, a pay-later Virtual Office membership, calendar blocks,
waiting-list rows). Restore the Dev backup afterwards. Credentials come from env vars
REGALIS_ADMIN_EMAIL/PASSWORD and REGALIS_CUST_EMAIL/PASSWORD.

usage: python -I e2e_admin_customer.py [--mail-dir DIR]
"""
import datetime as dt, json, os, subprocess, sys, time, urllib.request, urllib.error, glob

API = "http://127.0.0.1:81/api"
SQL = ["sqlcmd", "-S", r"localhost\SQLEXPRESS2025", "-E", "-d", "RegalisWorkSpace_Dev", "-I", "-h", "-1", "-W", "-s", "|"]
MAIL_DIR = sys.argv[sys.argv.index("--mail-dir") + 1] if "--mail-dir" in sys.argv else None
IST = dt.timezone(dt.timedelta(hours=5, minutes=30))
results = []


def check(name, ok, detail=""):
    results.append((name, bool(ok)))
    print(("PASS " if ok else "FAIL ") + name + (f"  -- {detail}" if detail and not ok else ""), flush=True)


def sql(q):
    out = subprocess.run(SQL + ["-b", "-Q", "SET NOCOUNT ON;" + q], capture_output=True, text=True)
    if out.returncode != 0:
        raise SystemExit("SQL failed: " + q[:120] + " :: " + out.stdout + out.stderr)
    out = out.stdout
    return [l.split("|") for l in out.strip().splitlines() if l.strip()]


def call(method, path, token=None, body=None, params=None):
    url = API + path
    if params:
        url += "?" + "&".join(f"{k}={v}" for k, v in params.items() if v is not None)
    data = json.dumps(body).encode() if body is not None else None
    req = urllib.request.Request(url, data=data, method=method)
    req.add_header("Content-Type", "application/json")
    if token:
        req.add_header("Authorization", "Bearer " + token)
    try:
        with urllib.request.urlopen(req, timeout=60) as r:
            return r.status, json.loads(r.read() or b"{}")
    except urllib.error.HTTPError as e:
        try:
            return e.code, json.loads(e.read() or b"{}")
        except Exception:
            return e.code, {}


def login(email, password, portal):
    for _ in range(3):
        s, b = call("POST", "/auth/login", body={"email": email, "password": password, "portal": portal})
        if s == 200:
            d = b.get("data") or {}
            return d.get("accessToken") or (d.get("tokens") or {}).get("accessToken")
        if s == 429:
            time.sleep(20)
            continue
        raise SystemExit(f"login {email} failed: {s} {b}")


admin = login(os.environ["REGALIS_ADMIN_EMAIL"], os.environ["REGALIS_ADMIN_PASSWORD"], "admin")
cust = login(os.environ["REGALIS_CUST_EMAIL"], os.environ["REGALIS_CUST_PASSWORD"], "customer")
cust_id = int(sql(f"SELECT UserId FROM MST_User WHERE Email='{os.environ['REGALIS_CUST_EMAIL']}'")[0][0])
cust_email = os.environ["REGALIS_CUST_EMAIL"]
loc_id = int(sql("SELECT TOP 1 LocationId FROM MST_Location WHERE IsDefault=1 AND IsDeleted=0")[0][0])
today = dt.datetime.now(IST).date()

# ---- 8 My QR hidden from the customer menu ------------------------------------------
s, b = call("GET", "/menus/my", cust)
routes = json.dumps(b.get("data"))
check("8  customer menu has no /qr entry", s == 200 and '"/qr"' not in routes and '"/passes"' in routes, routes[:200])

# ---- 12 dashboard occupancy -------------------------------------------------------------
def occupancy(params=None):
    s, b = call("GET", "/dashboard/admin", admin, params=params)
    card = next(c for c in b["data"]["cards"] if c["key"] == "occupancy")
    return float(card["value"].rstrip("%")), card["hint"]

pct0, hint0 = occupancy()
check("12 occupancy card answers", 0 <= pct0 <= 100, f"{pct0} {hint0}")
pct_loc, hint_loc = occupancy({"locationId": loc_id})
check("4/12 dashboard accepts locationId and names the location", "at " in hint_loc, hint_loc)
s, _ = call("GET", "/dashboard/admin", cust)
check("12 customers cannot read the admin dashboard", s in (401, 403))

# A confirmed booking that spans several days (like a monthly desk) must count only today's open hours.
ws = sql(f"SELECT TOP 1 w.WorkspaceId FROM MST_Workspace w JOIN MST_WorkspaceType t ON t.WorkspaceTypeId=w.WorkspaceTypeId WHERE t.TypeCode='CABIN' AND w.LocationId={loc_id} AND w.IsBookable=1 AND w.IsDeleted=0")[0][0]
tmpl = sql("SELECT TOP 1 BookingId FROM TRN_Booking ORDER BY BookingId")[0][0]
cols = [r[0] for r in sql("SELECT c.name FROM sys.columns c JOIN sys.types ty ON ty.user_type_id=c.user_type_id WHERE c.object_id=OBJECT_ID('TRN_Booking') AND c.is_identity=0 AND c.is_computed=0 AND ty.name<>'timestamp' ORDER BY c.column_id")]
start = dt.datetime.combine(today - dt.timedelta(days=3), dt.time(0, 0))
end = start + dt.timedelta(days=30)
over = {"BookingNumber": "'E2E-OCC-1'", "Status": "2", "WorkspaceId": ws, "LocationId": str(loc_id),
        "StartUtc": f"'{start:%Y-%m-%d %H:%M}'", "EndUtc": f"'{end:%Y-%m-%d %H:%M}'", "DurationMinutes": str(30 * 1440),
        "BookingDate": f"'{start:%Y-%m-%d}'", "IsDeleted": "0"}
sel = ",".join(over.get(c, c) for c in cols)
sql(f"INSERT INTO TRN_Booking ({','.join(cols)}) SELECT {sel} FROM TRN_Booking WHERE BookingId={tmpl}")
pct1, hint1 = occupancy()
open_h = float(hint1.split(" of ")[1].split(" h")[0])
booked_h = float(hint1.split(" of ")[0])
wh = sql(f"SELECT DATEDIFF(minute, WorkingHoursStart, WorkingHoursEnd) FROM MST_Location WHERE LocationId={loc_id}")[0][0]
check("12 a 30-day booking adds exactly one workspace's open hours today",
      abs((booked_h - float(hint0.split(' of ')[0])) - int(wh) / 60) < 0.11, f"{hint0} -> {hint1}")
check("12 occupancy stays a sane percentage (was ~400% before the fix)", pct1 <= 100 and pct1 > pct0, f"{pct0}->{pct1}")
check("12 percentage = booked / open hours", abs(pct1 - round(booked_h / open_h * 100, 1)) <= 0.15, f"{pct1} vs {booked_h}/{open_h}")
sql("DELETE FROM TRN_Booking WHERE BookingNumber='E2E-OCC-1'")

# ---- 5 / 3 admin booking location + cancel ------------------------------------------------
day = today + dt.timedelta(days=4)
desk = sql(f"SELECT TOP 1 w.WorkspaceId FROM MST_Workspace w JOIN MST_WorkspaceType t ON t.WorkspaceTypeId=w.WorkspaceTypeId WHERE t.TypeCode='MEETING_ROOM' AND w.LocationId={loc_id} AND w.IsBookable=1 AND w.IsDeleted=0")[0][0]
start_utc = dt.datetime.combine(day, dt.time(10, 0), IST).astimezone(dt.timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ")
s, b = call("POST", "/bookings/admin", admin, {"customerId": cust_id, "workspaceId": int(desk), "startUtc": start_utc, "durationMinutes": 60, "paymentMethod": "cash"})
check("5  admin creates a booking with a location", s == 200, f"{s} {b.get('message')}")
bk = b["data"]["booking"]
check("5  booking carries its locationId", bk.get("locationId") == loc_id, bk)
s, b = call("GET", "/bookings", admin, params={"page": 1, "pageSize": 50, "locationId": loc_id})
check("4  admin booking list filters by locationId", s == 200 and any(x["bookingId"] == bk["bookingId"] for x in b["data"]["items"]))
s, b = call("GET", "/bookings", admin, params={"page": 1, "pageSize": 50, "locationId": 99999})
check("4  another location shows none of them", s == 200 and b["data"]["totalCount"] == 0)
n0 = int(sql(f"SELECT COUNT(*) FROM TRN_Notification WHERE UserId={cust_id}")[0][0])
s, b = call("POST", f"/bookings/{bk['bookingId']}/cancel", admin, {"reason": "E2E admin cancel"})
check("3  admin cancels the customer's booking", s == 200, f"{s} {b.get('message')}")
check("3  refund follows the policy (>=24 h ahead: 100% to wallet)", s == 200 and b["data"]["refundPercent"] == 100 and b["data"]["refundedToWallet"])
row = sql(f"SELECT Status, CancellationReason, CancelledByUserId FROM TRN_Booking WHERE BookingId={bk['bookingId']}")[0]
check("3  DB: status Cancelled (5), reason and admin stored", row[0] == "5" and row[1] == "E2E admin cancel" and row[2] != "NULL", row)
s, b = call("GET", f"/bookings/{bk['bookingId']}", cust)
check("3  customer sees the booking as cancelled", s == 200 and b["data"]["status"] == 5)
n1 = int(sql(f"SELECT COUNT(*) FROM TRN_Notification WHERE UserId={cust_id}")[0][0])
check("3  customer got a 'Booking cancelled' notification", n1 > n0)
s, b = call("POST", f"/bookings/{bk['bookingId']}/cancel", admin, {"reason": "again"})
check("3  a cancelled booking cannot be cancelled twice", s in (400, 409, 422))

# ---- 2 mobile with country code -------------------------------------------------------------
s, prof = call("GET", "/profile", cust)
p = prof["data"]
orig_mobile = p["mobileNumber"]
digits = orig_mobile[-10:]
body = {k: p.get(k) for k in ("fullName", "companyName", "address", "addressLine1", "addressLine2", "city", "state", "country", "postalCode", "dateOfBirth", "gender", "gstin")}
body["mobileNumber"] = "+91" + digits
s, b = call("PUT", "/profile", cust, body)
check("2  profile saves +91 form of the same number", s == 200, f"{s} {b}")
check("2  DB stores '+91' + digits", sql(f"SELECT MobileNumber FROM MST_User WHERE UserId={cust_id}")[0][0] == "+91" + digits)
other = sql(f"SELECT TOP 1 MobileNumber FROM MST_User WHERE UserId<>{cust_id} AND LEN(MobileNumber)=10 AND MobileNumber NOT LIKE '+%' AND IsDeleted=0")[0][0]
body["mobileNumber"] = "+91" + other
s, b = call("PUT", "/profile", cust, body)
check("2  +91 form of another user's old 10-digit number is refused as a duplicate", s == 409, f"{s} {b.get('message')}")
body["mobileNumber"] = "+91abc"
s, _ = call("PUT", "/profile", cust, body)
check("2  letters in the number are refused by the API", s == 400)
body["mobileNumber"] = orig_mobile
call("PUT", "/profile", cust, body)

# ---- 14 several day passes (one-pass rule off) ---------------------------------------------
s, b = call("GET", "/passes/purchase-options", cust, params={"type": 1})
check("14 every day-pass plan is buyable (no 'one pass at a time' block)", s == 200 and all(x["allowed"] for x in b["data"]["plans"]), [x.get("reason") for x in b["data"]["plans"] if not x["allowed"]])

# ---- 10 Virtual Office pay later -----------------------------------------------------------
s, b = call("GET", "/passes/pay-later", cust)
check("10 pay later offered for memberships", s == 200 and "Membership" in (b["data"].get("allowedKinds") or []), b.get("data"))
vo = int(sql("SELECT PassPlanId FROM MST_PassPlan WHERE PlanCode='MEM-VIRTUAL-1M'")[0][0])
s, b = call("POST", "/passes/purchase", cust, {"passPlanId": vo, "validFrom": str(today), "paymentMethod": "paylater"})
check("10 Virtual Office reserved with pay later", s == 200, f"{s} {b.get('message')}")
if s == 200:
    ps = b["data"]["pass"]
    r = sql(f"SELECT Status, IsPayLater, AmountPaid, TotalAmount FROM TRN_CustomerPass WHERE CustomerPassId={ps['customerPassId']}")[0]
    check("10 pass: active, IsPayLater=1, nothing paid yet", r[0] == "1" and r[1] == "1" and float(r[2]) == 0, r)
    inv = sql(f"SELECT COUNT(*) FROM TRN_Invoice WHERE CustomerId={cust_id} AND Status IN (1,2) AND PaidAmount=0 AND TotalAmount={r[3]}")
    check("10 an unpaid invoice exists for it", int(inv[0][0]) >= 1)

# ---- 11 seat waiting list -------------------------------------------------------------------
wday = today + dt.timedelta(days=6)
s, b = call("POST", "/waitlist", cust, {"waitDate": str(wday), "locationId": loc_id})
check("11 cannot join while desks are free (SEAT_AVAILABLE)", s in (400, 422) and "free" in (b.get("message") or ""), b.get("message"))
s, b = call("POST", "/waitlist", cust, {"waitDate": str(today - dt.timedelta(days=1))})
check("11 past dates are refused", s in (400, 422))
s, b = call("POST", "/waitlist", admin, {"waitDate": str(wday)})
check("11 staff cannot join (customers only)", s in (403, 401))
# Make every desk full on wday by blocking them for the whole day.
desks = [r[0] for r in sql(f"SELECT w.WorkspaceId FROM MST_Workspace w JOIN MST_WorkspaceType t ON t.WorkspaceTypeId=w.WorkspaceTypeId WHERE t.TypeCode='DESK' AND w.LocationId={loc_id} AND w.IsBookable=1 AND w.IsActive=1 AND w.IsDeleted=0")]
d0 = dt.datetime.combine(wday, dt.time(0, 0), IST).astimezone(dt.timezone.utc)
blocks = []
for w in desks:
    s, b = call("POST", "/calendar/block", admin, {"workspaceId": int(w), "startUtc": d0.strftime("%Y-%m-%dT%H:%M:%SZ"), "endUtc": (d0 + dt.timedelta(days=1)).strftime("%Y-%m-%dT%H:%M:%SZ"), "blockReason": 1, "reasonNotes": "E2E waitlist"})
    if s in (200, 201):
        blocks.append(b["data"]["workspaceBlockId"])
check("11 test setup: every desk blocked on the date", len(blocks) == len(desks), f"{len(blocks)}/{len(desks)}")
s, b = call("POST", "/waitlist", cust, {"waitDate": str(wday), "locationId": loc_id})
check("11 customer joins the waiting list for a full date", s == 200 and b["data"]["status"] == 1 and b["data"]["position"] == 1, f"{s} {b.get('message')}")
entry = b["data"]["seatWaitlistId"] if s == 200 else None
s, b2 = call("POST", "/waitlist", cust, {"waitDate": str(wday), "locationId": loc_id})
check("11 joining again returns the same entry (no duplicate)", s == 200 and b2["data"]["seatWaitlistId"] == entry)
s, b = call("GET", "/waitlist/my", cust)
check("11 'My waiting list' shows it", s == 200 and any(x["seatWaitlistId"] == entry for x in b["data"]))
s, b = call("GET", "/waitlist", admin, params={"page": 1, "pageSize": 20, "locationId": loc_id})
check("11 admin list shows it with customer details", s == 200 and any(x["seatWaitlistId"] == entry and x["customerEmail"] for x in b["data"]["items"]))
s, _ = call("GET", "/waitlist", cust, params={"page": 1})
check("11 customers cannot read the admin list", s in (401, 403))
# Free one desk; the worker (every minute) must notify.
call("POST", f"/calendar/unblock/{blocks[0]}", admin)
deadline = time.time() + 150
notified = None
while time.time() < deadline:
    r = sql(f"SELECT IsSeatFree, NotifyCount FROM TRN_SeatWaitlist WHERE SeatWaitlistId={entry}")[0]
    if r[1] != "0":
        notified = r
        break
    time.sleep(5)
check("11 worker notices the freed desk and notifies (IsSeatFree=1, NotifyCount=1)", notified == ["1", "1"], notified)
nrow = sql(f"SELECT TOP 1 Title, ActionRoute FROM TRN_Notification WHERE UserId={cust_id} AND ReferenceType='SeatWaitlist' ORDER BY NotificationId DESC")
check("11 in-app notification 'A desk is free' links to /passes?date=", nrow and nrow[0][0].startswith("A desk is free") and f"date={wday}" in nrow[0][1], nrow)
time.sleep(65)
check("11 no second notice while the desk stays free", sql(f"SELECT NotifyCount FROM TRN_SeatWaitlist WHERE SeatWaitlistId={entry}")[0][0] == "1")
s, b = call("POST", f"/waitlist/my/{entry}/remove", cust)
check("11 customer removes own entry", s == 200 and sql(f"SELECT Status FROM TRN_SeatWaitlist WHERE SeatWaitlistId={entry}")[0][0] == "3")
s, b = call("POST", f"/waitlist/my/{entry}/remove", cust)
check("11 removing twice is refused", s in (400, 422))
# Re-block, re-join, then admin removes with a reason.
s, b = call("POST", "/calendar/block", admin, {"workspaceId": int(desks[0]), "startUtc": d0.strftime("%Y-%m-%dT%H:%M:%SZ"), "endUtc": (d0 + dt.timedelta(days=1)).strftime("%Y-%m-%dT%H:%M:%SZ"), "blockReason": 1, "reasonNotes": "E2E waitlist"})
blocks[0] = b["data"]["workspaceBlockId"]
s, b = call("POST", "/waitlist", cust, {"waitDate": str(wday), "locationId": loc_id})
entry2 = b["data"]["seatWaitlistId"] if s == 200 else None
check("11 customer can join again after leaving", s == 200 and entry2 != entry)
s, _ = call("POST", f"/waitlist/{entry2}/remove", admin, {"reason": ""})
check("11 admin removal needs a reason", s in (400, 422))
s, _ = call("POST", f"/waitlist/{entry2}/remove", admin, {"reason": "E2E: desk reserved for an event"})
r = sql(f"SELECT Status, CloseReason FROM TRN_SeatWaitlist WHERE SeatWaitlistId={entry2}")[0]
check("11 admin removes with reason (status 4, reason stored)", s == 200 and r == ["4", "E2E: desk reserved for an event"], r)
nrow = sql(f"SELECT TOP 1 Title FROM TRN_Notification WHERE UserId={cust_id} AND ReferenceType='SeatWaitlist' ORDER BY NotificationId DESC")
check("11 customer told about the removal", nrow and nrow[0][0] == "Removed from the waiting list")
for bid in blocks:
    call("POST", f"/calendar/unblock/{bid}", admin)

# ---- 17 emails to the registered address ---------------------------------------------------
if MAIL_DIR:
    deadline = time.time() + 240
    want = ["Booking cancelled", "A desk is free", "Removed from the waiting list"]
    found = {}
    while time.time() < deadline and len(found) < len(want):
        for f in glob.glob(os.path.join(MAIL_DIR, "*.eml")):
            raw = open(f, "rb").read().decode("utf-8", "replace")
            head = raw.split("\r\n\r\n", 1)[0]
            for w in want:
                if w in head and w not in found:
                    found[w] = head
        time.sleep(10)
    for w in want:
        h = found.get(w, "")
        check(f"17 email '{w}' sent to the customer's registered address", cust_email.lower() in h.lower(), h[:200])

ok = sum(1 for _, k in results if k)
print(f"\n{ok}/{len(results)} passed")
sys.exit(0 if ok == len(results) else 1)
